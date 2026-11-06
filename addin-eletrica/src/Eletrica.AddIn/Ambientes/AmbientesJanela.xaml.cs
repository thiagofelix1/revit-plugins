using System.ComponentModel;
using System.Windows;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Ambientes;

public partial class AmbientesJanela : Window
{
    public sealed class Linha : INotifyPropertyChanged
    {
        string _usoTexto = "";
        public string Id { get; init; } = "";
        public string Numero { get; init; } = "";
        public string Nome { get; init; } = "";
        public string Nivel { get; init; } = "";
        public string Origem { get; init; } = "";
        public double Area { get; init; }
        public double Perimetro { get; init; }
        public bool Confirmado { get; init; }
        public string Situacao => Confirmado ? "confirmado" : "sugerido";

        public string UsoTexto
        {
            get => _usoTexto;
            set { _usoTexto = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UsoTexto))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    static readonly UsoAmbiente[] Usos = Enum.GetValues<UsoAmbiente>();

    public Dictionary<string, UsoAmbiente>? Resultado { get; private set; }

    readonly List<Linha> _linhas;

    public AmbientesJanela(IReadOnlyList<AmbienteLido> ambientes)
    {
        InitializeComponent();
        ColunaUso.ItemsSource = Usos.Select(ClassificadorDeAmbientes.Nome).ToList();
        _linhas = ambientes.OrderBy(a => a.Nivel).ThenBy(a => a.Numero).Select(a => new Linha
        {
            Id = a.Id, Numero = a.Numero, Nome = a.Nome, Nivel = a.Nivel, Origem = a.Origem, Area = a.Area, Perimetro = a.Perimetro,
            Confirmado = a.Confirmado, UsoTexto = ClassificadorDeAmbientes.Nome(a.Uso),
        }).ToList();
        Grade.ItemsSource = _linhas;
    }

    void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        Grade.CommitEdit();
        Resultado = _linhas.ToDictionary(l => l.Id, l => Usos.First(u => ClassificadorDeAmbientes.Nome(u) == l.UsoTexto));
        DialogResult = true;
    }
}
