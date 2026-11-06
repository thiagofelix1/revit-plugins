using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Calculo;

/// <summary>
/// Balanceamento de fases do quadro (4.2.5.6, item 3.3 do plano): os circuitos com fase imposta pelo projetista ficam
/// onde ele pôs; os outros entram por potência decrescente na(s) fase(s) com menos carga. Devolve a(s) fase(s) de cada
/// circuito ("A", "B", "AB", "ABC"), a carga por fase e o desequilíbrio.
/// </summary>
public static class Fases
{
    public sealed record Resultado(IReadOnlyDictionary<string, string> FasePorCircuito, IReadOnlyList<CargaPorFase> Cargas, double DesequilibrioPercentual, IReadOnlyList<string> Avisos);

    public static readonly string[] Nomes = ["A", "B", "C"];

    public static Resultado Distribuir(Quadro q, IReadOnlyList<(Circuito Circuito, double PotenciaVA)> circuitos, double uo)
    {
        var n = Math.Max(1, Math.Min(3, q.Fases));
        var carga = new double[n];
        var fases = new Dictionary<string, string>();
        var avisos = new List<string>();

        int FasesDe(Ligacao l) => Math.Min(l switch { Ligacao.FN => 1, Ligacao.FF or Ligacao.FFN => 2, _ => 3 }, n);

        // 1. Impostas
        foreach (var (c, p) in circuitos.Where(x => !string.IsNullOrWhiteSpace(x.Circuito.FaseImposta)))
        {
            var pedidas = c.FaseImposta!.ToUpperInvariant().Where(ch => ch is 'A' or 'B' or 'C').Select(ch => ch - 'A').Where(k => k < n).Distinct().ToList();
            var precisa = FasesDe(c.Ligacao);
            if (pedidas.Count != precisa)
            {
                avisos.Add($"circuito {c.Numero}: fase imposta \"{c.FaseImposta}\" não combina com a ligação ({precisa} fase(s)); balanceado automaticamente");
                continue;
            }
            foreach (var k in pedidas) carga[k] += p / precisa;
            fases[c.Id] = string.Concat(pedidas.OrderBy(k => k).Select(k => Nomes[k]));
        }

        // 2. Automáticos, da maior para a menor potência, na(s) fase(s) menos carregada(s)
        foreach (var (c, p) in circuitos.Where(x => !fases.ContainsKey(x.Circuito.Id)).OrderByDescending(x => x.PotenciaVA).ThenBy(x => x.Circuito.Numero, StringComparer.OrdinalIgnoreCase))
        {
            var precisa = FasesDe(c.Ligacao);
            var escolhidas = Enumerable.Range(0, n).OrderBy(k => carga[k]).ThenBy(k => k).Take(precisa).OrderBy(k => k).ToList();
            foreach (var k in escolhidas) carga[k] += p / precisa;
            fases[c.Id] = string.Concat(escolhidas.Select(k => Nomes[k]));
        }

        var cargas = Enumerable.Range(0, n).Select(k => new CargaPorFase(Nomes[k], carga[k], uo > 0 ? carga[k] / uo : 0)).ToList();
        var media = carga.Average();
        var desequilibrio = media > 0 && n > 1 ? 100 * (carga.Max() - carga.Min()) / media : 0;
        if (desequilibrio > 20) avisos.Add($"desequilíbrio entre fases de {desequilibrio:0} % (recomendado ≤ 20 %): redistribuir os circuitos monofásicos ou impor fases");
        return new Resultado(fases, cargas, desequilibrio, avisos);
    }
}
