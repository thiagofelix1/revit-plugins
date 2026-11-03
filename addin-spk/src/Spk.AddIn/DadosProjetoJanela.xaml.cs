using System.Windows;
using System.Windows.Controls;
using Spk.Motor.Normas;

namespace Spk.AddIn;

public partial class DadosProjetoJanela : Window
{
    static readonly (ClasseRisco Valor, string Texto)[] Classes =
        Enum.GetValues<ClasseRisco>().Select(c => (c, DadosProjeto.NomeClasse(c))).ToArray();

    static readonly (TipoBase Valor, string Texto)[] Bases =
    [
        (TipoBase.Figura42, "Densidade e área pela Figura 42"),
        (TipoBase.DensidadeArea, "Densidade e área informadas"),
        (TipoBase.Esfr, "ESFR: 12 bicos, pressão mínima"),
    ];

    public DadosProjeto? Resultado { get; private set; }

    public DadosProjetoJanela(DadosProjeto dados)
    {
        InitializeComponent();
        Classe.ItemsSource = Classes.Select(c => c.Texto).ToList();
        Classe.SelectedIndex = Array.FindIndex(Classes, c => c.Valor == dados.Classe);
        Base.ItemsSource = Bases.Select(b => b.Texto).ToList();
        Base.SelectedIndex = Array.FindIndex(Bases, b => b.Valor == dados.Base);

        Area.Text = Entrada.F(dados.AreaOperacaoM2);
        Densidade.Text = dados.DensidadeLminM2 > 0 ? Entrada.F(dados.DensidadeLminM2) : "";
        Pressao.Text = dados.PressaoMinimaKPa > 0 ? Entrada.F(dados.PressaoMinimaKPa) : "";
        Item.Text = dados.ItemNorma;
        Hidrantes.Text = Entrada.F(dados.VazaoHidrantesLmin);
        Duracao.Text = Entrada.F(dados.DuracaoMin);
        FatorC.Text = Entrada.F(dados.FatorC);
        Inclinacao.Text = Entrada.F(dados.InclinacaoTetoPct);
        Estatica.Text = Entrada.F(dados.PressaoEstaticaKPa);
        Residual.Text = Entrada.F(dados.PressaoResidualKPa);
        VazaoResidual.Text = Entrada.F(dados.VazaoResidualLmin);
        AtualizarCampos();
    }

    TipoBase BaseEscolhida => Bases[Math.Max(Base.SelectedIndex, 0)].Valor;

    void Base_SelectionChanged(object sender, SelectionChangedEventArgs e) => AtualizarCampos();

    void AtualizarCampos()
    {
        if (Area is null) return;
        var esfr = BaseEscolhida == TipoBase.Esfr;
        var densidade = BaseEscolhida == TipoBase.DensidadeArea;
        Area.IsEnabled = RotuloArea.IsEnabled = !esfr;
        Densidade.IsEnabled = RotuloDensidade.IsEnabled = densidade;
        Pressao.IsEnabled = RotuloPressao.IsEnabled = esfr;
    }

    void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dados = Ler();
            dados.CriarBase();
            if (dados.Classe == ClasseRisco.Armazenamento && (dados.VazaoHidrantesLmin is null || dados.DuracaoMin is null))
                throw new FormatException("Armazenamento: informe a demanda de hidrantes e a duração da IT-24.");
            var abastecimento = new[] { dados.PressaoEstaticaKPa, dados.PressaoResidualKPa, dados.VazaoResidualLmin };
            if (abastecimento.Any(v => v is null) && abastecimento.Any(v => v is not null))
                throw new FormatException("Abastecimento: preencha pressão estática, pressão residual e vazão, ou deixe os três em branco.");
            if (dados.PressaoResidualKPa > dados.PressaoEstaticaKPa)
                throw new FormatException("Abastecimento: a pressão residual não pode passar da estática.");

            Resultado = dados;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "SPK — Dados do projeto", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    DadosProjeto Ler()
    {
        var baseEscolhida = BaseEscolhida;
        return new DadosProjeto
        {
            Classe = Classes[Math.Max(Classe.SelectedIndex, 0)].Valor,
            Base = baseEscolhida,
            AreaOperacaoM2 = baseEscolhida == TipoBase.Esfr ? 0 : Entrada.Numero(Area.Text, "Área de operação", 10, 2000),
            DensidadeLminM2 = baseEscolhida == TipoBase.DensidadeArea ? Entrada.Numero(Densidade.Text, "Densidade", 0.5, 100) : 0,
            PressaoMinimaKPa = baseEscolhida == TipoBase.Esfr ? Entrada.Numero(Pressao.Text, "Pressão mínima ESFR", 48, 2000) : 0,
            ItemNorma = Item.Text.Trim(),
            VazaoHidrantesLmin = Entrada.Opcional(Hidrantes.Text, "Demanda de hidrantes", 0, 20000),
            DuracaoMin = Entrada.Opcional(Duracao.Text, "Duração", 10, 600),
            FatorC = Entrada.Numero(FatorC.Text, "Fator C", 80, 150),
            InclinacaoTetoPct = Entrada.Numero(Inclinacao.Text, "Inclinação do teto", 0, 200),
            PressaoEstaticaKPa = Entrada.Opcional(Estatica.Text, "Pressão estática", 50, 5000),
            PressaoResidualKPa = Entrada.Opcional(Residual.Text, "Pressão residual", 0, 5000),
            VazaoResidualLmin = Entrada.Opcional(VazaoResidual.Text, "Vazão residual", 1, 100000),
        };
    }

    void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
