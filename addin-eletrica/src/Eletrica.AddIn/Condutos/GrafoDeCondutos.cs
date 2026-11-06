using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Condutos;

/// <summary>
/// Os eletrodutos do modelo como grafo: nós nas pontas dos conectores (arredondadas ao milímetro), arestas são cadeias de
/// eletrodutos ligados por curvas e luvas (uma cadeia = um trecho do motor, entre caixas, tês, pontas livres e pontos).
/// Caixas de passagem (família do papel Caixa, ou com "caixa", "box" ou "condulete" no nome) quebram a cadeia no nó em
/// que estão, mesmo sem conector. Pontos e quadros são presos ao nó mais próximo dentro de uma tolerância.
/// </summary>
public sealed class GrafoDeCondutos
{
    public sealed class Cadeia
    {
        public string Id { get; init; } = "";
        public string De { get; init; } = "";
        public string Para { get; init; } = "";
        public double Comprimento { get; init; }
        public int Curvas { get; init; }
        public List<Conduit> Conduites { get; } = [];
        public List<FamilyInstance> Conexoes { get; } = [];
        public TipoEletroduto Tipo { get; init; }
        public int? Dn { get; init; }
    }

    public sealed record CaixaLida(FamilyInstance Elemento, string No);

    const double ToleranciaNo = 0.002; // m: conectores encaixados ficam no mesmo nó
    const double ToleranciaPonto = 0.8; // m: um ponto "está" no eletroduto que chega a até isto dele
    const double ToleranciaCaixa = 0.08; // m: a caixa "está" no nó que fica a até isto do centro dela

    readonly Dictionary<string, XYZ> _nos = new();
    readonly List<(string A, string B, Element Elemento, double Comprimento)> _arestas = [];
    readonly HashSet<string> _nosDeCaixa = [];

    public IReadOnlyList<Cadeia> Cadeias { get; }
    public IReadOnlyList<CaixaLida> Caixas { get; }
    public IReadOnlyList<string> Avisos { get; }

    public GrafoDeCondutos(Document doc)
    {
        var avisos = new List<string>();
        var conduites = new FilteredElementCollector(doc).OfClass(typeof(Conduit)).Cast<Conduit>().ToList();
        var conexoes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ConduitFitting).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();

        foreach (var c in conduites)
        {
            var pontas = Conectores(c.ConnectorManager).Select(k => k.Origin).ToList();
            if (pontas.Count < 2)
            {
                if (c.Location is LocationCurve lc) pontas = [lc.Curve.GetEndPoint(0), lc.Curve.GetEndPoint(1)];
                else continue;
            }
            var a = No(pontas[0]);
            var b = No(pontas[1]);
            if (a == b) continue;
            _arestas.Add((a, b, c, Unidades.Metros(pontas[0].DistanceTo(pontas[1]))));
        }
        foreach (var f in conexoes)
        {
            var pontas = f.MEPModel?.ConnectorManager is { } cm ? Conectores(cm).Select(k => k.Origin).ToList() : [];
            if (pontas.Count < 2) continue;
            // Conexão: liga todas as pontas entre si com comprimento nulo (curva, luva, tê, cruzeta).
            for (var i = 0; i < pontas.Count; i++)
            for (var j = i + 1; j < pontas.Count; j++)
            {
                var a = No(pontas[i]);
                var b = No(pontas[j]);
                if (a != b) _arestas.Add((a, b, f, 0));
            }
        }

        // Caixas: quebram a cadeia no nó em que estão.
        var padrao = PadraoEletrica.Carregar(doc);
        var caixas = new List<CaixaLida>();
        foreach (var fi in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(fi => EhCaixa(fi, padrao)))
        {
            var nos = new List<string>();
            if (fi.MEPModel?.ConnectorManager is { } cm)
                foreach (var k in cm.Connectors.Cast<Connector>().Where(k => k.Domain == Domain.DomainCableTrayConduit && TemPosicao(k)))
                    if (NoMaisProximo(k.Origin, 0.02) is { } no) nos.Add(no);
            if (nos.Count == 0 && (fi.Location as LocationPoint)?.Point is { } p && NoMaisProximo(p, ToleranciaCaixa) is { } noCentro) nos.Add(noCentro);
            foreach (var no in nos.Distinct())
            {
                _nosDeCaixa.Add(no);
                caixas.Add(new CaixaLida(fi, no));
            }
        }
        Caixas = caixas;

