using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.Motor.Calculo;
using Spk.Motor.Normas;
using Spk.Motor.Relatorios;

namespace Spk.AddIn.Commands;

/// <summary>
/// Lê os bicos confirmados que têm fator K e área, monta a rede ligada a eles e calcula.
/// Nesta validação isso seleciona o ESFR K 360 / 12 m² e deixa de fora K 5,6 com área 0 e anotações.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class CalcularRedeCommand : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var doc = Application.ActiveUIDocument?.Document
                      ?? throw new InvalidOperationException("Abra um projeto antes de calcular.");

            var gerados = BicosGerados(doc);
            List<LeitorRede.BicoLido> bicos;
            string resumo;
            if (gerados.Count > 0)
            {
                var dados = DadosEsboco.Carregar();
                bicos = gerados.Select(fi => new LeitorRede.BicoLido(fi, dados.FatorK, dados.AreaM2, dados.PressaoMinimaKPa)).ToList();
                resumo = $"Malha gerada: {bicos.Count} bicos, K {dados.FatorK:0.##} L/min/bar½, área {dados.AreaM2:0.##} m².";
            }
            else
            {
                var linhas = MapeamentoGravado.Carregar().Where(l => l.EhChuveiroComMedidas).ToList();
                if (linhas.Count == 0)
                    throw new InvalidOperationException("Gere uma malha ou confirme famílias com fator K e área maiores que zero.");
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
                resumo = string.Join("\n", linhas.Select(l => $"{l.Tipo}: K {l.FatorK:0.##}, área {l.AreaCobertura:0.##} m²"));
            }

            var previa = new TaskDialog("SPK — Calcular")
            {
                MainInstruction = $"{bicos.Count} bicos entram no cálculo",
                MainContent = resumo + "\n\nO Revit fica ocupado até o fim. Numa rede grande pode levar alguns minutos.",
                CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel,
            };
            if (previa.Show() != TaskDialogResult.Ok) return;

            var lida = LeitorRede.Montar(doc, bicos);
            var esfr = bicos.All(b => b.FatorK >= 200);
            var pressaoMinima = bicos.Select(b => b.PressaoMinima).FirstOrDefault(p => p is > 0) ?? 283;
            var projeto = new ProjetoSpk(
                gerados.Count > 0 ? "Malha gerada" : "Rede lida do modelo",
                lida.Rede,
                esfr ? ClasseRisco.Armazenamento : ClasseRisco.Ordinario2,
                esfr
                    ? BaseDeProjeto.Esfr(pressaoMinima, "esboço ou mapeamento confirmado")
                    : BaseDeProjeto.PelaFigura42(ClasseRisco.Ordinario2, 140))
            {
                VazaoHidrantes = esfr ? 0 : null,
                DuracaoMinutos = esfr ? 60 : null,
            };

            var relatorio = CalculoSpk.Executar(projeto);
            var pasta = PastasSpk.Calculo;
            Directory.CreateDirectory(pasta);
            File.WriteAllText(Path.Combine(pasta, "memorial.md"), Memorial.Gerar(relatorio));
            File.WriteAllBytes(Path.Combine(pasta, "memorial.docx"), MemorialDocx.Gerar(relatorio));
            new ResultadoGravado
            {
                Atende = relatorio.Atende,
                PressaoNecessariaKPa = relatorio.Resultado.PressaoNecessaria,
                VazaoChuveirosLmin = relatorio.Resultado.VazaoChuveiros,
                ChuveiroCritico = relatorio.Resultado.ChuveiroCritico,
                Bicos = bicos.Count,
                Tubos = lida.Tubos,
                Chuveiros = relatorio.Resultado.Chuveiros.Select(c => new BicoGravado
                {
                    Id = c.Id,
                    Ramal = c.Ramal,
                    PressaoKPa = c.Pressao,
                    VazaoLmin = c.Vazao,
                }).ToList(),
            }.Salvar();

            var critico = relatorio.Resultado.Chuveiros.First(c => c.Id == relatorio.Resultado.ChuveiroCritico);
            TaskDialog.Show("SPK — Rede do modelo", $"""
                {(relatorio.Atende ? "ATENDE" : "NÃO ATENDE")}

                Bicos no modelo: {bicos.Count}    Tubos: {lida.Tubos}
                Áreas avaliadas: {relatorio.Busca?.CandidatosAvaliados ?? 1}

                Pressão na fonte: {relatorio.Resultado.PressaoNecessaria:0.0} kPa ({relatorio.Resultado.PressaoNecessaria / 100:0.00} bar)
                Vazão dos bicos: {relatorio.Resultado.VazaoChuveiros:0} L/min
                Bico crítico: {critico.Id} a {critico.Pressao / 100:0.00} bar ({critico.Vazao:0} L/min)

                Referência do memorial, só os 12 bicos da área: 2,83 bar e cerca de 7 564 L/min.
                {string.Join(" ", relatorio.Avisos)}
                {string.Join(" ", lida.Avisos)}

                Memorial em:
                {pasta}
                """);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            TaskDialog.Show("SPK — Calcular rede", e.Message);
        }
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
