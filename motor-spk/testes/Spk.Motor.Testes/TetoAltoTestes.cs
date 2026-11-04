using Spk.Motor.Calculo;
using Spk.Motor.Exemplos;
using Spk.Motor.Normas;

namespace Spk.Motor.Testes;

public class TetoAltoTestes
{
    static ProjetoSpk Projeto(double fatorK, double altura, BaseDeProjeto criterio, ClasseRisco classe = ClasseRisco.Ordinario2)
    {
        var rede = new GalpaoEspinhaDePeixe { Ramais = 4, ChuveirosPorLado = 4, FatorK = fatorK }.Criar();
        return new ProjetoSpk("Teto alto", rede, classe, criterio)
        {
            VazaoHidrantes = 1900,
            DuracaoMinutos = 60,
            AlturaDoTeto = rede.Chuveiros.ToDictionary(c => c.Id, _ => altura),
        };
    }

    [Fact]
    public void Pressao_acima_de_2070_kPa_reprova_o_tubo_de_aco()
    {
        var alta = CalculoSpk.Executar(Projeto(80, 5, new BaseDensidadeArea(60, 140)));
        var normal = CalculoSpk.Executar(Projeto(80, 5, new BaseDensidadeArea(8.1, 140)));

        Assert.Contains(alta.Verificacoes, v => v.Item == "5.3.2" && !v.Atende);
        Assert.Contains(normal.Verificacoes, v => v.Item == "5.3.2" && v.Atende);
    }

    [Theory]
    [InlineData(ClasseRisco.Leve, 7.0, 80, 4, 140)]
    [InlineData(ClasseRisco.Leve, 12.0, 115, 8, 230)]
    [InlineData(ClasseRisco.Ordinario1, 7.0, 115, 8, 230)]
    [InlineData(ClasseRisco.Ordinario2, 12.9, 115, 8, 230)]
    [InlineData(ClasseRisco.Ordinario2, 18.0, 115, 8, 230)]
    [InlineData(ClasseRisco.Extraordinario1, 9.0, 160, 12, 230)]
    [InlineData(ClasseRisco.Extraordinario2, 13.5, 160, 12, 340)]
    [InlineData(ClasseRisco.Extraordinario1, 13.6, 160, 20, 280)]
    public void Tabelas_7_e_8(ClasseRisco classe, double altura, double k, double densidade, double area)
    {
        var e = TetoAlto.Exigencia(classe, altura)!;

        Assert.Equal(k, e.FatorKMinimo);
        Assert.Equal(densidade, e.Densidade);
        Assert.Equal(area, e.AreaOperacao);
    }

    [Theory]
    [InlineData(ClasseRisco.Ordinario2, 6.1)]
    [InlineData(ClasseRisco.Armazenamento, 12.0)]
    [InlineData(ClasseRisco.Ordinario2, 30.5)]
    public void Fora_das_tabelas_nao_tem_exigencia(ClasseRisco classe, double altura) =>
        Assert.Null(TetoAlto.Exigencia(classe, altura));

    [Fact]
    public void Acima_de_18_m_so_K360()
    {
        var e = TetoAlto.Exigencia(ClasseRisco.Leve, 18.5)!;

        Assert.Equal(360, e.FatorKMinimo);
        Assert.Null(e.Densidade);
    }

    /// <summary>O caso real: área de vendas com 12,9 m, bico K 80, Figura 42 com 140 m². A pressão pode bastar, e o resultado não vale.</summary>
    [Fact]
    public void K_e_demanda_abaixo_da_tabela_nao_atendem()
    {
        var verificacoes = TetoAlto.Verificar(Projeto(80, 12.9, BaseDeProjeto.PelaFigura42(ClasseRisco.Ordinario2, 140)), 60);

        Assert.Contains(verificacoes, v => v.Item.EndsWith("Tabela 7") && !v.Atende && v.Detalhe.Contains("K 115"));
        Assert.Contains(verificacoes, v => v.Item.EndsWith("Tabela 8") && !v.Atende && v.Detalhe.Contains("sobre 230 m²"));
        Assert.Contains(verificacoes, v => v.Item.EndsWith("Tabela 11") && v.Atende);
    }

    [Fact]
    public void Projeto_que_segue_a_tabela_atende() =>
        Assert.All(TetoAlto.Verificar(Projeto(115, 12.9, new BaseDensidadeArea(8, 230)), 60), v => Assert.True(v.Atende, v.Detalhe));

    [Fact]
    public void K360_com_12_chuveiros_a_meio_bar_atende_pela_tabela_10() =>
        Assert.All(TetoAlto.Verificar(Projeto(360, 25, BaseDeProjeto.Esfr(50, "Tabela 10")), 60), v => Assert.True(v.Atende, v.Detalhe));

    [Fact]
    public void Acima_de_30_m_e_com_o_engenheiro()
    {
        var verificacao = Assert.Single(TetoAlto.Verificar(Projeto(360, 31, BaseDeProjeto.Esfr(50, "Tabela 10")), 60));

        Assert.False(verificacao.Atende);
    }

    [Fact]
    public void Duracao_menor_que_a_tabela_11_nao_atende() =>
        Assert.Contains(TetoAlto.Verificar(Projeto(115, 12.9, new BaseDensidadeArea(8, 230)), 30), v => v.Item.EndsWith("Tabela 11") && !v.Atende);

    [Fact]
    public void Teto_baixo_e_armazenamento_nao_entram()
    {
        Assert.Empty(TetoAlto.Verificar(Projeto(80, 3, new BaseDensidadeArea(8, 140)), 60));
        Assert.Empty(TetoAlto.Verificar(Projeto(80, 12, new BaseDensidadeArea(8, 140), ClasseRisco.Armazenamento), 60));
    }

    [Fact]
    public void O_calculo_reprova_mesmo_com_pressao_folgada()
    {
        var relatorio = CalculoSpk.Executar(Projeto(80, 12.9, BaseDeProjeto.PelaFigura42(ClasseRisco.Ordinario2, 140)));

        Assert.False(relatorio.Atende);
        Assert.Contains(relatorio.Verificacoes, v => v.Descricao == "Teto alto: fator K mínimo" && !v.Atende);
    }
}
