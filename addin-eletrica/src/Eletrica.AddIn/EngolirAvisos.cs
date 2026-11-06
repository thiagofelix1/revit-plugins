namespace Eletrica.AddIn;

/// <summary>Apaga os avisos da transação (marca duplicada, tipo renomeado) para não abrir um diálogo do Revit por elemento.</summary>
sealed class EngolirAvisos : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
    {
        if (accessor.GetSeverity() == FailureSeverity.Error)
            return FailureProcessingResult.Continue;
        foreach (var aviso in accessor.GetFailureMessages())
        {
            if (aviso.GetSeverity() == FailureSeverity.Warning)
                accessor.DeleteWarning(aviso);
        }

        return FailureProcessingResult.Continue;
    }

    public static void Aplicar(Transaction transacao)
    {
        var opcoes = transacao.GetFailureHandlingOptions();
        opcoes.SetFailuresPreprocessor(new EngolirAvisos());
        transacao.SetFailureHandlingOptions(opcoes);
    }
}

/// <summary>
///     Para uma transação pequena e opcional: apaga os avisos e, se o Revit acusar um erro, desfaz só ela, sem diálogo, e guarda
///     a mensagem. Assim um valor recusado não leva junto o trabalho das outras transações.
/// </summary>
sealed class DesfazerSeErro : IFailuresPreprocessor
{
    public List<string> Erros { get; } = [];

    public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
    {
        foreach (var falha in accessor.GetFailureMessages())
        {
            if (falha.GetSeverity() == FailureSeverity.Warning) accessor.DeleteWarning(falha);
            else Erros.Add(falha.GetDescriptionText());
        }
        return Erros.Count > 0 ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
    }

    public static DesfazerSeErro Aplicar(Transaction transacao)
    {
        var preprocessador = new DesfazerSeErro();
        var opcoes = transacao.GetFailureHandlingOptions();
        opcoes.SetFailuresPreprocessor(preprocessador);
        opcoes.SetClearAfterRollback(true);
        transacao.SetFailureHandlingOptions(opcoes);
        return preprocessador;
    }
}
