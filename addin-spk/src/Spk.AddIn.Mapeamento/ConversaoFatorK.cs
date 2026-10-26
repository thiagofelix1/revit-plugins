namespace Spk.AddIn.Mapeamento;

/// <summary>
///     O cálculo usa K em L/min/bar½. Famílias de fabricante costumam trazer o K americano (gpm/psi½), e um
///     número abaixo de 40 só pode estar nessa unidade: o menor K métrico usual é 40 (K 2,8 americano).
/// </summary>
public static class ConversaoFatorK
{
    public const double LimiarAmericano = 40;
    const double Fator = 14.41;

    static readonly (double Americano, double Metrico)[] Nominais =
    [
        (2.8, 40), (4.2, 60), (5.6, 80), (8.0, 115), (11.2, 160), (14.0, 200),
        (16.8, 240), (19.6, 280), (22.4, 320), (25.2, 360), (28.0, 400),
    ];

    /// <summary>Devolve o K métrico e uma nota quando houve conversão. Usa o nominal de catálogo quando o valor bate.</summary>
    public static (double Valor, string? Nota) ParaMetrico(double k)
    {
        if (k >= LimiarAmericano) return (k, null);
        var nominal = Nominais.FirstOrDefault(n => Math.Abs(n.Americano - k) < 0.05);
        var metrico = nominal.Metrico > 0 ? nominal.Metrico : Math.Round(k * Fator, 1);
        return (metrico, $"O parâmetro trouxe K {k:0.0#} (gpm/psi½); convertido para {metrico:0.#} L/min/bar½.");
    }
}
