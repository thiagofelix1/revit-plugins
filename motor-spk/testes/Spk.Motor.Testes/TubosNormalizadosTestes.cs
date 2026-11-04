using Spk.Motor.Normas;

namespace Spk.Motor.Testes;

public class TubosNormalizadosTestes
{
    [Theory]
    [InlineData(25, TubosNormalizados.Media, 27.0)]
    [InlineData(65, TubosNormalizados.Media, 68.6)]
    [InlineData(100, TubosNormalizados.Media, 105.3)]
    [InlineData(150, TubosNormalizados.Pesada, 153.9)]
    [InlineData(65, TubosNormalizados.Sch40, 62.68)]
    [InlineData(90, TubosNormalizados.Sch40, 90.12)]
    [InlineData(100, TubosNormalizados.Sch10, 108.2)]
    [InlineData(150, TubosNormalizados.Sch40, 154.08)]
    [InlineData(150, TubosNormalizados.Sch80, 146.36)]
    public void Interno_e_o_externo_menos_duas_espessuras(int dn, string classe, double interno)
    {
        Assert.Equal(interno, Assert.Single(TubosNormalizados.Linhas(dn), l => l.Classe == classe).Interno, 2);
    }

    [Fact]
    public void Classe_leve_nao_existe_acima_de_DN_100()
    {
        Assert.Contains(TubosNormalizados.Linhas(100), l => l.Classe == TubosNormalizados.Leve);
        Assert.DoesNotContain(TubosNormalizados.Linhas(125), l => l.Classe == TubosNormalizados.Leve);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(32)]
    [InlineData(40)]
    [InlineData(50)]
    [InlineData(65)]
    [InlineData(80)]
    [InlineData(90)]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    [InlineData(200)]
    public void Referencia_dos_comprimentos_equivalentes_confere_com_a_NBR_5590(int dn)
    {
        Assert.Contains(TubosNormalizados.Identificar(dn, TubosAco.Schedule40(dn)), l => l.Classe == TubosNormalizados.Sch40);
        Assert.Contains(TubosNormalizados.Identificar(dn, TubosAco.Schedule10(dn)), l => l.Classe == TubosNormalizados.Sch10);
    }

    [Fact]
    public void Conexao_em_DN_90_tem_comprimento_equivalente()
    {
        Assert.Equal(17 * 0.3048, ComprimentosEquivalentes.Obter(TipoConexao.TeOuCruzetaFluxoDesviado, 90), 1e-9);
        Assert.Equal(17 * 0.3048, ComprimentosEquivalentes.Obter(TipoConexao.TeOuCruzetaFluxoDesviado, 90, 120, TubosAco.Schedule40(90)), 1e-9);
        Assert.Throws<ArgumentException>(() => ComprimentosEquivalentes.Obter(TipoConexao.ValvulaBorboleta, 90));
    }

    [Theory]
    [InlineData("Ferro Galvanizado Vermelho BSP", TubosNormalizados.Norma5580, true)]
    [InlineData("Aço preto NPT (Schedule 40)", TubosNormalizados.Norma5590, true)]
    [InlineData("Aço galvanizado (NBR 5580)", TubosNormalizados.Norma5580, false)]
    [InlineData("Aço carbono - Schedule 10", TubosNormalizados.Norma5590, false)]
    [InlineData("Aço carbono", null, false)]
    public void Norma_pelo_nome(string nome, string? norma, bool roscado)
    {
        var lido = TubosNormalizados.NormaPeloNome(nome);
        Assert.Equal(norma, lido.Norma);
        Assert.Equal(roscado, lido.Roscado);
    }

    [Fact]
    public void Rosca_BSP_com_interno_de_Sch_40_informa_a_favor_da_seguranca()
    {
        var (atencao, informa) = TubosNormalizados.Conferir("Ferro Galvanizado Vermelho BSP", [(100, 102.26), (65, 62.71)]);

        Assert.Empty(atencao);
        var texto = Assert.Single(informa);
        Assert.Contains("NBR 5580 (rosca BSP)", texto);
        Assert.Contains("DN 65, DN 100", texto);
        Assert.Contains("NBR 5590 Sch 40", texto);
        Assert.Contains("a favor da segurança", texto);
    }

    [Fact]
    public void Nome_de_Sch_40_com_interno_de_classe_media_e_contra_a_seguranca()
    {
        var (atencao, informa) = TubosNormalizados.Conferir("Aço galvanizado (Schedule 40)", [(100, 105.3)]);

        Assert.Empty(informa);
        var texto = Assert.Single(atencao);
        Assert.StartsWith("ATENÇÃO", texto);
        Assert.Contains("menor que a real", texto);
    }

    [Fact]
    public void Interno_fora_das_normas_avisa()
    {
        var texto = Assert.Single(TubosNormalizados.Conferir("Aço carbono", [(50, 50)]).Atencao);
        Assert.Contains("DN 50 com interno de 50,0 mm", texto);
    }

    [Fact]
    public void Tubo_certo_nao_avisa()
    {
        var (atencao, informa) = TubosNormalizados.Conferir("Ferro Galvanizado Vermelho BSP", [(25, 27.0), (100, 105.3), (150, 155.1)]);
        Assert.Empty(atencao);
        Assert.Empty(informa);
    }

    [Fact]
    public void Tipo_que_nao_se_diz_de_aco_nao_e_conferido()
    {
        Assert.Empty(TubosNormalizados.Conferir("Padrão", [(50, 50)]).Atencao);
        Assert.Empty(TubosNormalizados.Conferir("Padrão (Cobre - NBR 13206 - Classe A)", [(50, 50)]).Atencao);
    }

    [Fact]
    public void Tubo_roscado_de_parede_fina_avisa()
    {
        var texto = Assert.Single(TubosNormalizados.Conferir("Aço galvanizado BSP", [(50, 54.3)]).Atencao);
        Assert.Contains("NBR 5580 classe leve", texto);
        Assert.Contains("5.3.2.4", texto);
    }

    [Fact]
    public void Sch_10_em_DN_200_fica_abaixo_da_Tabela_3()
    {
        var texto = Assert.Single(TubosNormalizados.Conferir("Aço carbono", [(200, 211.58)]).Atencao);
        Assert.Contains("Tabela 3", texto);
        Assert.Contains("4,78", texto);
    }
}
