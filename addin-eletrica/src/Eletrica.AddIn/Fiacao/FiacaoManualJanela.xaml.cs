using System.ComponentModel;
using System.Windows;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Fiacao;

public partial class FiacaoManualJanela : Window
{
    public const string Automatico = "automático";
    public const string Forcar = "forçar por aqui";
    public const string Evitar = "evitar por aqui";

    public sealed class Linha : INotifyPropertyChanged
    {
        string _modo = Automatico;
        public string Id { get; init; } = "";
        public string Numero { get; init; } = "";
        public string Quadro { get; init; } = "";
        public string Descricao { get; init; } = "";
        public string Hoje { get; init; } = "";

        public string Modo
        {
            get => _modo;
            set { _modo = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Modo))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    readonly List<Linha> _linhas;

    public (IReadOnlyList<string> Forcar, IReadOnlyList<string> Evitar, bool EvitarTodos)? Resultado { get; private set; }

    public FiacaoManualJanela(Instalacao i, int eletrodutos, IReadOnlySet<string> passam, IReadOnlySet<string> forcados, IReadOnlySet<string> evitados, bool evitarTodos)
    {
        InitializeComponent();
        ColunaModo.ItemsSource = new[] { Automatico, Forcar, Evitar };
        _linhas = i.Circuitos.OrderBy(c => i.QuadroDe(c).Nome).ThenBy(c => c.Numero, StringComparer.OrdinalIgnoreCase).Select(c => new Linha
        {
            Id = c.Id,
            Numero = c.Numero,
            Quadro = i.QuadroDe(c).Nome,
            Descricao = c.Descricao,
            Hoje = passam.Contains(c.Id) ? "passa" : "não passa",
            Modo = forcados.Contains(c.Id) ? Forcar : evitados.Contains(c.Id) ? Evitar : Automatico,
        }).ToList();
        Grade.ItemsSource = _linhas;
        EvitarTodos.IsChecked = evitarTodos;
        Cabecalho.Text = $"{eletrodutos} eletroduto(s) selecionado(s). \"Forçar\" obriga o circuito a passar por eles (entra na lista mesmo fora do menor caminho); " +
                         "\"evitar\" proíbe (o Passar circuitos procura outro caminho e avisa se não houver). O que ficar em automático segue o menor caminho. " +
                         "Fica gravado nos eletrodutos (AE Forçar / AE Evitar) e o Passar circuitos roda de novo ao aplicar.";
    }

    void Aplicar_Click(object sender, RoutedEventArgs e)
    {
        Grade.CommitEdit();
        Grade.CommitEdit();
        Resultado = (_linhas.Where(l => l.Modo == Forcar).Select(l => l.Id).ToList(), _linhas.Where(l => l.Modo == Evitar).Select(l => l.Id).ToList(), EvitarTodos.IsChecked == true);
        DialogResult = true;
    }
}
