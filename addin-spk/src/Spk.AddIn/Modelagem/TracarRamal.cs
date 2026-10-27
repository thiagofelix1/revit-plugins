using Autodesk.Revit.DB.Plumbing;

namespace Spk.AddIn.Modelagem;

public enum AlturaDoRamal { NaSubgeral, RelativaASubgeral, RelativaAoPiso }

/// <summary>Como traçar: a altura do ramal, quando dois bicos são da mesma fileira e se os bicos são ligados.</summary>
public sealed record OpcoesRamais
{
    public AlturaDoRamal Altura { get; init; } = AlturaDoRamal.NaSubgeral;

    /// <summary>Metros acima da subgeral (negativo: abaixo) ou acima do nível da subgeral, conforme <see cref="Altura" />.</summary>
    public double AlturaM { get; init; }

    /// <summary>Bicos até esta distância (m) um do outro, medida ao longo da subgeral, são da mesma fileira.</summary>
    public double ToleranciaFileiraM { get; init; } = 1.0;

    /// <summary>Falso: só os ramais, sem ligar os bicos (para ligar depois com Ligar bicos).</summary>
    public bool LigarBicos { get; init; } = true;
}

/// <summary>Uma fileira: quantos bicos, por que o ramal não foi traçado, e a ligação dos bicos.</summary>
public sealed record RamalTracado(int Bicos, string? Recusa, ResultadoLigacao? Ligacao);

public sealed record ResultadoRamais(IReadOnlyList<RamalTracado> Fileiras, IReadOnlyList<ElementId> Criados)
{
    public int Tracados => Fileiras.Count(f => f.Recusa is null);
    public int Ligados => Fileiras.Sum(f => f.Ligacao?.Ligados ?? 0);

    /// <summary>Por que cada bico não foi ligado: o motivo da fileira inteira ou o do bico.</summary>
    public IEnumerable<string> Recusados => Fileiras.SelectMany(f => f.Recusa is not null
        ? Enumerable.Repeat("ramal não traçado: " + f.Recusa, f.Bicos)
        : f.Ligacao?.Recusados ?? []);

