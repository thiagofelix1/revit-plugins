using Autodesk.Revit.DB.Electrical;

namespace Eletrica.AddIn.Fiacao;

/// <summary>
///     Etiquetas na planta (itens 6.1 e 6.2 do plano): eletrodutos e fios com AE Fiação, tomadas, luminárias, interruptores e
///     quadros, com as famílias de etiqueta carregadas (prefere as que têm "AE" no nome). Não repete o que já está etiquetado
///     na vista. Usado pelo botão Etiquetar e pelo teste automático.
/// </summary>
static class Etiquetagem
{
    public sealed record Feito(int Criadas, string? Recusa);

    public static Feito Executar(Document doc, View vista)
    {
        if (vista.ViewType is not (ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.EngineeringPlan))
            return new Feito(0, "Abra uma planta (piso ou forro) para etiquetar.");
        var tagEletroduto = TipoDeEtiqueta(doc, BuiltInCategory.OST_ConduitTags);
        var tagFio = TipoDeEtiqueta(doc, BuiltInCategory.OST_WireTags);
        var tagTomada = TipoDeEtiqueta(doc, BuiltInCategory.OST_ElectricalFixtureTags);
        var tagLuminaria = TipoDeEtiqueta(doc, BuiltInCategory.OST_LightingFixtureTags);
        var tagInterruptor = TipoDeEtiqueta(doc, BuiltInCategory.OST_LightingDeviceTags);
        var tagQuadro = TipoDeEtiqueta(doc, BuiltInCategory.OST_ElectricalEquipmentTags);
        if (tagEletroduto is null && tagFio is null && tagTomada is null && tagLuminaria is null && tagInterruptor is null && tagQuadro is null)
            return new Feito(0, "Nenhuma família de etiqueta de eletroduto, fio, tomada, luminária, interruptor ou quadro carregada. Carregue as etiquetas do escritório " +
                                "(com os rótulos AE Fiação, AE Ambiente, AE Potência, Circuit Number) e rode de novo.");

        var jaEtiquetados = new FilteredElementCollector(doc, vista.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>()
            .SelectMany(t => t.GetTaggedLocalElementIds()).ToHashSet();
        var criadas = 0;
        using var transacao = new Transaction(doc, "AutoEletrica — Etiquetar");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);
        if (tagEletroduto is not null)
            foreach (var c in new FilteredElementCollector(doc, vista.Id).OfClass(typeof(Conduit)).Cast<Conduit>())
            {
                if (jaEtiquetados.Contains(c.Id) || string.IsNullOrEmpty(ParametrosEletrica.LerTexto(c, ParametrosEletrica.Fiacao))) continue;
                if (c.Location is not LocationCurve lc || Unidades.Metros(lc.Curve.Length) < 0.6) continue;
                if (Criar(doc, tagEletroduto, vista, c, lc.Curve.Evaluate(0.5, true))) criadas++;
            }
        if (tagFio is not null)
            foreach (var w in new FilteredElementCollector(doc, vista.Id).OfClass(typeof(Wire)).Cast<Wire>())
            {
                if (jaEtiquetados.Contains(w.Id) || string.IsNullOrEmpty(ParametrosEletrica.LerTexto(w, ParametrosEletrica.Fiacao)) || w.NumberOfVertices < 2) continue;
                var meio = w.NumberOfVertices == 2 ? (w.GetVertex(0) + w.GetVertex(1)) / 2 : w.GetVertex(w.NumberOfVertices / 2);
                if (Criar(doc, tagFio, vista, w, meio)) criadas++;
            }
        foreach (var (categoria, tipo) in new[]
                 {
                     (BuiltInCategory.OST_ElectricalFixtures, tagTomada), (BuiltInCategory.OST_LightingFixtures, tagLuminaria),
                     (BuiltInCategory.OST_LightingDevices, tagInterruptor), (BuiltInCategory.OST_ElectricalEquipment, tagQuadro),
                 })
        {
            if (tipo is null) continue;
            foreach (var fi in new FilteredElementCollector(doc, vista.Id).OfCategory(categoria).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
            {
                if (jaEtiquetados.Contains(fi.Id) || (fi.Location as LocationPoint)?.Point is not { } p) continue;
                if (Criar(doc, tipo, vista, fi, p + new XYZ(Unidades.DeMetros(0.25), Unidades.DeMetros(0.25), 0))) criadas++;
            }
        }
        transacao.Commit();
        return new Feito(criadas, null);
    }

    /// <summary>Uma etiqueta; o Revit recusa algumas (elemento fora do recorte, família de etiqueta de outra categoria) e a recusa não derruba as outras.</summary>
    static bool Criar(Document doc, FamilySymbol tipo, View vista, Element elemento, XYZ ponto)
    {
        try
        {
            IndependentTag.Create(doc, tipo.Id, vista.Id, new Reference(elemento), false, TagOrientation.Horizontal, ponto);
            return true;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    static FamilySymbol? TipoDeEtiqueta(Document doc, BuiltInCategory categoria) =>
        new FilteredElementCollector(doc).OfCategory(categoria).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .OrderByDescending(s => (s.FamilyName + s.Name).Contains("AE", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
}
