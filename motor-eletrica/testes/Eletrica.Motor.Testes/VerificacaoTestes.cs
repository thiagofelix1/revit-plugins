using Eletrica.Motor.Calculo;
using Eletrica.Motor.Exemplos;
using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Testes;

public class VerificacaoTestes
{
    static Instalacao Sabotar(Func<Instalacao, (IEnumerable<Circuito>? circuitos, IEnumerable<Ponto>? pontos, IEnumerable<Ambiente>? ambientes)> alterar)
    {
        var i = new ApartamentoExemplo().Criar();
        var (c, p, a) = alterar(i);
        return new Instalacao(i.Nome, i.Alimentacao, a ?? i.Ambientes, i.Quadros, c ?? i.Circuitos, p ?? i.Pontos, i.Trechos);
    }

    [Fact]
    public void Exemplo_nao_tem_erros()
    {
        var problemas = Verificacao.Verificar(new ApartamentoExemplo().Criar());
        Assert.DoesNotContain(problemas, p => p.Gravidade == Gravidade.Erro);
        Assert.Contains(problemas, p => p.Codigo == "DR-01" && p.Elementos.Contains("C3"));
        Assert.Contains(problemas, p => p.Codigo == "Q-02");
        Assert.Contains(problemas, p => p.Codigo == "Q-01" && p.Mensagem.Contains("3 espaço"));
    }

    [Fact]
    public void Ponto_sem_circuito_e_erro()
    {
        var i = Sabotar(x => (null, x.Pontos.Select(p => p.Id == "T-sala-1" ? p with { Circuito = null } : p), null));
        Assert.Contains(Verificacao.Verificar(i), p => p.Codigo == "M-01" && p.Elementos.Contains("T-sala-1"));
    }

    [Fact]
    public void Tomadas_de_menos_na_sala()
    {
        var i = Sabotar(x => (null, x.Pontos.Where(p => p.Id is not ("T-sala-3" or "T-sala-4")), null));
        var problemas = Verificacao.Verificar(i);
        Assert.Contains(problemas, p => p.Codigo == "A-01" && p.Elementos.Contains("sala") && p.Mensagem.Contains("é 4"));
    }

    [Fact]
    public void Comodo_sem_luz_no_teto()
    {
        var i = Sabotar(x => (null, x.Pontos.Select(p => p.Id == "L-d2" ? p with { NoTeto = false } : p), null));
        Assert.Contains(Verificacao.Verificar(i), p => p.Codigo == "A-03" && p.Elementos.Contains("d2"));
        var sem = Sabotar(x => (null, x.Pontos.Where(p => p.Id != "L-d2"), null));
        Assert.Contains(Verificacao.Verificar(sem), p => p.Codigo == "A-02" && p.Elementos.Contains("d2"));
    }

    [Fact]
    public void Carga_de_iluminacao_abaixo_do_minimo()
    {
        var i = Sabotar(x => (null, x.Pontos.Select(p => p.Id == "L-sala" ? p with { PotenciaVA = 100 } : p), null));
        Assert.Contains(Verificacao.Verificar(i), p => p.Codigo == "A-04" && p.Mensagem.Contains("280 VA"));
    }

    [Fact]
    public void Chuveiro_em_tomada_e_tomada_no_volume_1()
    {
        var i = Sabotar(x => (null, x.Pontos.Select(p => p.Id == "CH" ? p with { CorrenteDaTomada = 20 } : p.Id == "T-bwc" ? p with { VolumeDoBanheiro = 1 } : p), null));
        var problemas = Verificacao.Verificar(i);
        Assert.Contains(problemas, p => p.Codigo == "B-02");
        Assert.Contains(problemas, p => p.Codigo == "B-01" && p.Elementos.Contains("T-bwc"));
    }

    [Fact]
    public void Luz_e_tomada_no_mesmo_circuito()
    {
        // tomadas da sala no circuito de iluminação: IB = (980 + 400)/127 = 10,9 A ≤ 16 A → aviso, não erro
        var i = Sabotar(x => (null, x.Pontos.Select(p => p.Id.StartsWith("T-sala") ? p with { Circuito = "C1" } : p), null));
        var problemas = Verificacao.Verificar(i);
        Assert.Contains(problemas, p => p.Codigo == "C-02" && p.Elementos.Contains("C1"));
        // com o chuveiro junto passa de 16 A → erro
        var pesado = Sabotar(x => (null, x.Pontos.Select(p => p.Id.StartsWith("T-sala") || p.Id == "CH" ? p with { Circuito = "C1" } : p), null));
        var problemas2 = Verificacao.Verificar(pesado);
        Assert.Contains(problemas2, p => p.Codigo == "C-01");
        Assert.Contains(problemas2, p => p.Codigo == "C-03" && p.Elementos.Contains("CH"));
    }

    [Fact]
    public void Tomadas_de_cozinha_misturadas_com_a_sala()
    {
        var i = Sabotar(x => (null, x.Pontos.Select(p => p.Id == "T-coz-4" ? p with { Circuito = "C2" } : p), null));
        Assert.Contains(Verificacao.Verificar(i), p => p.Codigo == "C-04" && p.Elementos.Contains("C2"));
    }

    [Fact]
    public void Ponto_sem_caminho_pelos_eletrodutos()
    {
        var i = new ApartamentoExemplo().Criar();
        var sem = new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, i.Pontos, i.Trechos.Where(t => t.Id != "desc-T-var"));
        Assert.Contains(Verificacao.Verificar(sem), p => p.Codigo == "M-03" && p.Elementos.Contains("T-var"));
    }

    [Fact]
    public void Nao_residencial_nao_cobra_pontos_minimos()
    {
        var i = new ApartamentoExemplo().Criar();
        var comercial = new Instalacao(i.Nome, i.Alimentacao with { Habitacao = false }, i.Ambientes, i.Quadros, i.Circuitos,
            i.Pontos.Where(p => p.Id is not ("T-sala-3" or "T-sala-4")), i.Trechos.Where(t => t.Id is not ("desc-T-sala-3" or "desc-T-sala-4")));
        Assert.DoesNotContain(Verificacao.Verificar(comercial), p => p.Codigo == "A-01");
    }

    [Fact]
    public void Modelo_invalido_lista_todos_os_erros()
    {
        var e = Assert.Throws<ErroDeModelo>(() => new Instalacao("x", new Alimentacao(127, 220, 2, EsquemaAterramento.TNCS), [], [],
            [new Circuito("C1", "1", "QX", TipoCarga.Iluminacao, Ligacao.FN)], [new Ponto("P", "amb", "C9", TipoCarga.Iluminacao, 100)]));
        Assert.Contains(e.Erros, m => m.Contains("QX"));
        Assert.Contains(e.Erros, m => m.Contains("amb"));
        Assert.Contains(e.Erros, m => m.Contains("C9"));
        Assert.Contains(e.Erros, m => m.Contains("pelo menos um quadro"));
    }
}
