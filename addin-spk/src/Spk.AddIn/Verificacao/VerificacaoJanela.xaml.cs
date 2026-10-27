using System.Windows;
using System.Windows.Input;

namespace Spk.AddIn.Verificacao;

/// <summary>Resultado da verificação, em árvore: grupos de problema e as ocorrências de cada um.</summary>
public partial class VerificacaoJanela : Window
{
    /// <summary>Um nó da árvore: um grupo (com filhos) ou uma ocorrência.</summary>
    public sealed class No
    {
        public string Texto { get; init; } = "";
        public string? Detalhe { get; init; }
        public string? Etiqueta { get; init; }
        public string Cor { get; init; } = "#78909C";
        public FontWeight Peso { get; init; } = FontWeights.Normal;
        public List<No> Filhos { get; init; } = [];
        public IReadOnlyList<ElementId> Elementos { get; init; } = [];
        public No? Pai { get; set; }
        public System.Windows.Visibility EtiquetaVisivel => Etiqueta is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        public System.Windows.Visibility DetalheVisivel => Detalhe is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }

    readonly ResultadoVerificacao resultado;

    /// <summary>Os elementos a selecionar e mostrar no modelo, se o engenheiro pediu.</summary>
    public IReadOnlyList<ElementId>? Escolhidos { get; private set; }

    public VerificacaoJanela(ResultadoVerificacao resultado)
    {
        InitializeComponent();
        this.resultado = resultado;
        Resumo.Text = resultado.Resumo();
        Arvore.ItemsSource = resultado.Grupos.Select(Grupo).ToList();
        Atualizar();
    }

    static No Grupo(Grupo g)
    {
        var (etiqueta, cor) = g.Gravidade switch
        {
            Gravidade.Erro => ("ERRO", "#C62828"),
            Gravidade.Aviso => ("AVISO", "#EF6C00"),
            _ => ("INFO", "#78909C"),
        };
        var no = new No
        {
            Texto = $"{g.Titulo} ({g.Ocorrencias.Count})",
            Detalhe = g.ComoResolver,
            Etiqueta = etiqueta,
            Cor = cor,
            Peso = FontWeights.SemiBold,
            Elementos = g.Ocorrencias.SelectMany(o => o.Elementos).Distinct().ToList(),
            Filhos = g.Ocorrencias.Take(500).Select(o => new No { Texto = o.Descricao, Elementos = o.Elementos }).ToList(),
        };
        foreach (var filho in no.Filhos) filho.Pai = no;
        return no;
    }

    No? Selecionado => Arvore.SelectedItem as No;

    void Atualizar()
    {
        BotaoMostrar.IsEnabled = Selecionado is { Elementos.Count: > 0 };
        BotaoTodos.IsEnabled = (Selecionado?.Pai ?? Selecionado) is { Elementos.Count: > 0 };
    }

    void Arvore_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) => Atualizar();

    void Arvore_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Selecionado is { Pai: not null, Elementos.Count: > 0 }) Mostrar_Click(sender, e);
    }

    void Mostrar_Click(object sender, RoutedEventArgs e)
    {
        if (Selecionado is not { Elementos.Count: > 0 } no) return;
        Escolhidos = no.Elementos;
        DialogResult = true;
    }

    void Todos_Click(object sender, RoutedEventArgs e)
    {
        if ((Selecionado?.Pai ?? Selecionado) is not { Elementos.Count: > 0 } grupo) return;
        Escolhidos = grupo.Elementos;
        DialogResult = true;
    }

    void Copiar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(resultado.Texto());
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Área de transferência ocupada por outro programa: nada a fazer além de tentar de novo.
        }
    }

    void Fechar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
