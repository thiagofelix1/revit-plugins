using Autodesk.Revit.DB.Plumbing;

namespace Spk.AddIn.Modelagem;

/// <summary>Uma rede de sala: quantos bicos, onde fica a entrada dela e por que não foi ligada.</summary>
public sealed record RedeLigada(int Bicos, XYZ Entrada, string? Recusa);

public sealed record ResultadoGeral(IReadOnlyList<RedeLigada> Redes, IReadOnlyList<ElementId> Criados, string? Aviso = null)
{
    public int Ligadas => Redes.Count(r => r.Recusa is null);

    public string Texto()
    {
        var linhas = new List<string> { $"{Ligadas} de {Redes.Count} rede(s) ligada(s) à geral." };
        foreach (var r in Redes.Where(r => r.Recusa is not null))
            linhas.Add($"• Rede de {r.Bicos} bico(s) com entrada em x {Metros(r.Entrada.X):0.00}, y {Metros(r.Entrada.Y):0.00}: {r.Recusa}");
        if (Aviso is not null) linhas.Add("• " + Aviso);
        return string.Join("\n", linhas);
    }

    static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);
}

/// <summary>
///     Liga as redes das salas a uma geral desenhada pelo engenheiro, como na obra: a VGA alimenta a geral, que passa pelo
///     corredor, e cada sala sai dela. A entrada de cada rede solta é a ponta aberta mais baixa: a descida até o piso que o
///     Gerar rede deixa (sai, com o cotovelo de cima) ou uma ponta horizontal desenhada à mão. Da entrada, um tubo vai até
///     a geral pela perpendicular a ela, em planta, e a geral é partida ali e ganha um tê; se a geral está em outra
///     altura, um tubo vertical faz a diferença (cotovelo embaixo ou em cima, tê na geral). Se a subgeral já aponta para
///     a geral, ela mesma é esticada. Cada rede numa transação; tudo num grupo: um Ctrl+Z desfaz.
/// </summary>
public static class LigarAGeral
{
    static readonly double MargemPonta = GeradorMalha.Pes(0.15);
    static readonly double SubidaMinima = GeradorMalha.Pes(0.15);
    static readonly double MesmaAltura = GeradorMalha.Pes(0.02);

