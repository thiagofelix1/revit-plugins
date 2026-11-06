using System.ComponentModel;
using System.Windows;
using Eletrica.Motor.Lancamento;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Circuitos;

public partial class CriarCircuitosJanela : Window
{
    public sealed class Linha : INotifyPropertyChanged
    {
        bool _criar = true;
        string _descricao = "";
        public CircuitoProposto Proposto { get; init; } = null!;
        public string Numero { get; init; } = "";
        public string Tipo { get; init; } = "";
        public string Ligacao { get; init; } = "";
        public int Pontos { get; init; }
        public double PotenciaVA { get; init; }
        public double Ib { get; init; }
        public string Motivo { get; init; } = "";
        public string Elementos { get; init; } = "";

        public bool Criar
        {
            get => _criar;
            set { _criar = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Criar))); }
        }

        public string Descricao
        {
            get => _descricao;
            set { _descricao = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Descricao))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    readonly List<Linha> _linhas;

    public IReadOnlyList<CircuitoProposto>? Resultado { get; private set; }

    public CriarCircuitosJanela(CriarCircuitos.Proposta proposta, string quadro, IReadOnlyList<string> avisos)
    {
        InitializeComponent();
        _linhas = proposta.Circuitos.Select(c => new Linha
        {
            Proposto = c,
            Numero = c.Numero,
            Descricao = c.Descricao,
            Tipo = DivisaoDeCircuitos.NomeDoTipo(c.Tipo),
            Ligacao = CriarCircuitos.Nome(c.Ligacao),
            Pontos = c.Pontos.Count,
            PotenciaVA = c.PotenciaVA,
            Ib = c.CorrenteDeProjeto,
            Motivo = c.Motivo,
            Elementos = string.Join(", ", c.Pontos.Select(p => proposta.Elementos.TryGetValue(p, out var fi) ? $"{fi.Name} ({fi.Id})" : p)),
        }).ToList();
        Grade.ItemsSource = _linhas;
        Cabecalho.Text = $"Divisão proposta para os {proposta.Elementos.Count} ponto(s) sem circuito, no quadro {quadro}: iluminação separada das tomadas, " +
                         "cozinha/copa e área de serviço/lavanderia exclusivas, banheiro à parte, cada equipamento acima de 10 A sozinho, o resto por ambiente até o " +
                         "limite do padrão (NBR 5410 4.2.5.5, 9.5.3). Edite a descrição, desmarque o que não quiser e crie; mover pontos entre circuitos é no Revit mesmo.";
        Rodape.Text = $"{_linhas.Count} circuito(s); {proposta.PotenciaTotal():N0} VA." + (avisos.Count > 0 ? $" {avisos.Count} aviso(s) na leitura." : "");
        Detalhe.Text = avisos.Count > 0 ? string.Join("\n", avisos.Take(6)) + (avisos.Count > 6 ? $"\n... e mais {avisos.Count - 6}" : "") : "Selecione um circuito para ver os elementos.";
    }

    void Grade_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Grade.SelectedItem is Linha l) Detalhe.Text = $"{l.Numero} {l.Descricao}: {l.Elementos}";
    }

    void Criar_Click(object sender, RoutedEventArgs e)
    {
        Grade.CommitEdit();
        Grade.CommitEdit();
        var escolhidos = _linhas.Where(l => l.Criar).Select(l => l.Proposto with { Descricao = l.Descricao.Trim() }).ToList();
        if (escolhidos.Count == 0)
        {
            MessageBox.Show(this, "Marque pelo menos um circuito.", "AutoEletrica — Criar circuitos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Resultado = escolhidos;
        DialogResult = true;
    }
}

static class PropostaExtensoes
{
    public static double PotenciaTotal(this CriarCircuitos.Proposta p) => p.Circuitos.Sum(c => c.PotenciaVA);
}
