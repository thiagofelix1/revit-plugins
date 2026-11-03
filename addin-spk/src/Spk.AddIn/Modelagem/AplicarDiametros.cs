using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Spk.Motor.Calculo;

namespace Spk.AddIn.Modelagem;

/// <summary>Quantos tubos mudaram, quantos já estavam certos, por que os outros não, e os que ficaram diferentes do pedido.</summary>
public sealed record ResultadoDiametros(
    int Mudados,
    int JaCertos,
    IReadOnlyList<string> Recusados,
    IReadOnlyList<string> Divergentes,
    IReadOnlyDictionary<int, int> TubosPorDn,
    IReadOnlyList<ElementId> Alterados,
    IReadOnlyList<string> Inseridas)
{
    public string Texto(int maximo = 10)
    {
        var linhas = new List<string>
        {
            $"{Mudados} tubo(s) mudaram de diâmetro; {JaCertos} já estavam no DN da tabela.",
            "Tubos por DN: " + string.Join(", ", TubosPorDn.OrderBy(p => p.Key).Select(p => $"DN {p.Key}: {p.Value}")),
        };
        if (Recusados.Count > 0)
        {
            linhas.Add($"{Recusados.Count} ficaram como estavam:");
            linhas.AddRange(Recusados.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(maximo)
                .Select(g => $"• {g.Key}{(g.Count() > 1 ? $" ({g.Count()})" : "")}"));
        }
        if (Inseridas.Count > 0)
            linhas.Add($"O Revit pôs {Inseridas.Count} peça(s) nova(s) entre tubos para casar os tamanhos: " +
                       string.Join(", ", Inseridas.GroupBy(n => n).Select(g => $"{g.Count()}× {g.Key}")) + ". Confira no modelo.");
        if (Divergentes.Count > 0)
        {
            linhas.Add($"{Divergentes.Count} terminaram com outro diâmetro (o Revit ajustou ao mudar o vizinho):");
            linhas.AddRange(Divergentes.Take(maximo).Select(d => "• " + d));
        }
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Põe nos tubos o DN que o dimensionamento por tabela pediu para os trechos deles. Um tubo com mais de um trecho
///     (bicos ligados por proximidade ao longo dele) fica com o maior. O DN vira o menor tamanho do segmento do tubo que
///     não fica abaixo do pedido (o DN 90 da tabela vira 100 se o segmento não tem 90). Todos os tubos mudam juntos,
///     antes de o Revit regenerar: assim o cotovelo vê os dois lados no DN novo e muda junto, em vez de ganhar uma redução
///     de cada lado. Se o Revit recusar ou algum tubo perder ligação, volta atrás e muda um tubo por vez, cada um numa
///     transação própria. Tudo num grupo, um Ctrl+Z desfaz.
/// </summary>
static class AplicarDiametros
{
    /// <summary>O DN pedido para cada tubo do modelo: o maior dos trechos dele.</summary>
    public static Dictionary<ElementId, int> Pedido(LeitorRede.RedeLida lida, DiametrosPorTabela dimensionamento)
    {
        var pedido = new Dictionary<ElementId, int>();
        foreach (var (trecho, dn) in dimensionamento.DnPorTrecho)
            if (lida.Tubos.TryGetValue(trecho, out var id))
                pedido[id] = Math.Max(dn, pedido.TryGetValue(id, out var outro) ? outro : 0);
        return pedido;
    }

    public static ResultadoDiametros Executar(Document doc, LeitorRede.RedeLida lida, DiametrosPorTabela dimensionamento)
    {
        var pedido = Pedido(lida, dimensionamento);

        var recusados = new List<string>();
        var alterados = new List<ElementId>();
        var alvo = new Dictionary<ElementId, double>();
        var jaCertos = 0;
        var mudar = new List<(Pipe Tubo, double Pes)>();
        var pecasAntes = PorFamilia(doc, Pecas(doc));
        using var grupo = new TransactionGroup(doc, "SPK — Diâmetros por tabela");
        grupo.Start();
        foreach (var (id, dn) in pedido.OrderBy(p => p.Key.Value))
        {
            if (doc.GetElement(id) is not Pipe tubo)
            {
                recusados.Add("tubo de vínculo ou apagado");
                continue;
            }
            if (Tamanho(doc, tubo, dn) is not { } pes)
            {
                recusados.Add($"o tipo de tubo {GeradorMalha.NomeDoTipo(doc, tubo.PipeType)} não tem DN {dn} ou maior");
                continue;
            }
            alvo[id] = pes;
            if (Math.Abs(Diametro(tubo) - pes) < 1e-6)
            {
                jaCertos++;
                continue;
            }
            mudar.Add((tubo, pes));
        }

        if (mudar.Count > 0 && TransacaoSegura.Executar(doc, "SPK — Diâmetros dos tubos", () => MudarTodos(doc, mudar, alvo)) is null)
        {
            alterados.AddRange(mudar.Select(m => m.Tubo.Id));
        }
        else
        {
            foreach (var (tubo, pes) in mudar)
            {
                var motivo = TransacaoSegura.Executar(doc, "SPK — Diâmetro do tubo", () => Mudar(doc, tubo, pes));
                if (motivo is null) alterados.Add(tubo.Id);
                else recusados.Add(motivo);
            }
        }
        grupo.Assimilate();
        // Saldo por família: a luva do bico que o Revit troca pela do DN novo, ou o cotovelo refeito, não contam.
        var depois = PorFamilia(doc, Pecas(doc));
        var antes = pecasAntes;
        var inseridas = depois.SelectMany(p => Enumerable.Repeat(p.Key, p.Value - antes.GetValueOrDefault(p.Key))).ToList();

        // Mudar um tubo pode redimensionar a peça ao lado e, por ela, o tubo vizinho: confere todos no fim.
        var divergentes = new List<string>();
        var porDn = new Dictionary<int, int>();
        foreach (var (id, pes) in alvo)
        {
            if (doc.GetElement(id) is not Pipe tubo) continue;
            var final = Diametro(tubo);
            var mm = (int)Math.Round(UnitUtils.ConvertFromInternalUnits(final, UnitTypeId.Millimeters));
            porDn[mm] = porDn.TryGetValue(mm, out var n) ? n + 1 : 1;
            if (Math.Abs(final - pes) > 1e-6)
                divergentes.Add($"tubo {id.Value}: pedido DN {Mm(pes)}, ficou DN {mm}");
        }
        return new ResultadoDiametros(alterados.Count, jaCertos, recusados, divergentes, porDn, alterados, inseridas);
    }

    /// <summary>
    /// Muda todos de uma vez. O Revit não muda o tamanho de um cotovelo ou de uma luva junto com os tubos: põe uma
    /// redução de cada lado. Então a peça entre dois tubos que vão para o mesmo DN sai antes e volta depois, no DN novo.
    /// </summary>
    static string? MudarTodos(Document doc, List<(Pipe Tubo, double Pes)> mudar, Dictionary<ElementId, double> alvo)
    {
        var mudando = mudar.Select(m => m.Tubo.Id).ToHashSet();
        var refazer = new List<(PartType Tipo, XYZ Ponto, Pipe A, Pipe B)>();
        var apagar = new List<ElementId>();
        foreach (var peca in mudar.SelectMany(m => Vizinhas(m.Tubo)).GroupBy(p => p.Id).Select(g => g.First()))
        {
            if (peca.MEPModel is not MechanicalFitting { PartType: PartType.Elbow or PartType.Union } conexao) continue;
            var tubos = Conectores(peca).SelectMany(c => c.AllRefs.Cast<Connector>()).Select(r => r.Owner).OfType<Pipe>()
                .GroupBy(t => t.Id).Select(g => g.First()).ToList();
            if (tubos.Count != 2 || !tubos.Any(t => mudando.Contains(t.Id))) continue;
            if (!alvo.TryGetValue(tubos[0].Id, out var a) || !alvo.TryGetValue(tubos[1].Id, out var b) || Math.Abs(a - b) > 1e-6) continue;
            refazer.Add((conexao.PartType, ((LocationPoint)peca.Location).Point, tubos[0], tubos[1]));
            apagar.Add(peca.Id);
        }

        var antes = mudar.Select(m => Ligados(m.Tubo)).ToList();
        if (apagar.Count > 0) doc.Delete(apagar);
        foreach (var (tubo, pes) in mudar) tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(pes);
        doc.Regenerate();
        foreach (var (tipo, ponto, a, b) in refazer)
        {
            if (Solta(a, ponto) is not { } ca || Solta(b, ponto) is not { } cb) return "a peça entre dois tubos não pôde ser refeita";
            if (tipo == PartType.Elbow) doc.Create.NewElbowFitting(ca, cb);
            else doc.Create.NewUnionFitting(ca, cb);
        }
        doc.Regenerate();
        for (var i = 0; i < mudar.Count; i++)
            if (Ligados(mudar[i].Tubo) < antes[i]) return "um tubo perderia ligação";
        return null;
    }

    /// <summary>As peças ligadas direto ao tubo.</summary>
    static List<FamilyInstance> Vizinhas(Pipe tubo) =>
        tubo.ConnectorManager.Connectors.Cast<Connector>().SelectMany(c => c.AllRefs.Cast<Connector>())
            .Select(r => r.Owner).OfType<FamilyInstance>().Where(p => p.Id != tubo.Id).ToList();

    static IEnumerable<Connector> Conectores(FamilyInstance peca) =>
        peca.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().Where(c => c.Domain == Domain.DomainPiping) ?? [];

    /// <summary>A ponta sem ligação do tubo mais perto do ponto.</summary>
    static Connector? Solta(Pipe tubo, XYZ ponto) =>
        tubo.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End && !c.IsConnected)
            .OrderBy(c => c.Origin.DistanceTo(ponto)).FirstOrDefault();

    /// <summary>Peças por família, menos as ligadas a bico: a luva entre o tubo e o bico muda com o DN do tubo, é esperada.</summary>
    static Dictionary<string, int> PorFamilia(Document doc, IEnumerable<ElementId> ids) =>
        ids.Select(doc.GetElement).OfType<FamilyInstance>()
            .Where(p => !Conectores(p).SelectMany(c => c.AllRefs.Cast<Connector>())
                .Any(r => r.Owner.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers))
            .GroupBy(p => p.Symbol?.FamilyName ?? "peça")
            .ToDictionary(g => g.Key, g => g.Count());

    static HashSet<ElementId> Pecas(Document doc) =>
        new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeFitting).WhereElementIsNotElementType().ToElementIds().ToHashSet();

    static string? Mudar(Document doc, Pipe tubo, double pes)
    {
        var ligadosAntes = Ligados(tubo);
        tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(pes);
        doc.Regenerate();
        if (Ligados(tubo) < ligadosAntes) return "o tubo perderia ligação com a peça vizinha (a peça não tem o DN novo)";
        return Math.Abs(Diametro(tubo) - pes) > 1e-6 ? "o Revit não aceitou o DN novo" : null;
    }

    /// <summary>
    /// O menor tamanho do tipo do tubo com DN nominal maior ou igual ao pedido. Pelo tipo, não pelo segmento do tubo: um tubo
    /// com um diâmetro que o segmento não tem fica sem segmento no Revit, e é justamente ele que precisa de um DN válido.
    /// </summary>
    static double? Tamanho(Document doc, Pipe tubo, int dnMm)
    {
        var pedido = UnitUtils.ConvertToInternalUnits(dnMm, UnitTypeId.Millimeters);
        var tamanhos = GeradorMalha.Tamanhos(doc, tubo.PipeType);
        if (tamanhos.Count == 0 && tubo.PipeSegment is { } segmento) tamanhos = segmento.GetSizes().Select(s => s.NominalDiameter).OrderBy(d => d).ToList();
        return tamanhos.Where(d => d >= pedido - 1e-6).Cast<double?>().FirstOrDefault();
    }

    static double Diametro(Pipe tubo) => tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();

    static int Mm(double pes) => (int)Math.Round(UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Millimeters));

    static int Ligados(Pipe tubo) => tubo.ConnectorManager.Connectors.Cast<Connector>().Count(c => c.IsConnected);
}
