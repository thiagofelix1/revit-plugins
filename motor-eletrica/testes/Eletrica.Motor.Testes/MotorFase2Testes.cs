using Eletrica.Motor.Calculo;
using Eletrica.Motor.Exemplos;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Testes;

/// <summary>Entrega 0.5: demanda, fases, grupos de DR, TT, condutores em paralelo, seções dessemelhantes, sugestões, catálogo.</summary>
public class MotorFase2Testes
{
    static Instalacao Exemplo(Func<Alimentacao, Alimentacao>? alimentacao = null, Func<Circuito, Circuito>? circuito = null)
    {
        var i = new ApartamentoExemplo().Criar();
        return new Instalacao(i.Nome, alimentacao?.Invoke(i.Alimentacao) ?? i.Alimentacao, i.Ambientes, i.Quadros,
            circuito is null ? i.Circuitos : i.Circuitos.Select(circuito), i.Pontos, i.Trechos, i.Caixas, i.Comandos);
    }

    [Fact]
    public void Fatores_de_demanda_de_iluminacao_e_tug_por_faixa()
    {
        Assert.Equal(860, FatoresDeDemanda.DemandaIluminacaoETug(1000), 0.1);
        Assert.Equal(860 + 750, FatoresDeDemanda.DemandaIluminacaoETug(2000), 0.1);
        Assert.Equal(860 + 750 + 330, FatoresDeDemanda.DemandaIluminacaoETug(2500), 0.1);
        Assert.Equal(1.0, FatoresDeDemanda.FatorAquecimento(2));
        Assert.Equal(0.84, FatoresDeDemanda.FatorAquecimento(3));
        Assert.Equal(0.45, FatoresDeDemanda.FatorAquecimento(40));
        Assert.Equal(0.90, FatoresDeDemanda.FatorArCondicionado(3));
    }

    [Fact]
    public void Demanda_do_quadro_tem_parcelas_e_fica_abaixo_da_instalada()
    {
        var r = CalculoEletrica.Executar(Exemplo());
        var q = r.Quadros.Single();
        Assert.NotNull(q.Demanda);
        Assert.True(q.PotenciaDeDemandaVA < q.PotenciaInstaladaVA);
        Assert.Contains(q.Demanda!.Parcelas, p => p.Categoria == FatoresDeDemanda.Categoria.IluminacaoETug && p.Fator < 1);
        Assert.Contains(q.Demanda.Parcelas, p => p.Categoria == FatoresDeDemanda.Categoria.Aquecimento && p.Fator == 1.0); // um chuveiro
        Assert.Equal(q.Demanda.DemandaVA, q.PotenciaDeDemandaVA, 1e-6);

        var sem = CalculoEletrica.Executar(Exemplo(a => a with { AplicarFatoresDeDemanda = false })).Quadros.Single();
        Assert.Equal(sem.PotenciaInstaladaVA, sem.PotenciaDeDemandaVA, 1e-6);
    }

    [Fact]
    public void Fases_balanceadas_e_fase_imposta_respeitada()
    {
        var r = CalculoEletrica.Executar(Exemplo());
        Assert.All(r.Circuitos, c => Assert.False(string.IsNullOrEmpty(c.Fases)));
        Assert.Equal("AB", r.Circuitos.Single(c => c.Id == "C5").Fases); // chuveiro bifásico
        Assert.Single(r.Circuitos.Single(c => c.Id == "C1").Fases);
        Assert.True(r.Quadros.Single().DesequilibrioPercentual < 40);

        var imposta = CalculoEletrica.Executar(Exemplo(circuito: c => c.Id == "C1" ? c with { FaseImposta = "B" } : c));
        Assert.Equal("B", imposta.Circuitos.Single(c => c.Id == "C1").Fases);

        var errada = CalculoEletrica.Executar(Exemplo(circuito: c => c.Id == "C5" ? c with { FaseImposta = "A" } : c));
        Assert.Contains(errada.Quadros.Single().Avisos, a => a.Contains("não combina"));
    }

    [Fact]
    public void Grupos_de_DR_separam_tipo_F_e_limitam_a_seis()
    {
        var r = CalculoEletrica.Executar(Exemplo());
        var grupos = r.Quadros.Single().GruposDR!;
        Assert.NotEmpty(grupos);
        // os circuitos com DR obrigatório (cozinha, área de serviço, chuveiro, banheiro, luz nas áreas molhadas) estão todos em algum grupo
        foreach (var numero in r.Quadros.Single().CircuitosComDR) Assert.Contains(grupos, g => g.Circuitos.Contains(numero));
        Assert.DoesNotContain(grupos, g => g.Circuitos.Contains("6")); // ar-condicionado em dormitório não exige DR (só a nota do tipo F)
        Assert.All(grupos, g => Assert.True(g.Circuitos.Count <= DispositivosDR.CircuitosPorDRRecomendados));
        Assert.All(grupos, g => Assert.Contains(g.In, DispositivosDR.CorrentesNominais));
        Assert.All(grupos, g => Assert.True(g.In >= g.Circuitos.Select(n => r.Circuitos.Single(c => c.Numero == n).Disjuntor.In).Max()));

        // VE num circuito próprio ganha DR tipo B sozinho
        var comVe = Exemplo(circuito: c => c.Id == "C7" ? c with { Tipo = TipoCarga.VeiculoEletrico, Descricao = "Carregador VE" } : c);
        var gruposVe = CalculoEletrica.Executar(comVe).Quadros.Single().GruposDR!;
        var doVe = Assert.Single(gruposVe, g => g.Circuitos.Contains("7"));
        Assert.Equal(TipoDr.B, doVe.Tipo);
        Assert.Single(doVe.Circuitos);
    }

