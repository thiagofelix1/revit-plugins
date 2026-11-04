using Autodesk.Revit.DB.Plumbing;
using Spk.Motor.Calculo;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.AddIn.Modelagem;

/// <summary>Como gerar a rede num ambiente: o arranjo, o drop do bico até o ramal, os diâmetros e as opções dos bicos.</summary>
public sealed record OpcoesRede
{
    /// <summary>Espinha de peixe (subgeral no meio) ou lateral (subgeral junto à parede).</summary>
    public Arranjo Arranjo { get; init; } = Arranjo.EspinhaDePeixe;

    /// <summary>Do defletor até o ramal, em m.</summary>
    public double DropM { get; init; } = 0.3;

    /// <summary>Nome do tipo de tubo da rede, escolhido pelo projetista. Nulo: a última escolha ou a sugestão.</summary>
    public string? TipoDeTubo { get; init; }

    public bool AplicarDiametros { get; init; } = true;
    public OpcoesBicos Bicos { get; init; } = new();

    /// <summary>
    /// Ambiente por onde passa a geral (o Id dele, ou parte do nome): as salas entram num setor só, com a geral pelo eixo
    /// dele. Nulo: cada sala com a sua entrada (a descida até o piso).
    /// </summary>
    public string? GeralPor { get; init; }

    /// <summary>Com geral: põe os bicos do corredor pela norma e liga cada um direto na geral (tê e descida).</summary>
    public bool BicosNoCorredor { get; init; } = true;

    /// <summary>
    /// Com geral: onde fica a descida da VGA, em planta (m). Nulo: na ponta do corredor (num L, a longe do canto). Com um
    /// ponto, a descida vai para o ponto do eixo do corredor mais perto dele e a geral sai dela para os dois lados.
    /// </summary>
    public Ponto2? EntradaDaGeral { get; init; }
}

/// <summary>A geral gerada pelo corredor e a ligação das salas a ela, ou por que não foi feita.</summary>
public sealed record GeralGerada(Ambiente Corredor, string? Recusa, ResultadoGeral? Ligacao, ResultadoDiametros? Diametros, IReadOnlyList<string> Avisos)
{
    public string Texto()
    {
        var nome = $"{Corredor.Numero} {Corredor.Nome}".Trim();
        if (Recusa is not null) return $"Geral por {nome}: {Recusa}";
        var linhas = new List<string> { $"Geral por {nome}: " + (Ligacao?.Texto().Replace("\n", "\n   ") ?? "") };
        if (Diametros is { } d)
            linhas.Add("   Diâmetros do setor: " + string.Join(", ", d.TubosPorDn.OrderBy(p => p.Key).Select(p => $"DN {p.Key}: {p.Value}")));
        linhas.AddRange(Avisos.Select(a => "   • " + a));
        return string.Join("\n", linhas);
    }
}

