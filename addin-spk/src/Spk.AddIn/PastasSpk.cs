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
}
