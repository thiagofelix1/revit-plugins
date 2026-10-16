namespace Spk.Motor.Calculo;

/// <summary>
/// Curva pressão × vazão disponível na fonte (bomba ou rede pública). Entre os pontos, interpola
/// linearmente em Q^1,85, que é a escala do gráfico de abastecimento da NBR 9.3.4.
/// </summary>
public sealed class CurvaAbastecimento
{
    public CurvaAbastecimento(IEnumerable<(double Vazao, double Pressao)> pontos, string descricao)
    {
        Pontos = pontos.OrderBy(p => p.Vazao).ToList();
        Descricao = descricao;
        if (Pontos.Count < 2) throw new ArgumentException("A curva precisa de pelo menos dois pontos.");
        if (Pontos[0].Vazao < 0) throw new ArgumentException("Vazão negativa na curva.");
    }

    public IReadOnlyList<(double Vazao, double Pressao)> Pontos { get; }
    public string Descricao { get; }

    /// <summary>Curva a partir de um ensaio: pressão estática e pressão residual medida com uma vazão de teste.</summary>
    public static CurvaAbastecimento DeEnsaio(double pressaoEstatica, double pressaoResidual, double vazaoEnsaio) =>
        new([(0, pressaoEstatica), (vazaoEnsaio, pressaoResidual)],
            $"Ensaio: estática {pressaoEstatica:0} kPa, residual {pressaoResidual:0} kPa a {vazaoEnsaio:0} L/min");

    /// <summary>Converte altura manométrica de catálogo de bomba (m.c.a.) para kPa.</summary>
    public static double McaParaKPa(double metrosColunaAgua) => metrosColunaAgua * Hidraulica.PesoEspecificoAgua;

    public bool Extrapola(double vazao) => vazao > Pontos[Pontos.Count - 1].Vazao;

    public double PressaoDisponivel(double vazao)
    {
        static double X(double q) => Math.Pow(q, Hidraulica.ExpoenteHazenWilliams);
        var k = 1;
        while (k < Pontos.Count - 1 && vazao > Pontos[k].Vazao) k++;
        var (q0, p0) = Pontos[k - 1];
        var (q1, p1) = Pontos[k];
        var t = (X(vazao) - X(q0)) / (X(q1) - X(q0));
        return p0 + t * (p1 - p0);
    }
}
