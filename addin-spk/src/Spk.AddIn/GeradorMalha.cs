using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

namespace Spk.AddIn;

/// <summary>
/// Malha retangular mínima: bicos, ramais, subgeral e descida até a fonte,
/// com tubos criados de conector a conector.
/// </summary>
static class GeradorMalha
{
    public static ResultadoGeracao Gerar(Document doc, XYZ origemNaPlanta, DadosEsboco dados)
    {
        var nivel = Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível. Abra uma planta ou crie um nível.");
        var tipoTubo = TipoComTe(doc);
        var diametro = DiametroComTe(doc, tipoTubo, dados.DnTubo);
        var tipoSistema = Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var simbolo = SimboloDeBico(doc) ?? throw new InvalidOperationException(
            "O projeto não tem família de sprinkler carregada. Carregue uma e rode Gerar de novo.");
        if (!simbolo.IsActive) simbolo.Activate();

        var sx = Pes(dados.EspacamentoRamalM);
        var sy = Pes(dados.EspacamentoEntreRamaisM);
        var zTubo = nivel.Elevation + Pes(dados.AlturaRedeM);
        var zBico = zTubo - Pes(dados.DropM);
        var x0 = origemNaPlanta.X;
        var y0 = origemNaPlanta.Y;
        var xSubgeral = x0 - sx;

        var bicos = new List<FamilyInstance>();
        for (var j = 0; j < dados.Ramais; j++)
        for (var i = 0; i < dados.BicosPorRamal; i++)
        {
            var bico = doc.Create.NewFamilyInstance(
                new XYZ(x0 + i * sx, y0 + j * sy, nivel.Elevation), simbolo, nivel, StructuralType.NonStructural);
            bico.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(DadosEsboco.Marca);
            bico.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(DadosEsboco.Marca);
            bicos.Add(bico);
        }

        doc.Regenerate();
        foreach (var bico in bicos) AjustarAltura(bico, zBico, nivel.Elevation);
        doc.Regenerate();

        var tubos = new List<Pipe>();
        var porRamal = dados.BicosPorRamal;
        for (var j = 0; j < dados.Ramais; j++)
        {
            var y = y0 + j * sy;
            for (var i = 0; i < porRamal; i++)
            {
                var bico = bicos[j * porRamal + i];
                var naPonta = OrigemDoConector(bico) ?? new XYZ(x0 + i * sx, y, zBico);
                var topo = new XYZ(naPonta.X, naPonta.Y, zTubo);
                tubos.Add(CriarTubo(doc, tipoSistema.Id, tipoTubo.Id, nivel.Id, topo, naPonta, diametro));
                if (i > 0)
                    tubos.Add(CriarTubo(doc, tipoSistema.Id, tipoTubo.Id, nivel.Id,
                        new XYZ(x0 + (i - 1) * sx, y, zTubo), new XYZ(x0 + i * sx, y, zTubo), diametro));
            }

            tubos.Add(CriarTubo(doc, tipoSistema.Id, tipoTubo.Id, nivel.Id,
                new XYZ(xSubgeral, y, zTubo), new XYZ(x0, y, zTubo), diametro));
            if (j > 0)
                tubos.Add(CriarTubo(doc, tipoSistema.Id, tipoTubo.Id, nivel.Id,
                    new XYZ(xSubgeral, y0 + (j - 1) * sy, zTubo), new XYZ(xSubgeral, y, zTubo), diametro));
        }

        tubos.Add(CriarTubo(doc, tipoSistema.Id, tipoTubo.Id, nivel.Id,
            new XYZ(xSubgeral, y0, zTubo), new XYZ(xSubgeral, y0, nivel.Elevation), diametro));

        doc.Regenerate();
        var ligacoes = Ligar(doc, tubos, bicos, nivel);
        var ids = new List<ElementId>();
        ids.AddRange(bicos.Select(b => b.Id));
        ids.AddRange(tubos.Select(t => t.Id));
        foreach (var conexao in ConexoesDos(tubos))
        {
            doc.GetElement(conexao)?.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(DadosEsboco.Marca);
            ids.Add(conexao);
        }
        var mm = diametro is > 0 ? UnitUtils.ConvertFromInternalUnits(diametro.Value, UnitTypeId.Millimeters) : 0;
        var resumo = $"{bicos.Count} bicos e {tubos.Count} tubos. Conexões criadas: {ligacoes}. Diâmetro {mm:0} mm.";
        return new ResultadoGeracao(resumo, ids, nivel.Id);
    }

    public sealed record ResultadoGeracao(string Resumo, IList<ElementId> Ids, ElementId NivelId);

