using System.Collections.Concurrent;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Calculo;

public sealed record OpcoesBusca
{
    /// <summary>Espaçamento entre chuveiros no ramal, em m. Se nulo, usa a mediana medida na rede.</summary>
    public double? EspacamentoNoRamal { get; init; }

    /// <summary>Área por chuveiro, em m². Se nula, usa a mediana das áreas de cobertura.</summary>
    public double? AreaPorChuveiro { get; init; }

    /// <summary>Se informada, a pior área é a de menor folga em relação à curva; senão, a de maior pressão necessária.</summary>
    public CurvaAbastecimento? Abastecimento { get; init; }

    /// <summary>Vazão somada na fonte para comparar com a curva (hidrantes, NBR 8.5.1.1), em L/min.</summary>
    public double VazaoAdicionalNaFonte { get; init; }

    public int TamanhoDoRanking { get; init; } = 10;
    public int MaximoParalelismo { get; init; } = -1;
}

public sealed record CandidatoArea(IReadOnlyList<string> Chuveiros, double PressaoNecessaria, double VazaoNecessaria, double? FolgaAbastecimento);

public sealed record ResultadoBusca(
    ResultadoDemanda MaisDesfavoravel,
    IReadOnlyList<CandidatoArea> Ranking,
    int CandidatosAvaliados,
    int ChuveirosNaArea,
    int ChuveirosPorRamal,
    double EspacamentoNoRamal,
    double AreaPorChuveiro);

/// <summary>
/// Varre as posições da área de operação retangular (NBR 9.4.4.2): N chuveiros, com n em cada ramal ao longo
/// do lado paralelo aos ramais, avançando por ramais adjacentes. Cada posição é calculada e a pior é escolhida,
/// o que atende 9.4.4.4.2 (confirmação por programa de que a área escolhida é a de maior demanda).
/// </summary>
public static class BuscaAreaMaisDesfavoravel
{
    sealed record Ponto(Chuveiro Chuveiro, double T);

    public static ResultadoBusca Executar(MotorHidraulico motor, BaseDeProjeto baseDeProjeto, OpcoesBusca? opcoes = null)
    {
        var op = opcoes ?? new OpcoesBusca();
        var rede = motor.Rede;
        var ramais = OrganizarRamais(rede, out var espacamentoMedido);

        var espacamento = op.EspacamentoNoRamal ?? espacamentoMedido;
        var areaPorChuveiro = op.AreaPorChuveiro ?? Mediana(rede.Chuveiros.Select(c => c.AreaCobertura));
        var (total, porRamal) = baseDeProjeto.Geometria(areaPorChuveiro, espacamento);
        total = Math.Min(total, rede.Chuveiros.Count);

        var candidatos = GerarCandidatos(ramais, total, porRamal);
        if (candidatos.Count == 0) throw new InvalidOperationException("Nenhuma posição válida para a área de operação.");

        var avaliados = new ConcurrentBag<CandidatoArea>();
        Parallel.ForEach(candidatos, new ParallelOptions { MaxDegreeOfParallelism = op.MaximoParalelismo }, ids =>
        {
            var r = motor.CalcularDemanda(ids.Select(id => baseDeProjeto.Requisito(rede.ObterChuveiro(id))).ToList());
            double? folga = op.Abastecimento is { } curva
                ? curva.PressaoDisponivel(r.VazaoNecessaria + op.VazaoAdicionalNaFonte) - r.PressaoNecessaria
                : null;
            avaliados.Add(new CandidatoArea(ids, r.PressaoNecessaria, r.VazaoNecessaria, folga));
        });

        var ranking = (op.Abastecimento != null
                ? avaliados.OrderBy(c => c.FolgaAbastecimento)
                : avaliados.OrderByDescending(c => c.PressaoNecessaria))
            .ThenByDescending(c => c.VazaoNecessaria)
            .ThenBy(c => string.Join(",", c.Chuveiros), StringComparer.Ordinal)
            .ToList();

        var pior = ranking[0];
        var detalhe = motor.CalcularDemanda(pior.Chuveiros.Select(id => baseDeProjeto.Requisito(rede.ObterChuveiro(id))).ToList());
        return new ResultadoBusca(detalhe, ranking.Take(op.TamanhoDoRanking).ToList(), candidatos.Count, total, porRamal,
            espacamento, areaPorChuveiro);
    }

