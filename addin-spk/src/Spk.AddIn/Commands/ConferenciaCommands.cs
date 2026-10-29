using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Modelagem;

namespace Spk.AddIn.Commands;

/// <summary>Desenha a conferência da norma na planta aberta (<see cref="ConferenciaVisual" />).</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ConferirNaPlantaCommand : ExternalCommand
{
    const string Titulo = "SPK — Conferir na planta";

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc)
        {
            TaskDialog.Show(Titulo, "Abra um projeto antes.");
            return;
        }
        if (ui.ActiveView is not ViewPlan { GenLevel: not null } planta)
        {
            TaskDialog.Show(Titulo, "Abra a planta do andar que quer conferir.");
            return;
        }
        var dados = DadosProjetoUi.Obter(doc);
        if (dados is null) return;
        try
        {
            var resultado = ConferenciaVisual.Executar(doc, planta, dados, new OpcoesBicos());
            new TaskDialog(Titulo)
            {
                MainInstruction = resultado.BicosFora + resultado.PontosDescobertos == 0
                    ? $"{resultado.Bicos} bico(s) conferidos: tudo dentro da regra"
                    : $"{resultado.BicosFora} bico(s) fora da regra e {resultado.PontosDescobertos} trecho(s) sem cobertura",
                MainContent = resultado.Texto() + "\n\nCírculo cinza: alcance de cada bico. Círculo vermelho: bico fora da regra ou trecho sem " +
                              "cobertura. Se os bicos não aparecem, a planta corta abaixo deles: use a planta de forro ou suba o corte do " +
                              "intervalo de vista. Corrija com Mover, Inserir ou Apagar bico e rode de novo; Limpar conferência apaga as marcas." +
                              (CoresDaRede.FaseDiferente(doc, planta) is { } fase ? "\n\nAtenção:" + fase : ""),
            }.Show();
        }
        catch (InvalidOperationException e)
        {
            TaskDialog.Show(Titulo, e.Message);
        }
    }
}

/// <summary>Apaga as marcas da conferência da vista aberta.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class LimparConferenciaCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc || ui.ActiveView is not { } vista) return;
        using var t = new Transaction(doc, "SPK — Limpar conferência");
        t.Start();
        ConferenciaVisual.Limpar(doc, vista);
        t.Commit();
    }
}