/// <summary>A rede de um ambiente: bicos, ramais, diâmetros, ou por que não foi gerada.</summary>
public sealed record RedeNoAmbiente(Ambiente Ambiente, string? Recusa, int Bicos, double? AlturaDaRede, ResultadoRamais? Ramais,
    ResultadoDiametros? Diametros, IReadOnlyList<string> Avisos)
{
    /// <summary>Altura da subgeral (a entrada da sala), quando não é a dos ramais: por baixo das vigas fundas.</summary>
    public double? AlturaDaSubgeral { get; init; }

    public string Texto()
    {
        var nome = $"{Ambiente.Numero} {Ambiente.Nome}".Trim();
        if (Recusa is not null) return $"{nome}: {Recusa}";
        var linhas = new List<string>
        {
            $"{nome}: {Bicos} bicos, rede a {AlturaDaRede:0.00} m, {Ramais?.Tracados ?? 0} ramal(is), {Ramais?.Ligados ?? 0} bico(s) ligado(s)" +
            (Diametros is { } d ? "; " + string.Join(", ", d.TubosPorDn.OrderBy(p => p.Key).Select(p => $"DN {p.Key}: {p.Value}")) : ""),
        };
        linhas.AddRange((Ramais?.Recusados ?? []).GroupBy(r => r).Select(g => $"   • {g.Key}{(g.Count() > 1 ? $" ({g.Count()})" : "")}"));
        linhas.AddRange(Avisos.Select(a => "   • " + a));
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Gera a rede de cada ambiente com as ferramentas de modelagem, na ordem em que o engenheiro faria: bicos pela norma
///     (<see cref="BicosPorAmbiente" />), a subgeral cruzando as fileiras (no meio, na espinha de peixe; junto à parede, no
///     lateral) com a descida até o piso na entrada, os ramais e a ligação dos bicos (<see cref="TracarRamal" />) e os
///     diâmetros pela tabela (<see cref="AplicarDiametros" />). A rede fica a <see cref="OpcoesRede.DropM" /> acima do
///     defletor, sem passar da laje ou do telhado. Cada ambiente num grupo: se uma etapa recusa, o ambiente volta inteiro.
///     Ambientes de paredes retas; num ambiente em L a subgeral pode sair dele, e isso vira aviso.
/// </summary>
static class GerarRede
{
    static readonly double FolgaDoTeto = 0.10;
    static readonly double DropMinimo = 0.15;
    const double DropComDnDoEsboco = 0.25;
    const int DnNaDescidaCurta = 25;

    public static IReadOnlyList<RedeNoAmbiente> Executar(Document doc, DadosProjeto dados, Edificio edificio, IEnumerable<Ambiente> ambientes,
        OpcoesRede opcoes, List<ElementId> criados) =>
        Executar(doc, dados, edificio, ambientes, opcoes, criados, null);

    /// <summary>Reta em planta (m): de A até B, na direção unitária A→B.</summary>
    internal sealed record Eixo(Ponto2 A, Ponto2 B)
    {
        public Ponto2 Direcao
        {
            get
            {
                var (dx, dy) = (B.X - A.X, B.Y - A.Y);
                var c = Math.Sqrt(dx * dx + dy * dy);
                return new Ponto2(dx / c, dy / c);
            }
        }
    }

    /// <summary>
    /// As salas num setor só: a geral pelo eixo do ambiente pedido (o trecho reto mais comprido que cabe nele, longe das
    /// paredes), com a descida da entrada na ponta A; em cada sala, os ramais paralelos à geral e a entrada na parede do
    /// lado dela; depois Ligar à geral e os diâmetros do setor inteiro. O corredor fica sem bicos (vira aviso).
    /// </summary>
    public static (IReadOnlyList<RedeNoAmbiente> Redes, GeralGerada? Geral) ExecutarComGeral(Document doc, DadosProjeto dados, Edificio edificio,
        IEnumerable<Ambiente> ambientes, OpcoesRede opcoes, List<ElementId> criados)
    {
        var corredor = opcoes.GeralPor is not { } alvo ? null
            : edificio.Ambientes.FirstOrDefault(a => a.Id == alvo)
              ?? edificio.Ambientes.FirstOrDefault(a => a.Nome.Contains(alvo, StringComparison.CurrentCultureIgnoreCase));
        var salas = ambientes.Where(a => a.Id != corredor?.Id).ToList();
        if (corredor is null) return (Executar(doc, dados, edificio, salas, opcoes, criados, null), null);
        GeralGerada Recusa(string motivo) => new(corredor, motivo, null, null, []);

        var avisos = new List<string>();
        var caminho = CaminhoDoCorredor(corredor, avisos);
        if (caminho.Count == 0)
            return (Executar(doc, dados, edificio, salas, opcoes, criados, null), Recusa("o ambiente é estreito demais para a geral (menos de 60 cm entre as paredes)"));
        // A geral em ramos que saem da descida da VGA: um só com a descida na ponta; dois com ela no meio do corredor.
        var ramos = RamosDaEntrada(caminho, opcoes.EntradaDaGeral);
        var trechos = ramos.SelectMany(r => r).ToList();

        // Cada sala fica de frente para um trecho da geral: os ramais dela correm paralelos a ele, a entrada do lado dele.
        var redes = Executar(doc, dados, edificio, salas, opcoes, criados, sala => TrechoDaSala(sala, trechos));
        var feitas = redes.Where(r => r.Recusa is null && r.AlturaDaRede is not null).ToList();
        if (feitas.Count == 0) return (redes, Recusa("nenhuma sala ganhou rede"));

        // Bicos do corredor, pela norma como numa sala, em fileira ao longo da geral: cada um liga direto nela. Sem forro, o
        // defletor desce até 30 cm da laje (o máximo sob teto liso), para a geral caber acima dele com o tê e a descida.
        var bicosDoCorredor = new List<ElementId>();
        double? defletorDoCorredor = null;
        if (opcoes.BicosNoCorredor)
        {
            var opcoesCorredor = opcoes.Bicos with { Rumo = trechos[0].Direcao };
            if (corredor.OrigemDoTeto is not OrigemDoTeto.Forro)
                opcoesCorredor = opcoesCorredor with { DefletorM = Math.Max(opcoesCorredor.DefletorM, 0.30) };
            using var grupoBicos = new TransactionGroup(doc, "SPK — Bicos do corredor");
            grupoBicos.Start();
            var posicionados = BicosPorAmbiente.Posicionar(doc, edificio, [corredor], BicosPorAmbiente.Limites(dados, opcoes.Bicos), opcoesCorredor, bicosDoCorredor).Single();
            if (posicionados.Recusa is null && posicionados.Defletor is { } defletor && bicosDoCorredor.Count > 0)
            {
                grupoBicos.Assimilate();
                defletorDoCorredor = defletor;
            }
            else
            {
                grupoBicos.RollBack();
                bicosDoCorredor.Clear();
                avisos.Add("o corredor ficou sem bicos (" + (posicionados.Recusa ?? "nenhum bico coube") + "): ponha com Bicos por ambiente e ligue à geral com Ligar bicos");
            }
        }
        else
        {
            avisos.Add("o corredor ficou sem bicos: ponha com Bicos por ambiente e ligue à geral com Ligar bicos ou Traçar ramais");
        }

        // Altura: a das redes das salas (a mais alta), acima dos bicos do corredor pelo drop, sem encostar na laje do corredor.
        var z = feitas.Max(r => r.AlturaDaSubgeral ?? r.AlturaDaRede!.Value);
        if (defletorDoCorredor is { } dc && z < dc + opcoes.DropM) z = dc + opcoes.DropM;
        if (corredor.OrigemDoTeto is OrigemDoTeto.Laje or OrigemDoTeto.Telhado && corredor.Teto is { } teto && z > teto - FolgaDoTeto)
        {
            z = teto - FolgaDoTeto;
            avisos.Add($"geral a {z:0.00} m, 10 cm abaixo do teto do corredor");
        }
        if (defletorDoCorredor is { } dc2 && z - dc2 < DropMinimo - 1e-6)
        {
            TransacaoSegura.Executar(doc, "SPK — Tirar bicos do corredor", () =>
            {
                doc.Delete(bicosDoCorredor);
                return null;
            });
            bicosDoCorredor.Clear();
            avisos.Add($"o corredor ficou sem bicos: não cabe a geral 15 cm acima do defletor ({dc2:0.00}) e 10 cm abaixo do teto");
        }
        criados.AddRange(bicosDoCorredor);
        var pontosDosBicos = bicosDoCorredor.Select(doc.GetElement).OfType<FamilyInstance>()
            .Select(b => ((LocationPoint)b.Location).Point).Select(p => new Ponto2(Metros(p.X), Metros(p.Y))).ToList();

        var dasSalas = feitas.Select(r => criados.Select(doc.GetElement).OfType<FamilyInstance>()
                .FirstOrDefault(b => b.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers && Dentro(r.Ambiente, b)))
            .OfType<FamilyInstance>().Select(b => LigarAGeral.Rede(doc, b)).ToList();

        // Cada ramo da geral vai da descida até 50 cm depois da entrada da última sala dele, e não até o fim do corredor:
        // depois do último tê não passa água. Trecho da ponta sem sala nenhuma sai; o que sobra é encurtado; ramo sem sala
        // nenhuma sai inteiro. Os bicos do corredor também seguram a geral: ela vai até 50 cm depois do último deles.
        var entradas = dasSalas.Select(r => LigarAGeral.Entrada(doc, r)).OfType<LigarAGeral.EntradaDaRede>()
            .Select(e => new Ponto2(Metros(e.Ponto.X), Metros(e.Ponto.Y))).Concat(pontosDosBicos).ToList();
        var descidaEm = ramos[0][0].A;
        foreach (var ramo in ramos)
            while (ramo.Count > 0)
            {
                var ultimo = ramo[^1];
                var d = ultimo.Direcao;
                double Ao(Ponto2 p) => (p.X - ultimo.A.X) * d.X + (p.Y - ultimo.A.Y) * d.Y;
                var comprimento = Ao(ultimo.B);
                var todos = ramos.SelectMany(r => r).ToList();
                var nele = entradas.Where(p => ReferenceEquals(TrechoDaEntrada(p, todos), ultimo)).Select(Ao).ToList();
                if (nele.Count == 0)
                {
                    ramo.RemoveAt(ramo.Count - 1);
                    continue;
                }
                if (nele.Max() + 0.5 < comprimento)
                {
                    comprimento = Math.Max(nele.Max() + 0.5, 0.5);
                    ramo[^1] = ultimo with { B = new Ponto2(ultimo.A.X + d.X * comprimento, ultimo.A.Y + d.Y * comprimento) };
                }
                break;
            }
        ramos.RemoveAll(r => r.Count == 0);
        if (ramos.Count == 0) return (redes, Recusa("nenhuma sala nem bico do corredor ficou em frente à geral"));
        trechos = ramos.SelectMany(r => r).ToList();
        if (ramos.Count > 1 || trechos.Count > 1)
            avisos.Add((ramos.Count > 1 ? "descida da VGA no meio do corredor, geral para os dois lados: " : "geral em L: ") +
                       string.Join(" | ", ramos.Select(r => string.Join(" + ", r.Select(t => $"{Comprimento(t):0.0} m")))) +
                       (trechos.Count > ramos.Count ? ", com cotovelo no canto" : ""));

        using var grupo = new TransactionGroup(doc, "SPK — Geral pelo corredor");
        grupo.Start();
        var geral = new List<Pipe>();
        var motivo = TransacaoSegura.Executar(doc, "SPK — Geral", () =>
        {
            var nivel = BicosPorAmbiente.Nivel(doc, Pes(corredor.Piso));
            var tipo = GeradorMalha.TipoComTe(doc, opcoes.TipoDeTubo);
            var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
            var esboco = DadosEsboco.Carregar(doc);
            var diametro = GeradorMalha.DiametroComTe(doc, tipo, esboco.DnSubgeral > 0 ? esboco.DnSubgeral : esboco.DnTubo);
            XYZ Xyz(Ponto2 p, double zM) => new(Pes(p.X), Pes(p.Y), Pes(zM));
            var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, Xyz(descidaEm, corredor.Piso), Xyz(descidaEm, z), diametro, marcar: false);
            criados.Add(descida.Id);
            foreach (var trecho in trechos)
            {
                var tubo = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, Xyz(trecho.A, z), Xyz(trecho.B, z), diametro, marcar: false);
                geral.Add(tubo);
                criados.Add(tubo.Id);
            }
            doc.Regenerate();
            GeradorMalha.Ligar(doc, [descida, .. geral], [], nivel);
            return null;
        });
        if (motivo is not null)
        {
            grupo.RollBack();
            return (redes, Recusa(motivo));
        }

        var ligacao = LigarAGeral.Executar(doc, geral, dasSalas);
        criados.AddRange(ligacao.Criados);

        // Os bicos do corredor na geral: os pedaços dela são os tubos horizontais da rede na linha de algum trecho.
        if (bicosDoCorredor.Count > 0)
        {
            var naLinha = LigarAGeral.Rede(doc, geral[0]).Select(doc.GetElement).OfType<Pipe>()
                .Where(t => t.Location is LocationCurve { Curve: Line l } && Math.Abs(l.Direction.Z) < 0.05
                            && trechos.Any(tr => NaLinha(tr, l.Evaluate(0.5, true))))
                .ToList();
            var noCorredor = LigarBicos.Executar(doc, bicosDoCorredor.Select(doc.GetElement).OfType<FamilyInstance>(), naLinha);
            criados.AddRange(noCorredor.Criados);
            // Bico que caiu em cima do tê de uma sala (ou do cotovelo) não liga ali: anda um pouco ao longo da geral e tenta
            // de novo, como o projetista faria. O que não couber sai: a rede gerada não fica com bico solto.
            bool Solto(FamilyInstance b) => b.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().Any(c => c.IsConnected) != true;
            var deslocados = new List<string>();
            foreach (var bico in bicosDoCorredor.Select(doc.GetElement).OfType<FamilyInstance>().Where(Solto).ToList())
            {
                var lp = ((LocationPoint)bico.Location).Point;
                var trecho = TrechoDaEntrada(new Ponto2(Metros(lp.X), Metros(lp.Y)), trechos);
                var dir = new XYZ(trecho.Direcao.X, trecho.Direcao.Y, 0);
                foreach (var passo in new[] { 0.25, -0.25, 0.40, -0.40 })
                {
                    if (TransacaoSegura.Executar(doc, "SPK — Deslocar bico do corredor", () =>
                        {
                            ElementTransformUtils.MoveElement(doc, bico.Id, dir * Pes(passo));
                            return null;
                        }) is not null) break;
                    var pedacos = LigarAGeral.Rede(doc, geral.First(g => g.IsValidObject)).Select(doc.GetElement).OfType<Pipe>()
                        .Where(t => t.Location is LocationCurve { Curve: Line l } && Math.Abs(l.Direction.Z) < 0.05 && trechos.Any(tr => NaLinha(tr, l.Evaluate(0.5, true))));
                    var outra = LigarBicos.Executar(doc, [bico], pedacos);
                    if (outra.Ligados == 1)
                    {
                        criados.AddRange(outra.Criados);
                        deslocados.Add($"{Math.Abs(passo) * 100:0} cm");
                        break;
                    }
                    TransacaoSegura.Executar(doc, "SPK — Voltar bico do corredor", () =>
                    {
                        ElementTransformUtils.MoveElement(doc, bico.Id, dir * Pes(-passo));
                        return null;
                    });
                }
            }
            if (deslocados.Count > 0)
                avisos.Add($"{deslocados.Count} bico(s) do corredor andaram ao longo da geral ({string.Join(", ", deslocados)}) para não cair no tê de uma sala: confira o espaçamento");
            var soltos = bicosDoCorredor.Select(doc.GetElement).OfType<FamilyInstance>().Where(Solto).Select(b => b.Id).ToList();
            if (soltos.Count > 0)
                TransacaoSegura.Executar(doc, "SPK — Tirar bicos soltos do corredor", () =>
                {
                    doc.Delete(soltos);
                    return null;
                });
            avisos.Insert(0, $"corredor: {bicosDoCorredor.Count - soltos.Count} de {bicosDoCorredor.Count} bico(s) ligados direto na geral" +
                             (soltos.Count > 0 ? $"; {soltos.Count} não couberam e saíram (" + string.Join("; ", noCorredor.Recusados.Distinct()) +
                                                 "): confira a cobertura com Conferir na planta" : ""));
        }

        ResultadoDiametros? diametros = null;
        if (opcoes.AplicarDiametros && TabelaDeDiametros.TemTabela(dados.Classe) && ligacao.Ligadas > 0)
        {
            try
            {
                var leitura = BicosDoModelo.Ler(doc);
                var umBico = dasSalas.SelectMany(r => r).Select(doc.GetElement).OfType<FamilyInstance>()
                    .First(b => b.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers);
                var lida = LeitorRede.Montar(doc, leitura.Bicos, dados.FatorC, null, [], umBico.Id);
                diametros = AplicarDiametros.Executar(doc, lida, DimensionamentoPorTabela.Calcular(lida.Rede, dados.Classe));
                avisos.AddRange(diametros.Recusados.Distinct());
                if (diametros.AvisoDeApertadas() is { } apertadas) avisos.Add(apertadas);
            }
            catch (InvalidOperationException e)
            {
                avisos.Add("diâmetros do setor não aplicados: " + e.Message);
            }
        }
        grupo.Assimilate();
        return (redes, new GeralGerada(corredor, null, ligacao, diametros, avisos));
    }

    static bool Dentro(Ambiente ambiente, FamilyInstance bico) =>
        bico.Location is LocationPoint lp
        && Spk.Motor.Geracao.BicosNoAmbiente.Dentro(ambiente.Contornos,
            new Ponto2(UnitUtils.ConvertFromInternalUnits(lp.Point.X, UnitTypeId.Meters), UnitUtils.ConvertFromInternalUnits(lp.Point.Y, UnitTypeId.Meters)));

    /// <summary>Um trecho de corredor em L tem pelo menos isto além do canto (abaixo disso é só a largura do corredor).</summary>
    const double PernaMinima = 3.0;

    /// <summary>
    /// O caminho da geral no corredor, em trechos a partir da descida. Reto: um trecho, o eixo do corredor. Em L: o eixo
    /// mais comprido e o eixo na outra direção se cruzam num canto dentro do corredor; a geral vem da ponta longe do canto
    /// (a descida da VGA), dobra no canto e segue pela outra perna. Corredor em T ou em cruz: só o eixo principal, com aviso.
    /// </summary>
    static List<Eixo> CaminhoDoCorredor(Ambiente corredor, List<string> avisos)
    {
        if (EixoDoCorredor(corredor) is not { } principal) return [];
        var d = principal.Direcao;
        var n = new Ponto2(-d.Y, d.X);
        if (EixoNaDirecao(corredor, n) is not { } outro) return [principal];
        double U(Ponto2 p) => p.X * d.X + p.Y * d.Y;
        double V(Ponto2 p) => p.X * n.X + p.Y * n.Y;
        var canto = new Ponto2(d.X * U(outro.A) + n.X * V(principal.A), d.Y * U(outro.A) + n.Y * V(principal.A));
        var ponta = Distancia(canto, outro.A) > Distancia(canto, outro.B) ? outro.A : outro.B;
        if (Distancia(canto, ponta) < PernaMinima || !LivreAte(corredor, canto, ponta)) return [principal];

        // O canto precisa estar numa ponta do eixo principal; no meio dele, o corredor é em T.
        var (ua, ub, uc) = (U(principal.A), U(principal.B), U(canto));
        if (Math.Min(Math.Abs(uc - ua), Math.Abs(uc - ub)) > 2.0)
        {
            avisos.Add("corredor em T ou em cruz: a geral segue só o trecho principal; as salas da outra perna ficam de fora");
            return [principal];
        }
        var inicio = Math.Abs(uc - ua) > Math.Abs(uc - ub) ? principal.A : principal.B;
        if (!LivreAte(corredor, inicio, canto)) return [principal];
        return [new Eixo(inicio, canto), new Eixo(canto, ponta)];
    }

    /// <summary>
    /// Os ramos da geral a partir da descida da VGA. Sem ponto: um ramo, o caminho inteiro, com a descida na ponta dele. Com
    /// ponto: a descida no ponto do caminho mais perto dele (se cair a menos de 50 cm de um canto, ela anda 50 cm para fora
    /// do canto, onde cabe o tê), e dois ramos, um para cada lado; o lado com menos de 30 cm sai.
    /// </summary>
    static List<List<Eixo>> RamosDaEntrada(List<Eixo> caminho, Ponto2? entrada)
    {
        if (entrada is not { } alvo) return [caminho.ToList()];
        var pontos = new List<Ponto2> { caminho[0].A };
        pontos.AddRange(caminho.Select(t => t.B));

        // O ponto do caminho mais perto do pedido.
        var (k, e) = (0, pontos[0]);
        var menor = double.MaxValue;
        for (var i = 0; i + 1 < pontos.Count; i++)
        {
            var (a, b) = (pontos[i], pontos[i + 1]);
            var c = Distancia(a, b);
            var t = c < 1e-9 ? 0 : Math.Clamp(((alvo.X - a.X) * (b.X - a.X) + (alvo.Y - a.Y) * (b.Y - a.Y)) / (c * c), 0, 1);
            var p = new Ponto2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            if (Distancia(p, alvo) < menor) (menor, k, e) = (Distancia(p, alvo), i, p);
        }
        // Num canto (vértice do meio do caminho) a descida faria um tê com os dois ramos em ângulo: anda 50 cm para fora.
        for (var i = 1; i + 1 < pontos.Count; i++)
        {
            if (Distancia(e, pontos[i]) >= 0.5) continue;
            var (de, para) = Distancia(pontos[i], pontos[i - 1]) >= Distancia(pontos[i], pontos[i + 1]) ? (i, i - 1) : (i, i + 1);
            var c = Distancia(pontos[de], pontos[para]);
            e = new Ponto2(pontos[de].X + (pontos[para].X - pontos[de].X) * 0.5 / c, pontos[de].Y + (pontos[para].Y - pontos[de].Y) * 0.5 / c);
            k = Math.Min(de, para);
        }

        var volta = new List<Eixo>();
        var atual = e;
        for (var i = k; i >= 0; i--)
        {
            if (Distancia(atual, pontos[i]) > 0.01) volta.Add(new Eixo(atual, pontos[i]));
            atual = pontos[i];
        }
        var vai = new List<Eixo>();
        atual = e;
        for (var i = k + 1; i < pontos.Count; i++)
        {
            if (Distancia(atual, pontos[i]) > 0.01) vai.Add(new Eixo(atual, pontos[i]));
            atual = pontos[i];
        }
        var ramos = new[] { vai, volta }.Where(r => r.Sum(Comprimento) >= 0.30).ToList();
        return ramos.Count > 0 ? ramos : [caminho.ToList()];
    }

    /// <summary>O trecho da geral que a sala tem à frente: o mais perto do centro dela entre os que ela ladeia (1 m ou mais).</summary>
    static Eixo TrechoDaSala(Ambiente sala, List<Eixo> trechos)
    {
        var laco = sala.Contornos[0];
        var centro = new Ponto2(laco.Average(p => p.X), laco.Average(p => p.Y));
        return trechos
            .Select(t =>
            {
                var d = t.Direcao;
                double U(Ponto2 p) => (p.X - t.A.X) * d.X + (p.Y - t.A.Y) * d.Y;
                var ladeia = Math.Min(laco.Max(U), Comprimento(t)) - Math.Max(laco.Min(U), 0);
                var longe = Math.Abs((centro.X - t.A.X) * d.Y - (centro.Y - t.A.Y) * d.X);
                return (Trecho: t, Ladeia: ladeia, Longe: longe);
            })
            .OrderBy(x => x.Ladeia >= 1.0 ? 0 : 1).ThenBy(x => x.Longe)
            .First().Trecho;
    }

    /// <summary>O trecho da geral onde a entrada da sala vai ligar: o mais perto dela entre os que passam em frente.</summary>
    static Eixo TrechoDaEntrada(Ponto2 entrada, List<Eixo> trechos) =>
        trechos.OrderBy(t =>
        {
            var d = t.Direcao;
            var u = (entrada.X - t.A.X) * d.X + (entrada.Y - t.A.Y) * d.Y;
            var fora = u < -0.5 || u > Comprimento(t) + 0.5;
            return (fora ? 1000 : 0) + Math.Abs((entrada.X - t.A.X) * d.Y - (entrada.Y - t.A.Y) * d.X);
        }).First();

    static double Comprimento(Eixo e) => Distancia(e.A, e.B);

    /// <summary>O ponto (em pés) está na reta do trecho, em planta, a até 5 cm.</summary>
    static bool NaLinha(Eixo trecho, XYZ p)
    {
        var d = trecho.Direcao;
        var (x, y) = (Metros(p.X), Metros(p.Y));
        return Math.Abs((x - trecho.A.X) * d.Y - (y - trecho.A.Y) * d.X) < 0.05;
    }
    static double Distancia(Ponto2 a, Ponto2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>A reta de A até B toda dentro do corredor, a 30 cm ou mais das paredes.</summary>
    static bool LivreAte(Ambiente corredor, Ponto2 a, Ponto2 b)
    {
        var passos = Math.Max(2, (int)(Distancia(a, b) / 0.10));
        for (var k = 0; k <= passos; k++)
            if (!Livre(corredor, new Ponto2(a.X + (b.X - a.X) * k / passos, a.Y + (b.Y - a.Y) * k / passos))) return false;
        return true;
    }

    static bool Livre(Ambiente corredor, Ponto2 p) =>
        Spk.Motor.Geracao.BicosNoAmbiente.Dentro(corredor.Contornos, p)
        && Spk.Motor.Geracao.BicosNoAmbiente.DistanciaAoContorno(corredor.Contornos, p) >= 0.30 - 1e-9;

    /// <summary>
    /// O eixo da geral no corredor: na direção da parede mais comprida, a reta (afastada pelo menos 30 cm das paredes)
    /// com o trecho contínuo dentro do ambiente mais comprido; no empate, a mais longe das paredes. As pontas ficam 30 cm
    /// antes das paredes.
    /// </summary>
    static Eixo? EixoDoCorredor(Ambiente corredor)
    {
        var laco = corredor.Contornos[0];
        var (maior, d) = (0.0, new Ponto2(1, 0));
        for (var i = 0; i < laco.Count; i++)
        {
            var (a, b) = (laco[i], laco[(i + 1) % laco.Count]);
            var c = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            if (c > maior) (maior, d) = (c, new Ponto2((b.X - a.X) / c, (b.Y - a.Y) / c));
        }
        return EixoNaDirecao(corredor, d);
    }

    /// <summary>Na direção <paramref name="d" />, a reta livre mais comprida dentro do corredor (ver <see cref="EixoDoCorredor" />).</summary>
    static Eixo? EixoNaDirecao(Ambiente corredor, Ponto2 d)
    {
        var laco = corredor.Contornos[0];
        var n = new Ponto2(-d.Y, d.X);
        double U(Ponto2 p) => p.X * d.X + p.Y * d.Y;
        double N(Ponto2 p) => p.X * n.X + p.Y * n.Y;
        Ponto2 P(double u, double v) => new(d.X * u + n.X * v, d.Y * u + n.Y * v);
        var (u0, u1, n0, n1) = (laco.Min(U), laco.Max(U), laco.Min(N), laco.Max(N));

        (double Comprimento, double Meio, Eixo Eixo)? melhor = null;
        for (var v = n0 + 0.30; v <= n1 - 0.30 + 1e-9; v += 0.05)
        {
            double? inicio = null;
            for (var u = u0; u <= u1 + 1e-9; u += 0.10)
            {
                var livre = Livre(corredor, P(u, v));
                if (livre && inicio is null) inicio = u;
                if ((!livre || u + 0.10 > u1 + 1e-9) && inicio is { } i0)
                {
                    var fim = livre ? u : u - 0.10;
                    var comprimento = fim - i0;
                    // Desempate: a reta mais longe das paredes (o centro da perna). Medir pelo meio do ambiente inteiro
                    // puxava a geral de um corredor em L para a parede de dentro.
                    var meio = -Spk.Motor.Geracao.BicosNoAmbiente.DistanciaAoContorno(corredor.Contornos, P((i0 + fim) / 2, v));
                    if (comprimento > 0.5 && (melhor is not { } m || comprimento > m.Comprimento + 0.05
                                              || Math.Abs(comprimento - m.Comprimento) <= 0.05 && meio < m.Meio))
                        melhor = (comprimento, meio, new Eixo(P(i0, v), P(fim, v)));
                    inicio = null;
                }
            }
        }
        return melhor?.Eixo;
    }

    /// <param name="geral">O trecho da geral que cada sala tem à frente; nulo: cada sala com a sua entrada.</param>
    static IReadOnlyList<RedeNoAmbiente> Executar(Document doc, DadosProjeto dados, Edificio edificio, IEnumerable<Ambiente> ambientes,
        OpcoesRede opcoes, List<ElementId> criados, Func<Ambiente, Eixo>? geral)
    {
        var limites = BicosPorAmbiente.Limites(dados, opcoes.Bicos);
        var resultados = new List<RedeNoAmbiente>();
        foreach (var ambiente in ambientes)
        {
            using var grupo = new TransactionGroup(doc, "SPK — Gerar rede no ambiente");
            grupo.Start();
            var deste = new List<ElementId>();
            var rede = Gerar(doc, dados, edificio, ambiente, limites, opcoes, deste, geral?.Invoke(ambiente));
            if (rede.Recusa is not null)
            {
                grupo.RollBack();
            }
            else
            {
                grupo.Assimilate();
                criados.AddRange(deste.Where(id => doc.GetElement(id) is not null));
            }
            resultados.Add(rede);
        }
        return resultados;
    }

    static RedeNoAmbiente Gerar(Document doc, DadosProjeto dados, Edificio edificio, Ambiente ambiente, LimitesDeBico limites,
        OpcoesRede opcoes, List<ElementId> criados, Eixo? geral)
    {
        RedeNoAmbiente Recusa(string motivo) => new(ambiente, motivo, 0, null, null, null, []);

        // Com geral, os ramais correm paralelos a ela: a subgeral sai perpendicular e aponta direto para a geral.
        var opcoesBicos = geral is null ? opcoes.Bicos : opcoes.Bicos with { Rumo = geral.Direcao };
        // Viga funda: bicos nos vãos. Vigas numa direção só: o ramal corre no vão, entre a laje e o defletor, que desce até
        // 25 cm da laje (o máximo sob teto liso é 30 cm) para caber o ramal 10 cm abaixo da laje e 15 cm acima do bico.
        // Grelha (vigas nas duas direções): o ramal no vão bateria nas vigas que o cruzam; a rede inteira passa por baixo
        // das vigas e sobe até cada bico, que tem de ser em pé.
        var vigasDosVaos = BicosPorAmbiente.VigasDosVaos(edificio, ambiente, limites, opcoesBicos);
        var grelha = vigasDosVaos is not null && Spk.Motor.Geracao.BicosNoAmbiente.EhGrelha(vigasDosVaos.Select(v => (v.A, v.B, v.Largura)).ToList());
        // Sob laje ou telhado a rede passa entre o defletor e o teto: 10 cm abaixo do teto e 15 cm acima do bico, dentro dos 30 cm
        // que o teto liso permite. Sob forro ela atravessa por cima, e o defletor fica onde o projetista pediu.
        var defletorPedido = opcoesBicos.DefletorM;
        if ((vigasDosVaos is not null && !grelha) || (vigasDosVaos is null && ambiente.OrigemDoTeto is OrigemDoTeto.Laje or OrigemDoTeto.Telhado))
            opcoesBicos = opcoesBicos with { DefletorM = Math.Max(opcoesBicos.DefletorM, FolgaDoTeto + DropMinimo) };
        var bicos = BicosPorAmbiente.Posicionar(doc, edificio, [ambiente], limites, opcoesBicos, criados).Single();
        if (bicos.Recusa is not null) return Recusa(bicos.Recusa);
        if (bicos.Posicao is not { Bicos.Count: > 0 } posicao || bicos.Defletor is not { } defletor) return Recusa("nenhum bico coube no ambiente");
        var bicosDoAmbiente = criados.Select(doc.GetElement).OfType<FamilyInstance>().ToList();

        var avisos = new List<string>();
        if (bicos.Falhas.Where(f => f.Tipo == Spk.Motor.Geracao.TipoDeFalha.Obstrucao).ToList() is { Count: > 0 } obstruidos)
            avisos.Add($"{obstruidos.Count} bico(s) obstruídos por viga: {obstruidos[0].Descricao}. Mova o bico ou confira em Conferir na planta");
        if (vigasDosVaos is null && opcoesBicos.DefletorM > defletorPedido + 1e-6)
            avisos.Add($"defletor a {opcoesBicos.DefletorM * 100:0} cm do teto, não {defletorPedido * 100:0} cm: é o mínimo para a rede caber embaixo da {(ambiente.OrigemDoTeto == OrigemDoTeto.Laje ? "laje" : "cobertura")}");
        double zRede, zSubgeral;
        if (bicos.VaosEntreVigas is { Count: > 0 } vaos && grelha)
        {
            // O conector do bico em pé aponta para baixo (o tubo chega por baixo).
            var emPe = bicosDoAmbiente.All(b => b.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>()
                .Any(c => c.Domain == Domain.DomainPiping && c.CoordinateSystem.BasisZ.Z < -0.7) == true);
            if (!emPe)
                return Recusa("vigas nas duas direções (quadros de vigas): a rede passa por baixo das vigas e sobe até cada bico, o que pede " +
                              "bico em pé (upright). Escolha um tipo em pé em Bico, ou ponha os bicos com Bicos por ambiente e desenhe a rede à mão");
            zRede = zSubgeral = vaos.Min(v => v.Fundo) - 0.15;
            if (defletor - zRede < DropMinimo - 1e-6) return Recusa("as vigas são rasas demais para a rede passar por baixo delas e subir até os bicos");
            avisos.Add($"bicos em pé nos quadros entre as vigas; ramais e subgeral por baixo das vigas a {zRede:0.00} m, com subida até cada bico. " +
                       "Confira a regra de obstrução: distância do bico à viga e altura do defletor acima do fundo dela");
        }
        else
        {
            // Altura da rede: o drop acima do defletor, sem encostar na laje ou no telhado (o forro a rede atravessa por cima).
            zRede = defletor + opcoes.DropM;
            if (ambiente.OrigemDoTeto is OrigemDoTeto.Laje or OrigemDoTeto.Telhado && ambiente.Teto is { } teto && zRede > teto - FolgaDoTeto)
            {
                zRede = teto - FolgaDoTeto;
                if (zRede - defletor < DropMinimo - 1e-6)
                    return Recusa($"sem espaço para a rede entre o defletor ({defletor:0.00}) e o teto ({teto:0.00}): " +
                                  "a rede precisa de pelo menos 15 cm acima do defletor e 10 cm abaixo do teto");
                avisos.Add($"rede baixada para {zRede:0.00} m, 10 cm abaixo do teto");
            }
            // Com bicos nos vãos, a subgeral passa por baixo das vigas (15 cm abaixo do fundo da mais baixa) e cada ramal sobe
            // dela até o vão; senão, a subgeral fica na altura dos ramais.
            zSubgeral = zRede;
            if (bicos.VaosEntreVigas is { Count: > 0 } paralelas)
            {
                zSubgeral = paralelas.Min(v => v.Fundo) - 0.15;
                if (zRede - zSubgeral < DropMinimo) return Recusa("as vigas são rasas demais para a subgeral passar por baixo e subir até o vão");
                avisos.Add($"bicos nos vãos entre as vigas; ramais nos vãos a {zRede:0.00} m e subgeral por baixo das vigas a {zSubgeral:0.00} m, " +
                           "com subida até cada ramal. Confira a regra de obstrução: distância do bico à viga e altura do defletor acima do fundo dela");
            }
            else
            {
                foreach (var viga in edificio.Vigas.Where(v => v.Fundo < zRede + 0.05 && v.Topo > zRede - 0.05).Take(3))
                    if (Cruza(ambiente, viga)) avisos.Add($"a rede, a {zRede:0.00} m, passa na altura da viga (fundo {viga.Fundo:0.00}): confira a travessia");
            }
        }
        var subgeral = new List<Pipe>();
        var motivo = TransacaoSegura.Executar(doc, "SPK — Subgeral", () => Subgeral(doc, ambiente, posicao, zSubgeral, opcoes.Arranjo, criados, avisos, subgeral, geral, opcoes.TipoDeTubo));
        if (motivo is not null) return Recusa(motivo);

        var opcoesRamais = zSubgeral < zRede - 0.01
            ? new OpcoesRamais { Altura = AlturaDoRamal.RelativaASubgeral, AlturaM = zRede - zSubgeral }
            : new OpcoesRamais();
        // Sob laje ou telhado sobram poucos centímetros entre o ramal e o conector do bico (9 cm no bico K5.6 da Tyco, com 15 cm até
        // o defletor), e o tê de um ramal grosso não cabe, mesmo com a descida fina. O ramal nasce no menor DN, e a tabela sobe o que precisar.
        if (opcoes.AplicarDiametros && zRede - defletor < DropComDnDoEsboco) opcoesRamais = opcoesRamais with { DnRamal = DnNaDescidaCurta };
        var ramais = TracarRamal.Executar(doc, bicosDoAmbiente, subgeral[0], opcoesRamais);
        criados.AddRange(ramais.Criados);
        if (ramais.Tracados == 0) return Recusa("nenhum ramal traçado: " + string.Join("; ", ramais.Fileiras.Select(f => f.Recusa).Distinct()));

        ResultadoDiametros? diametros = null;
        if (opcoes.AplicarDiametros && TabelaDeDiametros.TemTabela(dados.Classe))
        {
            try
            {
                var leitura = BicosDoModelo.Ler(doc);
                // O setor é o de um bico ligado: um bico que ficou solto não está em setor nenhum.
                var ligado = bicosDoAmbiente.FirstOrDefault(b => b.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().Any(c => c.IsConnected) == true)
                             ?? bicosDoAmbiente[0];
                var lida = LeitorRede.Montar(doc, leitura.Bicos, dados.FatorC, null, [], ligado.Id);
                diametros = AplicarDiametros.Executar(doc, lida, DimensionamentoPorTabela.Calcular(lida.Rede, dados.Classe));
                avisos.AddRange(diametros.Recusados.Distinct());
                if (diametros.AvisoDeApertadas() is { } apertadas) avisos.Add(apertadas);
            }
            catch (InvalidOperationException e)
            {
                avisos.Add("diâmetros não aplicados: " + e.Message);
            }
        }
        if (Tampar(doc, criados, Math.Min(zRede, zSubgeral)) is { } semTampao) avisos.Add(semTampao);
        return new RedeNoAmbiente(ambiente, null, bicosDoAmbiente.Count, zRede, ramais, diametros, avisos)
        {
            AlturaDaSubgeral = zSubgeral < zRede - 0.01 ? zSubgeral : null,
        };
    }

    /// <summary>
    /// A subgeral cruza as fileiras ao longo do eixo entre ramais (v): na espinha de peixe, entre as duas colunas de bicos
    /// do meio; no lateral, entre a parede e a primeira coluna. Começa 30 cm da parede de menor v, onde fica a descida até o
    /// piso (a entrada do setor), e termina 20 cm depois da última fileira (o mínimo para o tê).
    /// </summary>
    static string? Subgeral(Document doc, Ambiente ambiente, Spk.Motor.Geracao.PosicaoDeBicos posicao, double zRede, Arranjo arranjo,
        List<ElementId> criados, List<string> avisos, List<Pipe> subgeral, Eixo? geral, string? tipoDeTubo)
    {
        var u = posicao.DirecaoDosRamais;
        var v = new Ponto2(-u.Y, u.X);
        double U(Ponto2 p) => p.X * u.X + p.Y * u.Y;
        double V(Ponto2 p) => p.X * v.X + p.Y * v.Y;
        var colunas = Agrupar(posicao.Bicos.Select(U));
        var fileiras = Agrupar(posicao.Bicos.Select(V));
        var contorno = ambiente.Contornos[0];
        var (u0, v0, v1) = (contorno.Min(U), contorno.Min(V), contorno.Max(V));

        double uSub;
        if (arranjo == Arranjo.EspinhaDePeixe && colunas.Count >= 2)
        {
            var k = colunas.Count / 2;
            uSub = (colunas[k - 1] + colunas[k]) / 2;
        }
        else
        {
            if (arranjo == Arranjo.EspinhaDePeixe) avisos.Add("uma coluna de bicos só: subgeral junto à parede (lateral)");
            uSub = (u0 + colunas[0]) / 2;
        }
        // A entrada fica na parede de menor v; com geral, na parede do lado dela. Sem geral, se a primeira fileira fica colada
        // a essa parede e não cabe o tê, a entrada vai para a parede oposta.
        double vInicio, vFim;
        var pelaOposta = geral is not null ? V(geral.A) > (v0 + v1) / 2 : fileiras[0] - (v0 + 0.30) < 0.20;
        if (pelaOposta)
        {
            vInicio = v1 - 0.30;
            vFim = Math.Max(fileiras[0] - 0.20, v0 + 0.10);
            if (vInicio - fileiras[^1] < 0.20) return "a primeira fileira de bicos fica colada à parede da entrada: não cabe o tê da subgeral";
        }
        else
        {
            vInicio = v0 + 0.30;
            vFim = Math.Min(fileiras[^1] + 0.20, v1 - 0.10);
            if (fileiras[0] - vInicio < 0.20) return "a primeira fileira de bicos fica colada à parede da entrada: não cabe o tê da subgeral";
        }

        Ponto2 P(double a, double b) => new(u.X * a + v.X * b, u.Y * a + v.Y * b);
        var inicio = P(uSub, vInicio);
        var fim = P(uSub, vFim);
        for (var k = 0; k <= 20; k++)
            if (!Spk.Motor.Geracao.BicosNoAmbiente.Dentro(ambiente.Contornos,
                    new Ponto2(inicio.X + (fim.X - inicio.X) * k / 20, inicio.Y + (fim.Y - inicio.Y) * k / 20)))
            {
                avisos.Add("a subgeral sai do ambiente (ambiente que não é retângulo): confira o traçado");
                break;
            }

        var nivel = BicosPorAmbiente.Nivel(doc, Pes(ambiente.Piso));
        var tipo = GeradorMalha.TipoComTe(doc, tipoDeTubo);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var esboco = DadosEsboco.Carregar(doc);
        var diametro = GeradorMalha.DiametroComTe(doc, tipo, esboco.DnSubgeral > 0 ? esboco.DnSubgeral : esboco.DnTubo);
        XYZ Xyz(Ponto2 p, double z) => new(Pes(p.X), Pes(p.Y), Pes(z));
        var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, Xyz(inicio, ambiente.Piso), Xyz(inicio, zRede), diametro, marcar: false);
        var tubo = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, Xyz(inicio, zRede), Xyz(fim, zRede), diametro, marcar: false);
        subgeral.Add(tubo);
        criados.Add(descida.Id);
        criados.Add(tubo.Id);
        doc.Regenerate();
        GeradorMalha.Ligar(doc, [descida, tubo], [], nivel);
        return null;
    }

    /// <summary>
    /// Tampão nas pontas abertas da rede do ambiente na altura da rede (a ponta da subgeral depois da última fileira),
    /// como na obra. O pé da descida fica aberto: é a entrada do setor. O tampão vem das preferências de roteamento do
    /// tipo de tubo; sem ele, a ponta fica aberta e o aviso diz por quê.
    /// </summary>
    static string? Tampar(Document doc, List<ElementId> criados, double zRede)
    {
        var abertos = criados.Select(doc.GetElement).OfType<Pipe>()
            .Where(t => t.ConnectorManager.Connectors.Cast<Connector>()
                .Any(c => c.ConnectorType == ConnectorType.End && !c.IsConnected && c.Origin.Z > Pes(zRede - 0.05)))
            .ToList();
        if (abertos.Count == 0) return null;
        return TransacaoSegura.Executar(doc, "SPK — Tampão", () =>
        {
            foreach (var tubo in abertos) PlumbingUtils.PlaceCapOnOpenEnds(doc, tubo.Id, ElementId.InvalidElementId);
            return null;
        }) is { } erro
            ? "ponta da subgeral sem tampão (o tipo de tubo não tem tampão nas preferências de roteamento): " + erro
            : null;
    }

    /// <summary>Posições distintas (a menos de 30 cm viram uma), em ordem.</summary>
    static List<double> Agrupar(IEnumerable<double> valores)
    {
        var grupos = new List<List<double>>();
        foreach (var x in valores.OrderBy(x => x))
            if (grupos.Count > 0 && x - grupos[^1][^1] < 0.30) grupos[^1].Add(x);
            else grupos.Add([x]);
        return grupos.Select(g => g.Average()).ToList();
    }

    static bool Cruza(Ambiente ambiente, Viga viga) =>
        Enumerable.Range(0, 21).Any(k => Spk.Motor.Geracao.BicosNoAmbiente.Dentro(ambiente.Contornos,
            new Ponto2(viga.A.X + (viga.B.X - viga.A.X) * k / 20, viga.A.Y + (viga.B.Y - viga.A.Y) * k / 20)));

    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
    static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);
}
