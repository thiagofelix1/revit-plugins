using Spk.Motor.Calculo;
using Spk.Motor.Exemplos;
using Spk.Motor.Normas;
using Spk.Motor.Relatorios;

namespace Spk.Motor.Testes;

public class BuscaEProjetoTestes
{
    [Fact]
    public void Pior_area_fica_no_fim_da_rede_e_supera_qualquer_outra()
    {
        var rede = new GalpaoEspinhaDePeixe { Ramais = 10, ChuveirosPorLado = 6 }.Criar();
        var motor = new MotorHidraulico(rede);
        var baseProjeto = BaseDeProjeto.PelaFigura42(ClasseRisco.Ordinario2, 140);

        var busca = BuscaAreaMaisDesfavoravel.Executar(motor, baseProjeto);

        // 140 m² / 10,5 m² → 14 chuveiros; 1,2 × √140 / 3 → 5 por ramal.
        Assert.Equal(14, busca.ChuveirosNaArea);
        Assert.Equal(5, busca.ChuveirosPorRamal);
        Assert.Equal(14, busca.MaisDesfavoravel.Chuveiros.Count);
        Assert.Contains(busca.MaisDesfavoravel.Chuveiros, c => c.Ramal == "R9");
        Assert.Contains(busca.MaisDesfavoravel.ChuveiroCritico, new[] { "B-R9E5", "B-R9D5" });

        // Uma área perto do VGA exige menos pressão.
        var perto = rede.Chuveiros.Where(c => c.Ramal is "R0" or "R1" or "R2").Take(14).Select(c => baseProjeto.Requisito(c)).ToList();
        Assert.True(motor.CalcularDemanda(perto).PressaoNecessaria < busca.MaisDesfavoravel.PressaoNecessaria);
        Assert.All(busca.Ranking, c => Assert.True(c.PressaoNecessaria <= busca.MaisDesfavoravel.PressaoNecessaria + 1e-6));
    }

    [Fact]
    public void Grelha_converge_e_exige_menos_que_a_espinha_de_peixe()
    {
        var baseProjeto = BaseDeProjeto.PelaFigura42(ClasseRisco.Ordinario2, 140);
        var arvore = BuscaAreaMaisDesfavoravel.Executar(new MotorHidraulico(new GalpaoEspinhaDePeixe { Ramais = 12 }.Criar()), baseProjeto);
        var grelha = BuscaAreaMaisDesfavoravel.Executar(new MotorHidraulico(new GalpaoEspinhaDePeixe { Ramais = 12, Grelha = true }.Criar()), baseProjeto);

        Assert.True(grelha.MaisDesfavoravel.Estado.Convergiu);
        Assert.True(grelha.MaisDesfavoravel.Estado.ErroBalancoMaximo < 1e-6);
        Assert.True(grelha.MaisDesfavoravel.PressaoNecessaria < arvore.MaisDesfavoravel.PressaoNecessaria);
        // Na grelha, a malha leva água por trechos que não ligam a fonte à área diretamente.
        Assert.True(grelha.MaisDesfavoravel.Trechos.Count > arvore.MaisDesfavoravel.Trechos.Count);
    }

    [Fact]
    public void Esfr_abre_12_chuveiros_em_3_ramais()
    {
        var rede = new GalpaoEspinhaDePeixe { Ramais = 8, ChuveirosPorLado = 6, FatorK = 360 }.Criar();
        var busca = BuscaAreaMaisDesfavoravel.Executar(new MotorHidraulico(rede), BaseDeProjeto.Esfr(345, "Tabela 5.2.2.3.1 (exemplo)"));

        var chuveiros = busca.MaisDesfavoravel.Chuveiros;
        Assert.Equal(12, chuveiros.Count);
        Assert.Equal(3, chuveiros.Select(c => c.Ramal).Distinct().Count());
        Assert.All(chuveiros, c => Assert.True(c.Pressao >= 345 - 1e-6));
    }

    [Fact]
    public void Projeto_completo_gera_memorial_e_verificacoes()
    {
        var projeto = new ProjetoSpk("Galpão de exemplo — VGA 1", new GalpaoEspinhaDePeixe().Criar(), ClasseRisco.Ordinario2,
            BaseDeProjeto.PelaFigura42(ClasseRisco.Ordinario2, 140))
        {
            Abastecimento = new CurvaAbastecimento([(0, 900), (3000, 780), (4500, 600)], "Bomba de exemplo"),
        };

        var rel = CalculoSpk.Executar(projeto);
        var md = Memorial.Gerar(rel);

        Assert.Equal(950, rel.VazaoHidrantes);
        Assert.Equal(60, rel.DuracaoMinutos);
        Assert.True(rel.Atende, string.Join("\n", rel.Verificacoes.Where(v => !v.Atende).Select(v => v.Descricao + ": " + v.Detalhe)));
        Assert.Contains("Memorial de cálculo hidráulico", md);
        Assert.Contains(rel.Resultado.ChuveiroCritico, md);
    }

    [Fact]
    public void Setor_acima_da_Tabela_8_e_apontado()
    {
        // 30 ramais × 16 chuveiros × 10,5 m² = 5 040 m² > 4 800 m².
        var projeto = new ProjetoSpk("Setor grande", new GalpaoEspinhaDePeixe { Ramais = 30 }.Criar(), ClasseRisco.Ordinario1,
            BaseDeProjeto.PelaFigura42(ClasseRisco.Ordinario1, 140))
        {
            AreaOperacaoManual = ["B-R29E7", "B-R29E6", "B-R29E5", "B-R28E7", "B-R28E6", "B-R28E5"],
        };

        var rel = CalculoSpk.Executar(projeto);

        Assert.Contains(rel.Verificacoes, v => v.Item == "7.3, Tabela 8" && !v.Atende);
        Assert.Contains(rel.Verificacoes, v => v.Item == "9.4.4.2" && !v.Atende);
    }
}
