using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Ajuda;

namespace Spk.AddIn.Commands;

/// <summary>
///     Abre o Guia do SPK. O botão de uma etapa fecha o guia e executa o comando dela, como se o engenheiro tivesse
///     clicado no botão da faixa.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
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

        // Executa o comando aqui mesmo, com os dados deste: não depende de onde o botão está na faixa. Este comando é Manual
        // para os outros poderem abrir as transações deles.
        var tipo = typeof(GuiaCommand).Assembly.GetType($"{typeof(GuiaCommand).Namespace}.{acao.Comando}");
        if (tipo is null || Activator.CreateInstance(tipo) is not IExternalCommand comando)
        {
            TaskDialog.Show("SPK — Guia", $"Não foi possível abrir \"{acao.Texto}\" daqui. Use o botão na aba SPK.");
            return;
        }
        var mensagem = "";
        if (comando.Execute(ExternalCommandData, ref mensagem, new ElementSet()) == Result.Failed && mensagem.Length > 0)
            TaskDialog.Show("SPK — " + acao.Texto, mensagem);
    }
}
