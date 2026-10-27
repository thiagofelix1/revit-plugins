using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Modelagem;

namespace Spk.AddIn.Commands;

/// <summary>
/// Traça os ramais de várias fileiras de bicos até a subgeral. Usa a seleção (bicos e um tubo); sem ela, pede os bicos
/// (dá para arrastar uma janela) e depois a subgeral. As opções da última vez ficam para a próxima.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TracarRamalCommand : ExternalCommand
{
    const string Titulo = "SPK — Traçar ramais";

    static OpcoesRamais ultimas = new();

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
                    "Selecione os bicos (pode arrastar uma janela) e clique em Concluir").Select(r => doc.GetElement(r)).ToList());
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

        var subgeral = tubos[0];
        var cota = UnitUtils.ConvertFromInternalUnits(((LocationCurve)subgeral.Location).Curve.GetEndPoint(0).Z - subgeral.ReferenceLevel.Elevation,
            UnitTypeId.Meters);
        var dn = UnitUtils.ConvertFromInternalUnits(subgeral.Diameter, UnitTypeId.Millimeters);
        var janela = new TracarRamaisJanela(
            $"{bicos.Count} bicos e a subgeral DN {dn:0}, a {cota:0.00} m do nível. O SPK separa as fileiras e traça um ramal " +
            "por fileira, cruzando a subgeral, até o último bico. O DN do ramal é o de Dados do esboço; depois, use Diâmetros.",
            ultimas);
        if (janela.ShowDialog() != true || janela.Resultado is not { } opcoes) return;
        ultimas = opcoes;

        var resultado = TracarRamal.Executar(doc, bicos, subgeral, opcoes);
        var criados = resultado.Criados.Where(id => doc.GetElement(id) is not null).ToList();
        if (criados.Count > 0) ui.Selection.SetElementIds(criados);
        TaskDialog.Show(Titulo, resultado.Texto() + (resultado.Tracados > 0 ? "\n\nO que foi criado fica selecionado. Ctrl+Z desfaz tudo." : ""));
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
