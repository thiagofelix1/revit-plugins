using Spk.Motor.Exemplos;
using Spk.Motor.Calculo;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Testes;

public class ObstrucaoDeBicoTestes
{
    // Viga ao longo de X, em y = 0, com 0,20 m de largura: as laterais ficam em y = ±0,10. Fundo a 3,4 m, topo a 4 m.
    static Viga Viga(double largura = 0.2, double fundo = 3.4, double topo = 4) => new("v", "teste", new Ponto2(0, 0), new Ponto2(10, 0), largura, fundo, topo);

    static BicoNoEspaco Bico(string id, double x, double y, double defletor = 3.7) => new(id, new Ponto2(x, y), defletor);

    [Theory]
    [InlineData(0.10, 0.0)]
    [InlineData(0.30, 0.065)]
    [InlineData(0.449, 0.065)]
    [InlineData(0.60, 0.14)]
    [InlineData(1.35, 0.42)]
    [InlineData(2.249, 0.89)]
    public void Tabela_15(double distancia, double permitido) =>
        Assert.Equal(permitido, ObstrucaoDeBico.AlturaPermitida(distancia)!.Value, 6);

    [Fact]
    public void A_partir_de_2_25_m_a_tabela_nao_limita() =>
        Assert.Null(ObstrucaoDeBico.AlturaPermitida(2.25));

    [Fact]
    public void Defletor_acima_do_que_a_distancia_permite_e_acusado()
    {
        // A = 0,60 − 0,10 = 0,50 m: permite 0,09 m; o defletor está 0,30 m acima do fundo.
        var acusada = Assert.Single(ObstrucaoDeBico.Conferir([Bico("1", 5, 0.6)], [Viga()], 4.6));

        Assert.Equal(0.50, acusada.Distancia, 6);
        Assert.Equal(0.30, acusada.AcimaDoFundo, 6);
        Assert.Equal(0.09, acusada.Permitido, 6);
        Assert.False(acusada.PareceTrelica);
    }

    [Fact]
    public void Longe_o_bastante_passa() =>
        Assert.Empty(ObstrucaoDeBico.Conferir([Bico("1", 5, 1.2)], [Viga()], 4.6)); // A = 1,10 m: permite 0,305 m

    [Fact]
    public void Defletor_abaixo_do_fundo_da_viga_passa() =>
        Assert.Empty(ObstrucaoDeBico.Conferir([Bico("1", 5, 0.2, defletor: 3.3)], [Viga()], 4.6));

    [Fact]
    public void Viga_inteira_abaixo_do_bico_nao_e_da_tabela_15() =>
        Assert.Empty(ObstrucaoDeBico.Conferir([Bico("1", 5, 0.2, defletor: 4.2)], [Viga()], 4.6));

    [Fact]
    public void Depois_da_ponta_da_viga_conta_a_distancia_ate_a_ponta()
    {
        var acusada = Assert.Single(ObstrucaoDeBico.Conferir([Bico("1", 10.5, 0)], [Viga()], 4.6));

        Assert.Equal(0.5, acusada.Distancia, 6);
    }

    [Fact]
    public void Bicos_dos_dois_lados_a_meia_distancia_passam() =>
        Assert.Empty(ObstrucaoDeBico.Conferir([Bico("1", 5, 0.6), Bico("2", 5, -2.0)], [Viga()], 4.6));

    [Fact]
    public void Bico_do_outro_lado_longe_demais_nao_vale()
    {
        var acusadas = ObstrucaoDeBico.Conferir([Bico("1", 5, 0.6), Bico("2", 5, -2.5)], [Viga()], 4.6);

        Assert.Equal("1", Assert.Single(acusadas, a => a.Bico == "1").Bico);
    }

    [Fact]
    public void Obstrucao_de_1_2_m_ou_mais_nao_tem_a_excecao_dos_dois_lados() =>
        Assert.Contains(ObstrucaoDeBico.Conferir([Bico("1", 5, 1.0), Bico("2", 5, -1.0)], [Viga(largura: 1.3)], 4.6), a => a.Bico == "1");

    [Fact]
    public void Peca_alta_parece_trelica() =>
        Assert.True(Assert.Single(ObstrucaoDeBico.Conferir([Bico("1", 5, 0.6, defletor: 5)], [Viga(fundo: 3, topo: 5.5)], 4.6)).PareceTrelica);

    [Fact]
    public void Viga_macica_reprova_o_calculo_e_trelica_so_avisa()
    {
        var rede = new GalpaoEspinhaDePeixe { Ramais = 2, ChuveirosPorLado = 2, FatorK = 80 }.Criar();
        var bico = rede.Chuveiros[0].Id;
        ProjetoSpk Projeto(Viga viga) => new("Obstrução", rede, ClasseRisco.Ordinario2, new BaseDensidadeArea(8.1, 140))
        {
            VazaoHidrantes = 950,
            DuracaoMinutos = 60,
            Obstrucoes = [new ObstrucaoAcusada(bico, new Ponto2(0, 0), viga, 0.5, 0.3, 0.09)],
        };

        var macica = CalculoSpk.Executar(Projeto(Viga()));
        var trelica = CalculoSpk.Executar(Projeto(Viga(fundo: 3, topo: 5.5)));

        Assert.Contains(macica.Verificacoes, v => v.Item == ObstrucaoDeBico.Item && !v.Atende);
        Assert.Contains(trelica.Verificacoes, v => v.Item == ObstrucaoDeBico.Item && v.Atende);
        Assert.Contains(trelica.Avisos, a => a.StartsWith("ATENÇÃO") && a.Contains("treliça"));
    }
}
