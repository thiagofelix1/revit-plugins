using Autodesk.Revit.DB.Plumbing;

namespace Spk.AddIn.Modelagem;

/// <summary>Quantos bicos foram ligados, por que os outros não, e o que foi criado (para selecionar no fim).</summary>
public sealed record ResultadoLigacao(int Ligados, IReadOnlyList<string> Recusados, IReadOnlyList<ElementId> Criados)
{
    public string Texto(int maximo = 15)
    {
        var linhas = new List<string> { $"{Ligados} bico(s) ligado(s) ao ramal." };
        if (Recusados.Count > 0)
        {
            linhas.Add($"{Recusados.Count} ficaram como estavam:");
            linhas.AddRange(Recusados.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(maximo)
                .Select(g => $"• {g.Key}{(g.Count() > 1 ? $" ({g.Count()})" : "")}"));
        }
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Liga bicos soltos ao ramal que passa sobre eles, como o "Conectar a" do Revit, mas para vários bicos de uma vez.
///     Para cada bico: acha o ramal selecionado que passa sobre ele (em planta, até 3 m, longe das pontas do tubo), parte
///     o ramal no ponto mais perto do bico e põe um tê; se o bico está embaixo do ramal (pendente), desce um tubo até ele,
///     e se está acima (em pé), sobe; se está de lado, faz o braço horizontal, um cotovelo e a descida (ou a subida). Braço e descida com o diâmetro e o tipo do ramal (o
///     ajuste de diâmetro é outra ferramenta). Cada bico numa transação própria; tudo num grupo, um Ctrl+Z desfaz.
///     Os tubos criados não levam a marca da malha gerada: é rede do projeto, e a Rede de exemplo não pode apagá-los.
/// </summary>
public static class LigarBicos
{
    static readonly double MargemPonta = GeradorMalha.Pes(0.15);
    static readonly double BracoMaximo = GeradorMalha.Pes(3.0);
    static readonly double BracoMinimo = GeradorMalha.Pes(0.15);
    static readonly double Encontro = GeradorMalha.Pes(0.04);
    static readonly double Recuo = GeradorMalha.Pes(0.01);
    const int DnDaDescida = 25;
    static readonly double DescidaCurta = GeradorMalha.Pes(0.30);

    public static ResultadoLigacao Executar(Document doc, IEnumerable<FamilyInstance> bicos, IEnumerable<Pipe> ramais)
    {
        var candidatos = ramais.ToList();
        var criados = new List<ElementId>();
        var recusados = new List<string>();
        var ligados = 0;
        using var grupo = new TransactionGroup(doc, "SPK — Ligar bicos ao ramal");
        grupo.Start();
        foreach (var bico in bicos.OrderBy(b => b.Id.Value))
        {
            var deste = new List<ElementId>();
            var novos = new List<Pipe>();
            var motivo = TransacaoSegura.Executar(doc, "SPK — Ligar bico", () => Ligar(doc, bico, candidatos, novos, deste));
            if (motivo is null)
            {
                ligados++;
                criados.AddRange(deste);
                candidatos.AddRange(novos);
            }
            else
            {
                recusados.Add(Explicar(motivo, bico, candidatos));
            }
        }
        grupo.Assimilate();
        return new ResultadoLigacao(ligados, recusados, criados);
    }

    /// <summary>O Revit recusa com uma mensagem da geometria dele ("direção oposta"): numa descida curta, diz o que isso quer dizer.</summary>
    static string Explicar(string motivo, FamilyInstance bico, List<Pipe> ramais)
    {
        if (!motivo.StartsWith("o Revit") || Livre(bico) is not { } conector || Ramal(ramais, conector.Origin) is not { } achado) return motivo;
        var descida = Math.Abs(conector.Origin.Z - achado.Ponto.Z);
        if (descida >= DescidaCurta) return motivo;
        return $"a descida de {UnitUtils.ConvertFromInternalUnits(descida, UnitTypeId.Centimeters):0} cm é curta para as peças do ramal de " +
               $"{UnitUtils.ConvertFromInternalUnits(achado.Ramal.Diameter, UnitTypeId.Millimeters):0} mm: afaste o ramal do bico ou reduza o DN dele ({motivo})";
    }

    /// <summary>Nulo se ligou; senão, por que não.</summary>
    static string? Ligar(Document doc, FamilyInstance bico, List<Pipe> ramais, List<Pipe> novos, List<ElementId> criados)
    {
        if (Livre(bico) is not { } conectorBico) return "bico já ligado, ou sem conector de tubulação";
        var h = conectorBico.Origin;
        if (Ramal(ramais, h) is not { } achado)
            return "nenhum ramal selecionado passa sobre o bico (até 3 m; perto da ponta, só se a ponta estiver livre)";
        var (ramal, p, naPonta) = achado;
        if (Math.Abs(h.Z - p.Z) < GeradorMalha.Pes(0.05)) return "bico na altura do ramal: não cabe a descida (ou a subida)";
        // O conector aponta para onde o tubo chega: para cima no pendente, para baixo no bico em pé.
        var saida = conectorBico.CoordinateSystem.BasisZ;
        if (Math.Abs(saida.Z) < 0.7) return "bico de parede (conector de lado): ainda não suportado";
        if (saida.Z * (p.Z - h.Z) < 0)
            return h.Z > p.Z ? "bico pendente acima do ramal: o ramal tem de passar acima do bico"
                : "bico em pé abaixo do ramal: o ramal tem de passar abaixo do bico";

        var sistema = ramal.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId() ?? ElementId.InvalidElementId;
        if (sistema == ElementId.InvalidElementId) return "o ramal não tem tipo de sistema";
        var nivel = ramal.ReferenceLevel;
        var tipo = ramal.PipeType.Id;
        // Quem desce só leva água a um bico: no meio do ramal, sai do tê com o DN do bico, e não com o do ramal, cujo tê grande não
        // cabe numa descida curta. Na ponta o tubo segue o ramal: o cotovelo não reduz.
        var diametro = naPonta ? ramal.Diameter : Math.Min(ramal.Diameter, GeradorMalha.DiametroComTe(doc, ramal.PipeType, DnDaDescida) ?? ramal.Diameter);

        // No meio do ramal, parte o tubo no ponto mais perto do bico (os dois pedaços ficam com a ponta ali, sem ligação) e
        // põe um tê. Na ponta livre do ramal, o último bico: sem partir, com cotovelo.
        if (!naPonta && doc.GetElement(PlumbingUtils.BreakCurve(doc, ramal.Id, p)) is Pipe outroPedaco) novos.Add(outroPedaco);

        var canto = new XYZ(h.X, h.Y, p.Z);
        var temBraco = canto.DistanceTo(p) > GeradorMalha.Pes(0.01);
        if (temBraco && canto.DistanceTo(p) < BracoMinimo)
            return $"bico a {UnitUtils.ConvertFromInternalUnits(canto.DistanceTo(p), UnitTypeId.Centimeters):0} cm de lado do ramal: perto demais para braço com cotovelo " +
                   "e fora da linha para descida reta (alinhe o bico ao ramal ou afaste-o a 15 cm ou mais)";
        var primeiro = GeradorMalha.CriarTubo(doc, sistema, tipo, nivel.Id, p, temBraco ? canto : h, diametro, marcar: false);
        var descida = temBraco ? GeradorMalha.CriarTubo(doc, sistema, tipo, nivel.Id, canto, h, diametro, marcar: false) : null;
        criados.Add(primeiro.Id);
        if (descida is not null) criados.Add(descida.Id);
        doc.Regenerate();

        if (naPonta)
        {
            // Cotovelo na ponta do ramal: os dois tubos recuam 1 cm e o Revit põe a peça das preferências de roteamento.
            var naCurva = Abertos(doc, p, ramal, primeiro);
            if (naCurva.Count != 2) return "a ponta do ramal e a descida não se encontraram para o cotovelo";
            foreach (var c in naCurva) GeradorMalha.Recuar(c, Recuo);
            doc.Regenerate();
            naCurva = Abertos(doc, p, ramal, primeiro);
            doc.Create.NewElbowFitting(naCurva[0], naCurva[1]);
        }
        else
        {
            // Tê no ramal: os três tubos recuam 1 cm do encontro e o Revit põe a peça das preferências de roteamento.
            var noTe = Abertos(doc, p, ramal, novos.LastOrDefault(), primeiro);
            if (noTe.Count != 3) return "o ramal não se partiu como esperado (tubo curto demais ou já com peça ali)";
            foreach (var c in noTe) GeradorMalha.Recuar(c, Recuo);
            doc.Regenerate();
            noTe = Abertos(doc, p, ramal, novos.LastOrDefault(), primeiro);
            if (!GeradorMalha.TentarInserirTe(doc, noTe))
                GeradorMalha.InserirTePorFamilia(doc, p, nivel, GeradorMalha.SimboloDeTe(doc), noTe);
        }

        if (descida is not null)
        {
            var noCanto = Abertos(doc, canto, primeiro, descida);
            foreach (var c in noCanto) GeradorMalha.Recuar(c, Recuo);
            doc.Regenerate();
            noCanto = Abertos(doc, canto, primeiro, descida);
            if (noCanto.Count != 2) return "o braço e a descida não se encontraram para o cotovelo";
            doc.Create.NewElbowFitting(noCanto[0], noCanto[1]);
        }

        doc.Regenerate();
        var ponta = Abertos(doc, h, descida ?? primeiro).FirstOrDefault()
                    ?? throw new InvalidOperationException("a descida não chegou ao bico");
        ponta.ConnectTo(Livre(bico)!);
        return null;
    }

    /// <summary>
    /// O ramal horizontal mais perto do bico em planta e o ponto dele onde o bico se liga: o mais perto do bico, longe das
    /// pontas; ou a própria ponta, se o bico está nela e ela está livre (o último bico do ramal).
    /// </summary>
    static (Pipe Ramal, XYZ Ponto, bool NaPonta)? Ramal(List<Pipe> ramais, XYZ h)
    {
        (Pipe, XYZ, bool)? melhor = null;
        var menor = BracoMaximo;
        foreach (var ramal in ramais)
        {
            if (!ramal.IsValidObject || ramal.Location is not LocationCurve { Curve: Line linha }) continue;
            if (Math.Abs(linha.Direction.Z) > 0.05) continue;
            var a = linha.GetEndPoint(0);
            var t = (h - a).DotProduct(linha.Direction);
            XYZ p;
            var naPonta = false;
            if (t >= MargemPonta && t <= linha.Length - MargemPonta)
            {
                p = a + linha.Direction * t;
            }
            else if (t > -MargemPonta && t < MargemPonta && PontaLivre(ramal, a))
            {
                (p, naPonta) = (a, true);
            }
            else if (t > linha.Length - MargemPonta && t < linha.Length + MargemPonta && PontaLivre(ramal, linha.GetEndPoint(1)))
            {
                (p, naPonta) = (linha.GetEndPoint(1), true);
            }
            else
            {
                continue;
            }
            var emPlanta = new XYZ(h.X - p.X, h.Y - p.Y, 0).GetLength();
            if (emPlanta >= menor) continue;
            menor = emPlanta;
            melhor = (ramal, p, naPonta);
        }
        return melhor;
    }

    static bool PontaLivre(Pipe tubo, XYZ ponta) =>
        tubo.ConnectorManager.Connectors.Cast<Connector>()
            .Any(c => c.ConnectorType == ConnectorType.End && !c.IsConnected && c.Origin.DistanceTo(ponta) < Encontro);

    static Connector? Livre(FamilyInstance bico) =>
        bico.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>()
            .FirstOrDefault(c => c.ConnectorType == ConnectorType.End && c.Domain == Domain.DomainPiping && !c.IsConnected);

    /// <summary>Pontas sem ligação destes tubos perto do ponto.</summary>
    static List<Connector> Abertos(Document doc, XYZ ponto, params Pipe?[] tubos) =>
        tubos.Where(t => t is { IsValidObject: true })
            .SelectMany(t => t!.ConnectorManager.Connectors.Cast<Connector>())
            .Where(c => c.ConnectorType == ConnectorType.End && !c.IsConnected && c.Origin.DistanceTo(ponto) < Encontro)
            .ToList();
}
