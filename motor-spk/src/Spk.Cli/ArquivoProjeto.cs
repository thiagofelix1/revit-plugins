using System.Text.Json;
using System.Text.Json.Serialization;
using Spk.Motor.Calculo;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Cli;

/// <summary>Formato JSON de entrada. É o que o add-in do Revit vai exportar a partir do modelo.</summary>
public sealed class ArquivoProjeto
{
    public string Nome { get; set; } = "Projeto";
    public ClasseRisco Classe { get; set; }
    public ArquivoBase Base { get; set; } = new();
    public List<string>? AreaOperacaoManual { get; set; }
    public double? VazaoHidrantes { get; set; }
    public double? DuracaoMinutos { get; set; }
    public ArquivoAbastecimento? Abastecimento { get; set; }
    public ArquivoRede Rede { get; set; } = new();

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public ProjetoSpk ParaProjeto()
    {
        var rede = new Rede(Rede.Fonte,
            Rede.Nos.Select(n => new No(n.Id, n.X, n.Y, n.Z) { DemandaFixa = n.DemandaFixa }),
            Rede.Trechos.Select(t => new Trecho(t.Id, t.De, t.Para, t.DiametroInterno, t.Comprimento, t.C)
            {
                ComprimentoEquivalente = t.ComprimentoEquivalente,
                Conexoes = t.Conexoes ?? "",
                DiametroNominal = t.DiametroNominal,
                Material = t.Material,
            }),
            Rede.Chuveiros.Select(c => new Chuveiro(c.Id, c.No, c.FatorK, c.AreaCobertura) { Ramal = c.Ramal ?? "", PressaoMinima = c.PressaoMinima }));

        BaseDeProjeto baseProjeto = Base.Tipo switch
        {
            "figura42" => BaseDeProjeto.PelaFigura42(Classe, Base.Area ?? throw Faltando("base.area"), Base.Condicoes),
            "densidade" => new BaseDensidadeArea(Base.Densidade ?? throw Faltando("base.densidade"), Base.Area ?? throw Faltando("base.area")),
            "esfr" => BaseDeProjeto.Esfr(Base.PressaoMinima ?? throw Faltando("base.pressaoMinima"), Base.Item ?? "tabela da IT-24"),
            _ => throw new ArgumentException($"base.tipo '{Base.Tipo}' desconhecido (use figura42, densidade ou esfr)."),
        };

        return new ProjetoSpk(Nome, rede, Classe, baseProjeto)
        {
            AreaOperacaoManual = AreaOperacaoManual,
            VazaoHidrantes = VazaoHidrantes,
            DuracaoMinutos = DuracaoMinutos,
            Abastecimento = Abastecimento is { } a
                ? new CurvaAbastecimento(a.Pontos.Select(p => (p.Vazao, p.Pressao)), a.Descricao ?? "Abastecimento")
                : null,
        };
    }

    public static ArquivoProjeto DeRede(string nome, ClasseRisco classe, ArquivoBase baseProjeto, Rede rede) => new()
    {
        Nome = nome,
        Classe = classe,
        Base = baseProjeto,
        Rede = new ArquivoRede
        {
            Fonte = rede.NoFonte,
            Nos = rede.Nos.Select(n => new ArquivoNo { Id = n.Id, X = n.X, Y = n.Y, Z = n.Z, DemandaFixa = n.DemandaFixa }).ToList(),
            Trechos = rede.Trechos.Select(t => new ArquivoTrecho
            {
                Id = t.Id, De = t.De, Para = t.Para, DiametroInterno = t.DiametroInterno, Comprimento = t.Comprimento, C = t.C,
                ComprimentoEquivalente = Math.Round(t.ComprimentoEquivalente, 3), Conexoes = t.Conexoes == "" ? null : t.Conexoes,
                DiametroNominal = t.DiametroNominal, Material = t.Material,
            }).ToList(),
            Chuveiros = rede.Chuveiros.Select(c => new ArquivoChuveiro
            {
                Id = c.Id, No = c.No, FatorK = c.FatorK, AreaCobertura = c.AreaCobertura, Ramal = c.Ramal, PressaoMinima = c.PressaoMinima,
            }).ToList(),
        },
    };

    static ArgumentException Faltando(string campo) => new($"Campo obrigatório ausente: {campo}.");
}

public sealed class ArquivoBase
{
    /// <summary>figura42, densidade ou esfr.</summary>
    public string Tipo { get; set; } = "figura42";
    public double? Area { get; set; }
    public double? Densidade { get; set; }
    public double? PressaoMinima { get; set; }
    public string? Item { get; set; }
    public CondicoesAreaOperacao? Condicoes { get; set; }
}

public sealed class ArquivoAbastecimento
{
    public string? Descricao { get; set; }
    public List<ArquivoPontoCurva> Pontos { get; set; } = [];
}

public sealed class ArquivoPontoCurva
{
    public double Vazao { get; set; }
    public double Pressao { get; set; }
}

public sealed class ArquivoRede
{
    public string Fonte { get; set; } = "";
    public List<ArquivoNo> Nos { get; set; } = [];
    public List<ArquivoTrecho> Trechos { get; set; } = [];
    public List<ArquivoChuveiro> Chuveiros { get; set; } = [];
}

public sealed class ArquivoNo
{
    public string Id { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double DemandaFixa { get; set; }
}

public sealed class ArquivoTrecho
{
    public string Id { get; set; } = "";
    public string De { get; set; } = "";
    public string Para { get; set; } = "";
    public double DiametroInterno { get; set; }
    public double Comprimento { get; set; }
    public double C { get; set; } = 120;
    public double ComprimentoEquivalente { get; set; }
    public string? Conexoes { get; set; }
    public int? DiametroNominal { get; set; }
    public Material Material { get; set; } = Material.Aco;
}

public sealed class ArquivoChuveiro
{
    public string Id { get; set; } = "";
    public string No { get; set; } = "";
    public double FatorK { get; set; }
    public double AreaCobertura { get; set; }
    public string? Ramal { get; set; }
    public double? PressaoMinima { get; set; }
}
