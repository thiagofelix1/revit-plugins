using System.IO;

namespace Spk.AddIn;

/// <summary>
/// Pasta do plugin em Documentos\SPK. Não é a pasta temporária: dentro do Revit ela muda a cada sessão, e o memorial
/// e o PDF sumiam de onde o botão Memorial procura.
/// </summary>
static class PastasSpk
{
    public static string Raiz => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SPK");
    public static string Calculo => Path.Combine(Raiz, "calculo");

    /// <summary>Um memorial por projeto e setor, para o botão Memorial não abrir o de outro projeto calculado depois.</summary>
    public static string Memoriais => Path.Combine(Raiz, "memoriais");

    /// <summary>O memorial do setor deste projeto: "&lt;arquivo do projeto&gt; - &lt;setor&gt;.docx" em <see cref="Memoriais" />.</summary>
    public static string MemorialDo(Document doc, string setor)
    {
        var projeto = string.IsNullOrEmpty(doc.PathName) ? doc.Title : Path.GetFileNameWithoutExtension(doc.PathName);
        var invalidos = Path.GetInvalidFileNameChars();
        var nome = new string($"{projeto} - {setor}".Select(c => invalidos.Contains(c) ? '_' : c).ToArray()).Trim();
        return Path.Combine(Memoriais, nome + ".docx");
    }
}
