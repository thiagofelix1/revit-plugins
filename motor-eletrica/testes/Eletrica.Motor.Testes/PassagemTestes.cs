using Eletrica.Motor.Calculo;
using Eletrica.Motor.Exemplos;
using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Testes;

/// <summary>Passagem de fiação: caminho de cada circuito, paralelos, retornos e avisos.</summary>
public class PassagemTestes
{
    static readonly Alimentacao Alimentacao = new(127, 220, 2, EsquemaAterramento.TNCS) { Habitacao = true };

    /// <summary>Quadro em "q", caixa em "x", ponto de cada circuito em "a"; dois (ou mais) eletrodutos paralelos de q a x.</summary>
    static Instalacao Paralelos(int paralelos, int circuitos)
    {
        var ids = Enumerable.Range(1, circuitos).Select(n => $"C{n}").ToList();
        var trechos = Enumerable.Range(1, paralelos).Select(n => new Trecho($"q-x-{n}", "q", "x", 3.0, ids))
            .Append(new Trecho("x-a", "x", "a", 2.0, ids)).ToList();
        return new Instalacao("paralelos", Alimentacao,
            [new Ambiente("sala", "Sala", UsoAmbiente.Sala, 12, 14)],
            [new Quadro("QD", "QD-1", 2) { No = "q" }],
            ids.Select(id => new Circuito(id, id.Substring(1), "QD", TipoCarga.TomadaUsoGeral, Ligacao.FN)).ToList(),
            ids.Select(id => new Ponto($"P-{id}", "sala", id, TipoCarga.TomadaUsoGeral, 100) { No = "a" }).ToList(),
            trechos);
    }

    [Fact]
    public void Circuitos_se_dividem_entre_eletrodutos_paralelos()
    {
        var r = Passagem.Executar(Paralelos(paralelos: 2, circuitos: 4));
        Assert.Equal(4, r.CircuitosLevados);
        Assert.Equal(2, r.CircuitosPorTrecho["q-x-1"].Count);
        Assert.Equal(2, r.CircuitosPorTrecho["q-x-2"].Count);
        Assert.Equal(4, r.CircuitosPorTrecho["x-a"].Count);
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public void Paralelo_bem_mais_longo_nao_entra_na_divisao()
    {
        var i = Paralelos(paralelos: 2, circuitos: 3);
        var trechos = i.Trechos.Select(t => t.Id == "q-x-2" ? t with { Comprimento = 9.0 } : t).ToList();
        var longo = new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, i.Pontos, trechos);
        var r = Passagem.Executar(longo);
        Assert.Equal(3, r.CircuitosPorTrecho["q-x-1"].Count);
        Assert.False(r.CircuitosPorTrecho.ContainsKey("q-x-2"));
    }

    [Fact]
    public void Circuito_evitado_num_paralelo_vai_pelo_outro()
    {
        var i = Paralelos(paralelos: 2, circuitos: 2);
        // C1 não pode passar por q-x-1 (fiação manual "evitar"): vai pelo q-x-2, e o C2 também, que é o menos ocupado depois.
        var trechos = i.Trechos.Select(t => t.Id == "q-x-1" ? t with { Circuitos = ["C2"] } : t).ToList();
        var r = Passagem.Executar(new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, i.Pontos, trechos));
        Assert.Contains("C1", r.CircuitosPorTrecho["q-x-2"]);
        Assert.DoesNotContain("C1", r.CircuitosPorTrecho.TryGetValue("q-x-1", out var l) ? l : []);
    }

    [Fact]
    public void Pontos_sem_caminho_viram_um_aviso_por_circuito()
    {
        var i = Paralelos(paralelos: 1, circuitos: 1);
        var pontos = i.Pontos.Concat([
            new Ponto("solto-1", "sala", "C1", TipoCarga.TomadaUsoGeral, 100) { No = "longe-1" },
            new Ponto("solto-2", "sala", "C1", TipoCarga.TomadaUsoGeral, 100) { No = "longe-2" },
        ]).ToList();
        var r = Passagem.Executar(new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, pontos, i.Trechos));
        var aviso = Assert.Single(r.Avisos);
        Assert.Contains("2 de 3 ponto(s)", aviso);
        Assert.Equal(1, r.CircuitosLevados);
    }

    [Fact]
    public void Exemplo_leva_todos_os_circuitos_e_conta_retornos()
    {
        var ex = new ApartamentoExemplo().Criar();
        // Topologia aberta: todos os circuitos podem passar em todos os trechos.
        var todos = ex.Circuitos.Select(c => c.Id).ToList();
        var aberta = new Instalacao(ex.Nome, ex.Alimentacao, ex.Ambientes, ex.Quadros, ex.Circuitos, ex.Pontos,
            ex.Trechos.Select(t => t with { Circuitos = todos }),
            comandos: [new Comando("S-sala", "C1", [ex.Pontos.First(p => p.Tipo == TipoCarga.TomadaUsoGeral).Id], [ex.Pontos.First(p => p.Tipo == TipoCarga.Iluminacao).Id], TipoDeComando.Simples)]);
        var r = Passagem.Executar(aberta);
        Assert.Equal(ex.Circuitos.Count(c => ex.PontosDo(c).Any()), r.CircuitosLevados);
        // Cada circuito chega a todos os seus pontos: os trechos marcados com ele formam caminho até cada ponto.
        foreach (var c in ex.Circuitos.Where(c => ex.PontosDo(c).Any()))
            Assert.Contains(r.CircuitosPorTrecho, kv => kv.Value.Contains(c.Id));
        Assert.DoesNotContain(r.Avisos, a => a.Contains("sem caminho") || a.Contains("nenhum dos"));
    }
}
