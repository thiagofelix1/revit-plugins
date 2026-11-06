using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Lancamento;

public partial class InserirPontoJanela : Window
{
    public sealed record Escolha(FamilySymbol Simbolo, PapelFamilia Papel, PosicaoDoPonto Posicao, double Altura, double PotenciaVA, TipoCarga? Tipo, ElectricalSystem? Circuito);

    static readonly PapelFamilia[] Papeis = Enum.GetValues<PapelFamilia>().Where(p => p != PapelFamilia.Ignorar).ToArray();
    static readonly PosicaoDoPonto[] Posicoes = Enum.GetValues<PosicaoDoPonto>();
    static readonly TipoCarga[] Tipos = Enum.GetValues<TipoCarga>().Where(t => t != TipoCarga.Alimentador).ToArray();

    readonly Document _doc;
    readonly PadraoEletrica _padrao;
    readonly List<FamilySymbol> _simbolos;
    readonly List<ElectricalSystem> _circuitos;
    bool _carregando = true;

    public Escolha? Resultado { get; private set; }

    public InserirPontoJanela(Document doc, PadraoEletrica padrao, PapelFamilia inicial)
    {
        InitializeComponent();
        _doc = doc;
        _padrao = padrao;
        _simbolos = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .Where(s => s.Category?.BuiltInCategory is BuiltInCategory.OST_ElectricalFixtures or BuiltInCategory.OST_LightingFixtures or BuiltInCategory.OST_LightingDevices
                or BuiltInCategory.OST_ElectricalEquipment or BuiltInCategory.OST_ConduitFitting)
            .OrderBy(s => s.Category?.Name).ThenBy(PadraoEletrica.Nome).ToList();
        _circuitos = new FilteredElementCollector(doc).OfClass(typeof(ElectricalSystem)).Cast<ElectricalSystem>()
            .Where(s => s.SystemType == ElectricalSystemType.PowerCircuit && s.BaseEquipment is not null)
            .OrderBy(s => s.PanelName).ThenBy(s => s.CircuitNumber, StringComparer.OrdinalIgnoreCase).ToList();

        Papel.ItemsSource = Papeis.Select(PadraoEletrica.NomeDoPapel).ToList();
        Familia.ItemsSource = _simbolos.Select(s => $"{s.Category?.Name}: {PadraoEletrica.Nome(s)}").ToList();
        Posicao.ItemsSource = Posicoes.Select(PadraoEletrica.NomeDaPosicao).ToList();
        Tipo.ItemsSource = new[] { "(nenhum: interruptor, quadro, caixa)" }.Concat(Tipos.Select(t => t.ToString())).ToList();
        Circuito.ItemsSource = new[] { "(nenhum — dividir depois em Criar circuitos)" }
            .Concat(_circuitos.Select(c => $"{c.PanelName} {c.CircuitNumber} — {c.LoadName}")).ToList();
        Circuito.SelectedIndex = 0;
        _carregando = false;
        Papel.SelectedIndex = Math.Max(0, Array.IndexOf(Papeis, inicial));
    }

    void Papel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_carregando || Papel.SelectedIndex < 0) return;
        var papel = Papeis[Papel.SelectedIndex];
        var simbolo = _padrao.Simbolo(_doc, papel);
        Familia.SelectedIndex = simbolo is null ? -1 : _simbolos.FindIndex(s => s.Id == simbolo.Id);
        if (Familia.SelectedIndex < 0)
        {
            // sem papel confirmado: o primeiro tipo da categoria que faz sentido
            var categoria = papel switch
            {
                PapelFamilia.Luminaria or PapelFamilia.Arandela => BuiltInCategory.OST_LightingFixtures,
                PapelFamilia.Quadro => BuiltInCategory.OST_ElectricalEquipment,
                PapelFamilia.CaixaDePassagem => BuiltInCategory.OST_ConduitFitting,
                _ when PadraoEletrica.EhInterruptor(papel) => BuiltInCategory.OST_LightingDevices,
                _ => BuiltInCategory.OST_ElectricalFixtures,
            };
            Familia.SelectedIndex = _simbolos.FindIndex(s => s.Category?.BuiltInCategory == categoria);
        }
        Posicao.SelectedIndex = Array.IndexOf(Posicoes, PadraoEletrica.PosicaoDe(papel));
        Potencia.Text = Entrada.F(_padrao.PotenciaDe(papel, null));
        var tipo = PadraoEletrica.TipoDeCarga(papel);
        Tipo.SelectedIndex = tipo is null ? 0 : Array.IndexOf(Tipos, tipo.Value) + 1;
    }

    void Posicao_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Posicao.SelectedIndex < 0) return;
        Altura.Text = Entrada.F(_padrao.Lancamento.Altura(Posicoes[Posicao.SelectedIndex]));
    }

    void Inserir_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Familia.SelectedIndex < 0) throw new ArgumentException("Escolha a família. Se a lista está vazia, carregue as famílias elétricas do escritório no projeto.");
            var papel = Papeis[Math.Max(0, Papel.SelectedIndex)];
            Resultado = new Escolha(_simbolos[Familia.SelectedIndex], papel, Posicoes[Math.Max(0, Posicao.SelectedIndex)],
                Entrada.Numero(Altura.Text, "Altura", 0, 12), Entrada.Opcional(Potencia.Text, "Potência", 0, 100000) ?? 0,
                Tipo.SelectedIndex <= 0 ? null : Tipos[Tipo.SelectedIndex - 1],
                Circuito.SelectedIndex <= 0 ? null : _circuitos[Circuito.SelectedIndex - 1]);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "AutoEletrica — Inserir ponto", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
