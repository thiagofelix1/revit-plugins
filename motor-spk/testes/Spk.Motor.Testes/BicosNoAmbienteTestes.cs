using Spk.Motor.Geracao;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Testes;

public class BicosNoAmbienteTestes
{
    static IReadOnlyList<IReadOnlyList<Ponto2>> Retangulo(double x0, double y0, double x1, double y1) =>
        [new List<Ponto2> { new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1) }];

    static readonly LimitesDeBico Leve = EspacamentoDeBicos.Padrao(ClasseRisco.Leve);
    static readonly LimitesDeBico Ordinario = EspacamentoDeBicos.Padrao(ClasseRisco.Ordinario2);

    [Fact]
    public void Tabela_2_calculo_hidraulico()
    {
        Assert.Equal(20.9, Leve.AreaMaxima);
        Assert.Equal(4.6, Leve.DistanciaMaxima);
        Assert.Equal(2.3, Leve.ParedeMaxima, 6);
        Assert.Equal(12.1, Ordinario.AreaMaxima);
        Assert.Equal(9.3, EspacamentoDeBicos.Padrao(ClasseRisco.Extraordinario1, densidade: 12.2).AreaMaxima);
        Assert.Equal(3.7, EspacamentoDeBicos.Padrao(ClasseRisco.Extraordinario1, densidade: 12.2).DistanciaMaxima);
        Assert.Equal(12.1, EspacamentoDeBicos.Padrao(ClasseRisco.Extraordinario1, densidade: 8.1).AreaMaxima);
        Assert.Equal(18.6, EspacamentoDeBicos.Padrao(ClasseRisco.Leve, porTabela: true).AreaMaxima);
    }

    [Fact]
    public void Sala_de_6_por_8_leva_4_bicos_a_meio_passo_da_parede()
    {
        var sala = Retangulo(0, 0, 6, 8);
        var p = BicosNoAmbiente.Posicionar(sala, Ordinario);

        Assert.Equal(4, p.Bicos.Count);
        Assert.Contains(p.Bicos, b => Math.Abs(b.X - 1.5) < 1e-6 && Math.Abs(b.Y - 2) < 1e-6);
        Assert.Empty(BicosNoAmbiente.Conferir(sala, p.Bicos, Ordinario));
    }

    [Fact]
    public void Galpao_de_20_por_10_ordinario_escolhe_a_grade_com_menos_bicos()
    {
        var galpao = Retangulo(0, 0, 20, 10);
        var p = BicosNoAmbiente.Posicionar(galpao, Ordinario);

        // 5 × 3 daria 4,0 × 3,33 = 13,3 m² (acima de 12,1); 6 × 3 dá 3,33 × 3,33 = 11,1 m² com 18 bicos (5 × 4 daria 20).
        Assert.Equal(18, p.Bicos.Count);
        Assert.Empty(BicosNoAmbiente.Conferir(galpao, p.Bicos, Ordinario));
    }

    [Fact]
    public void Ambiente_em_L_fica_coberto_sem_bico_fora()
    {
        // 12 × 12 sem o quadrado de 6 × 6 do canto superior direito.
        IReadOnlyList<IReadOnlyList<Ponto2>> l = [new List<Ponto2> { new(0, 0), new(12, 0), new(12, 6), new(6, 6), new(6, 12), new(0, 12) }];
        var p = BicosNoAmbiente.Posicionar(l, Leve);

        Assert.All(p.Bicos, b => Assert.True(BicosNoAmbiente.Dentro(l, b)));
        var falhas = BicosNoAmbiente.Conferir(l, p.Bicos, Leve);
        Assert.True(falhas.Count == 0, string.Join("; ", falhas.Select(f => $"{f.Tipo} {f.Onde}: {f.Descricao}")) +
                                        " | bicos: " + string.Join("; ", p.Bicos));
    }

    [Fact]
    public void Conferir_acha_bico_longe_perto_e_area_demais()
    {
        var sala = Retangulo(0, 0, 10, 4);
        var falhas = BicosNoAmbiente.Conferir(sala, [new Ponto2(0.05, 2), new Ponto2(1.0, 2), new Ponto2(7.5, 2)], Ordinario);

        Assert.Contains(falhas, f => f.Tipo == TipoDeFalha.PertoDaParede);
        Assert.Contains(falhas, f => f.Tipo == TipoDeFalha.PertoDeOutroBico);
        Assert.Contains(falhas, f => f.Tipo == TipoDeFalha.DistanciaEntreBicos); // 6,5 m entre o 2º e o 3º
        Assert.Contains(falhas, f => f.Tipo is TipoDeFalha.PontoDescoberto or TipoDeFalha.LongeDaParede);
    }

    [Fact]
    public void Sala_sem_bico_e_descoberta() =>
        Assert.Contains(BicosNoAmbiente.Conferir(Retangulo(0, 0, 3, 3), [], Leve), f => f.Tipo == TipoDeFalha.PontoDescoberto);
}
