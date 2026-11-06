using System.Windows;
using Autodesk.Revit.DB.Electrical;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Padrao;

public partial class PadraoJanela : Window
{
    static readonly (AlturaDePassagem Valor, string Texto)[] Passagens =
    [
        (AlturaDePassagem.Teto, "Pelo teto (laje ou forro)"),
        (AlturaDePassagem.Piso, "Pelo piso"),
        (AlturaDePassagem.NivelDosPontos, "Na altura dos pontos (parede)"),
    ];

    readonly PadraoEletrica _padrao;
    readonly List<string> _tiposDeEletroduto;

    public PadraoEletrica? Resultado { get; private set; }

    public PadraoJanela(Document doc, PadraoEletrica padrao)
    {
        InitializeComponent();
        _padrao = padrao;
        var l = padrao.Lancamento;
        AlturaBaixa.Text = Entrada.F(l.AlturaBaixa);
        AlturaMedia.Text = Entrada.F(l.AlturaMedia);
        AlturaAlta.Text = Entrada.F(l.AlturaAlta);
        AlturaInterruptor.Text = Entrada.F(l.AlturaInterruptor);
        PeDireito.Text = Entrada.F(l.PeDireito);
        Afastamento.Text = Entrada.F(padrao.AfastamentoDaParede);
        PrefixoCircuito.Text = l.PrefixoCircuito;
        PrefixoQuadro.Text = l.PrefixoQuadro;
        PotenciaTug.Text = Entrada.F(l.PotenciaTug);
        PotenciaTugMolhada.Text = Entrada.F(l.PotenciaTugMolhada);
        PotenciaTue.Text = Entrada.F(l.PotenciaTue20A);
        PotenciaChuveiro.Text = Entrada.F(l.PotenciaChuveiro);
        PotenciaAr.Text = Entrada.F(l.PotenciaArCondicionado);
        CorrenteTug.Text = Entrada.F(l.CorrenteMaximaTug);
        CorrenteLuz.Text = Entrada.F(l.CorrenteMaximaIluminacao);
        PontosMaximos.Text = l.PontosMaximosPorCircuito.ToString();
        CorrenteFaseFase.Text = Entrada.F(l.CorrenteParaFaseFase);
        SugerirChuveiro.IsChecked = l.SugerirChuveiro;
        SugerirAr.IsChecked = l.SugerirArCondicionado;

        _tiposDeEletroduto = new FilteredElementCollector(doc).OfClass(typeof(ConduitType)).Cast<ConduitType>()
            .OrderByDescending(t => t.IsWithFitting).ThenBy(t => t.Name).Select(t => t.Name).ToList();
        TipoEletroduto.ItemsSource = _tiposDeEletroduto;
        TipoEletroduto.SelectedIndex = Math.Max(0, _tiposDeEletroduto.IndexOf(padrao.TipoEletroduto ?? ""));
        DnPadrao.Text = padrao.DnPadrao.ToString();
        Passagem.ItemsSource = Passagens.Select(p => p.Texto).ToList();
        Passagem.SelectedIndex = Math.Max(0, Array.FindIndex(Passagens, p => p.Valor == padrao.Passagem));
        CotaTeto.Text = Entrada.F(padrao.CotaTeto);
        CotaPiso.Text = Entrada.F(padrao.CotaPiso);
        FioCurvo.IsChecked = padrao.FioCurvo;
        CorFases.Text = padrao.CorFases;
        CorNeutro.Text = padrao.CorNeutro;
        CorPE.Text = padrao.CorPE;
        CorRetorno.Text = padrao.CorRetorno;
    }

    void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var lancamento = new PadraoDeLancamento
            {
                AlturaBaixa = Entrada.Numero(AlturaBaixa.Text, "Altura baixa", 0, 3),
                AlturaMedia = Entrada.Numero(AlturaMedia.Text, "Altura média", 0, 3),
                AlturaAlta = Entrada.Numero(AlturaAlta.Text, "Altura alta", 0, 6),
                AlturaInterruptor = Entrada.Numero(AlturaInterruptor.Text, "Altura do interruptor", 0, 3),
                PeDireito = Entrada.Numero(PeDireito.Text, "Pé-direito", 1.8, 12),
                PrefixoCircuito = PrefixoCircuito.Text.Trim(),
                PrefixoQuadro = PrefixoQuadro.Text.Trim(),
                PotenciaTug = Entrada.Numero(PotenciaTug.Text, "Potência da TUG", 1, 10000),
                PotenciaTugMolhada = Entrada.Numero(PotenciaTugMolhada.Text, "Potência da TUG molhada", 1, 10000),
                PotenciaTue20A = Entrada.Numero(PotenciaTue.Text, "Potência da TUE", 1, 50000),
                PotenciaChuveiro = Entrada.Numero(PotenciaChuveiro.Text, "Potência do chuveiro", 1, 50000),
                PotenciaArCondicionado = Entrada.Numero(PotenciaAr.Text, "Potência do ar-condicionado", 1, 50000),
                CorrenteMaximaTug = Entrada.Numero(CorrenteTug.Text, "Corrente máxima TUG", 1, 63),
                CorrenteMaximaIluminacao = Entrada.Numero(CorrenteLuz.Text, "Corrente máxima iluminação", 1, 63),
                PontosMaximosPorCircuito = Entrada.Inteiro(PontosMaximos.Text, "Pontos máximos", 1, 60),
                CorrenteParaFaseFase = Entrada.Numero(CorrenteFaseFase.Text, "Corrente para fase-fase", 1, 200),
                SugerirChuveiro = SugerirChuveiro.IsChecked == true,
                SugerirArCondicionado = SugerirAr.IsChecked == true,
            };
            _padrao.Lancamento = lancamento;
            _padrao.AfastamentoDaParede = Entrada.Numero(Afastamento.Text, "Afastamento da parede", 0, 0.5);
            _padrao.TipoEletroduto = TipoEletroduto.SelectedIndex >= 0 ? _tiposDeEletroduto[TipoEletroduto.SelectedIndex] : null;
            _padrao.DnPadrao = Entrada.Inteiro(DnPadrao.Text, "DN padrão", 16, 150);
            _padrao.Passagem = Passagens[Math.Max(0, Passagem.SelectedIndex)].Valor;
            _padrao.CotaTeto = Entrada.Numero(CotaTeto.Text, "Cota do eixo no teto", 1.5, 12);
            _padrao.CotaPiso = Entrada.Numero(CotaPiso.Text, "Cota do eixo no piso", -0.5, 0.5);
            _padrao.FioCurvo = FioCurvo.IsChecked == true;
            _padrao.CorFases = CorFases.Text.Trim();
            _padrao.CorNeutro = CorNeutro.Text.Trim();
            _padrao.CorPE = CorPE.Text.Trim();
            _padrao.CorRetorno = CorRetorno.Text.Trim();
            Resultado = _padrao;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "AutoEletrica — Padrão", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
