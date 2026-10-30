using Autodesk.Revit.DB.Plumbing;
using Spk.AddIn.Modelagem;

namespace Spk.AddIn.Verificacao;

/// <summary>
///     Põe o tê nas derivações que Verificar modelo achou sem conexão: a ponta de um tubo encosta no corpo de outro, e a
///     água não passa. Para cada uma: confere que o tubo que chega aponta para o eixo do outro (na mesma altura, até 2 cm;
///     fora disso precisa de subida, e fica para o Ligar tubos); estica a ponta dele até o eixo; parte o tubo de passagem
///     ali e põe o tê (duas derivações no mesmo ponto, de lados opostos, viram cruzeta). Como a reconexão, não pode
///     desfazer ligação nenhuma que já existia: se desfizer, só aquela derivação volta atrás, com o motivo. Cada uma numa
///     transação; tudo num grupo, um Ctrl+Z.
/// </summary>
public static class Derivacoes
{
    /// <summary>Até quanto a ponta pode sair da altura (ou do lado) do eixo do outro tubo e ainda virar tê.</summary>
    static readonly double ForaDoEixo = UnitUtils.ConvertToInternalUnits(0.02, UnitTypeId.Meters);

    /// <summary>O tê precisa de corpo de tubo dos dois lados: encontro a menos disto de uma ponta fica para o projetista.</summary>
    static readonly double LongeDaPonta = UnitUtils.ConvertToInternalUnits(0.10, UnitTypeId.Meters);

    static readonly double MesmoPonto = UnitUtils.ConvertToInternalUnits(0.05, UnitTypeId.Meters);

    sealed record Caso(Pipe Ramal, XYZ Encontro, Pipe Alvo);

    /// <summary>Conexão (tê, cruzeta, luva) posta por cima de um tubo inteiro: as duas bocas da passagem sobre o eixo dele.</summary>
    sealed record PorCima(FamilyInstance Peca, Connector BocaA, Connector BocaB, Pipe Alvo)
    {
        /// <summary>Quanto a peça anda para as bocas caírem no eixo do tubo (nulo: já estão nele).</summary>
        public XYZ? Deslocar { get; init; }
    }

    /// <summary>Até quanto uma peça pode estar fora do eixo do tubo e ainda ser trazida para ele.</summary>
    static readonly double DeslocamentoMaximo = UnitUtils.ConvertToInternalUnits(0.10, UnitTypeId.Meters);

    /// <summary>
    ///     Corrige e verifica de novo, até 3 passadas, enquanto alguma derivação for corrigida: partir um tubo pode deixar
    ///     derivações que só aparecem no pedaço novo. As recusas que valem são as da última passada.
    /// </summary>
    public static ResultadoReconexao ExecutarAteParar(Document doc, IEnumerable<Ocorrencia> derivacoes)
    {
        // Todas as passadas num grupo só: um Ctrl+Z desfaz a correção inteira.
        using var grupo = new TransactionGroup(doc, "SPK — Pôr tê nas derivações");
        grupo.Start();
        var total = 0;
        var feito = Executar(doc, derivacoes);
        for (var passada = 1; ; passada++)
        {
            total += feito.Ligadas;
            if (feito.Ligadas == 0 || passada >= 3) break;
            var restantes = VerificacaoModelo.Verificar(doc).Grupos.FirstOrDefault(g => g.Codigo == "derivacao-sem-conexao");
            if (restantes is null) break;
            feito = Executar(doc, restantes.Ocorrencias);
        }
        grupo.Assimilate();
        return new ResultadoReconexao(total, feito.Recusadas);
    }

