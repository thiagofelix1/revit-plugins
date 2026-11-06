using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Normas;

/// <summary>
/// Eletrodutos: tamanhos nominais da ABNT NBR 15465:2007 (Tabelas 4 e 5) e diâmetros internos típicos de catálogo,
/// porque a 15465 só fixa o diâmetro externo (3.3 e 5.2). Os internos devem ser trocados pelo catálogo do fabricante
/// que o escritório adota; ficam aqui com a fonte declarada.
/// </summary>
public static class Eletrodutos
{
    public const string Documento = "ABNT NBR 15465:2007 (DN e diâmetro externo); diâmetro interno de catálogo";

    public sealed record Tamanho(int Dn, double DiametroExterno, double DiametroInterno)
    {
        public double AreaInterna => Math.PI * DiametroInterno * DiametroInterno / 4;
    }

    static readonly Tamanho[] RigidoRoscavel =
    [
        new(20, 21.1, 17.0), new(25, 26.2, 21.6), new(32, 33.2, 27.8), new(40, 42.2, 36.2), new(50, 47.8, 41.2),
        new(60, 59.4, 52.4), new(75, 75.1, 67.3), new(85, 88.0, 79.8), new(110, 113.1, 103.5),
    ];

    static readonly Tamanho[] RigidoSoldavel =
    [
        new(20, 20.0, 16.6), new(25, 25.0, 21.4), new(32, 32.0, 28.2), new(40, 40.0, 36.0), new(50, 50.0, 45.6),
        new(60, 60.0, 55.4), new(75, 75.0, 70.0), new(85, 85.0, 80.0), new(110, 110.0, 104.6),
    ];

    static readonly Tamanho[] FlexivelCorrugado =
    [
        new(16, 16.0, 11.5), new(20, 20.0, 15.0), new(25, 25.0, 19.5), new(32, 32.0, 25.5), new(40, 40.0, 32.5),
    ];

    // Eletroduto de aço carbono pesado (NBR 5597/5598), diâmetros internos típicos.
    static readonly Tamanho[] Aco =
    [
        new(15, 21.3, 16.1), new(20, 26.9, 21.7), new(25, 33.7, 27.3), new(32, 42.4, 36.0), new(40, 48.3, 41.9),
        new(50, 60.3, 53.1), new(65, 76.1, 68.9), new(80, 88.9, 80.9), new(100, 114.3, 105.3),
    ];

    public static IReadOnlyList<Tamanho> Tamanhos(TipoEletroduto tipo) => tipo switch
    {
        TipoEletroduto.RigidoRoscavel => RigidoRoscavel,
        TipoEletroduto.RigidoSoldavel => RigidoSoldavel,
        TipoEletroduto.FlexivelCorrugado or TipoEletroduto.FlexivelPlano => FlexivelCorrugado,
        TipoEletroduto.Aco => Aco,
        _ => throw new ArgumentException($"{tipo} não é eletroduto: a ocupação segue outra regra (6.2.11.3 e 6.2.11.4)."),
    };

    public static Tamanho? PorDn(TipoEletroduto tipo, int dn) => Tamanhos(tipo).FirstOrDefault(t => t.Dn == dn);

    /// <summary>
    /// Menor DN cuja área interna comporta a área total dos condutores na taxa de ocupação da 5410 (6.2.11.1.6 a).
    /// Nulo se nem o maior tamanho basta (repetir o eletroduto ou mudar para calha).
    /// </summary>
    public static Tamanho? MenorQueComporta(TipoEletroduto tipo, double areaDosCondutores, int numeroDeCondutores)
    {
        var taxa = Nbr5410.TaxaDeOcupacaoMaxima(numeroDeCondutores);
        foreach (var t in Tamanhos(tipo))
            if (t.Dn >= Nbr5410.DnMinimo && areaDosCondutores <= taxa * t.AreaInterna + 1e-9) return t;
        return null;
    }

    /// <summary>Taxa de ocupação de um tamanho com a área de condutores dada.</summary>
    public static double TaxaDeOcupacao(Tamanho tamanho, double areaDosCondutores) => areaDosCondutores / tamanho.AreaInterna;
}
