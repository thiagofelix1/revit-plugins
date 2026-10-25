using System.Globalization;
using System.Text.RegularExpressions;

namespace Spk.AddIn;

/// <summary>Linha confirmada em Descobrir famílias.</summary>
sealed class LinhaMapeamento
{
    public string Categoria { get; set; } = "";
    public string Familia { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Papel { get; set; } = "";
    public string ParametroFatorK { get; set; } = "";
    public string ValorFatorK { get; set; } = "";
    public string ParametroAreaCobertura { get; set; } = "";
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

/// <summary>Mapeamento de famílias confirmado, gravado no modelo.</summary>
static class MapeamentoGravado
{
    public const string Chave = "Mapeamento";

    public static IReadOnlyList<LinhaMapeamento> Carregar(Document doc) =>
        ArmazenamentoSpk.Ler<List<LinhaMapeamento>>(doc, Chave)
        ?? throw new InvalidOperationException("Não há mapeamento confirmado neste projeto. Rode \"Famílias\" e confirme a tela.");

    public static void Salvar(Document doc, IReadOnlyList<LinhaMapeamento> linhas) =>
        ArmazenamentoSpk.GravarComTransacao(doc, Chave, linhas.ToList(), "SPK — Mapeamento de famílias");
}
