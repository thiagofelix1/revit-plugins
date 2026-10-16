namespace Spk.Motor.Calculo;

/// <param name="Folga">Quanto a vazão supera a exigida (0 = exatamente no limite).</param>
public sealed record ResultadoChuveiro(
    string Id, string No, string Ramal, double FatorK, double AreaCobertura,
    double Pressao, double Vazao, double VazaoMinima, double PressaoMinima, double Folga);

/// <summary>Resultado de um trecho, já orientado no sentido do fluxo (Montante → Jusante).</summary>
public sealed record ResultadoTrecho(
    string Id, string Montante, string Jusante, double Vazao, int? DiametroNominal, double DiametroInterno, double C,
    double Comprimento, double ComprimentoEquivalente, string Conexoes,
    double PerdaUnitaria, double PerdaAtrito, double PerdaElevacao,
    double PressaoMontante, double PressaoJusante, double Velocidade)
{
    public double ComprimentoTotal => Comprimento + ComprimentoEquivalente;
}

public sealed record ResultadoDemanda(
    EstadoRede Estado,
    IReadOnlyList<ResultadoChuveiro> Chuveiros,
    string ChuveiroCritico,
    IReadOnlyList<ResultadoTrecho> Trechos,
    int AvaliacoesDoSolver)
{
    /// <summary>Pressão necessária na fonte, em kPa.</summary>
    public double PressaoNecessaria => Estado.PressaoFonte;

    /// <summary>Vazão que sai da fonte (chuveiros e demandas fixas da rede), em L/min.</summary>
    public double VazaoNecessaria => Estado.VazaoFonte;

    public double VazaoChuveiros => Chuveiros.Sum(c => c.Vazao);
}
