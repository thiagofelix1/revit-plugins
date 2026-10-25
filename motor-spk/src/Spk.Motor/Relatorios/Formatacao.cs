using System.Globalization;
using Spk.Motor.Normas;

namespace Spk.Motor.Relatorios;

/// <summary>Formatação compartilhada entre os geradores de memorial (Markdown, DOCX, ...).</summary>
static class Formatacao
{
    static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static string F(double v, int casas) => v.ToString("N" + casas, Br);

    public static string NomeClasse(ClasseRisco c) => c switch
    {
        ClasseRisco.Leve => "Risco leve",
        ClasseRisco.Ordinario1 => "Risco ordinário grupo 1",
        ClasseRisco.Ordinario2 => "Risco ordinário grupo 2",
        ClasseRisco.Extraordinario1 => "Risco extraordinário grupo 1",
        ClasseRisco.Extraordinario2 => "Risco extraordinário grupo 2",
        _ => "Armazenamento (IT-24)",
    };
}
