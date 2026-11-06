namespace Eletrica.AddIn.Folhas;

/// <summary>
///     Desenho em vistas de desenho (drafting): tabelas, textos, linhas e retângulos medidos em mm no papel, numa escala
///     fixa. É como o plugin produz quadro de cargas, unifilar, prumada, legenda e lista sem depender de famílias de
///     anotação do escritório. Tudo o que o Revit não cria pela API (vista de legenda, tabela com colunas livres) vem daqui.
/// </summary>
static class Desenho
{
    public const int Escala = 50;
    public const string Prefixo = "AE — ";

    /// <summary>mm no papel → pés no modelo da vista de desenho, na escala fixa.</summary>
    public static double Mm(double mm) => UnitUtils.ConvertToInternalUnits(mm * Escala, UnitTypeId.Millimeters);

    /// <summary>
    ///     Nome aceito pelo Revit em vistas, folhas e tabelas: sem \ : { } [ ] | ; &lt; &gt; ? ` ~. Nomes de quadro de outros escritórios
    ///     e textos do plugin ("quadro: unifilar") traziam esses caracteres e o Revit recusava.
    /// </summary>
    public static string NomeValido(string nome)
    {
        const string proibidos = "\\:{}[]|;<>?`~";
        var limpo = new string(nome.Select(c => proibidos.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(limpo) ? "AE" : limpo.Trim();
    }

    public static ViewDrafting NovaVista(Document doc, string nome)
    {
        nome = NomeValido(nome);
        var antiga = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>().FirstOrDefault(v => v.Name == nome);
        if (antiga is not null) doc.Delete(antiga.Id);
        var tipo = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(t => t.ViewFamily == ViewFamily.Drafting);
        var vista = ViewDrafting.Create(doc, tipo.Id);
        vista.Name = nome;
        vista.Scale = Escala;
        return vista;
    }

    public static ElementId TipoDeTexto(Document doc)
    {
        var padrao = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        if (padrao != ElementId.InvalidElementId) return padrao;
        return new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).FirstElementId()
               ?? throw new InvalidOperationException("O projeto não tem tipo de texto.");
    }

    /// <summary>
    ///     Largura de um texto com quebra de linha: a API pede a largura no papel (como sai impressa), não no modelo, e dentro da
    ///     faixa que o tipo de texto admite. Passar a medida do modelo (vezes a escala) estourava o máximo.
    /// </summary>
    public static double LarguraNoPapel(Document doc, ElementId tipoDeTexto, double larguraMm)
    {
        var largura = UnitUtils.ConvertToInternalUnits(larguraMm, UnitTypeId.Millimeters);
        var minimo = TextElement.GetMinimumAllowedWidth(doc, tipoDeTexto);
        var maximo = TextElement.GetMaximumAllowedWidth(doc, tipoDeTexto);
        return Math.Min(Math.Max(largura, minimo), maximo);
    }

    public static TextNote? Texto(Document doc, View vista, double xMm, double yMm, string texto, double? larguraMm = null, bool negrito = false)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var opcoes = new TextNoteOptions(TipoDeTexto(doc)) { HorizontalAlignment = HorizontalTextAlignment.Left };
        var quebras = texto.Trim().Replace("\r\n", "\n").Replace("\n", "\r");
        var posicao = new XYZ(Mm(xMm), Mm(yMm), 0);
        var nota = larguraMm is { } l
            ? TextNote.Create(doc, vista.Id, posicao, LarguraNoPapel(doc, opcoes.TypeId, l), quebras, opcoes)
            : TextNote.Create(doc, vista.Id, posicao, quebras, opcoes);
        if (negrito)
        {
            var formato = nota.GetFormattedText();
            formato.SetBoldStatus(true);
            nota.SetFormattedText(formato);
        }
        return nota;
    }

    public static void Linha(Document doc, View vista, double x1, double y1, double x2, double y2)
    {
        if (Math.Abs(x1 - x2) < 1e-6 && Math.Abs(y1 - y2) < 1e-6) return;
        doc.Create.NewDetailCurve(vista, Line.CreateBound(new XYZ(Mm(x1), Mm(y1), 0), new XYZ(Mm(x2), Mm(y2), 0)));
    }

