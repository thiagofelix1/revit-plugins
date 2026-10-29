using Autodesk.Revit.DB.Plumbing;
using MechanicalFitting = Autodesk.Revit.DB.Mechanical.MechanicalFitting;

namespace Spk.AddIn.Folhas;

/// <summary>O resumo da lista: metros de tubo por DN, conexões por tipo, bicos e acessórios, e as tabelas criadas.</summary>
public sealed record ResumoMaterial(
    IReadOnlyList<(int Dn, double Metros, int Tubos)> Tubos,
    IReadOnlyList<(string Peca, int Quantidade)> Conexoes,
    IReadOnlyList<(string Tipo, int Quantidade)> Bicos,
    IReadOnlyList<(string Tipo, int Quantidade)> Acessorios,
    IReadOnlyList<string> TabelasCriadas)
{
    public string Texto()
    {
        var linhas = new List<string> { "Tubos:" };
        linhas.AddRange(Tubos.Select(t => $"• DN {t.Dn}: {t.Metros:0.0} m ({t.Tubos} trecho(s))"));
        linhas.Add($"  Total: {Tubos.Sum(t => t.Metros):0.0} m");
        linhas.Add("Conexões: " + (Conexoes.Count == 0 ? "nenhuma" : string.Join(", ", Conexoes.Select(c => $"{c.Quantidade} {c.Peca}"))));
        linhas.Add("Bicos: " + (Bicos.Count == 0 ? "nenhum" : string.Join(", ", Bicos.Select(b => $"{b.Quantidade} {b.Tipo}"))));
        if (Acessorios.Count > 0) linhas.Add("Válvulas e acessórios: " + string.Join(", ", Acessorios.Select(a => $"{a.Quantidade} {a.Tipo}")));
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Lista de material da rede: tabelas do Revit (quantitativos) de tubos por tipo e diâmetro, conexões, bicos e
///     válvulas, com o nome "SPK — ...". São tabelas comuns: atualizam sozinhas quando a rede muda, vão para uma folha
///     como qualquer tabela e exportam pelo menu do Revit. Se já existem, ficam como estão (o escritório pode ter mexido
///     nelas). O resumo em texto sai na hora, para conferir sem abrir as tabelas.
/// </summary>
static class ListaDeMaterial
{
    const double MmPorPe = 304.8;

    public const string NomeTubos = "SPK — Tubos por diâmetro";

    public static ResumoMaterial Gerar(Document doc)
    {
        var criadas = new List<string>();
        using (var t = new Transaction(doc, "SPK — Lista de material"))
        {
            t.Start();
            if (Tabela(doc, NomeTubos, BuiltInCategory.OST_PipeCurves,
                    [(BuiltInParameter.ELEM_TYPE_PARAM, "Tipo", true, false), (BuiltInParameter.RBS_PIPE_DIAMETER_PARAM, "Diâmetro", true, false),
                     (BuiltInParameter.CURVE_ELEM_LENGTH, "Comprimento", false, true)])) criadas.Add(NomeTubos);
            if (Tabela(doc, "SPK — Conexões", BuiltInCategory.OST_PipeFitting,
                    [(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM, "Peça", true, false), (BuiltInParameter.RBS_CALCULATED_SIZE, "Tamanho", true, false)]))
                criadas.Add("SPK — Conexões");
            if (Tabela(doc, "SPK — Bicos", BuiltInCategory.OST_Sprinklers,
                    [(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM, "Bico", true, false)])) criadas.Add("SPK — Bicos");
            if (Tabela(doc, "SPK — Válvulas e acessórios", BuiltInCategory.OST_PipeAccessory,
                    [(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM, "Peça", true, false), (BuiltInParameter.RBS_CALCULATED_SIZE, "Tamanho", true, false)]))
                criadas.Add("SPK — Válvulas e acessórios");
            t.Commit();
        }

        var tubos = new FilteredElementCollector(doc).OfClass(typeof(Pipe)).Cast<Pipe>()
            .Where(p => p.Location is LocationCurve)
            .GroupBy(p => (int)Math.Round(p.Diameter * MmPorPe))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Sum(p => ((LocationCurve)p.Location).Curve.Length) * MmPorPe / 1000, g.Count()))
            .ToList();
        var conexoes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeFitting).WhereElementIsNotElementType()
            .OfType<FamilyInstance>()
            .GroupBy(f => NomeDaPeca(f.MEPModel is MechanicalFitting m ? m.PartType : PartType.Undefined))
            .OrderByDescending(g => g.Count()).Select(g => (g.Key, g.Count())).ToList();
        var bicos = PorTipo(doc, BuiltInCategory.OST_Sprinklers);
        var acessorios = PorTipo(doc, BuiltInCategory.OST_PipeAccessory);
        return new ResumoMaterial(tubos, conexoes, bicos, acessorios, criadas);
    }

    public static ViewSchedule? TabelaDeTubos(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>().FirstOrDefault(v => v.Name == NomeTubos);

    /// <summary>
    ///     Cria a tabela da categoria, agrupada pelos campos marcados e sem listar item a item, com a contagem e o total
    ///     do campo de soma. Falso se ela já existia.
    /// </summary>
    static bool Tabela(Document doc, string nome, BuiltInCategory categoria,
        IReadOnlyList<(BuiltInParameter Parametro, string Titulo, bool Agrupar, bool Somar)> campos)
    {
        if (new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>().Any(v => v.Name == nome)) return false;
        var tabela = ViewSchedule.CreateSchedule(doc, new ElementId(categoria));
        tabela.Name = nome;
        var definicao = tabela.Definition;
        var disponiveis = definicao.GetSchedulableFields();
        foreach (var (parametro, titulo, agrupar, somar) in campos)
        {
            if (disponiveis.FirstOrDefault(f => f.ParameterId == new ElementId(parametro)) is not { } campo) continue;
            var adicionado = definicao.AddField(campo);
            adicionado.ColumnHeading = titulo;
            if (somar) adicionado.DisplayType = ScheduleFieldDisplayType.Totals;
            if (agrupar) definicao.AddSortGroupField(new ScheduleSortGroupField(adicionado.FieldId));
        }
        if (disponiveis.FirstOrDefault(f => f.FieldType == ScheduleFieldType.Count) is { } contagem)
            definicao.AddField(contagem).ColumnHeading = "Quantidade";
        definicao.IsItemized = false;
        definicao.ShowGrandTotal = true;
        definicao.ShowGrandTotalCount = true;
        return true;
    }

    static List<(string, int)> PorTipo(Document doc, BuiltInCategory categoria) =>
        new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsNotElementType().OfType<FamilyInstance>()
            .GroupBy(f => $"{f.Symbol?.Family?.Name} : {f.Symbol?.Name}")
            .OrderByDescending(g => g.Count()).Select(g => (g.Key, g.Count())).ToList();

    static string NomeDaPeca(PartType tipo) => tipo switch
    {
        PartType.Elbow => "cotovelo(s)",
        PartType.Tee => "tê(s)",
        PartType.Cross => "cruzeta(s)",
        PartType.Cap => "tampão(ões)",
        PartType.Transition => "redução(ões)",
        PartType.Union => "luva(s)",
        _ => "outra(s) peça(s)",
    };
}
