using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Calculo;

/// <summary>DN de cada trecho pela tabela, quantos chuveiros ele alimenta, e o que o engenheiro precisa saber.</summary>
public sealed record DiametrosPorTabela(
    IReadOnlyDictionary<string, int> DnPorTrecho,
    IReadOnlyDictionary<string, int> ChuveirosPorTrecho,
    IReadOnlyList<string> Avisos,
    bool EspacamentoMaior,
    double AreaDoSistema);

/// <summary>
/// Dimensionamento por tabela (<see cref="TabelaDeDiametros" />): a rede, a partir da fonte, é uma árvore; cada trecho
/// recebe o menor DN que alimenta os chuveiros a jusante dele. É o ponto de partida: o cálculo hidráulico confere, e
/// manda quando o sistema passa de 465 m², quando o K não é 80 ou quando há malha fechada.
/// </summary>
public static class DimensionamentoPorTabela
{
    public static DiametrosPorTabela Calcular(Rede rede, ClasseRisco classe)
    {
        if (!TabelaDeDiametros.TemTabela(classe))
            throw new InvalidOperationException("A norma não tem tabela de diâmetros para risco extra nem para armazenamento: " +
                                                "o dimensionamento é só pelo cálculo hidráulico.");

        var nos = rede.Nos.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var chuveiroNoNo = rede.Chuveiros.ToDictionary(c => c.No, StringComparer.Ordinal);
        var adj = new Dictionary<string, List<Trecho>>(StringComparer.Ordinal);
        foreach (var t in rede.Trechos)
        {
            Lista(adj, t.De).Add(t);
            Lista(adj, t.Para).Add(t);
        }

        // Árvore a partir da fonte: o trecho que chega a cada nó e a ordem de visita. Outro caminho até um nó já visitado
        // é malha fechada, que a tabela não cobre.
        var chegada = new Dictionary<string, Trecho?>(StringComparer.Ordinal) { [rede.NoFonte] = null };
        var ordem = new List<string> { rede.NoFonte };
        for (var i = 0; i < ordem.Count; i++)
        {
            var no = ordem[i];
            foreach (var t in adj.TryGetValue(no, out var ts) ? ts : [])
            {
                if (ReferenceEquals(t, chegada[no])) continue;
                var outro = t.De == no ? t.Para : t.De;
                if (chegada.ContainsKey(outro))
                    throw new InvalidOperationException("A rede tem malha fechada (grelha ou anel): o método por tabela vale só para " +
                                                        "rede em árvore. Dimensione pelo cálculo hidráulico.");
                chegada[outro] = t;
                ordem.Add(outro);
            }
        }

        var abaixo = ordem.ToDictionary(n => n, n => chuveiroNoNo.ContainsKey(n) ? 1 : 0, StringComparer.Ordinal);
        for (var i = ordem.Count - 1; i > 0; i--)
        {
            var t = chegada[ordem[i]]!;
            abaixo[t.De == ordem[i] ? t.Para : t.De] += abaixo[ordem[i]];
        }

        var avisos = new List<string>();
        var chuveiros = rede.Chuveiros.Where(c => chegada.ContainsKey(c.No)).ToList();
        var area = chuveiros.Sum(c => c.AreaCobertura);
        if (area >= TabelaDeDiametros.AreaMaximaSistemaNovo)
            avisos.Add($"O sistema tem {area:0} m²: em sistema novo, a tabela só vale abaixo de {TabelaDeDiametros.AreaMaximaSistemaNovo:0} m² " +
                       "(IT-23, 11.2.1.1). Os diâmetros servem de ponto de partida; o cálculo hidráulico decide.");
        if (chuveiros.Any(c => Math.Abs(c.FatorK - TabelaDeDiametros.FatorKDaTabela) > 0.5))
            avisos.Add("Há chuveiro com K diferente de 80: o sistema tem de ser calculado (IT-23, 11.2.1.2 a). " +
                       "Os diâmetros servem de ponto de partida.");

        var (dx, dy) = Topologia.DirecaoDosRamais(rede.Trechos, nos);
        var espacamento = classe != ClasseRisco.Leve ? MaiorEspacamento(chuveiros.Select(c => c.No).ToList(), rede.Trechos, nos, dx, dy) : 0;
        var espacado = espacamento > TabelaDeDiametros.EspacamentoLimite + 0.05;
        if (espacado)
            avisos.Add($"Chuveiros ou ramais a {espacamento:0.0#} m (mais de {TabelaDeDiametros.EspacamentoLimite:0.0} m): " +
                       "valem os números menores da Tabela 14 para DN 65, 80 e 90.");

        var dn = new Dictionary<string, int>(StringComparer.Ordinal);
        var porTrecho = new Dictionary<string, int>(StringComparer.Ordinal);
        var semTabela = 0;
        var leveComoOrdinario = false;
        foreach (var no in ordem.Skip(1))
        {
            var t = chegada[no]!;
            var n = abaixo[no];
            porTrecho[t.Id] = n;
            if (n == 0) continue;
            if (!TabelaDeDiametros.TemTabela(t.Material))
            {
                semTabela++;
                continue;
            }
            if (classe == ClasseRisco.Leve && n > TabelaDeDiametros.MaximoRiscoLeve(t.Material)) leveComoOrdinario = true;
            dn[t.Id] = TabelaDeDiametros.DnMinimo(classe, t.Material, n, espacado);
        }
        if (semTabela > 0)
            avisos.Add($"{semTabela} trecho(s) de material sem tabela (só aço e cobre têm): ficam como estão e vão pelo cálculo.");
        if (leveComoOrdinario)
            avisos.Add("Há trecho com mais chuveiros do que o DN 90 alimenta em risco leve: segue a tabela de risco ordinário (IT-23, 11.2.2.2.1).");

        // Ramal: começa no trecho paralelo aos ramais que sai de um trecho que não é (a subgeral ou a subida até o ramal).
        var lados = new List<int>();
        foreach (var no in ordem.Skip(1))
        {
            var t = chegada[no]!;
            var de = t.De == no ? t.Para : t.De;
            if (!Paralelo(t, nos, dx, dy) || (chegada[de] is { } anterior && Paralelo(anterior, nos, dx, dy))) continue;
            if (abaixo[no] > TabelaDeDiametros.MaximoPorLadoDoRamal) lados.Add(abaixo[no]);
        }
        if (lados.Count > 0)
            avisos.Add($"{lados.Count} ramal(is) com mais de {TabelaDeDiametros.MaximoPorLadoDoRamal} chuveiros de um lado da subgeral " +
                       $"(até {lados.Max()}). A IT-23 (11.2.2.1.1 e 11.2.3.1.1) aceita 9 ou 10 só com os DN 25 e 32 nos dois últimos " +
                       "segmentos e, com 10, o décimo por DN 65; acima disso, divida o ramal.");

        return new DiametrosPorTabela(dn, porTrecho, avisos, espacado, area);
    }

