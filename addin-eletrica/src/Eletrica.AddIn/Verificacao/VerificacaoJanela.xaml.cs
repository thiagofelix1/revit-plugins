using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using Eletrica.Motor.Calculo;

namespace Eletrica.AddIn.Verificacao;

/// <summary>Lista dos problemas do Verificar modelo, com o item da norma e os elementos para mostrar no modelo.</summary>
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
    readonly string _texto;

    public IReadOnlyList<ElementId>? Escolhidos { get; private set; }

    public VerificacaoJanela(IReadOnlyList<Problema> problemas, IReadOnlyDictionary<string, ElementId> mapa, IReadOnlyList<string> avisos, string cabecalho)
    {
        InitializeComponent();
        var erros = problemas.Count(p => p.Gravidade == Gravidade.Erro);
        var av = problemas.Count(p => p.Gravidade == Gravidade.Aviso);
        Resumo.Text = $"{cabecalho} " + (erros == 0 && av == 0 ? "Nenhum problema encontrado." : $"{erros} erro(s), {av} aviso(s), {problemas.Count - erros - av} nota(s).");
        _linhas = problemas.OrderBy(p => p.Gravidade).ThenBy(p => p.Codigo).Select(p => new Linha
        {
            Codigo = p.Codigo,
            Etiqueta = p.Gravidade switch { Gravidade.Erro => "ERRO", Gravidade.Aviso => "AVISO", _ => "NOTA" },
            Cor = p.Gravidade switch { Gravidade.Erro => "#C62828", Gravidade.Aviso => "#EF6C00", _ => "#78909C" },
            Item = "NBR 5410 " + p.Item,
            Mensagem = p.Mensagem,
            Elementos = p.Elementos.Where(mapa.ContainsKey).Select(e => mapa[e]).Distinct().ToList(),
        }).ToList();
        foreach (var a in avisos)
            _linhas.Insert(0, new Linha { Codigo = "LEITURA", Etiqueta = "AVISO", Cor = "#EF6C00", Item = "", Mensagem = a });
        Lista.ItemsSource = _linhas;
        _texto = string.Join("\n", _linhas.Select(l => $"[{l.Etiqueta}] {l.Codigo} {l.Mensagem} ({l.Item})"));
        BotaoMostrar.IsEnabled = false;
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

    void Copiar_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Resumo.Text + "\n\n" + _texto); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException) { }
    }

    /// <summary>Abre a janela; se o engenheiro escolher um item, seleciona os elementos e leva a vista até eles.</summary>
    public static void Mostrar(UIDocument ui, IReadOnlyList<Problema> problemas, IReadOnlyDictionary<string, ElementId> mapa, IReadOnlyList<string> avisos, string cabecalho)
    {
        var janela = new VerificacaoJanela(problemas, mapa, avisos, cabecalho);
        if (janela.ShowDialog() != true || janela.Escolhidos is not { Count: > 0 } escolhidos) return;
        var existentes = escolhidos.Where(id => ui.Document.GetElement(id) is not null).ToList();
        ui.Selection.SetElementIds(existentes);
        try { ui.ShowElements(existentes); }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException) { }
    }
}
