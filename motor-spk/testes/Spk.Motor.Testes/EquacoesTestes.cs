using Spk.Motor.Calculo;
using Spk.Motor.Normas;

namespace Spk.Motor.Testes;

public class EquacoesTestes
{
    [Fact]
    public void HazenWilliams_bate_com_a_forma_em_unidades_americanas()
    {
        // NFPA 13 (US): p [psi/ft] = 4,52 Q^1,85 / (C^1,85 d^4,87), Q em gpm e d em polegadas.
        const double q = 100, d = 52.5, c = 120;
        var qGpm = q / 3.785411784;
        var dPol = d / 25.4;
        var psiPorPe = 4.52 * Math.Pow(qGpm, 1.85) / (Math.Pow(c, 1.85) * Math.Pow(dPol, 4.87));
        var esperado = psiPorPe * 6.894757 / 0.3048;

        Assert.Equal(esperado, Hidraulica.PerdaUnitaria(q, c, d), esperado * 0.005);
    }

    [Fact]
    public void Fator_K_usa_pressao_em_bar()
    {
        Assert.Equal(80 * Math.Sqrt(0.5), Hidraulica.VazaoChuveiro(80, 50), 1e-9);
        Assert.Equal(56.25, Hidraulica.PressaoParaVazao(80, 60), 1e-9);
    }

    [Fact]
    public void Pressao_de_velocidade_e_rho_v2_sobre_2()
    {
        // 225 × Q² / D⁴ (a NBR imprime 255).
        Assert.Equal(225.16 * 500.0 * 500.0 / Math.Pow(52.5, 4), Hidraulica.PressaoVelocidade(500, 52.5), 1e-3);
    }

    [Fact]
    public void Exemplo_da_Figura_46_da_NBR()
    {
        var (total, porRamal) = AreaDeOperacao.NumeroDeChuveiros(140, 11.1, 3.7);
        Assert.Equal(13, total);
        Assert.Equal(4, porRamal);
    }

    [Theory]
    [InlineData(ClasseRisco.Ordinario2, 140, 8.1)]
    [InlineData(ClasseRisco.Ordinario2, 100, 8.1)]
    [InlineData(ClasseRisco.Ordinario2, 370, 6.9)]
    [InlineData(ClasseRisco.Leve, 210, 3.475)]
    public void Curvas_da_Figura_42(ClasseRisco classe, double area, double densidade)
    {
        Assert.Equal(densidade, Figura42.Curva(classe).Densidade(area), 1e-9);
    }

    [Fact]
    public void Area_acima_da_curva_e_rejeitada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Figura42.Curva(ClasseRisco.Leve).Densidade(300));
    }

    [Fact]
    public void Ajustes_sao_somados_sobre_a_area_original()
    {
        var r = AreaDeOperacao.Ajustar(200, ClasseRisco.Ordinario1, new CondicoesAreaOperacao
        {
            ChuveirosRespostaRapida = true,
            PeDireito = 4,
            InclinacaoTeto = 0.2,
        });
        // −35 % (Figura 43 a 4 m) + 30 % (teto inclinado) = −5 %.
        Assert.Equal(190, r.AreaFinal, 1e-9);
        Assert.Equal(2, r.Ajustes.Count);
    }

    [Fact]
    public void Alta_temperatura_nao_reduz_abaixo_de_190()
    {
        var r = AreaDeOperacao.Ajustar(230, ClasseRisco.Extraordinario1, new CondicoesAreaOperacao { ChuveirosAltaTemperatura = true });
        Assert.Equal(190, r.AreaFinal, 1e-9);
    }

    [Fact]
    public void Resposta_rapida_em_risco_extra_nao_reduz()
    {
        var r = AreaDeOperacao.Ajustar(230, ClasseRisco.Extraordinario1, new CondicoesAreaOperacao { ChuveirosRespostaRapida = true, PeDireito = 4 });
        Assert.Equal(230, r.AreaFinal);
        Assert.NotEmpty(r.Avisos);
    }

    [Fact]
    public void Comprimento_equivalente_corrige_pelo_fator_C()
    {
        Assert.Equal(20 * 0.3048, ComprimentosEquivalentes.Obter(TipoConexao.TeOuCruzetaFluxoDesviado, 100), 1e-9);
        Assert.Equal(20 * 0.3048 * 0.713, ComprimentosEquivalentes.Obter(TipoConexao.TeOuCruzetaFluxoDesviado, 100, 100), 1e-9);
    }

    [Fact]
    public void Curva_de_ensaio_passa_pelos_pontos_medidos()
    {
        var curva = CurvaAbastecimento.DeEnsaio(600, 400, 2000);
        Assert.Equal(600, curva.PressaoDisponivel(0), 1e-9);
        Assert.Equal(400, curva.PressaoDisponivel(2000), 1e-9);
        Assert.Equal(600 - 200 * Math.Pow(0.5, 1.85), curva.PressaoDisponivel(1000), 1e-9);
    }
}
