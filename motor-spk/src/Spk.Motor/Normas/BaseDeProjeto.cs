using Spk.Motor.Calculo;
using Spk.Motor.Modelo;

namespace Spk.Motor.Normas;

/// <summary>O critério que define quantos chuveiros abrem e o que cada um tem que entregar.</summary>
public abstract record BaseDeProjeto(string Descricao, string Referencia)
{
    /// <summary>Total de chuveiros na área de operação e quantos por ramal.</summary>
    public abstract (int Total, int PorRamal) Geometria(double areaPorChuveiro, double espacamentoNoRamal);

    public abstract RequisitoChuveiro Requisito(Chuveiro chuveiro);

    /// <summary>
    /// Método de densidade e área (NBR 8.5.5) com a Figura 42. A densidade vem da área escolhida;
    /// os ajustes mudam só a área (8.5.5.3 a 8.5.5.7).
    /// </summary>
    public static BaseDensidadeArea PelaFigura42(ClasseRisco classe, double areaEscolhida, CondicoesAreaOperacao? condicoes = null)
    {
        var curva = Figura42.Curva(classe);
        var densidade = curva.Densidade(areaEscolhida);
        var ajustada = AreaDeOperacao.Ajustar(Math.Max(areaEscolhida, curva.AreaMinima), classe, condicoes ?? new CondicoesAreaOperacao());
        return new BaseDensidadeArea(densidade, ajustada.AreaFinal)
        {
            Classe = classe,
            Ajuste = ajustada,
        };
    }

    /// <summary>
    /// Chuveiros ESFR em depósitos (IT-24 5.2.2.3.3 e equivalentes): 12 chuveiros, 4 em cada um de 3 ramais,
    /// área mínima de 89 m². A pressão mínima vem da tabela da IT para a mercadoria, a altura e o fator K.
    /// </summary>
    public static BaseNumeroFixo Esfr(double pressaoMinima, string itemDaTabela) =>
        new(12, 4, pressaoMinima, 89, "ESFR: 12 chuveiros (4 em 3 ramais)", $"IT-24 5.2.2.3.3; {itemDaTabela}");
}

public sealed record BaseDensidadeArea(double Densidade, double AreaOperacao)
    : BaseDeProjeto($"Densidade {Densidade:0.##} L/min/m² sobre {AreaOperacao:0.#} m²", "NBR 10897 8.5.5, Figura 42")
{
    public ClasseRisco? Classe { get; init; }
    public AreaOperacaoAjustada? Ajuste { get; init; }

    public override (int Total, int PorRamal) Geometria(double areaPorChuveiro, double espacamentoNoRamal) =>
        AreaDeOperacao.NumeroDeChuveiros(AreaOperacao, areaPorChuveiro, espacamentoNoRamal);

    /// <summary>9.4.4.6: vazão ≥ densidade × área do chuveiro; 9.4.4.10: pressão ≥ 48 kPa ou a de catálogo.</summary>
    public override RequisitoChuveiro Requisito(Chuveiro chuveiro) =>
        new(chuveiro.Id, Densidade * chuveiro.AreaCobertura,
            Math.Max(Nbr10897.PressaoMinimaChuveiro, chuveiro.PressaoMinima ?? 0));
}

public sealed record BaseNumeroFixo(int Total, int PorRamal, double PressaoMinima, double AreaMinima, string Descricao, string Referencia)
    : BaseDeProjeto(Descricao, Referencia)
{
    public override (int Total, int PorRamal) Geometria(double areaPorChuveiro, double espacamentoNoRamal) => (Total, PorRamal);

    public override RequisitoChuveiro Requisito(Chuveiro chuveiro) =>
        new(chuveiro.Id, 0, Math.Max(PressaoMinima, chuveiro.PressaoMinima ?? 0));
}
