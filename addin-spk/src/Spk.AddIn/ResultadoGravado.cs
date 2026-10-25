using System.IO;
using System.Text.Json;

namespace Spk.AddIn;

/// <summary>Último cálculo gravado em disco. As folhas leem isto para achar o bico crítico.</summary>
public sealed class ResultadoGravado
{
    public bool Atende { get; set; }
    public double PressaoNecessariaKPa { get; set; }
    public double VazaoChuveirosLmin { get; set; }
    public string? ChuveiroCritico { get; set; }
    public int Bicos { get; set; }
    public int Tubos { get; set; }
    public List<BicoGravado> Chuveiros { get; set; } = [];

    public static string Caminho => Path.Combine(PastasSpk.Calculo, "resultado.json");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public void Salvar()
    {
        Directory.CreateDirectory(PastasSpk.Calculo);
        File.WriteAllText(Caminho, JsonSerializer.Serialize(this, Json));
    }

    public static ResultadoGravado? Ler()
    {
        if (!File.Exists(Caminho)) return null;
        try
        {
            return JsonSerializer.Deserialize<ResultadoGravado>(File.ReadAllText(Caminho), Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class BicoGravado
{
    public string Id { get; set; } = "";
    public string? Ramal { get; set; }
    public double PressaoKPa { get; set; }
    public double VazaoLmin { get; set; }
}
