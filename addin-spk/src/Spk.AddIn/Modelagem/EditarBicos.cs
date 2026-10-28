using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

namespace Spk.AddIn.Modelagem;

/// <summary>O que uma edição fez: o bico novo (se houve), avisos e por que não foi feita.</summary>
public sealed record EdicaoDeBico(string? Recusa, ElementId? Bico, IReadOnlyList<string> Avisos)
{
    public string Texto(string feito) =>
        Recusa is not null ? "Não feito: " + Recusa : string.Join("\n", new[] { feito }.Concat(Avisos.Select(a => "• " + a)));
}

/// <summary>
///     Editar bicos sem quebrar a rede, para o engenheiro ajustar o que gerou ou desenhou:
///     <list type="bullet">
///         <item>Apagar: do bico, segue pelos tubos e conexões até o primeiro encontro com o resto da rede (tê, cruzeta). Tudo
///         antes dele só servia ao bico (descida, braço, a ponta do ramal se era o último) e sai, com a peça do encontro; o
///         encontro é refeito com as pontas que sobraram (2 alinhadas: um tubo só; 2 em ângulo: cotovelo; 3: tê; 4: cruzeta).</item>
///         <item>Inserir: um bico no ponto do ramal, com o tipo, o nível e a altura do bico mais perto da mesma rede, ligado
///         pelo Ligar bicos (tê e descida).</item>
///         <item>Mover: apaga e insere no ponto novo, ligado ao ramal mais perto da mesma rede (com braço, se ficar de lado).</item>
///     </list>
///     Cada edição num grupo: se uma parte falha, tudo volta; um Ctrl+Z desfaz.
/// </summary>
public static class EditarBicos
{
    static readonly double DistanciaMinima = GeradorMalha.Pes(1.8);

    /// <summary>Tipo, nível, altura e marca de um bico, para fazer outro igual.</summary>
    sealed record Molde(FamilySymbol Simbolo, Level Nivel, double Z, string? Marca);

    public static EdicaoDeBico Apagar(Document doc, FamilyInstance bico)
    {
        using var grupo = new TransactionGroup(doc, "SPK — Apagar bico");
        grupo.Start();
        var motivo = TransacaoSegura.Executar(doc, "SPK — Apagar bico", () => ApagarERefazer(doc, bico, out _));
        if (motivo is not null)
        {
            grupo.RollBack();
            return new EdicaoDeBico(motivo, null, []);
        }
        grupo.Assimilate();
        return new EdicaoDeBico(null, null, []);
    }

    public static EdicaoDeBico Inserir(Document doc, Pipe ramal, XYZ ponto)
    {
        var rede = LigarAGeral.Rede(doc, ramal);
        if (MoldeMaisPerto(doc, rede, ponto) is not { } molde)
            return new EdicaoDeBico("a rede desse ramal não tem bico para servir de modelo (tipo e altura)", null, []);
        return InserirNa(doc, molde, ponto, Ramais(doc, rede, ramal), "SPK — Inserir bico");
    }

    public static EdicaoDeBico Mover(Document doc, FamilyInstance bico, XYZ ponto)
    {
        if (MoldeDe(doc, bico) is not { } molde) return new EdicaoDeBico("o bico não tem nível ou tipo", null, []);
        using var grupo = new TransactionGroup(doc, "SPK — Mover bico");
        grupo.Start();
        ElementId? sobrou = null;
        var motivo = TransacaoSegura.Executar(doc, "SPK — Tirar o bico", () => ApagarERefazer(doc, bico, out sobrou));
        if (motivo is not null || sobrou is null || doc.GetElement(sobrou) is not { } resto)
        {
            grupo.RollBack();
            return new EdicaoDeBico(motivo ?? "o bico não está ligado a nenhuma rede", null, []);
        }
        var rede = LigarAGeral.Rede(doc, resto);
        var feito = InserirNa(doc, molde, ponto, Ramais(doc, rede, null), "SPK — Pôr o bico no lugar novo");
        if (feito.Recusa is not null)
        {
            grupo.RollBack();
            return feito;
        }
        grupo.Assimilate();
        return feito;
    }

    static EdicaoDeBico InserirNa(Document doc, Molde molde, XYZ ponto, List<Pipe> ramais, string nome)
    {
        using var grupo = new TransactionGroup(doc, nome);
        grupo.Start();
        FamilyInstance? novo = null;
        var motivo = TransacaoSegura.Executar(doc, "SPK — Bico novo", () =>
        {
            if (!molde.Simbolo.IsActive) molde.Simbolo.Activate();
            novo = doc.Create.NewFamilyInstance(new XYZ(ponto.X, ponto.Y, molde.Nivel.Elevation), molde.Simbolo, molde.Nivel, StructuralType.NonStructural);
            if (molde.Marca is not null) novo.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(molde.Marca);
            doc.Regenerate();
            GeradorMalha.AjustarAltura(novo, molde.Z, molde.Nivel.Elevation);
            return null;
        });
        if (motivo is not null || novo is null)
        {
            grupo.RollBack();
            return new EdicaoDeBico(motivo ?? "o bico não foi criado", null, []);
        }
        var ligacao = LigarBicos.Executar(doc, [novo], ramais);
        if (ligacao.Ligados == 0)
        {
            grupo.RollBack();
            return new EdicaoDeBico(ligacao.Recusados.FirstOrDefault() ?? "o bico não foi ligado ao ramal", null, []);
        }
        grupo.Assimilate();
        return new EdicaoDeBico(null, novo.Id, Espacamento(doc, novo));
    }

