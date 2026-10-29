using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Verificacao;

namespace Spk.AddIn.Commands;

/// <summary>Confere o modelo antes de calcular e mostra, item a item, o que impede a leitura da rede.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class VerificarModeloCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc)
        {
            TaskDialog.Show("SPK — Verificar modelo", "Abra um projeto antes.");
            return;
        }
        VerificacaoUi.Mostrar(ui, VerificacaoModelo.Verificar(doc));
    }
}

static class VerificacaoUi
{
    /// <summary>
    /// Abre a janela; se o engenheiro escolher um item, seleciona os elementos e leva a vista até eles. Se pedir para
    /// corrigir um grupo, corrige, mostra o que foi feito e abre a verificação de novo.
    /// </summary>
    public static void Mostrar(UIDocument ui, ResultadoVerificacao resultado)
    {
        var janela = new VerificacaoJanela(resultado);
        if (janela.ShowDialog() != true) return;
        if (janela.ParaCorrigir is { } grupo)
        {
            var feito = grupo.Codigo == "derivacao-sem-conexao"
                ? Derivacoes.ExecutarAteParar(ui.Document, grupo.Ocorrencias)
                : Reconexao.Executar(ui.Document, grupo.Ocorrencias);
            TaskDialog.Show("SPK — Corrigir", feito.Texto() + "\n\nCtrl+Z desfaz a correção inteira.");
            Mostrar(ui, VerificacaoModelo.Verificar(ui.Document));
            return;
        }
        if (janela.Escolhidos is not { Count: > 0 } escolhidos) return;
        var existentes = escolhidos.Where(id => ui.Document.GetElement(id) is not null).ToList();
        ui.Selection.SetElementIds(existentes);
        try
        {
            ui.ShowElements(existentes);
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            // Nenhuma vista aberta mostra esses elementos: ficam só selecionados.
        }
    }
}
