using Eletrica.Motor.Calculo;
using Eletrica.Motor.Exemplos;
using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Testes;

public class DimensionamentoTestes
{
    static Instalacao Simples(Circuito circuito, double? iccKA = 6, params Ponto[] pontos) =>
        new("teste", new Alimentacao(127, 220, 2, EsquemaAterramento.TNCS) { IccPresumidaKA = iccKA },
            [new Ambiente("a", "Sala", UsoAmbiente.Sala, 12, 14), new Ambiente("b", "Banheiro", UsoAmbiente.Banheiro, 3, 7)],
            [new Quadro("QD", "QD", 2)], [circuito], pontos);

    [Theory]
    [InlineData(2200, Ligacao.FN, 17.32)]
    [InlineData(5500, Ligacao.FF, 25.0)]
    [InlineData(4400, Ligacao.FFN, 17.32)]
    [InlineData(7620, Ligacao.FFF, 20.0)]
    public void Corrente_de_projeto(double va, Ligacao ligacao, double ib) =>
        Assert.Equal(ib, Eletrotecnica.CorrenteDeProjeto(va, ligacao, 127, 220), 2);

    [Fact]
    public void Chuveiro_5500_W_em_220_V_da_4_mm2_e_disjuntor_25_A()
    {
        // IB = 25 A; 2,5 mm² B1 dois condutores = 24 A (não passa); 4 mm² = 32 A, In = 25 A ≤ 32 A.
        var c = new Circuito("C5", "5", "QD", TipoCarga.Chuveiro, Ligacao.FF) { PotenciaVA = 5500, ComprimentoManual = 12 };
        var r = CalculoEletrica.Executar(Simples(c)).Circuitos.Single();
        Assert.Equal(25, r.CorrenteDeProjeto, 2);
        Assert.Equal(4, r.SecaoFase);
        Assert.Equal(25, r.Disjuntor.In);
        Assert.Equal(CurvaDisjuntor.B, r.Disjuntor.Curva);
        Assert.Equal(2, r.Disjuntor.Polos);
        Assert.Equal("capacidade de corrente", r.CriterioQueMandou);
        Assert.True(r.Atende, string.Join("; ", r.Reprovados.Select(p => p.Criterio + ": " + p.Valor)));
    }

    [Fact]
    public void Agrupamento_sobe_a_secao()
    {
        // Mesmo chuveiro com 4 circuitos no eletroduto: FCA 0,65 → 4 mm² dá 20,8 A < 25 A; 6 mm² dá 26,65 A ≥ 25 A.
        var c = new Circuito("C5", "5", "QD", TipoCarga.Chuveiro, Ligacao.FF) { PotenciaVA = 5500, ComprimentoManual = 12, AgrupamentoManual = 4 };
        var r = CalculoEletrica.Executar(Simples(c)).Circuitos.Single();
        Assert.Equal(6, r.SecaoFase);
        Assert.Equal(0.65, r.FatorDeAgrupamento);
        Assert.Equal(25, r.Disjuntor.In);
    }

    [Fact]
    public void Queda_de_tensao_manda_em_circuito_longo()
    {
        // 1 500 VA em 127 V (11,8 A) a 60 m: 2,5 mm² dá ~ 9 % (ida e volta), queda manda até 10 mm².
        var c = new Circuito("C2", "2", "QD", TipoCarga.TomadaUsoGeral, Ligacao.FN) { PotenciaVA = 1500, ComprimentoManual = 60 };
        var r = CalculoEletrica.Executar(Simples(c)).Circuitos.Single();
        Assert.Equal("queda de tensão", r.CriterioQueMandou);
        Assert.True(r.QuedaTerminal <= 4.0);
        Assert.True(r.SecaoFase >= 6);
        Assert.True(r.Atende);
    }

