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

    public static TheoryData<string> Formatos => new()
    {
        "0,0 26.5,0 26.5,1.6 14.5,1.6 14.5,4.9 12.5,4.9 12.5,1.6 0,1.6",
        "0,0 9.3,0 9.3,4.1 5.2,4.1 5.2,10.7 0,10.7",
        "0,0 17,0 17,3 11.4,3 11.4,7.7 4.3,7.7 4.3,3 0,3",
        "0,0 13.7,0 13.7,8.2 9.1,8.2 9.1,6.4 4.6,6.4 4.6,8.2 0,8.2",
        "0,0 8.7,0 8.7,5.0 3.2,5.0 3.2,4.7 0,4.7",
        "0,0 6.3,0 6.3,2.5 8.7,2.5 8.7,5.04 0,5.04",
    };

    [Theory]
    [MemberData(nameof(Formatos))]
    public void Fileiras_quase_alinhadas_viram_uma_linha_so(string formato)
    {
        // Cada pedaço do ambiente ganha a sua grade: sem alinhar, fileiras vizinhas ficam a poucos cm uma da outra, e o
        // ramal não liga o bico (perto demais para braço, fora da linha para descida reta).
        IReadOnlyList<IReadOnlyList<Ponto2>> sala = [formato.Split(' ').Select(t => t.Split(','))
            .Select(c => new Ponto2(double.Parse(c[0], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(c[1], System.Globalization.CultureInfo.InvariantCulture))).ToList()];
        foreach (var limites in new[] { Leve, Ordinario })
        {
            var p = BicosNoAmbiente.Posicionar(sala, limites);
            var v = new Ponto2(-p.DirecaoDosRamais.Y, p.DirecaoDosRamais.X);
            var linhas = p.Bicos.Select(b => b.X * v.X + b.Y * v.Y).ToList();
            foreach (var a in linhas)
            foreach (var b in linhas)
            {
                var d = Math.Abs(a - b);
                Assert.True(d < 1e-6 || d >= BicosNoAmbiente.MesmaFileira,
                    $"fileiras a {d * 100:0.0} cm: {string.Join("; ", p.Bicos)}");
            }
            Assert.Empty(BicosNoAmbiente.Conferir(sala, p.Bicos, limites).Where(f => f.Tipo == TipoDeFalha.ForaDoAmbiente));
        }
    }

    [Fact]
    public void Vigas_paralelas_fundas_ganham_um_bico_por_vao()
    {
        // Sala de 12 × 9 m com vigas de 20 cm ao longo de y em x = 3, 6 e 9: quatro vãos de ~3 m, uma fileira por vão.
        var sala = Retangulo(0, 0, 12, 9);
        var vigas = new[] { 3.0, 6.0, 9.0 }.Select(x => (new Ponto2(x, 0), new Ponto2(x, 9), 0.2)).ToList();
        var p = BicosNoAmbiente.PosicionarPorVaos(sala, vigas, Leve)!;

        Assert.Equal(8, p.Bicos.Count);
        Assert.True(Math.Abs(p.DirecaoDosRamais.Y) > 0.999, "ramais na direção das vigas");
        foreach (var b in p.Bicos)
            Assert.All(vigas, g => Assert.True(Math.Abs(b.X - g.Item1.X) > 0.1 + 0.1 - 1e-9, $"bico em x {b.X} em cima da viga em {g.Item1.X}"));
        Assert.Equal(4, p.Bicos.Select(b => Math.Round(b.X, 3)).Distinct().Count());
        Assert.Empty(BicosNoAmbiente.Conferir(sala, p.Bicos, Leve, p.DirecaoDosRamais));
    }

    [Fact]
    public void Grelha_de_vigas_ganha_bicos_em_cada_quadro()
    {
        // Vigas em x = 6 e em y = 4,5: quatro quadros de ~6 × 4,5 m, cada um com a sua grade, nenhum bico sobre viga.
        var sala = Retangulo(0, 0, 12, 9);
        var vigas = new List<(Ponto2, Ponto2, double)> { (new Ponto2(6, 0), new Ponto2(6, 9), 0.2), (new Ponto2(0, 4.5), new Ponto2(12, 4.5), 0.2) };
        Assert.True(BicosNoAmbiente.EhGrelha(vigas));
        var p = BicosNoAmbiente.PosicionarPorVaos(sala, vigas, Leve)!;

        Assert.Equal(8, p.Bicos.Count);
        Assert.All(p.Bicos, b => Assert.True(Math.Abs(b.X - 6) > 0.2 - 1e-9 && Math.Abs(b.Y - 4.5) > 0.2 - 1e-9, $"bico em {b} sobre viga"));
        foreach (var (qx, qy) in new[] { (3.0, 2.25), (9.0, 2.25), (3.0, 6.75), (9.0, 6.75) })
            Assert.Equal(2, p.Bicos.Count(b => Math.Abs(b.X - qx) < 3 && Math.Abs(b.Y - qy) < 2.25));
        Assert.Empty(BicosNoAmbiente.Conferir(sala, p.Bicos, Leve, p.DirecaoDosRamais));
    }

    [Fact]
    public void Viga_inclinada_fora_das_duas_direcoes_ainda_nao()
    {
        var sala = Retangulo(0, 0, 12, 9);
        var vigas = new List<(Ponto2, Ponto2, double)> { (new Ponto2(6, 0), new Ponto2(6, 9), 0.2), (new Ponto2(0, 0), new Ponto2(12, 9), 0.2) };
        Assert.Null(BicosNoAmbiente.PosicionarPorVaos(sala, vigas, Leve));
    }

    [Fact]
    public void Viga_larga_encostada_em_outra_vira_uma_faixa_so()
    {
        var sala = Retangulo(0, 0, 10, 8);
        var vigas = new List<(Ponto2, Ponto2, double)> { (new Ponto2(0, 4), new Ponto2(10, 4), 0.3), (new Ponto2(0, 4.2), new Ponto2(10, 4.2), 0.3) };
        var p = BicosNoAmbiente.PosicionarPorVaos(sala, vigas, Leve)!;
        Assert.All(p.Bicos, b => Assert.True(b.Y < 3.85 - 0.1 + 1e-9 || b.Y > 4.35 + 0.1 - 1e-9, $"bico em y {b.Y} sobre as vigas"));
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
