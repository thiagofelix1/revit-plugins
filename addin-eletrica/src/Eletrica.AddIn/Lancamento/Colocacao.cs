using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Lancamento;

/// <summary>
///     Colocar uma família no modelo e deixá-la com os dados do plugin: na altura pedida, com os parâmetros AE (potência,
///     tipo, posição, altura, ambiente). Resolve a hospedagem: família livre vai no nível; família de face vai na face da
///     parede ou do teto, inclusive quando a parede está num vínculo.
/// </summary>
static class Colocacao
{
    public sealed record Pedido(FamilySymbol Simbolo, PapelFamilia Papel, PosicaoDoPonto Posicao, double Altura, double PotenciaVA, TipoCarga? Tipo);

    /// <summary>Família que precisa de face ou hospedeiro (não aceita ser colocada só pelo ponto).</summary>
    public static bool Hospedada(FamilySymbol s) =>
        s.Family.FamilyPlacementType is FamilyPlacementType.OneLevelBasedHosted or FamilyPlacementType.WorkPlaneBased;

    public static bool DeFace(FamilySymbol s) => s.Family.FamilyPlacementType == FamilyPlacementType.WorkPlaneBased;

    /// <summary>
    ///     Coloca na parede: o ponto já está afastado da face, na altura certa; <paramref name="paraDentro" /> é a normal da parede
    ///     apontando para o ambiente. Família livre: no nível, virada para o ambiente. Família de face: na face da parede
    ///     (<paramref name="face" /> com a referência já no espaço do projeto), com o eixo X ao longo da parede.
    /// </summary>
    public static FamilyInstance NaParede(Document doc, FamilySymbol simbolo, XYZ ponto, XYZ paraDentro, Level nivel, Reference? face)
    {
        Ativar(simbolo);
        if (DeFace(simbolo))
        {
            if (face is null) throw new InvalidOperationException($"\"{PadraoEletrica.Nome(simbolo)}\" é família de face e a parede não deu uma face para hospedar.");
            var aoLongo = paraDentro.CrossProduct(XYZ.BasisZ);
            if (aoLongo.GetLength() < 1e-6) aoLongo = XYZ.BasisX;
            return doc.Create.NewFamilyInstance(face, ponto, aoLongo.Normalize(), simbolo);
        }
        if (simbolo.Family.FamilyPlacementType == FamilyPlacementType.OneLevelBasedHosted)
            throw new InvalidOperationException($"\"{PadraoEletrica.Nome(simbolo)}\" é hospedada em parede do próprio arquivo; com a arquitetura em vínculo use uma família de face ou livre.");
        var fi = doc.Create.NewFamilyInstance(ponto, simbolo, nivel, StructuralType.NonStructural);
        Virar(doc, fi, ponto, paraDentro);
        return fi;
    }

    /// <summary>Coloca no teto (ou solto, na cota do pé-direito, quando a família é livre ou não há teto).</summary>
    public static FamilyInstance NoTeto(Document doc, FamilySymbol simbolo, XYZ ponto, Level nivel, Reference? faceDoTeto)
    {
        Ativar(simbolo);
        if (DeFace(simbolo))
        {
            if (faceDoTeto is null) throw new InvalidOperationException($"\"{PadraoEletrica.Nome(simbolo)}\" é família de face e não há teto ou laje acima do ponto para hospedar.");
            return doc.Create.NewFamilyInstance(faceDoTeto, ponto, XYZ.BasisX, simbolo);
        }
        if (simbolo.Family.FamilyPlacementType == FamilyPlacementType.OneLevelBasedHosted)
            throw new InvalidOperationException($"\"{PadraoEletrica.Nome(simbolo)}\" é hospedada em elemento do próprio arquivo; use uma família de face ou livre.");
        var fi = doc.Create.NewFamilyInstance(new XYZ(ponto.X, ponto.Y, nivel.ProjectElevation), simbolo, nivel, StructuralType.NonStructural);
        doc.Regenerate();
        var atual = (fi.Location as LocationPoint)?.Point.Z ?? nivel.ProjectElevation;
        var dz = ponto.Z - atual;
        if (Math.Abs(dz) > 1e-6) Subir(doc, fi, dz);
        return fi;
    }

