namespace Spk.Motor.Modelo;

/// <summary>
/// Leitura da forma da rede a partir de nós e trechos, sem depender de onde eles vieram (Revit, IFC, JSON):
/// direção dos ramais, a que ramal cada bico pertence e qual parte da rede é o setor de uma VGA.
/// </summary>
public static class Topologia
{
    /// <summary>Direção horizontal média dos trechos de 1 m ou mais, com os sentidos alinhados entre si.</summary>
    public static (double Dx, double Dy) DirecaoDosRamais(IEnumerable<Trecho> trechos, IReadOnlyDictionary<string, No> nos)
    {
        double dx = 0, dy = 0;
        var tem = false;
        foreach (var trecho in trechos)
        {
            if (trecho.Comprimento < 1) continue;
            var a = nos[trecho.De];
            var b = nos[trecho.Para];
            double vx = b.X - a.X, vy = b.Y - a.Y;
            var norma = Math.Sqrt(vx * vx + vy * vy);
            if (norma < 1e-6) continue;
            vx /= norma;
            vy /= norma;
            if (!tem)
            {
                dx = vx;
                dy = vy;
                tem = true;
                continue;
            }
            if (vx * dx + vy * dy < 0) { vx = -vx; vy = -vy; }
            dx += vx;
            dy += vy;
        }

        var n = Math.Sqrt(dx * dx + dy * dy);
        return n < 1e-6 ? (1, 0) : (dx / n, dy / n);
    }

