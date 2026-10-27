namespace Spk.AddIn.Modelagem;

/// <summary>
/// Uma operação de modelagem numa transação própria. Se ela devolver um motivo, lançar erro do Revit ou o Revit acusar
/// erro ao confirmar, só ela volta atrás e o motivo é devolvido. As ferramentas chamam uma vez por peça, dentro de um
/// grupo de transações, para o engenheiro desfazer tudo com um Ctrl+Z e ver por que cada peça ficou de fora.
/// </summary>
static class TransacaoSegura
{
    /// <returns>Nulo se deu certo; senão, por que não.</returns>
    public static string? Executar(Document doc, string nome, Func<string?> acao)
    {
        var recusa = new RecusarErros();
        using var transacao = new Transaction(doc, nome);
        var opcoes = transacao.GetFailureHandlingOptions();
        opcoes.SetFailuresPreprocessor(recusa);
        transacao.SetFailureHandlingOptions(opcoes);
        transacao.Start();
        string? motivo;
        try
        {
            motivo = acao();
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            motivo = "o Revit recusou: " + e.Message;
        }
        if (motivo is not null)
        {
            transacao.RollBack();
            return motivo;
        }
        return transacao.Commit() == TransactionStatus.Committed ? null : "o Revit desfez: " + (recusa.Mensagem ?? "erro ao confirmar");
    }

    /// <summary>Apaga avisos; com erro, desfaz a transação inteira e guarda a mensagem.</summary>
    sealed class RecusarErros : IFailuresPreprocessor
    {
        public string? Mensagem { get; private set; }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor acessor)
        {
            foreach (var falha in acessor.GetFailureMessages())
            {
                if (falha.GetSeverity() == FailureSeverity.Warning) acessor.DeleteWarning(falha);
                else Mensagem ??= falha.GetDescriptionText();
            }
            return Mensagem is null ? FailureProcessingResult.Continue : FailureProcessingResult.ProceedWithRollBack;
        }
    }
}
