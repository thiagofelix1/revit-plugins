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

    /// <summary>Número da primeira folha do SPK, com os zeros que o escritório usa ("09"). Vazio: SPK-SETORES, SPK-ISO...</summary>
    public string? NumeroInicial { get; set; }

    /// <summary>Molde do código da prancha: {numero}, {numero:000} e {tipo} (PLA ou ISO). Ex.: NEPN-INC-{numero:000}-LG-SPK-{tipo}-R00.</summary>
    public string? CodigoFolha { get; set; }

    /// <summary>Parâmetro de texto da folha onde vai o código ("Código de Projeto" na Nova Era).</summary>
    public string? ParametroCodigo { get; set; }

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

    /// <summary>Lista pronta, não consulta preguiçosa: quem coloca as legendas cria viewports no meio do laço.</summary>
    public List<View> VistasDeLegenda(Document doc)
    {
        var nomes = Legendas.ToHashSet(StringComparer.Ordinal);
        return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && v.ViewType == ViewType.Legend && nomes.Contains(v.Name))
            .ToList();
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
        linhas.Add(string.IsNullOrWhiteSpace(NumeroInicial) ? "Folhas numeradas SPK-…" : $"Folhas a partir do número {NumeroInicial}");
        if (!string.IsNullOrWhiteSpace(CodigoFolha))
            linhas.Add($"Código das folhas: {CodigoFolha} em \"{ParametroCodigo}\"");
        return string.Join("\n", linhas);
    }
}