    [Fact]
    public void Em_TT_todo_circuito_leva_DR_e_RA_e_avaliado()
    {
        var semRa = CalculoEletrica.Executar(Exemplo(a => a with { Esquema = EsquemaAterramento.TT }));
        Assert.All(semRa.Circuitos, c => Assert.True(c.DR.Obrigatorio));
        // C2 (TUG de sala, dormitórios, circulação e varanda) não exige proteção adicional: DR de 300 mA só pelo TT; o chuveiro, 30 mA.
        var tug = semRa.Circuitos.Single(c => c.Id == "C2");
        Assert.Equal(300, tug.DR.CorrenteResidualMA);
        Assert.Equal(30, semRa.Circuitos.Single(c => c.Id == "C5").DR.CorrenteResidualMA);
        Assert.Contains(tug.Pareceres, p => p.Criterio == "seccionamento automático" && p.Atende is null);

        var comRa = CalculoEletrica.Executar(Exemplo(a => a with { Esquema = EsquemaAterramento.TT, ResistenciaDeAterramentoOhm = 100 }));
        Assert.Contains(comRa.Circuitos.Single(c => c.Id == "C2").Pareceres, p => p.Criterio == "seccionamento automático" && p.Atende == true); // 100 Ω × 0,3 A = 30 V ≤ 50
        var alta = CalculoEletrica.Executar(Exemplo(a => a with { Esquema = EsquemaAterramento.TT, ResistenciaDeAterramentoOhm = 200 }));
        Assert.Contains(alta.Circuitos.Single(c => c.Id == "C2").Pareceres, p => p.Criterio == "seccionamento automático" && p.Atende == false); // 60 V
    }

    [Fact]
    public void Condutores_em_paralelo_quando_nenhuma_secao_unica_atende()
    {
        // Carga enorme num circuito curto: 600 A em 220 V bifásico não cabe em 500 mm² único (B1), cabe em 2 × paralelo.
        var i = Exemplo(circuito: c => c.Id == "C5" ? c with { Descricao = "Carga pesada" } : c);
        var pontos = i.Pontos.Select(p => p.Id == "CH" ? p with { PotenciaVA = 120000 } : p);
        var pesada = new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, pontos, i.Trechos, i.Caixas, i.Comandos);
        var r = CalculoEletrica.Executar(pesada).Circuitos.Single(c => c.Id == "C5");
        Assert.Equal(2, r.CondutoresPorFase);
        Assert.Contains(r.Avisos, a => a.Contains("em paralelo"));
        Assert.True(r.Pareceres.Single(p => p.Criterio == "capacidade de corrente").Atende);
        Assert.True(r.CapacidadeCorrigida >= r.CorrenteDeProjeto);
    }

    [Fact]
    public void Secoes_dessemelhantes_no_mesmo_eletroduto_puxam_o_agrupamento_para_1_sobre_raiz_n()
    {
        // C1 (iluminação) imposta em 25 mm² no tronco QD-circB com circuitos de 2,5 e 4: segunda passada com 1/√5 = 0,45 no trecho.
        var r = CalculoEletrica.Executar(Exemplo(circuito: c => c.Id == "C1" ? c with { SecaoImposta = 25 } : c));
        var c3 = r.Circuitos.Single(c => c.Id == "C3");
        Assert.Contains(c3.Avisos, a => a.Contains("dessemelhantes"));
        Assert.Equal(Nbr5410.FatorDeAgrupamentoDessemelhante(5), c3.FatorDeAgrupamento, 3);
    }

    [Fact]
    public void Problema_do_calculo_traz_sugestao()
    {
        var r = CalculoEletrica.Executar(Exemplo(circuito: c => c.Id == "C5" ? c with { SecaoImposta = 1.5 } : c));
        var problema = Assert.Single(r.Problemas, p => p.Codigo == "D-IZ" && p.Elementos.Contains("C5"));
        Assert.Contains("Sugestão:", problema.Mensagem);
        Assert.Contains("subir a seção", problema.Mensagem);
    }

    [Fact]
    public void Folgas_e_PE_comum_entram_no_calculo()
    {
        var base_ = CalculoEletrica.Executar(Exemplo());
        var comFolga = CalculoEletrica.Executar(Exemplo(a => a with { FolgaDeComprimentoPercentual = 10, FolgaDeOcupacaoPercentual = 20 }));
        var c2 = base_.Circuitos.Single(c => c.Id == "C2");
        var c2Folga = comFolga.Circuitos.Single(c => c.Id == "C2");
        Assert.Equal(c2.Comprimento * 1.1, c2Folga.Comprimento, 1e-6);
        var tronco = base_.Trechos.Single(t => t.Id == "QD-circB");
        var troncoFolga = comFolga.Trechos.Single(t => t.Id == "QD-circB");
        Assert.Equal(tronco.AreaDosCondutores * 1.2, troncoFolga.AreaDosCondutores, 1e-6);

        var peComum = CalculoEletrica.Executar(Exemplo(a => a with { PEComumNoConduto = true })).Trechos.Single(t => t.Id == "QD-circB");
        Assert.True(peComum.Condutores < tronco.Condutores);
    }

    [Fact]
    public void Catalogo_tem_o_disjuntor_que_atende()
    {
        var d = Catalogo.Disjuntor(18, CurvaDisjuntor.C, 4.0, 2);
        Assert.NotNull(d);
        Assert.Equal(20, d!.In);
        Assert.Equal(4.5, d.IcnKA);
        Assert.Contains(Catalogo.Eletrodutos(), e => e.Tipo == TipoEletroduto.RigidoRoscavel && e.Dn == 25);
        Assert.Contains(Catalogo.Condutores(), c => c.Secao == 2.5 && c.Isolacao == Isolacao.Pvc70);
        Assert.Equal(2, Catalogo.Tomadas().Count());
    }
}
