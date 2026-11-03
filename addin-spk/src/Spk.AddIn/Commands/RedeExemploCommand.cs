using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Spk.AddIn.Commands;

/// <summary>
/// Gera um cenário de teste (de 12 bicos ESFR a edifício de 2 pavimentos), apaga a malha gerada anterior e abre a planta
/// e o 3D enquadrados. O cenário pode gravar também os critérios de projeto que combinam com ele.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class RedeExemploCommand : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var ui = Application.ActiveUIDocument ?? throw new InvalidOperationException("Abra um projeto antes de gerar.");
            var janela = new CenarioJanela(CenariosExemplo.Todos);
            if (janela.ShowDialog() != true || janela.Escolhido is not { } cenario) return;

            var (resumo, ids, vista3d, _) = Criar(ui.Document, cenario, janela.GravarCriterios);
            ui.ActiveView = vista3d;
            ui.ShowElements(ids);
            TaskDialog.Show("SPK — Rede de exemplo", $"""
                {cenario.Nome}

                {resumo}

                {(janela.GravarCriterios ? "Critérios gravados em Dados do projeto: " + cenario.Criterios.Resumo() : "Dados do projeto não foram alterados.")}

                A rede está na vista 3D "SPK — Rede exemplo" e na planta "SPK — Planta exemplo". Agora use Calcular.
                """);
        }
        catch (Exception e)
        {
            TaskDialog.Show("SPK — Rede de exemplo", e.Message);
        }
    }

    /// <summary>O cenário simples, sem mexer nos critérios. Usado pelo teste automático.</summary>
    public static (string Resumo, IList<ElementId> Ids, View3D Vista3d, ViewPlan Planta) Criar(Document doc) =>
        Criar(doc, CenariosExemplo.Simples, gravarCriterios: false);

    /// <summary>O cenário sem janelas: apaga a malha gerada anterior, cria as malhas e as vistas enquadradas.</summary>
    public static (string Resumo, IList<ElementId> Ids, View3D Vista3d, ViewPlan Planta) Criar(Document doc, CenarioExemplo cenario,
        bool gravarCriterios)
    {
        using var transacao = new Transaction(doc, "SPK — Rede de exemplo");
        EngolirAvisos.Aplicar(transacao);
        transacao.Start();
        try
        {
            ApagarExemplo(doc);
            var terreo = NivelBase(doc);
            cenario.Malhas[0].Esboco.Salvar(doc);
            if (gravarCriterios) cenario.Criterios.Salvar(doc);

            var ids = new List<ElementId>();
            var resumos = new List<string>();
            for (var k = 0; k < cenario.Malhas.Count; k++)
            {
                var malha = cenario.Malhas[k];
                var nivel = NivelNaCota(doc, terreo, malha.CotaNivelM);
                var gerado = GeradorMalha.Gerar(doc, new XYZ(Pes(malha.XM), Pes(malha.YM), 0), malha.Esboco, nivel);
                ids.AddRange(gerado.Ids);
                resumos.Add(cenario.Malhas.Count > 1 ? $"Malha {k + 1}, {nivel.Name}: {gerado.Resumo}" : gerado.Resumo);
            }

            var planta = GarantirPlanta(doc, terreo, cenario.Malhas.Max(m => m.CotaNivelM + m.Esboco.AlturaRedeM));
            var vista3d = Garantir3d(doc);
            Enquadrar(doc, vista3d, planta, ids);
            transacao.Commit();
            return (string.Join("\n", resumos), ids, vista3d, planta);
        }
        catch
        {
            if (transacao.GetStatus() == TransactionStatus.Started) transacao.RollBack();
            throw;
        }
    }

    /// <summary>O nível da planta aberta; fora de uma planta, o mais perto da cota zero. Sem nível, cria um.</summary>
    static Level NivelBase(Document doc)
    {
        if (doc.ActiveView is ViewPlan { GenLevel: { } doView }) return doView;
        return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                   .OrderBy(n => Math.Abs(n.ProjectElevation)).ThenByDescending(n => n.ProjectElevation).FirstOrDefault()
               ?? Level.Create(doc, 0);
    }

    /// <summary>O nível <paramref name="cotaM" /> metros acima do base; se não existe, cria "SPK +4,00" e afins.</summary>
    static Level NivelNaCota(Document doc, Level baseNivel, double cotaM)
    {
        if (Math.Abs(cotaM) < 1e-6) return baseNivel;
        var elevacao = baseNivel.ProjectElevation + Pes(cotaM);
        var existente = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(n => Math.Abs(n.ProjectElevation - elevacao) < Pes(0.05));
        if (existente is not null) return existente;
        var novo = Level.Create(doc, elevacao);
        try { novo.Name = $"SPK +{cotaM.ToString("0.00", Entrada.Cultura)}"; }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        return novo;
    }

    static void ApagarExemplo(Document doc)
    {
        var ids = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(DadosEsboco.EhGerado)
            .Select(e => e.Id)
            .ToList();
        if (ids.Count > 0) doc.Delete(ids);
    }

    static ViewPlan GarantirPlanta(Document doc, Level nivel, double alturaRedeM)
    {
        var existente = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
            .FirstOrDefault(v => !v.IsTemplate && v.Name == "SPK — Planta exemplo");
        if (existente is not null && existente.GenLevel?.Id != nivel.Id && doc.ActiveView?.Id != existente.Id)
        {
            doc.Delete(existente.Id);
            existente = null;
        }

        if (existente is not null)
        {
            Preparar(existente);
            try { AjustarCorte(existente, nivel, alturaRedeM); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
            return existente;
        }

        var tipo = TipoDeVista(doc, ViewFamily.FloorPlan);
        var planta = ViewPlan.Create(doc, tipo.Id, nivel.Id);
        planta.Name = "SPK — Planta exemplo";
        Preparar(planta);
        try { AjustarCorte(planta, nivel, alturaRedeM); }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        return planta;
    }

    static View3D Garantir3d(Document doc)
    {
        var existente = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>()
            .FirstOrDefault(v => !v.IsTemplate && v.Name == "SPK — Rede exemplo");
        if (existente is not null)
        {
            Preparar(existente);
            return existente;
        }

        var vista = View3D.CreateIsometric(doc, TipoDeVista(doc, ViewFamily.ThreeDimensional).Id);
        vista.Name = "SPK — Rede exemplo";
        Preparar(vista);
        return vista;
    }

    static void Preparar(View vista)
    {
        try { vista.ViewTemplateId = ElementId.InvalidElementId; }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }

        vista.DetailLevel = ViewDetailLevel.Fine;
        try { vista.DisplayStyle = DisplayStyle.HLR; }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        try { vista.Discipline = ViewDiscipline.Coordination; }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        Mostrar(vista);
        foreach (var categoria in new[] { BuiltInCategory.OST_Levels, BuiltInCategory.OST_Grids, BuiltInCategory.OST_CLines })
        {
            var id = new ElementId(categoria);
            try
            {
                if (vista.CanCategoryBeHidden(id)) vista.SetCategoryHidden(id, true);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        }
    }

    static void Enquadrar(Document doc, View3D iso, ViewPlan planta, IList<ElementId> ids)
    {
        var caixa = Medir(doc, ids);
        if (caixa is not (var min, var max)) return;

        var secao = new BoundingBoxXYZ { Min = min, Max = max };
        iso.SetSectionBox(secao);
        iso.IsSectionBoxActive = true;
        var centro = (min + max) / 2;
        var distancia = Math.Max((max - min).GetLength(), Pes(8)) * 2;
        var olho = centro + new XYZ(1, -1, 0.75).Normalize() * distancia;
        iso.SetOrientation(OlharPara(centro, olho));

        var crop = planta.CropBox;
        var inverso = crop.Transform.Inverse;
        var a = inverso.OfPoint(new XYZ(min.X, min.Y, 0));
        var b = inverso.OfPoint(new XYZ(max.X, max.Y, 0));
        crop.Min = new XYZ(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), crop.Min.Z);
        crop.Max = new XYZ(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), crop.Max.Z);
        planta.CropBox = crop;
        planta.CropBoxActive = true;
        planta.CropBoxVisible = false;
    }

    static ViewOrientation3D OlharPara(XYZ centro, XYZ olho)
    {
        var frente = (centro - olho).Normalize();
        var cima = XYZ.BasisZ - frente * frente.DotProduct(XYZ.BasisZ);
        if (cima.GetLength() < 1e-6) cima = XYZ.BasisY;
        return new ViewOrientation3D(olho, cima.Normalize(), frente);
    }

    static (XYZ Min, XYZ Max)? Medir(Document doc, IList<ElementId> ids)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
        double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        var algum = false;
        foreach (var id in ids)
        {
            var caixa = doc.GetElement(id)?.get_BoundingBox(null);
            if (caixa is null) continue;
            algum = true;
            x0 = Math.Min(x0, caixa.Min.X);
            y0 = Math.Min(y0, caixa.Min.Y);
            z0 = Math.Min(z0, caixa.Min.Z);
            x1 = Math.Max(x1, caixa.Max.X);
            y1 = Math.Max(y1, caixa.Max.Y);
            z1 = Math.Max(z1, caixa.Max.Z);
        }

        if (!algum) return null;
        var margem = Pes(2);
        return (new XYZ(x0 - margem, y0 - margem, z0 - margem), new XYZ(x1 + margem, y1 + margem, z1 + margem));
    }

    static void Mostrar(View vista)
    {
        foreach (var categoria in new[]
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_Sprinklers,
            BuiltInCategory.OST_PipeAccessory,
        })
        {
            var id = new ElementId(categoria);
            try
            {
                if (vista.CanCategoryBeHidden(id)) vista.SetCategoryHidden(id, false);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        }
    }

    /// <summary>Corte da planta 1 m acima da rede mais alta do cenário, para ela aparecer vista de cima.</summary>
    static void AjustarCorte(ViewPlan planta, Level nivel, double alturaRedeM)
    {
        var corte = Math.Max(4, alturaRedeM + 1);
        var intervalo = planta.GetViewRange();
        intervalo.SetLevelId(PlanViewPlane.TopClipPlane, nivel.Id);
        intervalo.SetOffset(PlanViewPlane.TopClipPlane, Pes(corte + 2));
        intervalo.SetLevelId(PlanViewPlane.CutPlane, nivel.Id);
        intervalo.SetOffset(PlanViewPlane.CutPlane, Pes(corte));
        planta.SetViewRange(intervalo);
    }

    static ViewFamilyType TipoDeVista(Document doc, ViewFamily familia) =>
        new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .First(t => t.ViewFamily == familia);

    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
}
