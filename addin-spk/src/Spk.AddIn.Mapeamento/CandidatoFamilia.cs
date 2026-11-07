using System.ComponentModel;

namespace Spk.AddIn.Mapeamento;

public enum PapelFamilia { Ignorar, Chuveiro, Fonte }

/// <summary>
///     Uma combinação categoria+família+tipo encontrada no modelo, pendente de confirmação do usuário.
///     Não depende da API do Revit: a descoberta (fora desta classe) traduz o que achou no modelo pra isto.
/// </summary>
public sealed class CandidatoFamilia : INotifyPropertyChanged
{
    public required string Categoria { get; init; }
    public required string Familia { get; init; }
    public required string Tipo { get; init; }
    public required int QuantidadeInstancias { get; init; }
    public required IReadOnlyList<string> ParametrosDisponiveis { get; init; }

    public string ParametrosTexto => string.Join(", ", ParametrosDisponiveis);

    PapelFamilia _papel = PapelFamilia.Ignorar;

    public PapelFamilia Papel
    {
        get => _papel;
        set { _papel = value; OnPropertyChanged(nameof(Papel)); }
    }

    string _parametroFatorK = "";

    public string ParametroFatorK
    {
        get => _parametroFatorK;
        set { _parametroFatorK = value; OnPropertyChanged(nameof(ParametroFatorK)); }
    }

    string _parametroAreaCobertura = "";

    public string ParametroAreaCobertura
    {
        get => _parametroAreaCobertura;
        set { _parametroAreaCobertura = value; OnPropertyChanged(nameof(ParametroAreaCobertura)); }
    }

    string _valorFatorK = "";

    /// <summary>K usado no cálculo, em L/min/bar½. Sempre editável, mesmo quando veio de um parâmetro.</summary>
    public string ValorFatorK
    {
        get => _valorFatorK;
        set { _valorFatorK = value; OnPropertyChanged(nameof(ValorFatorK)); }
    }

    string _valorAreaCobertura = "";

    /// <summary>Área de cobertura usada no cálculo, em m². Sempre editável.</summary>
    public string ValorAreaCobertura
    {
        get => _valorAreaCobertura;
        set { _valorAreaCobertura = value; OnPropertyChanged(nameof(ValorAreaCobertura)); }
    }

    /// <summary>Preenchida quando o K lido da família foi convertido de gpm/psi½.</summary>
    public string? NotaFatorK { get; init; }

    public string OrigemFatorK => string.IsNullOrWhiteSpace(ParametroFatorK)
        ? "Nenhum parâmetro de fator K encontrado. Informe o valor em L/min/bar½."
        : $"Preenchido a partir do parâmetro \"{ParametroFatorK}\". {NotaFatorK} O número continua editável.";

    public string OrigemAreaCobertura => string.IsNullOrWhiteSpace(ParametroAreaCobertura)
        ? "Nenhum parâmetro de área encontrado. Informe a área de cobertura em m²."
        : $"Preenchido a partir do parâmetro \"{ParametroAreaCobertura}\". Confira se é a área de cobertura. O número continua editável.";

    /// <summary>Marcado como chuveiro sem K ou área maior que zero: ficaria de fora do cálculo.</summary>
    public bool FaltamMedidas => Papel == PapelFamilia.Chuveiro && !(Positivo(ValorFatorK) && Positivo(ValorAreaCobertura));

    /// <summary>Aceita "360", "5.6" e "5,6", com texto depois do número ("9 m²"), como a leitura do mapeamento gravado.</summary>
    static bool Positivo(string texto)
    {
        var achado = System.Text.RegularExpressions.Regex.Match((texto ?? "").Trim(), @"^-?\d+([.,]\d+)?");
        return achado.Success && double.TryParse(achado.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var valor) && valor > 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged(string nome) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));

    /// <summary>Monta o candidato já com o papel e os parâmetros de fator K/área sugeridos por aproximação de nome.</summary>
    public static CandidatoFamilia Criar(string categoria, string familia, string tipo, int quantidadeInstancias,
        IReadOnlyList<string> parametrosDisponiveis, PapelFamilia papelSugerido = PapelFamilia.Chuveiro,
        string? parametroFatorK = null, string? valorFatorK = null,
        string? parametroAreaCobertura = null, string? valorAreaCobertura = null, string? notaFatorK = null)
    {
        var candidato = new CandidatoFamilia
        {
            Categoria = categoria,
            Familia = familia,
            Tipo = tipo,
            QuantidadeInstancias = quantidadeInstancias,
            ParametrosDisponiveis = parametrosDisponiveis,
            NotaFatorK = notaFatorK,
        };
        candidato.Papel = papelSugerido;
        candidato.ParametroFatorK = parametroFatorK ?? SugestaoDeParametro.Sugerir(parametrosDisponiveis, SugestaoDeParametro.PalavrasFatorK);
        candidato.ParametroAreaCobertura = parametroAreaCobertura ?? SugestaoDeParametro.Sugerir(parametrosDisponiveis, SugestaoDeParametro.PalavrasArea);
        candidato.ValorFatorK = valorFatorK ?? "";
        candidato.ValorAreaCobertura = valorAreaCobertura ?? "";
        return candidato;
    }
}
