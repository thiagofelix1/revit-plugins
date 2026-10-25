using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Spk.AddIn.Commands;

/// <summary>Monta as folhas da malha gerada e exporta o PDF.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class FolhasCommand : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var ui = Application.ActiveUIDocument ?? throw new InvalidOperationException("Abra um projeto antes de gerar as folhas.");
            var doc = ui.Document;
            SairDaFolha(ui);

            GeradorFolhas.Resultado gerado;
            using var transacao = new Transaction(doc, "SPK — Folhas");
            var opcoes = transacao.GetFailureHandlingOptions();
            opcoes.SetFailuresPreprocessor(new EngolirAvisos());
            transacao.SetFailureHandlingOptions(opcoes);
            transacao.Start();
            try
            {
                gerado = GeradorFolhas.Gerar(doc);
                transacao.Commit();
            }
            catch
            {
                if (transacao.GetStatus() == TransactionStatus.Started) transacao.RollBack();
                throw;
            }

            var pdf = Exportar(doc, gerado.Folhas);
            if (doc.GetElement(gerado.FolhaPlanta) is ViewSheet planta)
                ui.ActiveView = planta;

            TaskDialog.Show("SPK — Folhas", gerado.Texto + "\n\n" + pdf);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            TaskDialog.Show("SPK — Folhas", e.Message);
        }
    }

    static void SairDaFolha(UIDocument ui)
    {
        if (ui.ActiveView is not ViewSheet folha) return;
        if (folha.SheetNumber is not (GeradorFolhas.NumeroPlanta or GeradorFolhas.NumeroIso or GeradorFolhas.NumeroCorte))
            return;

        var outra = new FilteredElementCollector(ui.Document).OfClass(typeof(View)).Cast<View>()
            .FirstOrDefault(v => !v.IsTemplate && v.Id != folha.Id && v.ViewType is ViewType.FloorPlan or ViewType.ThreeD);
        if (outra is not null) ui.ActiveView = outra;
    }

    static string Exportar(Document doc, IList<ElementId> folhas)
    {
        var pasta = string.IsNullOrEmpty(doc.PathName)
            ? PastasSpk.Calculo
            : Path.Combine(Path.GetDirectoryName(doc.PathName)!, "SPK");
        Directory.CreateDirectory(pasta);
        var onde = string.IsNullOrEmpty(doc.PathName)
            ? "O projeto ainda não foi salvo. O PDF foi para a pasta temporária."
            : "PDF ao lado do projeto, na pasta SPK.";

        try
        {
            var pdf = new PDFExportOptions
            {
                FileName = "SPK-folhas",
                Combine = true,
                PaperFormat = ExportPaperFormat.Default,
            };
            var ok = doc.Export(pasta, folhas, pdf);
            var caminho = Path.Combine(pasta, "SPK-folhas.pdf");
            return ok
                ? $"{onde}\n{caminho}"
                : $"As folhas ficaram no projeto. O PDF não foi concluído.\n{caminho}";
        }
        catch (Autodesk.Revit.Exceptions.FileAccessException)
        {
            return "As folhas ficaram no projeto. Feche o PDF aberto e rode Folhas de novo para gravar o arquivo.";
        }
    }

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
    }
}
