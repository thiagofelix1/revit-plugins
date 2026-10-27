using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Spk.AddIn.Ajuda;

/// <summary>Um capítulo do Guia: um .md embutido no plugin, título tirado da primeira linha "# ".</summary>
public sealed record Capitulo(string Titulo, string Markdown);

/// <summary>
///     Lê os capítulos embutidos (Ajuda\*.md) e converte o Markdown que eles usam num FlowDocument: títulos # e ##,
///     parágrafos, listas com "- ", tabelas com "|" e **negrito**. Não é um Markdown completo, só o que o guia precisa.
/// </summary>
static class TextoDoGuia
{
    static readonly Brush Titulo = Congelado(new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)));
    static readonly Brush Texto = Congelado(new SolidColorBrush(Color.FromRgb(0x26, 0x32, 0x38)));
    static readonly Brush Borda = Congelado(new SolidColorBrush(Color.FromRgb(0xCF, 0xD8, 0xDC)));
    static readonly Brush Cabecalho = Congelado(new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF1)));

    public static List<Capitulo> Capitulos()
    {
        var assembly = typeof(TextoDoGuia).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => Ler(assembly, n))
            .ToList();
    }

    static Capitulo Ler(Assembly assembly, string recurso)
    {
        using var leitor = new StreamReader(assembly.GetManifestResourceStream(recurso)!);
        var markdown = leitor.ReadToEnd();
        var titulo = markdown.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("# "))?[2..] ?? recurso;
        return new Capitulo(titulo, markdown);
    }

    public static FlowDocument Documento(string markdown)
    {
        var documento = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13.5,
            Foreground = Texto,
            PagePadding = new Thickness(24, 16, 24, 24),
            LineHeight = 20,
            TextAlignment = TextAlignment.Left,
        };
        var linhas = markdown.Replace("\r", "").Split('\n');
        for (var i = 0; i < linhas.Length;)
        {
            var linha = linhas[i].TrimEnd();
            if (linha.Length == 0)
            {
                i++;
            }
            else if (linha.StartsWith("# "))
            {
                documento.Blocks.Add(Cabeca(linha[2..], 22, new Thickness(0, 0, 0, 10)));
                i++;
            }
            else if (linha.StartsWith("## "))
            {
                documento.Blocks.Add(Cabeca(linha[3..], 16, new Thickness(0, 14, 0, 6)));
                i++;
            }
            else if (linha.StartsWith("- "))
            {
                var lista = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(22, 0, 0, 0) };
                for (; i < linhas.Length && linhas[i].StartsWith("- "); i++)
                    lista.ListItems.Add(new ListItem(Paragrafo(linhas[i][2..].Trim(), new Thickness(0, 0, 0, 4))));
                documento.Blocks.Add(lista);
            }
            else if (Numerada(linha))
            {
                var lista = new List { MarkerStyle = TextMarkerStyle.Decimal, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(22, 0, 0, 0) };
                for (; i < linhas.Length && Numerada(linhas[i]); i++)
                    lista.ListItems.Add(new ListItem(Paragrafo(linhas[i][(linhas[i].IndexOf(". ", StringComparison.Ordinal) + 2)..].Trim(),
                        new Thickness(0, 0, 0, 4))));
                documento.Blocks.Add(lista);
            }
            else if (linha.StartsWith('|'))
            {
                var tabela = new List<string>();
                for (; i < linhas.Length && linhas[i].TrimStart().StartsWith('|'); i++) tabela.Add(linhas[i].Trim());
                documento.Blocks.Add(Tabela(tabela));
            }
            else
            {
                var texto = new List<string>();
                for (; i < linhas.Length && linhas[i].Trim().Length > 0 && !Especial(linhas[i]); i++) texto.Add(linhas[i].Trim());
                documento.Blocks.Add(Paragrafo(string.Join(" ", texto), new Thickness(0, 0, 0, 10)));
            }
        }

        return documento;
    }

    static bool Especial(string linha) => linha.StartsWith('#') || linha.StartsWith("- ") || Numerada(linha) || linha.TrimStart().StartsWith('|');

    /// <summary>"1. texto": item de lista numerada (o Markdown que o guia usa; títulos "## 1." começam com #).</summary>
    static bool Numerada(string linha) => Regex.IsMatch(linha, @"^\d+\. ");

    static Paragraph Cabeca(string texto, double tamanho, Thickness margem) =>
        new(new Run(texto)) { FontSize = tamanho, FontWeight = FontWeights.SemiBold, Foreground = Titulo, Margin = margem };

    static Paragraph Paragrafo(string texto, Thickness margem)
    {
        var paragrafo = new Paragraph { Margin = margem };
        Inlines(paragrafo.Inlines, texto);
        return paragrafo;
    }

    /// <summary>**negrito** vira Bold; "\\" vira uma barra só, como no Markdown.</summary>
    static void Inlines(InlineCollection destino, string texto)
    {
        var partes = Regex.Split(texto.Replace(@"\\", @"\"), @"(\*\*[^*]+\*\*)");
        foreach (var parte in partes.Where(p => p.Length > 0))
            destino.Add(parte.StartsWith("**") && parte.EndsWith("**") && parte.Length > 4
                ? new Bold(new Run(parte[2..^2]))
                : new Run(parte));
    }

    /// <summary>A segunda linha (|---|---|) separa o cabeçalho e não é desenhada.</summary>
    static Table Tabela(List<string> linhas)
    {
        var celulas = linhas
            .Where(l => !Regex.IsMatch(l, @"^\|[\s\-:|]+\|$"))
            .Select(l => l.Trim('|').Split('|').Select(c => c.Trim()).ToList())
            .ToList();
        var colunas = celulas.Max(c => c.Count);
        var tabela = new Table { CellSpacing = 0, Margin = new Thickness(0, 2, 0, 12), BorderBrush = Borda, BorderThickness = new Thickness(1, 1, 0, 0) };
        for (var c = 0; c < colunas; c++)
            tabela.Columns.Add(new TableColumn { Width = new GridLength(c == 0 && colunas == 2 ? 1 : 2, GridUnitType.Star) });

        var grupo = new TableRowGroup();
        for (var r = 0; r < celulas.Count; r++)
        {
            var linha = new TableRow();
            if (r == 0) linha.Background = Cabecalho;
            for (var c = 0; c < colunas; c++)
            {
                var texto = c < celulas[r].Count ? celulas[r][c] : "";
                var paragrafo = Paragrafo(r == 0 ? $"**{texto}**" : texto, new Thickness(0));
                linha.Cells.Add(new TableCell(paragrafo) { Padding = new Thickness(8, 5, 8, 5), BorderBrush = Borda, BorderThickness = new Thickness(0, 0, 1, 1) });
            }

            grupo.Rows.Add(linha);
        }

        tabela.RowGroups.Add(grupo);
        return tabela;
    }

    static T Congelado<T>(T objeto) where T : Freezable
    {
        objeto.Freeze();
        return objeto;
    }
}
