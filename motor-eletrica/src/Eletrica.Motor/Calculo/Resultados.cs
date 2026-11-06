using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Calculo;

/// <summary>Um critério conferido, com o número obtido, o limite, o item da norma e a explicação em uma frase.</summary>
public sealed record Parecer(string Criterio, bool? Atende, string Valor, string Limite, string Item, string Explicacao)
{
    public bool Reprovado => Atende == false;
    public bool NaoAvaliado => Atende is null;
}

public sealed record Disjuntor(double In, CurvaDisjuntor Curva, double IcnKA, int Polos)
{
    public double I2 => Nbr60898.FatorAtuacao * In;
    public double Ia => Nbr60898.CorrenteDeAtuacaoInstantanea(Curva, In);
    public override string ToString() => $"{Curva}{In:0.#} A, {Polos}P, {IcnKA:0.#} kA";
}

public sealed record EspecificacaoDR(bool Obrigatorio, string? Motivo, double CorrenteResidualMA, TipoDr Tipo, double In);

/// <summary>Queda de tensão de um trecho do caminho (alimentador ou terminal), em %.</summary>
public sealed record QuedaParcial(string Circuito, double Comprimento, double Percentual);

public sealed record ResultadoCircuito(
    string Id,
    string Numero,
    string Descricao,
    string Quadro,
    TipoCarga Tipo,
    Ligacao Ligacao,
    double PotenciaVA,
    double CorrenteDeProjeto,
    double Comprimento,
    bool ComprimentoEstimado,
    Isolacao Isolacao,
    MaterialCondutor Material,
    MetodoReferencia MetodoCritico,
    string? TrechoCritico,
    int CondutoresCarregados,
    double FatorDeTemperatura,
    double FatorDeAgrupamento,
    int CircuitosAgrupados,
    double SecaoFase,
    double SecaoNeutro,
    double SecaoPE,
    double CapacidadeDeReferencia,
    double CapacidadeCorrigida,
    Disjuntor Disjuntor,
    double QuedaTerminal,
    double QuedaTotal,
    IReadOnlyList<QuedaParcial> QuedasAMontante,
    double? IccMaximaNaOrigemKA,
    double? IccMinimaNoFimA,
    double? ImpedanciaDeFalta,
    double? EnergiaPassanteA2s,
    double EnergiaSuportadaA2s,
    EspecificacaoDR DR,
    IReadOnlyList<Parecer> Pareceres,
    string? CriterioQueMandou,
    IReadOnlyList<string> Avisos)
{
    public bool Atende => Pareceres.All(p => !p.Reprovado);
    public IEnumerable<Parecer> Reprovados => Pareceres.Where(p => p.Reprovado);

    /// <summary>Fase(s) do circuito no quadro ("A", "B", "AB", "ABC"), pelo balanceamento ou pela fase imposta.</summary>
    public string Fases { get; init; } = "";

    /// <summary>Condutores em paralelo por fase (6.2.5.7); 1 no caso comum.</summary>
    public int CondutoresPorFase { get; init; } = 1;
}

public sealed record CargaPorFase(string Fase, double PotenciaVA, double Corrente);

/// <summary>Um DR do quadro e os circuitos que ele protege (6.3.3.2; no máximo seis por DR para a fuga acumulada não desarmar).</summary>
public sealed record GrupoDR(int Numero, TipoDr Tipo, double CorrenteResidualMA, double In, int Polos, IReadOnlyList<string> Circuitos)
{
    public override string ToString() => $"DR{Numero} {In:0} A / {CorrenteResidualMA:0} mA tipo {Tipo} {Polos}P: {string.Join(", ", Circuitos)}";
}

public sealed record ResultadoQuadro(
    string Id,
    string Nome,
    int Fases,
    int NumeroDeCircuitos,
    int Reserva,
    double PotenciaInstaladaVA,
    double PotenciaDeDemandaVA,
    double CorrenteDeDemanda,
    IReadOnlyList<CargaPorFase> CargasPorFase,
    double DesequilibrioPercentual,
    double? IccNoBarramentoKA,
    Disjuntor? DisjuntorGeral,
    DispositivosDPS.Especificacao? Dps,
    string? MotivoDoDps,
    IReadOnlyList<string> CircuitosComDR,
    IReadOnlyList<string> Avisos,
    ResultadoDemanda? Demanda = null,
    IReadOnlyList<GrupoDR>? GruposDR = null);

public sealed record ResultadoTrecho(
    string Id,
    string De,
    string Para,
    double Comprimento,
    int Curvas90,
    TipoEletroduto TipoEletroduto,
    int? DnInformado,
    int? DnSugerido,
    int Condutores,
    double AreaDosCondutores,
    double? TaxaDeOcupacao,
    double TaxaMaxima,
    double ComprimentoEquivalente,
    double ComprimentoMaximo,
    IReadOnlyList<Parecer> Pareceres,
    int? RepeticoesSugeridas = null)
{
    public bool Atende => Pareceres.All(p => !p.Reprovado);
}

/// <summary>Um problema apontado pelo Verificar modelo: código estável, gravidade, item da norma e os elementos envolvidos.</summary>
public sealed record Problema(string Codigo, Gravidade Gravidade, string Mensagem, string Item, IReadOnlyList<string> Elementos);

public enum Gravidade { Erro, Aviso, Nota }

public sealed record ResultadoInstalacao(
    string Nome,
    Alimentacao Alimentacao,
    IReadOnlyList<ResultadoCircuito> Circuitos,
    IReadOnlyList<ResultadoQuadro> Quadros,
    IReadOnlyList<ResultadoTrecho> Trechos,
    IReadOnlyList<Problema> Problemas,
    TimeSpan Duracao)
{
    public bool Atende => Circuitos.All(c => c.Atende) && Trechos.All(t => t.Atende) && Problemas.All(p => p.Gravidade != Gravidade.Erro);
}
