using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

namespace Spk.AddIn;

/// <summary>
/// Malha retangular de bicos, ramais, subgeral e descida até a fonte, em três arranjos (subgeral lateral, espinha de
/// peixe, grelha), com tubos criados de conector a conector.
/// </summary>
static class GeradorMalha
{
    /// <param name="nivelPedido">O nível da malha. Nulo: o da planta aberta, ou o mais perto da cota zero.</param>
    public static ResultadoGeracao Gerar(Document doc, XYZ origemNaPlanta, DadosEsboco dados, Level? nivelPedido = null)
    {
        var nivel = nivelPedido ?? Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível. Abra uma planta ou crie um nível.");
        var tipoTubo = TipoComTe(doc);
        var diametroRamal = DiametroComTe(doc, tipoTubo, dados.DnTubo);
        var diametroSubgeral = DiametroComTe(doc, tipoTubo, dados.DnSubgeral > 0 ? dados.DnSubgeral : dados.DnTubo);
        var tipoSistema = Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var simbolo = SimboloDeBico(doc) ?? throw new InvalidOperationException(
            "O projeto não tem família de sprinkler carregada. Carregue uma e rode Gerar de novo.");
        if (!simbolo.IsActive) simbolo.Activate();

        var zTubo = nivel.Elevation + Pes(dados.AlturaRedeM);
        var zBico = zTubo - Pes(dados.DropM);
        var desenho = Desenhar(dados, origemNaPlanta.X, origemNaPlanta.Y);

        var bicos = new List<FamilyInstance>();
        foreach (var (x, y) in desenho.Bicos)
        {
            var bico = doc.Create.NewFamilyInstance(new XYZ(x, y, nivel.Elevation), simbolo, nivel, StructuralType.NonStructural);
            bico.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(DadosEsboco.Marca);
            bico.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(DadosEsboco.Marca);
            bicos.Add(bico);
        }

        doc.Regenerate();
        foreach (var bico in bicos) AjustarAltura(bico, zBico, nivel.Elevation);
        doc.Regenerate();

        var tubos = new List<Pipe>();
        for (var k = 0; k < bicos.Count; k++)
        {
            var (x, y) = desenho.Bicos[k];
            var naPonta = OrigemDoConector(bicos[k]) ?? new XYZ(x, y, zBico);
            tubos.Add(CriarTubo(doc, tipoSistema.Id, tipoTubo.Id, nivel.Id, new XYZ(naPonta.X, naPonta.Y, zTubo), naPonta, diametroRamal));
        }
        foreach (var t in desenho.Trechos)
            tubos.Add(CriarTubo(doc, tipoSistema.Id, tipoTubo.Id, nivel.Id,
                new XYZ(t.X0, t.Y0, zTubo), new XYZ(t.X1, t.Y1, zTubo), t.Subgeral ? diametroSubgeral : diametroRamal));
        tubos.Add(CriarTubo(doc, tipoSistema.Id, tipoTubo.Id, nivel.Id,
            new XYZ(desenho.Descida.X, desenho.Descida.Y, zTubo), new XYZ(desenho.Descida.X, desenho.Descida.Y, nivel.Elevation), diametroSubgeral));

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
        double Mm(double? d) => d is > 0 ? UnitUtils.ConvertFromInternalUnits(d.Value, UnitTypeId.Millimeters) : 0;
        var resumo = $"{bicos.Count} bicos e {tubos.Count} tubos ({NomeDo(dados.Arranjo)}). Conexões criadas: {ligacoes}. " +
                     $"Ramais DN {Mm(diametroRamal):0}, subgeral DN {Mm(diametroSubgeral):0}.";
        return new ResultadoGeracao(resumo, ids, nivel.Id);
    }

    public static string NomeDo(Arranjo arranjo) => arranjo switch
    {
        Arranjo.EspinhaDePeixe => "espinha de peixe",
        Arranjo.Grelha => "grelha",
        _ => "subgeral lateral",
    };

    /// <summary>A malha em planta, em pés: onde vão os bicos, os trechos horizontais e a descida até o nível.</summary>
    sealed record Desenho(
        List<(double X, double Y)> Bicos,
        List<(double X0, double Y0, double X1, double Y1, bool Subgeral)> Trechos,
        (double X, double Y) Descida);