    static IEnumerable<ElementId> ConexoesDos(List<Pipe> tubos)
    {
        var ids = new HashSet<ElementId>();
        foreach (var tubo in tubos)
        foreach (Connector conector in tubo.ConnectorManager.Connectors)
        foreach (Connector outro in conector.AllRefs)
        {
            if (outro.Owner is FamilyInstance peca && peca.Category?.Id.Value == (long)BuiltInCategory.OST_PipeFitting)
                ids.Add(peca.Id);
        }
        return ids;
    }

    static void AjustarAltura(FamilyInstance bico, double zDesejado, double zNivel)
    {
        var deslocamento = zDesejado - zNivel;
        var parametro = bico.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM)
                        ?? bico.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
        if (parametro is { IsReadOnly: false })
        {
            parametro.Set(deslocamento);
            return;
        }

        if (bico.Location is LocationPoint ponto && Math.Abs(ponto.Point.Z - zDesejado) > 1e-4)
            ElementTransformUtils.MoveElement(bico.Document, bico.Id, new XYZ(0, 0, zDesejado - ponto.Point.Z));
    }

    static XYZ? OrigemDoConector(FamilyInstance bico)
    {
        var gerente = bico.MEPModel?.ConnectorManager;
        if (gerente is null) return null;
        foreach (Connector conector in gerente.Connectors)
            if ((conector.ConnectorType & ConnectorType.End) == ConnectorType.End)
                return conector.Origin;
        return null;
    }

    static Pipe CriarTubo(Document doc, ElementId sistema, ElementId tipo, ElementId nivel, XYZ a, XYZ b, double? diametro)
    {
        var tubo = Pipe.Create(doc, sistema, tipo, nivel, a, b);
        tubo.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(DadosEsboco.Marca);
        if (diametro is > 0)
        {
            try { tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diametro.Value); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        }
        return tubo;
    }

    static PipeType TipoComTe(Document doc)
    {
        var tipos = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).Cast<PipeType>().ToList();
        if (tipos.Count == 0)
            throw new InvalidOperationException("O projeto não tem tipo de tubo. Carregue um tipo de tubulação.");
        return tipos.FirstOrDefault(t => t.RoutingPreferenceManager.GetNumberOfRules(RoutingPreferenceRuleGroupType.Junctions) > 0)
               ?? throw new InvalidOperationException(
                   "Nenhum tipo de tubo tem tê na preferência de roteamento. Abra o tipo de tubo e inclua um tê para o diâmetro usado.");
    }

    /// <summary>O DN pedido; se o tipo de tubo não tiver tê nele, o DN com tê mais próximo.</summary>
    static double? DiametroComTe(Document doc, PipeType tipo, int dnPedido)
    {
        var regras = tipo.RoutingPreferenceManager;
        var n = regras.GetNumberOfRules(RoutingPreferenceRuleGroupType.Junctions);
        double[] usuaisMm = [25, 32, 40, 50, 65, 80, 100, 125, 150, 200];
        foreach (var mm in usuaisMm.Prepend(dnPedido).Distinct().OrderBy(d => Math.Abs(d - dnPedido)))
        {
            var pes = UnitUtils.ConvertToInternalUnits(mm / 1000.0, UnitTypeId.Meters);
            if (TemTeNesseDiametro(doc, regras, n, pes)) return pes;
        }

        for (var i = 0; i < n; i++)
        {
            var regra = regras.GetRule(RoutingPreferenceRuleGroupType.Junctions, i);
            if (!EhFamiliaDeTe(doc, regra)) continue;
            for (var c = 0; c < regra.NumberOfCriteria; c++)
            {
                if (regra.GetCriterion(c) is not PrimarySizeCriterion tamanho) continue;
                if (tamanho.MinimumSize > 1e-6) return tamanho.MinimumSize;
            }
        }
        return null;
    }

    static bool TemTeNesseDiametro(Document doc, RoutingPreferenceManager regras, int n, double pes)
    {
        for (var i = 0; i < n; i++)
        {
            var regra = regras.GetRule(RoutingPreferenceRuleGroupType.Junctions, i);
            if (!EhFamiliaDeTe(doc, regra)) continue;
            for (var c = 0; c < regra.NumberOfCriteria; c++)
            {
                if (regra.GetCriterion(c) is not PrimarySizeCriterion tamanho) continue;
                if (pes + 1e-6 >= tamanho.MinimumSize && pes - 1e-6 <= tamanho.MaximumSize) return true;
            }
        }
        return false;
    }

    static bool EhFamiliaDeTe(Document doc, RoutingPreferenceRule regra)
    {
        if (doc.GetElement(regra.MEPPartId) is not FamilySymbol simbolo) return false;
        var parte = simbolo.Family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
        return parte == null || parte.AsInteger() == (int)PartType.Tee;
    }

    static int Ligar(Document doc, List<Pipe> tubos, List<FamilyInstance> bicos, Level nivel)
    {
        var grupos = new List<List<Connector>>();
        foreach (var conector in Conectores(tubos, bicos))
        {
            var grupo = grupos.FirstOrDefault(g => g[0].Origin.DistanceTo(conector.Origin) < 0.05);
            if (grupo is null) grupos.Add([conector]);
            else grupo.Add(conector);
        }

        var feitas = 0;
        var aAbrir = new List<(XYZ Centro, bool Te)>();
        foreach (var grupo in grupos)
        {
            var abertos = grupo.Where(c => !c.IsConnected).ToList();
            if (abertos.Count < 2) continue;
            if (abertos.Count == 2 && Colineares(abertos[0], abertos[1]))
            {
                abertos[0].ConnectTo(abertos[1]);
                feitas++;
                continue;
            }
            if (abertos.Count is not (2 or 3))
                throw new InvalidOperationException(
                    $"Encontro com {abertos.Count} tubos no ponto ({abertos[0].Origin.X:0.00}, {abertos[0].Origin.Y:0.00}). A malha mínima só liga 2 ou 3.");

            var centro = new XYZ(abertos.Average(c => c.Origin.X), abertos.Average(c => c.Origin.Y), abertos.Average(c => c.Origin.Z));
            foreach (var conector in abertos)
                Recuar(conector, Pes(0.01));
            aAbrir.Add((centro, abertos.Count == 3));
        }

        doc.Regenerate();
        var simboloTe = SimboloDeTe(doc);
        foreach (var encontro in aAbrir)
        {
            var proximos = Conectores(tubos, bicos)
                .Where(c => !c.IsConnected && c.Origin.DistanceTo(encontro.Centro) < Pes(0.04))
                .ToList();
            if (encontro.Te)
            {
                if (proximos.Count < 3)
                    throw new InvalidOperationException("O encontro do tê se desfez ao afastar os tubos.");
                var tres = proximos.Take(3).ToList();
                if (!TentarInserirTe(doc, tres))
                    InserirTePorFamilia(doc, encontro.Centro, nivel, simboloTe, tres);
            }
            else if (proximos.Count >= 2)
            {
                doc.Create.NewElbowFitting(proximos[0], proximos[1]);
            }
            feitas++;
        }
        return feitas;
    }

    static bool TentarInserirTe(Document doc, IReadOnlyList<Connector> conectores)
    {
        var c = conectores.ToArray();
        (int A, int B, int Ramo)[] ordens = [(0, 1, 2), (1, 0, 2), (0, 2, 1), (2, 0, 1), (1, 2, 0), (2, 1, 0)];
        foreach (var ordem in ordens.OrderBy(o => c[o.A].CoordinateSystem.BasisZ.DotProduct(c[o.B].CoordinateSystem.BasisZ)))
        {
            try
            {
                doc.Create.NewTeeFitting(c[ordem.A], c[ordem.B], c[ordem.Ramo]);
                return true;
            }
            catch (Exception e) when (e is Autodesk.Revit.Exceptions.ArgumentException or Autodesk.Revit.Exceptions.InvalidOperationException)
            {
            }
        }
        return false;
    }

    static void InserirTePorFamilia(Document doc, XYZ centro, Level nivel, FamilySymbol? simbolo, IReadOnlyList<Connector> tubos)
    {
        if (simbolo is null)
            throw new InvalidOperationException(
                "O Revit não inseriu o tê automático e não há família de tê carregada em Conexões de tubo.");
        if (!simbolo.IsActive) simbolo.Activate();
        var te = doc.Create.NewFamilyInstance(centro, simbolo, nivel, StructuralType.NonStructural);
        doc.Regenerate();
        var ramoTubo = ConectorRamo(tubos);
        var conectoresTe = ConectoresDe(te).ToList();
        if (conectoresTe.Count >= 3)
        {
            var ramoTe = ConectorRamo(conectoresTe);
            var atual = ramoTe.CoordinateSystem.BasisZ;
            var desejado = ramoTubo.CoordinateSystem.BasisZ.Negate();
            var eixo = atual.CrossProduct(desejado);
            if (eixo.GetLength() > 1e-6)
            {
                var angulo = atual.AngleTo(desejado);
                ElementTransformUtils.RotateElement(doc, te.Id, Line.CreateUnbound(centro, eixo.Normalize()), angulo);
                doc.Regenerate();
                conectoresTe = ConectoresDe(te).ToList();
            }
        }

        foreach (var tubo in tubos)
        {
            var destino = conectoresTe
                .Where(c => !c.IsConnected)
                .OrderBy(c => c.CoordinateSystem.BasisZ.DotProduct(tubo.CoordinateSystem.BasisZ))
                .ThenBy(c => c.Origin.DistanceTo(tubo.Origin))
                .FirstOrDefault();
            if (destino is null) continue;
            tubo.ConnectTo(destino);
        }
    }

    static Connector ConectorRamo(IReadOnlyList<Connector> conectores)
    {
        Connector melhor = conectores[0];
        var menor = double.MaxValue;
        foreach (var conector in conectores)
        {
            var paralelismo = conectores
                .Where(outro => !ReferenceEquals(outro, conector))
                .Select(outro => Math.Abs(conector.CoordinateSystem.BasisZ.DotProduct(outro.CoordinateSystem.BasisZ)))
                .DefaultIfEmpty(0)
                .Max();
            if (paralelismo < menor)
            {
                menor = paralelismo;
                melhor = conector;
            }
        }
        return melhor;
    }

    static FamilySymbol? SimboloDeTe(Document doc)
    {
        var filtro = new ElementMulticategoryFilter([BuiltInCategory.OST_PipeFitting]);
        return new FilteredElementCollector(doc).WherePasses(filtro).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .FirstOrDefault(s =>
            {
                var parte = s.Family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
                return parte != null && parte.AsInteger() == (int)PartType.Tee;
            });
    }

    static IEnumerable<Connector> ConectoresDe(FamilyInstance instancia)
    {
        var gerente = instancia.MEPModel?.ConnectorManager;
        if (gerente is null) yield break;
        foreach (Connector conector in gerente.Connectors)
            if ((conector.ConnectorType & ConnectorType.End) == ConnectorType.End)
                yield return conector;
    }

    static void Recuar(Connector conector, double recuoPes)
    {
        if (conector.Owner is not Pipe tubo) return;
        if (tubo.Location is not LocationCurve local || local.Curve is not Line linha) return;
        var p0 = linha.GetEndPoint(0);
        var p1 = linha.GetEndPoint(1);
        if (p0.DistanceTo(p1) <= recuoPes * 2.5) return;
        var direcao = (p1 - p0).Normalize();
        var recuaInicio = conector.Origin.DistanceTo(p0) < conector.Origin.DistanceTo(p1);
        local.Curve = recuaInicio
            ? Line.CreateBound(p0 + direcao * recuoPes, p1)
            : Line.CreateBound(p0, p1 - direcao * recuoPes);
    }

    static IEnumerable<Connector> Conectores(List<Pipe> tubos, List<FamilyInstance> bicos)
    {
        foreach (var tubo in tubos)
            foreach (Connector conector in tubo.ConnectorManager.Connectors)
                if (conector.ConnectorType == ConnectorType.End) yield return conector;
        foreach (var bico in bicos)
        {
            var gerente = bico.MEPModel?.ConnectorManager;
            if (gerente is null) continue;
            foreach (Connector conector in gerente.Connectors)
                if ((conector.ConnectorType & ConnectorType.End) == ConnectorType.End)
                    yield return conector;
        }
    }

    static bool Colineares(Connector a, Connector b) =>
        Math.Abs(Math.Abs(a.CoordinateSystem.BasisZ.DotProduct(b.CoordinateSystem.BasisZ)) - 1) < 0.05;

    /// <summary>
    /// O nível da planta aberta; fora de uma planta, o mais perto da cota zero (o térreo). O mais baixo seria o de
    /// fundação nos modelos do Revit, que têm "Sapata" e "Laje" abaixo do Nível 1.
    /// </summary>
    static Level? Nivel(Document doc)
    {
        if (doc.ActiveView is ViewPlan planta && planta.GenLevel is { } doView) return doView;
        return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(n => Math.Abs(n.ProjectElevation)).ThenByDescending(n => n.ProjectElevation).FirstOrDefault();
    }

    static PipingSystemType? Sistema(Document doc)
    {
        var tipos = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().ToList();
        return tipos.FirstOrDefault(t => t.SystemClassification == MEPSystemClassification.FireProtectWet) ?? tipos.FirstOrDefault();
    }

    static FamilySymbol? SimboloDeBico(Document doc)
    {
        var simbolos = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .Where(s => s.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers)
            .ToList();
        return simbolos.FirstOrDefault(s => s.FamilyName.Contains("ESFR", StringComparison.OrdinalIgnoreCase)
                                            || s.Name.Contains("ESFR", StringComparison.OrdinalIgnoreCase))
               ?? simbolos.FirstOrDefault();
    }

    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
}
