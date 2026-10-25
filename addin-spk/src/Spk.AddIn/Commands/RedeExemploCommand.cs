using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Spk.AddIn.Commands;

/// <summary>
/// Cria a malha de 12 bicos num ponto fixo, apaga a anterior e abre a planta e o 3D já enquadrados.
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
            var doc = ui.Document;
            View3D vista3d;
            ViewPlan planta;
            IList<ElementId> ids;
            string resumo;

            using var transacao = new Transaction(doc, "SPK — Rede de exemplo");
            transacao.Start();
            try
            {
                ApagarExemplo(doc);
                GarantirNivel(doc);
                var esboco = new DadosEsboco();
                esboco.Salvar(doc);
                var gerado = GeradorMalha.Gerar(doc, XYZ.Zero, esboco);
                var nivel = (Level)doc.GetElement(gerado.NivelId);
                planta = GarantirPlanta(doc, nivel);
                vista3d = Garantir3d(doc);
                Enquadrar(doc, vista3d, planta, gerado.Ids);
                ids = gerado.Ids;
                resumo = gerado.Resumo;
                transacao.Commit();
            }
            catch
            {
                if (transacao.GetStatus() == TransactionStatus.Started) transacao.RollBack();
                throw;
            }

            ui.ActiveView = vista3d;
            ui.ShowElements(ids);
            TaskDialog.Show("SPK — Rede de exemplo", $"""
                {resumo}

                A rede é o retângulo no centro da vista 3D "SPK — Rede exemplo":
                3 tubos paralelos, 4 bicos em cada, um tubo na lateral e uma descida.
                Ela fica no ponto zero do projeto, 3 m acima do nível.

                A planta "SPK — Planta exemplo" mostra o mesmo retângulo visto de cima.
                Agora use Calcular.
                """);
            _ = planta;
        }
        catch (Exception e)
        {
            TaskDialog.Show("SPK — Rede de exemplo", e.Message);
        }
    }

    static void ApagarExemplo(Document doc)
    {
        var ids = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() == DadosEsboco.Marca)
            .Select(e => e.Id)
            .ToList();
        if (ids.Count > 0) doc.Delete(ids);
    }

    static Level GarantirNivel(Document doc)
    {
        var nivel = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(n => n.Elevation)
            .FirstOrDefault();
        return nivel ?? Level.Create(doc, 0);
    }

    static ViewPlan GarantirPlanta(Document doc, Level nivel)
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
            try { AjustarCorte(existente, nivel); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
            return existente;
        }

        var tipo = TipoDeVista(doc, ViewFamily.FloorPlan);
        var planta = ViewPlan.Create(doc, tipo.Id, nivel.Id);
        planta.Name = "SPK — Planta exemplo";
        Preparar(planta);
        try { AjustarCorte(planta, nivel); }
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

    static void AjustarCorte(ViewPlan planta, Level nivel)
    {
        var intervalo = planta.GetViewRange();
        intervalo.SetLevelId(PlanViewPlane.CutPlane, nivel.Id);
        intervalo.SetOffset(PlanViewPlane.CutPlane, Pes(4));
        intervalo.SetLevelId(PlanViewPlane.TopClipPlane, nivel.Id);
        intervalo.SetOffset(PlanViewPlane.TopClipPlane, Pes(6));
        planta.SetViewRange(intervalo);
    }

    static ViewFamilyType TipoDeVista(Document doc, ViewFamily familia) =>
        new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .First(t => t.ViewFamily == familia);

    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
}
