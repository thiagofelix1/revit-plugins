using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Arquitetura;

namespace Spk.AddIn.Commands;

/// <summary>
/// Lê a arquitetura e a estrutura (do arquivo e dos vínculos) e mostra o que o SPK enxerga: ambientes com pé-direito e
/// o que forma o teto de cada um, e as vigas. Não muda nada no modelo.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ArquiteturaCommand : ExternalCommand
{
    public override void Execute()
    {
        if (Application.ActiveUIDocument?.Document is not { IsFamilyDocument: false } doc)
        {
            TaskDialog.Show("SPK — Arquitetura", "Abra um projeto antes.");
            return;
        }
        new ArquiteturaJanela(LeituraDoEdificio.Ler(doc)).ShowDialog();
    }
}
