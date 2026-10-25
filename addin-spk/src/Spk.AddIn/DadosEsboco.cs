namespace Spk.AddIn;

/// <summary>
///     Geometria da malha e o bico usado nela. Gravado no modelo; Gerar e Calcular leem daqui.
///     Pressão mínima e fator C ficam em <see cref="DadosProjeto" />, que são critérios de projeto.
/// </summary>
public sealed class DadosEsboco
{
    public const string Marca = "SPK-GERADO";
    public const string Chave = "Esboco";

    public int Ramais { get; set; } = 3;
    public int BicosPorRamal { get; set; } = 4;
    public double EspacamentoRamalM { get; set; } = 3;
    public double EspacamentoEntreRamaisM { get; set; } = 3;
    public double AlturaRedeM { get; set; } = 3;
    public double DropM { get; set; } = 0.3;

    /// <summary>Em L/min/bar½.</summary>
    public double FatorK { get; set; } = 360;

    public double AreaM2 { get; set; } = 12;

    public static DadosEsboco Carregar(Document doc) => ArmazenamentoSpk.Ler<DadosEsboco>(doc, Chave) ?? new DadosEsboco();

    public void Salvar(Document doc) => ArmazenamentoSpk.Gravar(doc, Chave, this);
}
