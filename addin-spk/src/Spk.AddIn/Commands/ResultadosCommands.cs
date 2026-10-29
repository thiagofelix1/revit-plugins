using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Folhas;
using Spk.AddIn.Modelagem;

namespace Spk.AddIn.Commands;

/// <summary>Pinta a rede da vista aberta pelo critério escolhido (<see cref="CoresDaRede" />).</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CoresDaRedeCommand : ExternalCommand
{
    const string Titulo = "SPK — Cores da rede";

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc || ui.ActiveView is not { } vista)
        {
            TaskDialog.Show(Titulo, "Abra um projeto antes.");
            return;
        }
        var pergunta = new TaskDialog(Titulo)
        {
            MainInstruction = "Pintar a rede desta vista por:",
            MainContent = "A cor vale só nesta vista e não muda o modelo; Tirar cores volta ao normal. Velocidade e pressão vêm do " +
                          "último cálculo de cada setor.",
            CommonButtons = TaskDialogCommonButtons.Cancel,
        };
        pergunta.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Diâmetro", "Cada DN numa cor fixa: DN 25 azul, 32 verde, 40 amarelo, 50 laranja, 65 vermelho...");
        pergunta.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Setor", "As mesmas cores da folha de setorização");
        pergunta.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Velocidade nos tubos", "Faixas de 1 m/s, do azul-claro ao vinho");
        pergunta.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Pressão nos bicos", "Cinco faixas entre a menor e a maior pressão");
        var criterio = pergunta.Show() switch
        {
            TaskDialogResult.CommandLink1 => CriterioDeCor.Diametro,
            TaskDialogResult.CommandLink2 => CriterioDeCor.Setor,
            TaskDialogResult.CommandLink3 => CriterioDeCor.Velocidade,
            TaskDialogResult.CommandLink4 => CriterioDeCor.Pressao,
            _ => (CriterioDeCor?)null,
        };
        if (criterio is null) return;
        try
        {
            var feito = CoresDaRede.Pintar(doc, vista, criterio.Value);
            TaskDialog.Show(Titulo, feito.Texto() + (feito.Legenda ? "\n\nA legenda está ao lado da rede, na planta." : ""));
        }
        catch (InvalidOperationException e)
        {
            TaskDialog.Show(Titulo, e.Message);
        }
    }
}

/// <summary>Volta a cor normal do que Cores da rede pintou na vista aberta.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TirarCoresCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc || ui.ActiveView is not { } vista) return;
        using var t = new Transaction(doc, "SPK — Tirar cores");
        t.Start();
        CoresDaRede.Limpar(doc, vista);
        t.Commit();
    }
}

/// <summary>Cria as tabelas de material da rede e mostra o resumo (<see cref="ListaDeMaterial" />).</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ListaDeMaterialCommand : ExternalCommand
{
    const string Titulo = "SPK — Lista de material";

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc)
        {
            TaskDialog.Show(Titulo, "Abra um projeto antes.");
            return;
        }
        var resumo = ListaDeMaterial.Gerar(doc);
        var dialogo = new TaskDialog(Titulo)
        {
            MainInstruction = $"{resumo.Tubos.Sum(t => t.Metros):0.0} m de tubo, {resumo.Conexoes.Sum(c => c.Quantidade)} conexões e " +
                              $"{resumo.Bicos.Sum(b => b.Quantidade)} bicos",
            MainContent = resumo.Texto() + "\n\n" + (resumo.TabelasCriadas.Count > 0
                ? $"Tabelas criadas no projeto: {string.Join(", ", resumo.TabelasCriadas)}. Elas se atualizam sozinhas e vão para uma folha como qualquer tabela."
                : "As tabelas SPK — ... já existiam e se atualizam sozinhas."),
            CommonButtons = TaskDialogCommonButtons.Close,
        };
        dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Abrir a tabela de tubos");
        if (dialogo.Show() == TaskDialogResult.CommandLink1 && ListaDeMaterial.TabelaDeTubos(doc) is { } tabela)
            ui.ActiveView = tabela;
    }
}
