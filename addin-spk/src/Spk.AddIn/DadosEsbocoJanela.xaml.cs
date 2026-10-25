using System.Globalization;
using System.Windows;

namespace Spk.AddIn;

public partial class DadosEsbocoJanela : Window
{
    public DadosEsboco? Resultado { get; private set; }

    public DadosEsbocoJanela(DadosEsboco dados)
    {
        InitializeComponent();
        Ramais.Text = dados.Ramais.ToString(Cultura);
        BicosPorRamal.Text = dados.BicosPorRamal.ToString(Cultura);
        EspacamentoRamal.Text = F(dados.EspacamentoRamalM);
        EspacamentoEntre.Text = F(dados.EspacamentoEntreRamaisM);
        Altura.Text = F(dados.AlturaRedeM);
        DropBico.Text = F(dados.DropM);
        FatorK.Text = F(dados.FatorK);
        Area.Text = F(dados.AreaM2);
        Pressao.Text = F(dados.PressaoMinimaKPa);
        FatorC.Text = F(dados.FatorC);
    }

    void Salvar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dados = new DadosEsboco
            {
                Ramais = Inteiro(Ramais.Text, "Ramais", 1, 30),
                BicosPorRamal = Inteiro(BicosPorRamal.Text, "Bicos por ramal", 1, 40),
                EspacamentoRamalM = Numero(EspacamentoRamal.Text, "Espaçamento no ramal", 0.3, 20),
                EspacamentoEntreRamaisM = Numero(EspacamentoEntre.Text, "Espaçamento entre ramais", 0.3, 20),
                AlturaRedeM = Numero(Altura.Text, "Altura da rede", 0.5, 40),
                DropM = Numero(DropBico.Text, "Drop", 0.05, 3),
                FatorK = Numero(FatorK.Text, "Fator K", 1, 500),
                AreaM2 = Numero(Area.Text, "Área de cobertura", 0.5, 40),
                PressaoMinimaKPa = Numero(Pressao.Text, "Pressão mínima", 48, 2000),
                FatorC = Numero(FatorC.Text, "Fator C", 80, 150),
            };
            dados.Salvar();
            Resultado = dados;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            MessageBox.Show(this, ex.Message, "SPK — Dados do esboço", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    static string F(double valor) => valor.ToString("0.##", Cultura);

    static int Inteiro(string texto, string nome, int minimo, int maximo)
    {
        if (!int.TryParse(texto.Trim(), NumberStyles.Integer, Cultura, out var valor)
            && !int.TryParse(texto.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out valor))
            throw new FormatException($"{nome}: informe um número inteiro.");
        if (valor < minimo || valor > maximo) throw new ArgumentOutOfRangeException(nome, $"{nome} deve ficar entre {minimo} e {maximo}.");
        return valor;
    }

    static double Numero(string texto, string nome, double minimo, double maximo)
    {
        var limpo = texto.Trim().Replace(',', '.');
        if (!double.TryParse(limpo, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
            throw new FormatException($"{nome}: informe um número.");
        if (valor < minimo || valor > maximo) throw new ArgumentOutOfRangeException(nome, $"{nome} deve ficar entre {minimo} e {maximo}.");
        return valor;
    }
}