        Cadeias = MontarCadeias();
        if (conduites.Count == 0) avisos.Add("O modelo não tem eletrodutos (Conduit): o cálculo usa os comprimentos informados nos circuitos, sem agrupamento por trecho.");
        Avisos = avisos;
    }

    /// <summary>Caixa de passagem: o papel confirmado em Famílias, ou o nome da família.</summary>
    public static bool EhCaixa(FamilyInstance fi, PadraoEletrica padrao)
    {
        var categoria = fi.Category?.BuiltInCategory;
        if (categoria is not (BuiltInCategory.OST_ConduitFitting or BuiltInCategory.OST_ElectricalFixtures or BuiltInCategory.OST_ElectricalEquipment or BuiltInCategory.OST_GenericModel)) return false;
        if (fi.Symbol is { } s && padrao.PapelDe(s) == PapelFamilia.CaixaDePassagem) return true;
        var nome = (fi.Symbol?.FamilyName + " " + fi.Name).ToLowerInvariant();
        return nome.Contains("caixa") || nome.Contains("condulete") || nome.Contains("junction box") || nome.Contains("pull box") || nome.Contains("passagem");
    }

    static IEnumerable<Connector> Conectores(ConnectorManager cm) => cm.Connectors.Cast<Connector>().Where(k => k.Domain != Domain.DomainUndefined && TemPosicao(k));

    /// <summary>
    ///     Só os conectores físicos (ponta ou curva) têm posição: os lógicos e os de superfície (caixas e quadros de modelos de
    ///     terceiros, como o Snowdon) lançam exceção em Origin.
    /// </summary>
    public static bool TemPosicao(Connector k) => k.ConnectorType is ConnectorType.End or ConnectorType.Curve or ConnectorType.Physical;

    // Grade espacial dos nós (células de 1 m): num prédio com milhares de eletrodutos, procurar nó a nó ficava quadrático.
    const double Celula = 1.0;
    readonly Dictionary<(long, long, long), List<string>> _grade = new();

    static (long, long, long) CelulaDe(XYZ m) => ((long)Math.Floor(m.X / Celula), (long)Math.Floor(m.Y / Celula), (long)Math.Floor(m.Z / Celula));

    IEnumerable<string> Vizinhos(XYZ m, double raio)
    {
        var (i, j, k) = CelulaDe(m);
        var n = (long)Math.Ceiling(raio / Celula);
        for (var di = -n; di <= n; di++)
        for (var dj = -n; dj <= n; dj++)
        for (var dk = -n; dk <= n; dk++)
            if (_grade.TryGetValue((i + di, j + dj, k + dk), out var lista))
                foreach (var no in lista) yield return no;
    }

    string No(XYZ p)
    {
        var m = new XYZ(Unidades.Metros(p.X), Unidades.Metros(p.Y), Unidades.Metros(p.Z));
        foreach (var chave in Vizinhos(m, ToleranciaNo))
            if (_nos[chave].DistanceTo(m) <= ToleranciaNo) return chave;
        var nova = $"n{_nos.Count + 1}";
        _nos[nova] = m;
        var celula = CelulaDe(m);
        if (!_grade.TryGetValue(celula, out var daCelula)) _grade[celula] = daCelula = [];
        daCelula.Add(nova);
        return nova;
    }

    /// <summary>Nó mais próximo do ponto (em coordenadas internas), dentro da tolerância, ou nulo.</summary>
    public string? NoMaisProximo(XYZ p, double tolerancia = ToleranciaPonto)
    {
        var m = new XYZ(Unidades.Metros(p.X), Unidades.Metros(p.Y), Unidades.Metros(p.Z));
        string? melhor = null;
        var menor = tolerancia;
        foreach (var chave in Vizinhos(m, tolerancia))
        {
            var d = _nos[chave].DistanceTo(m);
            if (d <= menor) { menor = d; melhor = chave; }
        }
        return melhor;
    }

    /// <summary>Nó mais próximo de um elemento: pelos conectores de eletroduto dele, senão pelo ponto de inserção.</summary>
    public string? NoDe(FamilyInstance fi)
    {
        if (fi.MEPModel?.ConnectorManager is { } cm)
            foreach (var k in cm.Connectors.Cast<Connector>().Where(k => k.Domain == Domain.DomainCableTrayConduit && TemPosicao(k)))
                if (NoMaisProximo(k.Origin, 0.05) is { } no) return no;
        var p = (fi.Location as LocationPoint)?.Point ?? fi.get_BoundingBox(null)?.Min;
        return p is null ? null : NoMaisProximo(p);
    }

    /// <summary>Posição do nó em coordenadas internas (pés).</summary>
    public XYZ? PosicaoDoNo(string no) =>
        _nos.TryGetValue(no, out var m) ? new XYZ(Unidades.DeMetros(m.X), Unidades.DeMetros(m.Y), Unidades.DeMetros(m.Z)) : null;

    /// <summary>
    /// Os vértices da cadeia em ordem, do nó De ao nó Para, em coordenadas internas: as pontas dos eletrodutos, encadeadas.
    /// Serve para desenhar o fio e para andar ao longo do trecho (caixas por regra).
    /// </summary>
    public List<XYZ> Percurso(Cadeia cadeia)
    {
        var inicio = PosicaoDoNo(cadeia.De) ?? (cadeia.Conduites[0].Location as LocationCurve)!.Curve.GetEndPoint(0);
        var restantes = new List<Conduit>(cadeia.Conduites);
        var pontos = new List<XYZ> { inicio };
        var atual = inicio;
        while (restantes.Count > 0)
        {
            Conduit? proximo = null;
            XYZ? longe = null;
            var menor = double.MaxValue;
            foreach (var c in restantes)
            {
                if (c.Location is not LocationCurve lc) continue;
                var a = lc.Curve.GetEndPoint(0);
                var b = lc.Curve.GetEndPoint(1);
                var da = a.DistanceTo(atual);
                var db = b.DistanceTo(atual);
                if (Math.Min(da, db) < menor)
                {
                    menor = Math.Min(da, db);
                    proximo = c;
                    longe = da <= db ? b : a;
                }
            }
            if (proximo is null || longe is null) break;
            restantes.Remove(proximo);
            pontos.Add(longe);
            atual = longe;
        }
        return pontos;
    }

    List<Cadeia> MontarCadeias()
    {
        // Grau de cada nó; nós de grau 2 ligados só por curvas/luvas/eletrodutos são internos à cadeia, menos onde há caixa.
        var grau = new Dictionary<string, int>();
        foreach (var (a, b, _, _) in _arestas)
        {
            grau[a] = grau.GetValueOrDefault(a) + 1;
            grau[b] = grau.GetValueOrDefault(b) + 1;
        }
        bool Interno(string no) => grau.GetValueOrDefault(no) == 2 && !_nosDeCaixa.Contains(no);

        var usadas = new HashSet<int>();
        var cadeias = new List<Cadeia>();
        var porNo = new Dictionary<string, List<int>>();
        for (var i = 0; i < _arestas.Count; i++)
        {
            Add(porNo, _arestas[i].A, i);
            Add(porNo, _arestas[i].B, i);
        }

        for (var i = 0; i < _arestas.Count; i++)
        {
            if (usadas.Contains(i)) continue;
            var (a, b, _, _) = _arestas[i];
            // começa em uma ponta que não é interna, se houver; senão (anel), em qualquer uma
            var inicio = !Interno(a) ? a : !Interno(b) ? b : a;
            var atual = inicio;
            var aresta = i;
            var conduites = new List<Conduit>();
            var conexoes = new List<FamilyInstance>();
            double comprimento = 0;
            var curvas = 0;
            string fim;
            while (true)
            {
                usadas.Add(aresta);
                var (x, y, el, l) = _arestas[aresta];
                comprimento += l;
                if (el is Conduit c) conduites.Add(c);
                else if (el is FamilyInstance f)
                {
                    conexoes.Add(f);
                    if (EhCurva(f)) curvas++;
                }
                var proximo = x == atual ? y : x;
                fim = proximo;
                if (!Interno(proximo) || proximo == inicio) break;
                var seguinte = porNo[proximo].FirstOrDefault(k => !usadas.Contains(k), -1);
                if (seguinte < 0) break;
                atual = proximo;
                aresta = seguinte;
            }
            if (conduites.Count == 0) continue;
            // Eletrodutos emendados sem conexão (ponta com ponta) também contam curva quando mudam de direção.
            curvas += CurvasSemConexao(conduites, conexoes);
            var principal = conduites.OrderByDescending(c => Unidades.Metros(c.Diameter)).First();
            var cadeia = new Cadeia
            {
                Id = conduites[0].UniqueId,
                De = inicio,
                Para = fim,
                Comprimento = comprimento,
                Curvas = curvas,
                Tipo = TipoDe(principal),
                Dn = DnDe(principal),
            };
            cadeia.Conduites.AddRange(conduites);
            cadeia.Conexoes.AddRange(conexoes);
            cadeias.Add(cadeia);
        }
        return cadeias;
    }

    /// <summary>Mudanças de direção entre eletrodutos consecutivos que não passam por uma conexão (tipo sem conexões).</summary>
    static int CurvasSemConexao(List<Conduit> conduites, List<FamilyInstance> conexoes)
    {
        if (conexoes.Count > 0 || conduites.Count < 2) return 0;
        var curvas = 0;
        for (var i = 1; i < conduites.Count; i++)
        {
            if (conduites[i - 1].Location is not LocationCurve { Curve: Line a } || conduites[i].Location is not LocationCurve { Curve: Line b }) continue;
            if (Math.Abs(a.Direction.DotProduct(b.Direction)) < Math.Cos(30 * Math.PI / 180)) curvas++;
        }
        return curvas;
    }

    static void Add(Dictionary<string, List<int>> d, string k, int v)
    {
        if (!d.TryGetValue(k, out var l)) d[k] = l = [];
        l.Add(v);
    }

    static bool EhCurva(FamilyInstance f)
    {
        var nome = (f.Symbol?.FamilyName + " " + f.Name).ToLowerInvariant();
        if (nome.Contains("luva") || nome.Contains("coupling") || nome.Contains("união") || nome.Contains("uniao")) return false;
        var n = f.MEPModel?.ConnectorManager is { } cm ? cm.Connectors.Size : 0;
        return n == 2;
    }

    public static TipoEletroduto TipoDe(Conduit c)
    {
        var nome = (c.Document.GetElement(c.GetTypeId())?.Name + " " + c.Name).ToLowerInvariant();
        if (nome.Contains("corrug") || nome.Contains("flex")) return TipoEletroduto.FlexivelCorrugado;
        if (nome.Contains("aço") || nome.Contains("aco") || nome.Contains("steel") || nome.Contains("emt") || nome.Contains("galvan") || nome.Contains("rmc") || nome.Contains("imc"))
            return TipoEletroduto.Aco;
        if (nome.Contains("sold")) return TipoEletroduto.RigidoSoldavel;
        return TipoEletroduto.RigidoRoscavel;
    }

    /// <summary>DN pelo diâmetro nominal do Revit (mm): o tamanho da NBR 15465 mais próximo.</summary>
    public static int? DnDe(Conduit c)
    {
        var mm = Unidades.Milimetros(c.Diameter);
        if (mm <= 0) return null;
        int[] dns = [16, 20, 25, 32, 40, 50, 60, 75, 85, 110];
        return dns.OrderBy(d => Math.Abs(d - mm)).First();
    }

    public IReadOnlyDictionary<string, XYZ> Nos => _nos;

    /// <summary>Os pedaços do grafo que não se ligam entre si (diagnóstico do traçado): cada um é o conjunto dos seus nós.</summary>
    public List<HashSet<string>> Componentes()
    {
        var vizinhos = _nos.Keys.ToDictionary(n => n, _ => new List<string>());
        foreach (var (a, b, _, _) in _arestas)
        {
            vizinhos[a].Add(b);
            vizinhos[b].Add(a);
        }
        var vistos = new HashSet<string>();
        var componentes = new List<HashSet<string>>();
        foreach (var inicio in _nos.Keys)
        {
            if (!vistos.Add(inicio)) continue;
            var componente = new HashSet<string> { inicio };
            var fila = new Queue<string>([inicio]);
            while (fila.Count > 0)
                foreach (var v in vizinhos[fila.Dequeue()])
                    if (vistos.Add(v))
                    {
                        componente.Add(v);
                        fila.Enqueue(v);
                    }
            componentes.Add(componente);
        }
        return componentes.OrderByDescending(c => c.Count).ToList();
    }
}
