using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Eletrica.Motor.Relatorios;

/// <summary>
///     Peças de documento Word em OpenXML puro (sem Word nem LibreOffice em quem roda, inclusive dentro do Revit): título,
///     subtítulo, parágrafo, tabela, A4 retrato. Usadas pelo memorial e pelo manual do usuário. Mesma receita do motor SPK.
/// </summary>
public static class Docx
{
    public const string CorTitulo = "1F4E79";
    public const string CorFundoCabecalho = "DDEBF7";
    public const string CorAtende = "2E7D32";
    public const string CorNaoAtende = "C62828";

    public static byte[] Documento(Action<Body> escrever, bool paisagem = false)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            escrever(body);
            body.AppendChild(new SectionProperties(
                paisagem ? new PageSize { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape } : new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 850, Right = 850, Bottom = 850, Left = 850 }));
        }
        return stream.ToArray();
    }

    public static Paragraph Titulo(string texto) => new(
        new ParagraphProperties(new SpacingBetweenLines { After = "200" }),
        new Run(new RunProperties(new Bold(), new FontSize { Val = "40" }, new Color { Val = CorTitulo }), new Text(texto)));

    public static Paragraph Subtitulo(string texto) => new(
        new ParagraphProperties(new SpacingBetweenLines { Before = "300", After = "120" }),
        new Run(new RunProperties(new Bold(), new FontSize { Val = "28" }, new Color { Val = CorTitulo }), new Text(texto)));

    public static Paragraph Paragrafo(string texto, bool negrito = false, bool italico = false, string? cor = null, int? tamanho = null)
    {
        var props = new List<OpenXmlElement>();
        if (negrito) props.Add(new Bold());
        if (italico) props.Add(new Italic());
        if (cor != null) props.Add(new Color { Val = cor });
        if (tamanho is { } t) props.Add(new FontSize { Val = (t * 2).ToString() });
        return new Paragraph(new Run(new RunProperties(props.ToArray()), new Text(texto) { Space = SpaceProcessingModeValues.Preserve }));
    }

    public static Paragraph Item(string texto) => new(
        new ParagraphProperties(new Indentation { Left = "360", Hanging = "240" }),
        new Run(new Text("• " + texto) { Space = SpaceProcessingModeValues.Preserve }));

    public static Table Tabela(IReadOnlyList<string> cabecalhos, IEnumerable<IReadOnlyList<string>> linhas, bool fonteMenor = false)
    {
        var tabela = new Table(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }));
        tabela.AppendChild(Linha(cabecalhos, cabecalho: true, fonteMenor));
        foreach (var linha in linhas) tabela.AppendChild(Linha(linha, cabecalho: false, fonteMenor));
        return tabela;
    }

    static TableRow Linha(IEnumerable<string> celulas, bool cabecalho, bool fonteMenor)
    {
        var linha = new TableRow();
        foreach (var texto in celulas)
        {
            var runProps = new List<OpenXmlElement> { new FontSize { Val = fonteMenor ? "16" : "20" } };
            if (cabecalho) runProps.Add(new Bold());
            var negrito = !cabecalho && texto.StartsWith("**") && texto.EndsWith("**") && texto.Length > 4;
            if (negrito) runProps.Add(new Bold());
            var limpo = negrito ? texto.Substring(2, texto.Length - 4) : texto;
            linha.AppendChild(new TableCell(
                new TableCellProperties(new Shading { Fill = cabecalho ? CorFundoCabecalho : "FFFFFF", Val = ShadingPatternValues.Clear }),
                new Paragraph(new Run(new RunProperties(runProps.ToArray()), new Text(limpo) { Space = SpaceProcessingModeValues.Preserve }))));
        }
        return linha;
    }
}
