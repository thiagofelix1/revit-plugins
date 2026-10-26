namespace Spk.Motor.Modelo;

/// <summary>
/// Leitura da forma da rede a partir de nós e trechos, sem depender de onde eles vieram (Revit, IFC, JSON):
/// direção dos ramais, a que ramal cada bico pertence e qual parte da rede é o setor de uma VGA.
/// </summary>
public static class Topologia
{
    /// <summary>Direção dos ramais: o eixo horizontal com mais comprimento de trechos de 1 m ou mais.</summary>
    public static (double Dx, double Dy) DirecaoDosRamais(IEnumerable<Trecho> trechos, IReadOnlyDictionary<string, No> nos) =>
        EixoDominante(trechos.Where(t => t.Comprimento >= 1).Select(t =>
        {
            var a = nos[t.De];
            var b = nos[t.Para];
            return (b.X - a.X, b.Y - a.Y);
        }));

    /// <summary>
    /// Eixo horizontal dominante de um conjunto de segmentos (dx, dy), pesando pelo comprimento. Soma em ângulo dobrado:
    /// segmentos perpendiculares se anulam em vez de entortar a média, então ramais em X com subgeral em Y dão
    /// exatamente X. Sentido normalizado para x positivo (ou y positivo, se vertical).
    /// </summary>
    public static (double Dx, double Dy) EixoDominante(IEnumerable<(double Dx, double Dy)> segmentos)
    {
        double c = 0, s = 0;
        foreach (var (dx, dy) in segmentos)
        {
            var comprimento = Math.Sqrt(dx * dx + dy * dy);
            if (comprimento < 1e-6) continue;
            // L·cos 2θ e L·sin 2θ, com cos θ = dx/L e sin θ = dy/L.
            c += (dx * dx - dy * dy) / comprimento;
            s += 2 * dx * dy / comprimento;
        }

        if (Math.Sqrt(c * c + s * s) < 1e-9) return (1, 0);
        var angulo = Math.Atan2(s, c) / 2;
        double x = Math.Cos(angulo), y = Math.Sin(angulo);
        if (x < -1e-9 || (Math.Abs(x) <= 1e-9 && y < 0)) { x = -x; y = -y; }
        return (x, y);
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
    /// O pedaço de rede com mais bicos e a sua fonte, a ponta de tubo mais baixa. É o primeiro de
    /// <see cref="Componentes" />.
    /// </summary>
    public static (string Fonte, HashSet<string> Nos) MaiorComponente(IReadOnlyDictionary<string, No> nos, IEnumerable<Trecho> trechos,
        ISet<string> nosDeBico)
    {
        var componentes = Componentes(nos, trechos, nosDeBico);
        if (componentes.Count == 0) throw new InvalidOperationException("Os bicos não formam uma rede contínua com os tubos encontrados.");
        return (componentes[0].Fonte, componentes[0].Nos);
    }

    /// <summary>
    /// Cada pedaço de rede contínuo que tem bico, com a sua fonte: a ponta de tubo (nó de grau 1, que não é bico) mais
    /// baixa, onde a descida encontraria a VGA. Num modelo sem as VGAs desenhadas, cada setor fica um pedaço separado.
    /// Ordem: mais bicos primeiro; empate pela posição da fonte, para a numeração dos setores sair sempre igual.
    /// </summary>
    public static List<(string Fonte, HashSet<string> Nos, int Bicos)> Componentes(IReadOnlyDictionary<string, No> nos,
        IEnumerable<Trecho> trechos, ISet<string> nosDeBico)
    {
        var lista = trechos.ToList();
        var adj = Adjacencia(lista);
        var grau = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var trecho in lista)
        {
            grau[trecho.De] = (grau.TryGetValue(trecho.De, out var g1) ? g1 : 0) + 1;
            grau[trecho.Para] = (grau.TryGetValue(trecho.Para, out var g2) ? g2 : 0) + 1;
        }

        var visto = new HashSet<string>(StringComparer.Ordinal);
        var componentes = new List<(string Fonte, HashSet<string> Nos, int Bicos)>();
        foreach (var inicio in nos.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!visto.Add(inicio)) continue;
            var comp = new HashSet<string>(StringComparer.Ordinal) { inicio };
            var fila = new Queue<string>(comp);
            var bicos = 0;
            while (fila.Count > 0)
            {
                var n = fila.Dequeue();
                if (nosDeBico.Contains(n)) bicos++;
                foreach (var vizinho in Vizinhos(adj, n))
                    if (visto.Add(vizinho) && comp.Add(vizinho)) fila.Enqueue(vizinho);
            }
            if (bicos == 0) continue;

            var candidatos = comp.Where(id => !nosDeBico.Contains(id)).Select(id => nos[id]).ToList();
            var fonte = candidatos
                            .Where(n => grau.TryGetValue(n.Id, out var g) && g == 1)
                            .OrderBy(n => n.Z).ThenBy(n => n.Id, StringComparer.Ordinal)
                            .Select(n => n.Id).FirstOrDefault()
                        ?? candidatos.OrderBy(n => n.Z).ThenBy(n => n.Id, StringComparer.Ordinal).Select(n => n.Id).FirstOrDefault();
            if (fonte is null) continue;
            componentes.Add((fonte, comp, bicos));
        }

        return componentes
            .OrderByDescending(c => c.Bicos)
            .ThenBy(c => nos[c.Fonte].X).ThenBy(c => nos[c.Fonte].Y)
            .ToList();
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