    public static void Retangulo(Document doc, View vista, double x, double yTopo, double largura, double altura)
    {
        Linha(doc, vista, x, yTopo, x + largura, yTopo);
        Linha(doc, vista, x + largura, yTopo, x + largura, yTopo - altura);
        Linha(doc, vista, x + largura, yTopo - altura, x, yTopo - altura);
        Linha(doc, vista, x, yTopo - altura, x, yTopo);
    }

    public sealed record Coluna(string Titulo, double LarguraMm);

    /// <summary>Tabela com título, cabeçalho e linhas; devolve a cota (mm) logo abaixo dela.</summary>
    public static double Tabela(Document doc, View vista, double xMm, double yTopoMm, string? titulo, IReadOnlyList<Coluna> colunas,
        IEnumerable<IReadOnlyList<string>> linhas, double alturaLinhaMm = 6, double margemMm = 1.2)
    {
        var largura = colunas.Sum(c => c.LarguraMm);
        var y = yTopoMm;
        if (!string.IsNullOrEmpty(titulo))
        {
            Texto(doc, vista, xMm, y, titulo!, largura, negrito: true);
            y -= alturaLinhaMm * 1.3;
        }
        var topoDaTabela = y;
        void LinhaDaTabela(IReadOnlyList<string> celulas, bool negrito)
        {
            var x = xMm;
            for (var k = 0; k < colunas.Count; k++)
            {
                var texto = k < celulas.Count ? celulas[k] : "";
                if (texto.StartsWith("**") && texto.EndsWith("**") && texto.Length > 4) texto = texto.Substring(2, texto.Length - 4);
                Texto(doc, vista, x + margemMm, y - margemMm, texto, colunas[k].LarguraMm - 2 * margemMm, negrito);
                x += colunas[k].LarguraMm;
            }
            y -= alturaLinhaMm;
            Linha(doc, vista, xMm, y, xMm + largura, y);
        }
        Linha(doc, vista, xMm, y, xMm + largura, y);
        LinhaDaTabela(colunas.Select(c => c.Titulo).ToList(), true);
        foreach (var l in linhas) LinhaDaTabela(l, false);
        var x = xMm;
        Linha(doc, vista, x, topoDaTabela, x, y);
        foreach (var c in colunas)
        {
            x += c.LarguraMm;
            Linha(doc, vista, x, topoDaTabela, x, y);
        }
        return y - alturaLinhaMm * 0.8;
    }

    /// <summary>Símbolo de disjuntor em planta unifilar: traço com a lâmina aberta, da esquerda para a direita.</summary>
    public static void Disjuntor(Document doc, View vista, double xMm, double yMm, double comprimentoMm = 6)
    {
        var c = comprimentoMm;
        Linha(doc, vista, xMm, yMm, xMm + c * 0.25, yMm);
        Linha(doc, vista, xMm + c * 0.25, yMm, xMm + c * 0.8, yMm + c * 0.35);
        Linha(doc, vista, xMm + c * 0.75, yMm, xMm + c, yMm);
        // o "x" do disjuntor sobre a lâmina
        Linha(doc, vista, xMm + c * 0.45, yMm + c * 0.05, xMm + c * 0.6, yMm + c * 0.3);
        Linha(doc, vista, xMm + c * 0.6, yMm + c * 0.05, xMm + c * 0.45, yMm + c * 0.3);
    }

    /// <summary>Símbolo de DR: caixa com a sigla.</summary>
    public static void Caixa(Document doc, View vista, double xMm, double yMm, double larguraMm, double alturaMm, string rotulo)
    {
        Retangulo(doc, vista, xMm, yMm + alturaMm / 2, larguraMm, alturaMm);
        Texto(doc, vista, xMm + 0.8, yMm + alturaMm / 2 - 0.6, rotulo, larguraMm - 1.6);
    }
}
