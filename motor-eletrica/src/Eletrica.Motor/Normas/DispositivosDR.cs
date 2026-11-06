using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Normas;

/// <summary>
/// Dispositivos DR (IEC 61008-1, IEC 61009-1, IEC 62423) e as obrigações da NBR 5410 (5.1.3.2.2, 9.1, 9.2). Os valores
/// das normas IEC vêm do mapa provisório <c>nr/normas/dr-iec-61008-61009.md</c>; conferir quando as ABNT NBR IEC chegarem.
/// </summary>
public static class DispositivosDR
{
    public const string Documento = "IEC 61008-1 / 61009-1; ABNT NBR 5410 5.1.3.2.2";

    /// <summary>Correntes diferenciais-residuais nominais padronizadas, em mA.</summary>
    public static readonly double[] CorrentesResiduais = [10, 30, 100, 300, 500];

    /// <summary>Correntes nominais padronizadas, em A.</summary>
    public static readonly double[] CorrentesNominais = [16, 25, 40, 63, 80, 100, 125];

    /// <summary>5.1.3.2.2: proteção adicional de pessoas, em mA.</summary>
    public const double AltaSensibilidade = 30;

    /// <summary>IEC 61008-1 5.3.x: o DR pode atuar a partir de 0,5 IΔn; a fuga permanente a jusante deve ficar abaixo disso (prática: 0,3 IΔn).</summary>
    public const double FatorDeNaoAtuacao = 0.5;
    public const double FatorDeFugaRecomendado = 0.3;

    /// <summary>IEC 61008-1 Tabela 1: tempos máximos de atuação do tipo geral, em s, a IΔn, 2 IΔn e 5 IΔn.</summary>
    public static readonly (double Multiplo, double Tempo)[] TemposTipoGeral = [(1, 0.3), (2, 0.15), (5, 0.04)];

    /// <summary>IEC 61008-1 Tabela 1: tempos máximos (e mínimos de não atuação) do tipo S, em s.</summary>
    public static readonly (double Multiplo, double Maximo, double MinimoNaoAtuacao)[] TemposTipoS = [(1, 0.5, 0.13), (2, 0.2, 0.06), (5, 0.15, 0.05)];

    /// <summary>Seletividade entre DRs em série: o de montante tipo S com IΔn pelo menos 3× o de jusante (prática de catálogo e 6.3.3.2.3).</summary>
    public const double RazaoDeSeletividade = 3;

    /// <summary>Número de circuitos num mesmo DR de 30 mA acima do qual o Verificar modelo avisa (desarme intempestivo por fuga acumulada).</summary>
    public const int CircuitosPorDRRecomendados = 6;

    /// <summary>
    /// 5.1.3.2.2: o ponto exige DR de 30 mA? Em habitação, todos os pontos dos locais molhados (alínea d), menos luminárias
    /// a 2,50 m ou mais; em não residencial, só tomadas (alínea e). Banheiros (a), áreas externas (b) e tomadas que podem
    /// alimentar equipamento externo (c) em qualquer caso.
    /// </summary>
    public static string? MotivoDeDR(Ponto ponto, Ambiente ambiente, bool habitacao)
    {
        var tomada = ponto.Tipo is TipoCarga.TomadaUsoGeral or TipoCarga.TomadaUsoEspecifico or TipoCarga.ArCondicionado or TipoCarga.VeiculoEletrico;
        if (ambiente.Banho) return "5.1.3.2.2 a) — ponto em local com banheira ou chuveiro";
        if (ambiente.Uso == UsoAmbiente.AreaExterna) return tomada ? "5.1.3.2.2 b) — tomada em área externa" : "5.1.3.2.2 d) — ponto em área externa";
        if (ponto.Tipo == TipoCarga.Chuveiro) return "5.1.3.2.2 a) — chuveiro ou aquecedor em banheiro";
        if (!ambiente.Molhado) return null;
        if (habitacao)
        {
            if (ponto.Tipo == TipoCarga.Iluminacao && ponto.Altura >= Nbr5410.AlturaQueDispensaDR) return null;
            return "5.1.3.2.2 d) — ponto em cozinha, copa, área de serviço, lavanderia, garagem ou local molhado de habitação";
        }
        return tomada ? "5.1.3.2.2 e) — tomada em local molhado ou sujeito a lavagem" : null;
    }

    /// <summary>Tipo de DR sugerido pelo tipo de carga: A como padrão onde há eletrônica, B para VE, F para inversores monofásicos.</summary>
    public static TipoDr TipoSugerido(TipoCarga tipo) => tipo switch
    {
        TipoCarga.VeiculoEletrico => TipoDr.B,
        TipoCarga.ArCondicionado => TipoDr.F,
        TipoCarga.Chuveiro or TipoCarga.Aquecimento => TipoDr.AC,
        _ => TipoDr.A,
    };

    /// <summary>Menor In padronizado do DR não inferior à corrente, em A.</summary>
    public static double? MenorInNaoInferiorA(double corrente)
    {
        foreach (var inA in CorrentesNominais)
            if (inA >= corrente - 1e-9) return inA;
        return null;
    }
}
