using Spk.Motor.Modelo;

namespace Spk.Motor.Normas;

/// <summary>
/// Dimensionamento por tabela: quantos chuveiros cada DN pode alimentar. Valores da IT-23/2025 do CBPMESP, 11.2
/// (Tabelas 12, 13 e 14), que reproduz o método por tabela da ABNT NBR 10897. Só risco leve e ordinário, tubo de aço
/// ou cobre.
/// </summary>
public static class TabelaDeDiametros
{
    public const string Fonte = "IT-23/2025 (CBPMESP), 11.2, Tabelas 12 a 14";

    /// <summary>11.2.1.1: em sistema novo, o método por tabela só vale abaixo desta área (m²).</summary>
    public const double AreaMaximaSistemaNovo = 465;

    /// <summary>11.2.2.1.1 e 11.2.3.1.1: chuveiros em cada lado da subgeral (9 ou 10 com as exceções a e b).</summary>
    public const int MaximoPorLadoDoRamal = 8;

    /// <summary>11.2.3.2.2: distância entre chuveiros ou entre ramais acima da qual vale a Tabela 14 (m).</summary>
    public const double EspacamentoLimite = 3.7;

    /// <summary>11.2.1.2 a): fora deste K, o sistema é sempre calculado.</summary>
    public const double FatorKDaTabela = 80;

    static readonly (int Dn, int Chuveiros)[] LeveAco = [(25, 2), (32, 3), (40, 5), (50, 10), (65, 30), (80, 60), (90, 100)];
    static readonly (int Dn, int Chuveiros)[] LeveCobre = [(25, 2), (32, 3), (40, 5), (50, 12), (65, 40), (80, 65), (90, 115)];

    static readonly (int Dn, int Chuveiros)[] OrdinarioAco =
        [(25, 2), (32, 3), (40, 5), (50, 10), (65, 20), (80, 40), (90, 65), (100, 100), (125, 160), (150, 275)];

    static readonly (int Dn, int Chuveiros)[] OrdinarioCobre =
        [(25, 2), (32, 3), (40, 5), (50, 12), (65, 25), (80, 45), (90, 75), (100, 115), (125, 180), (150, 300)];

    static readonly (int Dn, int Chuveiros)[] EspacadoAco = [(65, 15), (80, 30), (90, 60)];
    static readonly (int Dn, int Chuveiros)[] EspacadoCobre = [(65, 20), (80, 35), (90, 65)];

    public static bool TemTabela(ClasseRisco classe) => classe is ClasseRisco.Leve or ClasseRisco.Ordinario1 or ClasseRisco.Ordinario2;

    public static bool TemTabela(Material material) => material is Material.Aco or Material.Cobre;

    /// <summary>Maior número de chuveiros da tabela de risco leve; acima disso, 11.2.2.2.1 manda usar a de ordinário.</summary>
    public static int MaximoRiscoLeve(Material material) => (material == Material.Cobre ? LeveCobre : LeveAco).Last().Chuveiros;

    /// <summary>
    /// Menor DN que alimenta <paramref name="chuveiros" />. Risco leve acima do DN 90 segue a tabela de ordinário
    /// (11.2.2.2.1). Acima da última linha, o DN seguinte ("Ver Tabela 1": o limite passa a ser a área da coluna).
    /// </summary>
    /// <param name="espacamentoMaior">Chuveiros ou ramais a mais de 3,7 m (Tabela 14, só risco ordinário).</param>
    public static int DnMinimo(ClasseRisco classe, Material material, int chuveiros, bool espacamentoMaior = false)
    {
        if (!TemTabela(classe)) throw new ArgumentException($"Não há tabela de diâmetros para {classe}: só cálculo hidráulico.");
        if (!TemTabela(material)) throw new ArgumentException($"Não há tabela de diâmetros para tubo de {material}: só cálculo hidráulico.");
        if (chuveiros < 1) throw new ArgumentOutOfRangeException(nameof(chuveiros));
        var cobre = material == Material.Cobre;

        if (classe == ClasseRisco.Leve && chuveiros <= MaximoRiscoLeve(material))
            return Primeiro(cobre ? LeveCobre : LeveAco, chuveiros) ?? 100;

        var ordinario = cobre ? OrdinarioCobre : OrdinarioAco;
        if (espacamentoMaior)
        {
            // Tabela 14 troca as linhas dos DN 65, 80 e 90; os outros DN seguem a Tabela 13.
            var espacado = cobre ? EspacadoCobre : EspacadoAco;
            ordinario = ordinario.Select(l => espacado.Any(e => e.Dn == l.Dn) ? espacado.First(e => e.Dn == l.Dn) : l).ToArray();
        }
        return Primeiro(ordinario, chuveiros) ?? 200;
    }

    static int? Primeiro((int Dn, int Chuveiros)[] tabela, int chuveiros)
    {
        foreach (var (dn, maximo) in tabela)
            if (chuveiros <= maximo) return dn;
        return null;
    }
}
