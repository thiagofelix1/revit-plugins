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
