namespace Spk.AddIn;

/// <summary>
///     Padrão do escritório gravado no projeto: carimbo, etiqueta de tubo, templates por tipo de vista e legendas que as
///     folhas usam no lugar do estilo do plugin. Guarda nomes, não ids, porque os elementos vêm de outro arquivo e o
///     engenheiro pode recarregar a família sem perder a escolha.
/// </summary>
public sealed class PadraoEscritorio
{
    public const string Chave = "Padrao";

    /// <summary>"Família : Tipo" do carimbo.</summary>
    public string? Carimbo { get; set; }

    /// <summary>"Família : Tipo" da etiqueta de tubo (diâmetro, ou diâmetro e comprimento).</summary>
    public string? EtiquetaTubo { get; set; }

    public string? TemplatePlanta { get; set; }
    public string? Template3d { get; set; }
    public string? TemplateCorte { get; set; }
    public string? TemplateSetorizacao { get; set; }

    /// <summary>Vistas de legenda (símbolos, notas) colocadas nas folhas do setor e no isométrico.</summary>
    public List<string> Legendas { get; set; } = [];

    /// <summary>De qual arquivo o padrão foi importado, para o resumo.</summary>
    public string? Origem { get; set; }

    public static PadraoEscritorio Carregar(Document doc) => ArmazenamentoSpk.Ler<PadraoEscritorio>(doc, Chave) ?? new PadraoEscritorio();

    public void Salvar(Document doc) => ArmazenamentoSpk.Gravar(doc, Chave, this);

    public static string Nome(FamilySymbol simbolo) => $"{simbolo.FamilyName} : {simbolo.Name}";

    /// <summary>O tipo de família da categoria com este "Família : Tipo", ou inválido se não houver.</summary>
    public static ElementId Tipo(Document doc, BuiltInCategory categoria, string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return ElementId.InvalidElementId;
        return new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsElementType()
                   .OfType<FamilySymbol>().FirstOrDefault(s => Nome(s) == nome)?.Id
               ?? ElementId.InvalidElementId;
    }

    public IEnumerable<View> VistasDeLegenda(Document doc)
    {
        var nomes = Legendas.ToHashSet(StringComparer.Ordinal);
        return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && v.ViewType == ViewType.Legend && nomes.Contains(v.Name));
    }

    public string Resumo()
    {
        var linhas = new List<string>();
        if (Origem is not null) linhas.Add($"Importado de {Origem}.");
        linhas.Add($"Carimbo: {Carimbo ?? "o primeiro do projeto"}");
        linhas.Add($"Etiqueta de tubo: {EtiquetaTubo ?? "a padrão do projeto"}");
        linhas.Add($"Templates: planta {TemplatePlanta ?? "—"}; 3D {Template3d ?? "—"}; corte {TemplateCorte ?? "—"}; " +
                   $"setorização {TemplateSetorizacao ?? "—"}");
        linhas.Add(Legendas.Count == 0 ? "Legendas: nenhuma" : "Legendas: " + string.Join(", ", Legendas));
        return string.Join("\n", linhas);
    }
}
