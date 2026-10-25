using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Spk.AddIn;

/// <summary>Linha confirmada em Descobrir famílias. O JSON usa os nomes das propriedades em PascalCase.</summary>
sealed class LinhaMapeamento
{
    public string Familia { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Papel { get; set; } = "";
    public string ValorFatorK { get; set; } = "";
    public string ValorAreaCobertura { get; set; } = "";

    public bool EhChuveiroComMedidas =>
        Papel == "Chuveiro" && Numero(ValorFatorK) is > 0 && Numero(ValorAreaCobertura) is > 0;

    public double FatorK => Numero(ValorFatorK) ?? 0;
    public double AreaCobertura => Numero(ValorAreaCobertura) ?? 0;

    /// <summary>Aceita "360", "5.6" e "5,6". Ignora texto depois do número ("0 m²").</summary>
    public static double? Numero(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var encontrado = Regex.Match(texto.Trim(), @"^-?\d+([.,]\d+)?");
        if (!encontrado.Success) return null;
        var canonico = encontrado.Value.Replace(',', '.');
        return double.TryParse(canonico, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor) ? valor : null;
    }
}

static class MapeamentoGravado
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// O Revit às vezes grava em uma pasta temporária da sessão, não na temp padrão.
    /// Fica com o mapeamento.json mais recente entre os dois lugares.
    /// </summary>
    public static IReadOnlyList<LinhaMapeamento> Carregar()
    {
        var caminho = MaisRecente();
        if (caminho is null) throw new FileNotFoundException(
            "Não há mapeamento confirmado. Rode \"Descobrir famílias\" e confirme a tela.");

        var linhas = JsonSerializer.Deserialize<List<LinhaMapeamento>>(File.ReadAllText(caminho), Json)
                     ?? throw new InvalidOperationException("O mapeamento está vazio.");
        return linhas;
    }

    static string? MaisRecente()
    {
        var candidatos = new List<string> { Path.Combine(PastasSpk.Raiz, "mapeamento.json") };
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
        if (Directory.Exists(local))
        {
            candidatos.Add(Path.Combine(local, "spk-addin", "mapeamento.json"));
            foreach (var pasta in Directory.EnumerateDirectories(local))
                candidatos.Add(Path.Combine(pasta, "spk-addin", "mapeamento.json"));
        }

        return candidatos.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }
}