    /// <summary>Agrupa os chuveiros por ramal, ordena ao longo do ramal (T) e ordena os ramais pela posição transversal.</summary>
    static List<List<Ponto>> OrganizarRamais(Rede rede, out double espacamento)
    {
        var semRamal = rede.Chuveiros.Where(c => string.IsNullOrWhiteSpace(c.Ramal)).Select(c => c.Id).ToList();
        if (semRamal.Count > 0)
            throw new ArgumentException("Chuveiros sem ramal informado: " + string.Join(", ", semRamal.Take(10)));

        var grupos = rede.Chuveiros.GroupBy(c => c.Ramal)
            .Select(g => g.Select(c => rede.ObterNo(c.No)).Zip(g, (n, c) => (No: n, Chuveiro: c)).ToList())
            .ToList();

        // Direção comum dos ramais: soma das direções de cada ramal, alinhadas entre si.
        double dx = 0, dy = 0;
        foreach (var g in grupos.Where(g => g.Count >= 2))
        {
            var a = g.OrderByDescending(p => Dist2(p.No, g[0].No)).First().No;
            var b = g.OrderByDescending(p => Dist2(p.No, a)).First().No;
            double vx = b.X - a.X, vy = b.Y - a.Y;
            var norma = Math.Sqrt(vx * vx + vy * vy);
            if (norma == 0) continue;
            vx /= norma; vy /= norma;
            if (vx * dx + vy * dy < 0) { vx = -vx; vy = -vy; }
            dx += vx; dy += vy;
        }
        var nd = Math.Sqrt(dx * dx + dy * dy);
        if (nd == 0) { dx = 1; dy = 0; } else { dx /= nd; dy /= nd; }

        var ramais = grupos
            .Select(g => (
                Offset: g.Average(p => -dy * p.No.X + dx * p.No.Y),
                Pontos: g.Select(p => new Ponto(p.Chuveiro, dx * p.No.X + dy * p.No.Y)).OrderBy(p => p.T).ToList()))
            .OrderBy(r => r.Offset)
            .Select(r => r.Pontos)
            .ToList();

        var passos = ramais.SelectMany(r => r.Zip(r.Skip(1), (a, b) => b.T - a.T)).Where(d => d > 1e-6).ToList();
        espacamento = passos.Count > 0 ? Mediana(passos) : 1.0;
        return ramais;
    }

    static List<IReadOnlyList<string>> GerarCandidatos(List<List<Ponto>> ramais, int total, int porRamal)
    {
        var vistos = new HashSet<string>();
        var lista = new List<IReadOnlyList<string>>();

        for (var k = 0; k < ramais.Count; k++)
        foreach (var sentido in new[] { 1, -1 })
        {
            var primeiro = ramais[k];
            var nPrimeiro = Math.Min(Math.Min(porRamal, total), primeiro.Count);
            for (var w = 0; w + nPrimeiro <= primeiro.Count; w++)
            {
                var tInicio = primeiro[w].T;
                var tFim = primeiro[w + nPrimeiro - 1].T;
                foreach (var alinharNoFim in new[] { false, true })
                {
                    var ids = new List<string>();
                    for (var r = k; ids.Count < total && r >= 0 && r < ramais.Count; r += sentido)
                    {
                        var ramal = ramais[r];
                        var falta = Math.Min(Math.Min(porRamal, total - ids.Count), ramal.Count);
                        int inicio;
                        if (r == k) inicio = w;
                        else if (alinharNoFim && falta < porRamal) inicio = MaisProximo(ramal, tFim) - falta + 1;
                        else inicio = MaisProximo(ramal, tInicio);
                        inicio = Math.Max(0, Math.Min(inicio, ramal.Count - falta));
                        for (var i = inicio; i < inicio + falta; i++) ids.Add(ramal[i].Chuveiro.Id);
                    }
                    if (ids.Count < total) continue;
                    ids.Sort(StringComparer.Ordinal);
                    if (vistos.Add(string.Join("\u0001", ids))) lista.Add(ids);
                }
            }
        }
        return lista;
    }

    static int MaisProximo(List<Ponto> ramal, double t)
    {
        var melhor = 0;
        for (var i = 1; i < ramal.Count; i++)
            if (Math.Abs(ramal[i].T - t) < Math.Abs(ramal[melhor].T - t)) melhor = i;
        return melhor;
    }

    static double Dist2(No a, No b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    static double Mediana(IEnumerable<double> valores)
    {
        var v = valores.OrderBy(x => x).ToList();
        if (v.Count == 0) throw new ArgumentException("Sem valores.");
        return v.Count % 2 == 1 ? v[v.Count / 2] : 0.5 * (v[v.Count / 2 - 1] + v[v.Count / 2]);
    }
}
