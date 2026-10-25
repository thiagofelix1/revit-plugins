namespace Spk.Motor.Modelo;

/// <summary>
/// Contorno de um setor em planta: a união dos retângulos de cobertura dos bicos, num referencial alinhado aos ramais
/// (u ao longo do ramal, v entre ramais), em metros. Serve à hachura da setorização e ao mapa-chave.
/// </summary>
public static class Contorno
{
    /// <summary>
    /// Mediana das distâncias entre posições vizinhas, agrupando as que ficam a menos de <paramref name="tolerancia" />.
    /// Meio metro absorve bico fora de prumo no modelo; nenhum espaçamento de norma chega perto disso.
    /// </summary>
    public static double Passo(IEnumerable<double> posicoes, double tolerancia = 0.5, double padrao = 3.0)
    {
        var grupos = new List<double>();
        foreach (var p in posicoes.OrderBy(p => p))
            if (grupos.Count == 0 || p - grupos[grupos.Count - 1] > tolerancia) grupos.Add(p);
        var passos = new List<double>();
        for (var i = 1; i < grupos.Count; i++) passos.Add(grupos[i] - grupos[i - 1]);
        passos.Sort();
        return passos.Count == 0 ? padrao : passos[passos.Count / 2];
    }

    /// <summary>
    /// Laços do contorno dos bicos em (u, v). Cada bico cobre meio passo para cada lado; vãos de até
    /// <paramref name="vaoMaximo" /> entre coberturas vizinhas são fechados, para variações de espaçamento não
    /// picotarem a hachura. Laço externo em sentido anti-horário, furo em horário, sem pontos repetidos.
    /// </summary>
    public static List<List<(double U, double V)>> DosBicos(IReadOnlyList<(double U, double V)> bicos, double vaoMaximo = 1.5)
    {
        if (bicos.Count == 0) return [];
        var meioU = Limitar(Passo(bicos.Select(b => b.U)) / 2);
        var meioV = Limitar(Passo(bicos.Select(b => b.V)) / 2);
        var retangulos = bicos.Select(b => (b.U - meioU, b.V - meioV, b.U + meioU, b.V + meioV)).ToList();
        return DeRetangulos(retangulos, vaoMaximo);
    }

    static double Limitar(double meio) => Math.Max(0.9, Math.Min(meio, 2.5));

    /// <summary>União dos retângulos (u0, v0, u1, v1) rasterizada, com fechamento morfológico e traçado das bordas.</summary>
    public static List<List<(double U, double V)>> DeRetangulos(IReadOnlyList<(double U0, double V0, double U1, double V1)> retangulos,
        double vaoMaximo = 1.5)
    {
        if (retangulos.Count == 0) return [];
        var u0 = retangulos.Min(r => r.U0);
        var v0 = retangulos.Min(r => r.V0);
        var u1 = retangulos.Max(r => r.U1);
        var v1 = retangulos.Max(r => r.V1);
        var celula = Math.Max(0.25, Math.Max(u1 - u0, v1 - v0) / 1500);
        var raio = Math.Max(0, (int)Math.Ceiling(vaoMaximo / 2 / celula));
        var borda = raio + 1;
        var nx = (int)Math.Ceiling((u1 - u0) / celula) + 2 * borda;
        var ny = (int)Math.Ceiling((v1 - v0) / celula) + 2 * borda;
        var origemU = u0 - borda * celula;
        var origemV = v0 - borda * celula;

        var cheio = new bool[nx, ny];
        foreach (var r in retangulos)
        {
            var i0 = (int)Math.Round((r.U0 - origemU) / celula);
            var i1 = (int)Math.Round((r.U1 - origemU) / celula);
            var j0 = (int)Math.Round((r.V0 - origemV) / celula);
            var j1 = (int)Math.Round((r.V1 - origemV) / celula);
            for (var i = Math.Max(0, i0); i < Math.Min(nx, i1); i++)
            for (var j = Math.Max(0, j0); j < Math.Min(ny, j1); j++)
                cheio[i, j] = true;
        }

        if (raio > 0) cheio = Erodir(Dilatar(cheio, raio), raio);
        return Tracar(cheio).Select(laco => laco.Select(p => (origemU + p.I * celula, origemV + p.J * celula)).ToList()).ToList();
    }

    static bool[,] Dilatar(bool[,] m, int r) => Filtro(m, r, qualquer: true);

    static bool[,] Erodir(bool[,] m, int r) => Filtro(m, r, qualquer: false);

