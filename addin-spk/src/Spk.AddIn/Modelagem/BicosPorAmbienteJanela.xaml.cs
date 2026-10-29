using System.ComponentModel;
using System.Windows;
using Spk.AddIn.Arquitetura;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.AddIn.Modelagem;

public partial class BicosPorAmbienteJanela : Window
{
    public sealed class Linha : INotifyPropertyChanged
    {
        bool marcado;
        public required Ambiente Ambiente { get; init; }
        public string Nivel => Ambiente.Nivel;
        public string Nome => $"{Ambiente.Numero} {Ambiente.Nome}".Trim();
        public string Area => Ambiente.Area.ToString("0.0");
        public string PeDireito => Ambiente.PeDireito is { } pd ? pd.ToString("0.00") : "—";
        public string Teto => ArquiteturaJanela.TextoDoTeto(Ambiente.OrigemDoTeto);

        public bool Marcado
        {
            get => marcado;
            set
            {
                marcado = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Marcado)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    readonly List<Linha> linhas;

    /// <summary>Verdadeiro: posicionar; falso: só conferir os existentes.</summary>
    public bool Posicionar { get; private set; }
    public OpcoesBicos? Opcoes { get; private set; }

    /// <summary>No modo de geração: o arranjo, o drop e os diâmetros, com as opções dos bicos.</summary>
    public OpcoesRede? Rede { get; private set; }

    /// <summary>Gerar rede com geral: o engenheiro quer clicar onde fica a VGA, depois de fechar a janela.</summary>
    public bool VgaNoClique { get; private set; }

    readonly bool gerar;
    static readonly HashSet<string> ultimosMarcados = [];
    public IReadOnlyList<Ambiente> Escolhidos => linhas.Where(l => l.Marcado).Select(l => l.Ambiente).ToList();

    /// <param name="rede">Com ela, a janela é a do Gerar rede: mostra arranjo, drop e diâmetros, e o botão principal gera.</param>
    public BicosPorAmbienteJanela(Edificio edificio, IReadOnlyList<string> tipos, string? tipoPadrao, string resumo, OpcoesBicos opcoes,
        OpcoesRede? rede = null)
    {
        InitializeComponent();
        if (rede is not null)
        {
            gerar = true;
            Title = "SPK — Gerar rede nos ambientes";
            Geracao.Visibility = System.Windows.Visibility.Visible;
            BotaoConferir.Visibility = System.Windows.Visibility.Collapsed;
            BotaoPrincipal.Content = "Gerar rede";
            Arranjo.SelectedIndex = rede.Arranjo == AddIn.Arranjo.Lateral ? 1 : 0;
            Drop.Text = Entrada.F(rede.DropM);
            Diametros.IsChecked = rede.AplicarDiametros;
            var nomes = edificio.Ambientes.OrderBy(a => a.Piso).ThenBy(a => a.Numero, StringComparer.CurrentCulture)
                .Select(a => new ItemDeAmbiente(a)).ToList();
            GeralPor.ItemsSource = nomes;
            GeralPor.SelectedItem = nomes.FirstOrDefault(n => n.Ambiente.Id == rede.GeralPor)
                                    ?? nomes.FirstOrDefault(n => PareceCorredor(n.Ambiente.Nome)) ?? nomes.FirstOrDefault();
            JuntarNumSetor.IsChecked = rede.GeralPor is not null;
            BicosNoCorredor.IsChecked = rede.BicosNoCorredor;
        }
        Resumo.Text = resumo;
        // Começa com os ambientes da última vez (nenhum na primeira): o engenheiro escolhe onde mexer, e marcar o prédio
        // inteiro sem querer gerava rede até em escada e elevador.
        linhas = edificio.Ambientes.OrderBy(a => a.Piso).ThenBy(a => a.Numero, StringComparer.CurrentCulture)
            .Select(a => new Linha { Ambiente = a, Marcado = ultimosMarcados.Contains(a.Id) }).ToList();
        Lista.ItemsSource = linhas;
        Tipo.ItemsSource = tipos;
        Tipo.SelectedItem = opcoes.Tipo is { } t && tipos.Contains(t) ? t : tipoPadrao ?? tipos.FirstOrDefault();
        Defletor.Text = Entrada.F(opcoes.DefletorM);
        AbaixoDaViga.Text = Entrada.F(opcoes.AbaixoDaVigaM);
        Teto.SelectedIndex = (int)opcoes.Teto;
        Direcao.SelectedIndex = (int)opcoes.Direcao;
    }

    void Posicionar_Click(object sender, RoutedEventArgs e) => Fechar(true);

    void Conferir_Click(object sender, RoutedEventArgs e) => Fechar(false);

    void Fechar(bool posicionar)
    {
        if (!linhas.Any(l => l.Marcado))
        {
            MessageBox.Show(this, "Marque pelo menos um ambiente.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            Opcoes = new OpcoesBicos
            {
                Tipo = Tipo.SelectedItem as string,
                DefletorM = Entrada.Numero(Defletor.Text, "Defletor abaixo do teto", 0.025, 0.300),
                AbaixoDaVigaM = Entrada.Numero(AbaixoDaViga.Text, "Defletor abaixo da viga", 0.025, 0.150),
                Teto = (TipoDeTeto)Math.Max(0, Teto.SelectedIndex),
                Direcao = (DirecaoDosRamais)Math.Max(0, Direcao.SelectedIndex),
            };
            if (gerar)
                Rede = new OpcoesRede
                {
                    Arranjo = Arranjo.SelectedIndex == 1 ? AddIn.Arranjo.Lateral : AddIn.Arranjo.EspinhaDePeixe,
                    DropM = Entrada.Numero(Drop.Text, "Do defletor ao ramal", 0.15, 2),
                    AplicarDiametros = Diametros.IsChecked == true,
                    Bicos = Opcoes,
                    GeralPor = JuntarNumSetor.IsChecked == true ? (GeralPor.SelectedItem as ItemDeAmbiente)?.Ambiente.Id : null,
                    BicosNoCorredor = BicosNoCorredor.IsChecked == true,
                };
            VgaNoClique = gerar && VgaNoCliqueCaixa.IsChecked == true;
            Posicionar = posicionar;
            ultimosMarcados.Clear();
            ultimosMarcados.UnionWith(linhas.Where(l => l.Marcado).Select(l => l.Ambiente.Id));
            DialogResult = true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Um ambiente na lista da geral: "nível — número nome".</summary>
    sealed record ItemDeAmbiente(Ambiente Ambiente)
    {
        public override string ToString() => $"{Ambiente.Nivel} — {Ambiente.Numero} {Ambiente.Nome}".Trim();
    }

    static bool PareceCorredor(string nome) =>
        new[] { "corredor", "circula", "hall", "passagem", "corridor" }.Any(p => nome.Contains(p, StringComparison.CurrentCultureIgnoreCase));

    void Todos_Click(object sender, RoutedEventArgs e) => linhas.ForEach(l => l.Marcado = true);

    void Nenhum_Click(object sender, RoutedEventArgs e) => linhas.ForEach(l => l.Marcado = false);

    void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
