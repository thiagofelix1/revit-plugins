using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Spk.AddIn.Commands;

/// <summary>
/// Abre no programa padrão (Word etc.) o memorial do projeto aberto, do último setor calculado nele. Sem ele (cálculo
/// feito antes desta versão), oferece o último memorial gerado, que pode ser de outro projeto.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class AbrirMemorialCommand : ExternalCommand
{
    public override void Execute()
    {
        var ultimo = Path.Combine(PastasSpk.Calculo, "memorial.docx");
        var gravado = Context.ActiveDocument is { } aberto ? ResultadoGravado.Ler(aberto) : null;
        var caminho = Context.ActiveDocument is { } atual && gravado is not null ? PastasSpk.MemorialDo(atual, gravado.Setor) : ultimo;
        if (!File.Exists(caminho))
        {
            if (gravado is null || !File.Exists(ultimo))
            {
                TaskDialog.Show("SPK", "Ainda não há memorial deste projeto. Rode \"Calcular\" primeiro.");
                return;
            }
            var outro = new TaskDialog("SPK — Memorial")
            {
                MainInstruction = "O memorial deste projeto não está guardado",
                MainContent = $"O último cálculo deste projeto (setor {gravado.Setor}) foi feito antes de o SPK guardar um memorial por projeto. " +
                              "Rode Calcular de novo para gerá-lo.\n\nAbrir o último memorial gerado, que pode ser de outro projeto?",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.No,
            };
            if (outro.Show() != TaskDialogResult.Yes) return;
            caminho = ultimo;
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
