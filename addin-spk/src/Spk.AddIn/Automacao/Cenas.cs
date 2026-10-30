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
        "andarComSalas" => AndarComSalas(doc),
        "andarEmL" => AndarEmL(doc),
        "salaComVigas" => SalaComVigas(doc, grelha: false),
        "salaComGrelha" => SalaComVigas(doc, grelha: true),
        "teMecanico" => TeMecanico(doc),
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
    /// Apaga a rede da cópia e vincula uma arquitetura de um andar, deslocada 20 m em X e 10 m em Y: corredor de 24 × 2,5 m
    /// (y de 0 a 2,5) e, ao norte dele, quatro salas de 6 × 8 m ("Sala 1" a "Sala 4", x de 0 a 24), paredes de 4 m, forro
    /// a 2,80 m nas salas e laje no nível 4 m sobre tudo. Sem estrutura: o teste é juntar as salas num setor.
    /// </summary>
    static Cena AndarComSalas(Document doc)
    {
        var app = doc.Application;
        var pasta = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SPK", "testes", "saida");
        System.IO.Directory.CreateDirectory(pasta);
        var arquivo = System.IO.Path.Combine(pasta, "vinculo-andar-com-salas.rvt");
        double M(double metros) => GeradorMalha.Pes(metros);

        var arq = app.NewProjectDocument(Template(app, "DefaultBRAPTB.rte"));
        using (var t = new Transaction(arq, "SPK — andar de teste"))
        {
            t.Start();
            var (terreo, superior) = Niveis(arq);
            var tipoParede = arq.GetDefaultElementTypeId(ElementTypeGroup.WallType);
            XYZ P(double x, double y) => new(M(x), M(y), terreo.Elevation);
            var paredes = new List<(XYZ, XYZ)> { (P(0, 0), P(24, 0)), (P(24, 0), P(24, 10.5)), (P(24, 10.5), P(0, 10.5)), (P(0, 10.5), P(0, 0)), (P(0, 2.5), P(24, 2.5)) };
            foreach (var x in new[] { 6.0, 12.0, 18.0 }) paredes.Add((P(x, 2.5), P(x, 10.5)));
            foreach (var (a, b) in paredes) Wall.Create(arq, Line.CreateBound(a, b), tipoParede, terreo.Id, M(4), 0, false, false);
            arq.Regenerate();
            var corredor = arq.Create.NewRoom(terreo, new UV(M(12), M(1.25)));
            corredor.get_Parameter(BuiltInParameter.ROOM_NAME).Set("Corredor");
            corredor.get_Parameter(BuiltInParameter.ROOM_NUMBER).Set("200");
            var tipoForro = new FilteredElementCollector(arq).OfClass(typeof(CeilingType)).FirstElementId();
            for (var k = 0; k < 4; k++)
            {
                var sala = arq.Create.NewRoom(terreo, new UV(M(6 * k + 3), M(6.5)));
                sala.get_Parameter(BuiltInParameter.ROOM_NAME).Set($"Sala {k + 1}");
                sala.get_Parameter(BuiltInParameter.ROOM_NUMBER).Set($"20{k + 1}");
                var forro = Ceiling.Create(arq, [Retangulo(M(6 * k + 0.1), M(2.6), M(6 * k + 5.9), M(10.4), terreo.Elevation)], tipoForro, terreo.Id);
                forro.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).Set(M(2.8));
            }
            Floor.Create(arq, [Retangulo(0, 0, M(24), M(10.5), superior.Elevation)], arq.GetDefaultElementTypeId(ElementTypeGroup.FloorType), superior.Id);
            t.Commit();
        }
        arq.SaveAs(arquivo, new SaveAsOptions { OverwriteExistingFile = true });
        arq.Close(false);

        using (var t = new Transaction(doc, "SPK — vínculo de teste"))
        {
            t.Start();
            ApagarRede(doc);
            var tipo = RevitLinkType.Create(doc, ModelPathUtils.ConvertUserVisiblePathToModelPath(arquivo), new RevitLinkOptions(false));
            var instancia = RevitLinkInstance.Create(doc, tipo.ElementId);
            ElementTransformUtils.MoveElement(doc, instancia.Id, new XYZ(M(20), M(10), 0));
            t.Commit();
        }
        return new Cena("Andar vinculado (deslocado 20 m e 10 m): corredor de 24 × 2,5 m e 4 salas de 6 × 8 m com forro a 2,80 m.", [], []);
    }

    /// <summary>
    /// Como <see cref="AndarComSalas" />, com o corredor em L (2,5 m de largura): uma perna ao longo de x (de 0 a 16,5, y de 0
    /// a 2,5) e outra ao longo de y (x de 14 a 16,5, até y = 18,5). Salas: 1 e 2 (7 × 8 m) ao norte da primeira perna; 5 (7 ×
    /// 8 m) a oeste da segunda, em cima da 2; 3 e 4 (8 × 6 m) a leste da segunda. A sala 2 fica no canto, ao lado das duas.
    /// </summary>
    static Cena AndarEmL(Document doc)
    {
        var app = doc.Application;
        var pasta = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SPK", "testes", "saida");
        System.IO.Directory.CreateDirectory(pasta);
        var arquivo = System.IO.Path.Combine(pasta, "vinculo-andar-em-l.rvt");
        double M(double metros) => GeradorMalha.Pes(metros);

        var arq = app.NewProjectDocument(Template(app, "DefaultBRAPTB.rte"));
        using (var t = new Transaction(arq, "SPK — andar em L de teste"))
        {
            t.Start();
            var (terreo, superior) = Niveis(arq);
            var tipoParede = arq.GetDefaultElementTypeId(ElementTypeGroup.WallType);
            XYZ P(double x, double y) => new(M(x), M(y), terreo.Elevation);
            // Cada parede uma vez só (parede sobreposta vira aviso do Revit).
            var paredes = new (double, double, double, double)[]
            {
                (0, 0, 16.5, 0), (16.5, 0, 16.5, 18.5), (16.5, 18.5, 14, 18.5), (14, 18.5, 14, 2.5), (14, 2.5, 0, 2.5), (0, 2.5, 0, 0),
                (0, 2.5, 0, 10.5), (7, 2.5, 7, 18.5), (0, 10.5, 14, 10.5), (7, 18.5, 14, 18.5),
                (16.5, 4, 24.5, 4), (24.5, 4, 24.5, 16), (24.5, 16, 16.5, 16), (16.5, 10, 24.5, 10),
            };
            foreach (var (x0, y0, x1, y1) in paredes) Wall.Create(arq, Line.CreateBound(P(x0, y0), P(x1, y1)), tipoParede, terreo.Id, M(4), 0, false, false);
            arq.Regenerate();
            var corredor = arq.Create.NewRoom(terreo, new UV(M(8), M(1.25)));
            corredor.get_Parameter(BuiltInParameter.ROOM_NAME).Set("Corredor");
            corredor.get_Parameter(BuiltInParameter.ROOM_NUMBER).Set("200");
            var tipoForro = new FilteredElementCollector(arq).OfClass(typeof(CeilingType)).FirstElementId();
            // Forro também no corredor, a 2,80 m, como num escritório (no andarComSalas o corredor fica só com a laje).
            var laco = new CurveLoop();
            var cantos = new[] { (0.1, 0.1), (16.4, 0.1), (16.4, 18.4), (14.1, 18.4), (14.1, 2.4), (0.1, 2.4) };
            for (var i = 0; i < cantos.Length; i++)
                laco.Append(Line.CreateBound(P(cantos[i].Item1, cantos[i].Item2), P(cantos[(i + 1) % cantos.Length].Item1, cantos[(i + 1) % cantos.Length].Item2)));
            Ceiling.Create(arq, [laco], tipoForro, terreo.Id).get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).Set(M(2.8));
            var salas = new (string Numero, double X0, double Y0, double X1, double Y1)[]
            {
                ("201", 0, 2.5, 7, 10.5), ("202", 7, 2.5, 14, 10.5), ("203", 16.5, 4, 24.5, 10), ("204", 16.5, 10, 24.5, 16), ("205", 7, 10.5, 14, 18.5),
            };
            foreach (var (numero, x0, y0, x1, y1) in salas)
            {
                var sala = arq.Create.NewRoom(terreo, new UV(M((x0 + x1) / 2), M((y0 + y1) / 2)));
                sala.get_Parameter(BuiltInParameter.ROOM_NAME).Set($"Sala {numero[^1]}");
                sala.get_Parameter(BuiltInParameter.ROOM_NUMBER).Set(numero);
                var forro = Ceiling.Create(arq, [Retangulo(M(x0 + 0.1), M(y0 + 0.1), M(x1 - 0.1), M(y1 - 0.1), terreo.Elevation)], tipoForro, terreo.Id);
                forro.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).Set(M(2.8));
            }
            Floor.Create(arq, [Retangulo(0, 0, M(24.5), M(18.5), superior.Elevation)], arq.GetDefaultElementTypeId(ElementTypeGroup.FloorType), superior.Id);
            t.Commit();
        }
        arq.SaveAs(arquivo, new SaveAsOptions { OverwriteExistingFile = true });
        arq.Close(false);

        using (var t = new Transaction(doc, "SPK — vínculo de teste"))
        {
            t.Start();
            ApagarRede(doc);
            var tipo = RevitLinkType.Create(doc, ModelPathUtils.ConvertUserVisiblePathToModelPath(arquivo), new RevitLinkOptions(false));
            var instancia = RevitLinkInstance.Create(doc, tipo.ElementId);
            ElementTransformUtils.MoveElement(doc, instancia.Id, new XYZ(M(20), M(10), 0));
            t.Commit();
        }
        return new Cena("Andar vinculado (deslocado 20 m e 10 m): corredor em L (16,5 m + 18,5 m, 2,5 m de largura) e 5 salas com forro a 2,80 m.", [], []);
    }

    /// <summary>
    /// Apaga a rede da cópia e vincula, deslocados 20 m em X e 10 m em Y: arquitetura com uma sala de 12 × 9 m ("Sala com
    /// vigas", paredes de 4 m, laje no nível 4 m, sem forro) e estrutura com três vigas ao longo de y em x = 3, 6 e 9 m,
    /// com o fundo a 3,25 m (60 cm abaixo da face de baixo da laje de 15 cm): fundas demais para o bico ficar abaixo delas.
    /// </summary>
    /// <param name="grelha">Vigas nas duas direções: uma em x = 6 m e outra em y = 4,5 m (quatro quadros).</param>
    static Cena SalaComVigas(Document doc, bool grelha)
    {
        var app = doc.Application;
        var pasta = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SPK", "testes", "saida");
        System.IO.Directory.CreateDirectory(pasta);
        var arquivoArq = System.IO.Path.Combine(pasta, $"vinculo-sala-com-{(grelha ? "grelha" : "vigas")}-arq.rvt");
        var arquivoEst = System.IO.Path.Combine(pasta, $"vinculo-sala-com-{(grelha ? "grelha" : "vigas")}-est.rvt");
        double M(double metros) => GeradorMalha.Pes(metros);

        var arq = app.NewProjectDocument(Template(app, "DefaultBRAPTB.rte"));
        using (var t = new Transaction(arq, "SPK — sala de teste"))
        {
            t.Start();
            var (terreo, superior) = Niveis(arq);
            var tipoParede = arq.GetDefaultElementTypeId(ElementTypeGroup.WallType);
            XYZ P(double x, double y) => new(M(x), M(y), terreo.Elevation);
            foreach (var (a, b) in new[] { (P(0, 0), P(12, 0)), (P(12, 0), P(12, 9)), (P(12, 9), P(0, 9)), (P(0, 9), P(0, 0)) })
                Wall.Create(arq, Line.CreateBound(a, b), tipoParede, terreo.Id, M(4), 0, false, false);
            arq.Regenerate();
            var sala = arq.Create.NewRoom(terreo, new UV(M(6), M(4.5)));
            sala.get_Parameter(BuiltInParameter.ROOM_NAME).Set("Sala com vigas");
            sala.get_Parameter(BuiltInParameter.ROOM_NUMBER).Set("101");
            Floor.Create(arq, [Retangulo(0, 0, M(12), M(9), superior.Elevation)], arq.GetDefaultElementTypeId(ElementTypeGroup.FloorType), superior.Id);
            t.Commit();
        }
        arq.SaveAs(arquivoArq, new SaveAsOptions { OverwriteExistingFile = true });
        arq.Close(false);

        var est = app.NewProjectDocument(Template(app, "Structural Analysis-DefaultBRAPTB.rte"));
        using (var t = new Transaction(est, "SPK — vigas de teste"))
        {
            t.Start();
            var (_, superior) = Niveis(est);
            var simbolo = new FilteredElementCollector(est).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>().FirstOrDefault() ?? throw new InvalidOperationException("O template de estrutura não tem família de viga.");
            // Tipo com 75 cm de altura, se a família tiver o parâmetro; senão a viga desce até o fundo ficar a 3,25 m.
            var funda = (FamilySymbol)simbolo.Duplicate("SPK viga 20x75");
            var altura = new[] { "h", "d", "Altura", "Height", "H" }.Select(n => funda.LookupParameter(n))
                .FirstOrDefault(p => p is { IsReadOnly: false, StorageType: StorageType.Double });
            altura?.Set(M(0.75));
            new[] { "b", "bf", "Largura", "Width", "B" }.Select(n => funda.LookupParameter(n))
                .FirstOrDefault(p => p is { IsReadOnly: false, StorageType: StorageType.Double })?.Set(M(0.2));
            if (!funda.IsActive) funda.Activate();
            est.Regenerate();
            var vigas = new List<FamilyInstance>();
            var eixos = grelha
                ? new[] { (6.0, 0.0, 6.0, 9.0), (0.0, 4.5, 12.0, 4.5) }
                : new[] { (3.0, 0.0, 3.0, 9.0), (6.0, 0.0, 6.0, 9.0), (9.0, 0.0, 9.0, 9.0) };
            foreach (var (xa, ya, xb, yb) in eixos)
                vigas.Add(est.Create.NewFamilyInstance(Line.CreateBound(new XYZ(M(xa), M(ya), superior.Elevation), new XYZ(M(xb), M(yb), superior.Elevation)),
                    funda, superior, Autodesk.Revit.DB.Structure.StructuralType.Beam));
            est.Regenerate();
            foreach (var viga in vigas)
            {
                var fundo = viga.get_BoundingBox(null)?.Min.Z ?? superior.Elevation;
                var desce = fundo - (superior.Elevation - M(0.75));
                if (desce <= M(0.01)) continue;
                viga.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION)?.Set(-desce);
                viga.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION)?.Set(-desce);
            }
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
        return new Cena("Sala de 12 × 9 m vinculada, laje a 4 m, três vigas com o fundo a 3,25 m (deslocadas 20 m e 10 m).", [], []);
    }

    /// <summary>
    /// A geral que o engenheiro desenharia: tubos horizontais pelos pontos (m, coordenadas do projeto) na altura pedida
    /// acima do nível mais baixo, com cotovelos, e uma descida até o piso no primeiro ponto (a subida da VGA).
    /// </summary>
    public static List<Pipe> DesenharGeral(Document doc, PedidoGeral pedido)
    {
        using var t = new Transaction(doc, "SPK — geral de teste");
        t.Start();
        var nivel = GeradorMalha.Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível.");
        var tipo = GeradorMalha.TipoComTe(doc);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var diametro = GeradorMalha.DiametroComTe(doc, tipo, pedido.Dn);
        double M(double metros) => GeradorMalha.Pes(metros);
        var z = nivel.Elevation + M(pedido.AlturaM);
        var pontos = pedido.Pontos.Select(p => new XYZ(M(p[0]), M(p[1]), z)).ToList();
        var tubos = new List<Pipe>();
        if (pedido.DescidaNoInicio)
            tubos.Add(GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(pontos[0].X, pontos[0].Y, nivel.Elevation), pontos[0], diametro, marcar: false));
        for (var i = 0; i + 1 < pontos.Count; i++)
            tubos.Add(GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, pontos[i], pontos[i + 1], diametro, marcar: false));
        doc.Regenerate();
        GeradorMalha.Ligar(doc, tubos, [], nivel);
        t.Commit();
        return tubos.Where(p => p.IsValidObject && p.Location is LocationCurve { Curve: Line l } && Math.Abs(l.Direction.Z) < 0.05).ToList();
    }

    /// <summary>Tubos soltos, sem ligar, como o projetista desenha com a ferramenta Tubo antes de ligar peça a peça.</summary>
    public static List<Pipe> DesenharTubos(Document doc, IReadOnlyList<TuboDesenhado> pedidos)
    {
        using var t = new Transaction(doc, "SPK — tubos de teste");
        t.Start();
        var nivel = GeradorMalha.Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível.");
        var tipo = GeradorMalha.TipoComTe(doc);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        XYZ P(double[] m) => new(GeradorMalha.Pes(m[0]), GeradorMalha.Pes(m[1]), nivel.Elevation + GeradorMalha.Pes(m[2]));
        var tubos = pedidos.Select(p => GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, P(p.De), P(p.Ate),
            GeradorMalha.DiametroComTe(doc, tipo, p.Dn), marcar: false)).ToList();
        t.Commit();
        return tubos;
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
    /// Tês mecânicos sentados num tubo DN65 de 15 m (a 3 m de altura, ao longo de x, de x = 60 a 75 m), como o projetista
    /// os põe: a boca da passagem 34 mm abaixo do eixo, a saída para baixo. A família é criada aqui, do modelo de conexão
    /// hidráulica do Revit (nada de família de cliente): as duas bocas da passagem no mesmo ponto, em sentidos opostos.
    /// Cada tê leva um bico (o Verificar modelo só olha a rede que chega aos bicos). A (x = 62): descida DN25 de 30 cm e o
    /// bico na ponta. B (66): bico direto na saída. D (70): bico direto, tê fixado (alfinete). E (74): tê e bico num grupo
    /// de modelo. O Corrigir deve encaixar A e B e recusar D e E.
    /// </summary>
    static Cena TeMecanico(Document doc)
    {
        double M(double metros) => GeradorMalha.Pes(metros);
        // A família é salva e carregada antes da transação da cena (o documento da família tem a transação dele).
        var arquivoFamilia = FamiliaTeMecanico(doc.Application);

        using var transacao = new Transaction(doc, "SPK — cena de teste");
        transacao.Start();
        ApagarRede(doc);
        var nivel = GeradorMalha.Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível.");
        var tipo = GeradorMalha.TipoComTe(doc);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var z = nivel.Elevation + M(3.0);
        var tubo = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(M(60), 0, z), new XYZ(M(75), 0, z),
            GeradorMalha.DiametroComTe(doc, tipo, 65), marcar: false);
        var dn25 = GeradorMalha.DiametroComTe(doc, tipo, 25) ?? M(0.025);
        doc.Regenerate();

        if (!doc.LoadFamily(arquivoFamilia, out var familia))
            familia = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().First(f => f.Name == NomeTeMecanico);
        var simbolo = (FamilySymbol)doc.GetElement(familia.GetFamilySymbolIds().First());
        if (!simbolo.IsActive) simbolo.Activate();
        // As bocas da família nascem com o diâmetro do tubo e da descida.
        foreach (var (nome, valor) in new[] { ("Passagem", tubo.Diameter), ("Saida", dn25) })
            simbolo.LookupParameter(nome)?.Set(valor);
        doc.Regenerate();

        var desvio = M(0.034);
        FamilyInstance Te(double x) => doc.Create.NewFamilyInstance(new XYZ(M(x), 0, z - desvio), simbolo, StructuralType.NonStructural);
        Connector Saida(FamilyInstance te) => te.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
            .Single(c => c.CoordinateSystem.BasisZ.Z < -0.99);
        FamilyInstance BicoNaSaida(FamilyInstance te)
        {
            var saida = Saida(te);
            var bico = Bicos(doc, nivel, saida.Origin.Z, [(UnitUtils.ConvertFromInternalUnits(saida.Origin.X, UnitTypeId.Meters), 0, false)])[0];
            if (GeradorMalha.OrigemDoConector(bico) is { } conector) ElementTransformUtils.MoveElement(doc, bico.Id, saida.Origin - conector);
            doc.Regenerate();
            var boca = bico.MEPModel.ConnectorManager.Connectors.Cast<Connector>().First(c => c.ConnectorType == ConnectorType.End);
            boca.ConnectTo(Saida(te));
            return bico;
        }

        var a = Te(62);
        var b = Te(66);
        var d = Te(70);
        var e = Te(74);
        doc.Regenerate();

        // A: descida DN25 de 30 cm na saída e o bico na ponta de baixo dela.
        var saidaA = Saida(a).Origin;
        var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, saidaA, saidaA - new XYZ(0, 0, M(0.3)), dn25, marcar: false);
        doc.Regenerate();
        var pontas = descida.ConnectorManager.Connectors.Cast<Connector>().OrderBy(k => k.Origin.DistanceTo(saidaA)).ToList();
        pontas[0].ConnectTo(Saida(a));
        var bicoA = Bicos(doc, nivel, pontas[1].Origin.Z, [(60 + 2, 0, false)])[0];
        if (GeradorMalha.OrigemDoConector(bicoA) is { } conectorA) ElementTransformUtils.MoveElement(doc, bicoA.Id, pontas[1].Origin - conectorA);
        doc.Regenerate();
        bicoA.MEPModel.ConnectorManager.Connectors.Cast<Connector>().First(k => k.ConnectorType == ConnectorType.End).ConnectTo(pontas[1]);

        var bicos = new List<FamilyInstance> { bicoA, BicoNaSaida(b), BicoNaSaida(d) };
        d.Pinned = true;
        var bicoE = BicoNaSaida(e);
        bicos.Add(bicoE);
        doc.Regenerate();
        var grupo = doc.Create.NewGroup([e.Id, bicoE.Id]);
        grupo.GroupType.Name = "SPK teste - tê mecânico com bico";
        transacao.Commit();
        return new Cena("Tubo DN65 de 15 m com 4 tês mecânicos 34 mm abaixo do eixo, cada um com bico: por descida, direto, fixado e em grupo.", bicos, [tubo]);
    }

    const string NomeTeMecanico = "SPK teste - tê mecânico";

    /// <summary>
    /// Cria, do modelo de conexão hidráulica métrica do Revit, a família de um tê mecânico: duas meias-caixas de 4 cm
    /// encostadas em x = 0 (as bocas da passagem, no mesmo ponto, uma para +x e outra para -x) e um toco para baixo, com a
    /// boca da saída 6 cm abaixo. Os diâmetros das bocas são os parâmetros de tipo "Passagem" e "Saida". Devolve o .rfa.
    /// </summary>
    static string FamiliaTeMecanico(Autodesk.Revit.ApplicationServices.Application app)
    {
        var pastaModelos = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autodesk",
            $"RVT {app.VersionNumber}", "Family Templates");
        var modelo = System.IO.Directory.GetFiles(pastaModelos, "*.rft", System.IO.SearchOption.AllDirectories)
            .FirstOrDefault(f => System.IO.Path.GetFileName(f) is "Conexão hidráulica métrica.rft" or "Metric Pipe Fitting.rft")
            ?? throw new InvalidOperationException($"Modelo de família de conexão hidráulica não encontrado em {pastaModelos}.");
        var familia = app.NewFamilyDocument(modelo);
        double M(double metros) => GeradorMalha.Pes(metros);
        using (var t = new Transaction(familia, "SPK — tê mecânico de teste"))
        {
            t.Start();
            // O modelo em português sai como peça hidrossanitária: a família vira conexão de tubo, tipo tê.
            familia.OwnerFamily.FamilyCategory = Category.GetCategory(familia, BuiltInCategory.OST_PipeFitting);
            familia.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE)?.Set((int)PartType.Tee);
            var gerente = familia.FamilyManager;
            if (gerente.CurrentType is null) gerente.NewType("Padrão");
            var passagem = gerente.AddParameter("Passagem", GroupTypeId.Mechanical, SpecTypeId.PipeSize, false);
            var saida = gerente.AddParameter("Saida", GroupTypeId.Mechanical, SpecTypeId.PipeSize, false);
            gerente.Set(passagem, M(0.065));
            gerente.Set(saida, M(0.025));

            var a = M(0.02);
            var esquerda = Caixa(familia, -a, 0, -a, a, -a, a);
            var direita = Caixa(familia, 0, a, -a, a, -a, a);
            var toco = Caixa(familia, -a / 2, a / 2, -a / 2, a / 2, -M(0.06), -a);
            familia.Regenerate();
            foreach (var (forma, normal, parametro) in new[] { (esquerda, XYZ.BasisX, passagem), (direita, -XYZ.BasisX, passagem), (toco, -XYZ.BasisZ, saida) })
            {
                var face = forma.get_Geometry(new Options { ComputeReferences = true }).OfType<Solid>().SelectMany(s => s.Faces.OfType<PlanarFace>())
                    .First(f => f.FaceNormal.IsAlmostEqualTo(normal));
                var conector = ConnectorElement.CreatePipeConnector(familia, Autodesk.Revit.DB.Plumbing.PipeSystemType.Fitting, face.Reference);
                gerente.AssociateElementParameterToFamilyParameter(conector.get_Parameter(BuiltInParameter.CONNECTOR_DIAMETER), parametro);
            }
            t.Commit();
        }
        var pasta = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SPK", "testes", "saida");
        System.IO.Directory.CreateDirectory(pasta);
        var arquivo = System.IO.Path.Combine(pasta, NomeTeMecanico + ".rfa");
        familia.SaveAs(arquivo, new SaveAsOptions { OverwriteExistingFile = true });
        familia.Close(false);
        return arquivo;
    }

    /// <summary>Caixa sólida de x0 a x1, y0 a y1 e z0 a z1 (pés), no documento da família.</summary>
    static Extrusion Caixa(Document familia, double x0, double x1, double y0, double y1, double z0, double z1)
    {
        var perfil = new CurveArray();
        var p = new[] { new XYZ(x0, y0, z0), new XYZ(x1, y0, z0), new XYZ(x1, y1, z0), new XYZ(x0, y1, z0) };
        for (var i = 0; i < 4; i++) perfil.Append(Line.CreateBound(p[i], p[(i + 1) % 4]));
        var laco = new CurveArrArray();
        laco.Append(perfil);
        var plano = SketchPlane.Create(familia, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z0)));
        return familia.FamilyCreate.NewExtrusion(true, laco, plano, z1 - z0);
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
