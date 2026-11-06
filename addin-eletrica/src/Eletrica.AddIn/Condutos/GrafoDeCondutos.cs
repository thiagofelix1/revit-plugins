using Autodesk.Revit.DB.Electrical;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Condutos;

/// <summary>
/// Os eletrodutos do modelo como grafo: nós nas pontas dos conectores (arredondadas ao milímetro), arestas são cadeias de
/// eletrodutos ligados por curvas e luvas (uma cadeia = um trecho do motor, entre caixas, tês, pontas livres e pontos).
/// Pontos e quadros são presos ao nó mais próximo dentro de uma tolerância.
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

    const double ToleranciaNo = 0.002; // m: conectores encaixados ficam no mesmo nó
    const double ToleranciaPonto = 0.8; // m: um ponto "está" no eletroduto que chega a até isto dele

    readonly Dictionary<string, XYZ> _nos = new();
    readonly List<(string A, string B, Element Elemento, double Comprimento)> _arestas = [];

    public IReadOnlyList<Cadeia> Cadeias { get; }
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

        Cadeias = MontarCadeias();
        if (conduites.Count == 0) avisos.Add("O modelo não tem eletrodutos (Conduit): o cálculo usa os comprimentos informados nos circuitos, sem agrupamento por trecho.");
        Avisos = avisos;
    }

    static IEnumerable<Connector> Conectores(ConnectorManager cm) => cm.Connectors.Cast<Connector>().Where(k => k.Domain != Domain.DomainUndefined);

    string No(XYZ p)
    {
        var m = new XYZ(Unidades.Metros(p.X), Unidades.Metros(p.Y), Unidades.Metros(p.Z));
        foreach (var (chave, existente) in _nos)
            if (existente.DistanceTo(m) <= ToleranciaNo) return chave;
        var nova = $"n{_nos.Count + 1}";
        _nos[nova] = m;
        return nova;
    }

    /// <summary>Nó mais próximo do ponto (em coordenadas internas), dentro da tolerância, ou nulo.</summary>
    public string? NoMaisProximo(XYZ p, double tolerancia = ToleranciaPonto)
    {
        var m = new XYZ(Unidades.Metros(p.X), Unidades.Metros(p.Y), Unidades.Metros(p.Z));
        string? melhor = null;
        var menor = tolerancia;
        foreach (var (chave, existente) in _nos)
        {
            var d = existente.DistanceTo(m);
            if (d <= menor) { menor = d; melhor = chave; }
        }
        return melhor;
    }

    /// <summary>Nó mais próximo de um elemento: pelos conectores de eletroduto dele, senão pelo ponto de inserção.</summary>
    public string? NoDe(FamilyInstance fi)
    {
        if (fi.MEPModel?.ConnectorManager is { } cm)
            foreach (var k in cm.Connectors.Cast<Connector>().Where(k => k.Domain == Domain.DomainCableTrayConduit))
                if (NoMaisProximo(k.Origin, 0.05) is { } no) return no;
        var p = (fi.Location as LocationPoint)?.Point ?? fi.get_BoundingBox(null)?.Min;
        return p is null ? null : NoMaisProximo(p);
    }

    List<Cadeia> MontarCadeias()
    {
        // Grau de cada nó; nós de grau 2 ligados só por curvas/luvas/eletrodutos são internos à cadeia.
        var grau = new Dictionary<string, int>();
        foreach (var (a, b, _, _) in _arestas)
        {
            grau[a] = grau.GetValueOrDefault(a) + 1;
            grau[b] = grau.GetValueOrDefault(b) + 1;
        }
        bool Interno(string no) => grau.GetValueOrDefault(no) == 2;

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
}
