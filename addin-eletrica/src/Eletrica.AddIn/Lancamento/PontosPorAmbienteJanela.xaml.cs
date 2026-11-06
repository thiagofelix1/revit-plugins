using System.ComponentModel;
using System.Windows;
using Eletrica.AddIn.Ambientes;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Lancamento;

public partial class PontosPorAmbienteJanela : Window
{
    public sealed class Linha : INotifyPropertyChanged
    {
        bool _lancar;
        public AmbienteLido Ambiente { get; init; } = null!;
        public string Nome { get; init; } = "";
        public string Uso { get; init; } = "";
        public double Area { get; init; }
        public double Perimetro { get; init; }
        public int Tomadas { get; set; }
        public int Tomadas600 { get; set; }
        public int TomadasMedia { get; set; }
        public double LuzVA { get; set; }
        public bool Interruptor { get; set; } = true;
        public bool Chuveiro { get; set; }
        public bool Ar { get; set; }
        public int Existentes { get; init; }
        public string Motivos { get; init; } = "";

        public bool Lancar
        {
            get => _lancar;
            set { _lancar = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lancar))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    readonly List<Linha> _linhas;

    public IReadOnlyList<Linha>? Resultado { get; private set; }

    public PontosPorAmbienteJanela(IReadOnlyList<PropostaDoAmbiente> propostas, IReadOnlyDictionary<string, int> existentes, IReadOnlyDictionary<string, AmbienteLido> lidos)
    {
        InitializeComponent();
        _linhas = propostas.Select(p =>
        {
            var tomadas = p.Pontos.Where(x => x.Tipo == TipoCarga.TomadaUsoGeral).ToList();
            var ja = existentes.TryGetValue(p.Ambiente.Id, out var n) ? n : 0;
            var lido = lidos[p.Ambiente.Id];
            return new Linha
            {
                Ambiente = lido,
                Nome = $"{lido.Numero} {lido.Nome}".Trim() + (lido.Nivel.Length > 0 ? $" ({lido.Nivel})" : ""),
                Uso = ClassificadorDeAmbientes.Nome(p.Ambiente.Uso),
                Area = p.Ambiente.Area,
                Perimetro = p.Ambiente.Perimetro,
                Tomadas = tomadas.Sum(x => x.Quantidade),
                Tomadas600 = tomadas.Where(x => x.PotenciaVA >= 600).Sum(x => x.Quantidade),
                TomadasMedia = tomadas.Where(x => x.Posicao == PosicaoDoPonto.Media).Sum(x => x.Quantidade),
                LuzVA = p.CargaDeIluminacao,
                Chuveiro = p.Pontos.Any(x => x.Tipo == TipoCarga.Chuveiro),
                Ar = p.Pontos.Any(x => x.Tipo == TipoCarga.ArCondicionado),
                Existentes = ja,
                Lancar = ja == 0,
                Motivos = string.Join("\n", p.Pontos.Select(x => $"{x.Quantidade}× {x.Tipo} {x.PotenciaVA:0} VA ({x.Posicao}): {x.Motivo} [{x.Item}]")),
            };
        }).OrderBy(l => l.Ambiente.Nivel).ThenBy(l => l.Ambiente.Numero).ToList();
        Grade.ItemsSource = _linhas;
        Rodape.Text = $"{_linhas.Count} ambiente(s); \"Já tem\" conta os pontos elétricos que estão dentro do ambiente hoje.";
    }

    void Lancar_Click(object sender, RoutedEventArgs e)
    {
        Grade.CommitEdit();
        Grade.CommitEdit();
        var marcados = _linhas.Where(l => l.Lancar).ToList();
        if (marcados.Count == 0)
        {
            MessageBox.Show(this, "Marque pelo menos um ambiente.", "AutoEletrica — Pontos por ambiente", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Resultado = marcados;
        DialogResult = true;
    }
}
