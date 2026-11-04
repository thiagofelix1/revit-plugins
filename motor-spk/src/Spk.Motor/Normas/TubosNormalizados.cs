using System.Globalization;

namespace Spk.Motor.Normas;

/// <summary>Uma classe de tubo de aço num DN. O interno não vem pronto nas normas: é o externo menos duas espessuras.</summary>
public sealed record LinhaDeTubo(string Norma, string Classe, int Dn, double Externo, double Espessura)
{
    public double Interno => Externo - 2 * Espessura;

    public string Nome => $"NBR {Norma} {Classe}";

    /// <summary>NBR 10897 5.3.2.4: tubo roscado pede NBR 5580 classe média ou NBR 5590 classe normal. Classe mais grossa também serve (5.3.1).</summary>
    public bool AceitaRosca => Classe is not (TubosNormalizados.Leve or TubosNormalizados.Sch10);
}

/// <summary>
/// Tubos de aço aceitos pela NBR 10897 5.3.2, contra os quais o interno do tipo de tubo do Revit é conferido.
/// NBR 5580:2002, Tabela A.1; NBR 5590:2015, Tabela A.4 (DN 150 Sch 40 e Sch 80 pela Tabela A.5, porque a A.4 desalinha os rótulos dessa linha).
/// </summary>
public static class TubosNormalizados
{
    public const string Norma5580 = "5580";
    public const string Norma5590 = "5590";
    public const string Leve = "classe leve";
    public const string Media = "classe média";
    public const string Pesada = "classe pesada";
    public const string Sch10 = "Sch 10";
    public const string Sch40 = "Sch 40";
    public const string Sch80 = "Sch 80";

    /// <summary>Diferença de interno para considerar o mesmo tubo: as tabelas do Revit arredondam em polegada.</summary>
    const double Tolerancia = 0.2;

    // DN, externo, classes L, M e P. NaN: a classe não existe nesse DN.
    static readonly (int Dn, double Externo, double L, double M, double P)[] Nbr5580 =
    [
        (25, 33.7, 2.65, 3.35, 3.75), (32, 42.4, 2.65, 3.35, 3.75), (40, 48.3, 3.00, 3.35, 3.75), (50, 60.3, 3.00, 3.75, 4.50),
        (65, 76.1, 3.35, 3.75, 4.50), (80, 88.9, 3.35, 4.00, 4.50), (90, 101.6, 3.75, 4.25, 5.00), (100, 114.3, 3.75, 4.50, 5.60),
        (125, 139.7, double.NaN, 4.75, 5.60), (150, 165.1, double.NaN, 5.00, 5.60),
    ];

    // DN, externo, schedules 10, 40 e 80.
    static readonly (int Dn, double Externo, double S10, double S40, double S80)[] Nbr5590 =
    [
        (25, 33.4, 2.77, 3.38, 4.55), (32, 42.2, 2.77, 3.56, 4.85), (40, 48.3, 2.77, 3.68, 5.08), (50, 60.3, 2.77, 3.91, 5.54),
        (65, 73.0, 3.05, 5.16, 7.01), (80, 88.9, 3.05, 5.49, 7.62), (90, 101.6, 3.05, 5.74, 8.08), (100, 114.3, 3.05, 6.02, 8.56),
        (125, 141.3, 3.40, 6.55, 9.52), (150, 168.3, 3.40, 7.11, 10.97), (200, 219.1, 3.76, 8.18, 12.70),
    ];

    public static IReadOnlyList<LinhaDeTubo> Linhas(int dn)
    {
        var linhas = new List<LinhaDeTubo>();
        foreach (var t in Nbr5580.Where(t => t.Dn == dn))
        {
            if (!double.IsNaN(t.L)) linhas.Add(new LinhaDeTubo(Norma5580, Leve, dn, t.Externo, t.L));
            linhas.Add(new LinhaDeTubo(Norma5580, Media, dn, t.Externo, t.M));
            linhas.Add(new LinhaDeTubo(Norma5580, Pesada, dn, t.Externo, t.P));
        }
        foreach (var t in Nbr5590.Where(t => t.Dn == dn))
        {
            linhas.Add(new LinhaDeTubo(Norma5590, Sch10, dn, t.Externo, t.S10));
            linhas.Add(new LinhaDeTubo(Norma5590, Sch40, dn, t.Externo, t.S40));
            linhas.Add(new LinhaDeTubo(Norma5590, Sch80, dn, t.Externo, t.S80));
        }
        return linhas;
    }

    /// <summary>As classes com este interno. Vazio: não é de nenhuma das duas normas.</summary>
    public static IReadOnlyList<LinhaDeTubo> Identificar(int dn, double interno) =>
        Linhas(dn).Where(l => Math.Abs(l.Interno - interno) <= Tolerancia).ToList();

    /// <summary>NBR 10897 Tabela 3: parede mínima da NBR 5590 em tubo soldado ou acoplado. Nulo: DN fora da tabela.</summary>
    public static double? EspessuraMinima(int dn) => dn switch
    {
        25 or 32 or 40 or 50 => 2.77,
        65 or 80 or 90 or 100 => 3.05,
        125 or 150 => 3.4,
        200 or 250 => 4.78,
        300 => 8.38,
        _ => null,
    };

