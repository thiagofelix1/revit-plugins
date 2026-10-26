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

            var leitura = BicosDoModelo.Ler(doc);
            var bicos = leitura.Bicos;

            var kAmericano = bicos.Where(b => b.FatorK < MenorKMetrico).Select(b => b.FatorK).Distinct().ToList();
            if (kAmericano.Count > 0)
                throw new InvalidOperationException(
                    $"Fator K {string.Join(", ", kAmericano.Select(k => k.ToString("0.##")))} parece estar em gpm/psi½. " +
                    "O cálculo usa L/min/bar½ (K 5,6 → 80; K 25,2 → 360). Corrija em Famílias ou em Dados do esboço.");

            var previa = new TaskDialog("SPK — Calcular")
            {
                MainInstruction = $"{bicos.Count} bicos entram no cálculo",
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
            var lida = LeitorRede.Montar(doc, bicos, dados.FatorC, fonte, vgas.Select(v => v.Id).ToList(), doSetor);
            var projeto = dados.CriarProjeto(leitura.Gerados ? "Malha gerada" : "Rede lida do modelo", lida.Rede);
            var relatorio = CalculoSpk.Executar(projeto);

            var pasta = PastasSpk.Calculo;
            Directory.CreateDirectory(pasta);
            File.WriteAllText(Path.Combine(pasta, "memorial.md"), Memorial.Gerar(relatorio));
            File.WriteAllBytes(Path.Combine(pasta, "memorial.docx"), MemorialDocx.Gerar(relatorio));

            var avisoParametros = GravarNoModelo(doc, lida, relatorio, dados);

            var critico = relatorio.Resultado.Chuveiros.First(c => c.Id == relatorio.Resultado.ChuveiroCritico);
            var falhas = relatorio.Verificacoes.Where(v => !v.Atende).Select(v => $"• {v.Descricao} ({v.Item}): {v.Detalhe}").ToList();
            TaskDialog.Show("SPK — Calcular", $"""
                {(relatorio.Atende ? "ATENDE" : "NÃO ATENDE")}
                {(falhas.Count > 0 ? string.Join("\n", falhas) + "\n" : "")}
                Setor: {lida.Setor}    Bicos: {lida.Rede.Chuveiros.Count}    Trechos: {lida.Rede.Trechos.Count}    Áreas avaliadas: {relatorio.Busca?.CandidatosAvaliados ?? 1}
                Pressão na fonte: {relatorio.Resultado.PressaoNecessaria:0.0} kPa ({relatorio.Resultado.PressaoNecessaria / 100:0.00} bar)
                Vazão dos bicos: {relatorio.Resultado.VazaoChuveiros:0} L/min
                Bico crítico: {critico.Id} a {critico.Pressao / 100:0.00} bar ({critico.Vazao:0} L/min)

                {string.Join("\n", relatorio.Avisos.Concat(lida.Avisos).Append(avisoParametros).OfType<string>())}

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

    /// <summary>
    /// Preenche os parâmetros SPK de bicos e tubos do setor e grava o resultado. Bicos fora da área ficam com pressão e
    /// vazão zeradas, para nenhuma etiqueta mostrar valor de um cálculo anterior.
    /// </summary>
    static string? GravarNoModelo(Document doc, LeitorRede.RedeLida lida, RelatorioSpk relatorio, DadosProjeto dados)
    {
        using var transacao = new Transaction(doc, "SPK — Resultado do cálculo");
        transacao.Start();
        var aviso = ParametrosSpk.Garantir(doc);

        var daArea = relatorio.Resultado.Chuveiros.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var bicos = new List<BicoGravado>();
        foreach (var chuveiro in lida.Rede.Chuveiros)
        {
            var elemento = doc.GetElement(lida.Bicos[chuveiro.Id]);
            daArea.TryGetValue(chuveiro.Id, out var calculado);
            if (elemento is not null)
            {
                ParametrosSpk.Gravar(elemento, ParametrosSpk.Numero, chuveiro.Id);
                ParametrosSpk.Gravar(elemento, ParametrosSpk.Setor, lida.Setor);
                ParametrosSpk.Gravar(elemento, ParametrosSpk.AreaCalculo, calculado is not null);
                ParametrosSpk.Gravar(elemento, ParametrosSpk.Pressao, calculado?.Pressao ?? 0);
                ParametrosSpk.Gravar(elemento, ParametrosSpk.Vazao, calculado?.Vazao ?? 0);
            }
            bicos.Add(new BicoGravado
            {
                Id = chuveiro.Id,
                Elemento = lida.Bicos[chuveiro.Id].Value,
                Ramal = chuveiro.Ramal,
                NaArea = calculado is not null,
                PressaoKPa = calculado?.Pressao ?? 0,
                VazaoLmin = calculado?.Vazao ?? 0,
            });
        }

        var porTubo = relatorio.Resultado.Trechos
            .Where(t => lida.Tubos.ContainsKey(t.Id))
            .GroupBy(t => lida.Tubos[t.Id])
            .ToDictionary(g => g.Key, g => (Vazao: g.Max(t => Math.Abs(t.Vazao)), Velocidade: g.Max(t => Math.Abs(t.Velocidade))));
        foreach (var id in lida.Tubos.Values.Distinct())
        {
            if (doc.GetElement(id) is not { } tubo) continue;
            porTubo.TryGetValue(id, out var valores);
            ParametrosSpk.Gravar(tubo, ParametrosSpk.Setor, lida.Setor);
            ParametrosSpk.Gravar(tubo, ParametrosSpk.Vazao, valores.Vazao);
            ParametrosSpk.Gravar(tubo, ParametrosSpk.Velocidade, valores.Velocidade);
        }

        new ResultadoGravado
        {
            Setor = lida.Setor,
            Classificacao = dados.Resumo(),
            Atende = relatorio.Atende,
            PressaoNecessariaKPa = relatorio.Resultado.PressaoNecessaria,
            VazaoChuveirosLmin = relatorio.Resultado.VazaoChuveiros,
            VazaoTotalLmin = relatorio.VazaoTotal,
            ChuveiroCritico = relatorio.Resultado.ChuveiroCritico,
            Chuveiros = bicos,
            Tubos = lida.Tubos.Values.Distinct().Select(i => i.Value).ToList(),
        }.Salvar(doc);

        if (transacao.Commit() != TransactionStatus.Committed)
            return "O Revit desfez a gravação dos resultados no modelo. O memorial foi gerado, mas as folhas não vão mostrar este cálculo.";
        return aviso;
    }

    /// <summary>
    /// Sem VGA no modelo, cada pedaço de rede é um setor. O engenheiro clica num bico ou tubo do que quer calcular, ou
    /// fica com o de mais bicos (elemento nulo). Cancelar devolve <c>Seguir = false</c>.
    /// </summary>
    static (bool Seguir, ElementId? Elemento) EscolherSetor(UIDocument ui)
    {
        var dialogo = new TaskDialog("SPK — Calcular")
        {
            MainInstruction = "Qual setor calcular?",
            MainContent = "O modelo não tem VGA marcada em Famílias. Cada rede sem ligação com as outras é tratada como um setor, " +
                          "com a entrada na ponta mais baixa da descida.",
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
