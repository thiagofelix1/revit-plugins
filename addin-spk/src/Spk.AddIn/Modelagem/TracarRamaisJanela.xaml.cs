using System.Windows;

namespace Spk.AddIn.Modelagem;

public partial class TracarRamaisJanela : Window
{
    public OpcoesRamais? Resultado { get; private set; }

    public TracarRamaisJanela(string resumo, OpcoesRamais opcoes)
    {
        InitializeComponent();
        Resumo.Text = resumo;
        NaSubgeral.IsChecked = opcoes.Altura == AlturaDoRamal.NaSubgeral;
        RelativaASubgeral.IsChecked = opcoes.Altura == AlturaDoRamal.RelativaASubgeral;
        RelativaAoPiso.IsChecked = opcoes.Altura == AlturaDoRamal.RelativaAoPiso;
        AcimaDaSubgeral.Text = Entrada.F(opcoes.Altura == AlturaDoRamal.RelativaASubgeral ? opcoes.AlturaM : 0.3);
        AcimaDoPiso.Text = Entrada.F(opcoes.Altura == AlturaDoRamal.RelativaAoPiso ? opcoes.AlturaM : 3.0);
        Tolerancia.Text = Entrada.F(opcoes.ToleranciaFileiraM);
        Ligar.IsChecked = opcoes.LigarBicos;
    }

    void Tracar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var altura = RelativaASubgeral.IsChecked == true ? AlturaDoRamal.RelativaASubgeral
                : RelativaAoPiso.IsChecked == true ? AlturaDoRamal.RelativaAoPiso
                : AlturaDoRamal.NaSubgeral;
            Resultado = new OpcoesRamais
            {
                Altura = altura,
                AlturaM = altura switch
                {
                    AlturaDoRamal.RelativaASubgeral => Entrada.Numero(AcimaDaSubgeral.Text, "Altura acima da subgeral", -5, 5),
                    AlturaDoRamal.RelativaAoPiso => Entrada.Numero(AcimaDoPiso.Text, "Altura acima do nível", 0.3, 40),
                    _ => 0,
                },
                ToleranciaFileiraM = Entrada.Numero(Tolerancia.Text, "Distância entre bicos da mesma fileira", 0.1, 5),
                LigarBicos = Ligar.IsChecked == true,
            };
            DialogResult = true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            MessageBox.Show(this, ex.Message, "SPK — Traçar ramais", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
