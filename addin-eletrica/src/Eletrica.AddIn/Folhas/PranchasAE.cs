using Eletrica.AddIn.Fiacao;
using Eletrica.AddIn.Leitura;
using Eletrica.Motor.Calculo;

namespace Eletrica.AddIn.Folhas;

/// <summary>Região da folha em pés, como o Revit mede.</summary>
readonly record struct Regiao(double X0, double Y0, double X1, double Y1)
{
    public double Largura => X1 - X0;
    public double Altura => Y1 - Y0;
    public XYZ Centro => new((X0 + X1) / 2, (Y0 + Y1) / 2, 0);
    public Regiao Parte(double fx0, double fy0, double fx1, double fy1) => new(X0 + Largura * fx0, Y0 + Altura * fy0, X0 + Largura * fx1, Y0 + Altura * fy1);
}

/// <summary>
///     Pranchas (itens 9.6 e 9.7 do plano) no carimbo do projeto: uma planta por nível com os eletrodutos, pontos, fios
///     e etiquetas (vista "AE — Planta — nível", com template "AE — Planta" se existir), uma folha por quadro com o quadro de
///     cargas e o unifilar, e a folha de documentos com a prumada, a legenda de fiação e a lista de material. As folhas do
///     plugin têm número AE-… e ficam registradas no projeto: rodar de novo refaz as mesmas folhas e não toca nas outras.
/// </summary>
static class PranchasAE
{
    public const string PrefixoNumero = "AE-";
    const string ChaveRegistro = "FolhasAE";
    const string TemplatePlanta = "AE — Planta";

    public sealed record Feito(IReadOnlyList<ViewSheet> Folhas, IReadOnlyList<string> Avisos);

    public static Feito Gerar(Document doc, LeitorInstalacao.Lida lida, ResultadoInstalacao r, Padrao.PadraoEletrica padrao)
    {
        var avisos = new List<string>();
        var folhas = new List<ViewSheet>();
        var carimbo = Carimbo(doc);
        if (carimbo == ElementId.InvalidElementId)
            throw new InvalidOperationException("O projeto não tem família de carimbo (Title Block) carregada. Carregue o carimbo do escritório e rode de novo.");

        // Vistas que vão para as folhas: quadros, unifilares, prumada, legenda, lista (refeitas agora).
        var quadros = QuadrosNoRevit.Gerar(doc, lida, r);
        avisos.AddRange(quadros.Avisos);
        var lista = ListaDeMaterialRevit.Gerar(doc, lida, r);
        avisos.AddRange(lista.Avisos);
        var legenda = LegendaDeFiacao.Gerar(doc, r, lida.Instalacao, padrao);

        using var transacao = new Transaction(doc, "AutoEletrica — Pranchas");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);

        // Plantas por nível
        var niveis = NiveisComEletrica(doc);
        var n = 1;
        foreach (var (nivel, caixa) in niveis)
        {
            var planta = Planta(doc, nivel, caixa, avisos);
            var folha = Recriar(doc, carimbo, $"{PrefixoNumero}PL-{n:00}", $"Elétrica — planta {nivel.Name}");
            doc.Regenerate();
            var util = Util(folha);
            ColocarNaRegiao(doc, folha, planta, util.Parte(0, 0, 0.72, 1), avisos);
            Colocar(doc, folha, legenda, util.Parte(0.74, 0.55, 1, 1).Centro, avisos);
            folhas.Add(folha);
            n++;
        }
        if (niveis.Count == 0) avisos.Add("Nenhum nível com elementos elétricos: nenhuma planta gerada.");

        // Uma folha por quadro: quadro de cargas à esquerda, unifilar à direita
        var k = 1;
        var nomes = QuadrosNoRevit.NomesUnicos(r.Quadros);
        foreach (var q in r.Quadros)
        {
            var nome = nomes[q.Id];
            var cargas = quadros.Vistas.FirstOrDefault(v => v.Name == Desenho.NomeValido($"{Desenho.Prefixo}Quadro de cargas — {nome}"));
            var unifilar = quadros.Vistas.FirstOrDefault(v => v.Name == Desenho.NomeValido($"{Desenho.Prefixo}Unifilar — {nome}"));
            var folha = Recriar(doc, carimbo, $"{PrefixoNumero}QD-{k:00}", $"Elétrica — {nome} - quadro de cargas e unifilar");
            doc.Regenerate();
            var util = Util(folha);
            if (cargas is not null) Colocar(doc, folha, cargas, util.Parte(0, 0, 0.5, 1).Centro, avisos);
            if (unifilar is not null) Colocar(doc, folha, unifilar, util.Parte(0.52, 0, 1, 1).Centro, avisos);
            folhas.Add(folha);
            k++;
        }

