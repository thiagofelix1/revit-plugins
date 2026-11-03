using System.Windows;
using Spk.Motor.Modelo;

namespace Spk.AddIn.Arquitetura;

public partial class ArquiteturaJanela : Window
{
    public sealed record Linha(string Nivel, string Ambiente, string Area, string PeDireito, string Teto, string Origem);

    readonly string relatorio;

    public ArquiteturaJanela(Edificio edificio)
    {
        InitializeComponent();
        Resumo.Text = $"{edificio.Ambientes.Count} ambientes e {edificio.Vigas.Count} vigas lidos";
        Detalhe.Text = string.Join("\n", edificio.Fontes.Concat(edificio.Avisos).DefaultIfEmpty("Nada lido."));
        Lista.ItemsSource = edificio.Ambientes.OrderBy(a => a.Piso).ThenBy(a => a.Numero, StringComparer.CurrentCulture)
            .Select(a => new Linha(a.Nivel, string.IsNullOrEmpty(a.Numero) ? a.Nome : $"{a.Numero} {a.Nome}", a.Area.ToString("0.0"),
                a.PeDireito is { } pd ? pd.ToString("0.00") : "—", TextoDoTeto(a.OrigemDoTeto), a.Origem))
            .ToList();
        relatorio = Relatorio(edificio);
    }

    public static string TextoDoTeto(OrigemDoTeto origem) => origem switch
    {
        OrigemDoTeto.Forro => "forro",
        OrigemDoTeto.Laje => "laje",
        OrigemDoTeto.Telhado => "telhado",
        OrigemDoTeto.AlturaDoAmbiente => "altura do ambiente",
        _ => "não achado",
    };

    /// <summary>Uma linha por ambiente e por viga, para copiar e para o teste automático.</summary>
    public static string Relatorio(Edificio edificio) =>
        string.Join("\n", edificio.Fontes.Concat(edificio.Avisos)
            .Concat(edificio.Ambientes.Select(a =>
                $"{a.Nivel} | {a.Numero} {a.Nome} | {a.Area:0.0} m² | piso {a.Piso:0.00} | teto {(a.Teto is { } t ? t.ToString("0.00") : "—")} " +
                $"({TextoDoTeto(a.OrigemDoTeto)}) | pé-direito {(a.PeDireito is { } pd ? pd.ToString("0.00") : "—")}{(a.Inclinacao is > 0.02 ? $" | inclinação {a.Inclinacao * 100:0.#} %" : "")} | {Extensao(a)} | {a.Origem}"))
            .Concat(edificio.Vigas.Select(v =>
                $"viga | {v.Comprimento:0.00} m × {v.Largura:0.00} m | fundo {v.Fundo:0.00} | topo {v.Topo:0.00} | " +
                $"de {v.A.X:0.00};{v.A.Y:0.00} a {v.B.X:0.00};{v.B.Y:0.00} | {v.Origem}")));

    static string Extensao(Ambiente a)
    {
        var pontos = a.Contornos.SelectMany(c => c).ToList();
        return pontos.Count == 0 ? "sem contorno"
            : $"x {pontos.Min(p => p.X):0.00} a {pontos.Max(p => p.X):0.00}, y {pontos.Min(p => p.Y):0.00} a {pontos.Max(p => p.Y):0.00}";
    }

    void Copiar_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(relatorio);

    void Fechar_Click(object sender, RoutedEventArgs e) => Close();
}