    /// <summary>As redes com bico que não tocam a geral e cuja entrada fica até 3 m acima ou abaixo dela.</summary>
    public static List<HashSet<ElementId>> RedesSoltas(Document doc, IReadOnlyList<Pipe> geral)
    {
        var naGeral = new HashSet<ElementId>(geral.SelectMany(g => Rede(doc, g)));
        var zGeral = geral.Select(Z).Average();
        var redes = new List<HashSet<ElementId>>();
        var vistos = new HashSet<ElementId>();
        foreach (var bico in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sprinklers).WhereElementIsNotElementType())
        {
            if (vistos.Contains(bico.Id) || naGeral.Contains(bico.Id)) continue;
            var rede = Rede(doc, bico);
            vistos.UnionWith(rede);
            if (Entrada(doc, rede) is { } entrada && Math.Abs(entrada.Ponto.Z - zGeral) < GeradorMalha.Pes(3)) redes.Add(rede);
        }
        return redes;
    }

    public static ResultadoGeral Executar(Document doc, IReadOnlyList<Pipe> geral, IReadOnlyList<HashSet<ElementId>> redes)
    {
        var pedacos = geral.Where(g => g.Location is LocationCurve { Curve: Line l } && Math.Abs(l.Direction.Z) < 0.05).ToList();
        var criados = new List<ElementId>();
        var resultado = new List<RedeLigada>();
        using var grupo = new TransactionGroup(doc, "SPK — Ligar à geral");
        grupo.Start();
        foreach (var rede in redes)
        {
            var bicos = rede.Count(id => doc.GetElement(id)?.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers);
            if (Entrada(doc, rede) is not { } entrada)
            {
                resultado.Add(new RedeLigada(bicos, XYZ.Zero, "a rede não tem ponta aberta para ligar"));
                continue;
            }
            var novos = new List<Pipe>();
            var recusa = pedacos.Count == 0
                ? "a geral precisa de um tubo reto e horizontal"
                : TransacaoSegura.Executar(doc, "SPK — Ligar sala à geral", () => Ligar(doc, entrada, pedacos, novos));
            resultado.Add(new RedeLigada(bicos, entrada.Ponto, recusa));
            if (recusa is null) criados.AddRange(novos.Where(t => t.IsValidObject && !pedacos.Contains(t)).Select(t => t.Id));
        }
        var aviso = resultado.Any(r => r.Recusa is null) ? Tampar(doc, pedacos) : null;
        grupo.Assimilate();
        return new ResultadoGeral(resultado, criados, aviso);
    }

    /// <summary>
    /// Tampão nas pontas abertas da geral, como na obra, menos na entrada do setor: a ponta aberta mais baixa da rede
    /// inteira (a descida da VGA). Se a entrada é ponta da própria geral, esse tubo fica sem tampão.
    /// </summary>
    static string? Tampar(Document doc, List<Pipe> pedacos)
    {
        if (pedacos.FirstOrDefault(t => t.IsValidObject) is not { } algum) return null;
        var entrada = Rede(doc, algum).Select(doc.GetElement).OfType<Pipe>()
            .SelectMany(t => t.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End && !c.IsConnected))
            .OrderBy(c => c.Origin.Z).FirstOrDefault();
        var abertos = pedacos.Where(t => t.IsValidObject && t.ConnectorManager.Connectors.Cast<Connector>()
                .Any(c => c.ConnectorType == ConnectorType.End && !c.IsConnected))
            .Where(t => entrada is null || entrada.Owner.Id != t.Id)
            .ToList();
        if (abertos.Count == 0) return null;
        return TransacaoSegura.Executar(doc, "SPK — Tampão da geral", () =>
        {
            foreach (var tubo in abertos) PlumbingUtils.PlaceCapOnOpenEnds(doc, tubo.Id, ElementId.InvalidElementId);
            return null;
        }) is { } erro
            ? "ponta da geral sem tampão (o tipo de tubo não tem tampão nas preferências de roteamento): " + erro
            : null;
    }

    /// <summary>A ponta por onde a rede recebe água: a descida até o piso (com o cotovelo em cima) ou uma ponta horizontal.</summary>
    internal sealed record EntradaDaRede(XYZ Ponto, Pipe? Descida, FamilyInstance? Cotovelo, Pipe Subgeral, Connector Ponta);

    internal static EntradaDaRede? Entrada(Document doc, HashSet<ElementId> rede)
    {
        var aberta = rede.Select(doc.GetElement).OfType<Pipe>()
            .SelectMany(t => t.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End && !c.IsConnected)
                .Select(c => (Tubo: t, Conector: c)))
            .OrderBy(p => p.Conector.Origin.Z).FirstOrDefault();
        if (aberta.Tubo is null) return null;
        var (tubo, conector) = aberta;
        if (tubo.Location is not LocationCurve { Curve: Line linha }) return null;
        if (Math.Abs(linha.Direction.Z) < 0.95) return new EntradaDaRede(conector.Origin, null, null, tubo, conector);

        // Descida: o outro lado dela chega num cotovelo, que leva à subgeral horizontal.
        var topo = tubo.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.End && c.IsConnected);
        var cotovelo = topo?.AllRefs.Cast<Connector>().Select(r => r.Owner).OfType<FamilyInstance>().FirstOrDefault();
        var conectoresDoCotovelo = cotovelo?.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().ToList();
        if (cotovelo is null || conectoresDoCotovelo is not { Count: 2 }) return null;
        var paraSubgeral = conectoresDoCotovelo.SelectMany(c => c.AllRefs.Cast<Connector>())
            .FirstOrDefault(r => r.Owner is Pipe p && p.Id != tubo.Id);
        if (paraSubgeral?.Owner is not Pipe subgeral) return null;
        return new EntradaDaRede(paraSubgeral.Origin, tubo, cotovelo, subgeral, paraSubgeral);
    }

    static string? Ligar(Document doc, EntradaDaRede entrada, List<Pipe> pedacos, List<Pipe> novos)
    {
        var subgeral = entrada.Subgeral;
        Passo("tirar a descida", () =>
        {
            if (entrada.Cotovelo is not null) doc.Delete(entrada.Cotovelo.Id);
            if (entrada.Descida is not null) doc.Delete(entrada.Descida.Id);
            doc.Regenerate();
        });
        var ponta = subgeral.ConnectorManager.Connectors.Cast<Connector>()
            .Where(c => c.ConnectorType == ConnectorType.End && !c.IsConnected)
            .OrderBy(c => c.Origin.DistanceTo(entrada.Ponto)).FirstOrDefault();
        if (ponta is null) return "a ponta da subgeral não ficou livre";
        var p = ponta.Origin;
        if (subgeral.Location is not LocationCurve { Curve: Line eixoSub }) return "a subgeral precisa ser um tubo reto";
        var outraPonta = eixoSub.GetEndPoint(0).DistanceTo(p) < eixoSub.GetEndPoint(1).DistanceTo(p) ? eixoSub.GetEndPoint(1) : eixoSub.GetEndPoint(0);
        if (Math.Abs(eixoSub.Direction.Z) > 0.05) return "a ponta de entrada da rede não é horizontal";
        var sentido = Horizontal(p - outraPonta).Normalize();

        // Onde a ligação encontra a geral, em planta, longe das pontas e dos tês que já existem: se a subgeral aponta para a
        // geral (até ~8°), no cruzamento da reta dela com a geral, e ela é esticada até lá (um cotovelo entre tubos quase
        // alinhados o Revit recusa); senão, no pé da perpendicular da entrada até a geral.
        Pipe? geral = null;
        XYZ? g = null;
        var esticar = false;
        foreach (var pedaco in pedacos.Where(t => t.IsValidObject).OrderBy(t => Distancia(t, p)))
        {
            var linha = (Line)((LocationCurve)pedaco.Location).Curve;
            var a = linha.GetEndPoint(0);
            var dg = Horizontal(linha.Direction).Normalize();
            var normal = new XYZ(-dg.Y, dg.X, 0);
            var lado = Horizontal(a - p).DotProduct(normal);
            var avanco = sentido.DotProduct(normal);
            var cruza = Math.Abs(avanco) > Math.Cos(8 * Math.PI / 180) && lado / avanco > 0;
            var alvo = cruza ? p + sentido * (lado / avanco) : p + normal * lado;
            var t = Horizontal(alvo - a).DotProduct(dg);
            if (t < MargemPonta || t > linha.Length - MargemPonta) continue;
            geral = pedaco;
            g = a + linha.Direction * t;
            esticar = cruza;
            break;
        }
        if (geral is null || g is null) return "a geral não passa em frente a esta rede (o encontro cai fora dela ou perto de um tê)";

        var rumo = Horizontal(g - p);
        if (rumo.GetLength() < MargemPonta) return "a entrada da rede fica em cima da geral";
        rumo = rumo.Normalize();
        if (rumo.DotProduct(sentido) < -0.5)
            return "a geral fica do lado de dentro da rede (a subgeral começa na parede oposta à geral): " +
                   "trace a geral do outro lado ou gere a rede com a entrada voltada para ela";

        var nivel = geral.ReferenceLevel;
        var sistema = subgeral.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId() ?? ElementId.InvalidElementId;
        if (sistema == ElementId.InvalidElementId) return "a subgeral não tem tipo de sistema";
        var dz = g.Z - p.Z;
        if (Math.Abs(dz) >= MesmaAltura && Math.Abs(dz) < SubidaMinima)
            return "a geral e a subgeral ficam a menos de 15 cm de altura uma da outra: não cabem o tê e o cotovelo da subida";
        var mesmaAltura = Math.Abs(dz) < MesmaAltura;
        var canto = new XYZ(g.X, g.Y, p.Z);

        // Trecho horizontal até em frente à geral: a própria subgeral esticada, se aponta para lá; senão um tubo novo.
        Pipe horizontal = subgeral;
        if (esticar)
        {
            // A linha nova mantém a ordem das pontas do tubo (como em Recuar): invertida, o Revit não estica o tubo.
            var ate = mesmaAltura ? g : canto;
            var pontaEhInicio = eixoSub.GetEndPoint(0).DistanceTo(p) < eixoSub.GetEndPoint(1).DistanceTo(p);
            Passo("esticar a subgeral", () =>
            {
                ((LocationCurve)subgeral.Location).Curve = pontaEhInicio ? Line.CreateBound(ate, outraPonta) : Line.CreateBound(outraPonta, ate);
                doc.Regenerate();
                if (!subgeral.ConnectorManager.Connectors.Cast<Connector>().Any(c => c.Origin.DistanceTo(ate) < GeradorMalha.Pes(0.02)))
                    throw new InvalidOperationException("o Revit não esticou o tubo até a geral");
            });
        }
        else
        {
            horizontal = GeradorMalha.CriarTubo(doc, sistema, subgeral.PipeType.Id, nivel.Id, p, mesmaAltura ? g : canto, subgeral.Diameter, marcar: false);
            novos.Add(horizontal);
        }
        var subida = mesmaAltura ? null : GeradorMalha.CriarTubo(doc, sistema, subgeral.PipeType.Id, nivel.Id, canto, g, subgeral.Diameter, marcar: false);
        if (subida is not null) novos.Add(subida);

        Pipe? outroPedaco = null;
        Passo("partir a geral", () =>
        {
            outroPedaco = doc.GetElement(PlumbingUtils.BreakCurve(doc, geral.Id, g)) as Pipe;
            doc.Regenerate();
        });
        if (outroPedaco is not null)
        {
            pedacos.Add(outroPedaco);
            novos.Add(outroPedaco);
        }

        string? motivo = null;
        if (!esticar) Passo("cotovelo na entrada", () => motivo = TracarRamal.Juntar(doc, p, nivel, [subgeral, horizontal]));
        if (motivo is null && subida is not null) Passo("cotovelo da subida", () => motivo = TracarRamal.Juntar(doc, canto, nivel, [horizontal, subida]));
        if (motivo is null) Passo("tê na geral", () => motivo = TracarRamal.Juntar(doc, g, nivel, [geral, outroPedaco, subida ?? horizontal]));
        return motivo;
    }

    /// <summary>Roda um passo da ligação; se o Revit recusar, a mensagem diz qual passo foi.</summary>
    static void Passo(string nome, Action acao)
    {
        try
        {
            acao();
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            throw new InvalidOperationException($"{nome}: {e.Message}", e);
        }
    }

    /// <summary>Tudo o que está ligado ao elemento pelos conectores (sem o sistema).</summary>
    public static HashSet<ElementId> Rede(Document doc, Element inicio)
    {
        var vistos = new HashSet<ElementId> { inicio.Id };
        var fila = new Queue<Element>();
        fila.Enqueue(inicio);
        while (fila.Count > 0)
        {
            foreach (var c in Conectores(fila.Dequeue()))
            foreach (Connector outro in c.AllRefs)
            {
                if (outro.ConnectorType == ConnectorType.Logical || outro.Owner is null or MEPSystem) continue;
                if (vistos.Add(outro.Owner.Id)) fila.Enqueue(outro.Owner);
            }
        }
        return vistos;
    }

    static IEnumerable<Connector> Conectores(Element e) => e switch
    {
        MEPCurve m => m.ConnectorManager.Connectors.Cast<Connector>(),
        FamilyInstance { MEPModel.ConnectorManager: { } cm } => cm.Connectors.Cast<Connector>(),
        _ => [],
    };

    static double Z(Pipe t) => t.Location is LocationCurve lc ? (lc.Curve.GetEndPoint(0).Z + lc.Curve.GetEndPoint(1).Z) / 2 : 0;

    static double Distancia(Pipe t, XYZ p) =>
        t.Location is LocationCurve { Curve: Line l } ? Horizontal(p - l.Project(new XYZ(p.X, p.Y, l.GetEndPoint(0).Z)).XYZPoint).GetLength() : double.MaxValue;

    static XYZ Horizontal(XYZ v) => new(v.X, v.Y, 0);
}