        // Documentos: prumada, lista de material, tabelas nativas
        var docs = Recriar(doc, carimbo, $"{PrefixoNumero}DOC-01", "Elétrica — prumada, legenda e lista de material");
        doc.Regenerate();
        var regiao = Util(docs);
        if (quadros.Vistas.FirstOrDefault(v => v.Name == $"{Desenho.Prefixo}Prumada elétrica") is { } prumada) Colocar(doc, docs, prumada, regiao.Parte(0, 0.5, 0.5, 1).Centro, avisos);
        Colocar(doc, docs, lista.Vista, regiao.Parte(0.52, 0, 1, 1).Centro, avisos);
        var y = regiao.Y0 + regiao.Altura * 0.45;
        foreach (var tabela in lista.Tabelas.Take(4))
        {
            try
            {
                ScheduleSheetInstance.Create(doc, docs.Id, tabela.Id, new XYZ(regiao.X0, y, 0));
                y -= regiao.Altura * 0.11;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException e) { avisos.Add($"Tabela {tabela.Name} não entrou na folha: {e.Message}"); }
        }
        folhas.Add(docs);

        transacao.Commit();
        return new Feito(folhas, avisos);
    }

    // ----- folhas -----

    static Dictionary<string, string> Registro(Document doc) => ArmazenamentoEletrica.Ler<Dictionary<string, string>>(doc, ChaveRegistro) ?? new Dictionary<string, string>();

    /// <summary>Apaga a folha do plugin com este papel (pelo número ou pelo registro) e cria outra com o carimbo. Folha que não é do plugin não é tocada.</summary>
    static ViewSheet Recriar(Document doc, ElementId carimbo, string numero, string nome)
    {
        var registro = Registro(doc);
        var ids = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Where(s => s.SheetNumber == numero).Select(s => s.Id).ToList();
        if (registro.TryGetValue(numero, out var unico) && doc.GetElement(unico) is ViewSheet anterior) ids.Add(anterior.Id);
        if (ids.Count > 0) doc.Delete(ids.Distinct().ToList());
        var folha = ViewSheet.Create(doc, carimbo);
        folha.SheetNumber = numero;
        folha.Name = Desenho.NomeValido(nome);
        registro[numero] = folha.UniqueId;
        ArmazenamentoEletrica.Gravar(doc, ChaveRegistro, registro);
        return folha;
    }

    static ElementId Carimbo(Document doc) =>
        new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().ToElementIds().FirstOrDefault() ?? ElementId.InvalidElementId;

    /// <summary>A área útil da folha menos margem e a coluna da direita do carimbo.</summary>
    static Regiao Util(ViewSheet folha)
    {
        var contorno = folha.Outline;
        var largura = contorno.Max.U - contorno.Min.U;
        var altura = contorno.Max.V - contorno.Min.V;
        var area = largura < 0.3 || altura < 0.3
            ? new Regiao(0, 0, Unidades.DeMetros(0.841), Unidades.DeMetros(0.594))
            : new Regiao(contorno.Min.U, contorno.Min.V, contorno.Max.U, contorno.Max.V);
        return area.Parte(0.03, 0.05, 0.84, 0.97);
    }

    static Viewport? Colocar(Document doc, ViewSheet folha, View vista, XYZ centro, List<string> avisos)
    {
        if (!Viewport.CanAddViewToSheet(doc, folha.Id, vista.Id))
        {
            avisos.Add($"A vista {vista.Name} já está em outra folha e não entrou em {folha.SheetNumber}.");
            return null;
        }
        try
        {
            var viewport = Viewport.Create(doc, folha.Id, vista.Id, centro);
            doc.Regenerate();
            var quadro = viewport.GetBoxOutline();
            var atual = (quadro.MinimumPoint + quadro.MaximumPoint) / 2;
            viewport.SetBoxCenter(viewport.GetBoxCenter() + new XYZ(centro.X - atual.X, centro.Y - atual.Y, 0));
            return viewport;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e)
        {
            avisos.Add($"{vista.Name} em {folha.SheetNumber}: {e.Message}");
            return null;
        }
    }

    static readonly int[] Escalas = [20, 25, 50, 75, 100, 125, 150, 200, 250, 500];

    /// <summary>Coloca a planta no centro da região e sobe a escala até caber.</summary>
    static void ColocarNaRegiao(Document doc, ViewSheet folha, View vista, Regiao regiao, List<string> avisos)
    {
        var viewport = Colocar(doc, folha, vista, regiao.Centro, avisos);
        if (viewport is null) return;
        for (var tentativa = 0; tentativa < 8; tentativa++)
        {
            doc.Regenerate();
            var caixa = viewport.GetBoxOutline();
            if (caixa.MaximumPoint.X - caixa.MinimumPoint.X <= regiao.Largura && caixa.MaximumPoint.Y - caixa.MinimumPoint.Y <= regiao.Altura) break;
            var maior = Escalas.FirstOrDefault(e => e > vista.Scale);
            if (maior == 0) break;
            try { vista.Scale = maior; }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { break; }
        }
        doc.Regenerate();
        var quadro = viewport.GetBoxOutline();
        var centro = (quadro.MinimumPoint + quadro.MaximumPoint) / 2;
        viewport.SetBoxCenter(viewport.GetBoxCenter() + new XYZ(regiao.Centro.X - centro.X, regiao.Centro.Y - centro.Y, 0));
    }

    // ----- plantas -----

    static readonly BuiltInCategory[] CategoriasEletricas =
    [
        BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
        BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_Conduit, BuiltInCategory.OST_ConduitFitting,
    ];

    /// <summary>Níveis que têm elementos elétricos, com a caixa deles em planta.</summary>
    static List<(Level Nivel, BoundingBoxXYZ Caixa)> NiveisComEletrica(Document doc)
    {
        var niveis = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation).ToList();
        var porNivel = new Dictionary<ElementId, BoundingBoxXYZ>();
        foreach (var e in new FilteredElementCollector(doc).WherePasses(new ElementMulticategoryFilter(CategoriasEletricas)).WhereElementIsNotElementType())
        {
            var caixa = e.get_BoundingBox(null);
            if (caixa is null) continue;
            var z = (caixa.Min.Z + caixa.Max.Z) / 2;
            var nivel = niveis.LastOrDefault(l => l.ProjectElevation <= z + Unidades.DeMetros(0.3)) ?? niveis.FirstOrDefault();
            if (nivel is null) continue;
            if (!porNivel.TryGetValue(nivel.Id, out var acumulada)) porNivel[nivel.Id] = new BoundingBoxXYZ { Min = caixa.Min, Max = caixa.Max };
            else
            {
                acumulada.Min = new XYZ(Math.Min(acumulada.Min.X, caixa.Min.X), Math.Min(acumulada.Min.Y, caixa.Min.Y), Math.Min(acumulada.Min.Z, caixa.Min.Z));
                acumulada.Max = new XYZ(Math.Max(acumulada.Max.X, caixa.Max.X), Math.Max(acumulada.Max.Y, caixa.Max.Y), Math.Max(acumulada.Max.Z, caixa.Max.Z));
            }
        }
        return niveis.Where(l => porNivel.ContainsKey(l.Id)).Select(l => (l, porNivel[l.Id])).ToList();
    }

    static ViewPlan Planta(Document doc, Level nivel, BoundingBoxXYZ caixa, List<string> avisos)
    {
        var nome = Desenho.NomeValido($"{Desenho.Prefixo}Planta — {nivel.Name}");
        var antiga = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().FirstOrDefault(v => v.Name == nome && !v.IsTemplate);
        if (antiga is not null) doc.Delete(antiga.Id);
        var tipo = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(t => t.ViewFamily == ViewFamily.FloorPlan);
        var planta = ViewPlan.Create(doc, tipo.Id, nivel.Id);
        planta.Name = Desenho.NomeValido(nome);
        var template = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().FirstOrDefault(v => v.IsTemplate && v.Name == TemplatePlanta && v.ViewType == ViewType.FloorPlan);
        if (template is not null)
        {
            try { planta.ViewTemplateId = template.Id; }
            catch (Autodesk.Revit.Exceptions.ApplicationException e) { avisos.Add($"Template {TemplatePlanta}: {e.Message}"); }
        }
        else if (planta.ViewTemplateId != ElementId.InvalidElementId)
        {
            // O tipo de vista do escritório já aplica um template padrão: ele manda na escala, no detalhe e na visibilidade.
            avisos.Add($"{nome}: usa o template padrão do tipo de vista ({doc.GetElement(planta.ViewTemplateId)?.Name}); crie um template \"{TemplatePlanta}\" para o padrão do AutoEletrica.");
        }
        else
        {
            planta.Scale = 50;
            planta.DetailLevel = ViewDetailLevel.Medium;
            var meioTom = new OverrideGraphicSettings().SetHalftone(true);
            foreach (var vinculo in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).ToElementIds())
            {
                try { planta.SetElementOverrides(vinculo, meioTom); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { }
            }
        }
        // Corte acima do mais alto elemento elétrico (eletrodutos no teto), recorte na caixa com folga.
        try
        {
            var intervalo = planta.GetViewRange();
            var corte = Math.Max(caixa.Max.Z - nivel.ProjectElevation + Unidades.DeMetros(0.3), Unidades.DeMetros(1.2));
            intervalo.SetLevelId(PlanViewPlane.TopClipPlane, nivel.Id);
            intervalo.SetOffset(PlanViewPlane.TopClipPlane, corte + Unidades.DeMetros(0.5));
            intervalo.SetLevelId(PlanViewPlane.CutPlane, nivel.Id);
            intervalo.SetOffset(PlanViewPlane.CutPlane, corte);
            planta.SetViewRange(intervalo);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e) { avisos.Add($"Intervalo da planta {nivel.Name}: {e.Message}"); }
        try
        {
            var folga = Unidades.DeMetros(2.0);
            planta.CropBoxActive = true;
            planta.CropBoxVisible = false;
            var crop = planta.CropBox;
            var inverso = crop.Transform.Inverse;
            var a = inverso.OfPoint(new XYZ(caixa.Min.X - folga, caixa.Min.Y - folga, 0));
            var b = inverso.OfPoint(new XYZ(caixa.Max.X + folga, caixa.Max.Y + folga, 0));
            crop.Min = new XYZ(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), crop.Min.Z);
            crop.Max = new XYZ(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), crop.Max.Z);
            planta.CropBox = crop;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e) { avisos.Add($"Recorte da planta {nivel.Name}: {e.Message}"); }
        return planta;
    }
}
