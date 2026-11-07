using Spk.Motor.Calculo;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Testes;

public class VelocidadeTestes
{
    /// <summary>Um bico na ponta de um tubo de 2 m, com a fonte na outra ponta.</summary>
    static ProjetoSpk UmBico(double interno, int dn, double fatorK, double area, double densidade, double comprimento = 2) =>
        new("Um bico",
            new Rede("F", [new No("F", 0, 0, 0), new No("B", comprimento, 0, 0)],
                [new Trecho("F-B", "F", "B", interno, comprimento, 120) { DiametroNominal = dn }],
                [new Chuveiro("B", "B", fatorK, area) { Ramal = "R1" }]),
            ClasseRisco.Ordinario2, new BaseDensidadeArea(densidade, area));

    [Fact]
    public void Velocidade_fora_do_comum_vira_atencao()
    {
        // 25 L/min/m² × 30 m² = 750 L/min num DN 25: cerca de 22 m/s.
        var rel = CalculoSpk.Executar(UmBico(26.64, 25, 360, 30, 25));

        var aviso = Assert.Single(rel.Avisos, a => a.Contains("velocidade acima de"));
        Assert.StartsWith("ATENÇÃO", aviso);
        Assert.Contains("F-B", aviso);
    }

    [Fact]
    public void Velocidade_comum_nao_avisa()
    {
        // 8,1 × 12 = cerca de 97 L/min num DN 50: menos de 1 m/s.
        var rel = CalculoSpk.Executar(UmBico(52.5, 50, 80, 12, 8.1));

        Assert.DoesNotContain(rel.Avisos, a => a.Contains("velocidade acima de"));
    }

    [Fact]
    public void Trecho_curto_da_ligacao_do_bico_nao_avisa()
    {
        // A mesma vazão alta, mas num trecho de 10 cm: é a ligação do bico à peça, com interno adotado.
        var rel = CalculoSpk.Executar(UmBico(26.64, 25, 360, 30, 25, comprimento: 0.1));

        Assert.DoesNotContain(rel.Avisos, a => a.Contains("velocidade acima de"));
    }
}
