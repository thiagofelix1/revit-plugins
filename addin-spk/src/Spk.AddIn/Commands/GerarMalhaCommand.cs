using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Spk.AddIn.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DadosEsbocoCommand : ExternalCommand
{
    public override void Execute()
    {
        var doc = Application.ActiveUIDocument?.Document;
        if (doc is null)
        {
            TaskDialog.Show("SPK — Dados do esboço", "Abra um projeto antes.");
            return;
        }

        var janela = new DadosEsbocoJanela(DadosEsboco.Carregar(doc));
        if (janela.ShowDialog() == true && janela.Resultado is { } dados)
            ArmazenamentoSpk.GravarComTransacao(doc, DadosEsboco.Chave, dados, "SPK — Dados do esboço");
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
            var janela = new DadosEsbocoJanela(DadosEsboco.Carregar(ui.Document));
            if (janela.ShowDialog() != true || janela.Resultado is not { } confirmado) return;

            var origem = ui.Selection.PickPoint("Clique a origem da malha, no primeiro bico.");
            using var transacao = new Transaction(ui.Document, "SPK — Gerar malha");
            transacao.Start();
            GeradorMalha.ResultadoGeracao gerado;
            try
            {
                confirmado.Salvar(ui.Document);
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