    /// <summary>
    /// Apaga o bico e o caminho só dele até o primeiro encontro com o resto da rede, e refaz o encontro. Devolve um
    /// elemento que ficou na rede (para achar os ramais dela depois), ou nulo se o bico estava solto.
    /// </summary>
    static string? ApagarERefazer(Document doc, FamilyInstance bico, out ElementId? sobrou)
    {
        sobrou = null;
        var apagar = new List<ElementId> { bico.Id };
        Element atual = bico;
        var vindo = Conectores(bico).FirstOrDefault(c => c.IsConnected);
        FamilyInstance? encontro = null;
        while (vindo is not null)
        {
            var proximo = Ligado(vindo, atual);
            if (proximo is null) break;
            var deste = Conectores(proximo.Owner).ToList();
            if (proximo.Owner is FamilyInstance peca && deste.Count >= 3)
            {
                encontro = peca;
                break;
            }
            // Segue pelo outro lado do tubo ou da peça de duas pontas (cotovelo, luva); ponta solta: acabou o caminho.
            apagar.Add(proximo.Owner.Id);
            atual = proximo.Owner;
            vindo = deste.FirstOrDefault(c => c.IsConnected && c.Origin.DistanceTo(proximo.Origin) > GeradorMalha.Pes(0.001));
        }

        if (encontro is null)
        {
            doc.Delete(apagar);
            return null;
        }

        // As pontas que o encontro ligava fora do caminho do bico: depois de apagar, elas ficam livres e são religadas.
        var caminho = new HashSet<ElementId>(apagar);
        var pontas = Conectores(encontro).Select(c => Ligado(c, encontro)).OfType<Connector>()
            .Where(c => !caminho.Contains(c.Owner.Id)).Select(c => (Dono: c.Owner.Id, Origem: c.Origin)).ToList();
        var centro = (encontro.Location as LocationPoint)?.Point ?? pontas.Select(p => p.Origem).Aggregate((a, b) => (a + b) / 2);
        apagar.Add(encontro.Id);
        doc.Delete(apagar);
        doc.Regenerate();
        sobrou = pontas.Select(p => p.Dono).FirstOrDefault(id => doc.GetElement(id) is not null);

        var livres = pontas.Select(p => (Elemento: doc.GetElement(p.Dono), p.Origem))
            .Select(p => p.Elemento is null ? null : Conectores(p.Elemento).Where(c => !c.IsConnected).OrderBy(c => c.Origin.DistanceTo(p.Origem)).FirstOrDefault())
            .OfType<Connector>().ToList();
        return Refazer(doc, centro, livres);
    }

    /// <summary>Religa as pontas livres no lugar do encontro apagado: tampão, um tubo só, cotovelo, tê ou cruzeta.</summary>
    static string? Refazer(Document doc, XYZ centro, List<Connector> livres)
    {
        switch (livres.Count)
        {
            case 0:
                return null;
            case 1:
                if (livres[0].Owner is Pipe sozinho) PlumbingUtils.PlaceCapOnOpenEnds(doc, sozinho.Id, ElementId.InvalidElementId);
                return null;
            case 2:
                var (a, b) = (livres[0], livres[1]);
                var alinhados = Math.Abs(a.CoordinateSystem.BasisZ.DotProduct(b.CoordinateSystem.BasisZ)) > 0.999;
                if (alinhados && a.Owner is Pipe ta && b.Owner is Pipe tb) return Emendar(doc, ta, a, tb, b);
                doc.Create.NewElbowFitting(a, b);
                return null;
            case 3:
                if (!GeradorMalha.TentarInserirTe(doc, livres))
                    GeradorMalha.InserirTePorFamilia(doc, centro, NivelDe(doc, livres), GeradorMalha.SimboloDeTe(doc), livres);
                return null;
            case 4:
                var par = livres.Skip(1).OrderBy(c => c.CoordinateSystem.BasisZ.DotProduct(livres[0].CoordinateSystem.BasisZ)).First();
                var outros = livres.Where(c => !ReferenceEquals(c, livres[0]) && !ReferenceEquals(c, par)).ToList();
                doc.Create.NewCrossFitting(livres[0], par, outros[0], outros[1]);
                return null;
            default:
                return $"o encontro ficou com {livres.Count} pontas livres";
        }
    }

