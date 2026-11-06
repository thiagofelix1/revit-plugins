using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Normas;

/// <summary>
/// Fatores de demanda por tipo de carga (NBR 5410 4.2.1.1 admite a demanda; os valores vêm da prática e das normas das
/// distribuidoras, não da 5410). São os padrões do motor, declarados no memorial como tais; a norma da distribuidora do
/// projeto deve substituí-los quando chegar (item 1.7 do plano).
/// </summary>
public static class FatoresDeDemanda
{
    public const string Fonte = "prática de projeto e normas de distribuidoras (padrão do motor; conferir com a distribuidora)";

    /// <summary>Iluminação e tomadas de uso geral em habitação: fatores por faixa acumulada de potência instalada, em kVA.</summary>
    static readonly (double AteKva, double Fator)[] FaixasIluminacaoETug =
    [
        (1, 0.86), (2, 0.75), (3, 0.66), (4, 0.59), (5, 0.52), (6, 0.45), (7, 0.40), (8, 0.35), (9, 0.31), (10, 0.27), (double.MaxValue, 0.24),
    ];

    /// <summary>Aquecedores de água e chuveiros elétricos, pelo número de aparelhos.</summary>
    static readonly double[] Aquecimento = [1.00, 1.00, 0.84, 0.76, 0.70, 0.65, 0.60, 0.57, 0.54, 0.52, 0.49, 0.48, 0.47, 0.46, 0.45];

    /// <summary>Aparelhos de ar-condicionado, pelo número de aparelhos.</summary>
    static readonly double[] ArCondicionado = [1.00, 1.00, 0.90, 0.86, 0.82, 0.78, 0.75, 0.72, 0.70, 0.70, 0.68, 0.66, 0.65];

    /// <summary>Potência de demanda de iluminação + TUG a partir da instalada (VA), somando faixa a faixa.</summary>
    public static double DemandaIluminacaoETug(double instaladaVA)
    {
        double restante = instaladaVA / 1000, anterior = 0, demanda = 0;
        foreach (var (ate, fator) in FaixasIluminacaoETug)
        {
            var largura = Math.Min(ate - anterior, restante);
            if (largura <= 0) break;
            demanda += largura * fator;
            restante -= largura;
            anterior = ate;
        }
        return demanda * 1000;
    }

    /// <summary>Fator médio de iluminação + TUG para a potência instalada (para o relatório).</summary>
    public static double FatorIluminacaoETug(double instaladaVA) => instaladaVA <= 0 ? 1 : DemandaIluminacaoETug(instaladaVA) / instaladaVA;

    public static double FatorAquecimento(int aparelhos) => aparelhos <= 0 ? 1 : Aquecimento[Math.Min(aparelhos, Aquecimento.Length) - 1];

    public static double FatorArCondicionado(int aparelhos) => aparelhos <= 0 ? 1 : ArCondicionado[Math.Min(aparelhos, ArCondicionado.Length) - 1];

    /// <summary>Fator de tomadas de uso específico, motores e carregadores: 1,0 (demanda só com dado da distribuidora).</summary>
    public const double FatorUsoEspecifico = 1.0;

    /// <summary>Categoria de demanda de um tipo de carga.</summary>
    public static Categoria CategoriaDe(TipoCarga tipo) => tipo switch
    {
        TipoCarga.Iluminacao or TipoCarga.TomadaUsoGeral => Categoria.IluminacaoETug,
        TipoCarga.Chuveiro or TipoCarga.Aquecimento => Categoria.Aquecimento,
        TipoCarga.ArCondicionado => Categoria.ArCondicionado,
        _ => Categoria.UsoEspecifico,
    };

    public enum Categoria { IluminacaoETug, Aquecimento, ArCondicionado, UsoEspecifico }

    public static string Nome(Categoria c) => c switch
    {
        Categoria.IluminacaoETug => "iluminação e tomadas de uso geral",
        Categoria.Aquecimento => "chuveiros e aquecedores",
        Categoria.ArCondicionado => "ar-condicionado",
        _ => "uso específico (TUE, motores, VE, alimentadores)",
    };
}
