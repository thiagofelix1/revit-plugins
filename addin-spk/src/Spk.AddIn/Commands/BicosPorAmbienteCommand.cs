using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Arquitetura;
using Spk.AddIn.Modelagem;
using Spk.Motor.Normas;

namespace Spk.AddIn.Commands;

/// <summary>
/// Posiciona bicos nos ambientes da arquitetura (do arquivo ou dos vínculos) pela regra de espaçamento da norma, com o
/// defletor na altura do teto, ou confere os bicos que já estão neles. As opções da última vez ficam para a próxima.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BicosPorAmbienteCommand : ExternalCommand
{
    const string Titulo = "SPK — Bicos por ambiente";

    static OpcoesBicos ultimas = new();

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
            if (dados.Classe == ClasseRisco.Armazenamento || dados.Base == TipoBase.Esfr)
            {
                TaskDialog.Show(Titulo, "Armazenamento e ESFR têm regras próprias de espaçamento (IT-24, Tabelas 3 e 4): ainda não automático. " +
                                        "Esta ferramenta usa a Tabela 2 (bico spray de cobertura-padrão).");
                return;
            }

            var edificio = LeituraDoEdificio.Ler(doc);
            if (edificio.Ambientes.Count == 0)
            {
                TaskDialog.Show(Titulo, "Nenhum ambiente na arquitetura.\n\n" + string.Join("\n", edificio.Avisos));
                return;
            }
            var limites = BicosPorAmbiente.Limites(dados, ultimas);
            var tipos = BicosPorAmbiente.Simbolos(doc).Select(BicosPorAmbiente.Nome).ToList();
            if (tipos.Count == 0)
            {
                TaskDialog.Show(Titulo, "O projeto não tem família de sprinkler carregada. Carregue uma e rode de novo.");
                return;
            }
            var resumo = $"{DadosProjeto.NomeClasse(dados.Classe)}: até {limites.AreaMaxima:0.0} m² por bico, {limites.DistanciaMaxima:0.0} m entre " +
                         $"bicos e {limites.ParedeMaxima:0.00} m até a parede (Tabela 2, teto incombustível; {EspacamentoDeBicos.Fonte}). " +
                         $"{edificio.Ambientes.Count} ambientes lidos ({string.Join("; ", edificio.Fontes)}).";
            var janela = new BicosPorAmbienteJanela(edificio, tipos, BicosPorAmbiente.TipoPadrao(doc), resumo, ultimas);
            if (janela.ShowDialog() != true || janela.Opcoes is not { } opcoes) return;
            ultimas = opcoes;
            limites = BicosPorAmbiente.Limites(dados, opcoes);

            if (!janela.Posicionar)
            {
                var conferidos = BicosPorAmbiente.Conferir(doc, edificio, janela.Escolhidos, limites, opcoes);
                var comFalha = conferidos.Count(r => r.Falhas.Count > 0);
                Mostrar($"{conferidos.Count} ambiente(s) conferido(s); {comFalha} com problema", conferidos.Select(r => r.Texto()));
                return;
            }

            var criados = new List<ElementId>();
            var resultados = BicosPorAmbiente.Posicionar(doc, edificio, janela.Escolhidos, limites, opcoes, criados);
            if (criados.Count > 0) ui.Selection.SetElementIds(criados);
            Mostrar($"{criados.Count} bico(s) em {resultados.Count(r => r.Recusa is null)} de {resultados.Count} ambiente(s)",
                resultados.Select(r => r.Texto()).Append("Os bicos criados ficam selecionados. Ctrl+Z desfaz tudo. Depois: Traçar ramais."));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            TaskDialog.Show(Titulo, e.Message);
        }
    }

    static void Mostrar(string titulo, IEnumerable<string> linhas) =>
        new TaskDialog(Titulo) { MainInstruction = titulo, MainContent = string.Join("\n", linhas) }.Show();
}
