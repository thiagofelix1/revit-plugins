using System.Windows;
using Eletrica.AddIn.Padrao;

namespace Eletrica.AddIn.Familias;

public partial class FamiliasJanela : Window
{
    readonly List<CandidatoFamilia> _linhas;

    /// <summary>Papel → "Família : Tipo" confirmados (um tipo por papel; o último da lista vence se houver dois).</summary>
    public Dictionary<string, string>? Resultado { get; private set; }

    public FamiliasJanela(List<CandidatoFamilia> candidatos)
    {
        InitializeComponent();
        ColunaPapel.ItemsSource = CandidatoFamilia.Papeis.Select(PadraoEletrica.NomeDoPapel).ToList();
        _linhas = candidatos;
        Grade.ItemsSource = _linhas;
    }

    void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        Grade.CommitEdit();
        var resultado = new Dictionary<string, string>();
        foreach (var l in _linhas.Where(l => l.Papel != PapelFamilia.Ignorar))
            resultado[l.Papel.ToString()] = l.Nome;
        var repetidos = _linhas.Where(l => l.Papel != PapelFamilia.Ignorar).GroupBy(l => l.Papel).Where(g => g.Count() > 1).ToList();
        if (repetidos.Count > 0)
        {
            var texto = string.Join("\n", repetidos.Select(g => $"• {PadraoEletrica.NomeDoPapel(g.Key)}: {string.Join(", ", g.Select(l => l.Nome))}"));
            if (MessageBox.Show(this, "Mais de uma família no mesmo papel; o plugin usa a última de cada papel:\n\n" + texto + "\n\nConfirmar assim?",
                    "AutoEletrica — Famílias", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        }
        Resultado = resultado;
        DialogResult = true;
    }
}
