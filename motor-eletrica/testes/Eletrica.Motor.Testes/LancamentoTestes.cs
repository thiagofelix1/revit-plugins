using Eletrica.Motor.Calculo;
using Eletrica.Motor.Exemplos;
using Eletrica.Motor.Lancamento;
using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Testes;

public class LancamentoTestes
{
    static readonly PadraoDeLancamento Padrao = new();
    static readonly Alimentacao Bifasica = new(127, 220, 2, EsquemaAterramento.TNCS);

    [Fact]
    public void Sala_de_18m2_pede_4_tomadas_de_100VA_e_280VA_de_luz()
    {
        var sala = new Ambiente("sala", "Sala", UsoAmbiente.Sala, 18.0, 17.2);
        var p = PropostaDePontos.Propor(sala, Padrao);
        Assert.Equal(4, p.Tomadas);
        Assert.Equal(280, p.CargaDeIluminacao);
        var luz = Assert.Single(p.Pontos, x => x.Tipo == TipoCarga.Iluminacao);
        Assert.Equal(PosicaoDoPonto.Teto, luz.Posicao);
        Assert.All(p.Pontos.Where(x => x.Tipo == TipoCarga.TomadaUsoGeral), x => Assert.Equal(100, x.PotenciaVA));
        Assert.All(p.Pontos.Where(x => x.Tipo == TipoCarga.TomadaUsoGeral), x => Assert.Equal(PosicaoDoPonto.Baixa, x.Posicao));
        Assert.DoesNotContain(p.Pontos, x => x.Tipo == TipoCarga.Chuveiro);
    }

    [Fact]
    public void Cozinha_de_12m_pede_4_tomadas_3_de_600VA_2_sobre_a_bancada()
    {
        var coz = new Ambiente("coz", "Cozinha", UsoAmbiente.Cozinha, 8.5, 12.0);
        var p = PropostaDePontos.Propor(coz, Padrao);
        Assert.Equal(4, p.Tomadas);
        var tomadas = p.Pontos.Where(x => x.Tipo == TipoCarga.TomadaUsoGeral).ToList();
        Assert.Equal(3, tomadas.Where(x => x.PotenciaVA == 600).Sum(x => x.Quantidade));
        Assert.Equal(2, tomadas.Where(x => x.Posicao == PosicaoDoPonto.Media).Sum(x => x.Quantidade));
        Assert.Equal(1, tomadas.Where(x => x.PotenciaVA == 100).Sum(x => x.Quantidade));
    }

    [Fact]
    public void Banheiro_pede_1_tomada_de_600VA_na_media_e_chuveiro()
    {
        var bwc = new Ambiente("bwc", "Banheiro", UsoAmbiente.Banheiro, 3.2, 7.4);
        var p = PropostaDePontos.Propor(bwc, Padrao);
        Assert.Equal(1, p.Tomadas);
        var t = Assert.Single(p.Pontos, x => x.Tipo == TipoCarga.TomadaUsoGeral);
        Assert.Equal(600, t.PotenciaVA);
        Assert.Equal(PosicaoDoPonto.Media, t.Posicao);
        var ch = Assert.Single(p.Pontos, x => x.Tipo == TipoCarga.Chuveiro);
        Assert.Equal(5500, ch.PotenciaVA);
        var sem = PropostaDePontos.Propor(bwc, Padrao with { SugerirChuveiro = false });
        Assert.DoesNotContain(sem.Pontos, x => x.Tipo == TipoCarga.Chuveiro);
    }

    [Fact]
    public void Ambiente_pequeno_e_varanda_pedem_1_tomada_e_luz_na_parede()
    {
        var dep = new Ambiente("dep", "Depósito", UsoAmbiente.Deposito, 2.0, 5.8);
        var p = PropostaDePontos.Propor(dep, Padrao);
        Assert.Equal(1, p.Tomadas);
        Assert.Equal(PosicaoDoPonto.Alta, p.Pontos.Single(x => x.Tipo == TipoCarga.Iluminacao).Posicao);
        var var = PropostaDePontos.Propor(new Ambiente("var", "Varanda", UsoAmbiente.Varanda, 3.0, 7.0), Padrao);
        Assert.Equal(1, var.Tomadas);
    }

