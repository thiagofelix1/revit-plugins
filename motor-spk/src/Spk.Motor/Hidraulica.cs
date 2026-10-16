namespace Spk.Motor;

/// <summary>
/// Equações básicas. Unidades do motor inteiro: vazão em L/min, pressão em kPa,
/// diâmetro interno em mm, comprimento e cota em m, fator K em L/min/bar^0,5.
/// </summary>
public static class Hidraulica
{
    /// <summary>Peso específico da água (ρ = 1000 kg/m³), em kPa por metro de coluna.</summary>
    public const double PesoEspecificoAgua = 9.80665;

    public const double ExpoenteHazenWilliams = 1.85;

    /// <summary>NBR 10897 9.4.2.1.1: J = 605 × Q^1,85 / (C^1,85 × d^4,87) × 10^5, com J em kPa/m.</summary>
    public const double CoeficienteHazenWilliams = 6.05e7;

    /// <summary>Perda de carga unitária de Hazen-Williams, em kPa/m.</summary>
    public static double PerdaUnitaria(double vazao, double c, double diametroInterno) =>
        CoeficienteHazenWilliams * Math.Pow(Math.Abs(vazao), ExpoenteHazenWilliams)
        / (Math.Pow(c, ExpoenteHazenWilliams) * Math.Pow(diametroInterno, 4.87));

    /// <summary>Resistência r do trecho, tal que a perda total é r × |Q|^1,85 (kPa).</summary>
    public static double Resistencia(double comprimentoTotal, double c, double diametroInterno) =>
        CoeficienteHazenWilliams * comprimentoTotal
        / (Math.Pow(c, ExpoenteHazenWilliams) * Math.Pow(diametroInterno, 4.87));

    /// <summary>Velocidade média, em m/s.</summary>
    public static double Velocidade(double vazao, double diametroInterno)
    {
        var areaM2 = Math.PI * diametroInterno * diametroInterno / 4e6;
        return Math.Abs(vazao) / 60000.0 / areaM2;
    }

    /// <summary>
    /// Pressão de velocidade ρv²/2, em kPa (≈ 225 × Q² / D⁴).
    /// A NBR 9.4.2.1.2 imprime 255; o valor físico é 225.
    /// </summary>
    public static double PressaoVelocidade(double vazao, double diametroInterno)
    {
        var v = Velocidade(vazao, diametroInterno);
        return 1000.0 * v * v / 2.0 / 1000.0;
    }

    /// <summary>NBR 5.2.2.1: Q = K × √P, com P em bar.</summary>
    public static double VazaoChuveiro(double fatorK, double pressao) =>
        pressao <= 0 ? 0 : fatorK * Math.Sqrt(pressao / 100.0);

    /// <summary>Pressão necessária para o chuveiro descarregar a vazão dada, em kPa.</summary>
    public static double PressaoParaVazao(double fatorK, double vazao)
    {
        var raiz = vazao / fatorK;
        return 100.0 * raiz * raiz;
    }

    /// <summary>Fator K convertido para L/min/kPa^0,5.</summary>
    public static double FatorKEmKPa(double fatorK) => fatorK / 10.0;
}
