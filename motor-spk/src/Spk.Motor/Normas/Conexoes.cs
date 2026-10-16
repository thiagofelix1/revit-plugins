namespace Spk.Motor.Normas;

public enum TipoConexao
{
    Cotovelo45,
    Cotovelo90Padrao,
    Cotovelo90Longo,
    /// <summary>Tê ou cruzeta com mudança de direção do fluxo (sem mudança não conta: 9.4.4.7 e).</summary>
    TeOuCruzetaFluxoDesviado,
    ValvulaBorboleta,
    ValvulaGaveta,
    ValvulaRetencao,
}

/// <summary>
/// Comprimentos equivalentes de literatura (tabela de aço Schedule 40 com C = 120 da NFPA 13),
/// usados só quando não há dado do fabricante (NBR 9.4.3.4). CONFERIR com o projetista e com catálogos.
/// </summary>
public static class ComprimentosEquivalentes
{
    static readonly int[] Dns = [25, 32, 40, 50, 65, 80, 100, 125, 150, 200];

    // Valores em pés, na ordem de Dns. NaN = não tabelado.
    static readonly Dictionary<TipoConexao, double[]> Pes = new()
    {
        [TipoConexao.Cotovelo45] = [1, 1, 2, 2, 3, 3, 4, 5, 7, 9],
        [TipoConexao.Cotovelo90Padrao] = [2, 3, 4, 5, 6, 7, 10, 12, 14, 18],
        [TipoConexao.Cotovelo90Longo] = [2, 2, 2, 3, 4, 5, 6, 8, 9, 13],
        [TipoConexao.TeOuCruzetaFluxoDesviado] = [5, 6, 8, 10, 12, 15, 20, 25, 30, 35],
        [TipoConexao.ValvulaBorboleta] = [double.NaN, double.NaN, double.NaN, 6, 7, 10, 12, 9, 10, 12],
        [TipoConexao.ValvulaGaveta] = [double.NaN, double.NaN, double.NaN, 1, 1, 1, 2, 2, 3, 4],
        [TipoConexao.ValvulaRetencao] = [5, 7, 9, 11, 14, 16, 22, 27, 32, 45],
    };

    static readonly (double C, double Fator)[] CorrecaoC = [(100, 0.713), (120, 1.0), (130, 1.16), (140, 1.33), (150, 1.51)];

    /// <summary>Comprimento equivalente em m, corrigido para o C do tubo e, se informado, para o diâmetro interno real.</summary>
    public static double Obter(TipoConexao tipo, int diametroNominal, double c = 120, double? diametroInterno = null)
    {
        var i = Array.IndexOf(Dns, diametroNominal);
        if (i < 0) throw new ArgumentException($"DN {diametroNominal} fora da tabela.");
        var pes = Pes[tipo][i];
        if (double.IsNaN(pes)) throw new ArgumentException($"{tipo} não tabelado para DN {diametroNominal}.");

        var metros = pes * 0.3048 * FatorDeC(c);
        if (diametroInterno is { } di)
            metros *= Math.Pow(di / TubosAco.Schedule40(diametroNominal), 4.87);
        return metros;
    }

    public static double Somar(IEnumerable<(TipoConexao Tipo, int Quantidade)> conexoes, int diametroNominal, double c = 120,
        double? diametroInterno = null) =>
        conexoes.Sum(x => x.Quantidade * Obter(x.Tipo, diametroNominal, c, diametroInterno));

    static double FatorDeC(double c)
    {
        if (c < CorrecaoC[0].C || c > CorrecaoC[CorrecaoC.Length - 1].C)
            throw new ArgumentOutOfRangeException(nameof(c), "Fator C fora da faixa 100–150.");
        for (var k = 1; k < CorrecaoC.Length; k++)
        {
            if (c > CorrecaoC[k].C) continue;
            var (c0, f0) = CorrecaoC[k - 1];
            var (c1, f1) = CorrecaoC[k];
            return f0 + (c - c0) / (c1 - c0) * (f1 - f0);
        }
        return 1.0;
    }
}

/// <summary>Diâmetros internos (mm) de tubos de aço ASME B36.10. CONFERIR quais tubos o projetista usa (NBR 5580, NBR 5590).</summary>
public static class TubosAco
{
    static readonly Dictionary<int, double> Sch40 = new()
    {
        [25] = 26.64, [32] = 35.05, [40] = 40.89, [50] = 52.50, [65] = 62.71,
        [80] = 77.93, [100] = 102.26, [125] = 128.19, [150] = 154.05, [200] = 202.72,
    };

    static readonly Dictionary<int, double> Sch10 = new()
    {
        [25] = 27.86, [32] = 36.62, [40] = 42.72, [50] = 54.79, [65] = 66.93,
        [80] = 82.80, [100] = 108.20, [125] = 134.50, [150] = 161.47, [200] = 211.56,
    };

    public static double Schedule40(int diametroNominal) =>
        Sch40.TryGetValue(diametroNominal, out var d) ? d : throw new ArgumentException($"DN {diametroNominal} sem Schedule 40.");

    public static double Schedule10(int diametroNominal) =>
        Sch10.TryGetValue(diametroNominal, out var d) ? d : throw new ArgumentException($"DN {diametroNominal} sem Schedule 10.");
}
