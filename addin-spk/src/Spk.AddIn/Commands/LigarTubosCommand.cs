using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Modelagem;

namespace Spk.AddIn.Commands;

/// <summary>
/// Liga tubos aos pares (<see cref="LigarTubos" />): o tubo que chega e o de destino, vários pares seguidos; Esc termina.
/// Recusa aparece na hora, com o motivo; o par seguinte continua.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class LigarTubosCommand : ExternalCommand
{
    const string Titulo = "SPK — Ligar tubos";

    public override void Execute()
    {
        if (EdicaoUi.Documento(Application, Titulo) is not { } ui) return;
        var doc = ui.Document;
        var pecas = new List<string>();
        try
        {
            while (true)
            {
                var chega = (Pipe)doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new SoTubos(),
                    "Clique no tubo que chega (o da ponta solta) (Esc para terminar)"));
                var alvo = (Pipe)doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new SoTubos(),
                    "Clique no tubo onde ele vai ligar"));
                var feito = LigarTubos.Executar(doc, chega, alvo);
                if (feito.Recusa is not null)
                {
                    TaskDialog.Show(Titulo, "Não ligou: " + feito.Recusa + ".");
                    continue;
                }
                pecas.Add(feito.Peca!);
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        if (pecas.Count > 0)
            TaskDialog.Show(Titulo, $"{pecas.Count} ligação(ões): " +
                                    string.Join(", ", pecas.GroupBy(p => p).Select(g => $"{g.Count()} {g.Key}")) +
                                    ".\n\nDepois: Verificar modelo, Diâmetros e Calcular. Ctrl+Z desfaz cada ligação.");
    }

    sealed class SoTubos : ISelectionFilter
    {
        public bool AllowElement(Element elemento) => elemento is Pipe;
        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }
}
