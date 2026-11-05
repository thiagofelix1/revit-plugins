using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Modelagem;
using Spk.AddIn.Servicos;
using Spk.Motor.Calculo;
using Spk.Motor.Normas;

namespace Spk.AddIn.Commands;

/// <summary>
/// Diâmetros por tabela: lê a rede do setor como o Calcular, conta os bicos a jusante de cada trecho, mostra o que vai
/// mudar e põe nos tubos o DN da tabela. No fim, oferece calcular para conferir.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DiametrosCommand : ExternalCommand
{
    const string Titulo = "SPK — Diâmetros";

    sealed record Plano(LeitorRede.RedeLida Lida, DiametrosPorTabela Dimensionamento);

    public override void Execute()
    {
        try
        {
            var ui = Application.ActiveUIDocument;
            if (ui?.Document is not { IsFamilyDocument: false } doc)
            {
                TaskDialog.Show(Titulo, "Abra um projeto antes.");
                return;
            }

            var dados = DadosProjetoUi.Obter(doc);
            if (dados is null) return;
            if (!TabelaDeDiametros.TemTabela(dados.Classe))
            {
                TaskDialog.Show(Titulo, $"{DadosProjeto.NomeClasse(dados.Classe)}: a norma não tem tabela de diâmetros para essa classe. " +
                                        "O dimensionamento é só pelo cálculo hidráulico (Calcular).");
                return;
            }
            if (!SetorUi.ModeloPronto(ui, Titulo, "Seguir mesmo assim")) return;

            var leitura = BicosDoModelo.Ler(doc);
            ServicoCalculo.ValidarFatorK(leitura.Bicos);
            var plano = SetorUi.NoSetor(ui, Titulo, dados, leitura, (fonte, vgas, doSetor) =>
            {
                var lida = LeitorRede.Montar(doc, leitura.Bicos, dados.FatorC, fonte, vgas, doSetor);
                return new Plano(lida, DimensionamentoPorTabela.Calcular(lida.Rede, dados.Classe));
            });
            if (plano is null) return;

            var pedido = AplicarDiametros.Pedido(plano.Lida, plano.Dimensionamento);
            if (pedido.Count == 0)
            {
                TaskDialog.Show(Titulo, "Nenhum tubo do modelo nesse setor para dimensionar.");
                return;
            }
            var pontas = AplicarDiametros.PontasForaDaLinha(doc, plano.Lida, pedido);
            var previa = new TaskDialog(Titulo)
            {
                MainInstruction = $"Diâmetros por tabela em {pedido.Count} tubos ({plano.Lida.Setor})",
                MainContent = $"{DadosProjeto.NomeClasse(dados.Classe)}, {plano.Lida.Rede.Chuveiros.Count} bicos, " +
                              $"{plano.Dimensionamento.AreaDoSistema:0} m². Fonte: {TabelaDeDiametros.Fonte}.\n" +
                              "Pela tabela: " + string.Join(", ", pedido.Values.GroupBy(d => d).OrderBy(g => g.Key)
                                  .Select(g => $"DN {g.Key}: {g.Count()}")) +
                              (plano.Dimensionamento.Avisos.Count > 0 ? "\n\n" + string.Join("\n", plano.Dimensionamento.Avisos.Select(a => "• " + a)) : "") +
                              "\n\nO DN vira o menor tamanho do tubo que não fica abaixo da tabela. Ctrl+Z desfaz tudo." +
                              (pontas.Count > 0
                                  ? $"\n\n{pontas.Count} ponta(s) sem água depois da última derivação ficariam em DN diferente da linha. A tabela não dá DN a elas. " +
                                    "Uma ponta mais larga deixa a peça ao lado maior que a linha, e a pressão calculada pode ficar menor que a real. " +
                                    "Mantenha a ponta só se ela for de uma ampliação prevista."
                                  : ""),
                CommonButtons = pontas.Count > 0 ? TaskDialogCommonButtons.Cancel : TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel,
            };
            if (pontas.Count > 0)
            {
                previa.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Aplicar e ajustar as pontas", "Cada ponta fica no DN do tubo em linha com ela.");
                previa.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Aplicar sem mexer nas pontas", "O Calcular vai avisar se alguma ponta ficar mais larga que a linha.");
            }
            var resposta = previa.Show();
            if (resposta is not (TaskDialogResult.Ok or TaskDialogResult.CommandLink1 or TaskDialogResult.CommandLink2)) return;

            var resultado = AplicarDiametros.Executar(doc, plano.Lida, plano.Dimensionamento, ajustarPontas: resposta == TaskDialogResult.CommandLink1);
            if (resultado.Alterados.Count > 0) ui.Selection.SetElementIds(resultado.Alterados.ToList());

            var fim = new TaskDialog(Titulo)
            {
                MainInstruction = "Diâmetros aplicados",
                MainContent = resultado.Texto() + "\n\nOs tubos alterados ficam selecionados. A tabela é o ponto de partida: " +
                              "o cálculo confere se a pressão no bico mais desfavorável atende.",
                CommonButtons = TaskDialogCommonButtons.Close,
            };
            fim.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Calcular agora para conferir");
            if (fim.Show() != TaskDialogResult.CommandLink1) return;

            var mensagem = "";
            if (new CalcularRedeCommand().Execute(ExternalCommandData, ref mensagem, new ElementSet()) == Result.Failed && mensagem.Length > 0)
                TaskDialog.Show("SPK — Calcular", mensagem);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            TaskDialog.Show(Titulo, e.Message);
        }
    }
}
