using System.IO;

namespace Spk.AddIn;

static class PastasSpk
{
    public static string Raiz => Path.Combine(Path.GetTempPath(), "spk-addin");
    public static string Calculo => Path.Combine(Raiz, "calculo");
}
