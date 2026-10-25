using System.Windows;

namespace Spk.AddIn;

public partial class DadosEsbocoJanela : Window
{
    public DadosEsboco? Resultado { get; private set; }

    public DadosEsbocoJanela(DadosEsboco dados)
    {
        InitializeComponent();
        Ramais.Text = dados.Ramais.ToString(Entrada.Cultura);
        BicosPorRamal.Text = dados.BicosPorRamal.ToString(Entrada.Cultura);
        EspacamentoRamal.Text = Entrada.F(dados.EspacamentoRamalM);
        EspacamentoEntre.Text = Entrada.F(dados.EspacamentoEntreRamaisM);
        Altura.Text = Entrada.F(dados.AlturaRedeM);
        DropBico.Text = Entrada.F(dados.DropM);
        FatorK.Text = Entrada.F(dados.FatorK);
        Area.Text = Entrada.F(dados.AreaM2);
    }

    void Salvar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Resultado = new DadosEsboco
            {
                Ramais = Entrada.Inteiro(Ramais.Text, "Ramais", 1, 30),
                BicosPorRamal = Entrada.Inteiro(BicosPorRamal.Text, "Bicos por ramal", 1, 40),
                EspacamentoRamalM = Entrada.Numero(EspacamentoRamal.Text, "Espaçamento no ramal", 0.3, 20),
                EspacamentoEntreRamaisM = Entrada.Numero(EspacamentoEntre.Text, "Espaçamento entre ramais", 0.3, 20),
                AlturaRedeM = Entrada.Numero(Altura.Text, "Altura da rede", 0.5, 40),
                DropM = Entrada.Numero(DropBico.Text, "Drop", 0.05, 3),
                FatorK = Entrada.Numero(FatorK.Text, "Fator K (L/min/bar½)", 40, 500),
                AreaM2 = Entrada.Numero(Area.Text, "Área de cobertura", 0.5, 40),
            };
            DialogResult = true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            MessageBox.Show(this, ex.Message, "SPK — Dados do esboço", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
