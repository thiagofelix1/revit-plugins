using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn;

/// <summary>
///     Dados da alimentação escolhidos pelo engenheiro na janela "Dados do projeto" (NBR 5410 4.2.2, 4.2.3, 4.2.6, 5.4.2.1).
///     O plugin não deduz tensão, esquema nem corrente de curto: sem estes dados confirmados, o cálculo não roda.
/// </summary>
public sealed class DadosProjeto
{
    public const string Chave = "DadosProjeto";

    public double TensaoFaseNeutro { get; set; } = 127;
    public double TensaoFaseFase { get; set; } = 220;
    public int Fases { get; set; } = 2;
    public EsquemaAterramento Esquema { get; set; } = EsquemaAterramento.TNCS;

    /// <summary>Icc trifásica presumida no ponto de entrega, em kA. Nula: critérios de curto não avaliados.</summary>
    public double? IccPresumidaKA { get; set; } = 6;

    public bool LinhaAerea { get; set; } = true;
    public int DiasDeTrovoada { get; set; } = 30;
    public bool TemSpda { get; set; }
    public double TemperaturaAmbiente { get; set; } = 30;
    public bool Habitacao { get; set; } = true;

    // Opções de cálculo (entrega 0.5)
    public bool AplicarFatoresDeDemanda { get; set; } = true;

    /// <summary>Resistência do eletrodo de aterramento, em Ω (só TT). Nula: não avaliada.</summary>
    public double? ResistenciaDeAterramentoOhm { get; set; }

    public double FolgaDeComprimentoPercentual { get; set; }
    public double FolgaDeOcupacaoPercentual { get; set; }
    public bool PEComumNoConduto { get; set; }

    public static DadosProjeto? Carregar(Document doc) => ArmazenamentoEletrica.Ler<DadosProjeto>(doc, Chave);

    public Alimentacao CriarAlimentacao() => new(TensaoFaseNeutro, TensaoFaseFase, Fases, Esquema)
    {
        IccPresumidaKA = IccPresumidaKA,
        LinhaAerea = LinhaAerea,
        DiasDeTrovoada = DiasDeTrovoada,
        TemSpda = TemSpda,
        TemperaturaAmbiente = TemperaturaAmbiente,
        Habitacao = Habitacao,
        AplicarFatoresDeDemanda = AplicarFatoresDeDemanda,
        ResistenciaDeAterramentoOhm = ResistenciaDeAterramentoOhm,
        FolgaDeComprimentoPercentual = FolgaDeComprimentoPercentual,
        FolgaDeOcupacaoPercentual = FolgaDeOcupacaoPercentual,
        PEComumNoConduto = PEComumNoConduto,
    };

    public string Resumo() =>
        $"{TensaoFaseNeutro:0}/{TensaoFaseFase:0} V, {Fases}F, {NomeEsquema(Esquema)}, Icc {(IccPresumidaKA is { } i ? i.ToString("0.#") + " kA" : "—")}, " +
        $"{(LinhaAerea ? "aérea" : "subterrânea")}, {DiasDeTrovoada} dias de trovoada{(TemSpda ? ", SPDA" : "")}, {(Habitacao ? "habitação" : "não residencial")}";

    public static string NomeEsquema(EsquemaAterramento e) => e switch
    {
        EsquemaAterramento.TNS => "TN-S",
        EsquemaAterramento.TNC => "TN-C",
        EsquemaAterramento.TNCS => "TN-C-S",
        EsquemaAterramento.TT => "TT",
        _ => "IT",
    };
}
