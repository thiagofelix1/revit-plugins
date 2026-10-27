using Autodesk.Revit.DB.Architecture;
using Spk.Motor.Modelo;

namespace Spk.AddIn.Arquitetura;

/// <summary>
/// Lê a arquitetura e a estrutura do arquivo e dos vínculos Revit carregados (um IFC vinculado também é um vínculo
/// Revit): ambientes com contorno, piso e teto, e vigas com largura, fundo e topo. Tudo nas coordenadas do projeto de
/// incêndio, em metros. O teto de cada ambiente é o primeiro forro, laje ou telhado acima do ponto do ambiente, achado
/// por um raio vertical que atravessa os vínculos; a viga sai da própria geometria, o que vale para qualquer família.
/// </summary>
static class LeituraDoEdificio
{
    sealed record Fonte(Document Doc, Transform Transformacao, string Nome);

    static readonly ElementMulticategoryFilter Tetos = new(new[]
    {
        BuiltInCategory.OST_Ceilings, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Roofs,
    });

    public static Edificio Ler(Document doc)
    {
        var avisos = new List<string>();
        var fontes = new List<Fonte> { new(doc, Transform.Identity, "este arquivo") };
        foreach (var vinculo in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            var nome = doc.GetElement(vinculo.GetTypeId())?.Name ?? vinculo.Name;
            if (vinculo.GetLinkDocument() is { } vinculado) fontes.Add(new Fonte(vinculado, vinculo.GetTotalTransform(), nome));
            else avisos.Add($"Vínculo \"{nome}\" não carregado: o SPK não enxerga o que está nele. Recarregue em Gerenciar vínculos.");
        }

        var ambientes = new List<Ambiente>();
        var vigas = new List<Viga>();
        var resumo = new List<string>();
        // O raio precisa de uma vista 3D com os vínculos visíveis: uma vista temporária, desfeita no fim.
        using var transacao = new Transaction(doc, "SPK — leitura da arquitetura");
        transacao.Start();
        try
        {
            var vista = VistaTemporaria(doc);
            var raio = new ReferenceIntersector(Tetos, FindReferenceTarget.Element, vista) { FindReferencesInRevitLinks = true };
            foreach (var fonte in fontes)
            {
                var antes = (ambientes.Count, vigas.Count);
                ambientes.AddRange(Ambientes(doc, fonte, raio));
                vigas.AddRange(Vigas(fonte));
                if (fonte.Doc == doc && ambientes.Count == antes.Item1 && vigas.Count == antes.Item2) continue;
                resumo.Add($"{fonte.Nome}: {ambientes.Count - antes.Item1} ambientes, {vigas.Count - antes.Item2} vigas");
            }
        }
        finally
        {
            transacao.RollBack();
        }

        var semTeto = ambientes.Count(a => a.OrigemDoTeto is OrigemDoTeto.NaoAchado or OrigemDoTeto.AlturaDoAmbiente);
        if (semTeto > 0)
            avisos.Add($"{semTeto} ambiente(s) sem forro, laje ou telhado acima: o teto ficou com a altura do ambiente ou sem valor.");
        if (ambientes.Count == 0)
            avisos.Add("Nenhum ambiente: a arquitetura não tem ambientes (Room) colocados, ou o vínculo não está carregado.");
        return new Edificio(ambientes, vigas, resumo, avisos);
    }

    static View3D VistaTemporaria(Document doc)
    {
        var tipo = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .First(t => t.ViewFamily == ViewFamily.ThreeDimensional);
        var vista = View3D.CreateIsometric(doc, tipo.Id);
        vista.DetailLevel = ViewDetailLevel.Fine;
        return vista;
    }

