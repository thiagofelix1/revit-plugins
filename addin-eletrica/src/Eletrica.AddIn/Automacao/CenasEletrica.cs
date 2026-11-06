using System.IO;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Structure;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Circuitos;
using Eletrica.AddIn.Condutos;
using Eletrica.AddIn.Lancamento;
using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Automacao;

/// <summary>
///     Cenas do teste automático, montadas do zero no projeto aberto. Tudo sai dos templates que vêm com o Revit (projeto
///     elétrico brasileiro, arquitetura brasileira, famílias de instalação elétrica, luminária, equipamento elétrico e
///     carimbo A1): nada de família de cliente, e a cena roda em qualquer máquina com o Revit em português instalado.
/// </summary>
static class CenasEletrica
{
    public static string Montar(Document doc, string nome, DadosProjeto dados, Dictionary<string, double> metricas) => nome switch
    {
        "apartamento" => Apartamento(doc, dados, metricas),
        _ => throw new InvalidOperationException($"Cena desconhecida: \"{nome}\"."),
    };

    static double M(double metros) => Unidades.DeMetros(metros);

    /// <summary>Deslocamento do vínculo de arquitetura, para testar a transformação do vínculo em todas as ferramentas.</summary>
    static readonly XYZ Deslocamento = new(M(20), M(10), 0);

    // Apartamento de 10 × 8 m, paredes de 2,80 m (coordenadas locais da arquitetura, em m).
    static readonly (string Nome, string Numero, double X0, double Y0, double X1, double Y1)[] Comodos =
    [
        ("Sala de estar", "01", 0, 3, 5, 8),
        ("Cozinha", "02", 5, 5, 8, 8),
        ("Área de serviço", "03", 8, 5, 10, 8),
        ("Circulação", "04", 5, 3, 10, 5),
        ("Banheiro", "05", 8, 0, 10, 3),
        ("Dormitório 1", "06", 0, 0, 4, 3),
        ("Dormitório 2", "07", 4, 0, 8, 3),
    ];

    static readonly (double X0, double Y0, double X1, double Y1)[] Paredes =
    [
        (0, 0, 10, 0), (10, 0, 10, 8), (10, 8, 0, 8), (0, 8, 0, 0), // contorno
        (0, 3, 10, 3), (5, 3, 5, 8), (5, 5, 10, 5), (8, 5, 8, 8), (8, 0, 8, 3), (4, 0, 4, 3),
    ];

    /// <summary>Portas: sala ↔ circulação, cozinha, área de serviço e banheiro ↔ circulação, dormitório 1 ↔ sala, dormitório 2 ↔ circulação.</summary>
    static readonly (double X, double Y)[] Portas = [(5, 4), (6.5, 5), (9, 5), (9, 3), (2, 3), (6.5, 3)];

    const double PeDireito = 2.80;

