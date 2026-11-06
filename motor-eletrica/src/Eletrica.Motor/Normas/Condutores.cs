using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Normas;

/// <summary>
/// Dados físicos dos condutores: diâmetros externos da ABNT NBR NM 247-3:2002 (Tabelas 1 e 3) para condutores isolados
/// 450/750 V, temperaturas de regime das normas de cabo (NM 247-3, NBR 7286, 7287, 7288, 13248) e resistividade do cobre
/// e do alumínio. Os diâmetros dos cabos 0,6/1 kV são valores típicos de catálogo (a substituir pelo fabricante do escritório).
/// </summary>
public static class Condutores
{
    /// <summary>Seções comerciais usadas em projeto predial, em mm² (Tabela 47 e 6.2.3.7: cobre a partir de 1,5; alumínio a partir de 16).</summary>
    public static readonly double[] SecoesDeProjeto = [1.5, 2.5, 4, 6, 10, 16, 25, 35, 50, 70, 95, 120, 150, 185, 240, 300, 400, 500];

    /// <summary>Resistividade a 20 °C, em Ω·mm²/m (IEC 60228: cobre 1/58 ≈ 0,017241; alumínio 1/36 ≈ 0,028264).</summary>
    public static double Resistividade20(MaterialCondutor material) => material == MaterialCondutor.Cobre ? 0.017241 : 0.028264;

    /// <summary>Coeficiente de temperatura da resistência, por K.</summary>
    public static double CoeficienteDeTemperatura(MaterialCondutor material) => material == MaterialCondutor.Cobre ? 0.00393 : 0.00403;

    /// <summary>Resistência por metro de um condutor, em Ω/m, na temperatura dada.</summary>
    public static double ResistenciaPorMetro(MaterialCondutor material, double secao, double temperatura) =>
        Resistividade20(material) * (1 + CoeficienteDeTemperatura(material) * (temperatura - 20)) / secao;

    /// <summary>
    /// Reatância indutiva por metro, em Ω/m, para condutores em eletroduto ou cabos justapostos (valor típico de catálogo
    /// e da IEC 60364-5-52, Anexo G: 0,08 mΩ/m). Só pesa em seções grandes.
    /// </summary>
    public const double ReatanciaPorMetro = 0.00008;

    /// <summary>Temperatura máxima em serviço contínuo (Tabela 35 da 5410; NM 247-3 2.5; NBR 7286/7287 4.2; NBR 7288 4.2).</summary>
    public static double TemperaturaMaxima(Isolacao isolacao) => isolacao == Isolacao.Pvc70 ? 70 : 90;

    /// <summary>Temperatura limite de curto-circuito (Tabela 35; NBR 7288 4.4: 160 °C; NBR 7286/7287 4.4: 250 °C).</summary>
    public static double TemperaturaDeCurto(Isolacao isolacao) => isolacao == Isolacao.Pvc70 ? 160 : 250;

    /// <summary>Temperatura de sobrecarga (NBR 7288 4.3: 100 °C; 7286/7287 4.3: 130 °C).</summary>
    public static double TemperaturaDeSobrecarga(Isolacao isolacao) => isolacao == Isolacao.Pvc70 ? 100 : 130;

    /// <summary>
    /// Temperatura de operação estimada com a corrente de projeto (IEC 60364-5-52 Anexo G): θ = θamb + (θmax − 30) × (IB/IZ)².
    /// Usada na resistência para a queda de tensão.
    /// </summary>
    public static double TemperaturaDeOperacao(Isolacao isolacao, double ambiente, double ib, double iz)
    {
        if (iz <= 0) return TemperaturaMaxima(isolacao);
        var razao = Math.Min(1.0, ib / iz);
        return ambiente + (TemperaturaMaxima(isolacao) - 30) * razao * razao;
    }

    // NM 247-3, Tabela 1 (tipo 01, rígido classe 1 ou 2) e Tabela 3 (tipo 02, flexível classe 5): diâmetro externo máximo, em mm.
    static readonly (double Secao, double Rigido, double Flexivel)[] Diametros450750 =
    [
        (0.5, 2.7, 2.5), (0.75, 2.7, 2.7), (1, 2.8, 2.8), (1.5, 3.3, 3.4), (2.5, 4.0, 4.1), (4, 4.6, 4.8), (6, 5.2, 5.3), (10, 6.7, 6.8),
        (16, 7.8, 8.1), (25, 9.7, 10.2), (35, 10.9, 11.7), (50, 12.8, 13.9), (70, 14.6, 16.0), (95, 17.1, 18.2), (120, 18.8, 20.2),
        (150, 20.9, 22.5), (185, 23.3, 24.9), (240, 26.6, 28.4), (300, 29.6, 31.7), (400, 33.2, 36.0), (500, 36.9, 40.1), (630, 41.5, 46.1),
    ];

    // Cabo unipolar 0,6/1 kV EPR/XLPE: diâmetro externo típico de catálogo (Prysmian/Nexans), em mm. Substituir pelo catálogo do escritório.
    static readonly (double Secao, double Diametro)[] Diametros061kV =
    [
        (1.5, 5.5), (2.5, 6.0), (4, 6.6), (6, 7.2), (10, 8.4), (16, 9.6), (25, 11.5), (35, 12.7), (50, 14.5), (70, 16.5), (95, 18.5),
        (120, 20.5), (150, 22.5), (185, 25.0), (240, 28.0), (300, 31.0), (400, 35.0), (500, 39.0),
    ];

    /// <summary>Diâmetro externo máximo, em mm, para a taxa de ocupação (6.2.11.1.6). Condutor isolado 450/750 V em PVC; cabo 0,6/1 kV nas outras isolações.</summary>
    public static double DiametroExterno(double secao, Isolacao isolacao, bool flexivel = true)
    {
        if (isolacao == Isolacao.Pvc70)
        {
            foreach (var (s, rigido, flex) in Diametros450750)
                if (Math.Abs(s - secao) < 1e-9) return flexivel ? flex : rigido;
        }
        else
        {
            foreach (var (s, d) in Diametros061kV)
                if (Math.Abs(s - secao) < 1e-9) return d;
        }
        throw new ArgumentException($"Sem diâmetro externo para {secao} mm² em {isolacao}.");
    }

    /// <summary>Área da seção externa do condutor, em mm².</summary>
    public static double AreaExterna(double secao, Isolacao isolacao, bool flexivel = true)
    {
        var d = DiametroExterno(secao, isolacao, flexivel);
        return Math.PI * d * d / 4;
    }

    /// <summary>Norma que descreve o condutor, para a lista de material.</summary>
    public static string NormaDoCabo(Isolacao isolacao) => isolacao switch
    {
        Isolacao.Pvc70 => "ABNT NBR NM 247-3 (450/750 V, PVC 70 °C)",
        Isolacao.Epr90 => "ABNT NBR 7286 (0,6/1 kV, EPR 90 °C)",
        _ => "ABNT NBR 7287 (0,6/1 kV, XLPE 90 °C)",
    };
}