    /// <summary>
    /// O que o nome do tipo de tubo diz da norma. A rosca manda: BSP segue a ISO 7-1, que é a da NBR 5580 (1.2), e NPT é a da
    /// NBR 5590 (4.7.2). Nula: o nome não diz.
    /// </summary>
    public static (string? Norma, bool Roscado, string Motivo) NormaPeloNome(string nome)
    {
        var n = nome.ToLowerInvariant();
        var bsp = n.Contains("bsp") || n.Contains("iso 7");
        var npt = n.Contains("npt");
        if (bsp && npt) return (null, true, "");
        if (bsp) return (Norma5580, true, "rosca BSP");
        if (npt) return (Norma5590, true, "rosca NPT");
        var de5580 = n.Contains(Norma5580);
        var de5590 = n.Contains(Norma5590) || n.Contains("schedule") || n.Contains("sch ");
        if (de5580 == de5590) return (null, false, "");
        return de5580 ? (Norma5580, false, "") : (Norma5590, false, "");
    }

    /// <summary>
    /// O interno de cada DN de um tipo de tubo contra as normas. Só avisa e nunca troca o diâmetro: o tubo é escolha do projetista.
    /// Tipo que não se diz de aço, como "Padrão", não é conferido. Interno menor que o do nome dá perda maior que a real, a favor da
    /// segurança, e só informa.
    /// </summary>
    public static (List<string> Atencao, List<string> Informa) Conferir(string nomeDoTipo, IEnumerable<(int Dn, double Interno)> tubos)
    {
        var (atencao, informa) = (new List<string>(), new List<string>());
        if (Nbr10897.TuboPeloNome(nomeDoTipo, false) is not (TuboFatorC.AcoPretoSistemaMolhado or TuboFatorC.Galvanizado)) return (atencao, informa);

        var (norma, roscado, motivo) = NormaPeloNome(nomeDoTipo);
        var semNorma = new List<(int Dn, double Interno)>();
        var outraNorma = new List<(int Dn, double Interno, LinhaDeTubo Achada, LinhaDeTubo? DoNome)>();
        var semRosca = new List<(int Dn, LinhaDeTubo Achada)>();
        var paredeFina = new List<(int Dn, LinhaDeTubo Achada, double Minima)>();
        foreach (var (dn, interno) in tubos.Distinct().OrderBy(t => t.Dn))
        {
            if (Linhas(dn).Count == 0) continue;
            var achadas = Identificar(dn, interno);
            if (achadas.Count == 0)
            {
                semNorma.Add((dn, interno));
                continue;
            }
            if (norma is not null && achadas.All(l => l.Norma != norma))
                outraNorma.Add((dn, interno, achadas[0], Linhas(dn).FirstOrDefault(l => l.Norma == norma && l.Classe is Media or Sch40)));
            if (roscado && achadas.All(l => !l.AceitaRosca)) semRosca.Add((dn, achadas[0]));
            if (achadas.All(l => l.Norma == Norma5590) && EspessuraMinima(dn) is { } minima && achadas.All(l => l.Espessura < minima))
                paredeFina.Add((dn, achadas[0], minima));
        }

        if (semNorma.Count > 0)
            atencao.Add($"ATENÇÃO: tubo {nomeDoTipo}: {string.Join(", ", semNorma.Select(t => $"DN {t.Dn} com interno de {F(t.Interno)} mm"))} não " +
                        "corresponde a nenhuma classe das NBR 5580 e 5590 (NBR 10897 5.3.2). O interno entra direto na perda de carga: confira o segmento no Revit.");
        if (outraNorma.Count > 0)
        {
            var exemplo = outraNorma.Where(t => t.DoNome is not null).DefaultIfEmpty(outraNorma[outraNorma.Count - 1]).Last();
            var classes = string.Join(" e ", outraNorma.Select(t => t.Achada.Nome).Distinct());
            var comparacao = exemplo.DoNome is { } doNome ? $" (DN {exemplo.Dn}: {F(exemplo.Interno)} mm; na {doNome.Nome}, {F(doNome.Interno)} mm)" : "";
            var contra = outraNorma.Any(t => t.DoNome is { } d && t.Interno > d.Interno + Tolerancia);
            var texto = $"tubo {nomeDoTipo}: o nome indica NBR {norma}{(motivo.Length > 0 ? $" ({motivo})" : "")}, mas o interno de " +
                        $"{string.Join(", ", outraNorma.Select(t => $"DN {t.Dn}"))} é de {classes}{comparacao}. Confira o segmento no Revit: ";
            if (contra) atencao.Add("ATENÇÃO: " + texto + "se o tubo real for o do nome, a pressão calculada fica menor que a real.");
            else informa.Add("Em " + texto + "se o tubo real for o do nome, a pressão calculada fica maior que a real, a favor da segurança.");
        }
        if (semRosca.Count > 0)
            atencao.Add($"ATENÇÃO: tubo {nomeDoTipo}: o nome indica tubo roscado, mas {string.Join(", ", semRosca.Select(t => $"DN {t.Dn} é {t.Achada.Nome}"))}. " +
                        "Tubo roscado pede NBR 5580 classe média ou NBR 5590 classe normal (NBR 10897 5.3.2.4).");
        if (paredeFina.Count > 0)
            atencao.Add($"ATENÇÃO: tubo {nomeDoTipo}: {string.Join(", ", paredeFina.Select(t => $"DN {t.Dn} ({t.Achada.Nome}) tem parede de {F(t.Achada.Espessura)} mm, e a Tabela 3 da NBR 10897 pede {F(t.Minima)} mm"))}.");
        return (atencao, informa);
    }

    static string F(double valor) => valor.ToString("0.0#", CultureInfo.GetCultureInfo("pt-BR"));
}