    [Fact]
    public void Iluminacao_comeca_em_1_5_e_forca_em_2_5()
    {
        var luz = new Circuito("C1", "1", "QD", TipoCarga.Iluminacao, Ligacao.FN) { PotenciaVA = 600, ComprimentoManual = 10 };
        var tug = new Circuito("C2", "2", "QD", TipoCarga.TomadaUsoGeral, Ligacao.FN) { PotenciaVA = 600, ComprimentoManual = 10 };
        Assert.Equal(1.5, CalculoEletrica.Executar(Simples(luz)).Circuitos.Single().SecaoFase);
        Assert.Equal(2.5, CalculoEletrica.Executar(Simples(tug)).Circuitos.Single().SecaoFase);
    }

    [Fact]
    public void Sem_Icc_os_criterios_de_curto_ficam_nao_avaliados()
    {
        var c = new Circuito("C2", "2", "QD", TipoCarga.TomadaUsoGeral, Ligacao.FN) { PotenciaVA = 1200, ComprimentoManual = 15 };
        var r = CalculoEletrica.Executar(Simples(c, iccKA: null)).Circuitos.Single();
        Assert.Contains(r.Pareceres, p => p.Criterio == "curto-circuito" && p.NaoAvaliado);
        Assert.Null(r.IccMaximaNaOrigemKA);
        Assert.True(r.Atende);
    }

    [Fact]
    public void Curto_minimo_no_fim_do_circuito_longo_reprova_o_seccionamento()
    {
        // 1,5 mm² de iluminação a 60 m com disjuntor C 10 A: Ia = 100 A; Ikmín fase-PE (≈ 58 A) fica abaixo.
        var c = new Circuito("C1", "1", "QD", TipoCarga.Iluminacao, Ligacao.FN) { PotenciaVA = 400, ComprimentoManual = 60, SecaoImposta = 1.5, DisjuntorImposto = 10 };
        var r = CalculoEletrica.Executar(Simples(c)).Circuitos.Single();
        var zs = r.Pareceres.Single(p => p.Criterio == "seccionamento automático");
        Assert.True(r.IccMinimaNoFimA < 100, $"Ikmín = {r.IccMinimaNoFimA}");
        Assert.True(zs.Reprovado);
        // com curva B (Ia = 50 A) passa
        var b = CalculoEletrica.Executar(Simples(c with { CurvaImposta = CurvaDisjuntor.B })).Circuitos.Single();
        Assert.False(b.Pareceres.Single(p => p.Criterio == "seccionamento automático").Reprovado);
    }

    [Fact]
    public void Icc_no_barramento_decresce_pelo_alimentador()
    {
        var i = new Instalacao("dois quadros", new Alimentacao(127, 220, 2, EsquemaAterramento.TNCS) { IccPresumidaKA = 10 },
            [new Ambiente("a", "Sala", UsoAmbiente.Sala, 12, 14)],
            [new Quadro("QGBT", "QGBT", 2), new Quadro("QD", "QD", 2) { QuadroPai = "QGBT" }],
            [
                new Circuito("AL", "A1", "QGBT", TipoCarga.Alimentador, Ligacao.FFN) { QuadroAlimentado = "QD", ComprimentoManual = 40, Isolacao = Isolacao.Epr90 },
                new Circuito("C2", "2", "QD", TipoCarga.TomadaUsoGeral, Ligacao.FN) { PotenciaVA = 3000, ComprimentoManual = 20 },
                new Circuito("C5", "5", "QD", TipoCarga.Chuveiro, Ligacao.FF) { PotenciaVA = 6000, ComprimentoManual = 15 },
            ], []);
        var r = CalculoEletrica.Executar(i);
        var qgbt = r.Quadros.Single(q => q.Id == "QGBT");
        var qd = r.Quadros.Single(q => q.Id == "QD");
        Assert.Equal(10 * Eletrotecnica.FatorDeTensaoMaximo, qgbt.IccNoBarramentoKA!.Value, 2);
        Assert.True(qd.IccNoBarramentoKA < qgbt.IccNoBarramentoKA);
        var al = r.Circuitos.Single(c => c.Id == "AL");
        Assert.Equal(9000, al.PotenciaVA);
        Assert.Equal(9000 / (2 * 127.0), al.CorrenteDeProjeto, 2);
        var c2 = r.Circuitos.Single(c => c.Id == "C2");
        Assert.True(c2.QuedaTotal > c2.QuedaTerminal);
        Assert.Single(c2.QuedasAMontante);
        Assert.Equal(9000, qd.PotenciaInstaladaVA);
        Assert.Equal(2, qgbt.Reserva);
    }