    /// <summary>
    /// Dois tubos alinhados viram um só: o primeiro estica até a outra ponta do segundo (mantendo a ordem das pontas), o
    /// segundo sai, e o que estava ligado na outra ponta dele passa a ligar no primeiro.
    /// </summary>
    static string? Emendar(Document doc, Pipe a, Connector pontaA, Pipe b, Connector pontaB)
    {
        if (a.Location is not LocationCurve { Curve: Line la } || b.Location is not LocationCurve { Curve: Line lb }) return "tubo que não é reto";
        var longeB = lb.GetEndPoint(0).DistanceTo(pontaB.Origin) < lb.GetEndPoint(1).DistanceTo(pontaB.Origin) ? lb.GetEndPoint(1) : lb.GetEndPoint(0);
        var conectorLonge = Conectores(b).OrderBy(c => c.Origin.DistanceTo(longeB)).First();
        var alemDeB = conectorLonge.IsConnected ? Ligado(conectorLonge, b) : null;
        var (alemDono, alemOrigem) = (alemDeB?.Owner.Id, alemDeB?.Origin);
        var aEhInicio = la.GetEndPoint(0).DistanceTo(pontaA.Origin) < la.GetEndPoint(1).DistanceTo(pontaA.Origin);
        var fixo = aEhInicio ? la.GetEndPoint(1) : la.GetEndPoint(0);
        doc.Delete(b.Id);
        ((LocationCurve)a.Location).Curve = aEhInicio ? Line.CreateBound(longeB, fixo) : Line.CreateBound(fixo, longeB);
        doc.Regenerate();
        if (alemDono is null || doc.GetElement(alemDono) is not { } alem) return null;
        var naPonta = Conectores(a).OrderBy(c => c.Origin.DistanceTo(longeB)).First();
        var destino = Conectores(alem).Where(c => !c.IsConnected).OrderBy(c => c.Origin.DistanceTo(alemOrigem!)).FirstOrDefault();
        if (destino is null) return "a peça depois do tubo emendado não ficou livre para religar";
        naPonta.ConnectTo(destino);
        return null;
    }

    static Level NivelDe(Document doc, List<Connector> conectores) =>
        conectores.Select(c => c.Owner).OfType<MEPCurve>().Select(t => t.ReferenceLevel).FirstOrDefault(n => n is not null)
        ?? GeradorMalha.Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível.");

    /// <summary>O conector de outro elemento ligado a este (sem o sistema).</summary>
    static Connector? Ligado(Connector conector, Element dono) =>
        conector.AllRefs.Cast<Connector>().FirstOrDefault(r => r.ConnectorType != ConnectorType.Logical && r.Owner is not null and not MEPSystem && r.Owner.Id != dono.Id);

    static IEnumerable<Connector> Conectores(Element e) => e switch
    {
        MEPCurve m => m.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End),
        FamilyInstance { MEPModel.ConnectorManager: { } cm } => cm.Connectors.Cast<Connector>().Where(c => (c.ConnectorType & ConnectorType.End) == ConnectorType.End),
        _ => [],
    };

    /// <summary>Os tubos horizontais da rede (o ramal pedido primeiro): onde o bico novo pode se ligar.</summary>
    static List<Pipe> Ramais(Document doc, HashSet<ElementId> rede, Pipe? primeiro) =>
        rede.Select(doc.GetElement).OfType<Pipe>()
            .Where(t => t.Location is LocationCurve { Curve: Line l } && Math.Abs(l.Direction.Z) < 0.05)
            .OrderBy(t => primeiro is not null && t.Id == primeiro.Id ? 0 : 1).ToList();

    static Molde? MoldeMaisPerto(Document doc, HashSet<ElementId> rede, XYZ ponto) =>
        rede.Select(doc.GetElement).OfType<FamilyInstance>()
            .Where(b => b.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers && b.Location is LocationPoint)
            .OrderBy(b => Horizontal(((LocationPoint)b.Location).Point - ponto).GetLength())
            .Select(b => MoldeDe(doc, b)).FirstOrDefault(m => m is not null);

    static Molde? MoldeDe(Document doc, FamilyInstance bico)
    {
        if (bico.Location is not LocationPoint lp || doc.GetElement(bico.LevelId) is not Level nivel) return null;
        var marca = bico.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
        return new Molde(bico.Symbol, nivel, lp.Point.Z, marca == DadosEsboco.Marca ? marca : null);
    }

    /// <summary>Aviso se o bico ficou a menos de 1,8 m de outro (8.12.4): o engenheiro decide.</summary>
    static List<string> Espacamento(Document doc, FamilyInstance novo)
    {
        if (novo.Location is not LocationPoint lp) return [];
        var perto = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sprinklers).WhereElementIsNotElementType()
            .Where(b => b.Id != novo.Id && b.Location is LocationPoint)
            .Select(b => Horizontal(((LocationPoint)b.Location).Point - lp.Point).GetLength())
            .DefaultIfEmpty(double.MaxValue).Min();
        return perto < DistanciaMinima
            ? [$"o bico ficou a {UnitUtils.ConvertFromInternalUnits(perto, UnitTypeId.Meters):0.00} m de outro (mínimo 1,8 m, 8.12.4)"]
            : [];
    }

    static XYZ Horizontal(XYZ v) => new(v.X, v.Y, 0);
}
