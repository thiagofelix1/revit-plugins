using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Calculo;

/// <summary>
/// Grafo dos eletrodutos. Dá o comprimento real de cada circuito (do quadro ao ponto mais distante, pelos trechos em
/// que o circuito passa) e o caminho, para o agrupamento trecho a trecho.
/// </summary>
public sealed class Grafo
{
    readonly Instalacao _instalacao;
    readonly Dictionary<string, List<Trecho>> _porNo = new();

    public Grafo(Instalacao instalacao)
    {
        _instalacao = instalacao;
        foreach (var t in instalacao.Trechos)
        {
            Adicionar(t.De, t);
            Adicionar(t.Para, t);
        }
    }

    void Adicionar(string no, Trecho t)
    {
        if (!_porNo.TryGetValue(no, out var lista)) _porNo[no] = lista = [];
        lista.Add(t);
    }

    /// <summary>
    /// Comprimento do circuito até o ponto mais distante, em m, e os trechos percorridos. Nulo se o circuito não tem trechos
    /// ou algum ponto não tem caminho até o quadro.
    /// </summary>
    public (double Comprimento, IReadOnlyList<Trecho> Caminho)? ComprimentoDe(Circuito circuito)
    {
        var trechos = _instalacao.TrechosDo(circuito).ToList();
        if (trechos.Count == 0) return null;
        var origem = _instalacao.QuadroDe(circuito).NoDoQuadro;
        var destinos = circuito.QuadroAlimentado is { } qa
            ? [_instalacao.Quadro(qa)!.NoDoQuadro]
            : _instalacao.PontosDo(circuito).Select(p => p.NoDoPonto).Distinct().ToList();
        if (destinos.Count == 0) destinos = trechos.SelectMany(t => new[] { t.De, t.Para }).Distinct().ToList();

        var (dist, anterior) = Dijkstra(origem, circuito.Id);
        double maior = -1;
        string? maisLonge = null;
        foreach (var d in destinos)
        {
            if (!dist.TryGetValue(d, out var valor)) return null;
            if (valor > maior) { maior = valor; maisLonge = d; }
        }
        if (maisLonge is null) return null;

        var caminho = new List<Trecho>();
        for (var no = maisLonge; anterior.TryGetValue(no, out var passo); no = passo.De == no ? passo.Para : passo.De)
            caminho.Add(passo);
        caminho.Reverse();
        return (maior, caminho);
    }

    /// <summary>Caminho (trechos) do quadro até cada nó de destino do circuito. Destinos sem caminho ficam fora do dicionário.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Trecho>> CaminhosDe(Circuito circuito)
    {
        var resultado = new Dictionary<string, IReadOnlyList<Trecho>>();
        var destinos = circuito.QuadroAlimentado is { } qa
            ? [_instalacao.Quadro(qa)!.NoDoQuadro]
            : _instalacao.PontosDo(circuito).Select(p => p.NoDoPonto).Distinct().ToList();
        if (destinos.Count == 0) return resultado;
        var (dist, anterior) = Dijkstra(_instalacao.QuadroDe(circuito).NoDoQuadro, circuito.Id);
        foreach (var d in destinos)
        {
            if (!dist.ContainsKey(d)) continue;
            var caminho = new List<Trecho>();
            for (var no = d; anterior.TryGetValue(no, out var passo); no = passo.De == no ? passo.Para : passo.De)
                caminho.Add(passo);
            caminho.Reverse();
            resultado[d] = caminho;
        }
        return resultado;
    }

    /// <summary>Pontos do circuito sem caminho pelos eletrodutos até o quadro.</summary>
    public IEnumerable<Ponto> PontosSemCaminho(Circuito circuito)
    {
        if (!_instalacao.TrechosDo(circuito).Any()) yield break;
        var (dist, _) = Dijkstra(_instalacao.QuadroDe(circuito).NoDoQuadro, circuito.Id);
        foreach (var p in _instalacao.PontosDo(circuito))
            if (!dist.ContainsKey(p.NoDoPonto)) yield return p;
    }

    (Dictionary<string, double> Dist, Dictionary<string, Trecho> Anterior) Dijkstra(string origem, string circuito)
    {
        var dist = new Dictionary<string, double> { [origem] = 0 };
        var anterior = new Dictionary<string, Trecho>();
        var fila = new SortedSet<(double, string)>(Comparer<(double, string)>.Create((a, b) =>
            a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : string.CompareOrdinal(a.Item2, b.Item2))) { (0, origem) };
        while (fila.Count > 0)
        {
            var atual = fila.Min;
            fila.Remove(atual);
            var (d, no) = atual;
            if (d > dist[no]) continue;
            if (!_porNo.TryGetValue(no, out var trechos)) continue;
            foreach (var t in trechos)
            {
                if (!t.Circuitos.Contains(circuito)) continue;
                var vizinho = t.De == no ? t.Para : t.De;
                var nd = d + t.Comprimento;
                if (dist.TryGetValue(vizinho, out var atualD) && atualD <= nd) continue;
                dist[vizinho] = nd;
                anterior[vizinho] = t;
                fila.Add((nd, vizinho));
            }
        }
        return (dist, anterior);
    }
}