    public static ResultadoReconexao Executar(Document doc, IEnumerable<Ocorrencia> derivacoes)
    {
        var recusadas = new List<string>();
        var casos = new List<Caso>();
        var porCima = new List<PorCima>();
        foreach (var ocorrencia in derivacoes.Where(o => o.Elementos.Count == 2))
        {
            var dono = doc.GetElement(ocorrencia.Elementos[0]);
            if (doc.GetElement(ocorrencia.Elementos[1]) is not Pipe alvo || dono is null) continue;
            if (dono.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers)
            {
                recusadas.Add("bico encostado no corpo do tubo: ligue com Ligar bicos");
                continue;
            }
            if (dono is FamilyInstance peca)
            {
                // O tubo passa por dentro da peça sem ser partido: as bocas da passagem ficam no eixo dele, sem ligação. Peça
                // posta deslocada (tê mecânico sentado em cima do tubo, a uns 3 cm do eixo) anda até o eixo antes.
                var bocas = BocasNoEixo(peca, alvo);
                if (bocas.Count == 2) porCima.Add(new PorCima(peca, bocas[0], bocas[1], alvo));
                else if (Deslocada(peca, alvo) is { } desvio) porCima.Add(new PorCima(peca, desvio.A, desvio.B, alvo) { Deslocar = desvio.Ate });
                else recusadas.Add((bocas.Count == 1
                    ? "conexão com uma boca só sobre o eixo do tubo (a outra fora dele): ligue à mão"
                    : "a boca da conexão encosta no tubo de lado, fora do eixo dele: ligue à mão") + $" ({peca.Symbol?.Family?.Name})");
                continue;
            }
            if (dono is not Pipe ramal) continue;
            if (Encontro(ramal, alvo, out var motivo) is { } encontro) casos.Add(new Caso(ramal, encontro, alvo));
            else recusadas.Add(motivo!);
        }

        // Derivações no mesmo ponto do mesmo tubo (um de cada lado) viram uma cruzeta.
        var grupos = new List<List<Caso>>();
        foreach (var caso in casos)
        {
            var junto = grupos.FirstOrDefault(g => g[0].Alvo.Id == caso.Alvo.Id && g[0].Encontro.DistanceTo(caso.Encontro) < MesmoPonto);
            if (junto is null) grupos.Add([caso]);
            else junto.Add(caso);
        }

        var ligadas = 0;
        // O tubo de passagem é partido a cada derivação: os pedaços dele continuam valendo para as próximas.
        var pedacos = casos.Select(c => c.Alvo).Concat(porCima.Select(p => p.Alvo)).DistinctBy(t => t.Id)
            .ToDictionary(t => t.Id, t => new List<Pipe> { t });
        using var grupo = new TransactionGroup(doc, "SPK — Pôr tê nas derivações");
        grupo.Start();
        foreach (var junto in grupos)
        {
            var lista = pedacos[junto[0].Alvo.Id];
            var motivo = TransacaoSegura.Executar(doc, "SPK — Tê na derivação", () => PorTe(doc, junto, lista));
            if (motivo is null) ligadas += junto.Count;
            else recusadas.AddRange(junto.Select(c => Reconexao.NoGrupo(doc, [c.Ramal, .. lista]) ?? motivo));
        }
        foreach (var caso in porCima)
        {
            var lista = pedacos[caso.Alvo.Id];
            var motivo = TransacaoSegura.Executar(doc, "SPK — Encaixar conexão no tubo", () => Encaixar(doc, caso, lista));
            if (motivo is null) ligadas++;
            else recusadas.Add($"{Reconexao.NoGrupo(doc, [caso.Peca, .. lista]) ?? motivo} ({caso.Peca.Symbol?.Family?.Name})");
        }
        grupo.Assimilate();
        return new ResultadoReconexao(ligadas, recusadas);
    }

