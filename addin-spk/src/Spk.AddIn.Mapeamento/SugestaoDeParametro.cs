namespace Spk.AddIn.Mapeamento;

/// <summary>
///     Sugere, por aproximação de nome, qual parâmetro de uma família corresponde a fator K ou área de
///     cobertura — pra pré-preencher a confirmação em vez de o usuário digitar do zero. É só um chute
///     inicial: o usuário confirma ou troca na própria UI.
/// </summary>
public static class SugestaoDeParametro
{
    public static readonly string[] PalavrasFatorK = ["fator k", "fator-k", "k-factor", "kfactor", "fator"];
    public static readonly string[] PalavrasArea = ["área de cobertura", "area de cobertura", "coverage area", "área", "area"];

    public static string Sugerir(IReadOnlyList<string> disponiveis, string[] palavrasChave) =>
        disponiveis.FirstOrDefault(p => palavrasChave.Any(k => p.Contains(k, StringComparison.OrdinalIgnoreCase))) ?? "";
}
