using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Arquitetura;
using Spk.AddIn.Modelagem;
using Spk.Motor.Normas;

namespace Spk.AddIn.Commands;

/// <summary>
/// Gera a rede nos ambientes escolhidos: bicos pela norma, subgeral com a descida, ramais e ligação dos bicos, e os
/// diâmetros da tabela (<see cref="GerarRede" />). As opções da última vez ficam para a próxima.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class GerarRedeCommand : ExternalCommand
{
    const string Titulo = "SPK — Gerar rede";

    static OpcoesRede ultimas = new();

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
                                        "Para um galpão de teste, use Malha retangular.");
                return;
            }

            var edificio = LeituraDoEdificio.Ler(doc);
            if (edificio.Ambientes.Count == 0)
            {
                TaskDialog.Show(Titulo, "Nenhum ambiente na arquitetura: a geração parte dos ambientes (Room) da arquitetura vinculada.\n\n" +
                                        string.Join("\n", edificio.Avisos) + "\n\nSem arquitetura, use Malha retangular.");
                return;
            }
            var tipos = BicosPorAmbiente.Simbolos(doc).Select(BicosPorAmbiente.Nome).ToList();
            if (tipos.Count == 0)
            {
                TaskDialog.Show(Titulo, "O projeto não tem família de sprinkler carregada. Carregue uma e rode de novo.");
                return;
            }
            var limites = BicosPorAmbiente.Limites(dados, ultimas.Bicos);
            var resumo = $"Para cada ambiente marcado: bicos pela norma ({DadosProjeto.NomeClasse(dados.Classe)}: até {limites.AreaMaxima:0.0} m² " +
                         $"por bico e {limites.DistanciaMaxima:0.0} m entre bicos), a subgeral com a descida até o piso na parede de entrada, " +
                         "os ramais, a ligação dos bicos e os diâmetros. Depois: Verificar modelo e Calcular.";
            // O tipo de tubo aparece já preenchido (a última escolha neste projeto ou a sugestão) e é o projetista quem confirma.
            var tiposDeTubo = GeradorMalha.TiposComTe(doc).Select(t => (t.Name, GeradorMalha.NomeDoTipo(doc, t))).ToList();
            var janela = new BicosPorAmbienteJanela(edificio, tipos, BicosPorAmbiente.TipoPadrao(doc), resumo, ultimas.Bicos,
                ultimas with { TipoDeTubo = GeradorMalha.TipoComTe(doc).Name }, tiposDeTubo);
            if (janela.ShowDialog() != true || janela.Rede is not { } opcoes) return;
            ultimas = opcoes;
            if (opcoes.GeralPor is not null && janela.VgaNoClique)
            {
                // O ponto da VGA vale só para esta geração: na próxima, a pergunta volta.
                try
                {
                    var p = ui.Selection.PickPoint("Clique no corredor onde fica a descida da VGA (a geral sai dela para os dois lados)");
                    opcoes = opcoes with
                    {
                        EntradaDaGeral = new Spk.Motor.Modelo.Ponto2(UnitUtils.ConvertFromInternalUnits(p.X, UnitTypeId.Meters),
                            UnitUtils.ConvertFromInternalUnits(p.Y, UnitTypeId.Meters)),
                    };
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return;
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    TaskDialog.Show(Titulo, "Abra uma planta para clicar onde fica a VGA.");
                    return;
                }
            }

            var esboco = DadosEsboco.Carregar(doc);
            esboco.TipoDeTubo = opcoes.TipoDeTubo;
            ArmazenamentoSpk.GravarComTransacao(doc, DadosEsboco.Chave, esboco, "SPK — Tipo de tubo");

            var criados = new List<ElementId>();
            var (redes, geral) = GerarRede.ExecutarComGeral(doc, dados, edificio, janela.Escolhidos, opcoes, criados);
            criados = criados.Where(id => doc.GetElement(id) is not null).ToList();
            if (criados.Count > 0) ui.Selection.SetElementIds(criados);
            new TaskDialog(Titulo)
            {
                MainInstruction = $"Rede gerada em {redes.Count(r => r.Recusa is null)} de {redes.Count} ambiente(s)" +
                                  (geral is { Ligacao: { } l } ? $", {l.Ligadas} ligada(s) à geral" : ""),
                MainContent = $"Tubos: {GeradorMalha.NomeDoTipo(doc, GeradorMalha.TipoComTe(doc, opcoes.TipoDeTubo))}\n\n" + string.Join("\n", redes.Select(r => r.Texto())) + (geral is null ? "" : "\n\n" + geral.Texto()) +
                              "\n\nO que foi criado fica selecionado. Ctrl+Z desfaz cada etapa. Depois: Verificar modelo e Calcular.",
            }.Show();
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