    /// <summary>
    /// Peça com duas bocas livres na direção do tubo, fora do eixo dele pelo mesmo desvio (até 10 cm, de lado): o tê
    /// mecânico posto em cima do tubo. Devolve as bocas e o deslocamento que as leva ao eixo.
    /// </summary>
    static (Connector A, Connector B, XYZ Ate)? Deslocada(FamilyInstance peca, Pipe tubo)
    {
        if (tubo.Location is not LocationCurve { Curve: Line l } || peca.MEPModel?.ConnectorManager is not { } gerente) return null;
        var paralelas = gerente.Connectors.Cast<Connector>()
            .Where(c => c.ConnectorType == ConnectorType.End && c.Domain == Domain.DomainPiping && !c.IsConnected)
            .Where(c => Math.Abs(c.CoordinateSystem.BasisZ.DotProduct(l.Direction)) > 0.99)
            .ToList();
        if (paralelas.Count != 2) return null;
        XYZ Desvio(Connector c) => Projetar(l, c.Origin) - c.Origin;
        var (da, db) = (Desvio(paralelas[0]), Desvio(paralelas[1]));
        if (da.GetLength() > DeslocamentoMaximo || da.DistanceTo(db) > UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Millimeters)) return null;
        var t0 = (paralelas[0].Origin - l.GetEndPoint(0)).DotProduct(l.Direction);
        var t1 = (paralelas[1].Origin - l.GetEndPoint(0)).DotProduct(l.Direction);
        if (Math.Min(t0, t1) <= 0 || Math.Max(t0, t1) >= l.Length) return null;
        return (paralelas[0], paralelas[1], (da + db) / 2);
    }

    /// <summary>As bocas livres da peça que estão sobre o eixo do tubo (até 5 mm) e na direção dele.</summary>
    static List<Connector> BocasNoEixo(FamilyInstance peca, Pipe tubo)
    {
        if (tubo.Location is not LocationCurve { Curve: Line l } || peca.MEPModel?.ConnectorManager is not { } gerente) return [];
        var tolerancia = UnitUtils.ConvertToInternalUnits(5, UnitTypeId.Millimeters);
        return gerente.Connectors.Cast<Connector>()
            .Where(c => c.ConnectorType == ConnectorType.End && c.Domain == Domain.DomainPiping && !c.IsConnected)
            .Where(c => Math.Abs(c.CoordinateSystem.BasisZ.DotProduct(l.Direction)) > 0.99)
            .Where(c =>
            {
                var t = (c.Origin - l.GetEndPoint(0)).DotProduct(l.Direction);
                return t > 0 && t < l.Length && (l.GetEndPoint(0) + l.Direction * t).DistanceTo(c.Origin) < tolerancia;
            })
            .ToList();
    }

    /// <summary>
    ///     Encaixa no tubo a conexão que foi posta por cima dele: parte o tubo nas duas bocas, apaga o pedaço que ficou
    ///     dentro da peça e liga as pontas às bocas. O tubo e a boca precisam ter o mesmo diâmetro (senão falta a redução).
    /// </summary>
    static string? Encaixar(Document doc, PorCima caso, List<Pipe> pedacos)
    {
        if (caso.Deslocar is { } deslocar)
        {
            // A peça anda até o eixo levando o que está pendurado nela (bucha, bico) até o primeiro tubo, a descida, que
            // estica ou encolhe esses cm. Mover a peça sozinha não adianta: o Revit a devolve para onde os vizinhos mandam.
            var (cadeia, descidas) = Pendurados(caso.Peca);
            if (cadeia is null) return "a conexão está presa a peças demais para trazer até o eixo do tubo: ligue à mão";
            if (descidas.Count > 1 || descidas.Any(d => d.Tubo.Location is not LocationCurve { Curve: Line l } || Math.Abs(l.Direction.DotProduct(deslocar.Normalize())) < 0.99))
                return "a conexão está presa a um tubo que não anda junto com ela: ligue à mão";
            var todos = cadeia.Cast<Element>().Concat(descidas.Select(d => d.Tubo)).ToList();
            int Ligacoes() => todos.Where(v => v.IsValidObject).SelectMany(Conectores).Count(c => c.IsConnected);
            var ligadas = Ligacoes();
            // A descida é solta da cadeia, a cadeia anda e a ponta da descida vai até a boca no lugar novo.
            // Peça fixada (alfinete, ou presa a um grupo de modelo) não anda: o Revit ignora o mover sem dar erro.
            if (cadeia.Any(e => e.Pinned)) return "a conexão (ou o bico pendurado nela) está fixada: solte o alfinete e corrija de novo";
            foreach (var (_, ponta, boca, _) in descidas) ponta.DisconnectFrom(boca);
            ElementTransformUtils.MoveElements(doc, cadeia.Select(e => e.Id).ToList(), deslocar);
            foreach (var (tubo, ponta, _, _) in descidas)
            {
                var linhaDescida = (Line)((LocationCurve)tubo.Location).Curve;
                var inicio = linhaDescida.GetEndPoint(0).DistanceTo(ponta.Origin) < linhaDescida.GetEndPoint(1).DistanceTo(ponta.Origin);
                var nova = ponta.Origin + deslocar;
                ((LocationCurve)tubo.Location).Curve = inicio ? Line.CreateBound(nova, linhaDescida.GetEndPoint(1)) : Line.CreateBound(linhaDescida.GetEndPoint(0), nova);
            }
            doc.Regenerate();
            foreach (var (tubo, _, boca, onde) in descidas)
            {
                // O conector lido antes de mover guarda a posição antiga: a boca é buscada de novo na peça.
                var aqui = onde + deslocar;
                var bocaNova = Conectores(boca.Owner).Where(c => !c.IsConnected).OrderBy(c => c.Origin.DistanceTo(aqui)).FirstOrDefault();
                var ponta = Pontas(tubo).Where(c => !c.IsConnected).OrderBy(c => c.Origin.DistanceTo(aqui)).FirstOrDefault();
                var folga = UnitUtils.ConvertToInternalUnits(2, UnitTypeId.Millimeters);
                if (bocaNova is null || ponta is null || ponta.Origin.DistanceTo(bocaNova.Origin) > folga)
                    return "a descida não acompanhou a conexão: ligue à mão";
                ponta.ConnectTo(bocaNova);
            }
            doc.Regenerate();
            if (Ligacoes() != ligadas) return "trazer a conexão para o eixo do tubo desligaria a peça ligada nela";
        }
        // Depois de mover, os conectores lidos antes ainda guardam a posição antiga: as bocas são buscadas de novo na peça.
        var (bocaA, bocaB) = (caso.BocaA, caso.BocaB);
        if (caso.Deslocar is not null && caso.Alvo.Location is LocationCurve { Curve: Line eixo })
        {
            // As duas bocas livres na direção do tubo são as da passagem (a terceira, a saída, está ligada à descida).
            var livres = Conectores(caso.Peca).Where(c => !c.IsConnected && Math.Abs(c.CoordinateSystem.BasisZ.DotProduct(eixo.Direction)) > 0.99).ToList();
            if (livres.Count != 2) return "a conexão mudou depois de trazida para o eixo";
            (bocaA, bocaB) = (livres[0], livres[1]);
        }
        var (a, b) = (bocaA.Origin, bocaB.Origin);
        var alvo = pedacos.Where(t => t.IsValidObject).FirstOrDefault(t => Dentro(t, a) && Dentro(t, b));
        if (alvo is null)
            return caso.Deslocar is null ? "o tubo de passagem mudou: ligue à mão" : "a conexão não chegou ao eixo do tubo: ligue à mão";
        var milimetro = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters);
        if (Math.Abs(bocaA.Radius - alvo.Diameter / 2) > milimetro || Math.Abs(bocaB.Radius - alvo.Diameter / 2) > milimetro)
            return "a conexão e o tubo têm diâmetros diferentes: troque o tamanho da conexão ou ponha a redução";

        var vizinhos = new List<Element> { alvo, caso.Peca };
        var antes = vizinhos.SelectMany(Conectores).Where(c => c.IsConnected).Select(c => c.Origin).ToList();
        var linha = (Line)((LocationCurve)alvo.Location).Curve;

        // Peça de comprimento zero (luva ou união com as duas bocas no mesmo ponto): parte o tubo ali uma vez e liga cada
        // boca ao pedaço do lado para onde ela aponta.
        if (a.DistanceTo(b) < UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Centimeters))
        {
            if (doc.GetElement(PlumbingUtils.BreakCurve(doc, alvo.Id, Projetar(linha, a))) is not Pipe outro) return "o Revit não partiu o tubo";
            doc.Regenerate();
            pedacos.Add(outro);
            // Uma boca para cada pedaço: a boca A vai para o lado para onde ela aponta, a B para o outro. A boca pode estar
            // até 5 mm fora do eixo (o tubo é partido na projeção dela).
            double Aponta(Pipe t, Connector boca) =>
                (((Line)((LocationCurve)t.Location).Curve).Evaluate(0.5, true) - boca.Origin).DotProduct(boca.CoordinateSystem.BasisZ);
            var ladoA = Aponta(alvo, bocaA) >= Aponta(outro, bocaA) ? alvo : outro;
            foreach (var (boca, lado) in new[] { (bocaA, ladoA), (bocaB, ladoA == alvo ? outro : alvo) })
            {
                var ponta = Pontas(lado).Where(c => !c.IsConnected).OrderBy(c => c.Origin.DistanceTo(boca.Origin)).FirstOrDefault();
                if (ponta is null || ponta.Origin.DistanceTo(boca.Origin) > milimetro * 6) return "a ponta do tubo não ficou na boca da conexão";
                ponta.ConnectTo(boca);
            }
            doc.Regenerate();
            var depois = new Element[] { alvo, outro, caso.Peca }.SelectMany(Conectores).ToList();
            foreach (var origem in antes)
                if (!depois.Any(c => c.IsConnected && c.Origin.DistanceTo(origem) < milimetro))
                    return "encaixar desligaria uma peça vizinha: ligue à mão";
            return null;
        }

        // Parte o tubo em A e depois em B (no pedaço que ainda contém B): sobram três pedaços.
        double Ao(XYZ p) => (p - linha.GetEndPoint(0)).DotProduct(linha.Direction);
        var (x, y) = Ao(a) < Ao(b) ? (a, b) : (b, a);
        var novos = new List<Pipe> { alvo };
        if (doc.GetElement(PlumbingUtils.BreakCurve(doc, alvo.Id, Projetar(linha, x))) is Pipe p1) novos.Add(p1);
        else return "o Revit não partiu o tubo";
        doc.Regenerate();
        var comY = novos.FirstOrDefault(t => Dentro(t, y));
        if (comY is null) return "o tubo não ficou com a segunda boca da conexão";
        if (doc.GetElement(PlumbingUtils.BreakCurve(doc, comY.Id, Projetar(linha, y))) is Pipe p2) novos.Add(p2);
        else return "o Revit não partiu o tubo";
        doc.Regenerate();

        // O pedaço de dentro da peça é o que tem uma ponta em cada boca.
        var dentro = novos.FirstOrDefault(t => Pontas(t).Count(c => c.Origin.DistanceTo(x) < milimetro * 2 || c.Origin.DistanceTo(y) < milimetro * 2) == 2);
        if (dentro is null) return "não achou o pedaço de tubo dentro da peça";
        doc.Delete(dentro.Id);
        doc.Regenerate();
        var fora = novos.Where(t => t.IsValidObject).ToList();
        pedacos.AddRange(fora.Where(t => !pedacos.Contains(t)));

        foreach (var boca in new[] { bocaA, bocaB })
        {
            var ponta = fora.SelectMany(Pontas).Where(c => !c.IsConnected).OrderBy(c => c.Origin.DistanceTo(boca.Origin)).FirstOrDefault();
            if (ponta is null || ponta.Origin.DistanceTo(boca.Origin) > milimetro * 6) return "a ponta do tubo não ficou na boca da conexão";
            ponta.ConnectTo(boca);
        }
        doc.Regenerate();

        var agora = fora.Cast<Element>().Append(caso.Peca).SelectMany(Conectores).ToList();
        foreach (var origem in antes)
            if (!agora.Any(c => c.IsConnected && c.Origin.DistanceTo(origem) < milimetro))
                return "encaixar desligaria uma peça vizinha: ligue à mão";
        return null;
    }

    /// <summary>
    ///     A peça e as peças penduradas nela (bucha, bico...) até o primeiro tubo de cada lado, e esses tubos com a ponta
    ///     ligada na cadeia, a boca da cadeia e onde ela está. Nula se a cadeia passar de quatro peças (é rede, não um bico).
    /// </summary>
    static (List<FamilyInstance>? Cadeia, List<(Pipe Tubo, Connector Ponta, Connector Boca, XYZ Onde)> Descidas) Pendurados(FamilyInstance peca)
    {
        var cadeia = new List<FamilyInstance> { peca };
        var descidas = new List<(Pipe, Connector, Connector, XYZ)>();
        for (var i = 0; i < cadeia.Count; i++)
        {
            foreach (var boca in Conectores(cadeia[i]).Where(c => c.IsConnected))
                foreach (var outro in boca.AllRefs.Cast<Connector>().Where(r => r.ConnectorType == ConnectorType.End && r.Owner.Id != cadeia[i].Id))
                {
                    if (outro.Owner is Pipe tubo)
                    {
                        if (!descidas.Any(d => d.Item1.Id == tubo.Id)) descidas.Add((tubo, outro, boca, boca.Origin));
                    }
                    else if (outro.Owner is FamilyInstance fi && cadeia.All(c => c.Id != fi.Id))
                    {
                        if (cadeia.Count == 4) return (null, descidas);
                        cadeia.Add(fi);
                    }
                }
        }
        return (cadeia, descidas);
    }

    static XYZ Projetar(Line l, XYZ p) => l.GetEndPoint(0) + l.Direction * (p - l.GetEndPoint(0)).DotProduct(l.Direction);

    /// <summary>O ponto está no tubo, sobre o eixo (até 5 mm) e a mais de 1 cm das pontas.</summary>
    static bool Dentro(Pipe tubo, XYZ p)
    {
        if (tubo.Location is not LocationCurve { Curve: Line l }) return false;
        var t = (p - l.GetEndPoint(0)).DotProduct(l.Direction);
        var centimetro = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Centimeters);
        return t > centimetro && t < l.Length - centimetro && Projetar(l, p).DistanceTo(p) < centimetro / 2;
    }

    static IEnumerable<Connector> Conectores(Element e) => e switch
    {
        Pipe t => Pontas(t),
        FamilyInstance { MEPModel.ConnectorManager: { } cm } => cm.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End),
        _ => [],
    };

    /// <summary>
    ///     O ponto do eixo do tubo de passagem onde a derivação entra: o cruzamento das duas retas. Nulo, com o motivo, se o
    ///     tubo corre ao longo do outro, não chega na altura do eixo dele ou chega perto demais de uma ponta.
    /// </summary>
    static XYZ? Encontro(Pipe ramal, Pipe alvo, out string? motivo)
    {
        motivo = null;
        if (ramal.Location is not LocationCurve { Curve: Line r } || alvo.Location is not LocationCurve { Curve: Line a })
        {
            motivo = "tubo curvo: ligue à mão";
            return null;
        }
        var (p, d) = (r.GetEndPoint(0), r.Direction);
        var (q, e) = (a.GetEndPoint(0), a.Direction);
        var cos = d.DotProduct(e);
        if (Math.Abs(cos) > Math.Cos(20 * Math.PI / 180))
        {
            motivo = "o tubo corre quase paralelo ao outro: não é derivação, alinhe à mão";
            return null;
        }
        // Pontos mais próximos das duas retas.
        var w = p - q;
        var (b, dw, ew) = (cos, d.DotProduct(w), e.DotProduct(w));
        var denominador = 1 - b * b;
        var s = (b * ew - dw) / denominador;
        var t = (ew - b * dw) / denominador;
        var noRamal = p + d * s;
        var noAlvo = q + e * t;
        var fora = noRamal.DistanceTo(noAlvo);
        if (fora > ForaDoEixo)
        {
            motivo = $"a ponta passa a {UnitUtils.ConvertFromInternalUnits(fora, UnitTypeId.Centimeters).ToString("0", Entrada.Cultura)} cm " +
                     "do eixo do outro tubo (acima, abaixo ou de lado): precisa de subida, use Ligar tubos";
            return null;
        }
        if (t < LongeDaPonta || t > a.Length - LongeDaPonta)
        {
            motivo = "a derivação chega perto demais da ponta do outro tubo para caber o tê: ligue à mão";
            return null;
        }
        return noAlvo;
    }

    /// <summary>Estica a ponta de cada tubo que chega até o eixo, parte o tubo de passagem e põe o tê (ou a cruzeta).</summary>
    static string? PorTe(Document doc, List<Caso> casos, List<Pipe> pedacos)
    {
        var x = casos[0].Encontro;
        // O pedaço do tubo de passagem que contém o ponto (ele pode ter sido partido por uma derivação anterior).
        var alvo = pedacos.Where(t => t.IsValidObject).FirstOrDefault(t => Contem(t, x));
        if (alvo is null) return "o tubo de passagem mudou: ligue à mão";

        // As ligações que existiam em volta não podem sumir: o retrato guarda onde havia conector ligado.
        var vizinhos = new[] { alvo }.Concat(casos.Select(c => c.Ramal)).ToList();
        var antes = vizinhos.SelectMany(Pontas).Where(c => c.IsConnected).Select(c => c.Origin).ToList();

        foreach (var caso in casos)
        {
            var ponta = Pontas(caso.Ramal).Where(c => !c.IsConnected).OrderBy(c => c.Origin.DistanceTo(x)).FirstOrDefault();
            if (ponta is null) return "a ponta do tubo que chega já está ligada";
            if (ponta.Origin.DistanceTo(x) < UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters)) continue;
            // Mantém a ordem das pontas (invertida, o Revit não muda o tubo) e a outra ponta no lugar.
            var linha = (Line)((LocationCurve)caso.Ramal.Location).Curve;
            var inicio = linha.GetEndPoint(0).DistanceTo(ponta.Origin) < linha.GetEndPoint(1).DistanceTo(ponta.Origin);
            ((LocationCurve)caso.Ramal.Location).Curve = inicio ? Line.CreateBound(x, linha.GetEndPoint(1)) : Line.CreateBound(linha.GetEndPoint(0), x);
        }
        doc.Regenerate();

        if (doc.GetElement(PlumbingUtils.BreakCurve(doc, alvo.Id, x)) is Pipe outro) pedacos.Add(outro);
        else return "o Revit não partiu o tubo de passagem";
        doc.Regenerate();

        var nivel = alvo.ReferenceLevel ?? GeradorMalha.Nivel(doc);
        if (nivel is null) return "o tubo não tem nível";
        var tubos = new List<Pipe?> { alvo, pedacos[^1] };
        tubos.AddRange(casos.Select(c => c.Ramal));
        if (TracarRamal.Juntar(doc, x, nivel, tubos) is { } recusa) return recusa;
        doc.Regenerate();

        // Nenhuma ligação de antes pode ter sumido (o Revit às vezes arrasta o vizinho ao mexer na ponta de um tubo).
        var agora = vizinhos.Append(pedacos[^1]).Where(t => t.IsValidObject).SelectMany(Pontas).ToList();
        foreach (var origem in antes)
            if (!agora.Any(c => c.IsConnected && c.Origin.DistanceTo(origem) < UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters)))
                return "pôr o tê desligaria uma peça vizinha: ligue à mão";
        return null;
    }

    /// <summary>O ponto cai no corpo do tubo, longe das pontas.</summary>
    static bool Contem(Pipe tubo, XYZ x)
    {
        if (tubo.Location is not LocationCurve { Curve: Line l }) return false;
        var t = (x - l.GetEndPoint(0)).DotProduct(l.Direction);
        var projecao = l.GetEndPoint(0) + l.Direction * t;
        return projecao.DistanceTo(x) < MesmoPonto && t > LongeDaPonta && t < l.Length - LongeDaPonta;
    }

    static IEnumerable<Connector> Pontas(Pipe t) =>
        t.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End);
}
