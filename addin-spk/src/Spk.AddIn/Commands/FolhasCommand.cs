using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Folhas;
using Spk.AddIn.Servicos;

namespace Spk.AddIn.Commands;

/// <summary>
/// Monta as folhas que o modelo permite e exporta o PDF. Cada conjunto roda em subtransação: se um falha, os outros ficam
/// e o motivo aparece no fim.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class FolhasCommand : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var ui = Application.ActiveUIDocument ?? throw new InvalidOperationException("Abra um projeto antes de gerar as folhas.");
            SairDasVistasDoPlugin(ui);
            var resultado = ServicoFolhas.Gerar(ui.Document);
            if (resultado.Folhas.Count > 0 && ui.Document.GetElement(resultado.Folhas[0]) is ViewSheet primeira)
                ui.ActiveView = primeira;
            TaskDialog.Show("SPK — Folhas", resultado.Texto());
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            TaskDialog.Show("SPK — Folhas", e.Message);
        }
    }

    /// <summary>O Revit não apaga a vista ativa. As vistas e folhas do plugin são recriadas, então sai delas antes.</summary>
    static void SairDasVistasDoPlugin(UIDocument ui)
    {
        if (!DoPlugin(ui.ActiveView)) return;
        var outra = new FilteredElementCollector(ui.Document).OfClass(typeof(View)).Cast<View>()
                        .FirstOrDefault(v => !v.IsTemplate && !DoPlugin(v)
                                             && v.ViewType is ViewType.FloorPlan or ViewType.ThreeD or ViewType.CeilingPlan or ViewType.Section)
                    ?? throw new InvalidOperationException("Abra uma vista que não seja do SPK (uma planta qualquer) e rode Folhas de novo.");
        ui.ActiveView = outra;
    }

    static bool DoPlugin(View? vista) => vista switch
    {
        ViewSheet folha => CarimboSpk.EhDoSpk(folha.Document, folha),
        null => false,
        _ => vista.Name.StartsWith(PranchaSpk.PrefixoVista, StringComparison.Ordinal),
    };
}