    [Fact]
    public void Apartamento_exemplo_atende_e_tem_os_circuitos_esperados()
    {
        var r = CalculoEletrica.Executar(new ApartamentoExemplo().Criar());
        Assert.True(r.Atende, string.Join("\n", r.Problemas.Where(p => p.Gravidade == Gravidade.Erro).Select(p => p.Codigo + " " + p.Mensagem)));
        Assert.Equal(8, r.Circuitos.Count);
        var chuveiro = r.Circuitos.Single(c => c.Id == "C5");
        Assert.Equal(25, chuveiro.CorrenteDeProjeto, 2);
        Assert.Equal(4, chuveiro.SecaoFase);
        Assert.Equal("QD-bwc", chuveiro.TrechoCritico);
        Assert.True(chuveiro.DR.Obrigatorio);
        Assert.False(chuveiro.ComprimentoEstimado);
        var luz = r.Circuitos.Single(c => c.Id == "C1");
        Assert.Equal(1040, luz.PotenciaVA);
        Assert.Equal(5, luz.CircuitosAgrupados);
        var tug = r.Circuitos.First(c => c.Id == "C2");
        Assert.Equal("QD-circA", tug.TrechoCritico);
        Assert.Equal(3, tug.CircuitosAgrupados);
        Assert.Equal(0.70, tug.FatorDeAgrupamento);
        Assert.Equal(2.5, tug.SecaoFase);
        Assert.Equal(10, tug.Disjuntor.In);
        var qd = r.Quadros.Single();
        Assert.Equal(3, qd.Reserva);
        Assert.NotNull(qd.Dps);
        Assert.Contains("5", qd.CircuitosComDR);
        Assert.Contains("3", qd.CircuitosComDR);
        Assert.Contains("8", qd.CircuitosComDR);
        Assert.DoesNotContain("2", qd.CircuitosComDR);
        Assert.All(r.Trechos, t => Assert.True(t.Atende, t.Id + ": " + string.Join("; ", t.Pareceres.Where(p => p.Reprovado).Select(p => p.Valor))));
    }

    [Fact]
    public void Eletroduto_pequeno_demais_e_apontado()
    {
        var i = new ApartamentoExemplo().Criar();
        var tronco = i.Trechos.Single(t => t.Id == "QD-circB") with { Dn = 20 };
        var sabotada = new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, i.Pontos, i.Trechos.Select(t => t.Id == "QD-circB" ? tronco : t));
        var r = CalculoEletrica.Executar(sabotada);
        var t = r.Trechos.Single(t => t.Id == "QD-circB");
        Assert.False(t.Atende);
        Assert.True(t.TaxaDeOcupacao > 0.40);
        Assert.Equal(40, t.DnSugerido);
        Assert.Contains(r.Problemas, p => p.Codigo == "E-OCUP");
    }

    [Fact]
    public void Trecho_longo_e_com_curvas_demais_e_apontado()
    {
        var i = new ApartamentoExemplo().Criar();
        var longo = i.Trechos.Single(t => t.Id == "circ-d1") with { Comprimento = 12, Curvas90 = 4 };
        var sabotada = new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, i.Pontos, i.Trechos.Select(t => t.Id == "circ-d1" ? longo : t));
        var r = CalculoEletrica.Executar(sabotada);
        Assert.Contains(r.Problemas, p => p.Codigo == "E-15M");
        Assert.Contains(r.Problemas, p => p.Codigo == "E-CURV");
    }
}
