using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Folhas;

namespace Spk.AddIn.Commands;

/// <summary>
/// Monta as folhas que o modelo permite e exporta o PDF. Cada conjunto roda em subtransação: se um falha, os outros ficam
/// e o motivo aparece no fim.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class FolhasCommand : ExternalCommand
{
    /// <summary>Na ordem do PDF, como no jogo da Nova Era: setorização (009), isométrico (013), setor calculado (014).</summary>
    static readonly (string Nome, Func<ContextoFolhas, FolhasGeradas> Gerar)[] Conjuntos =
    [
        ("Setorização", FolhasSetorizacao.Gerar),
        ("Isométrico geral", FolhasIsometrico.Gerar),
        ("Setor calculado", FolhasDoSetor.Gerar),
    ];

    public override void Execute()
    {
        try
        {
            var ui = Application.ActiveUIDocument ?? throw new InvalidOperationException("Abra um projeto antes de gerar as folhas.");
            var doc = ui.Document;
            SairDasVistasDoPlugin(ui);

            var contexto = new ContextoFolhas(doc);
            var textos = new List<string>();
            var folhas = new List<ElementId>();
            using (var transacao = new Transaction(doc, "SPK — Folhas"))
            {
                var opcoes = transacao.GetFailureHandlingOptions();
                opcoes.SetFailuresPreprocessor(new EngolirAvisos());
                transacao.SetFailureHandlingOptions(opcoes);
                transacao.Start();
                foreach (var (nome, gerar) in Conjuntos)
                {
                    using var sub = new SubTransaction(doc);
                    sub.Start();
                    try
                    {
                        var gerado = gerar(contexto);
                        sub.Commit();
                        folhas.AddRange(gerado.Folhas);
                        textos.Add($"{nome}\n{gerado.Texto}");
                    }
                    catch (Exception e) when (e is not Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                        textos.Add($"{nome}: não gerado. {e.Message}");
                    }
                }

                if (folhas.Count == 0)
                {
                    transacao.RollBack();
                    TaskDialog.Show("SPK — Folhas", string.Join("\n\n", textos));
                    return;
                }
                if (transacao.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException(
                        "O Revit desfez as folhas ao confirmar (um erro do modelo, não um aviso). Nada foi alterado.\n\n" +
                        string.Join("\n\n", textos));
            }

            var pdf = Exportar(doc, folhas);
            if (doc.GetElement(folhas[0]) is ViewSheet primeira)
                ui.ActiveView = primeira;

            TaskDialog.Show("SPK — Folhas", string.Join("\n\n", textos) + "\n\n" + pdf);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            TaskDialog.Show("SPK — Folhas", e.Message);
        }
    }

    /// <summary>O Revit não apaga a vista ativa. As vistas e folhas do plugin são recriadas, então sai delas antes.</summary>
    static void SairDasVistasDoPlugin(UIDocument ui)
    {
        if (!DoPlugin(ui.ActiveView)) return;
        var outra = new FilteredElementCollector(ui.Document).OfClass(typeof(View)).Cast<View>()
                        .FirstOrDefault(v => !v.IsTemplate && !DoPlugin(v)
                                             && v.ViewType is ViewType.FloorPlan or ViewType.ThreeD or ViewType.CeilingPlan or ViewType.Section)
                    ?? throw new InvalidOperationException("Abra uma vista que não seja do SPK (uma planta qualquer) e rode Folhas de novo.");
        ui.ActiveView = outra;
    }

    static bool DoPlugin(View? vista) => vista switch
    {
        ViewSheet folha => folha.SheetNumber.StartsWith(PranchaSpk.PrefixoNumero, StringComparison.Ordinal),
        null => false,
        _ => vista.Name.StartsWith(PranchaSpk.PrefixoVista, StringComparison.Ordinal),
    };

    static string Exportar(Document doc, IList<ElementId> folhas)
    {
        var pasta = string.IsNullOrEmpty(doc.PathName)
            ? PastasSpk.Calculo
            : Path.Combine(Path.GetDirectoryName(doc.PathName)!, "SPK");
        Directory.CreateDirectory(pasta);
        var onde = string.IsNullOrEmpty(doc.PathName)
            ? @"O projeto ainda não foi salvo. O PDF foi para Documentos\SPK\calculo."
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
}
