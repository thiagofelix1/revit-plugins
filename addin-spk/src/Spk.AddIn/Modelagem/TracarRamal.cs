using Autodesk.Revit.DB.Plumbing;

namespace Spk.AddIn.Modelagem;

/// <summary>O ramal traçado (ou por que não) e a ligação dos bicos a ele.</summary>
public sealed record ResultadoRamal(string? Recusa, Pipe? Ramal, ResultadoLigacao? Ligacao)
{
    public string Texto() => Recusa is not null
        ? "O ramal não foi traçado: " + Recusa
        : $"Ramal traçado ({Ligacao!.Ligados + Ligacao.Recusados.Count} bicos na fileira).\n" + Ligacao.Texto();
}

/// <summary>
///     Traça o ramal de uma fileira de bicos até a subgeral e liga os bicos a ele. O ramal segue a reta que melhor passa
///     pelos bicos em planta, na altura da subgeral, do cruzamento com ela até o último bico; a subgeral é partida ali e
///     ganha um tê. Os bicos são ligados pelo Ligar bicos (tê e descida, braço quando o bico está de lado, cotovelo no
///     último). Uma fileira de cada vez, de um lado só da subgeral: dos dois lados precisaria de cruzeta.
///     Diâmetro do ramal: o DN de Dados do esboço, com tê no tipo de tubo da subgeral. Tudo num grupo: um Ctrl+Z desfaz.
/// </summary>
public static class TracarRamal
{
    static readonly double MargemPonta = GeradorMalha.Pes(0.15);

    public static ResultadoRamal Executar(Document doc, IReadOnlyList<FamilyInstance> bicos, Pipe subgeral)
    {
        if (bicos.Count == 0) return new ResultadoRamal("nenhum bico selecionado", null, null);
        using var grupo = new TransactionGroup(doc, "SPK — Traçar ramal");
        grupo.Start();
        Pipe? ramal = null;
        var recusa = TransacaoSegura.Executar(doc, "SPK — Traçar ramal", () => Tracar(doc, bicos, subgeral, out ramal));
        if (recusa is not null)
        {
            grupo.RollBack();
            return new ResultadoRamal(recusa, null, null);
        }
        var ligacao = LigarBicos.Executar(doc, bicos, [ramal!]);
        grupo.Assimilate();
        return new ResultadoRamal(null, ramal, ligacao);
    }