    /// <summary>
    /// Ramal de cada bico, numerado pela posição transversal. Dois bicos estão no mesmo ramal quando ligados por
    /// trechos paralelos à direção dos ramais ou por descidas curtas (menos de 0,30 m na horizontal). A subgeral,
    /// perpendicular, separa os ramais. Os dois lados de uma subgeral na mesma linha formam um ramal só (NBR, Figura 46).
    /// </summary>
    public static Dictionary<string, int> RamalDeCadaBico(IEnumerable<string> nosDeBico, IEnumerable<Trecho> trechos,
        IReadOnlyDictionary<string, No> nos, double dx, double dy)
    {
        var pai = new Dictionary<string, string>(StringComparer.Ordinal);
        string Raiz(string n)
        {
            while (pai.TryGetValue(n, out var p) && p != n) n = p;
            return n;
        }

        foreach (var trecho in trechos)
        {
            var a = nos[trecho.De];
            var b = nos[trecho.Para];
            double hx = b.X - a.X, hy = b.Y - a.Y;
            var horizontal = Math.Sqrt(hx * hx + hy * hy);
            if (horizontal >= 0.30 && Math.Abs((hx * dx + hy * dy) / horizontal) <= 0.95) continue;
            var ra = Raiz(trecho.De);
            var rb = Raiz(trecho.Para);
            if (ra != rb) pai[ra] = rb;
        }

        var grupos = nosDeBico
            .GroupBy(Raiz, StringComparer.Ordinal)
            .OrderBy(g => g.Average(id => Transversal(nos[id], dx, dy)))
            .ToList();

        var ramal = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < grupos.Count; i++)
            foreach (var id in grupos[i])
                ramal[id] = i;
        return ramal;
    }

    /// <summary>
    /// O setor de uma VGA: cada lado dela é explorado sem atravessar outra VGA, e o lado com mais bicos é o
    /// setor (a jusante); o outro leva ao barrilete e à bomba. Vale também quando os dois lados encostam no mesmo nó.
    /// </summary>
    public static HashSet<string> SetorDaVga(string vga, IEnumerable<Trecho> trechos, ISet<string> nosDeBico, ISet<string> nosDeVga)
    {
        var adj = Adjacencia(trechos);
        HashSet<string>? melhor = null;
        var melhorQtd = 0;
        foreach (var vizinho in Vizinhos(adj, vga).Distinct())
        {
            if (nosDeVga.Contains(vizinho)) continue;
            var lado = new HashSet<string>(StringComparer.Ordinal) { vizinho };
            var fila = new Queue<string>(lado);
            var qtd = 0;
            while (fila.Count > 0)
            {
                var n = fila.Dequeue();
                if (nosDeBico.Contains(n)) qtd++;
                foreach (var proximo in Vizinhos(adj, n))
                    if (proximo != vga && !nosDeVga.Contains(proximo) && lado.Add(proximo)) fila.Enqueue(proximo);
            }
            if (qtd > melhorQtd) { melhorQtd = qtd; melhor = lado; }
        }

        if (melhor is null) throw new InvalidOperationException("Nenhum bico está ligado à VGA escolhida.");
        melhor.Add(vga);
        return melhor;
    }

    /// <summary>
    /// Sem VGA: o grupo de nós ligados com mais bicos, e como fonte a ponta de tubo (grau 1) mais baixa dele.
    /// </summary>
    public static (string Fonte, HashSet<string> Nos) MaiorComponente(IReadOnlyDictionary<string, No> nos, IEnumerable<Trecho> trechos,
        ISet<string> nosDeBico)
    {
        var lista = trechos.ToList();
        var adj = Adjacencia(lista);
        var visto = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string>? melhor = null;
        var melhorQtd = 0;
        foreach (var inicio in nos.Keys)
        {
            if (!visto.Add(inicio)) continue;
            var comp = new HashSet<string>(StringComparer.Ordinal) { inicio };
            var fila = new Queue<string>(comp);
            var qtd = 0;
            while (fila.Count > 0)
            {
                var n = fila.Dequeue();
                if (nosDeBico.Contains(n)) qtd++;
                foreach (var vizinho in Vizinhos(adj, n))
                    if (visto.Add(vizinho) && comp.Add(vizinho)) fila.Enqueue(vizinho);
            }
            if (qtd > melhorQtd) { melhorQtd = qtd; melhor = comp; }
        }

        if (melhor is null) throw new InvalidOperationException("Os bicos não formam uma rede contínua com os tubos encontrados.");

        var grau = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var trecho in lista.Where(t => melhor.Contains(t.De) && melhor.Contains(t.Para)))
        {
            grau[trecho.De] = (grau.TryGetValue(trecho.De, out var g1) ? g1 : 0) + 1;
            grau[trecho.Para] = (grau.TryGetValue(trecho.Para, out var g2) ? g2 : 0) + 1;
        }

        var candidatos = melhor.Where(id => !nosDeBico.Contains(id)).Select(id => nos[id]).ToList();
        var fonte = candidatos
                        .Where(n => grau.TryGetValue(n.Id, out var g) && g == 1)
                        .OrderBy(n => n.Z).ThenBy(n => n.Id, StringComparer.Ordinal)
                        .Select(n => n.Id).FirstOrDefault()
                    ?? candidatos.OrderBy(n => n.Z).ThenBy(n => n.Id, StringComparer.Ordinal).Select(n => n.Id).FirstOrDefault()
                    ?? throw new InvalidOperationException("A rede dos bicos não tem uma ponta de tubo para servir de fonte.");
        return (fonte, melhor);
    }

    /// <summary>
    /// Numeração dos nós a partir da fonte (1), em largura: o número cresce com a distância em trechos até a fonte,
    /// como os pontos de referência de um memorial. Empates na ordem do nó vizinho, para sair sempre igual.
    /// </summary>
    public static Dictionary<string, int> Numerar(string fonte, IEnumerable<Trecho> trechos)
    {
        var adj = Adjacencia(trechos);
        var numero = new Dictionary<string, int>(StringComparer.Ordinal) { [fonte] = 1 };
        var fila = new Queue<string>();
        fila.Enqueue(fonte);
        while (fila.Count > 0)
        {
            var n = fila.Dequeue();
            foreach (var vizinho in Vizinhos(adj, n).Distinct().OrderBy(v => v, StringComparer.Ordinal))
            {
                if (numero.ContainsKey(vizinho)) continue;
                numero[vizinho] = numero.Count + 1;
                fila.Enqueue(vizinho);
            }
        }
        return numero;
    }

    static double Transversal(No no, double dx, double dy) => -dy * no.X + dx * no.Y;

    static Dictionary<string, List<string>> Adjacencia(IEnumerable<Trecho> trechos)
    {
        var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Ligar(string a, string b)
        {
            if (!adj.TryGetValue(a, out var lista)) adj[a] = lista = [];
            lista.Add(b);
        }
        foreach (var trecho in trechos)
        {
            Ligar(trecho.De, trecho.Para);
            Ligar(trecho.Para, trecho.De);
        }
        return adj;
    }

    static IEnumerable<string> Vizinhos(Dictionary<string, List<string>> adj, string no) =>
        adj.TryGetValue(no, out var lista) ? lista : Enumerable.Empty<string>();
}
