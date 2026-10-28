using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Modelagem;

namespace Spk.AddIn.Commands;

/// <summary>
/// Liga as redes soltas das salas à geral que o engenheiro desenhou (<see cref="LigarAGeral" />). Usa os tubos
/// selecionados como geral; sem seleção, pede para clicar neles. Antes de ligar, diz quantas redes achou.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class LigarAGeralCommand : ExternalCommand
{
    const string Titulo = "SPK — Ligar à geral";

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc)
        {
            TaskDialog.Show(Titulo, "Abra um projeto antes.");
            return;
        }

        var geral = ui.Selection.GetElementIds().Select(doc.GetElement).OfType<Pipe>().ToList();
        if (geral.Count == 0)
        {
            try
            {
                geral = ui.Selection.PickObjects(ObjectType.Element, new SoTubos(),
                        "Selecione os tubos da geral (desenhe antes com a ferramenta Tubo, da VGA pelo corredor) e clique em Concluir")
                    .Select(r => doc.GetElement(r)).OfType<Pipe>().ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }
        }
        if (geral.Count == 0)
        {
            TaskDialog.Show(Titulo, "Selecione pelo menos um tubo da geral.");
            return;
        }

        var redes = LigarAGeral.RedesSoltas(doc, geral);
        if (redes.Count == 0)
        {
            TaskDialog.Show(Titulo, "Nenhuma rede solta perto da geral: as redes com bico já estão ligadas a ela, ou ficam a mais de 3 m de altura dela.");
            return;
        }
        var bicos = redes.Sum(r => r.Count(id => doc.GetElement(id)?.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers));
        var pergunta = new TaskDialog(Titulo)
        {
            MainInstruction = $"Ligar {redes.Count} rede(s) solta(s) ({bicos} bicos) à geral?",
            MainContent = "Cada rede perde a descida até o piso (a entrada dela) e ganha um tubo até a geral, com um tê na geral. " +
                          "Depois: Diâmetros, Verificar modelo e Calcular, com a entrada na ponta da geral ou na VGA.",
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
        };
        if (pergunta.Show() != TaskDialogResult.Yes) return;

        var resultado = LigarAGeral.Executar(doc, geral, redes);
        if (resultado.Criados.Count > 0) ui.Selection.SetElementIds(resultado.Criados.ToList());
        TaskDialog.Show(Titulo, resultado.Texto() + "\n\nO que foi criado fica selecionado. Ctrl+Z desfaz tudo.");
    }

    sealed class SoTubos : ISelectionFilter
    {
        public bool AllowElement(Element elemento) => elemento is Pipe;
        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }
}
