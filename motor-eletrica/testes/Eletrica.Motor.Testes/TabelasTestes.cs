using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Testes;

/// <summary>Valores das tabelas digitados conferidos contra a norma (e a IEC 60364-5-52), um por coluna pelo menos.</summary>
public class TabelasTestes
{
    [Theory]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.B1, 2, 2.5, 24)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.B1, 3, 2.5, 21)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.B1, 2, 1.5, 17.5)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.B1, 2, 10, 57)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.A1, 3, 4, 24)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.A2, 2, 6, 32)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.B2, 3, 16, 62)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.C, 2, 25, 112)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.D, 3, 35, 103)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.D, 2, 0.5, 12)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.B1, 2, 1000, 1012)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.B1, 2, 2.5, 31)]
    [InlineData(Isolacao.Xlpe90, MetodoReferencia.B1, 3, 2.5, 28)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.A1, 2, 1.5, 19)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.C, 3, 70, 229)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.D, 2, 16, 95)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.E, 2, 16, 94)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.E, 3, 1.5, 18.5)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.F, 3, 25, 110)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.FPlano, 3, 25, 114)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.G, 3, 25, 146)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.GVertical, 3, 25, 130)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.E, 2, 1.5, 26)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.E, 3, 35, 158)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.F, 2, 50, 242)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.F, 3, 95, 328)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.G, 3, 240, 781)]
    public void Tabelas_36_a_39_cobre(Isolacao isolacao, MetodoReferencia metodo, int n, double secao, double esperado) =>
        Assert.Equal(esperado, Nbr5410Capacidades.Capacidade(isolacao, MaterialCondutor.Cobre, metodo, n, secao));

    [Theory]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.B1, 2, 16, 60)]
    [InlineData(Isolacao.Pvc70, MetodoReferencia.D, 3, 50, 94)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.C, 2, 95, 241)]
    [InlineData(Isolacao.Epr90, MetodoReferencia.E, 3, 16, 77)]
    public void Tabelas_36_a_39_aluminio(Isolacao isolacao, MetodoReferencia metodo, int n, double secao, double esperado) =>
        Assert.Equal(esperado, Nbr5410Capacidades.Capacidade(isolacao, MaterialCondutor.Aluminio, metodo, n, secao));

    [Fact]
    public void Aluminio_abaixo_de_16_nao_e_tabelado() =>
        Assert.Throws<ArgumentException>(() => Nbr5410Capacidades.Capacidade(Isolacao.Pvc70, MaterialCondutor.Aluminio, MetodoReferencia.B1, 2, 10));

    [Fact]
    public void Colunas_tem_o_tamanho_das_secoes()
    {
        foreach (var metodo in Enum.GetValues<MetodoReferencia>())
        foreach (var isolacao in new[] { Isolacao.Pvc70, Isolacao.Epr90 })
        {
            var n = metodo is MetodoReferencia.FPlano or MetodoReferencia.G or MetodoReferencia.GVertical ? 3 : 2;
            Assert.True(Nbr5410Capacidades.Capacidade(isolacao, MaterialCondutor.Cobre, metodo, n, 1000) > 0);
            Assert.True(Nbr5410Capacidades.Capacidade(isolacao, MaterialCondutor.Cobre, metodo, n, 0.5) > 0);
        }
    }

    [Fact]
    public void Capacidades_crescem_com_a_secao()
    {
        foreach (var metodo in new[] { MetodoReferencia.A1, MetodoReferencia.B1, MetodoReferencia.C, MetodoReferencia.D, MetodoReferencia.E, MetodoReferencia.F })
        foreach (var isolacao in new[] { Isolacao.Pvc70, Isolacao.Epr90 })
        {
            double anterior = 0;
            foreach (var s in Nbr5410Capacidades.Secoes)
            {
                var v = Nbr5410Capacidades.Capacidade(isolacao, MaterialCondutor.Cobre, metodo, 2, s);
                Assert.True(v > anterior, $"{isolacao} {metodo} {s} mm²: {v} não cresce a partir de {anterior}");
                anterior = v;
            }
        }
    }

    [Theory]
    [InlineData(Isolacao.Pvc70, 30, 1.00)]
    [InlineData(Isolacao.Pvc70, 35, 0.94)]
    [InlineData(Isolacao.Pvc70, 40, 0.87)]
    [InlineData(Isolacao.Pvc70, 20, 1.12)]
    [InlineData(Isolacao.Epr90, 40, 0.91)]
    [InlineData(Isolacao.Epr90, 60, 0.71)]
    public void Tabela_40(Isolacao isolacao, double temperatura, double fator) =>
        Assert.Equal(fator, Nbr5410.FatorDeTemperaturaAr(isolacao, temperatura), 3);

    [Theory]
    [InlineData(1, 1.00)]
    [InlineData(2, 0.80)]
    [InlineData(3, 0.70)]
    [InlineData(4, 0.65)]
    [InlineData(6, 0.57)]
    [InlineData(9, 0.50)]
    [InlineData(12, 0.45)]
    [InlineData(16, 0.41)]
    [InlineData(25, 0.38)]
    public void Tabela_42_feixe(int circuitos, double fator) => Assert.Equal(fator, Nbr5410.FatorDeAgrupamentoEmFeixe(circuitos));

    [Theory]
    [InlineData(Ligacao.FN, false, 2)]
    [InlineData(Ligacao.FF, false, 2)]
    [InlineData(Ligacao.FFN, false, 3)]
    [InlineData(Ligacao.FFF, false, 3)]
    [InlineData(Ligacao.FFFN, false, 3)]
    [InlineData(Ligacao.FFFN, true, 4)]
    public void Tabela_46(Ligacao ligacao, bool harmonicas, int n) => Assert.Equal(n, Nbr5410.CondutoresCarregados(ligacao, harmonicas));

    [Theory]
    [InlineData(1.5, 1.5)]
    [InlineData(16, 16)]
    [InlineData(25, 16)]
    [InlineData(35, 16)]
    [InlineData(50, 25)]
    [InlineData(120, 60)]
    public void Tabela_58_PE(double fase, double pe) => Assert.Equal(pe, Nbr5410.SecaoDoPE(fase));

    [Theory]
    [InlineData(35, 25)]
    [InlineData(70, 35)]
    [InlineData(240, 120)]
    public void Tabela_48_neutro_reduzido(double fase, double neutro) =>
        Assert.Equal(neutro, Nbr5410.SecaoDoNeutro(fase, Ligacao.FFFN, harmonicasAltas: false, permitirReduzido: true));

    [Fact]
    public void Neutro_igual_a_fase_fora_das_condicoes()
    {
        Assert.Equal(70, Nbr5410.SecaoDoNeutro(70, Ligacao.FFFN, harmonicasAltas: true, permitirReduzido: true));
        Assert.Equal(70, Nbr5410.SecaoDoNeutro(70, Ligacao.FN, false, true));
        Assert.Equal(25, Nbr5410.SecaoDoNeutro(25, Ligacao.FFFN, false, true));
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(12, 3)]
    [InlineData(13, 4)]
    [InlineData(30, 4)]
    [InlineData(40, 6)]
    public void Tabela_59_reserva(int circuitos, int reserva) => Assert.Equal(reserva, Nbr5410.ReservaDoQuadro(circuitos));

    [Theory]
    [InlineData(127, 1, 0.8)]
    [InlineData(220, 1, 0.4)]
    [InlineData(220, 2, 0.2)]
    [InlineData(400, 1, 0.2)]
    public void Tabela_25(double uo, int situacao, double tempo) => Assert.Equal(tempo, Nbr5410.TempoMaximoTN(uo, (Situacao)situacao));

    [Theory]
    [InlineData(MaterialCondutor.Cobre, Isolacao.Pvc70, 2.5, 115)]
    [InlineData(MaterialCondutor.Cobre, Isolacao.Pvc70, 400, 103)]
    [InlineData(MaterialCondutor.Cobre, Isolacao.Epr90, 2.5, 143)]
    [InlineData(MaterialCondutor.Aluminio, Isolacao.Xlpe90, 50, 94)]
    public void Tabela_30_k(MaterialCondutor material, Isolacao isolacao, double secao, double k) => Assert.Equal(k, Nbr5410.FatorK(material, isolacao, secao));

    [Theory]
    [InlineData(4, 100)]
    [InlineData(6, 100)]
    [InlineData(8, 100)]
    [InlineData(10, 160)]
    [InlineData(14, 220)]
    [InlineData(18, 280)]
    public void Carga_minima_de_iluminacao_9_5_2_1_2(double area, double va) => Assert.Equal(va, Nbr5410.CargaMinimaDeIluminacao(area));

    [Theory]
    [InlineData(UsoAmbiente.Sala, 18, 17.2, 4)]
    [InlineData(UsoAmbiente.Dormitorio, 9, 12, 3)]
    [InlineData(UsoAmbiente.Cozinha, 8.5, 12, 4)]
    [InlineData(UsoAmbiente.AreaServico, 3.6, 8, 3)]
    [InlineData(UsoAmbiente.Banheiro, 3, 7, 1)]
    [InlineData(UsoAmbiente.Varanda, 3, 7, 1)]
    [InlineData(UsoAmbiente.Circulacao, 2, 6, 1)]
    [InlineData(UsoAmbiente.Escritorio, 12, 14, 3)]
    public void Pontos_de_tomada_minimos_9_5_2_2_1(UsoAmbiente uso, double area, double perimetro, int pontos) =>
        Assert.Equal(pontos, Nbr5410.PontosDeTomadaMinimos(uso, area, perimetro));

    [Fact]
    public void Potencias_minimas_das_tomadas_9_5_2_2_2()
    {
        Assert.Equal([600, 600, 600, 100], Nbr5410.PotenciasMinimasDasTomadas(UsoAmbiente.Cozinha, 4).ToArray());
        Assert.Equal([600, 600, 100, 100], Nbr5410.PotenciasMinimasDasTomadas(UsoAmbiente.Cozinha, 4, conjuntoMolhadoComMaisDeSeis: true).ToArray());
        Assert.Equal([100, 100], Nbr5410.PotenciasMinimasDasTomadas(UsoAmbiente.Sala, 2).ToArray());
    }

    [Theory]
    [InlineData(CurvaDisjuntor.B, 20, 100)]
    [InlineData(CurvaDisjuntor.C, 20, 200)]
    [InlineData(CurvaDisjuntor.D, 20, 400)]
    public void NM_60898_atuacao_instantanea(CurvaDisjuntor curva, double inA, double ia) => Assert.Equal(ia, Nbr60898.CorrenteDeAtuacaoInstantanea(curva, inA));

    [Theory]
    [InlineData(9.6, 10)]
    [InlineData(10, 10)]
    [InlineData(10.1, 13)]
    [InlineData(25.1, 32)]
    [InlineData(125, 125)]
    public void NM_60898_In_preferencial(double corrente, double inA) => Assert.Equal(inA, Nbr60898.MenorInNaoInferiorA(corrente));

    [Fact]
    public void NM_60898_In_acima_de_125_nao_existe() => Assert.Null(Nbr60898.MenorInNaoInferiorA(126));

    [Theory]
    [InlineData(3, CurvaDisjuntor.C, 16, 6, 42_000)]
    [InlineData(3, CurvaDisjuntor.B, 16, 6, 35_000)]
    [InlineData(2, CurvaDisjuntor.C, 32, 10, 370_000)]
    [InlineData(3, CurvaDisjuntor.C, 40, 6, 66_000)]
    public void NM_60898_classes_de_energia(int classe, CurvaDisjuntor curva, double inA, double icn, double i2t) =>
        Assert.Equal(i2t, Nbr60898.EnergiaPassanteMaxima(classe, curva, inA, icn)!.Value, 0);

    [Fact]
    public void NM_60898_classe_D_ou_classe_1_sem_limite()
    {
        Assert.Null(Nbr60898.EnergiaPassanteMaxima(3, CurvaDisjuntor.D, 16, 6));
        Assert.Null(Nbr60898.EnergiaPassanteMaxima(1, CurvaDisjuntor.C, 16, 6));
    }

    [Theory]
    [InlineData(4.5, 4.5)]
    [InlineData(10, 7.5)]
    [InlineData(20, 10)]
    public void NM_60898_Tabela_15_Ics(double icn, double ics) => Assert.Equal(ics, Nbr60898.CapacidadeDeServico(icn));

    [Theory]
    [InlineData(2.5, true, 4.1)]
    [InlineData(2.5, false, 4.0)]
    [InlineData(1.5, true, 3.4)]
    [InlineData(16, false, 7.8)]
    public void NM_247_3_diametros(double secao, bool flexivel, double diametro) => Assert.Equal(diametro, Condutores.DiametroExterno(secao, Isolacao.Pvc70, flexivel));

    [Fact]
    public void Eletroduto_DN_20_leva_seis_condutores_de_2_5()
    {
        // 6 × π × 4,1²/4 = 79,2 mm² ≤ 40 % de 227 mm² (DN 20 roscável, interno 17 mm)
        var area = 6 * Condutores.AreaExterna(2.5, Isolacao.Pvc70);
        Assert.Equal(20, Eletrodutos.MenorQueComporta(TipoEletroduto.RigidoRoscavel, area, 6)!.Dn);
        var sete = 7 * Condutores.AreaExterna(2.5, Isolacao.Pvc70);
        Assert.Equal(25, Eletrodutos.MenorQueComporta(TipoEletroduto.RigidoRoscavel, sete, 7)!.Dn);
    }

    [Theory]
    [InlineData(1, 0.53)]
    [InlineData(2, 0.31)]
    [InlineData(3, 0.40)]
    [InlineData(12, 0.40)]
    public void Taxa_de_ocupacao_6_2_11_1_6(int condutores, double taxa) => Assert.Equal(taxa, Nbr5410.TaxaDeOcupacaoMaxima(condutores));

    [Fact]
    public void DPS_obrigatorio_com_linha_aerea_em_AQ2()
    {
        var a = new Alimentacao(127, 220, 2, EsquemaAterramento.TNCS) { LinhaAerea = true, DiasDeTrovoada = 40 };
        Assert.NotNull(DispositivosDPS.MotivoDeObrigatoriedade(a));
        Assert.Null(DispositivosDPS.MotivoDeObrigatoriedade(a with { LinhaAerea = false }));
        Assert.Null(DispositivosDPS.MotivoDeObrigatoriedade(a with { DiasDeTrovoada = 20 }));
        Assert.NotNull(DispositivosDPS.MotivoDeObrigatoriedade(a with { LinhaAerea = false, TemSpda = true }));
    }

    [Fact]
    public void DPS_especificacao_TNCS_127_220()
    {
        var a = new Alimentacao(127, 220, 2, EsquemaAterramento.TNCS) { LinhaAerea = true, DiasDeTrovoada = 40 };
        var e = DispositivosDPS.Especificar(a, quadroDeEntrada: true);
        Assert.Equal(DispositivosDPS.Classe.I, e.Classe);
        Assert.Equal(3, e.Quantidade);
        Assert.Equal(139.7, e.UcMinimo, 1);
        Assert.Equal(175, e.UcComercial);
        Assert.Equal(1.5, e.UpMaximo);
        Assert.Equal(12.5, e.IimpMinimoKA);
    }

    [Fact]
    public void DPS_em_TT_trifasico_220_380_e_3_mais_1()
    {
        var a = new Alimentacao(220, 380, 3, EsquemaAterramento.TT) { TemSpda = true };
        var e = DispositivosDPS.Especificar(a, true);
        Assert.Equal(4, e.Quantidade);
        Assert.Equal(275, e.UcComercial);
        Assert.Equal(2.5, e.UpMaximo);
        Assert.Equal(20, e.InMinimoKA);
        Assert.Equal(16, e.SecaoDeLigacao);
    }

    [Fact]
    public void DR_obrigatorio_pelo_uso_do_ambiente()
    {
        var cozinha = new Ambiente("c", "Cozinha", UsoAmbiente.Cozinha, 9, 12);
        var sala = new Ambiente("s", "Sala", UsoAmbiente.Sala, 18, 17);
        var tomada = new Ponto("t", "c", "C3", TipoCarga.TomadaUsoGeral, 600);
        var luzBaixa = new Ponto("l", "c", "C1", TipoCarga.Iluminacao, 100) { Altura = 2.4 };
        var luzAlta = luzBaixa with { Altura = 2.6 };
        Assert.NotNull(DispositivosDR.MotivoDeDR(tomada, cozinha, habitacao: true));
        Assert.NotNull(DispositivosDR.MotivoDeDR(luzBaixa, cozinha, habitacao: true));
        Assert.Null(DispositivosDR.MotivoDeDR(luzAlta, cozinha, habitacao: true));
        Assert.Null(DispositivosDR.MotivoDeDR(luzBaixa, cozinha, habitacao: false));
        Assert.NotNull(DispositivosDR.MotivoDeDR(tomada, cozinha, habitacao: false));
        Assert.Null(DispositivosDR.MotivoDeDR(tomada with { Ambiente = "s" }, sala, true));
    }
}
