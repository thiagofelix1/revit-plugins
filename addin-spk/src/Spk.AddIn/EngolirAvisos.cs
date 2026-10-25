namespace Spk.AddIn;

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
