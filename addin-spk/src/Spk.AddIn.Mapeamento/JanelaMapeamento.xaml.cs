using System.Collections.ObjectModel;
using System.Windows;

namespace Spk.AddIn.Mapeamento;

public partial class JanelaMapeamento : Window
{
    public ObservableCollection<CandidatoFamilia> Candidatos { get; }

    /// <summary>Null se o usuário cancelou; senão, a lista com os papéis/parâmetros confirmados.</summary>
    public IReadOnlyList<CandidatoFamilia>? Resultado { get; private set; }

    public JanelaMapeamento(IEnumerable<CandidatoFamilia> candidatos)
    {
        Candidatos = new ObservableCollection<CandidatoFamilia>(candidatos);
        InitializeComponent();
        Grade.ItemsSource = Candidatos;
        ColunaPapel.ItemsSource = Enum.GetValues(typeof(PapelFamilia));
    }

    void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        // Bico sem K ou área ficava de fora do cálculo sem aviso: aqui ele não passa.
        var semMedidas = Candidatos.Where(c => c.FaltamMedidas).Select(c => $"• {c.Familia} : {c.Tipo}").ToList();
        if (semMedidas.Count > 0)
        {
            MessageBox.Show(this,
                "Estes bicos estão sem fator K ou área de cobertura maior que zero, e ficariam fora do cálculo:\n\n" +
                string.Join("\n", semMedidas.Take(10)) + (semMedidas.Count > 10 ? $"\n... e mais {semMedidas.Count - 10}" : "") +
                "\n\nPreencha o K (L/min/bar½) e a área (m²), ou marque a linha como Ignorar.",
                "SPK — Famílias", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Resultado = Candidatos;
        DialogResult = true;
        Close();
    }

    void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        Resultado = null;
        DialogResult = false;
        Close();
    }
}
