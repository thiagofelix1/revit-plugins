using Spk.Motor.Calculo;
using Spk.Motor.Exemplos;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Testes;

/// <summary>
/// O sentido em que o trecho foi anotado (De → Para) vem da ordem dos conectores no Revit e muda de uma leitura para
/// outra. A água escolhe o sentido; o resultado não pode depender da anotação.
/// </summary>
public class SentidoDosTrechosTestes
{
    static RelatorioSpk Calcular(Rede rede) =>
        CalculoSpk.Executar(new ProjetoSpk("Sentido", rede, ClasseRisco.Ordinario2, BaseDeProjeto.PelaFigura42(ClasseRisco.Ordinario2, 140)));

    static double PressaoDoCritico(RelatorioSpk rel) =>
        rel.Resultado.Chuveiros.Single(c => c.Id == rel.Resultado.ChuveiroCritico).Pressao;

    static Rede Inverter(Rede rede, Func<int, bool> quais) =>
        new(rede.NoFonte, rede.Nos, rede.Trechos.Select((t, i) => quais(i) ? t with { De = t.Para, Para = t.De } : t), rede.Chuveiros);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inverter_trechos_nao_muda_o_resultado(bool grelha)
    {
        var rede = new GalpaoEspinhaDePeixe { Ramais = 6, ChuveirosPorLado = 5, Grelha = grelha }.Criar();
        var original = Calcular(rede);

        foreach (var invertida in new[] { Inverter(rede, _ => true), Inverter(rede, i => i % 2 == 0), Inverter(rede, i => i % 3 == 1) })
        {
            var rel = Calcular(invertida);
            Assert.Equal(original.Resultado.PressaoNecessaria, rel.Resultado.PressaoNecessaria, 3);
            // Em rede simétrica, dois bicos espelhados empatam: compara a pressão do crítico, não o nome.
            Assert.Equal(PressaoDoCritico(original), PressaoDoCritico(rel), 3);
        }
    }
}