    /// <summary>
    ///     Apartamento de dois dormitórios: arquitetura vinculada (deslocada 20 m e 10 m) com sete ambientes, portas e pé-direito
    ///     de 2,80 m; sistema de distribuição 127/220 V trifásico; seis famílias de teste com conector elétrico (tomada 10 A,
    ///     luminária, interruptor, chuveiro 220 V, quadro, caixa de passagem) e carimbo A1 se o projeto não tiver; o quadro QD-1
    ///     na circulação a 1,50 m; o padrão do escritório com essas famílias e o tipo de eletroduto do template; os usos dos
    ///     ambientes confirmados pelo nome; dados do projeto 127/220 V TN-C-S, Icc 6 kA, linha aérea, 40 dias de trovoada.
    /// </summary>
    static string Apartamento(Document doc, DadosProjeto dados, Dictionary<string, double> metricas)
    {
        var app = doc.Application;
        var pasta = Path.Combine(TesteAutomatico.PastaSaida, "apartamento");
        Directory.CreateDirectory(pasta);
        var linhas = new List<string>();

        // 1. Famílias de teste (cada uma no seu documento de família, antes da transação do projeto).
        var moldes = new[]
        {
            new Molde(PapelFamilia.Tomada10A, "AE teste - Tomada 10A", "Instalação elétrica métrica.rft", BuiltInCategory.OST_ElectricalFixtures, ElectricalSystemType.PowerCircuit, 127, 1, 100),
            new Molde(PapelFamilia.Luminaria, "AE teste - Luminaria", "Luminárias métricas.rft", BuiltInCategory.OST_LightingFixtures, ElectricalSystemType.PowerCircuit, 127, 1, 100),
            new Molde(PapelFamilia.Interruptor1, "AE teste - Interruptor", "Instalação elétrica métrica.rft", BuiltInCategory.OST_LightingDevices, ElectricalSystemType.PowerCircuit, 127, 1, 0),
            new Molde(PapelFamilia.Chuveiro, "AE teste - Chuveiro", "Instalação elétrica métrica.rft", BuiltInCategory.OST_ElectricalFixtures, ElectricalSystemType.PowerCircuit, 220, 2, 5500),
            new Molde(PapelFamilia.Quadro, "AE teste - Quadro", "Equipamento elétrico métrico.rft", BuiltInCategory.OST_ElectricalEquipment, ElectricalSystemType.PowerBalanced, 220, 3, 0) { Parte = PartType.PanelBoard },
            new Molde(PapelFamilia.CaixaDePassagem, "AE teste - Caixa de passagem", "Equipamento elétrico métrico.rft", BuiltInCategory.OST_GenericModel, null, 0, 0, 0),
        };
        var arquivos = moldes.Select(m => (m, Arquivo: CriarFamilia(app, pasta, m))).ToList();
        string? carimbo = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().Any()
            ? null : CriarCarimbo(app, pasta);

        // 2. Arquitetura num arquivo próprio, salvo e vinculado.
        var arquivoArq = Path.Combine(pasta, "vinculo-apartamento.rvt");
        var (ambientes, portas) = CriarArquitetura(app, arquivoArq);
        linhas.Add($"arquitetura: {ambientes} ambientes, {portas} porta(s)");
        metricas["cena.ambientes"] = ambientes;
        metricas["cena.portas"] = portas;

        FamilyInstance quadro;
        DistributionSysType sistema;
        using (var t = new Transaction(doc, "AutoEletrica — cena apartamento"))
        {
            t.Start();
            EngolirAvisos.Aplicar(t);
            if (DadosProjeto.Carregar(doc) is null)
                ArmazenamentoEletrica.Gravar(doc, DadosProjeto.Chave, dados = new DadosProjeto
                {
                    TensaoFaseNeutro = 127, TensaoFaseFase = 220, Fases = 3, Esquema = EsquemaAterramento.TNCS, IccPresumidaKA = 6,
                    LinhaAerea = true, DiasDeTrovoada = 40, Habitacao = true,
                });
            sistema = GarantirSistema(doc, dados.TensaoFaseNeutro, dados.TensaoFaseFase);
            linhas.Add($"sistema de distribuição: {sistema.Name}");

            var padrao = PadraoEletrica.Carregar(doc);
            foreach (var (m, arquivo) in arquivos)
            {
                if (!doc.LoadFamily(arquivo, out var familia))
                    familia = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().First(f => f.Name == m.Nome);
                var simbolo = (FamilySymbol)doc.GetElement(familia.GetFamilySymbolIds().First());
                padrao.Familias[m.Papel.ToString()] = PadraoEletrica.Nome(simbolo);
            }
            if (carimbo is not null) doc.LoadFamily(carimbo);
            metricas["cena.familias"] = arquivos.Count;

            var tipoEletroduto = new FilteredElementCollector(doc).OfClass(typeof(ConduitType)).Cast<ConduitType>()
                .OrderByDescending(c => c.IsWithFitting).FirstOrDefault() ?? throw new InvalidOperationException("O template não tem tipo de eletroduto.");
            padrao.TipoEletroduto = tipoEletroduto.Name;
            padrao.DnPadrao = 25;
            padrao.Passagem = AlturaDePassagem.Teto;
            padrao.CotaTeto = 2.70;
            padrao.Lancamento = padrao.Lancamento with { PeDireito = PeDireito };
            padrao.Salvar(doc);
            linhas.Add($"eletroduto: {tipoEletroduto.Name}{(tipoEletroduto.IsWithFitting ? " (com conexões)" : " (sem conexões)")}");

            var tipoVinculo = RevitLinkType.Create(doc, ModelPathUtils.ConvertUserVisiblePathToModelPath(arquivoArq), new RevitLinkOptions(false));
            var vinculo = RevitLinkInstance.Create(doc, tipoVinculo.ElementId);
            ElementTransformUtils.MoveElement(doc, vinculo.Id, Deslocamento);

            // Quadro na parede leste da circulação, a 1,50 m.
            var nivel = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation).First();
            var simboloQuadro = padrao.Simbolo(doc, PapelFamilia.Quadro) ?? throw new InvalidOperationException("Família do quadro não carregou.");
            Colocacao.Ativar(simboloQuadro);
            var ponto = new XYZ(M(9.75), M(4.0), nivel.ProjectElevation) + Deslocamento;
            quadro = doc.Create.NewFamilyInstance(ponto, simboloQuadro, nivel, StructuralType.NonStructural);
            doc.Regenerate();
            Colocacao.AjustarAltura(doc, quadro, 1.50, nivel);
            quadro.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME)?.Set("QD-1");
            ParametrosEletrica.Garantir(doc);
            t.Commit();
        }

        // O sistema de distribuição numa transação só dele: se o Revit recusar (erro "sistema de distribuição inválido"), só
        // isso volta, a cena continua e o relatório diz por quê, com o conector do quadro e o sistema tentado.
        var aceitou = TentarSistema(doc, quadro, sistema, out var recusa);
        if (!aceitou)
        {
            linhas.Add($"aviso: o quadro recusou o sistema {Descrever(sistema)} ({recusa}); conector da família: {ConectorDaFamilia(doc, quadro)}");
            // Diagnóstico: quais sistemas do projeto o Revit aceita neste quadro (cada teste é desfeito).
            var aceitos = new List<string>();
            foreach (var outro in new FilteredElementCollector(doc).OfClass(typeof(DistributionSysType)).Cast<DistributionSysType>().Where(d => d.Id != sistema.Id))
            {
                using var grupo = new TransactionGroup(doc, "AutoEletrica — sondar sistema");
                grupo.Start();
                if (TentarSistema(doc, quadro, outro, out _)) aceitos.Add(Descrever(outro));
                grupo.RollBack();
            }
            linhas.Add("sistemas aceitos pelo quadro: " + (aceitos.Count == 0 ? "nenhum" : string.Join(" | ", aceitos)));
            // Tentativa: um sistema novo com tipos de tensão próprios e faixa larga (±10 %).
            DistributionSysType? novo = null;
            using (var t = new Transaction(doc, "AutoEletrica — sistema novo (cena)"))
            {
                t.Start();
                var config = ElectricalSetting.GetElectricalSettings(doc);
                var ff = config.AddVoltageType($"{dados.TensaoFaseFase:0} V AE", V(dados.TensaoFaseFase), V(dados.TensaoFaseFase * 0.9), V(dados.TensaoFaseFase * 1.1));
                var fn = config.AddVoltageType($"{dados.TensaoFaseNeutro:0} V AE", V(dados.TensaoFaseNeutro), V(dados.TensaoFaseNeutro * 0.9), V(dados.TensaoFaseNeutro * 1.1));
                novo = config.AddDistributionSysType($"{dados.TensaoFaseNeutro:0}/{dados.TensaoFaseFase:0} V AE 2", ElectricalPhase.ThreePhase, ElectricalPhaseConfiguration.Wye, 4, ff, fn);
                t.Commit();
            }
            aceitou = TentarSistema(doc, quadro, novo, out var recusaNovo);
            linhas.Add(aceitou ? $"sistema novo aceito: {Descrever(novo)}" : $"sistema novo também recusado: {Descrever(novo)} ({recusaNovo})");
        }
        metricas["cena.sistemaNoQuadro"] = aceitou ? 1 : 0;

        // 3. Usos confirmados pelo nome (o vínculo precisa estar carregado para os ambientes aparecerem).
        var lidos = LeituraDeAmbientes.Ler(doc);
        var confirmados = UsosConfirmados.Carregar(doc);
        foreach (var a in lidos) confirmados.Usos[a.Id] = a.Uso;
        ArmazenamentoEletrica.GravarComTransacao(doc, UsosConfirmados.Chave, confirmados, "AutoEletrica — usos (cena)");
        linhas.Add("usos: " + string.Join("; ", lidos.Select(a => $"{a.Nome} = {ClassificadorDeAmbientes.Nome(a.Uso)} ({a.Area:0.0} m², {a.Perimetro:0.0} m)")));
        metricas["cena.ambientesLidos"] = lidos.Count;
        linhas.Add($"quadro: {Quadros.Nome(quadro)}");
        return string.Join("\n", linhas);
    }

    sealed record Molde(PapelFamilia Papel, string Nome, string Template, BuiltInCategory Categoria, ElectricalSystemType? Sistema, double Volts, int Polos, double VA)
    {
        public PartType? Parte { get; init; }
    }

    /// <summary>
    ///     Família de teste pelo template pedido (ou pelo de equipamento elétrico, mudando a categoria): uma caixa de 10 × 4 × 10 cm,
    ///     não hospedada, com um conector elétrico na tensão, polos e carga pedidos. Devolve o .rfa salvo.
    /// </summary>
    static string CriarFamilia(Autodesk.Revit.ApplicationServices.Application app, string pasta, Molde m)
    {
        // O template pedido; se ele não existir ou não aceitar a categoria, o de equipamento elétrico (aceita quase todas).
        var candidatos = new[] { m.Template, "Equipamento elétrico métrico.rft" }.Distinct().Select(n => ModeloDeFamilia(app, n)).OfType<string>().ToList();
        if (candidatos.Count == 0) throw new InvalidOperationException($"Template de família \"{m.Template}\" não encontrado.");
        Document? familia = null;
        string modelo = candidatos[0];
        foreach (var candidato in candidatos)
        {
            var doc = app.NewFamilyDocument(candidato);
            var dono = doc.OwnerFamily;
            if (dono.FamilyCategory?.BuiltInCategory == m.Categoria || dono.IsAppropriateCategoryId(new ElementId(m.Categoria)))
            {
                familia = doc;
                modelo = candidato;
                break;
            }
            doc.Close(false);
        }
        if (familia is null) throw new InvalidOperationException($"{m.Nome}: nenhum template aceita a categoria {m.Categoria}.");
        using (var t = new Transaction(familia, "AE — família de teste"))
        {
            t.Start();
            var dono = familia.OwnerFamily;
            if (dono.FamilyCategory?.BuiltInCategory != m.Categoria) dono.FamilyCategory = Category.GetCategory(familia, m.Categoria);
            if (dono.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED) is { IsReadOnly: false } plano) plano.Set(0);
            if (m.Parte is { } parte) dono.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE)?.Set((int)parte);
            var gerente = familia.FamilyManager;
            if (gerente.CurrentType is null) gerente.NewType("Padrão");
            else gerente.RenameCurrentType("Padrão");

            var a = M(0.05);
            var caixa = Caixa(familia, -a, a, -M(0.02), M(0.02), 0, M(0.10));
            familia.Regenerate();
            if (m.Sistema is { } sistema)
            {
                var conector = new FilteredElementCollector(familia).OfClass(typeof(ConnectorElement)).Cast<ConnectorElement>()
                                   .FirstOrDefault(c => c.Domain == Domain.DomainElectrical)
                               ?? ConnectorElement.CreateElectricalConnector(familia, sistema, Face(caixa, XYZ.BasisY).Reference);
                Definir(conector, BuiltInParameter.RBS_ELEC_VOLTAGE, UnitUtils.ConvertToInternalUnits(m.Volts, UnitTypeId.Volts));
                Definir(conector, BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES, m.Polos);
                if (m.VA > 0) Definir(conector, BuiltInParameter.RBS_ELEC_APPARENT_LOAD, UnitUtils.ConvertToInternalUnits(m.VA, UnitTypeId.VoltAmperes));
            }
            t.Commit();
        }
        var arquivo = Path.Combine(pasta, m.Nome + ".rfa");
        familia.SaveAs(arquivo, new SaveAsOptions { OverwriteExistingFile = true });
        familia.Close(false);
        return arquivo;
    }

    /// <summary>Carimbo A1 pelo template do Revit (só a moldura que ele traz), para o projeto ter um bloco de margens.</summary>
    static string? CriarCarimbo(Autodesk.Revit.ApplicationServices.Application app, string pasta)
    {
        var modelo = ModeloDeFamilia(app, "A1 métrico.rft") ?? ModeloDeFamilia(app, "A1 metric.rft");
        if (modelo is null) return null;
        var familia = app.NewFamilyDocument(modelo);
        var arquivo = Path.Combine(pasta, "AE teste - Carimbo A1.rfa");
        familia.SaveAs(arquivo, new SaveAsOptions { OverwriteExistingFile = true });
        familia.Close(false);
        return arquivo;
    }

    static (int Ambientes, int Portas) CriarArquitetura(Autodesk.Revit.ApplicationServices.Application app, string arquivo)
    {
        var arq = app.NewProjectDocument(Template(app, "DefaultBRAPTB.rte"));
        int ambientes = 0, portas = 0;
        using (var t = new Transaction(arq, "AE — arquitetura de teste"))
        {
            t.Start();
            EngolirAvisos.Aplicar(t);
            var terreo = new FilteredElementCollector(arq).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
            var tipoParede = arq.GetDefaultElementTypeId(ElementTypeGroup.WallType);
            XYZ P(double x, double y) => new(M(x), M(y), terreo.Elevation);
            var paredes = Paredes.Select(p => Wall.Create(arq, Line.CreateBound(P(p.X0, p.Y0), P(p.X1, p.Y1)), tipoParede, terreo.Id, M(PeDireito), 0, false, false)).ToList();
            arq.Regenerate();
            foreach (var (nome, numero, x0, y0, x1, y1) in Comodos)
            {
                var room = arq.Create.NewRoom(terreo, new UV(M((x0 + x1) / 2), M((y0 + y1) / 2)));
                room.get_Parameter(BuiltInParameter.ROOM_NAME).Set(nome);
                room.get_Parameter(BuiltInParameter.ROOM_NUMBER).Set(numero);
                room.get_Parameter(BuiltInParameter.ROOM_UPPER_LEVEL)?.Set(terreo.Id);
                room.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET)?.Set(M(PeDireito));
                ambientes++;
            }
            // Laje sobre o apartamento, na altura do pé-direito: o teto que uma luminária de face procura.
            var tipoLaje = arq.GetDefaultElementTypeId(ElementTypeGroup.FloorType);
            if (tipoLaje != ElementId.InvalidElementId)
            {
                var contorno = new CurveLoop();
                XYZ[] cantos = [P(0, 0), P(10, 0), P(10, 8), P(0, 8)];
                for (var k = 0; k < 4; k++) contorno.Append(Line.CreateBound(cantos[k], cantos[(k + 1) % 4]));
                var laje = Floor.Create(arq, [contorno], tipoLaje, terreo.Id);
                laje.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(M(PeDireito + 0.10));
            }

            var simboloPorta = new FilteredElementCollector(arq).OfCategory(BuiltInCategory.OST_Doors).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .OrderBy(s => Math.Abs((s.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble() ?? M(0.8)) - M(0.8))).FirstOrDefault();
            if (simboloPorta is not null)
            {
                if (!simboloPorta.IsActive) simboloPorta.Activate();
                foreach (var (x, y) in Portas)
                {
                    var ponto = P(x, y);
                    var parede = paredes.FirstOrDefault(w => w.Location is LocationCurve lc && lc.Curve.Distance(ponto) < M(0.01));
                    if (parede is null) continue;
                    try
                    {
                        arq.Create.NewFamilyInstance(ponto, simboloPorta, parede, terreo, StructuralType.NonStructural);
                        portas++;
                    }
                    catch (Autodesk.Revit.Exceptions.ApplicationException) { }
                }
            }
            t.Commit();
        }
        arq.SaveAs(arquivo, new SaveAsOptions { OverwriteExistingFile = true });
        arq.Close(false);
        return (ambientes, portas);
    }

    /// <summary>O sistema de distribuição trifásico Uo/U do projeto, criado (com as tensões) se não houver.</summary>
    static DistributionSysType GarantirSistema(Document doc, double uo, double u)
    {
        var config = ElectricalSetting.GetElectricalSettings(doc);
        VoltageType Tensao(double v)
        {
            foreach (var objeto in config.VoltageTypes)
                if (objeto is VoltageType vt && Math.Abs(Unidades.Volts(vt.ActualValue) - v) < 0.5) return vt;
            return config.AddVoltageType($"{v:0} V (AE)", V(v), V(v * 0.9), V(v * 1.1));
        }
        var fn = Tensao(uo);
        var ff = Tensao(u);
        foreach (var objeto in config.DistributionSysTypes)
            if (objeto is DistributionSysType ds && ds.ElectricalPhase == ElectricalPhase.ThreePhase && ds.NumWires >= 4
                && ds.VoltageLineToGround is { } lg && Math.Abs(Unidades.Volts(lg.ActualValue) - uo) < 0.5
                && ds.VoltageLineToLine is { } ll && Math.Abs(Unidades.Volts(ll.ActualValue) - u) < 0.5)
                return ds;
        return config.AddDistributionSysType($"{uo:0}/{u:0} V trifásico (AE)", ElectricalPhase.ThreePhase, ElectricalPhaseConfiguration.Wye, 4, ff, fn);
    }

    static double V(double volts) => UnitUtils.ConvertToInternalUnits(volts, UnitTypeId.Volts);

    /// <summary>Põe o sistema no quadro numa transação só dele; se o Revit recusar, só ela volta e a mensagem sai em <paramref name="recusa" />.</summary>
    static bool TentarSistema(Document doc, FamilyInstance quadro, DistributionSysType sistema, out string recusa)
    {
        using var t = new Transaction(doc, "AutoEletrica — sistema do quadro (cena)");
        t.Start();
        var falhas = DesfazerSeErro.Aplicar(t);
        var parametro = quadro.get_Parameter(BuiltInParameter.RBS_FAMILY_CONTENT_DISTRIBUTION_SYSTEM);
        if (parametro is not { IsReadOnly: false })
        {
            t.RollBack();
            recusa = "o parâmetro de sistema de distribuição do quadro é só leitura";
            return false;
        }
        parametro.Set(sistema.Id);
        var aceitou = t.Commit() == TransactionStatus.Committed && falhas.Erros.Count == 0;
        recusa = string.Join("; ", falhas.Erros);
        return aceitou;
    }

    static string Descrever(DistributionSysType ds)
    {
        static string Faixa(VoltageType? v) => v is null ? "-" :
            $"{Unidades.Volts(v.ActualValue):0} ({Unidades.Volts(v.MinValue):0}–{Unidades.Volts(v.MaxValue):0})";
        try
        {
            return $"\"{ds.Name}\" {ds.ElectricalPhase} {ds.ElectricalPhaseConfiguration} {ds.NumWires} fios, " +
                   $"FF {Faixa(ds.VoltageLineToLine)} V, FN {Faixa(ds.VoltageLineToGround)} V";
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e) { return $"\"{ds.Name}\" ({e.Message})"; }
    }

    /// <summary>Diagnóstico: o conector elétrico da família do quadro como o editor de famílias o vê (tensão, polos, sistema).</summary>
    static string ConectorDaFamilia(Document doc, FamilyInstance quadro)
    {
        Document? familia = null;
        try
        {
            familia = doc.EditFamily(quadro.Symbol.Family);
            var partes = new List<string>();
            foreach (var c in new FilteredElementCollector(familia).OfClass(typeof(ConnectorElement)).Cast<ConnectorElement>().Where(c => c.Domain == Domain.DomainElectrical))
            {
                var volts = c.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE) is { } pv ? Unidades.Volts(pv.AsDouble()).ToString("0") : "?";
                var polos = c.get_Parameter(BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES)?.AsInteger().ToString() ?? "?";
                partes.Add($"{c.SystemClassification}, {volts} V, {polos} polo(s)");
            }
            return partes.Count == 0 ? "nenhum conector elétrico" : string.Join(" | ", partes);
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException) { return $"não lido ({e.Message})"; }
        finally { familia?.Close(false); }
    }

    // ----- traçado em estrela -----

    /// <summary>
    ///     Traçado para testar a passagem de fiação sem desenhar à mão: em cada ambiente, um eletroduto do quadro até a luminária
    ///     (ou o primeiro ponto, se não houver luminária) e um da luminária até cada outro ponto do ambiente, todos pela cota de
    ///     passagem do padrão. É o "estrela" que o projetista desenha num apartamento pequeno.
    /// </summary>
    public static (string Texto, int Eletrodutos, List<string> Recusas) TracarEstrela(Document doc, PadraoEletrica padrao)
    {
        var opcoes = TracarEletroduto.OpcoesDoPadrao(doc, padrao);
        var quadro = Quadros.Todos(doc).FirstOrDefault() ?? throw new InvalidOperationException("Nenhum quadro no modelo.");
        var origemQuadro = TracarEletroduto.ExtremoDe(quadro) ?? throw new InvalidOperationException("O quadro não tem ponto de inserção.");
        var lidos = LeituraDeAmbientes.Ler(doc);
        var pontos = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .Where(fi => fi.Category?.BuiltInCategory is BuiltInCategory.OST_ElectricalFixtures or BuiltInCategory.OST_LightingFixtures or BuiltInCategory.OST_LightingDevices)
            .Where(fi => fi.Location is LocationPoint).ToList();
        var recusas = new List<string>();
        var eletrodutos = 0;
        var ambientes = 0;
        using var grupo = new TransactionGroup(doc, "AutoEletrica — traçado em estrela (teste)");
        grupo.Start();
        foreach (var porAmbiente in pontos.GroupBy(p => LeituraDeAmbientes.Em(lidos, ((LocationPoint)p.Location).Point)?.Id ?? ""))
        {
            if (porAmbiente.Key == "")
            {
                recusas.AddRange(porAmbiente.Select(p => $"{p.Name} ({p.Id}): fora de ambiente"));
                continue;
            }
            var lista = porAmbiente.ToList();
            var centro = lista.FirstOrDefault(p => p.Category?.BuiltInCategory == BuiltInCategory.OST_LightingFixtures) ?? lista[0];
            var extremoCentro = TracarEletroduto.ExtremoDe(centro)!;
            void Ligar(TracarEletroduto.Extremo de, TracarEletroduto.Extremo para, string rotulo)
            {
                List<Autodesk.Revit.DB.Electrical.Conduit> novos = [];
                var nivel = Colocacao.NivelAbaixo(doc, de.Ponto.Z) ?? throw new InvalidOperationException("sem nível");
                var motivo = TransacaoSegura.Executar(doc, "AutoEletrica — trecho (teste)", () =>
                {
                    novos = TracarEletroduto.Entre(doc, opcoes, nivel, de, para);
                    return null;
                });
                if (motivo is null) eletrodutos += novos.Count;
                else recusas.Add($"{rotulo}: {motivo}");
            }
            Ligar(origemQuadro, extremoCentro, $"quadro → {centro.Name} ({centro.Id})");
            foreach (var p in lista.Where(p => p.Id != centro.Id))
                Ligar(extremoCentro, TracarEletroduto.ExtremoDe(p)!, $"{centro.Name} → {p.Name} ({p.Id})");
            ambientes++;
        }
        grupo.Assimilate();
        return ($"{eletrodutos} eletroduto(s) em {ambientes} ambiente(s), {recusas.Count} recusa(s)", eletrodutos, recusas);
    }

    /// <summary>A planta de piso do nível do primeiro quadro (ou do nível mais baixo), criada se não houver.</summary>
    public static View PlantaDosQuadros(Document doc)
    {
        var niveis = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation).ToList();
        var quadro = Quadros.Todos(doc).FirstOrDefault();
        var nivel = quadro is not null && doc.GetElement(quadro.LevelId) is Level n ? n : niveis.First();
        var planta = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
            .FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan && v.GenLevel?.Id == nivel.Id);
        if (planta is not null) return planta;
        using var t = new Transaction(doc, "AutoEletrica — planta (teste)");
        t.Start();
        var tipo = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(v => v.ViewFamily == ViewFamily.FloorPlan);
        planta = ViewPlan.Create(doc, tipo.Id, nivel.Id);
        t.Commit();
        return planta;
    }

    // ----- utilidades -----

    public static string Template(Autodesk.Revit.ApplicationServices.Application app, string nome)
    {
        var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autodesk", $"RVT {app.VersionNumber}", "Templates");
        return Directory.GetFiles(pasta, nome, SearchOption.AllDirectories).FirstOrDefault()
               ?? throw new InvalidOperationException($"Template \"{nome}\" não encontrado em {pasta}.");
    }

    static string? ModeloDeFamilia(Autodesk.Revit.ApplicationServices.Application app, string nome)
    {
        var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autodesk", $"RVT {app.VersionNumber}", "Family Templates");
        return Directory.Exists(pasta) ? Directory.GetFiles(pasta, nome, SearchOption.AllDirectories).FirstOrDefault() : null;
    }

    static void Definir(Element e, BuiltInParameter parametro, double valor)
    {
        if (e.get_Parameter(parametro) is not { IsReadOnly: false } p) return;
        if (p.StorageType == StorageType.Integer) p.Set((int)Math.Round(valor));
        else p.Set(valor);
    }

    static void Definir(Element e, BuiltInParameter parametro, int valor)
    {
        if (e.get_Parameter(parametro) is not { IsReadOnly: false } p) return;
        if (p.StorageType == StorageType.Double) p.Set((double)valor);
        else p.Set(valor);
    }

    static PlanarFace Face(Extrusion forma, XYZ normal) =>
        forma.get_Geometry(new Options { ComputeReferences = true }).OfType<Solid>().SelectMany(s => s.Faces.OfType<PlanarFace>())
            .First(f => f.FaceNormal.IsAlmostEqualTo(normal));

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
}
