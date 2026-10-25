using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.Motor.Calculo;
using Spk.Motor.Relatorios;

namespace Spk.AddIn.Commands;

/// <summary>
/// Lê os bicos (da malha gerada ou das famílias confirmadas), monta a rede ligada a eles e calcula
/// com os critérios de "Dados do projeto". Grava o resultado no modelo e o memorial em disco.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CalcularRedeCommand : ExternalCommand
{
    /// <summary>Abaixo disso o número só pode ser K americano (gpm/psi½); o menor K métrico usual é 57.</summary>
    const double MenorKMetrico = 40;

    public override void Execute()
    {
        try
        {
            var doc = Application.ActiveUIDocument?.Document
                      ?? throw new InvalidOperationException("Abra um projeto antes de calcular.");

            var dados = DadosProjetoUi.Obter(doc);
            if (dados is null) return;

            var gerados = BicosGerados(doc);
            List<LeitorRede.BicoLido> bicos;
            string resumo;
            if (gerados.Count > 0)
            {
                var esboco = DadosEsboco.Carregar(doc);
                bicos = gerados.Select(fi => new LeitorRede.BicoLido(fi, esboco.FatorK, esboco.AreaM2)).ToList();
                resumo = $"Malha gerada: {bicos.Count} bicos, K {esboco.FatorK:0.##} L/min/bar½, área {esboco.AreaM2:0.##} m².";
            }
            else
            {
                var linhas = MapeamentoGravado.Carregar(doc).Where(l => l.EhChuveiroComMedidas).ToList();
                if (linhas.Count == 0)
                    throw new InvalidOperationException("Gere uma malha ou confirme em Famílias os bicos com fator K e área maiores que zero.");
                bicos = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Select(fi => (Instancia: fi, Linha: linhas.FirstOrDefault(l =>
                        fi.Symbol?.Family?.Name == l.Familia && fi.Symbol.Name == l.Tipo)))
                    .Where(p => p.Linha is not null)
                    .Select(p => new LeitorRede.BicoLido(p.Instancia, p.Linha!.FatorK, p.Linha.AreaCobertura))
                    .ToList();
                if (bicos.Count == 0)
                    throw new InvalidOperationException("O modelo aberto não tem as famílias confirmadas no mapeamento.");
                resumo = string.Join("\n", linhas.Select(l => $"{l.Tipo}: K {l.FatorK:0.##} L/min/bar½, área {l.AreaCobertura:0.##} m²"));
            }

            var kAmericano = bicos.Where(b => b.FatorK < MenorKMetrico).Select(b => b.FatorK).Distinct().ToList();
            if (kAmericano.Count > 0)
                throw new InvalidOperationException(
                    $"Fator K {string.Join(", ", kAmericano.Select(k => k.ToString("0.##")))} parece estar em gpm/psi½. " +
                    "O cálculo usa L/min/bar½ (K 5,6 → 80; K 25,2 → 360). Corrija em Famílias ou em Dados do esboço.");

            var previa = new TaskDialog("SPK — Calcular")
            {
                MainInstruction = $"{bicos.Count} bicos entram no cálculo",
                MainContent = $"{dados.Resumo()}\n{resumo}\n\nPara mudar a classe ou a base, cancele e use Dados do projeto. " +
                              "O Revit fica ocupado até o fim; numa rede grande pode levar alguns minutos.",
                CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel,
            };
            if (previa.Show() != TaskDialogResult.Ok) return;

            var vgas = gerados.Count > 0 ? [] : MapeamentoGravado.Vgas(doc);
            var fonte = vgas.Count switch
            {
                0 => null,
                1 => vgas[0],
                _ => EscolherVga(Application.ActiveUIDocument!, vgas),
            };
            var lida = LeitorRede.Montar(doc, bicos, dados.FatorC, fonte, vgas.Select(v => v.Id).ToList());
            var projeto = dados.CriarProjeto(gerados.Count > 0 ? "Malha gerada" : "Rede lida do modelo", lida.Rede);
            var relatorio = CalculoSpk.Executar(projeto);

            var pasta = PastasSpk.Calculo;
            Directory.CreateDirectory(pasta);
            File.WriteAllText(Path.Combine(pasta, "memorial.md"), Memorial.Gerar(relatorio));
            File.WriteAllBytes(Path.Combine(pasta, "memorial.docx"), MemorialDocx.Gerar(relatorio));

            var gravado = new ResultadoGravado
            {
                Atende = relatorio.Atende,
                PressaoNecessariaKPa = relatorio.Resultado.PressaoNecessaria,
                VazaoChuveirosLmin = relatorio.Resultado.VazaoChuveiros,
                ChuveiroCritico = relatorio.Resultado.ChuveiroCritico,
                Bicos = bicos.Count,
                Tubos = lida.Tubos,
                BicosDaRede = lida.Rede.Chuveiros.Select(c => c.Id).ToList(),
                Chuveiros = relatorio.Resultado.Chuveiros.Select(c => new BicoGravado
                {
                    Id = c.Id,
                    Ramal = c.Ramal,
                    PressaoKPa = c.Pressao,
                    VazaoLmin = c.Vazao,
                }).ToList(),
            };
            ArmazenamentoSpk.GravarComTransacao(doc, ResultadoGravado.Chave, gravado, "SPK — Resultado do cálculo");

            var critico = relatorio.Resultado.Chuveiros.First(c => c.Id == relatorio.Resultado.ChuveiroCritico);
            var falhas = relatorio.Verificacoes.Where(v => !v.Atende).Select(v => $"• {v.Descricao} ({v.Item}): {v.Detalhe}").ToList();
            TaskDialog.Show("SPK — Calcular", $"""
                {(relatorio.Atende ? "ATENDE" : "NÃO ATENDE")}
                {(falhas.Count > 0 ? string.Join("\n", falhas) + "\n" : "")}
                Bicos: {bicos.Count}    Tubos: {lida.Tubos}    Áreas avaliadas: {relatorio.Busca?.CandidatosAvaliados ?? 1}
                Pressão na fonte: {relatorio.Resultado.PressaoNecessaria:0.0} kPa ({relatorio.Resultado.PressaoNecessaria / 100:0.00} bar)
                Vazão dos bicos: {relatorio.Resultado.VazaoChuveiros:0} L/min
                Bico crítico: {critico.Id} a {critico.Pressao / 100:0.00} bar ({critico.Vazao:0} L/min)

                {string.Join("\n", relatorio.Avisos.Concat(lida.Avisos))}

                Memorial em:
                {pasta}
                """);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            TaskDialog.Show("SPK — Calcular", e.Message);
        }
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

    static List<FamilyInstance> BicosGerados(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(fi =>
                fi.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() == DadosEsboco.Marca
                || fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() == DadosEsboco.Marca)
            .ToList();
}
