using System.Windows;
using System.Windows.Input;

namespace Spk.AddIn;

/// <summary>Escolha do cenário da Rede de exemplo.</summary>
public partial class CenarioJanela : Window
{
    public sealed record Item(CenarioExemplo Cenario)
    {
        public string Titulo => $"{Cenario.Nome}  ({Cenario.TotalDeBicos} bicos)";
        public string Descricao => $"{Cenario.Descricao}\nCritérios: {Cenario.Criterios.Resumo()}";
    }

    public CenarioExemplo? Escolhido { get; private set; }
    public bool GravarCriterios => Criterios.IsChecked == true;

    public CenarioJanela(IReadOnlyList<CenarioExemplo> cenarios)
    {
        InitializeComponent();
        Lista.ItemsSource = cenarios.Select(c => new Item(c)).ToList();
        Lista.SelectedIndex = 0;
    }

    void Gerar_Click(object sender, RoutedEventArgs e)
    {
        if (Lista.SelectedItem is not Item item) return;
        Escolhido = item.Cenario;
        DialogResult = true;
    }

    void Lista_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Gerar_Click(sender, e);

    void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
