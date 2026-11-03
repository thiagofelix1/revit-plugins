using System.IO;
using Spk.AddIn.Folhas;

namespace Spk.AddIn.Servicos;

/// <summary>
/// As folhas sem janelas: gera cada conjunto em subtransação e exporta o PDF. O comando Folhas põe a troca de vista e o
/// diálogo em volta; o teste automático chama direto.
/// </summary>
static class ServicoFolhas
{
    /// <summary>Na ordem do PDF, como no jogo da Nova Era: setorização (009), isométrico (013), setor calculado (014).</summary>
    static readonly (string Nome, Func<ContextoFolhas, FolhasGeradas> Gerar)[] Conjuntos =
    [
        ("Setorização", FolhasSetorizacao.Gerar),
        ("Isométrico geral", FolhasIsometrico.Gerar),
        ("Setor calculado", FolhasDoSetor.Gerar),
    ];

    /// <param name="Pdf">Caminho do PDF, ou nulo se não foi gravado.</param>
    /// <param name="Situacao">Onde o PDF foi parar, ou por que não foi.</param>
    public sealed record Resultado(IReadOnlyList<string> Textos, IReadOnlyList<ElementId> Folhas, string? Pdf, string Situacao)
    {
        public string Texto() => string.Join("\n\n", Textos) + "\n\n" + Situacao;
    }

    /// <param name="pastaPdf">Onde gravar o PDF; nula: ao lado do projeto, na pasta SPK.</param>
    public static Resultado Gerar(Document doc, string? pastaPdf = null)
    {
        if (ResultadoGravado.Ler(doc) is { } calculo && AssinaturaDaRede.Mudou(doc, calculo))
            throw new InvalidOperationException("A rede ou os Dados do projeto mudaram desde o último cálculo, e as folhas mostrariam o resultado antigo. Rode Calcular de novo.");
        var contexto = new ContextoFolhas(doc);
        var textos = new List<string>();
        var folhas = new List<ElementId>();
        using (var transacao = new Transaction(doc, "SPK — Folhas"))
        {
            EngolirAvisos.Aplicar(transacao);
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
                return new Resultado(textos, folhas, null, "Nenhuma folha foi gerada.");
            }

            // Carimbo por último: a numeração do escritório vale para o conjunto, na ordem do PDF.
            using (var sub = new SubTransaction(doc))
            {
                sub.Start();
                var avisos = CarimboSpk.Preencher(doc, folhas.Select(doc.GetElement).OfType<ViewSheet>().ToList());
                sub.Commit();
                var numeros = string.Join(", ", folhas.Select(doc.GetElement).OfType<ViewSheet>().Select(f => f.SheetNumber));
                textos.Add($"Carimbo\nFolhas: {numeros}." + (avisos.Count > 0 ? "\n" + string.Join("\n", avisos.Select(a => "• " + a)) : ""));
            }
            if (transacao.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException(
                    "O Revit desfez as folhas ao confirmar (um erro do modelo, não um aviso). Nada foi alterado.\n\n" +
                    string.Join("\n\n", textos));
        }

        var (pdf, situacao) = Exportar(doc, folhas, pastaPdf);
        return new Resultado(textos, folhas, pdf, situacao);
    }

    static (string? Pdf, string Situacao) Exportar(Document doc, IList<ElementId> folhas, string? pastaPdf)
    {
        var pasta = pastaPdf
                    ?? (string.IsNullOrEmpty(doc.PathName) ? PastasSpk.Calculo : Path.Combine(Path.GetDirectoryName(doc.PathName)!, "SPK"));
        Directory.CreateDirectory(pasta);
        var onde = pastaPdf is not null ? "PDF na pasta pedida."
            : string.IsNullOrEmpty(doc.PathName) ? @"O projeto ainda não foi salvo. O PDF foi para Documentos\SPK\calculo."
            : "PDF ao lado do projeto, na pasta SPK.";

        try
        {
            var opcoes = new PDFExportOptions
            {
                FileName = "SPK-folhas",
                Combine = true,
                PaperFormat = ExportPaperFormat.Default,
            };
            var ok = doc.Export(pasta, folhas, opcoes);
            var caminho = Path.Combine(pasta, "SPK-folhas.pdf");
            return ok
                ? (caminho, $"{onde}\n{caminho}")
                : (null, $"As folhas ficaram no projeto. O PDF não foi concluído.\n{caminho}");
        }
        catch (Autodesk.Revit.Exceptions.FileAccessException)
        {
            return (null, "As folhas ficaram no projeto. Feche o PDF aberto e rode Folhas de novo para gravar o arquivo.");
        }
    }
}
