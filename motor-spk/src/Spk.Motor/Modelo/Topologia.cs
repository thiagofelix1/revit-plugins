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

        var lista = trechos.ToList();
        var bicos = new HashSet<string>(nosDeBico, StringComparer.Ordinal);
        var adj = Adjacencia(lista);
        foreach (var trecho in lista)
        {
            var a = nos[trecho.De];
            var b = nos[trecho.Para];
            double hx = b.X - a.X, hy = b.Y - a.Y;
            var horizontal = Math.Sqrt(hx * hx + hy * hy);
            if (horizontal >= 0.30 && Math.Abs((hx * dx + hy * dy) / horizontal) <= 0.95
                && !Braco(adj, nos, bicos, trecho.De, trecho.Para, dx, dy) && !Braco(adj, nos, bicos, trecho.Para, trecho.De, dx, dy)) continue;
            var ra = Raiz(trecho.De);
            var rb = Raiz(trecho.Para);
            if (ra != rb) pai[ra] = rb;
        }

        var grupos = bicos
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
    /// O trecho que sai de <paramref name="de" /> para <paramref name="para" /> é o braço de um bico (armover): a partir de
    /// <paramref name="para" />, sem nenhuma derivação no caminho (nós de grau 2: cotovelo, descida) e sem trecho paralelo
    /// ao ramal, chega-se a um bico na ponta da rede. Braço perpendicular ao ramal não separa ramais; a subgeral separa,
    /// porque liga tês ou leva a trechos paralelos (outro ramal, mesmo que de um bico só).
    /// </summary>
    static bool Braco(Dictionary<string, List<string>> adj, IReadOnlyDictionary<string, No> nos, ISet<string> bicos,
        string de, string para, double dx, double dy)
    {
        var anterior = de;
        var atual = para;
        for (var passos = 0; passos < 20; passos++)
        {
            if (passos > 0 && ParaleloAoRamal(nos[anterior], nos[atual], dx, dy)) return false;
            var vizinhos = Vizinhos(adj, atual).ToList();
            if (bicos.Contains(atual)) return vizinhos.Count == 1;
            if (vizinhos.Count != 2) return false;
            var proximo = vizinhos[0] == anterior ? vizinhos[1] : vizinhos[0];
            (anterior, atual) = (atual, proximo);
        }
        return false;
    }

    static bool ParaleloAoRamal(No a, No b, double dx, double dy)
    {
        double hx = b.X - a.X, hy = b.Y - a.Y;
        var horizontal = Math.Sqrt(hx * hx + hy * hy);
        return horizontal >= 0.30 && Math.Abs((hx * dx + hy * dy) / horizontal) > 0.95;
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
    /// Cada pedaço de rede contínuo que tem bico, com a sua fonte: a ponta aberta (nó de grau 1, que não é bico nem ponta
    /// fechada) de maior diâmetro efetivo (ver <see cref="DiametroEfetivo" />), e entre as iguais a mais baixa. É por ali
    /// que a VGA alimentaria: num modelo real a ponta mais baixa costuma ser um dreno fino, e uma ponta morta sem tampão
    /// pode ser mais grossa que a rede em que ela se pendura. Num modelo sem as VGAs desenhadas, cada setor fica um pedaço
    /// separado. Ordem: mais bicos primeiro; empate pela posição da fonte, para a numeração dos setores sair sempre igual.
    /// </summary>
    /// <param name="fechados">Nós que nunca são entrada (tampões): ficam por último na escolha.</param>
    public static List<(string Fonte, HashSet<string> Nos, int Bicos)> Componentes(IReadOnlyDictionary<string, No> nos,
        IEnumerable<Trecho> trechos, ISet<string> nosDeBico, ISet<string>? fechados = null)
    {
        var lista = trechos.ToList();
        var adj = Adjacencia(lista);
        var grau = new Dictionary<string, int>(StringComparer.Ordinal);
        var arestas = new Dictionary<string, List<(string Outro, double Diametro)>>(StringComparer.Ordinal);
        foreach (var trecho in lista)
        {
            grau[trecho.De] = (grau.TryGetValue(trecho.De, out var g1) ? g1 : 0) + 1;
            grau[trecho.Para] = (grau.TryGetValue(trecho.Para, out var g2) ? g2 : 0) + 1;
            // Ao milímetro: diferenças de arredondamento não podem desempatar.
            var d = Math.Round(trecho.DiametroInterno);
            if (!arestas.TryGetValue(trecho.De, out var a)) arestas[trecho.De] = a = [];
            if (!arestas.TryGetValue(trecho.Para, out var b)) arestas[trecho.Para] = b = [];
            a.Add((trecho.Para, d));
            b.Add((trecho.De, d));
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
                            .OrderBy(n => fechados?.Contains(n.Id) == true ? 1 : 0)
                            .ThenByDescending(n => DiametroEfetivo(n.Id, arestas, nosDeBico))
                            .ThenBy(n => n.Z).ThenBy(n => n.Id, StringComparer.Ordinal)
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
    /// O diâmetro com que a ponta alimenta a rede: o mais fino do caminho dela até o primeiro encontro (passando por
    /// cotovelos), comparado com o maior dos outros tubos desse encontro. A entrada de verdade tem a tubulação grossa dos
    /// dois lados (a coluna de 150 mm sobe para a geral de 150 mm); um dreno é fino; uma ponta morta sem tampão, mesmo
    /// grossa, só alimenta a rede mais fina em que se pendura.
    /// </summary>
    static double DiametroEfetivo(string ponta, Dictionary<string, List<(string Outro, double Diametro)>> arestas, ISet<string> nosDeBico)
    {
        var anterior = (string?)null;
        var atual = ponta;
        var minimo = double.MaxValue;
        for (var passos = 0; passos < 1000; passos++)
        {
            if (!arestas.TryGetValue(atual, out var deste)) break;
            var seguintes = deste.Where(a => a.Outro != anterior).ToList();
            if (anterior is not null && (seguintes.Count != 1 || nosDeBico.Contains(atual)))
                return seguintes.Count == 0 ? minimo : Math.Min(minimo, seguintes.Max(a => a.Diametro));
            if (seguintes.Count == 0) break;
            var (outro, diametro) = seguintes[0];
            minimo = Math.Min(minimo, diametro);
            (anterior, atual) = (atual, outro);
        }
        return minimo == double.MaxValue ? 0 : minimo;
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
