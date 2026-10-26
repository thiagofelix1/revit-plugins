using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Spk.AddIn.Commands;

/// <summary>Abre no programa padrão (Word etc.) o último memorial.docx gerado pelo cálculo.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class AbrirMemorialCommand : ExternalCommand
{
    public override void Execute()
    {
        var caminho = Path.Combine(PastasSpk.Calculo, "memorial.docx");
        if (!File.Exists(caminho))
        {
            TaskDialog.Show("SPK", "Ainda não há memorial gerado. Rode \"Calcular\" primeiro.");
            return;
        }

        Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
    }
}
