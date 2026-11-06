using System.Globalization;

namespace Eletrica.AddIn;

/// <summary>Leitura dos campos das janelas: aceita vírgula ou ponto e explica o erro com o nome do campo.</summary>
static class Entrada
{
    public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static string F(double valor) => valor.ToString("0.##", Cultura);

    public static string F(double? valor) => valor is { } v ? F(v) : "";

    public static int Inteiro(string texto, string nome, int minimo, int maximo)
    {
        if (!int.TryParse(texto.Trim(), NumberStyles.Integer, Cultura, out var valor))
            throw new FormatException($"{nome}: informe um número inteiro.");
        if (valor < minimo || valor > maximo) throw new ArgumentOutOfRangeException(nome, $"{nome} deve ficar entre {minimo} e {maximo}.");
        return valor;
    }

    public static double Numero(string texto, string nome, double minimo, double maximo) =>
        Opcional(texto, nome, minimo, maximo) ?? throw new FormatException($"{nome}: informe um número.");

    public static double? Opcional(string texto, string nome, double minimo, double maximo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (!double.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
            throw new FormatException($"{nome}: informe um número.");
        if (valor < minimo || valor > maximo) throw new ArgumentOutOfRangeException(nome, $"{nome} deve ficar entre {F(minimo)} e {F(maximo)}.");
        return valor;
    }
}