    static string? Tracar(Document doc, IReadOnlyList<FamilyInstance> bicos, Pipe subgeral, out Pipe? ramal)
    {
        ramal = null;
        if (subgeral.Location is not LocationCurve { Curve: Line eixo } || Math.Abs(eixo.Direction.Z) > 0.05)
            return "a subgeral precisa ser um tubo reto e horizontal";
        var pontos = bicos.Select(b => (b.Location as LocationPoint)?.Point).OfType<XYZ>().ToList();
        if (pontos.Count != bicos.Count) return "bico sem posição";

        // Reta dos bicos em planta: o eixo principal da nuvem de pontos (com um bico só, perpendicular à subgeral), passando
        // pela mediana do deslocamento lateral: um bico de lado não puxa o ramal para fora da fileira.
        var media = new XYZ(pontos.Average(p => p.X), pontos.Average(p => p.Y), 0);
        var direcao = pontos.Count > 1 ? EixoPrincipal(pontos, media) : new XYZ(-eixo.Direction.Y, eixo.Direction.X, 0);
        var normal = new XYZ(-direcao.Y, direcao.X, 0);
        var laterais = pontos.Select(p => (p.X - media.X) * normal.X + (p.Y - media.Y) * normal.Y).OrderBy(v => v).ToList();
        var mediana = laterais.Count % 2 == 1 ? laterais[laterais.Count / 2]
            : (laterais[laterais.Count / 2 - 1] + laterais[laterais.Count / 2]) / 2;
        var centro = media + normal * mediana;
        var seno = Math.Abs(direcao.X * eixo.Direction.Y - direcao.Y * eixo.Direction.X);
        if (seno < 0.5) return "a fileira de bicos está quase paralela à subgeral: o ramal tem de cruzar a subgeral";

        // Cruzamento da reta dos bicos com a subgeral, em planta, na altura da subgeral.
        var a = eixo.GetEndPoint(0);
        var d = eixo.Direction;
        var denominador = direcao.X * d.Y - direcao.Y * d.X;
        var s = ((a.X - centro.X) * d.Y - (a.Y - centro.Y) * d.X) / denominador;
        var cruzamento = new XYZ(centro.X + direcao.X * s, centro.Y + direcao.Y * s, 0);
        var aoLongo = (new XYZ(cruzamento.X, cruzamento.Y, a.Z) - a).DotProduct(d);
        if (aoLongo < MargemPonta || aoLongo > eixo.Length - MargemPonta)
            return "a fileira de bicos não cruza a subgeral selecionada (ou cruza perto da ponta dela)";
        var inicio = a + d * aoLongo;

        // Os bicos têm de ficar todos do mesmo lado da subgeral; o ramal vai do cruzamento até o último deles.
        var posicoes = pontos.Select(p => (p.X - inicio.X) * direcao.X + (p.Y - inicio.Y) * direcao.Y).ToList();
        if (posicoes.All(t => t < 0))
        {
            direcao = direcao.Negate();
            posicoes = posicoes.Select(t => -t).ToList();
        }
        if (posicoes.Any(t => t < MargemPonta))
            return "há bicos dos dois lados da subgeral (ou em cima dela): trace uma fileira de cada lado";
        var fim = inicio + direcao * posicoes.Max();
        if (pontos.Any(p => p.Z > inicio.Z - GeradorMalha.Pes(0.05)))
            return "bicos na altura da subgeral ou acima (bico em pé): ainda não suportado";

        var sistema = subgeral.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId() ?? ElementId.InvalidElementId;
        if (sistema == ElementId.InvalidElementId) return "a subgeral não tem tipo de sistema";
        var diametro = GeradorMalha.DiametroComTe(doc, subgeral.PipeType, DadosEsboco.Carregar(doc).DnTubo) ?? subgeral.Diameter;
        var nivel = subgeral.ReferenceLevel;

        // Parte a subgeral no cruzamento e põe o tê com o ramal novo.
        var outroPedaco = doc.GetElement(PlumbingUtils.BreakCurve(doc, subgeral.Id, inicio)) as Pipe;
        ramal = GeradorMalha.CriarTubo(doc, sistema, subgeral.PipeType.Id, nivel.Id, inicio, fim, diametro, marcar: false);
        doc.Regenerate();
        var noTe = new[] { subgeral, outroPedaco, ramal }.Where(t => t is { IsValidObject: true })
            .SelectMany(t => t!.ConnectorManager.Connectors.Cast<Connector>())
            .Where(c => c.ConnectorType == ConnectorType.End && !c.IsConnected && c.Origin.DistanceTo(inicio) < GeradorMalha.Pes(0.04))
            .ToList();
        if (noTe.Count != 3) return "a subgeral não se partiu como esperado (já há peça nesse ponto?)";
        foreach (var c in noTe) GeradorMalha.Recuar(c, GeradorMalha.Pes(0.01));
        doc.Regenerate();
        noTe = new[] { subgeral, outroPedaco, ramal }.Where(t => t is { IsValidObject: true })
            .SelectMany(t => t!.ConnectorManager.Connectors.Cast<Connector>())
            .Where(c => c.ConnectorType == ConnectorType.End && !c.IsConnected && c.Origin.DistanceTo(inicio) < GeradorMalha.Pes(0.04))
            .ToList();
        if (!GeradorMalha.TentarInserirTe(doc, noTe))
            GeradorMalha.InserirTePorFamilia(doc, inicio, nivel, GeradorMalha.SimboloDeTe(doc), noTe);
        return null;
    }

    /// <summary>Direção, em planta, em que os pontos mais se espalham (eixo principal da covariância).</summary>
    static XYZ EixoPrincipal(List<XYZ> pontos, XYZ centro)
    {
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var p in pontos)
        {
            double x = p.X - centro.X, y = p.Y - centro.Y;
            sxx += x * x;
            syy += y * y;
            sxy += x * y;
        }
        var angulo = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        return new XYZ(Math.Cos(angulo), Math.Sin(angulo), 0);
    }
}
