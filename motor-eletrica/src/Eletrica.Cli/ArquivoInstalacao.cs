using System.Text.Json;
using System.Text.Json.Serialization;
using Eletrica.Motor.Modelo;

namespace Eletrica.Cli;

/// <summary>Formato JSON da instalação: o que o add-in exporta e a linha de comando lê. Os registros do modelo são serializados como estão.</summary>
public sealed class ArquivoInstalacao
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Nome { get; set; } = "";
    public Alimentacao Alimentacao { get; set; } = new(127, 220, 2, EsquemaAterramento.TNCS);
    public List<Ambiente> Ambientes { get; set; } = [];
    public List<Quadro> Quadros { get; set; } = [];
    public List<Circuito> Circuitos { get; set; } = [];
    public List<Ponto> Pontos { get; set; } = [];
    public List<Trecho> Trechos { get; set; } = [];
    public List<Caixa> Caixas { get; set; } = [];
    public List<Comando> Comandos { get; set; } = [];

    public static ArquivoInstalacao De(Instalacao i) => new()
    {
        Nome = i.Nome,
        Alimentacao = i.Alimentacao,
        Ambientes = [.. i.Ambientes],
        Quadros = [.. i.Quadros],
        Circuitos = [.. i.Circuitos],
        Pontos = [.. i.Pontos],
        Trechos = [.. i.Trechos],
        Caixas = [.. i.Caixas],
        Comandos = [.. i.Comandos],
    };

    public Instalacao ParaInstalacao() => new(Nome, Alimentacao, Ambientes, Quadros, Circuitos, Pontos, Trechos, Caixas, Comandos);
}
