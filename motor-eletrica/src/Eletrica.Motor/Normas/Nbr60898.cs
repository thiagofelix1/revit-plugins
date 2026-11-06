using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Normas;

/// <summary>ABNT NBR NM 60898:2004 — disjuntores para instalações domésticas e similares. Valores de projeto.</summary>
public static class Nbr60898
{
    public const string Documento = "ABNT NBR NM 60898:2004";

    /// <summary>5.3.2: correntes nominais preferenciais, em A.</summary>
    public static readonly double[] CorrentesNominais = [6, 8, 10, 13, 16, 20, 25, 32, 40, 50, 63, 80, 100, 125];

    /// <summary>5.3.4: capacidades de curto-circuito nominais padronizadas (inclusive as da nota Mercosul), em kA.</summary>
    public static readonly double[] CapacidadesDeCurto = [1.0, 1.5, 2.0, 2.5, 3.0, 4.5, 5.0, 6.0, 7.5, 9.0, 10.0, 20.0, 25.0];

    /// <summary>8.6.2.2 e 8.6.2.3: correntes convencionais de não atuação e de atuação, em múltiplos de In.</summary>
    public const double FatorNaoAtuacao = 1.13;
    public const double FatorAtuacao = 1.45;

    /// <summary>Tabela 2 e Tabela 6: limites da faixa de atuação instantânea, em múltiplos de In (o superior é o Ia garantido em menos de 0,1 s).</summary>
    public static (double Inferior, double Superior) FaixaInstantanea(CurvaDisjuntor curva) => curva switch
    {
        CurvaDisjuntor.B => (3, 5),
        CurvaDisjuntor.C => (5, 10),
        CurvaDisjuntor.D => (10, 20),
        _ => throw new ArgumentOutOfRangeException(nameof(curva)),
    };

    /// <summary>Corrente que garante atuação instantânea (t &lt; 0,1 s): o limite superior da faixa, em A.</summary>
    public static double CorrenteDeAtuacaoInstantanea(CurvaDisjuntor curva, double inA) => FaixaInstantanea(curva).Superior * inA;

    /// <summary>Tabela 6, ensaio c: a 2,55 In o disjuntor atua entre 1 s e 60 s (In ≤ 32 A) ou 120 s (In &gt; 32 A).</summary>
    public static double TempoMaximoA2_55In(double inA) => inA <= 32 ? 60 : 120;

    /// <summary>8.6.2.1: tempo convencional, em h.</summary>
    public static double TempoConvencional(double inA) => inA <= 63 ? 1 : 2;

    /// <summary>Tabela 15: Ics em função de Icn.</summary>
    public static double CapacidadeDeServico(double icnKA) => icnKA switch
    {
        <= 6 => icnKA,
        <= 10 => Math.Max(6, 0.75 * icnKA),
        _ => Math.Max(7.5, 0.5 * icnKA),
    };

    /// <summary>
    /// Corrente nominal mínima usual por tipo de circuito (prática de projeto, não é item da norma): 10 A em iluminação e
    /// tomadas de uso geral, para não ficar com disjuntor de 6 A protegendo tomadas.
    /// </summary>
    public static double InMinimoUsual(TipoCarga tipo) => tipo is TipoCarga.Iluminacao or TipoCarga.TomadaUsoGeral ? 10 : 0;

    /// <summary>Menor corrente nominal preferencial que não é menor que <paramref name="corrente" />; nula se passa de 125 A.</summary>
    public static double? MenorInNaoInferiorA(double corrente)
    {
        foreach (var inA in CorrentesNominais)
            if (inA >= corrente - 1e-9) return inA;
        return null;
    }

    /// <summary>Menor Icn padronizada que cobre a corrente de curto presumida, em kA.</summary>
    public static double IcnMinima(double iccKA)
    {
        foreach (var icn in CapacidadesDeCurto)
            if (icn >= iccKA - 1e-9) return icn;
        return CapacidadesDeCurto[CapacidadesDeCurto.Length - 1];
    }

    /// <summary>
    /// Anexo ZA, Tabelas ZA.1 e ZA.2: I²t máximo deixado passar (A²s) por classe de limitação de energia, curva B ou C,
    /// In até 16 A ou de 16 a 32 A (40 A pode usar 120 %), por Icn de 3, 4,5, 6 e 10 kA. Classe 1 não tem limite.
    /// Nulo quando a combinação não está no anexo (curva D, In acima de 40 A, Icn fora dos quatro valores).
    /// </summary>
    public static double? EnergiaPassanteMaxima(int classe, CurvaDisjuntor curva, double inA, double icnKA)
    {
        if (classe is not (2 or 3) || curva == CurvaDisjuntor.D || inA > 40) return null;
        var coluna = Array.IndexOf([3.0, 4.5, 6.0, 10.0], icnKA);
        if (coluna < 0) return null;
        var b = curva == CurvaDisjuntor.B;
        double[] valores = (inA <= 16, classe, b) switch
        {
            (true, 2, true) => [31_000, 60_000, 100_000, 240_000],
            (true, 2, false) => [37_000, 75_000, 120_000, 290_000],
            (true, 3, true) => [15_000, 25_000, 35_000, 70_000],
            (true, 3, false) => [18_000, 30_000, 42_000, 84_000],
            (false, 2, true) => [40_000, 80_000, 130_000, 310_000],
            (false, 2, false) => [50_000, 100_000, 160_000, 370_000],
            (false, 3, true) => [18_000, 32_000, 45_000, 90_000],
            _ => [22_000, 39_000, 55_000, 110_000],
        };
        var valor = valores[coluna];
        return inA > 32 ? valor * 1.2 : valor;
    }

    /// <summary>
    /// Curva sugerida pelo tipo de carga (prática de projeto, não é item da norma): B para resistivas e iluminação sem partida,
    /// C para uso geral e motores pequenos, D para transformadores e partidas pesadas.
    /// </summary>
    public static CurvaDisjuntor CurvaSugerida(TipoCarga tipo) => tipo switch
    {
        TipoCarga.Chuveiro or TipoCarga.Aquecimento => CurvaDisjuntor.B,
        TipoCarga.Motor => CurvaDisjuntor.D,
        _ => CurvaDisjuntor.C,
    };
}
