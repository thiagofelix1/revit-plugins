using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Spk.AddIn.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DadosEsbocoCommand : ExternalCommand
{
    public override void Execute()
    {
        var janela = new DadosEsbocoJanela(DadosEsboco.Carregar());
        janela.ShowDialog();
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class GerarMalhaCommand : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var ui = Application.ActiveUIDocument ?? throw new InvalidOperationException("Abra um projeto antes de gerar.");
            var dados = DadosEsboco.Carregar();
            var janela = new DadosEsbocoJanela(dados);
            if (janela.ShowDialog() != true || janela.Resultado is not { } confirmado) return;

            var origem = ui.Selection.PickPoint("Clique a origem da malha, no primeiro bico.");
            using var transacao = new Transaction(ui.Document, "SPK — Gerar malha");
            transacao.Start();
            GeradorMalha.ResultadoGeracao gerado;
            try
            {
                gerado = GeradorMalha.Gerar(ui.Document, origem, confirmado);
                transacao.Commit();
            }
            catch
            {
                if (transacao.GetStatus() == TransactionStatus.Started) transacao.RollBack();
                throw;
            }
            TaskDialog.Show("SPK — Gerar malha", gerado.Resumo + "\n\nAgora use Calcular.");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            TaskDialog.Show("SPK — Gerar malha", e.Message);
        }
    }
}