    /// <summary>
    /// Pontos e trechos de cada arranjo. Todo encontro tem 2 tubos (cotovelo) ou 3 (tê), que é o que <see cref="Ligar" />
    /// sabe ligar: na espinha de peixe os ramais de um lado ficam deslocados dos do outro, para não haver cruzeta.
    /// </summary>
    static Desenho Desenhar(DadosEsboco dados, double x0, double y0)
    {
        var sx = Pes(dados.EspacamentoRamalM);
        var sy = Pes(dados.EspacamentoEntreRamaisM);
        var bicos = new List<(double X, double Y)>();
        var trechos = new List<(double X0, double Y0, double X1, double Y1, bool Subgeral)>();

        // Um ramal: da subgeral (xInicio) passando pelos bicos, em ordem; com xFim, segue até a outra subgeral.
        void Ramal(double y, double xInicio, IReadOnlyList<double> xsBicos, double? xFim = null)
        {
            var anterior = xInicio;
            foreach (var x in xsBicos)
            {
                bicos.Add((x, y));
                trechos.Add((anterior, y, x, y, false));
                anterior = x;
            }
            if (xFim is { } fim) trechos.Add((anterior, y, fim, y, false));
        }

        // A subgeral passando pelos pontos de encontro, de baixo para cima. A ponta que acaba num cotovelo com um ramal
        // fica no diâmetro do ramal (a subgeral reduz até ali): o Revit não liga cotovelo entre dois diâmetros.
        void Subgeral(double x, IEnumerable<double> ys, bool reduzInicio = false, bool reduzFim = false)
        {
            var lista = ys.OrderBy(y => y).ToList();
            for (var k = 1; k < lista.Count; k++)
            {
                var ponta = (k == 1 && reduzInicio) || (k == lista.Count - 1 && reduzFim);
                trechos.Add((x, lista[k - 1], x, lista[k], !ponta));
            }
        }

        var n = dados.BicosPorRamal;
        var ysRamais = Enumerable.Range(0, dados.Ramais).Select(j => y0 + j * sy).ToList();
        switch (dados.Arranjo)
        {
            case Arranjo.EspinhaDePeixe:
            {
                var deslocamento = Math.Min(Pes(0.5), sy / 4);
                var direita = Enumerable.Range(0, n).Select(i => x0 + sx / 2 + i * sx).ToList();
                var esquerda = Enumerable.Range(0, n).Select(i => x0 - sx / 2 - i * sx).ToList();
                foreach (var y in ysRamais)
                {
                    Ramal(y, x0, direita);
                    Ramal(y + deslocamento, x0, esquerda);
                }
                var inicio = y0 - sy / 2;
                Subgeral(x0, ysRamais.Concat(ysRamais.Select(y => y + deslocamento)).Append(inicio), reduzFim: true);
                return new Desenho(bicos, trechos, (x0, inicio));
            }
            case Arranjo.Grelha:
            {
                var xs = Enumerable.Range(0, n).Select(i => x0 + i * sx).ToList();
                var xEsquerda = x0 - sx;
                var xDireita = x0 + n * sx;
                foreach (var y in ysRamais) Ramal(y, xEsquerda, xs, xDireita);
                Subgeral(xEsquerda, ysRamais, reduzFim: true);
                Subgeral(xDireita, ysRamais, reduzInicio: true, reduzFim: true);
                return new Desenho(bicos, trechos, (xEsquerda, y0));
            }
            default:
            {
                var xs = Enumerable.Range(0, n).Select(i => x0 + i * sx).ToList();
                var xSubgeral = x0 - sx;
                foreach (var y in ysRamais) Ramal(y, xSubgeral, xs);
                Subgeral(xSubgeral, ysRamais, reduzFim: true);
                return new Desenho(bicos, trechos, (xSubgeral, y0));
            }
        }
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

    internal static void AjustarAltura(FamilyInstance bico, double zDesejado, double zNivel)
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

    internal static XYZ? OrigemDoConector(FamilyInstance bico)
    {
        var gerente = bico.MEPModel?.ConnectorManager;
        if (gerente is null) return null;
        foreach (Connector conector in gerente.Connectors)
            if ((conector.ConnectorType & ConnectorType.End) == ConnectorType.End)
                return conector.Origin;
        return null;
    }

    /// <param name="marcar">Marca como malha gerada (a Rede de exemplo apaga o que tem a marca). Falso nas ferramentas de
    /// modelagem, que desenham num projeto real.</param>
    internal static Pipe CriarTubo(Document doc, ElementId sistema, ElementId tipo, ElementId nivel, XYZ a, XYZ b, double? diametro,
        bool marcar = true)
    {
        var tubo = Pipe.Create(doc, sistema, tipo, nivel, a, b);
        if (marcar) tubo.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(DadosEsboco.Marca);
        if (diametro is > 0)
        {
            try { tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diametro.Value); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        }
        return tubo;
    }

    internal static PipeType TipoComTe(Document doc)
    {
        var tipos = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).Cast<PipeType>().ToList();
        if (tipos.Count == 0)
            throw new InvalidOperationException("O projeto não tem tipo de tubo. Carregue um tipo de tubulação.");
        return tipos.FirstOrDefault(t => t.RoutingPreferenceManager.GetNumberOfRules(RoutingPreferenceRuleGroupType.Junctions) > 0)
               ?? throw new InvalidOperationException(
                   "Nenhum tipo de tubo tem tê na preferência de roteamento. Abra o tipo de tubo e inclua um tê para o diâmetro usado.");
    }

    /// <summary>O DN pedido; se o tipo de tubo não tiver tê nele, o DN com tê mais próximo.</summary>
    internal static double? DiametroComTe(Document doc, PipeType tipo, int dnPedido)
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

    internal static int Ligar(Document doc, List<Pipe> tubos, List<FamilyInstance> bicos, Level nivel)
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

    internal static bool TentarInserirTe(Document doc, IReadOnlyList<Connector> conectores)
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

    internal static void InserirTePorFamilia(Document doc, XYZ centro, Level nivel, FamilySymbol? simbolo, IReadOnlyList<Connector> tubos)
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

    internal static FamilySymbol? SimboloDeTe(Document doc)
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

    internal static void Recuar(Connector conector, double recuoPes)
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
    internal static Level? Nivel(Document doc)
    {
        if (doc.ActiveView is ViewPlan planta && planta.GenLevel is { } doView) return doView;
        return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(n => Math.Abs(n.ProjectElevation)).ThenByDescending(n => n.ProjectElevation).FirstOrDefault();
    }

    internal static PipingSystemType? Sistema(Document doc)
    {
        var tipos = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().ToList();
        return tipos.FirstOrDefault(t => t.SystemClassification == MEPSystemClassification.FireProtectWet) ?? tipos.FirstOrDefault();
    }

    internal static FamilySymbol? SimboloDeBico(Document doc)
    {
        var simbolos = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .Where(s => s.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers)
            .ToList();
        return simbolos.FirstOrDefault(s => s.FamilyName.Contains("ESFR", StringComparison.OrdinalIgnoreCase)
                                            || s.Name.Contains("ESFR", StringComparison.OrdinalIgnoreCase))
               ?? simbolos.FirstOrDefault();
    }

    internal static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
}
