using Spk.Motor.Modelo;

namespace Spk.Motor.Normas;

/// <summary>NBR 4 e 8.3. Armazenamento segue a IT-24 (ou a ABNT NBR 13792).</summary>
public enum ClasseRisco { Leve, Ordinario1, Ordinario2, Extraordinario1, Extraordinario2, Armazenamento }

public enum TuboFatorC
{
    FerroFundidoSemRevestimento,
    AcoPretoSistemaSeco,
    AcoPretoSistemaMolhado,
    Galvanizado,
    PlasticoCertificado,
    FerroFundidoComCimento,
    CobreOuInox,
}

/// <summary>Valores numéricos da ABNT NBR 10897:2020 usados pelo motor. Cada um cita o item de origem.</summary>
public static class Nbr10897
{
    public const string Documento = "ABNT NBR 10897:2020";

    /// <summary>9.4.4.10.</summary>
    public const double PressaoMinimaChuveiro = 48;

    /// <summary>9.4.4.11 (só risco extra ou extraordinário).</summary>
    public const double PressaoMaximaRiscoExtra = 1210;

    /// <summary>9.4.3.1.</summary>
    public const double ToleranciaBalanceamento = 1.0;

    /// <summary>9.4.4.2: lado paralelo aos ramais ≥ 1,2 × √área.</summary>
    public const double FatorLadoParaleloAosRamais = 1.2;

    /// <summary>8.5.5.4: inclinação a partir da qual a área cresce 30 %.</summary>
    public const double InclinacaoTetoLimite = 0.167;

    /// <summary>8.5.5.3.1 c).</summary>
    public const double PeDireitoMaximoRespostaRapida = 6.1;

    public static bool EhRiscoExtra(ClasseRisco c) => c is ClasseRisco.Extraordinario1 or ClasseRisco.Extraordinario2;

    /// <summary>Tabela 8 (cálculo hidráulico): área máxima servida por uma coluna (um VGA) por pavimento, em m².</summary>
    public static double AreaMaximaPorColuna(ClasseRisco c) => c switch
    {
        ClasseRisco.Leve or ClasseRisco.Ordinario1 or ClasseRisco.Ordinario2 => 4800,
        _ => 3700,
    };

    /// <summary>7.3.3: área total máxima de um sistema que mistura risco extra/armazenamento com leve/ordinário.</summary>
    public const double AreaMaximaSistemaMisto = 4800;

    /// <summary>Tabela 29: demanda de hidrantes (L/min) e duração do abastecimento (min).</summary>
    public static (double Vazao, double DuracaoMinutos) DemandaHidrantes(ClasseRisco c) => c switch
    {
        ClasseRisco.Leve => (380, 30),
        ClasseRisco.Ordinario1 or ClasseRisco.Ordinario2 => (950, 60),
        ClasseRisco.Extraordinario1 or ClasseRisco.Extraordinario2 => (1900, 90),
        _ => throw new ArgumentException("Armazenamento: consultar a IT-24 ou a ABNT NBR 13792."),
    };

    /// <summary>Tabela 30 (tubos novos).</summary>
    public static double FatorC(TuboFatorC tubo) => tubo switch
    {
        TuboFatorC.FerroFundidoSemRevestimento => 100,
        TuboFatorC.AcoPretoSistemaSeco => 100,
        TuboFatorC.AcoPretoSistemaMolhado => 120,
        TuboFatorC.Galvanizado => 120,
        TuboFatorC.PlasticoCertificado => 150,
        TuboFatorC.FerroFundidoComCimento => 140,
        TuboFatorC.CobreOuInox => 150,
        _ => throw new ArgumentOutOfRangeException(nameof(tubo)),
    };

    /// <summary>9.4.1.2: DN 25 para material ferroso, DN 20 para cobre e não metálicos.</summary>
    public static int DiametroNominalMinimo(Material material) => material == Material.Aco ? 25 : 20;
}
