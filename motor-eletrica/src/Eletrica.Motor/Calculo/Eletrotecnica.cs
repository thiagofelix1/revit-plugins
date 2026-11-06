using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Calculo;

/// <summary>Fórmulas elementares: corrente de projeto, queda de tensão, impedâncias e curto-circuito pelo método das impedâncias.</summary>
public static class Eletrotecnica
{
    /// <summary>
    /// Corrente de projeto IB (4.2.1): potência aparente dividida pela tensão do esquema. Em duas fases com neutro admite-se
    /// carga repartida entre as duas fases; em trifásico, carga equilibrada.
    /// </summary>
    public static double CorrenteDeProjeto(double potenciaVA, Ligacao ligacao, double uo, double uff, double fatorDePotencia = 1, double rendimento = 1)
    {
        var s = potenciaVA / rendimento;
        return ligacao switch
        {
            Ligacao.FN => s / uo,
            Ligacao.FF => s / uff,
            Ligacao.FFN => s / (2 * uo),
            Ligacao.FFF or Ligacao.FFFN => s / (Math.Sqrt(3) * uff),
            _ => throw new ArgumentOutOfRangeException(nameof(ligacao)),
        };
    }

    /// <summary>Tensão de referência da queda e da corrente: fase-neutro nos circuitos com neutro de uma fase, fase-fase nos demais.</summary>
    public static double TensaoDoCircuito(Ligacao ligacao, double uo, double uff) => ligacao == Ligacao.FN ? uo : uff;

    /// <summary>Fator do circuito na queda de tensão: 2 (ida e volta) em monofásico e bifásico, √3 em trifásico.</summary>
    public static double FatorDeQueda(Ligacao ligacao) => ligacao is Ligacao.FFF or Ligacao.FFFN ? Math.Sqrt(3) : 2;

    /// <summary>
    /// Queda de tensão em %, 6.2.7: ΔU = b · IB · L · (R cos φ + X sen φ) / U × 100, com R na temperatura de operação.
    /// </summary>
    public static double QuedaDeTensaoPercentual(double ib, double comprimento, double rPorMetro, double xPorMetro, double fatorDePotencia, Ligacao ligacao, double tensao)
    {
        var seno = Math.Sqrt(Math.Max(0, 1 - fatorDePotencia * fatorDePotencia));
        var queda = FatorDeQueda(ligacao) * ib * comprimento * (rPorMetro * fatorDePotencia + xPorMetro * seno);
        return 100 * queda / tensao;
    }

    /// <summary>Impedância série R + jX, em Ω.</summary>
    public readonly record struct Impedancia(double R, double X)
    {
        public double Modulo => Math.Sqrt(R * R + X * X);
        public static Impedancia operator +(Impedancia a, Impedancia b) => new(a.R + b.R, a.X + b.X);
        public static Impedancia operator *(Impedancia a, double k) => new(a.R * k, a.X * k);
        public static readonly Impedancia Zero = new(0, 0);
    }

    /// <summary>Relação X/R admitida para a rede a montante do ponto de entrega quando só a corrente de curto é conhecida.</summary>
    public const double RelacaoXRDaRede = 3.0;

    /// <summary>Fator de tensão do método das impedâncias: 1,05 para o curto máximo e 0,95 para o mínimo (IEC 60909, Tabela 1, baixa tensão).</summary>
    public const double FatorDeTensaoMaximo = 1.05;
    public const double FatorDeTensaoMinimo = 0.95;

    /// <summary>Resistência a quente no curto mínimo: 1,5 × a resistência a 20 °C (IEC 61200-53 e guia da IEC 60364, método da composição).</summary>
    public const double FatorDeResistenciaAQuente = 1.5;

    /// <summary>Impedância equivalente da rede no ponto de entrega a partir da Icc trifásica presumida, por fase.</summary>
    public static Impedancia ImpedanciaDaRede(double iccKA, double uo)
    {
        var z = uo / (iccKA * 1000);
        var r = z / Math.Sqrt(1 + RelacaoXRDaRede * RelacaoXRDaRede);
        return new Impedancia(r, r * RelacaoXRDaRede);
    }

    /// <summary>Impedância de um condutor, em Ω, a 20 °C.</summary>
    public static Impedancia ImpedanciaDoCondutor(MaterialCondutor material, double secao, double comprimento) =>
        new(Condutores.ResistenciaPorMetro(material, secao, 20) * comprimento, Condutores.ReatanciaPorMetro * comprimento);

    /// <summary>Corrente de curto trifásica máxima no fim de uma impedância por fase, em A.</summary>
    public static double CurtoMaximo(Impedancia porFase, double uo) => FatorDeTensaoMaximo * uo / porFase.Modulo;

    /// <summary>
    /// Corrente de curto mínima fase-neutro ou fase-PE, em A: tensão fase-neutro com fator 0,95, rede a montante mais as
    /// resistências de ida (fase) e volta (neutro ou PE) a quente (×1,5) e as reatâncias. É o Ikmín de Zs·Ia ≤ Uo (5.1.2.2.4.2).
    /// </summary>
    public static double CurtoMinimo(Impedancia rede, Impedancia ida, Impedancia volta, double uo)
    {
        var total = new Impedancia(rede.R + FatorDeResistenciaAQuente * (ida.R + volta.R), rede.X + ida.X + volta.X);
        return FatorDeTensaoMinimo * uo / total.Modulo;
    }

    /// <summary>Impedância do percurso de falta Zs (rede + fase + PE, a quente), em Ω.</summary>
    public static double ImpedanciaDeFalta(Impedancia rede, Impedancia fase, Impedancia pe) =>
        new Impedancia(rede.R + FatorDeResistenciaAQuente * (fase.R + pe.R), rede.X + fase.X + pe.X).Modulo;
}
