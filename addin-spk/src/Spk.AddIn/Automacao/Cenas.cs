using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

namespace Spk.AddIn.Automacao;

/// <summary>Uma cena montada para testar uma ferramenta: os bicos e os ramais que a ferramenta deve usar.</summary>
sealed record Cena(string Resumo, List<FamilyInstance> Bicos, List<Pipe> Ramais);

/// <summary>
/// Cenas de teste das ferramentas de modelagem, montadas do zero numa cópia do projeto. Apagam antes a malha gerada
/// que a cópia tiver, para o cálculo ver só a cena.
/// </summary>
static class Cenas
{
    public static Cena Montar(Document doc, string nome) => nome switch
    {
        "ramalComBicosSoltos" => RamalComBicosSoltos(doc),
        "subgeralComBicosSoltos" => SubgeralComBicosSoltos(doc),
        "subgeralComFileiras" => SubgeralComFileiras(doc),
        "arquiteturaVinculada" => ArquiteturaVinculada(doc),
        _ => throw new InvalidOperationException($"Cena desconhecida: \"{nome}\"."),
    };

    /// <summary>
    /// Subgeral de 9 m no eixo Y, a 3 m de altura, com descida até o piso, e uma fileira de 5 bicos soltos em y = 4,5 m, de
    /// x = 1,5 a 13,5 m, 30 cm abaixo da subgeral; o do meio fica 0,5 m de lado (pede braço). Os ramais da cena: só a
    /// subgeral, que é o tubo que Traçar ramal recebe.
    /// </summary>
    static Cena SubgeralComBicosSoltos(Document doc)
    {
        using var transacao = new Transaction(doc, "SPK — cena de teste");
        transacao.Start();
        ApagarMalhaGerada(doc);
        var nivel = GeradorMalha.Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível.");
        var tipo = GeradorMalha.TipoComTe(doc);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var diametro = GeradorMalha.DiametroComTe(doc, tipo, 65);
        double M(double metros) => GeradorMalha.Pes(metros);
        var z = nivel.Elevation;
        var zSubgeral = z + M(3.0);

        var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, z), new XYZ(0, 0, zSubgeral), diametro);
        var subgeral = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, zSubgeral), new XYZ(0, M(9), zSubgeral), diametro);
        doc.Regenerate();
        GeradorMalha.Ligar(doc, [descida, subgeral], [], nivel);

        var bicos = Bicos(doc, nivel, zSubgeral - M(0.3),
            [(1.5, 4.5, true), (4.5, 4.5, true), (7.5, 5.0, true), (10.5, 4.5, true), (13.5, 4.5, true)]);
        transacao.Commit();
        return new Cena("Subgeral de 9 m com descida e fileira de 5 bicos soltos (1 de lado).", bicos, [subgeral]);
    }

    /// <summary>
    /// Subgeral de 12 m no eixo Y, a 3 m de altura, com descida até o piso, e três fileiras de 4 bicos soltos (y = 3, 6 e
    /// 9 m), dois de cada lado da subgeral (x = ±1,5 e ±4,5 m), 30 cm abaixo dela; na fileira do meio, um bico 0,4 m de
    /// lado (pede braço). Os ramais da cena: só a subgeral.
    /// </summary>
    static Cena SubgeralComFileiras(Document doc)
    {
        using var transacao = new Transaction(doc, "SPK — cena de teste");
        transacao.Start();
        ApagarMalhaGerada(doc);
        var nivel = GeradorMalha.Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível.");
        var tipo = GeradorMalha.TipoComTe(doc);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var diametro = GeradorMalha.DiametroComTe(doc, tipo, 65);
        double M(double metros) => GeradorMalha.Pes(metros);
        var z = nivel.Elevation;
        var zSubgeral = z + M(3.0);

        var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, z), new XYZ(0, 0, zSubgeral), diametro);
        var subgeral = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, zSubgeral), new XYZ(0, M(12), zSubgeral), diametro);
        doc.Regenerate();
        GeradorMalha.Ligar(doc, [descida, subgeral], [], nivel);

        var posicoes = new List<(double X, double Y, bool Marcado)>();
        foreach (var y in new[] { 3.0, 6.0, 9.0 })
            foreach (var x in new[] { -4.5, -1.5, 1.5, 4.5 })
                posicoes.Add((x, y == 6.0 && x == 1.5 ? 6.4 : y, true));
        var bicos = Bicos(doc, nivel, zSubgeral - M(0.3), posicoes.ToArray());
        transacao.Commit();
        return new Cena("Subgeral de 12 m com descida e 3 fileiras de 4 bicos soltos, dos dois lados (1 de lado).", bicos, [subgeral]);
    }

    /// <summary>
    /// Apaga a rede que a cópia do projeto tiver, gera dois modelos e os vincula ao projeto, deslocados 20 m em X e 10 m
    /// em Y (para testar a transformação):
    /// arquitetura com duas salas de 6 × 8 m (paredes de 4 m), forro a 2,80 m só na sala A e laje no nível 4 m sobre as
    /// duas; e estrutura com uma viga sobre a sala B, no nível 4 m. Os arquivos ficam na pasta de saída do teste.
    /// </summary>
    static Cena ArquiteturaVinculada(Document doc)
    {
        var app = doc.Application;
        var pasta = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SPK", "testes", "saida");
        System.IO.Directory.CreateDirectory(pasta);
        var arquivoArq = System.IO.Path.Combine(pasta, "vinculo-arquitetura.rvt");
        var arquivoEst = System.IO.Path.Combine(pasta, "vinculo-estrutura.rvt");
        double M(double metros) => GeradorMalha.Pes(metros);

        var arq = app.NewProjectDocument(Template(app, "DefaultBRAPTB.rte"));
        using (var t = new Transaction(arq, "SPK — arquitetura de teste"))
        {
            t.Start();
            var (terreo, superior) = Niveis(arq);
            var tipoParede = arq.GetDefaultElementTypeId(ElementTypeGroup.WallType);
            XYZ P(double x, double y) => new(M(x), M(y), terreo.Elevation);
            foreach (var (a, b) in new[] { (P(0, 0), P(12, 0)), (P(12, 0), P(12, 8)), (P(12, 8), P(0, 8)), (P(0, 8), P(0, 0)), (P(6, 0), P(6, 8)) })
                Wall.Create(arq, Line.CreateBound(a, b), tipoParede, terreo.Id, M(4), 0, false, false);
            arq.Regenerate();
            var salaA = arq.Create.NewRoom(terreo, new UV(M(3), M(4)));
            salaA.get_Parameter(BuiltInParameter.ROOM_NAME).Set("Sala A");
            salaA.get_Parameter(BuiltInParameter.ROOM_NUMBER).Set("101");
            var salaB = arq.Create.NewRoom(terreo, new UV(M(9), M(4)));
            salaB.get_Parameter(BuiltInParameter.ROOM_NAME).Set("Depósito B");
            salaB.get_Parameter(BuiltInParameter.ROOM_NUMBER).Set("102");

            var tipoForro = new FilteredElementCollector(arq).OfClass(typeof(CeilingType)).FirstElementId();
            if (tipoForro == ElementId.InvalidElementId) throw new InvalidOperationException("O template de arquitetura não tem tipo de forro.");
            var forro = Ceiling.Create(arq, [Retangulo(M(0.1), M(0.1), M(5.9), M(7.9), terreo.Elevation)], tipoForro, terreo.Id);
            forro.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).Set(M(2.8));
            var tipoLaje = arq.GetDefaultElementTypeId(ElementTypeGroup.FloorType);
            Floor.Create(arq, [Retangulo(0, 0, M(12), M(8), superior.Elevation)], tipoLaje, superior.Id);
            t.Commit();
        }
        arq.SaveAs(arquivoArq, new SaveAsOptions { OverwriteExistingFile = true });
        arq.Close(false);

        var est = app.NewProjectDocument(Template(app, "Structural Analysis-DefaultBRAPTB.rte"));
        using (var t = new Transaction(est, "SPK — estrutura de teste"))
        {
            t.Start();
            var (_, superior) = Niveis(est);
            var simbolo = new FilteredElementCollector(est).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>().FirstOrDefault() ?? throw new InvalidOperationException("O template de estrutura não tem família de viga.");
            if (!simbolo.IsActive) simbolo.Activate();
            est.Create.NewFamilyInstance(Line.CreateBound(new XYZ(M(9), 0, superior.Elevation), new XYZ(M(9), M(8), superior.Elevation)),
                simbolo, superior, Autodesk.Revit.DB.Structure.StructuralType.Beam);
            t.Commit();
        }
        est.SaveAs(arquivoEst, new SaveAsOptions { OverwriteExistingFile = true });
        est.Close(false);

        using (var t = new Transaction(doc, "SPK — vínculos de teste"))
        {
            t.Start();
            ApagarRede(doc);
            foreach (var arquivo in new[] { arquivoArq, arquivoEst })
            {
                var tipo = RevitLinkType.Create(doc, ModelPathUtils.ConvertUserVisiblePathToModelPath(arquivo), new RevitLinkOptions(false));
                var instancia = RevitLinkInstance.Create(doc, tipo.ElementId);
                ElementTransformUtils.MoveElement(doc, instancia.Id, new XYZ(M(20), M(10), 0));
            }
            t.Commit();
        }
        return new Cena("Arquitetura (2 salas, forro na A, laje sobre as duas) e estrutura (1 viga) vinculadas, deslocadas 20 m e 10 m.", [], []);
    }

    /// <summary>
    /// Vincula modelos que já existem (arquitetura, estrutura) origem interna com origem interna, como o engenheiro faz
    /// com os arquivos que recebe. Pode apagar antes a rede da cópia e copiar os níveis dos vínculos (com uma planta
    /// para cada nível novo), como o Copiar/Monitorar do Revit.
    /// </summary>
    public static string Vincular(Document doc, PedidoVinculos pedido)
    {
        var linhas = new List<string>();
        using var t = new Transaction(doc, "SPK — vincular modelos");
        t.Start();
        if (pedido.ApagarRede) linhas.Add($"{ApagarRede(doc)} elementos da rede apagados");
        var instancias = new List<RevitLinkInstance>();
        foreach (var arquivo in pedido.Arquivos)
        {
            if (!System.IO.File.Exists(arquivo)) throw new InvalidOperationException($"Arquivo não encontrado: {arquivo}");
            var tipo = RevitLinkType.Create(doc, ModelPathUtils.ConvertUserVisiblePathToModelPath(arquivo), new RevitLinkOptions(false));
            instancias.Add(RevitLinkInstance.Create(doc, tipo.ElementId, ImportPlacement.Origin));
            linhas.Add($"vinculado {System.IO.Path.GetFileName(arquivo)}");
        }
        if (pedido.CopiarNiveis)
        {
            var existentes = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            var tipoPlanta = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.FloorPlan);
            var criados = 0;
            foreach (var instancia in instancias)
            {
                if (instancia.GetLinkDocument() is not { } vinculado) continue;
                var dz = instancia.GetTotalTransform().Origin.Z;
                foreach (var nivel in new FilteredElementCollector(vinculado).OfClass(typeof(Level)).Cast<Level>().OrderBy(n => n.Elevation))
                {
                    var elevacao = nivel.Elevation + dz;
                    if (existentes.Any(n => Math.Abs(n.Elevation - elevacao) < GeradorMalha.Pes(0.05))) continue;
                    var novo = Level.Create(doc, elevacao);
                    try { novo.Name = nivel.Name; }
                    catch (Autodesk.Revit.Exceptions.ArgumentException) { }
                    if (tipoPlanta is not null) ViewPlan.Create(doc, tipoPlanta.Id, novo.Id);
                    existentes.Add(novo);
                    criados++;
                }
            }
            linhas.Add($"{criados} níveis copiados dos vínculos");
        }
        t.Commit();
        return string.Join("; ", linhas);
    }

    /// <summary>Apaga bicos, tubos, conexões e acessórios: a cópia começa sem rede, e só a que o teste gerar entra no cálculo.</summary>
    static int ApagarRede(Document doc)
    {
        var rede = new FilteredElementCollector(doc).WhereElementIsNotElementType()
            .Where(e => e.Category?.Id.Value is (long)BuiltInCategory.OST_Sprinklers or (long)BuiltInCategory.OST_PipeCurves
                or (long)BuiltInCategory.OST_PipeFitting or (long)BuiltInCategory.OST_PipeAccessory)
            .Select(e => e.Id).ToList();
        if (rede.Count > 0) doc.Delete(rede);
        return rede.Count;
    }

    static string Template(Autodesk.Revit.ApplicationServices.Application app, string nome)
    {
        var pasta = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autodesk",
            $"RVT {app.VersionNumber}", "Templates");
        var arquivo = System.IO.Directory.GetFiles(pasta, nome, System.IO.SearchOption.AllDirectories).FirstOrDefault();
        return arquivo ?? throw new InvalidOperationException($"Template \"{nome}\" não encontrado em {pasta}.");
    }

    /// <summary>O nível mais baixo e um nível a 4 m acima dele (criado se não houver).</summary>
    static (Level Terreo, Level Superior) Niveis(Document doc)
    {
        var niveis = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(n => n.Elevation).ToList();
        var terreo = niveis.First();
        var alvo = terreo.Elevation + GeradorMalha.Pes(4);
        var superior = niveis.FirstOrDefault(n => Math.Abs(n.Elevation - alvo) < 0.01) ?? Level.Create(doc, alvo);
        return (terreo, superior);
    }

    static CurveLoop Retangulo(double x0, double y0, double x1, double y1, double z)
    {
        var p = new[] { new XYZ(x0, y0, z), new XYZ(x1, y0, z), new XYZ(x1, y1, z), new XYZ(x0, y1, z) };
        return CurveLoop.Create(Enumerable.Range(0, 4).Select(i => (Curve)Line.CreateBound(p[i], p[(i + 1) % 4])).ToList());
    }

    static void ApagarMalhaGerada(Document doc)
    {
        var anteriores = new FilteredElementCollector(doc).WhereElementIsNotElementType()
            .Where(e => e.Category?.Id.Value is (long)BuiltInCategory.OST_Sprinklers or (long)BuiltInCategory.OST_PipeCurves
                or (long)BuiltInCategory.OST_PipeFitting)
            .Where(DadosEsboco.EhGerado).Select(e => e.Id).ToList();
        if (anteriores.Count > 0) doc.Delete(anteriores);
    }

    /// <summary>Bicos soltos nas posições (em metros), com o conector na cota pedida; marcados ou não como malha gerada.</summary>
    static List<FamilyInstance> Bicos(Document doc, Level nivel, double zBico, (double X, double Y, bool Marcado)[] posicoes)
    {
        var simbolo = GeradorMalha.SimboloDeBico(doc) ?? throw new InvalidOperationException("O projeto não tem família de sprinkler.");
        if (!simbolo.IsActive) simbolo.Activate();
        var bicos = new List<FamilyInstance>();
        foreach (var (x, y, marcado) in posicoes)
        {
            var bico = doc.Create.NewFamilyInstance(new XYZ(GeradorMalha.Pes(x), GeradorMalha.Pes(y), nivel.Elevation), simbolo, nivel,
                StructuralType.NonStructural);
            if (marcado) bico.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(DadosEsboco.Marca);
            bicos.Add(bico);
        }
        doc.Regenerate();
        foreach (var bico in bicos) GeradorMalha.AjustarAltura(bico, zBico, nivel.Elevation);
        return bicos;
    }

    /// <summary>
    /// Ramal reto de 15 m a 3 m de altura, com descida até o piso na ponta de origem, e bicos soltos 30 cm abaixo: três
    /// embaixo do ramal, dois 60 cm de lado (pedem braço e cotovelo) e um 2 m depois do fim do ramal (deve ser recusado,
    /// e fica sem a marca da malha gerada para o cálculo não contar com ele).
    /// </summary>
    static Cena RamalComBicosSoltos(Document doc)
    {
        using var transacao = new Transaction(doc, "SPK — cena de teste");
        transacao.Start();
        ApagarMalhaGerada(doc);
        var nivel = GeradorMalha.Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível.");
        var tipo = GeradorMalha.TipoComTe(doc);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var diametro = GeradorMalha.DiametroComTe(doc, tipo, 40);
        double M(double metros) => GeradorMalha.Pes(metros);
        var z = nivel.Elevation;
        var zRamal = z + M(3.0);

        var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, z), new XYZ(0, 0, zRamal), diametro);
        var ramal = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, zRamal), new XYZ(M(15), 0, zRamal), diametro);
        doc.Regenerate();
        GeradorMalha.Ligar(doc, [descida, ramal], [], nivel);

        var bicos = Bicos(doc, nivel, zRamal - M(0.3),
            [(1.5, 0, true), (4.5, 0, true), (7.5, 0.6, true), (10.5, -0.6, true), (13.5, 0, true), (17, 0, false)]);
        transacao.Commit();

        return new Cena($"Ramal de 15 m com descida e {bicos.Count} bicos soltos (2 de lado, 1 fora do ramal).", bicos, [ramal]);
    }
}
