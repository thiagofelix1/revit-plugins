using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Modelagem;

namespace Spk.AddIn.Commands;

/// <summary>Põe um bico no ponto do ramal clicado, igual ao bico mais perto da mesma rede, já ligado (<see cref="EditarBicos" />).</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class InserirBicoCommand : ExternalCommand
{
    const string Titulo = "SPK — Inserir bico";

    public override void Execute()
    {
        if (EdicaoUi.Documento(Application, Titulo) is not { } ui) return;
        var doc = ui.Document;
        var bicos = 0;
        var avisos = new List<string>();
        try
        {
            while (true)
            {
                var referencia = ui.Selection.PickObject(ObjectType.PointOnElement, new EdicaoUi.SoTubos(),
                    "Clique no ramal onde vai o bico novo (Esc para terminar)");
                if (doc.GetElement(referencia) is not Pipe ramal || ramal.Location is not LocationCurve { Curve: Line linha }) continue;
                var ponto = linha.Project(referencia.GlobalPoint).XYZPoint;
                var feito = EditarBicos.Inserir(doc, ramal, ponto);
                if (feito.Recusa is not null)
                {
                    TaskDialog.Show(Titulo, feito.Texto(""));
                    continue;
                }
                bicos++;
                avisos.AddRange(feito.Avisos);
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        if (bicos > 0)
            TaskDialog.Show(Titulo, $"{bicos} bico(s) inserido(s) e ligado(s)." + (avisos.Count > 0 ? "\n" + string.Join("\n", avisos.Select(a => "• " + a)) : "") +
                                    "\n\nDepois: Diâmetros, Verificar modelo e Calcular. Ctrl+Z desfaz cada bico.");
    }
}

/// <summary>Apaga bicos com o caminho só deles até a rede e refaz o encontro (<see cref="EditarBicos" />).</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ApagarBicoCommand : ExternalCommand
{
    const string Titulo = "SPK — Apagar bico";

    public override void Execute()
    {
        if (EdicaoUi.Documento(Application, Titulo) is not { } ui) return;
        var doc = ui.Document;
        var bicos = EdicaoUi.Bicos(ui.Selection.GetElementIds().Select(doc.GetElement));
        if (bicos.Count == 0)
        {
            try
            {
                bicos = EdicaoUi.Bicos(ui.Selection.PickObjects(ObjectType.Element, new EdicaoUi.SoBicos(),
                    "Selecione os bicos a apagar e clique em Concluir").Select(r => doc.GetElement(r)));
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }
        }
        var apagados = 0;
        var recusas = new List<string>();
        foreach (var bico in bicos)
        {
            var feito = EditarBicos.Apagar(doc, bico);
            if (feito.Recusa is null) apagados++;
            else recusas.Add(feito.Recusa);
        }
        TaskDialog.Show(Titulo, $"{apagados} de {bicos.Count} bico(s) apagado(s), com a descida e o braço, e a rede emendada." +
                                (recusas.Count > 0 ? "\n" + string.Join("\n", recusas.Distinct().Select(r => "• " + r)) : "") +
                                "\n\nDepois: Diâmetros, Verificar modelo e Calcular. Ctrl+Z desfaz cada bico.");
    }
}

/// <summary>Leva um bico para o ponto clicado, religado ao ramal mais perto da mesma rede (<see cref="EditarBicos" />).</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class MoverBicoCommand : ExternalCommand
{
    const string Titulo = "SPK — Mover bico";

    public override void Execute()
    {
        if (EdicaoUi.Documento(Application, Titulo) is not { } ui) return;
        var doc = ui.Document;
        try
        {
            var bicos = EdicaoUi.Bicos(ui.Selection.GetElementIds().Select(doc.GetElement));
            if (bicos.Count == 0)
                bicos = [(FamilyInstance)doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new EdicaoUi.SoBicos(), "Clique no bico a mover"))];
            if (bicos.Count == 1)
            {
                var ponto = ui.Selection.PickPoint("Clique no lugar novo do bico, em planta");
                var feito = EditarBicos.Mover(doc, bicos[0], ponto);
                if (feito.Bico is not null) ui.Selection.SetElementIds([feito.Bico]);
                TaskDialog.Show(Titulo, feito.Texto("Bico movido e religado ao ramal.") + "\n\nDepois: Diâmetros, Verificar modelo e Calcular. Ctrl+Z desfaz.");
                return;
            }

            // Vários: o mesmo deslocamento para todos, como o Mover do Revit (ponto de referência e destino).
            var de = ui.Selection.PickPoint($"{bicos.Count} bicos: clique no ponto de referência");
            var para = ui.Selection.PickPoint("Clique no destino do ponto de referência");
            var feitos = EditarBicos.MoverVarios(doc, bicos, para - de);
            var novos = feitos.Select(f => f.Bico).OfType<ElementId>().ToList();
            if (novos.Count > 0) ui.Selection.SetElementIds(novos);
            var recusas = feitos.Where(f => f.Recusa is not null).Select(f => f.Recusa!).Distinct().ToList();
            var avisos = feitos.SelectMany(f => f.Avisos).Distinct().ToList();
            TaskDialog.Show(Titulo, $"{novos.Count} de {bicos.Count} bico(s) movido(s) e religado(s) ({feitos.Count(f => f.Deslizou)} deslizaram na linha do ramal)." +
                                    string.Concat(recusas.Select(r => "\n• não movido: " + r)) + string.Concat(avisos.Select(a => "\n• " + a)) +
                                    "\n\nDepois: Diâmetros, Verificar modelo e Calcular. Ctrl+Z desfaz cada bico.");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            TaskDialog.Show(Titulo, "Abra uma planta para clicar o lugar novo do bico.");
        }
    }
}

static class EdicaoUi
{
    public static UIDocument? Documento(UIApplication app, string titulo)
    {
        if (app.ActiveUIDocument is { Document.IsFamilyDocument: false } ui) return ui;
        TaskDialog.Show(titulo, "Abra um projeto antes.");
        return null;
    }

    public static List<FamilyInstance> Bicos(IEnumerable<Element?> elementos) =>
        elementos.OfType<FamilyInstance>().Where(e => e.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers).ToList();

    public sealed class SoBicos : ISelectionFilter
    {
        public bool AllowElement(Element elemento) => elemento.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers;
        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }

    public sealed class SoTubos : ISelectionFilter
    {
        public bool AllowElement(Element elemento) => elemento is Pipe;
        public bool AllowReference(Reference referencia, XYZ ponto) => true;
    }
}
