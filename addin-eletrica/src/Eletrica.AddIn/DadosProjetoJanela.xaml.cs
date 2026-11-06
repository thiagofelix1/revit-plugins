using System.Windows;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn;

public partial class DadosProjetoJanela : Window
{
    static readonly (double Uo, double Uff, string Texto)[] Sistemas =
    [
        (127, 220, "127 / 220 V"),
        (220, 380, "220 / 380 V"),
        (115, 230, "115 / 230 V"),
        (254, 440, "254 / 440 V"),
    ];

    static readonly (EsquemaAterramento Valor, string Texto)[] Esquemas =
    [
        (EsquemaAterramento.TNCS, "TN-C-S (PEN da distribuidora, PE separado na entrada)"),
        (EsquemaAterramento.TNS, "TN-S (neutro e PE separados desde a fonte)"),
        (EsquemaAterramento.TT, "TT (eletrodo próprio; proteção por DR)"),
        (EsquemaAterramento.IT, "IT (fonte isolada; supervisor de isolamento)"),
        (EsquemaAterramento.TNC, "TN-C (PEN em toda a instalação)"),
    ];

    public DadosProjeto? Resultado { get; private set; }

    public DadosProjetoJanela(DadosProjeto dados)
    {
        InitializeComponent();
        Sistema.ItemsSource = Sistemas.Select(s => s.Texto).ToList();
        var indice = Array.FindIndex(Sistemas, s => Math.Abs(s.Uo - dados.TensaoFaseNeutro) < 1);
        Sistema.SelectedIndex = indice < 0 ? 0 : indice;
        Fases.ItemsSource = new[] { "1 (monofásica)", "2 (bifásica)", "3 (trifásica)" };
        Fases.SelectedIndex = Math.Max(0, Math.Min(2, dados.Fases - 1));
        Esquema.ItemsSource = Esquemas.Select(e => e.Texto).ToList();
        Esquema.SelectedIndex = Math.Max(0, Array.FindIndex(Esquemas, e => e.Valor == dados.Esquema));
        Icc.Text = Entrada.F(dados.IccPresumidaKA);
        Aerea.IsChecked = dados.LinhaAerea;
        Trovoadas.Text = dados.DiasDeTrovoada.ToString();
        Spda.IsChecked = dados.TemSpda;
        Temperatura.Text = Entrada.F(dados.TemperaturaAmbiente);
        Habitacao.IsChecked = dados.Habitacao;
    }

    void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sistema = Sistemas[Math.Max(Sistema.SelectedIndex, 0)];
            Resultado = new DadosProjeto
            {
                TensaoFaseNeutro = sistema.Uo,
                TensaoFaseFase = sistema.Uff,
                Fases = Fases.SelectedIndex + 1,
                Esquema = Esquemas[Math.Max(Esquema.SelectedIndex, 0)].Valor,
                IccPresumidaKA = Entrada.Opcional(Icc.Text, "Icc presumida", 0.1, 200),
                LinhaAerea = Aerea.IsChecked == true,
                DiasDeTrovoada = Entrada.Inteiro(Trovoadas.Text, "Dias de trovoada", 0, 365),
                TemSpda = Spda.IsChecked == true,
                TemperaturaAmbiente = Entrada.Numero(Temperatura.Text, "Temperatura ambiente", 5, 80),
                Habitacao = Habitacao.IsChecked == true,
            };
            DialogResult = true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "AutoEletrica — Dados do projeto", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
