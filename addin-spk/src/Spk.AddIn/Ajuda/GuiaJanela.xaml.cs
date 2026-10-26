using System.Windows;
using System.Windows.Controls;

namespace Spk.AddIn.Ajuda;

/// <summary>Guia do SPK: a situação do projeto aberto, com atalhos para cada etapa, e os capítulos de leitura.</summary>
public partial class GuiaJanela : Window
{
    /// <summary>Um item da navegação: a situação do projeto (sem capítulo) ou um capítulo.</summary>
    public sealed record Item(string Titulo, Capitulo? Capitulo);

    /// <summary>O comando escolhido num botão de etapa. O Guia fecha e quem abriu dispara o comando.</summary>
    public AcaoDaEtapa? Escolhida { get; private set; }

    public GuiaJanela(string arquivo, IReadOnlyList<Etapa> etapas, IReadOnlyList<Capitulo> capitulos)
    {
        InitializeComponent();
        Arquivo.Text = arquivo;
        Etapas.ItemsSource = etapas;
        Navegacao.ItemsSource = new[] { new Item("Seu projeto agora", null) }
            .Concat(capitulos.Select(c => new Item(c.Titulo, c)))
            .ToList();
        Navegacao.SelectedIndex = 0;
    }

    void Navegacao_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Navegacao.SelectedItem is not Item item) return;
        var capitulo = item.Capitulo;
        PainelSituacao.Visibility = capitulo is null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        Leitura.Visibility = capitulo is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        if (capitulo is not null) Leitura.Document = TextoDoGuia.Documento(capitulo.Markdown);
    }

    void Acao_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AcaoDaEtapa acao) return;
        Escolhida = acao;
        DialogResult = true;
    }

    void Fechar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
