using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Servicos;

namespace Spk.AddIn.Commands;

/// <summary>
/// Lê os bicos (da malha gerada ou das famílias confirmadas), monta a rede ligada a eles e calcula
/// com os critérios de "Dados do projeto". Grava o resultado no modelo e o memorial em disco.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CalcularRedeCommand : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var doc = Application.ActiveUIDocument?.Document
                      ?? throw new InvalidOperationException("Abra um projeto antes de calcular.");

            var dados = DadosProjetoUi.Obter(doc);
            if (dados is null) return;

            if (!SetorUi.ModeloPronto(Application.ActiveUIDocument!, "SPK — Calcular", "Calcular mesmo assim")) return;

            var leitura = BicosDoModelo.Ler(doc);
            ServicoCalculo.ValidarFatorK(leitura.Bicos);

            var previa = new TaskDialog("SPK — Calcular")
            {
                MainInstruction = $"{leitura.Bicos.Count} bicos entram no cálculo",
                MainContent = $"{dados.Resumo()}\n{leitura.Resumo}\n\nPara mudar a classe ou a base, cancele e use Dados do projeto. " +
                              "O Revit fica ocupado até o fim; numa rede grande pode levar alguns minutos.",
                CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel,
            };
            if (previa.Show() != TaskDialogResult.Ok) return;

            var resultado = SetorUi.NoSetor(Application.ActiveUIDocument!, "SPK — Calcular", dados, leitura,
                (fonte, vgas, doSetor) => ServicoCalculo.Executar(doc, dados, leitura, fonte, vgas, doSetor));
            if (resultado is null) return;
            TaskDialog.Show("SPK — Calcular", resultado.Texto());
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            TaskDialog.Show("SPK — Calcular", e.Message);
        }
    }
}