    static IEnumerable<Ambiente> Ambientes(Document doc, Fonte fonte, ReferenceIntersector raio)
    {
        var opcoes = new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
        foreach (var ambiente in new FilteredElementCollector(fonte.Doc).OfCategory(BuiltInCategory.OST_Rooms)
                     .WhereElementIsNotElementType().OfType<Room>())
        {
            if (ambiente.Area <= 0 || ambiente.Location is not LocationPoint local) continue;
            var contornos = ambiente.GetBoundarySegments(opcoes)
                .Select(laco => (IReadOnlyList<Ponto2>)laco.SelectMany(s => s.GetCurve().Tessellate().SkipLast(1))
                    .Select(p => fonte.Transformacao.OfPoint(p)).Select(p => new Ponto2(Metros(p.X), Metros(p.Y))).ToList())
                .Where(l => l.Count >= 3).ToList();
            var ponto = fonte.Transformacao.OfPoint(local.Point);
            var piso = ponto.Z + (ambiente.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET)?.AsDouble() ?? 0);

            // O primeiro teto acima do ponto do ambiente, a partir de 10 cm acima do nível. O que estiver a menos de
            // PeDireitoMinimo do nível não é teto: é a laje em que o ambiente está quando ela fica acima do nível
            // (contrapiso grosso, piso elevado, terraço sobre a garagem), uma floreira ou o tampo de um elemento baixo. Se
            // o raio saiu de dentro do elemento e a face achada está até PisoAcimaDoNivel, ela é o topo do piso de verdade.
            var nivel = piso;
            var inicio = new XYZ(ponto.X, ponto.Y, nivel + Pes(0.1));
            ReferenceWithContext? acima = null;
            foreach (var r in raio.Find(inicio, XYZ.BasisZ).Where(r => r.Proximity > 1e-6).OrderBy(r => r.Proximity))
            {
                var z = inicio.Z + r.Proximity;
                if (z - nivel >= Pes(PeDireitoMinimo))
                {
                    acima = r;
                    break;
                }
                if (z - nivel <= Pes(PisoAcimaDoNivel)
                    && Elemento(doc, r.GetReference()) is { } achado
                    && achado.Elemento.get_BoundingBox(null) is { } caixa
                    && Math.Min(achado.Transformacao.OfPoint(caixa.Min).Z, achado.Transformacao.OfPoint(caixa.Max).Z) < inicio.Z)
                    piso = Math.Max(piso, z);
            }
            double? teto = null;
            var origem = OrigemDoTeto.NaoAchado;
            if (acima is not null)
            {
                teto = inicio.Z + acima.Proximity;
                origem = Categoria(doc, acima.GetReference()) switch
                {
                    (long)BuiltInCategory.OST_Ceilings => OrigemDoTeto.Forro,
                    (long)BuiltInCategory.OST_Roofs => OrigemDoTeto.Telhado,
                    _ => OrigemDoTeto.Laje,
                };
            }
            else if (ambiente.UnboundedHeight > 0)
            {
                teto = nivel + ambiente.UnboundedHeight;
                origem = OrigemDoTeto.AlturaDoAmbiente;
            }

            yield return new Ambiente($"{fonte.Nome}#{ambiente.Id.Value}", ambiente.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? ambiente.Name,
                ambiente.Number, ambiente.Level?.Name ?? "", fonte.Nome, contornos, ambiente.Area * 0.09290304, Metros(piso),
                teto is { } t ? Metros(t) : null, origem);
        }
    }

    /// <summary>A categoria do elemento achado, no arquivo ou dentro do vínculo.</summary>
    static long? Categoria(Document doc, Reference referencia) => Elemento(doc, referencia)?.Elemento.Category?.Id.Value;

    /// <summary>O elemento achado, no arquivo ou dentro do vínculo, com a transformação para as coordenadas do projeto.</summary>
    static (Element Elemento, Transform Transformacao)? Elemento(Document doc, Reference referencia)
    {
        var elemento = doc.GetElement(referencia.ElementId);
        if (elemento is RevitLinkInstance vinculo && referencia.LinkedElementId != ElementId.InvalidElementId)
            return vinculo.GetLinkDocument()?.GetElement(referencia.LinkedElementId) is { } vinculado
                ? (vinculado, vinculo.GetTotalTransform())
                : null;
        return elemento is null ? null : (elemento, Transform.Identity);
    }

    /// <summary>Abaixo disso, acima do nível do ambiente, o que o raio acha não é teto (m).</summary>
    const double PeDireitoMinimo = 2.0;

    /// <summary>Até quanto acima do nível o topo da laje em que o ambiente está passa a ser o piso dele (m).</summary>
    const double PisoAcimaDoNivel = 0.6;

    static IEnumerable<Viga> Vigas(Fonte fonte)
    {
        var opcoes = new Options { DetailLevel = ViewDetailLevel.Fine };
        foreach (var viga in new FilteredElementCollector(fonte.Doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
                     .WhereElementIsNotElementType().OfType<FamilyInstance>())
        {
            if (viga.Location is not LocationCurve { Curve: { } curva }) continue;
            var a = fonte.Transformacao.OfPoint(curva.GetEndPoint(0));
            var b = fonte.Transformacao.OfPoint(curva.GetEndPoint(1));
            var eixo = new XYZ(b.X - a.X, b.Y - a.Y, 0);
            if (eixo.GetLength() < 1e-6) continue;
            var normal = new XYZ(-eixo.Y, eixo.X, 0).Normalize();
            var pontos = Vertices(viga.get_Geometry(opcoes)).Select(fonte.Transformacao.OfPoint).ToList();
            if (pontos.Count == 0) continue;
            var lateral = pontos.Select(p => (p - a).DotProduct(normal)).ToList();
            yield return new Viga($"{fonte.Nome}#{viga.Id.Value}", fonte.Nome,
                new Ponto2(Metros(a.X), Metros(a.Y)), new Ponto2(Metros(b.X), Metros(b.Y)),
                Metros(lateral.Max() - lateral.Min()), Metros(pontos.Min(p => p.Z)), Metros(pontos.Max(p => p.Z)));
        }
    }

    static IEnumerable<XYZ> Vertices(GeometryElement? geometria)
    {
        if (geometria is null) yield break;
        foreach (var objeto in geometria)
        {
            switch (objeto)
            {
                case Solid { Volume: > 0 } solido:
                    foreach (Edge aresta in solido.Edges)
                        foreach (var p in aresta.Tessellate())
                            yield return p;
                    break;
                case GeometryInstance instancia:
                    foreach (var p in Vertices(instancia.GetInstanceGeometry()))
                        yield return p;
                    break;
            }
        }
    }

    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
    static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);
}
