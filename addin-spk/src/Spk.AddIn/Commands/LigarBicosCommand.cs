using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Modelagem;

namespace Spk.AddIn.Commands;

/// <summary>
/// Liga bicos soltos ao ramal. Usa o que já estiver selecionado (bicos e tubos); sem isso, pede para selecionar.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class LigarBicosCommand : ExternalCommand
{
    const string Titulo = "SPK — Ligar bicos";

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc)
        {
            TaskDialog.Show(Titulo, "Abra um projeto antes.");
            return;
        }

        var selecionados = ui.Selection.GetElementIds().Select(doc.GetElement).ToList();
        var (bicos, ramais) = Separar(selecionados);
        if (bicos.Count == 0 || ramais.Count == 0)
        {
            try
            {
                var escolhidos = ui.Selection.PickObjects(ObjectType.Element, new BicosOuTubos(),
                    "Selecione os bicos e o ramal (ou os ramais) e clique em Concluir");
                (bicos, ramais) = Separar(escolhidos.Select(r => doc.GetElement(r)).ToList());
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }
        }
        if (bicos.Count == 0 || ramais.Count == 0)
        {
            TaskDialog.Show(Titulo, "Selecione pelo menos um bico e um tubo de ramal.");
            return;
        }

        var resultado = LigarBicos.Executar(doc, bicos, ramais);
        if (resultado.Criados.Count > 0) ui.Selection.SetElementIds(resultado.Criados.ToList());
        TaskDialog.Show(Titulo, resultado.Texto() + "\n\nOs tubos criados ficam selecionados. Ctrl+Z desfaz a ligação inteira.");
    }

    static (List<FamilyInstance> Bicos, List<Pipe> Ramais) Separar(List<Element?> elementos) =>
        (elementos.OfType<FamilyInstance>().Where(e => e.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers).ToList(),
            elementos.OfType<Pipe>().ToList());

    sealed class BicosOuTubos : ISelectionFilter
    {
        public bool AllowElement(Element elemento) =>
            elemento is Pipe || elemento.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers;

        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }
}
