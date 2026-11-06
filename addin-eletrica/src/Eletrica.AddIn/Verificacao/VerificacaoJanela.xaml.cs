using System.IO;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using Eletrica.Motor.Calculo;

namespace Eletrica.AddIn.Verificacao;

/// <summary>Lista dos problemas do Verificar modelo, com o item da norma e os elementos para mostrar no modelo; filtro por gravidade e relatório em Markdown.</summary>
public partial class VerificacaoJanela : Window
{
    public sealed class Linha
    {
        public string Codigo { get; init; } = "";
        public string Etiqueta { get; init; } = "";
        public string Cor { get; init; } = "#78909C";
        public string Item { get; init; } = "";
        public string Mensagem { get; init; } = "";
        public IReadOnlyList<ElementId> Elementos { get; init; } = [];
    }

    readonly List<Linha> _linhas;
    readonly string _cabecalho;
    readonly string _projeto;

    public IReadOnlyList<ElementId>? Escolhidos { get; private set; }

    public VerificacaoJanela(IReadOnlyList<Problema> problemas, IReadOnlyDictionary<string, ElementId> mapa, IReadOnlyList<string> avisos, string cabecalho, string projeto = "projeto")
    {
        InitializeComponent();
        _projeto = projeto;
        var erros = problemas.Count(p => p.Gravidade == Gravidade.Erro);
        var av = problemas.Count(p => p.Gravidade == Gravidade.Aviso);
        _cabecalho = $"{cabecalho} " + (erros == 0 && av == 0 ? "Nenhum problema encontrado." : $"{erros} erro(s), {av} aviso(s), {problemas.Count - erros - av} nota(s).");
        Resumo.Text = _cabecalho;
        _linhas = problemas.OrderBy(p => p.Gravidade).ThenBy(p => p.Codigo).Select(p => new Linha
        {
            Codigo = p.Codigo,
            Etiqueta = p.Gravidade switch { Gravidade.Erro => "ERRO", Gravidade.Aviso => "AVISO", _ => "NOTA" },
            Cor = p.Gravidade switch { Gravidade.Erro => "#C62828", Gravidade.Aviso => "#EF6C00", _ => "#78909C" },
            Item = p.Item == "modelo" ? "modelo do Revit" : "NBR 5410 " + p.Item,
            Mensagem = p.Mensagem,
            Elementos = p.Elementos.Where(mapa.ContainsKey).Select(e => mapa[e]).Distinct().ToList(),
        }).ToList();
        foreach (var a in avisos)
            _linhas.Insert(0, new Linha { Codigo = "LEITURA", Etiqueta = "AVISO", Cor = "#EF6C00", Item = "", Mensagem = a });
        Filtro.ItemsSource = new[] { "Tudo", "Só erros", "Erros e avisos", "Só notas" };
        Filtro.SelectedIndex = 0;
        Lista.ItemsSource = _linhas;
        BotaoMostrar.IsEnabled = false;
    }

    void Filtro_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Lista is null) return;
        Lista.ItemsSource = Filtro.SelectedIndex switch
        {
            1 => _linhas.Where(l => l.Etiqueta == "ERRO").ToList(),
            2 => _linhas.Where(l => l.Etiqueta != "NOTA").ToList(),
            3 => _linhas.Where(l => l.Etiqueta == "NOTA").ToList(),
            _ => _linhas,
        };
    }

    void Lista_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        BotaoMostrar.IsEnabled = Lista.SelectedItem is Linha { Elementos.Count: > 0 };

    void Lista_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Mostrar_Click(sender, e);

    void Mostrar_Click(object sender, RoutedEventArgs e)
    {
        if (Lista.SelectedItem is not Linha { Elementos.Count: > 0 } linha) return;
        Escolhidos = linha.Elementos;
        DialogResult = true;
    }

    string Relatorio()
    {
        var linhas = new List<string>
        {
            $"# Verificação AutoEletrica — {_projeto}", "", $"{DateTime.Now:dd/MM/yyyy HH:mm}. {_cabecalho}", "",
            "| Gravidade | Código | Item | Problema | Elementos |", "|---|---|---|---|---|",
        };
        linhas.AddRange(_linhas.Select(l => $"| {l.Etiqueta} | {l.Codigo} | {l.Item} | {l.Mensagem.Replace("|", "/")} | {string.Join(", ", l.Elementos.Select(id => id.Value))} |"));
        linhas.Add("");
        linhas.Add("Cada linha cita o item da ABNT NBR 5410:2004 (ou \"modelo do Revit\" para a consistência do arquivo). Gerado pelo Verificar modelo do AutoEletrica.");
        return string.Join("\n", linhas);
    }

    void Copiar_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Relatorio()); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException) { }
    }

    void Salvar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pasta = Path.Combine(PastasEletrica.Raiz, "verificacao");
            Directory.CreateDirectory(pasta);
            var nome = string.Concat(_projeto.Split(Path.GetInvalidFileNameChars()));
            var caminho = Path.Combine(pasta, $"{nome}-verificacao.md");
            File.WriteAllText(caminho, Relatorio());
            MessageBox.Show(this, $"Relatório salvo em:\n{caminho}", "AutoEletrica — Verificar modelo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "AutoEletrica — Verificar modelo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Abre a janela; se o engenheiro escolher um item, seleciona os elementos e leva a vista até eles.</summary>
    public static void Mostrar(UIDocument ui, IReadOnlyList<Problema> problemas, IReadOnlyDictionary<string, ElementId> mapa, IReadOnlyList<string> avisos, string cabecalho)
    {
        var janela = new VerificacaoJanela(problemas, mapa, avisos, cabecalho, ui.Document.Title);
        if (janela.ShowDialog() != true || janela.Escolhidos is not { Count: > 0 } escolhidos) return;
        var existentes = escolhidos.Where(id => ui.Document.GetElement(id) is not null).ToList();
        ui.Selection.SetElementIds(existentes);
        try { ui.ShowElements(existentes); }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException) { }
    }
}
