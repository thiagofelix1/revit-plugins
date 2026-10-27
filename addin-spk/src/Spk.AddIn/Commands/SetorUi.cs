using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Spk.AddIn.Verificacao;

namespace Spk.AddIn.Commands;

/// <summary>
/// O que Calcular e Diâmetros perguntam antes de ler a rede: se o modelo está pronto e qual setor (VGA, ou pedaço de
/// rede sem VGA).
/// </summary>
static class SetorUi
{
    /// <summary>
    /// Leitura estrita: com erro de modelagem (peças encostadas sem ligar, bico solto, família não confirmada), o SPK
    /// não segue calado. Mostra os problemas; seguir mesmo assim liga a rede por proximidade, que pode errar.
    /// </summary>
    /// <param name="seguir">O texto do botão de seguir mesmo assim ("Calcular mesmo assim").</param>
    public static bool ModeloPronto(UIDocument ui, string titulo, string seguir)
    {
        var verificacao = VerificacaoModelo.Verificar(ui.Document);
        if (verificacao.Erros == 0) return true;

        var erros = verificacao.Grupos.Where(g => g.Gravidade == Gravidade.Erro).Select(g => $"• {g.Titulo}: {g.Ocorrencias.Count}");
        var dialogo = new TaskDialog(titulo)
        {
            MainInstruction = $"O modelo tem {verificacao.Erros} problema(s) que afetam a leitura da rede",
            MainContent = string.Join("\n", erros) + "\n\nCom eles, o SPK precisa ligar a rede por proximidade, o que pode juntar " +
                          "o que não devia ou deixar trechos de fora. O certo é corrigir e rodar de novo.",
            CommonButtons = TaskDialogCommonButtons.Cancel,
        };
        dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Ver os problemas");
        dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, seguir, "Ligação por proximidade, com aviso no resultado");
        switch (dialogo.Show())
        {
            case TaskDialogResult.CommandLink1:
                VerificacaoUi.Mostrar(ui, verificacao);
                return false;
            case TaskDialogResult.CommandLink2:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Escolhe o setor e chama <paramref name="acao" /> com a VGA (nula sem VGA), as VGAs do modelo e o elemento do
    /// setor. Se a VGA não alcança a rede (vão no modelo), oferece seguir pelo pedaço de rede do setor. Nulo se o
    /// engenheiro cancelou.
    /// </summary>
    public static T? NoSetor<T>(UIDocument ui, string titulo, DadosProjeto dados, BicosDoModelo.Leitura leitura,
        Func<FamilyInstance?, IReadOnlyCollection<ElementId>, ElementId?, T> acao) where T : class
    {
        var doc = ui.Document;
        var vgas = leitura.Gerados ? [] : MapeamentoGravado.Vgas(doc);
        var fonte = vgas.Count switch
        {
            0 => null,
            1 => vgas[0],
            _ => EscolherVga(ui, vgas),
        };
        ElementId? doSetor = null;
        // Sem VGA, cada rede separada é um setor. Na malha gerada só pergunta se o cenário tem mais de uma.
        if (vgas.Count == 0 && (!leitura.Gerados || LeitorRede.Montar(doc, leitura.Bicos, dados.FatorC).TotalDeSetores > 1))
        {
            var (seguir, escolhido) = EscolherSetor(ui, titulo,
                leitura.Gerados ? "A malha gerada tem mais de um setor (redes separadas)." : null);
            if (!seguir) return null;
            doSetor = escolhido;
        }

        try
        {
            return acao(fonte, vgas.Select(v => v.Id).ToList(), doSetor);
        }
        catch (VgaDesligadaException e)
        {
            var (seguir, escolhido) = EscolherSetor(ui, titulo, e.Message);
            return seguir ? acao(null, [], escolhido) : null;
        }
    }

    /// <summary>
    /// Sem VGA no modelo, cada pedaço de rede é um setor. O engenheiro clica num bico ou tubo do que quer, ou fica com o
    /// de mais bicos (elemento nulo). Cancelar devolve <c>Seguir = false</c>.
    /// </summary>
    static (bool Seguir, ElementId? Elemento) EscolherSetor(UIDocument ui, string titulo, string? motivo)
    {
        var dialogo = new TaskDialog(titulo)
        {
            MainInstruction = "Qual setor?",
            MainContent = (motivo ?? "O modelo não tem VGA marcada em Famílias.") +
                          " Cada rede sem ligação com as outras é tratada como um setor, com a entrada na ponta mais baixa da descida.",
            CommonButtons = TaskDialogCommonButtons.Cancel,
        };
        dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Clicar num bico ou tubo do setor");
        dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "O setor com mais bicos");
        switch (dialogo.Show())
        {
            case TaskDialogResult.CommandLink1:
                var referencia = ui.Selection.PickObject(ObjectType.Element, new BicoOuTubo(), "Clique num bico ou tubo do setor.");
                return (true, referencia.ElementId);
            case TaskDialogResult.CommandLink2:
                return (true, null);
            default:
                return (false, null);
        }
    }

    sealed class BicoOuTubo : ISelectionFilter
    {
        public bool AllowElement(Element elemento) => elemento.Category?.Id.Value is long id
            && (id == (long)BuiltInCategory.OST_Sprinklers || id == (long)BuiltInCategory.OST_PipeCurves);

        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }

    static FamilyInstance EscolherVga(UIDocument ui, List<FamilyInstance> vgas)
    {
        var referencia = ui.Selection.PickObject(ObjectType.Element, new SoEstes(vgas.Select(v => v.Id).ToHashSet()),
            $"Há {vgas.Count} VGAs. Clique na VGA do setor.");
        return (FamilyInstance)ui.Document.GetElement(referencia);
    }

    sealed class SoEstes(HashSet<ElementId> ids) : ISelectionFilter
    {
        public bool AllowElement(Element elemento) => ids.Contains(elemento.Id);
        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }
}
