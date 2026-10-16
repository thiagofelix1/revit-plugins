namespace Spk.Motor.Normas;

/// <summary>Reta de uma curva da Figura 42 (densidade em L/min/m², área em m²).</summary>
public sealed record CurvaDensidadeArea(
    ClasseRisco Classe, double AreaMinima, double DensidadeNaAreaMinima, double AreaMaxima, double DensidadeNaAreaMaxima)
{
    /// <summary>Densidade para a área escolhida. Abaixo da área mínima vale a densidade da mínima (8.5.4 a e b).</summary>
    public double Densidade(double area)
    {
        if (area <= AreaMinima) return DensidadeNaAreaMinima;
        if (area > AreaMaxima + 1e-9)
            throw new ArgumentOutOfRangeException(nameof(area), $"Área {area} m² acima da curva ({AreaMaxima} m²).");
        var t = (area - AreaMinima) / (AreaMaxima - AreaMinima);
        return DensidadeNaAreaMinima + t * (DensidadeNaAreaMaxima - DensidadeNaAreaMinima);
    }
}

public static class Figura42
{
    // Extremos das retas lidos no gráfico da NBR 8.5.2 (equivalentes SI das curvas da NFPA 13).
    // CONFERIR com o projetista: o PDF não traz os valores em tabela.
    public static CurvaDensidadeArea Curva(ClasseRisco classe) => classe switch
    {
        ClasseRisco.Leve => new(classe, 140, 4.1, 280, 2.85),
        ClasseRisco.Ordinario1 => new(classe, 140, 6.1, 370, 4.9),
        ClasseRisco.Ordinario2 => new(classe, 140, 8.1, 370, 6.9),
        ClasseRisco.Extraordinario1 => new(classe, 230, 12.2, 465, 10.2),
        ClasseRisco.Extraordinario2 => new(classe, 230, 16.3, 465, 14.3),
        _ => throw new ArgumentException("Armazenamento não usa a Figura 42: use as curvas e tabelas da IT-24."),
    };
}

public sealed record CondicoesAreaOperacao
{
    public bool ChuveirosRespostaRapida { get; init; }
    public bool SistemaTuboMolhado { get; init; } = true;

    /// <summary>Pé-direito em m; no teto inclinado, a altura máxima (8.5.5.3.3).</summary>
    public double PeDireito { get; init; }

    /// <summary>Inclinação do teto como fração (0,167 = 16,7 %).</summary>
    public double InclinacaoTeto { get; init; }

    public bool AcaoPreviaBloqueioDuplo { get; init; }
    public bool ChuveirosAltaTemperatura { get; init; }
}

public sealed record AjusteAplicado(string Descricao, string Item, double Percentual);

public sealed record AreaOperacaoAjustada(
    double AreaOriginal, double AreaFinal, IReadOnlyList<AjusteAplicado> Ajustes, IReadOnlyList<string> Avisos);

public static class AreaDeOperacao
{
    /// <summary>
    /// Ajustes de 8.5.5.3 a 8.5.5.6, somados sobre a área original (8.5.5.7; IT-24 5.1.8).
    /// A densidade não muda; só a área.
    /// </summary>
    public static AreaOperacaoAjustada Ajustar(double areaOriginal, ClasseRisco classe, CondicoesAreaOperacao condicoes)
    {
        var ajustes = new List<AjusteAplicado>();
        var avisos = new List<string>();

        if (condicoes.ChuveirosRespostaRapida)
        {
            if (Nbr10897.EhRiscoExtra(classe))
                avisos.Add("Chuveiros de resposta rápida não podem ser usados em risco extra (8.5.5.2.2).");
            else if (!condicoes.SistemaTuboMolhado)
                avisos.Add("Redução por resposta rápida exige sistema de tubo molhado (8.5.5.3.1 a); não aplicada.");
            else if (condicoes.PeDireito <= 0)
                avisos.Add("Informe o pé-direito para aplicar a redução por resposta rápida (Figura 43).");
            else if (condicoes.PeDireito > Nbr10897.PeDireitoMaximoRespostaRapida)
                avisos.Add("Pé-direito acima de 6,1 m: sem redução por resposta rápida (8.5.5.3.1 c).");
            else
            {
                var h = condicoes.PeDireito;
                var reducao = h <= 3 ? 40 : 55 - 5 * h;
                ajustes.Add(new AjusteAplicado($"Resposta rápida, pé-direito {h:0.##} m", "8.5.5.3, Figura 43", -reducao));
                avisos.Add("Com resposta rápida, a área de operação precisa ter pelo menos 5 chuveiros (8.5.5.3.2).");
            }
        }

        if (condicoes.InclinacaoTeto > Nbr10897.InclinacaoTetoLimite)
            ajustes.Add(new AjusteAplicado("Teto com inclinação acima de 16,7 %", "8.5.5.4", 30));

        if (condicoes.AcaoPreviaBloqueioDuplo)
            ajustes.Add(new AjusteAplicado("Ação prévia com bloqueio duplo", "8.5.5.5", 30));

        if (condicoes.ChuveirosAltaTemperatura && Nbr10897.EhRiscoExtra(classe))
        {
            var semEste = areaOriginal * (1 + ajustes.Sum(a => a.Percentual) / 100);
            var reducaoM2 = Math.Min(0.25 * areaOriginal, Math.Max(0, semEste - 190));
            if (reducaoM2 > 0)
                ajustes.Add(new AjusteAplicado("Alta temperatura em risco extra (limitada a 190 m²)", "8.5.5.6",
                    -100 * reducaoM2 / areaOriginal));
        }

        var final = areaOriginal * (1 + ajustes.Sum(a => a.Percentual) / 100);
        return new AreaOperacaoAjustada(areaOriginal, final, ajustes, avisos);
    }

    /// <summary>
    /// 9.4.4.2 e Figura 46: total de chuveiros na área e quantos ficam em cada ramal
    /// (lado paralelo aos ramais ≥ 1,2√A). Frações sempre arredondam para cima.
    /// </summary>
    public static (int Total, int PorRamal) NumeroDeChuveiros(double area, double areaPorChuveiro, double espacamentoNoRamal)
    {
        const double eps = 1e-9;
        var total = (int)Math.Ceiling(area / areaPorChuveiro - eps);
        var porRamal = (int)Math.Ceiling(Nbr10897.FatorLadoParaleloAosRamais * Math.Sqrt(area) / espacamentoNoRamal - eps);
        return (total, Math.Min(porRamal, total));
    }
}
