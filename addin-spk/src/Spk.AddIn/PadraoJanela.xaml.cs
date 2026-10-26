using System.Windows;
using System.Windows.Controls;

namespace Spk.AddIn;

/// <summary>O que o arquivo de origem oferece para cada papel do padrão, e a sugestão do plugin.</summary>
public sealed record OpcoesPadrao(
    string Origem,
    IReadOnlyList<string> Carimbos,
    IReadOnlyList<string> EtiquetasTubo,
    IReadOnlyList<string> Templates,
    IReadOnlyList<string> Legendas,
    PadraoEscritorio Sugestao);

public partial class PadraoJanela : Window
{
    const string Nenhum = "(nenhum)";

    public sealed class ItemLegenda
    {
        public string Nome { get; init; } = "";
        public bool Marcada { get; set; }
    }

    readonly List<ItemLegenda> legendas;

    /// <summary>Nomes no arquivo de origem. O comando copia e grava os nomes como ficaram no projeto.</summary>
    public PadraoEscritorio? Resultado { get; private set; }

    public PadraoJanela(OpcoesPadrao opcoes)
    {
        InitializeComponent();
        Explicacao.Text = $"Origem: {opcoes.Origem}. O plugin copia para este projeto o que estiver escolhido e usa nas folhas. " +
                          "Deixe em (nenhum) o que não quiser: as folhas usam o estilo do plugin nesse ponto.";
        Preencher(Carimbo, opcoes.Carimbos, opcoes.Sugestao.Carimbo);
        Preencher(EtiquetaTubo, opcoes.EtiquetasTubo, opcoes.Sugestao.EtiquetaTubo);
        Preencher(TemplatePlanta, opcoes.Templates, opcoes.Sugestao.TemplatePlanta);
        Preencher(Template3d, opcoes.Templates, opcoes.Sugestao.Template3d);
        Preencher(TemplateCorte, opcoes.Templates, opcoes.Sugestao.TemplateCorte);
        Preencher(TemplateSetorizacao, opcoes.Templates, opcoes.Sugestao.TemplateSetorizacao);

        var sugeridas = opcoes.Sugestao.Legendas.ToHashSet(StringComparer.Ordinal);
        legendas = opcoes.Legendas.Select(n => new ItemLegenda { Nome = n, Marcada = sugeridas.Contains(n) }).ToList();
        Legendas.ItemsSource = legendas;
    }

    static void Preencher(ComboBox combo, IReadOnlyList<string> nomes, string? sugerido)
    {
        var itens = new List<string> { Nenhum };
        itens.AddRange(nomes);
        combo.ItemsSource = itens;
        combo.SelectedItem = sugerido is not null && nomes.Contains(sugerido) ? sugerido : Nenhum;
    }

    static string? Escolha(ComboBox combo) => combo.SelectedItem is string nome && nome != Nenhum ? nome : null;

    void Importar_Click(object sender, RoutedEventArgs e)
    {
        Resultado = new PadraoEscritorio
        {
            Carimbo = Escolha(Carimbo),
            EtiquetaTubo = Escolha(EtiquetaTubo),
            TemplatePlanta = Escolha(TemplatePlanta),
            Template3d = Escolha(Template3d),
            TemplateCorte = Escolha(TemplateCorte),
            TemplateSetorizacao = Escolha(TemplateSetorizacao),
            Legendas = legendas.Where(l => l.Marcada).Select(l => l.Nome).ToList(),
        };
        DialogResult = true;
    }

    void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