    [Fact]
    public void Proposta_do_apartamento_passa_no_verificar_por_ambiente()
    {
        // Lança o que a proposta manda, num circuito só por tipo, e confere que as regras A- não reclamam de quantidade nem de carga.
        var ex = new ApartamentoExemplo().Criar();
        var pontos = new List<Ponto>();
        var n = 0;
        foreach (var prop in PropostaDePontos.Propor(ex.Ambientes, Padrao))
        foreach (var pp in prop.Pontos)
        for (var k = 0; k < pp.Quantidade; k++)
            pontos.Add(new Ponto($"P{++n}", prop.Ambiente.Id, pp.Tipo == TipoCarga.Iluminacao ? "C1" : "C2", pp.Tipo, pp.PotenciaVA)
            {
                Altura = Padrao.Altura(pp.Posicao), NoTeto = pp.Posicao == PosicaoDoPonto.Teto, CorrenteDaTomada = pp.CorrenteDaTomada, VolumeDoBanheiro = pp.Tipo == TipoCarga.Chuveiro ? 1 : 3,
            });
        var i = new Instalacao("prop", ex.Alimentacao, ex.Ambientes, ex.Quadros, ex.Circuitos.Where(c => c.Id is "C1" or "C2"), pontos);
        var problemas = Verificacao.Verificar(i);
        Assert.DoesNotContain(problemas, p => p.Codigo is "A-01" or "A-02" or "A-03" or "A-04" or "A-06");
    }

    [Fact]
    public void Divisao_separa_luz_cozinha_servico_banheiro_e_chuveiro()
    {
        var ex = new ApartamentoExemplo().Criar();
        var pontos = ex.Pontos.Select(p => p with { Circuito = null }).ToList();
        var ambientes = ex.Ambientes.ToDictionary(a => a.Id, a => a);
        var circuitos = DivisaoDeCircuitos.Propor(pontos, ambientes, ex.Alimentacao, Padrao);

        // todos os pontos entram, uma vez só
        Assert.Equal(pontos.Count, circuitos.Sum(c => c.Pontos.Count));
        Assert.Equal(pontos.Count, circuitos.SelectMany(c => c.Pontos).Distinct().Count());

        // chuveiro, ar e micro-ondas (20 A) em circuito próprio; chuveiro e ar em fase-fase
        var ch = Assert.Single(circuitos, c => c.Pontos.Contains("CH"));
        Assert.Single(ch.Pontos);
        Assert.Equal(Ligacao.FF, ch.Ligacao);
        Assert.Equal(TipoCarga.Chuveiro, ch.Tipo);
        Assert.Single(Assert.Single(circuitos, c => c.Pontos.Contains("AC-d1")).Pontos);
        Assert.Single(Assert.Single(circuitos, c => c.Pontos.Contains("MO")).Pontos);

        // iluminação separada das tomadas
        foreach (var c in circuitos.Where(c => c.Tipo == TipoCarga.Iluminacao))
            Assert.All(c.Pontos, id => Assert.Equal(TipoCarga.Iluminacao, ex.Ponto(id)!.Tipo));

        // cozinha só com cozinha; área de serviço só com área de serviço; banheiro só com banheiro
        foreach (var c in circuitos.Where(c => c.Tipo == TipoCarga.TomadaUsoGeral))
        {
            var usos = c.Pontos.Select(id => ambientes[ex.Ponto(id)!.Ambiente].Uso).Distinct().ToList();
            if (usos.Contains(UsoAmbiente.Cozinha)) Assert.Equal([UsoAmbiente.Cozinha], usos);
            if (usos.Contains(UsoAmbiente.AreaServico)) Assert.Equal([UsoAmbiente.AreaServico], usos);
            if (usos.Contains(UsoAmbiente.Banheiro)) Assert.Equal([UsoAmbiente.Banheiro], usos);
        }

        // nenhum circuito agrupado passa do limite do padrão
        foreach (var c in circuitos.Where(c => c.Pontos.Count > 1))
        {
            Assert.True(c.CorrenteDeProjeto <= Padrao.CorrenteMaximaTug + 1e-9, $"{c.Numero} {c.Descricao}: {c.CorrenteDeProjeto:0.0} A");
            Assert.True(c.Pontos.Count <= Padrao.PontosMaximosPorCircuito);
        }

        // numeração contínua com o prefixo
        Assert.Equal(Enumerable.Range(1, circuitos.Count).Select(k => "C" + k), circuitos.Select(c => c.Numero));

        // montada de volta, a instalação passa no Verificar sem erros de divisão
        var novos = circuitos.Select(c => new Circuito(c.Numero, c.Numero.Substring(1), "QD", c.Tipo, c.Ligacao) { Descricao = c.Descricao }).ToList();
        var porPonto = circuitos.SelectMany(c => c.Pontos.Select(p => (p, c.Numero))).ToDictionary(x => x.p, x => x.Numero);
        var i = new Instalacao("div", ex.Alimentacao, ex.Ambientes, ex.Quadros, novos, pontos.Select(p => p with { Circuito = porPonto[p.Id] }));
        Assert.DoesNotContain(Verificacao.Verificar(i), p => p.Codigo is "C-01" or "C-03" or "C-04" or "M-01");
    }

