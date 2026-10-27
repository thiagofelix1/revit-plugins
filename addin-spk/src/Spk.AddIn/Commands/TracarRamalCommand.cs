using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Modelagem;

namespace Spk.AddIn.Commands;

/// <summary>
/// Traça o ramal de uma fileira de bicos até a subgeral. Usa a seleção (bicos e um tubo); sem ela, pede os bicos e
/// depois a subgeral.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TracarRamalCommand : ExternalCommand
{
    const string Titulo = "SPK — Traçar ramal";

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc)
        {
            TaskDialog.Show(Titulo, "Abra um projeto antes.");
            return;
        }

        var selecionados = ui.Selection.GetElementIds().Select(doc.GetElement).ToList();
        var bicos = Bicos(selecionados);
        var tubos = selecionados.OfType<Pipe>().ToList();
        try
        {
            if (bicos.Count == 0)
                bicos = Bicos(ui.Selection.PickObjects(ObjectType.Element, new SoBicos(),
                    "Selecione os bicos da fileira e clique em Concluir").Select(r => doc.GetElement(r)).ToList());
            if (tubos.Count != 1)
                tubos = [(Pipe)doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new SoTubos(), "Clique na subgeral"))];
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return;
        }
        if (bicos.Count == 0)
        {
            TaskDialog.Show(Titulo, "Selecione pelo menos um bico.");
            return;
        }

        var resultado = TracarRamal.Executar(doc, bicos, tubos[0]);
        if (resultado.Ramal is { IsValidObject: true } ramal) ui.Selection.SetElementIds([ramal.Id]);
        TaskDialog.Show(Titulo, resultado.Texto() + (resultado.Recusa is null ? "\n\nCtrl+Z desfaz o ramal inteiro." : ""));
    }

    static List<FamilyInstance> Bicos(IEnumerable<Element?> elementos) =>
        elementos.OfType<FamilyInstance>().Where(e => e.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers).ToList();

    sealed class SoBicos : ISelectionFilter
    {
        public bool AllowElement(Element elemento) => elemento.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers;
        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }

    sealed class SoTubos : ISelectionFilter
    {
        public bool AllowElement(Element elemento) => elemento is Pipe;
        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }
}