    static List<Trecho> Lista(Dictionary<string, List<Trecho>> adj, string no)
    {
        if (!adj.TryGetValue(no, out var lista)) adj[no] = lista = [];
        return lista;
    }

    static bool Paralelo(Trecho t, IReadOnlyDictionary<string, No> nos, double dx, double dy)
    {
        var a = nos[t.De];
        var b = nos[t.Para];
        double hx = b.X - a.X, hy = b.Y - a.Y;
        var horizontal = Math.Sqrt(hx * hx + hy * hy);
        return horizontal >= 0.30 && Math.Abs(b.Z - a.Z) < 0.05 && Math.Abs((hx * dx + hy * dy) / horizontal) > 0.95;
    }

    /// <summary>
    /// Maior distância, em planta, entre chuveiros vizinhos de um ramal ou entre ramais vizinhos que se sobrepõem ao
    /// longo da direção dos ramais.
    /// </summary>
    static double MaiorEspacamento(List<string> nosDeBico, IReadOnlyList<Trecho> trechos, IReadOnlyDictionary<string, No> nos,
        double dx, double dy)
    {
        var ramal = Topologia.RamalDeCadaBico(nosDeBico, trechos, nos, dx, dy);
        double Ao(string id) => nos[id].X * dx + nos[id].Y * dy;
        double Transversal(string id) => -nos[id].X * dy + nos[id].Y * dx;

        var maior = 0.0;
        var ramais = ramal.GroupBy(r => r.Value, r => r.Key)
            .Select(g => (Ramal: g.Select(Ao).OrderBy(v => v).ToList(), Posicao: g.Average(Transversal)))
            .OrderBy(r => r.Posicao)
            .ToList();
        foreach (var (ao, _) in ramais)
            for (var i = 1; i < ao.Count; i++)
                maior = Math.Max(maior, ao[i] - ao[i - 1]);
        for (var i = 1; i < ramais.Count; i++)
        {
            var (a, b) = (ramais[i - 1], ramais[i]);
            var sobrepoe = a.Ramal[0] <= b.Ramal.Last() + 0.5 && b.Ramal[0] <= a.Ramal.Last() + 0.5;
            if (sobrepoe) maior = Math.Max(maior, b.Posicao - a.Posicao);
        }
        return maior;
    }
}