    static void Virar(Document doc, FamilyInstance fi, XYZ ponto, XYZ paraDentro)
    {
        var frente = fi.FacingOrientation;
        var alvo = new XYZ(paraDentro.X, paraDentro.Y, 0);
        if (alvo.GetLength() < 1e-6 || frente.GetLength() < 1e-6) return;
        alvo = alvo.Normalize();
        var angulo = Math.Atan2(frente.X * alvo.Y - frente.Y * alvo.X, frente.X * alvo.X + frente.Y * alvo.Y);
        if (Math.Abs(angulo) < 1e-4) return;
        ElementTransformUtils.RotateElement(doc, fi.Id, Line.CreateBound(ponto, ponto + XYZ.BasisZ), angulo);
    }

    public static void Ativar(FamilySymbol simbolo)
    {
        if (!simbolo.IsActive) simbolo.Activate();
    }

    /// <summary>
    ///     Leva o ponto à altura pedida sobre o nível: pelo parâmetro de elevação quando a família tem e aceita, senão
    ///     movendo o elemento na vertical (vale para famílias de face em parede). Devolve a altura final em m.
    /// </summary>
    public static double AjustarAltura(Document doc, FamilyInstance fi, double alturaM, Level nivel)
    {
        if ((fi.Location as LocationPoint)?.Point is not { } p) return alturaM;
        var alvoZ = nivel.ProjectElevation + Unidades.DeMetros(alturaM);
        var dz = alvoZ - p.Z;
        if (Math.Abs(dz) < Unidades.DeMetros(0.005)) return alturaM;
        var parametro = fi.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);
        if (parametro is { IsReadOnly: false, StorageType: StorageType.Double } && fi.Host is null)
        {
            parametro.Set(parametro.AsDouble() + dz);
            doc.Regenerate();
            if ((fi.Location as LocationPoint)?.Point is { } depois && Math.Abs(depois.Z - alvoZ) < Unidades.DeMetros(0.01)) return alturaM;
        }
        Subir(doc, fi, alvoZ - ((fi.Location as LocationPoint)?.Point.Z ?? p.Z));
        return alturaM;
    }

    static void Subir(Document doc, FamilyInstance fi, double dz)
    {
        try { ElementTransformUtils.MoveElement(doc, fi.Id, new XYZ(0, 0, dz)); }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { }
    }

    /// <summary>Altura do ponto sobre o nível, em m.</summary>
    public static double AlturaDe(Document doc, FamilyInstance fi, Level? nivel)
    {
        if ((fi.Location as LocationPoint)?.Point is not { } p) return 0;
        nivel ??= NivelDe(doc, fi, p);
        return nivel is null ? 0 : Unidades.Metros(p.Z - nivel.ProjectElevation);
    }

    /// <summary>O nível do elemento, ou o mais alto que fica abaixo do ponto.</summary>
    public static Level? NivelDe(Document doc, FamilyInstance? fi, XYZ ponto)
    {
        if (fi is not null && doc.GetElement(fi.LevelId) is Level n) return n;
        if (fi?.Host is Level h) return h;
        return NivelAbaixo(doc, ponto.Z);
    }

    public static Level? NivelAbaixo(Document doc, double z)
    {
        var niveis = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation).ToList();
        return niveis.LastOrDefault(l => l.ProjectElevation <= z + Unidades.DeMetros(0.3)) ?? niveis.FirstOrDefault();
    }

    /// <summary>Grava os parâmetros AE do ponto recém-colocado. Precisa de transação aberta.</summary>
    public static void Marcar(FamilyInstance fi, Pedido pedido, AmbienteLido? ambiente, double altura)
    {
        if (pedido.PotenciaVA > 0) ParametrosEletrica.Gravar(fi, ParametrosEletrica.Potencia, pedido.PotenciaVA);
        if (pedido.Tipo is { } tipo) ParametrosEletrica.Gravar(fi, ParametrosEletrica.TipoDoPonto, tipo.ToString());
        ParametrosEletrica.Gravar(fi, ParametrosEletrica.Posicao, PadraoEletrica.NomeDaPosicao(pedido.Posicao));
        ParametrosEletrica.Gravar(fi, ParametrosEletrica.Altura, altura);
        if (ambiente is not null) ParametrosEletrica.Gravar(fi, ParametrosEletrica.Ambiente, ambiente.Nome);
        // A carga aparente da família, quando é de instância, recebe a potência para o Revit somar no circuito.
        if (pedido.PotenciaVA > 0 && fi.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD) is { IsReadOnly: false, StorageType: StorageType.Double } carga)
            carga.Set(UnitUtils.ConvertToInternalUnits(pedido.PotenciaVA, UnitTypeId.VoltAmperes));
    }

    /// <summary>Face de uma parede (do arquivo ou de um vínculo) que contém o ponto e olha para <paramref name="paraDentro" />, como referência no espaço do projeto.</summary>
    public static Reference? FaceDaParede(Document doc, Element parede, Transform transformacao, RevitLinkInstance? vinculo, XYZ pontoNoProjeto, XYZ paraDentro)
    {
        var local = transformacao.Inverse.OfPoint(pontoNoProjeto);
        var normal = transformacao.Inverse.OfVector(paraDentro).Normalize();
        var opcoes = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine, IncludeNonVisibleObjects = false };
        Reference? melhor = null;
        var menor = double.MaxValue;
        foreach (var solido in Solidos(parede.get_Geometry(opcoes)))
        foreach (Face face in solido.Faces)
        {
            if (face is not PlanarFace pf || face.Reference is null) continue;
            if (pf.FaceNormal.DotProduct(normal) < 0.9) continue; // a face que olha para o ambiente tem a normal para dentro
            var projecao = face.Project(local);
            if (projecao is null) continue;
            if (projecao.Distance < menor)
            {
                menor = projecao.Distance;
                melhor = face.Reference;
            }
        }
        if (melhor is null || menor > Unidades.DeMetros(0.25)) return null;
        return vinculo is null ? melhor : melhor.CreateLinkReference(vinculo);
    }

    /// <summary>Primeira face de teto, forro ou laje acima do ponto (no arquivo ou nos vínculos), com a cota, ou nulo.</summary>
    public static (Reference Face, double Z)? FaceDoTeto(Document doc, View3D vista, XYZ ponto, double alturaMaxima)
    {
        var filtro = new ElementMulticategoryFilter([BuiltInCategory.OST_Ceilings, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Roofs, BuiltInCategory.OST_StructuralFraming]);
        var raio = new ReferenceIntersector(filtro, FindReferenceTarget.Face, vista) { FindReferencesInRevitLinks = true };
        var inicio = new XYZ(ponto.X, ponto.Y, ponto.Z + Unidades.DeMetros(0.3));
        ReferenceWithContext? achado = null;
        foreach (var r in raio.Find(inicio, XYZ.BasisZ).Where(r => r.Proximity > 1e-6).OrderBy(r => r.Proximity))
        {
            if (r.Proximity > Unidades.DeMetros(alturaMaxima)) break;
            achado = r;
            break;
        }
        if (achado is null) return null;
        return (achado.GetReference(), inicio.Z + achado.Proximity);
    }

    static IEnumerable<Solid> Solidos(GeometryElement? g)
    {
        if (g is null) yield break;
        foreach (var o in g)
        {
            if (o is Solid { Volume: > 0 } s) yield return s;
            else if (o is GeometryInstance gi)
                foreach (var s2 in Solidos(gi.GetInstanceGeometry())) yield return s2;
        }
    }

    /// <summary>Uma vista 3D para os raios do ReferenceIntersector: a primeira do projeto, ou uma criada na transação (apagar depois).</summary>
    public static View3D Vista3d(Document doc, out bool criada)
    {
        var existente = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault(v => !v.IsTemplate && v.CanBePrinted);
        if (existente is not null)
        {
            criada = false;
            return existente;
        }
        var tipo = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(t => t.ViewFamily == ViewFamily.ThreeDimensional);
        criada = true;
        return View3D.CreateIsometric(doc, tipo.Id);
    }

    /// <summary>Contorno externo do ambiente nas coordenadas do projeto, com a parede de cada segmento.</summary>
    public static List<SegmentoDeParede> Contorno(AmbienteLido a)
    {
        var opcoes = new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
        var lacos = a.Room.GetBoundarySegments(opcoes);
        if (lacos is null || lacos.Count == 0) return [];
        // O laço de maior área é o contorno externo; os outros são pilares e vazios.
        IList<BoundarySegment>? externo = null;
        double maiorArea = 0;
        foreach (var laco in lacos)
        {
            var pontos = laco.SelectMany(s => s.GetCurve().Tessellate().Take(s.GetCurve().Tessellate().Count - 1)).Select(p => a.Transformacao.OfPoint(p)).ToList();
            var area = Math.Abs(AreaAssinada(pontos));
            if (area > maiorArea)
            {
                maiorArea = area;
                externo = laco;
            }
        }
        if (externo is null) return [];
        var vertices = externo.SelectMany(s => s.GetCurve().Tessellate().Take(s.GetCurve().Tessellate().Count - 1)).Select(p => a.Transformacao.OfPoint(p)).ToList();
        var antiHorario = AreaAssinada(vertices) > 0;
        var lista = new List<SegmentoDeParede>();
        foreach (var s in externo)
        {
            var curva = s.GetCurve();
            var p0 = a.Transformacao.OfPoint(curva.GetEndPoint(0));
            var p1 = a.Transformacao.OfPoint(curva.GetEndPoint(1));
            var d = p1 - p0;
            if (d.GetLength() < 1e-6) continue;
            var dir = new XYZ(d.X, d.Y, 0).Normalize();
            // Para o laço anti-horário, o interior fica à esquerda da direção do segmento.
            var paraDentro = antiHorario ? new XYZ(-dir.Y, dir.X, 0) : new XYZ(dir.Y, -dir.X, 0);
            var elemento = s.ElementId != ElementId.InvalidElementId ? a.Doc.GetElement(s.ElementId) : null;
            lista.Add(new SegmentoDeParede(p0, p1, dir, paraDentro, Unidades.Metros(d.GetLength()), elemento));
        }
        return lista;
    }

    public sealed record SegmentoDeParede(XYZ Inicio, XYZ Fim, XYZ Direcao, XYZ ParaDentro, double Comprimento, Element? Parede)
    {
        public bool EhParede => Parede is Wall;
        public XYZ Em(double distancia) => Inicio + Direcao * Unidades.DeMetros(distancia);
        public XYZ Meio => Em(Comprimento / 2);
    }

    static double AreaAssinada(IReadOnlyList<XYZ> p)
    {
        double a = 0;
        for (var i = 0; i < p.Count; i++)
        {
            var j = (i + 1) % p.Count;
            a += p[i].X * p[j].Y - p[j].X * p[i].Y;
        }
        return a / 2;
    }

    /// <summary>Vínculo que contém o documento do ambiente, se não for o próprio arquivo.</summary>
    public static RevitLinkInstance? VinculoDe(Document doc, AmbienteLido a)
    {
        if (a.Doc == doc) return null;
        return new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
            .FirstOrDefault(v => v.GetLinkDocument() is { } d && d.Equals(a.Doc));
    }

    /// <summary>Portas do ambiente (do arquivo dele): pela sala de origem/destino na última fase, ou pela proximidade ao contorno.</summary>
    public static List<(XYZ Local, double Largura)> Portas(AmbienteLido a, List<SegmentoDeParede> contorno)
    {
        var portas = new List<(XYZ, double)>();
        var fase = a.Doc.Phases.Cast<Phase>().LastOrDefault();
        foreach (var porta in new FilteredElementCollector(a.Doc).OfCategory(BuiltInCategory.OST_Doors).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
        {
            if ((porta.Location as LocationPoint)?.Point is not { } p) continue;
            var local = a.Transformacao.OfPoint(p);
            var pertence = false;
            if (fase is not null)
            {
                try { pertence = porta.get_FromRoom(fase)?.Id == a.Room.Id || porta.get_ToRoom(fase)?.Id == a.Room.Id; }
                catch (Autodesk.Revit.Exceptions.ApplicationException) { pertence = false; }
            }
            if (!pertence && contorno.Count > 0)
                pertence = contorno.Any(s => DistanciaAoSegmento(local, s) < 0.35) && Math.Abs(local.Z - contorno[0].Inicio.Z) < Unidades.DeMetros(1.5);
            if (!pertence) continue;
            var largura = porta.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble() ?? porta.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble() ?? Unidades.DeMetros(0.8);
            portas.Add((local, Unidades.Metros(largura)));
        }
        return portas;
    }

    /// <summary>Distância em planta do ponto ao segmento, em m.</summary>
    public static double DistanciaAoSegmento(XYZ p, SegmentoDeParede s)
    {
        var a = new XYZ(s.Inicio.X, s.Inicio.Y, 0);
        var b = new XYZ(s.Fim.X, s.Fim.Y, 0);
        var q = new XYZ(p.X, p.Y, 0);
        var ab = b - a;
        var t = ab.GetLength() < 1e-9 ? 0 : Math.Max(0, Math.Min(1, (q - a).DotProduct(ab) / ab.DotProduct(ab)));
        return Unidades.Metros((a + ab * t).DistanceTo(q));
    }

    /// <summary>Posição ao longo do segmento (em m desde o início) do ponto projetado.</summary>
    public static double ProjecaoNoSegmento(XYZ p, SegmentoDeParede s)
    {
        var a = new XYZ(s.Inicio.X, s.Inicio.Y, 0);
        var q = new XYZ(p.X, p.Y, 0);
        return Unidades.Metros((q - a).DotProduct(s.Direcao));
    }
}