    /// <summary>Máximo (dilatação) ou mínimo (erosão) num quadrado de lado 2r+1, separável em linhas e colunas. Fora da grade é vazio.</summary>
    static bool[,] Filtro(bool[,] m, int r, bool qualquer)
    {
        int nx = m.GetLength(0), ny = m.GetLength(1);
        var meio = new bool[nx, ny];
        for (var i = 0; i < nx; i++)
        for (var j = 0; j < ny; j++)
            meio[i, j] = Janela(k => k >= 0 && k < nx && m[k, j], i, r, qualquer);
        var fim = new bool[nx, ny];
        for (var i = 0; i < nx; i++)
        for (var j = 0; j < ny; j++)
            fim[i, j] = Janela(k => k >= 0 && k < ny && meio[i, k], j, r, qualquer);
        return fim;
    }

    static bool Janela(Func<int, bool> valor, int centro, int r, bool qualquer)
    {
        for (var k = centro - r; k <= centro + r; k++)
        {
            var v = valor(k);
            if (qualquer && v) return true;
            if (!qualquer && !v) return false;
        }
        return !qualquer;
    }

    /// <summary>
    /// Arestas de borda com o cheio à esquerda, encadeadas em laços. Num vértice com duas saídas (células que se tocam
    /// só pela quina) vira à esquerda, o que mantém as duas células em laços separados.
    /// </summary>
    static List<List<(int I, int J)>> Tracar(bool[,] cheio)
    {
        int nx = cheio.GetLength(0), ny = cheio.GetLength(1);
        bool C(int i, int j) => i >= 0 && j >= 0 && i < nx && j < ny && cheio[i, j];

        var saidas = new Dictionary<(int, int), List<(int, int)>>();
        void Aresta((int, int) de, (int, int) para)
        {
            if (!saidas.TryGetValue(de, out var lista)) saidas[de] = lista = [];
            lista.Add(para);
        }

        for (var i = 0; i < nx; i++)
        for (var j = 0; j < ny; j++)
        {
            if (!cheio[i, j]) continue;
            if (!C(i, j - 1)) Aresta((i, j), (i + 1, j));
            if (!C(i + 1, j)) Aresta((i + 1, j), (i + 1, j + 1));
            if (!C(i, j + 1)) Aresta((i + 1, j + 1), (i, j + 1));
            if (!C(i - 1, j)) Aresta((i, j + 1), (i, j));
        }

        var lacos = new List<List<(int I, int J)>>();
        while (saidas.Count > 0)
        {
            // O vértice mais baixo e mais à esquerda nunca é quina dupla: não há célula abaixo dele.
            var inicio = saidas.Keys.OrderBy(k => k.Item2).ThenBy(k => k.Item1).First();
            var laco = new List<(int I, int J)>();
            var atual = inicio;
            (int, int)? direcao = null;
            do
            {
                laco.Add(atual);
                var lista = saidas[atual];
                var proximo = lista.Count == 1 || direcao is null ? lista[0] : AEsquerda(atual, lista, direcao.Value);
                lista.Remove(proximo);
                if (lista.Count == 0) saidas.Remove(atual);
                direcao = (proximo.Item1 - atual.Item1, proximo.Item2 - atual.Item2);
                atual = proximo;
            } while (atual != inicio);
            lacos.Add(Simplificar(laco));
        }
        return lacos;
    }

    static (int, int) AEsquerda((int, int) atual, List<(int, int)> opcoes, (int, int) direcao)
    {
        var esquerda = (-direcao.Item2, direcao.Item1);
        foreach (var o in opcoes)
            if ((o.Item1 - atual.Item1, o.Item2 - atual.Item2) == esquerda) return o;
        return opcoes[0];
    }

    /// <summary>Tira os vértices no meio de arestas colineares.</summary>
    static List<(int I, int J)> Simplificar(List<(int I, int J)> laco)
    {
        var n = laco.Count;
        var resultado = new List<(int I, int J)>();
        for (var k = 0; k < n; k++)
        {
            var a = laco[(k - 1 + n) % n];
            var b = laco[k];
            var c = laco[(k + 1) % n];
            var cruz = (b.I - a.I) * (c.J - b.J) - (b.J - a.J) * (c.I - b.I);
            if (cruz != 0) resultado.Add(b);
        }
        return resultado;
    }

    /// <summary>Área com sinal pela fórmula do laço: positiva no sentido anti-horário.</summary>
    public static double Area(IReadOnlyList<(double U, double V)> laco)
    {
        double soma = 0;
        for (var k = 0; k < laco.Count; k++)
        {
            var a = laco[k];
            var b = laco[(k + 1) % laco.Count];
            soma += a.U * b.V - b.U * a.V;
        }
        return soma / 2;
    }
}
