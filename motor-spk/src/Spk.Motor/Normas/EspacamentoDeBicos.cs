namespace Spk.Motor.Normas;

/// <summary>Tipo de teto da Tabela 2 da IT-23/2025 (o mesmo da NBR 10897).</summary>
public enum TipoDeTeto
{
    /// <summary>Incombustível, obstruído ou não; ou combustível não obstruído.</summary>
    IncombustivelOuNaoObstruido,

    /// <summary>Combustível obstruído.</summary>
    CombustivelObstruido,

    /// <summary>Combustível com elementos estruturais a menos de 0,90 m entre si.</summary>
    CombustivelComElementosProximos,
}

/// <summary>Limites de posição de um bico: área, espaçamento, distâncias à parede e do defletor ao teto, em m e m².</summary>
public sealed record LimitesDeBico(
    double AreaMaxima,
    double DistanciaMaxima,
    double DistanciaMinima,
    double ParedeMinima,
    double DefletorMinimo,
    double DefletorMaximo,
    string Fonte)
{
    /// <summary>8.12.2.2.1: até a parede, metade da distância máxima entre bicos.</summary>
    public double ParedeMaxima => DistanciaMaxima / 2;

    /// <summary>8.12.2.2.2: paredes irregulares: nenhum ponto do piso a mais de ¾ da distância máxima do bico.</summary>
    public double AlcanceMaximo => 0.75 * DistanciaMaxima;
}

/// <summary>
/// Espaçamento de chuveiros spray em pé e pendentes de cobertura-padrão: IT-23/2025 (CBPMESP), 8.11 a 8.13, Tabela 2,
/// que reproduz a ABNT NBR 10897. Cobertura estendida, laterais, CCAE e ESFR ficam para depois.
/// </summary>
public static class EspacamentoDeBicos
{
    public const string Fonte = "IT-23/2025 (CBPMESP), 8.11 a 8.13 e Tabela 2";

    /// <summary>Tabela 2, nota a e b: risco extra com densidade a partir de 10,2 mm/min (L/min/m²).</summary>
    public const double DensidadeLimiteExtra = 10.2;

    /// <summary>8.13.1.2: com vigas, o defletor fica entre 25 e 150 mm abaixo do fundo da viga e a no máximo 560 mm do teto.</summary>
    public const double DefletorAbaixoDaVigaMinimo = 0.025;
    public const double DefletorAbaixoDaVigaMaximo = 0.150;
    public const double DefletorMaximoComViga = 0.560;

    /// <param name="porTabela">Sistema dimensionado por tabela (senão, cálculo hidráulico).</param>
    /// <param name="densidade">L/min/m²; só pesa no risco extra calculado.</param>
    public static LimitesDeBico Padrao(ClasseRisco classe, TipoDeTeto teto = TipoDeTeto.IncombustivelOuNaoObstruido,
        bool porTabela = false, double densidade = 0)
    {
        if (classe == ClasseRisco.Armazenamento)
            throw new ArgumentException("Armazenamento segue a IT-24 (ou CCAE/ESFR): o espaçamento de bico padrão da Tabela 2 não se aplica.");
        var extra = Nbr10897.EhRiscoExtra(classe);
        var leve = classe == ClasseRisco.Leve;
        var densidadeAlta = densidade >= DensidadeLimiteExtra;

        double area = (teto, porTabela) switch
        {
            (TipoDeTeto.IncombustivelOuNaoObstruido, true) => leve ? 18.6 : extra ? 8.4 : 12.1,
            (TipoDeTeto.IncombustivelOuNaoObstruido, false) => leve ? 20.9 : extra ? (densidadeAlta ? 9.3 : 12.1) : 12.1,
            (TipoDeTeto.CombustivelObstruido, true) => leve ? 15.6 : extra ? 8.4 : 12.1,
            (TipoDeTeto.CombustivelObstruido, false) => leve ? 15.6 : extra ? (densidadeAlta ? 9.3 : 12.1) : 12.1,
            (_, true) => leve ? 12.1 : extra ? 8.4 : 12.1,
            _ => leve ? 12.1 : extra ? (densidadeAlta ? 9.3 : 12.1) : 12.1,
        };
        var distancia = !extra ? 4.6 : porTabela || densidadeAlta ? 3.7 : 4.6;
        return new LimitesDeBico(area, distancia, DistanciaMinima: 1.8, ParedeMinima: 0.10, DefletorMinimo: 0.025,
            DefletorMaximo: 0.300, Fonte);
    }
}
