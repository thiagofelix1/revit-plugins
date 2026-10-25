using System.IO;
using System.Text.Json;

namespace Spk.AddIn;

/// <summary>Dados da malha mínima. A janela grava isto; Gerar e Calcular leem.</summary>
public sealed class DadosEsboco
{
    public const string Marca = "SPK-GERADO";

    public int Ramais { get; set; } = 3;
    public int BicosPorRamal { get; set; } = 4;
    public double EspacamentoRamalM { get; set; } = 3;
    public double EspacamentoEntreRamaisM { get; set; } = 3;
    public double AlturaRedeM { get; set; } = 3;
    public double DropM { get; set; } = 0.3;
    public double FatorK { get; set; } = 360;
    public double AreaM2 { get; set; } = 12;
    public double PressaoMinimaKPa { get; set; } = 283;
    public double FatorC { get; set; } = 120;

    public static string Caminho => Path.Combine(PastasSpk.Raiz, "esboco.json");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public void Salvar()
    {
        Directory.CreateDirectory(PastasSpk.Raiz);
        File.WriteAllText(Caminho, JsonSerializer.Serialize(this, Json));
    }

    public static DadosEsboco Carregar()
    {
        if (!File.Exists(Caminho)) return new DadosEsboco();
        return JsonSerializer.Deserialize<DadosEsboco>(File.ReadAllText(Caminho), Json) ?? new DadosEsboco();
    }
}