    public string Texto()
    {
        var linhas = new List<string> { $"{Tracados} de {Fileiras.Count} ramal(is) traçado(s); {Ligados} bico(s) ligado(s)." };
        for (var i = 0; i < Fileiras.Count; i++)
            if (Fileiras[i].Recusa is { } recusa)
                linhas.Add($"• Fileira {i + 1} ({Fileiras[i].Bicos} bicos): {recusa}");
        var bicos = Fileiras.Where(f => f.Recusa is null).SelectMany(f => f.Ligacao?.Recusados ?? []).ToList();
        if (bicos.Count > 0)
        {
            linhas.Add($"{bicos.Count} bico(s) ficaram sem ligar:");
            linhas.AddRange(bicos.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(10)
                .Select(g => $"• {g.Key}{(g.Count() > 1 ? $" ({g.Count()})" : "")}"));
        }
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Traça os ramais de várias fileiras de bicos até a subgeral e liga os bicos a eles. As fileiras saem dos bicos
///     selecionados: bicos próximos ao longo da subgeral são da mesma fileira. Cada ramal segue a reta que melhor passa
///     pelos bicos da fileira, em planta, e cruza a subgeral; perto da perpendicular à subgeral, sai perpendicular. Na altura da subgeral, a subgeral é partida ali e ganha um
///     tê (bicos de um lado) ou uma cruzeta (dos dois lados). Em outra altura, um tubo vertical curto (a subida, o "riser
///     nipple") liga o tê da subgeral ao ramal, com cotovelo (um lado) ou tê (o ramal passa por cima da subgeral). Os
///     bicos são ligados pelo Ligar bicos. Diâmetro do ramal: o DN de Dados do esboço (depois, Diâmetros). Cada fileira
///     numa transação própria; tudo num grupo: um Ctrl+Z desfaz.
/// </summary>
public static class TracarRamal
{
    static readonly double MargemPonta = GeradorMalha.Pes(0.15);
    static readonly double Encontro = GeradorMalha.Pes(0.04);
    static readonly double Recuo = GeradorMalha.Pes(0.01);
    static readonly double SubidaMinima = GeradorMalha.Pes(0.15);

    public static ResultadoRamais Executar(Document doc, IReadOnlyList<FamilyInstance> bicos, Pipe subgeral, OpcoesRamais opcoes)
    {
        if (subgeral.Location is not LocationCurve { Curve: Line eixo } || Math.Abs(eixo.Direction.Z) > 0.05)
            return new ResultadoRamais([new RamalTracado(bicos.Count, "a subgeral precisa ser um tubo reto e horizontal", null)], []);

        var fileiras = Fileiras(bicos, eixo, GeradorMalha.Pes(opcoes.ToleranciaFileiraM));
        var pedacos = new List<Pipe> { subgeral };
        var criados = new List<ElementId>();
        var tracados = new List<RamalTracado>();
        using var grupo = new TransactionGroup(doc, "SPK — Traçar ramais");
        grupo.Start();
        foreach (var fileira in fileiras)
        {
            var ramais = new List<Pipe>();
            var novos = new List<Pipe>();
            var recusa = TransacaoSegura.Executar(doc, "SPK — Traçar ramal",
                () => Tracar(doc, fileira, pedacos, eixo, opcoes, ramais, novos));
            if (recusa is not null)
            {
                tracados.Add(new RamalTracado(fileira.Count, recusa, null));
                continue;
            }
            pedacos.AddRange(novos.Where(t => EhDaSubgeral(t, eixo)));
            criados.AddRange(novos.Where(t => !EhDaSubgeral(t, eixo)).Select(t => t.Id));
            var ligacao = opcoes.LigarBicos ? LigarBicos.Executar(doc, fileira, ramais) : null;
            if (ligacao is not null) criados.AddRange(ligacao.Criados);
            tracados.Add(new RamalTracado(fileira.Count, null, ligacao));
        }
        grupo.Assimilate();
        return new ResultadoRamais(tracados, criados);
    }

    /// <summary>Fileiras: os bicos ordenados ao longo da subgeral, partidos onde o vão passa da tolerância.</summary>
    static List<List<FamilyInstance>> Fileiras(IReadOnlyList<FamilyInstance> bicos, Line eixo, double tolerancia)
    {
        var a = eixo.GetEndPoint(0);
        var d = eixo.Direction;
        var ordenados = bicos.Where(b => b.Location is LocationPoint)
            .Select(b => (Bico: b, S: (((LocationPoint)b.Location).Point - a).DotProduct(d)))
            .OrderBy(x => x.S).ToList();
        var fileiras = new List<List<FamilyInstance>>();
        for (var i = 0; i < ordenados.Count; i++)
        {
            if (i == 0 || ordenados[i].S - ordenados[i - 1].S > tolerancia) fileiras.Add([]);
            fileiras[^1].Add(ordenados[i].Bico);
        }
        return fileiras;
    }

    static string? Tracar(Document doc, List<FamilyInstance> bicos, List<Pipe> pedacos, Line eixo, OpcoesRamais opcoes,
        List<Pipe> ramais, List<Pipe> novos)
    {
        var pontos = bicos.Select(b => ((LocationPoint)b.Location).Point).ToList();

        // Reta dos bicos em planta: o eixo principal da nuvem de pontos (com um bico só, perpendicular à subgeral), passando
        // pela mediana do deslocamento lateral: um bico de lado não puxa o ramal para fora da fileira.
        var media = new XYZ(pontos.Average(p => p.X), pontos.Average(p => p.Y), 0);
        // Perto da perpendicular à subgeral (até 10°), o ramal sai perpendicular: um bico de lado entortaria a reta.
        var perpendicular = new XYZ(-eixo.Direction.Y, eixo.Direction.X, 0);
        var direcao = pontos.Count > 1 ? EixoPrincipal(pontos, media) : perpendicular;
        if (Math.Abs(direcao.DotProduct(perpendicular)) > Math.Cos(10 * Math.PI / 180)) direcao = perpendicular;
        var normal = new XYZ(-direcao.Y, direcao.X, 0);
        var laterais = pontos.Select(p => (p.X - media.X) * normal.X + (p.Y - media.Y) * normal.Y).OrderBy(v => v).ToList();
        var mediana = laterais.Count % 2 == 1 ? laterais[laterais.Count / 2]
            : (laterais[laterais.Count / 2 - 1] + laterais[laterais.Count / 2]) / 2;
        var centro = media + normal * mediana;
        var seno = Math.Abs(direcao.X * eixo.Direction.Y - direcao.Y * eixo.Direction.X);
        if (seno < 0.5) return "a fileira de bicos está quase paralela à subgeral: o ramal tem de cruzar a subgeral";

        // Cruzamento da reta dos bicos com o eixo da subgeral, em planta, e o pedaço da subgeral que passa ali (ramais já
        // traçados partiram a subgeral).
        var a = eixo.GetEndPoint(0);
        var d = eixo.Direction;
        var s = ((a.X - centro.X) * d.Y - (a.Y - centro.Y) * d.X) / (direcao.X * d.Y - direcao.Y * d.X);
        var cruzamento = new XYZ(centro.X + direcao.X * s, centro.Y + direcao.Y * s, 0);
        Pipe? subgeral = null;
        XYZ? inicio = null;
        foreach (var pedaco in pedacos.Where(p => p.IsValidObject))
        {
            if (pedaco.Location is not LocationCurve { Curve: Line linha }) continue;
            var p0 = linha.GetEndPoint(0);
            var t = (new XYZ(cruzamento.X, cruzamento.Y, p0.Z) - p0).DotProduct(linha.Direction);
            if (t < MargemPonta || t > linha.Length - MargemPonta) continue;
            subgeral = pedaco;
            inicio = p0 + linha.Direction * t;
            break;
        }
        if (subgeral is null || inicio is null)
            return "a fileira não cruza a subgeral selecionada (ou cruza perto da ponta dela ou do tê de outro ramal)";

        var posicoes = pontos.Select(p => (p.X - inicio.X) * direcao.X + (p.Y - inicio.Y) * direcao.Y).ToList();
        if (posicoes.Any(t => Math.Abs(t) < MargemPonta))
            return "há bico em cima da subgeral, onde o ramal a cruza";
        var nivel = subgeral.ReferenceLevel;
        var zRamal = opcoes.Altura switch
        {
            AlturaDoRamal.RelativaASubgeral => inicio.Z + GeradorMalha.Pes(opcoes.AlturaM),
            AlturaDoRamal.RelativaAoPiso => nivel.Elevation + GeradorMalha.Pes(opcoes.AlturaM),
            _ => inicio.Z,
        };
        var subida = Math.Abs(zRamal - inicio.Z) > GeradorMalha.Pes(0.02);
        if (subida && Math.Abs(zRamal - inicio.Z) < SubidaMinima)
            return "o ramal fica a menos de 15 cm da subgeral: não cabem o tê e o cotovelo da subida";

        var sistema = subgeral.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId() ?? ElementId.InvalidElementId;
        if (sistema == ElementId.InvalidElementId) return "a subgeral não tem tipo de sistema";
        var tipo = subgeral.PipeType;
        var diametro = GeradorMalha.DiametroComTe(doc, tipo, DadosEsboco.Carregar(doc).DnTubo) ?? subgeral.Diameter;

        // O ramal sai do topo da subida (ou do cruzamento) para cada lado que tem bico, até o último bico.
        var topo = new XYZ(inicio.X, inicio.Y, zRamal);
        if (posicoes.Max() > 0)
            ramais.Add(GeradorMalha.CriarTubo(doc, sistema, tipo.Id, nivel.Id, topo, topo + direcao * posicoes.Max(), diametro, marcar: false));
        if (posicoes.Min() < 0)
            ramais.Add(GeradorMalha.CriarTubo(doc, sistema, tipo.Id, nivel.Id, topo, topo + direcao * posicoes.Min(), diametro, marcar: false));
        novos.AddRange(ramais);

        var outroPedaco = doc.GetElement(PlumbingUtils.BreakCurve(doc, subgeral.Id, inicio)) as Pipe;
        if (outroPedaco is not null) novos.Add(outroPedaco);
        var niple = subida ? GeradorMalha.CriarTubo(doc, sistema, tipo.Id, nivel.Id, inicio, topo, diametro, marcar: false) : null;
        if (niple is not null) novos.Add(niple);
        doc.Regenerate();

        var naSubgeral = new List<Pipe?> { subgeral, outroPedaco };
        naSubgeral.AddRange(niple is not null ? [niple] : ramais);
        if (Juntar(doc, inicio, nivel, naSubgeral) is { } motivo) return motivo;
        if (niple is not null && Juntar(doc, topo, nivel, [niple, .. ramais]) is { } noTopo) return noTopo;
        return null;
    }

    /// <summary>
    /// Liga as pontas soltas destes tubos que se encontram no ponto, com a peça que o número de pontas pede: cotovelo
    /// (2), tê (3) ou cruzeta (4). Os tubos recuam 1 cm e o Revit põe a peça das preferências de roteamento.
    /// </summary>
    static string? Juntar(Document doc, XYZ ponto, Level nivel, IReadOnlyList<Pipe?> tubos)
    {
        var abertos = Abertos(ponto, tubos);
        foreach (var c in abertos) GeradorMalha.Recuar(c, Recuo);
        doc.Regenerate();
        abertos = Abertos(ponto, tubos);
        switch (abertos.Count)
        {
            case 2:
                doc.Create.NewElbowFitting(abertos[0], abertos[1]);
                return null;
            case 3:
                if (!GeradorMalha.TentarInserirTe(doc, abertos))
                    GeradorMalha.InserirTePorFamilia(doc, ponto, nivel, GeradorMalha.SimboloDeTe(doc), abertos);
                return null;
            case 4:
                // Cruzeta: os dois primeiros conectores alinhados entre si, os outros dois também.
                var par = abertos.Skip(1).OrderBy(c => c.CoordinateSystem.BasisZ.DotProduct(abertos[0].CoordinateSystem.BasisZ)).First();
                var outros = abertos.Where(c => !ReferenceEquals(c, abertos[0]) && !ReferenceEquals(c, par)).ToList();
                try
                {
                    doc.Create.NewCrossFitting(abertos[0], par, outros[0], outros[1]);
                    return null;
                }
                catch (Exception e) when (e is Autodesk.Revit.Exceptions.ArgumentException or Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    return "o Revit não pôs a cruzeta (o tipo de tubo não tem cruzeta nas preferências de roteamento?): " +
                           "trace com o ramal acima ou abaixo da subgeral, com subida";
                }
            default:
                return $"o encontro dos tubos ficou com {abertos.Count} pontas soltas, e não 2 a 4 (já havia peça nesse ponto?)";
        }
    }

    static List<Connector> Abertos(XYZ ponto, IReadOnlyList<Pipe?> tubos) =>
        tubos.Where(t => t is { IsValidObject: true })
            .SelectMany(t => t!.ConnectorManager.Connectors.Cast<Connector>())
            .Where(c => c.ConnectorType == ConnectorType.End && !c.IsConnected && c.Origin.DistanceTo(ponto) < Encontro)
            .ToList();

    /// <summary>Tubo na linha da subgeral (um pedaço dela depois de partida), e não ramal ou subida.</summary>
    static bool EhDaSubgeral(Pipe tubo, Line eixo)
    {
        if (!tubo.IsValidObject || tubo.Location is not LocationCurve { Curve: Line linha }) return false;
        if (Math.Abs(linha.Direction.DotProduct(eixo.Direction)) < 0.999) return false;
        var p = linha.GetEndPoint(0);
        var a = eixo.GetEndPoint(0);
        return (p - a).CrossProduct(eixo.Direction).GetLength() < GeradorMalha.Pes(0.02);
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