    [Fact]
    public void Divisao_em_220V_mantem_chuveiro_fase_neutro_quando_cabe_no_limite()
    {
        var a = new Alimentacao(220, 380, 3, EsquemaAterramento.TNCS);
        var amb = new Dictionary<string, Ambiente> { ["bwc"] = new("bwc", "Banheiro", UsoAmbiente.Banheiro, 3, 7) };
        var ch = new Ponto("CH", "bwc", null, TipoCarga.Chuveiro, 4400);
        var c = Assert.Single(DivisaoDeCircuitos.Propor([ch], amb, a, Padrao));
        Assert.Equal(Ligacao.FN, c.Ligacao); // 20 A em 220 V não passa do limite de 20 A
        var forte = Assert.Single(DivisaoDeCircuitos.Propor([ch with { PotenciaVA = 7500 }], amb, a, Padrao));
        Assert.Equal(Ligacao.FF, forte.Ligacao);
        var mono = Assert.Single(DivisaoDeCircuitos.Propor([ch with { PotenciaVA = 7500 }], amb, a with { Fases = 1 }, Padrao));
        Assert.Equal(Ligacao.FN, mono.Ligacao); // monofásica: não há fase-fase
    }

    [Fact]
    public void Comando_conta_retornos_e_verificar_acusa_luminaria_sem_interruptor()
    {
        Assert.Equal(TipoDeComando.Simples, Comando.TipoPor(1));
        Assert.Equal(TipoDeComando.Paralelo, Comando.TipoPor(2));
        Assert.Equal(TipoDeComando.Intermediario, Comando.TipoPor(3));
        Assert.Equal(0, Comando.RetornosEntreInterruptores(TipoDeComando.Simples));
        Assert.Equal(2, Comando.RetornosEntreInterruptores(TipoDeComando.Paralelo));

        var ex = new ApartamentoExemplo().Criar();
        var comandos = new[] { new Comando("S-sala", "C1", ["I-sala"], ["L-sala"], TipoDeComando.Simples) };
        var i = new Instalacao(ex.Nome, ex.Alimentacao, ex.Ambientes, ex.Quadros, ex.Circuitos, ex.Pontos, ex.Trechos, comandos: comandos);
        var problemas = Verificacao.Verificar(i);
        Assert.DoesNotContain(problemas, p => p.Codigo == "L-01" && p.Elementos.Contains("L-sala"));
        Assert.Contains(problemas, p => p.Codigo == "L-01" && p.Elementos.Contains("L-coz"));

        // luminárias de circuitos diferentes num mesmo comando: erro
        var errado = new Instalacao(ex.Nome, ex.Alimentacao, ex.Ambientes, ex.Quadros, ex.Circuitos, ex.Pontos, ex.Trechos,
            comandos: [new Comando("S", "C1", ["I"], ["L-sala", "T-sala-1"], TipoDeComando.Simples)]);
        Assert.Contains(Verificacao.Verificar(errado), p => p.Codigo == "L-02");
    }

    [Fact]
    public void Retornos_entram_na_ocupacao_do_trecho()
    {
        var ex = new ApartamentoExemplo().Criar();
        var comRetorno = new Instalacao(ex.Nome, ex.Alimentacao, ex.Ambientes, ex.Quadros, ex.Circuitos, ex.Pontos,
            ex.Trechos.Select(t => t.Id == "QD-circA" ? t with { Retornos = ["C1", "C1"] } : t));
        var sem = CalculoEletrica.Executar(ex).Trechos.Single(t => t.Id == "QD-circA");
        var com = CalculoEletrica.Executar(comRetorno).Trechos.Single(t => t.Id == "QD-circA");
        Assert.Equal(sem.Condutores + 2, com.Condutores);
        Assert.True(com.AreaDosCondutores > sem.AreaDosCondutores);
    }

    [Fact]
    public void Grafo_acha_caminho_entre_dois_nos()
    {
        var ex = new ApartamentoExemplo().Criar();
        var grafo = new Grafo(ex);
        var caminho = grafo.CaminhoEntre("QD", "cxA-circ", "C1");
        Assert.NotNull(caminho);
        Assert.Single(caminho!);
        Assert.Null(grafo.CaminhoEntre("QD", "cxA-circ", "C5"));
        Assert.Empty(grafo.CaminhoEntre("QD", "QD", "C1")!);
    }
}
