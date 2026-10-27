using Spk.Motor.Calculo;
using Spk.Motor.Exemplos;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Testes;

public class DiametrosTestes
{
    [Theory]
    [InlineData(ClasseRisco.Leve, 1, 25)]
    [InlineData(ClasseRisco.Leve, 2, 25)]
    [InlineData(ClasseRisco.Leve, 3, 32)]
    [InlineData(ClasseRisco.Leve, 5, 40)]
    [InlineData(ClasseRisco.Leve, 6, 50)]
    [InlineData(ClasseRisco.Leve, 30, 65)]
    [InlineData(ClasseRisco.Leve, 31, 80)]
    [InlineData(ClasseRisco.Leve, 100, 90)]
    [InlineData(ClasseRisco.Leve, 101, 125)] // acima do DN 90 segue a tabela de ordinário
    [InlineData(ClasseRisco.Ordinario1, 20, 65)]
    [InlineData(ClasseRisco.Ordinario2, 21, 80)]
    [InlineData(ClasseRisco.Ordinario2, 275, 150)]
    [InlineData(ClasseRisco.Ordinario2, 276, 200)]
    public void Tabelas_12_e_13_em_aco(ClasseRisco classe, int chuveiros, int dn) =>
        Assert.Equal(dn, TabelaDeDiametros.DnMinimo(classe, Material.Aco, chuveiros));

    [Theory]
    [InlineData(10, 50)]
    [InlineData(11, 65)]
    [InlineData(16, 80)]
    [InlineData(31, 90)]
    [InlineData(61, 100)]
    [InlineData(101, 125)]
    public void Tabela_14_com_espacamento_maior_que_3_7_m(int chuveiros, int dn) =>
        Assert.Equal(dn, TabelaDeDiametros.DnMinimo(ClasseRisco.Ordinario1, Material.Aco, chuveiros, espacamentoMaior: true));

    [Fact]
    public void Cobre_alimenta_mais_chuveiros() =>
        Assert.Equal(50, TabelaDeDiametros.DnMinimo(ClasseRisco.Leve, Material.Cobre, 12));

    [Fact]
    public void Risco_extra_nao_tem_tabela() =>
        Assert.Throws<ArgumentException>(() => TabelaDeDiametros.DnMinimo(ClasseRisco.Extraordinario1, Material.Aco, 5));

    [Fact]
    public void Espinha_de_peixe_conta_os_chuveiros_a_jusante()
    {
        // 6 ramais × 2 lados × 4 chuveiros = 48; 3,0 × 3,5 m: nada passa de 3,7 m.
        var rede = new GalpaoEspinhaDePeixe { Ramais = 6, ChuveirosPorLado = 4 }.Criar();
        var d = DimensionamentoPorTabela.Calcular(rede, ClasseRisco.Ordinario1);

        Assert.False(d.EspacamentoMaior);
        Assert.Equal(48, d.ChuveirosPorTrecho["COLUNA"]);
        Assert.Equal(90, d.DnPorTrecho["COLUNA"]); // 41 a 65 chuveiros
        Assert.Equal(40, d.ChuveirosPorTrecho["SG0-1"]);
        Assert.Equal(80, d.DnPorTrecho["SG0-1"]);
        Assert.Equal(4, d.ChuveirosPorTrecho["T-R0E0"]);
        Assert.Equal(40, d.DnPorTrecho["T-R0E0"]);
        Assert.Equal(25, d.DnPorTrecho["T-R0E3"]);
        Assert.Contains(d.Avisos, a => a.Contains("465 m²")); // 48 × 10,5 m² = 504 m²
        Assert.DoesNotContain(d.Avisos, a => a.Contains("lado da subgeral"));
    }

    [Fact]
    public void Ramal_com_mais_de_8_de_um_lado_avisa()
    {
        var rede = new GalpaoEspinhaDePeixe { Ramais = 2, ChuveirosPorLado = 9 }.Criar();
        var d = DimensionamentoPorTabela.Calcular(rede, ClasseRisco.Leve);

        Assert.Contains(d.Avisos, a => a.StartsWith("4 ramal(is)"));
    }

    [Fact]
    public void Ramais_a_4_m_usam_a_tabela_14()
    {
        var rede = new GalpaoEspinhaDePeixe { Ramais = 6, ChuveirosPorLado = 4, EspacamentoRamais = 4.0 }.Criar();
        var d = DimensionamentoPorTabela.Calcular(rede, ClasseRisco.Ordinario2);

        Assert.True(d.EspacamentoMaior);
        Assert.Equal(90, d.DnPorTrecho["COLUNA"]); // Tabela 14: DN 90 até 60 chuveiros (na 13, 65)
        Assert.Equal(80, d.DnPorTrecho["SG3-4"]); // 16 chuveiros: na Tabela 14 o DN 65 vai só até 15
    }

    [Fact]
    public void Grelha_nao_se_dimensiona_por_tabela()
    {
        var rede = new GalpaoEspinhaDePeixe { Ramais = 3, ChuveirosPorLado = 2, Grelha = true }.Criar();
        Assert.Throws<InvalidOperationException>(() => DimensionamentoPorTabela.Calcular(rede, ClasseRisco.Leve));
    }
}
