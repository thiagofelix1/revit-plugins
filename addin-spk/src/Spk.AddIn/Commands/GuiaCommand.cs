using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Ajuda;

namespace Spk.AddIn.Commands;

/// <summary>
///     Abre o Guia do SPK. O botão de uma etapa fecha o guia e dispara o comando da faixa, depois que este terminar
///     (PostCommand), como se o engenheiro tivesse clicado no botão.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class GuiaCommand : ExternalCommand
{
    public override void Execute()
    {
        var doc = Application.ActiveUIDocument?.Document;
        var arquivo = doc is null ? "Nenhum projeto aberto."
            : string.IsNullOrEmpty(doc.PathName) ? $"{doc.Title} (ainda não salvo)"
            : Path.GetFileName(doc.PathName);
        var janela = new GuiaJanela(arquivo, SituacaoDoProjeto.Ler(doc), TextoDoGuia.Capitulos());
        if (janela.ShowDialog() != true || janela.Escolhida is not { } acao) return;

        var id = RevitCommandId.LookupCommandId($"CustomCtrl_%CustomCtrl_%{SpkApplication.Aba}%{acao.Painel}%{acao.Comando}");
        if (id is null || !Application.CanPostCommand(id))
        {
            TaskDialog.Show("SPK — Guia", $"Não foi possível abrir \"{acao.Texto}\" daqui. Use o botão na aba SPK.");
            return;
        }

        Application.PostCommand(id);
    }
}
