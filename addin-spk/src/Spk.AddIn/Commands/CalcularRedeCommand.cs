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

            var vgas = leitura.Gerados ? [] : MapeamentoGravado.Vgas(doc);
            var fonte = vgas.Count switch
            {
                0 => null,
                1 => vgas[0],
                _ => EscolherVga(Application.ActiveUIDocument!, vgas),
            };
            ElementId? doSetor = null;
            if (!leitura.Gerados && vgas.Count == 0)
            {
                var (seguir, escolhido) = EscolherSetor(Application.ActiveUIDocument!);
                if (!seguir) return;
                doSetor = escolhido;
            }

            ServicoCalculo.Resultado resultado;
            try
            {
                resultado = ServicoCalculo.Executar(doc, dados, leitura, fonte, vgas.Select(v => v.Id).ToList(), doSetor);
            }
            catch (VgaDesligadaException e)
            {
                // A válvula não alcança a rede (vão no modelo): calcula pelo pedaço de rede do setor, se o engenheiro quiser.
                var (seguir, escolhido) = EscolherSetor(Application.ActiveUIDocument!, e.Message);
                if (!seguir) return;
                resultado = ServicoCalculo.Executar(doc, dados, leitura, null, [], escolhido);
            }
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

    /// <summary>
    /// Sem VGA no modelo, cada pedaço de rede é um setor. O engenheiro clica num bico ou tubo do que quer calcular, ou
    /// fica com o de mais bicos (elemento nulo). Cancelar devolve <c>Seguir = false</c>.
    /// </summary>
    static (bool Seguir, ElementId? Elemento) EscolherSetor(UIDocument ui, string? motivo = null)
    {
        var dialogo = new TaskDialog("SPK — Calcular")
        {
            MainInstruction = "Qual setor calcular?",
            MainContent = (motivo ?? "O modelo não tem VGA marcada em Famílias.") +
                          " Cada rede sem ligação com as outras é tratada como um setor, com a entrada na ponta mais baixa da descida.",
            CommonButtons = TaskDialogCommonButtons.Cancel,
        };
        dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Clicar num bico ou tubo do setor");
        dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Calcular o setor com mais bicos");
        switch (dialogo.Show())
        {
            case TaskDialogResult.CommandLink1:
                var referencia = ui.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, new BicoOuTubo(),
                    "Clique num bico ou tubo do setor que quer calcular.");
                return (true, referencia.ElementId);
            case TaskDialogResult.CommandLink2:
                return (true, null);
            default:
                return (false, null);
        }
    }

    sealed class BicoOuTubo : Autodesk.Revit.UI.Selection.ISelectionFilter
    {
        public bool AllowElement(Element elemento) => elemento.Category?.Id.Value is long id
            && (id == (long)BuiltInCategory.OST_Sprinklers || id == (long)BuiltInCategory.OST_PipeCurves);

        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }

    static FamilyInstance EscolherVga(UIDocument ui, List<FamilyInstance> vgas)
    {
        var referencia = ui.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element,
            new SoEstes(vgas.Select(v => v.Id).ToHashSet()), $"Há {vgas.Count} VGAs. Clique na VGA do setor que quer calcular.");
        return (FamilyInstance)ui.Document.GetElement(referencia);
    }

    sealed class SoEstes(HashSet<ElementId> ids) : Autodesk.Revit.UI.Selection.ISelectionFilter
    {
        public bool AllowElement(Element elemento) => ids.Contains(elemento.Id);
        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }
}
