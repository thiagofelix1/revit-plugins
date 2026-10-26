using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Spk.AddIn.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DadosProjetoCommand : ExternalCommand
{
    public override void Execute()
    {
        var doc = Application.ActiveUIDocument?.Document;
        if (doc is null)
        {
            TaskDialog.Show("SPK — Dados do projeto", "Abra um projeto antes.");
            return;
        }

        DadosProjetoUi.Editar(doc);
    }
}

static class DadosProjetoUi
{
    /// <summary>Abre a janela e grava no modelo se o engenheiro confirmar. Nulo se cancelar.</summary>
    public static DadosProjeto? Editar(Document doc)
    {
        var janela = new DadosProjetoJanela(DadosProjeto.Carregar(doc) ?? new DadosProjeto());
        if (janela.ShowDialog() != true || janela.Resultado is not { } dados) return null;
        ArmazenamentoSpk.GravarComTransacao(doc, DadosProjeto.Chave, dados, "SPK — Dados do projeto");
        return dados;
    }

    /// <summary>Os dados confirmados; se ainda não houver, pede antes de seguir.</summary>
    public static DadosProjeto? Obter(Document doc) => DadosProjeto.Carregar(doc) ?? Editar(doc);
}
