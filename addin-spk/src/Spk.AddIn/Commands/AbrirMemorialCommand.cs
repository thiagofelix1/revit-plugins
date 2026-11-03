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

        if (Context.ActiveDocument is { } doc && ResultadoGravado.Ler(doc) is { } calculo && Servicos.AssinaturaDaRede.Mudou(doc, calculo))
        {
            var pergunta = new TaskDialog("SPK — Memorial")
            {
                MainInstruction = "A rede mudou depois do último cálculo",
                MainContent = "A rede ou os Dados do projeto mudaram, e este memorial mostra o resultado antigo. Rode Calcular de novo para atualizá-lo.\n\nAbrir mesmo assim?",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.No,
            };
            if (pergunta.Show() != TaskDialogResult.Yes) return;
        }

        Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
    }
}
