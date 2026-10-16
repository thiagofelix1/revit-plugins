using Spk.Motor.Calculo;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Testes;

public class MotorHidraulicoTestes
{
    const double G = Hidraulica.PesoEspecificoAgua;
    static readonly double D25 = TubosAco.Schedule40(25);
    static readonly double D40 = TubosAco.Schedule40(40);

    static RequisitoChuveiro Req(string id, double q) => new(id, q, Nbr10897.PressaoMinimaChuveiro);

    [Fact]
    public void Um_tubo_e_um_chuveiro_mais_alto()
    {
        var rede = new Rede("S",
            [new No("S", 0, 0, 0), new No("B", 10, 0, 5)],
            [new Trecho("T", "S", "B", D25, 10, 120)],
            [new Chuveiro("B1", "B", 80, 12)]);

        var r = new MotorHidraulico(rede).CalcularDemanda([Req("B1", 60)]);

        var esperado = 56.25 + Hidraulica.PerdaUnitaria(60, 120, D25) * 10 + G * 5;
        Assert.Equal(esperado, r.PressaoNecessaria, 0.01);
        Assert.Equal(60, r.VazaoNecessaria, 0.01);
    }

    [Fact]
    public void Pressao_minima_de_48_kPa_governa_quando_a_densidade_e_baixa()
    {
        var rede = new Rede("S",
            [new No("S", 0, 0, 0), new No("B", 3, 0, 0)],
            [new Trecho("T", "S", "B", D25, 3, 120)],
            [new Chuveiro("B1", "B", 80, 9)]);

        var r = new MotorHidraulico(rede).CalcularDemanda([Req("B1", 20)]);

        Assert.Equal(48, r.Chuveiros[0].Pressao, 0.01);
        Assert.Equal(80 * Math.Sqrt(0.48), r.Chuveiros[0].Vazao, 0.01);
    }

    [Fact]
    public void Arvore_bate_com_o_metodo_classico_de_mao()
    {
        // S —6 m DN40— J; J —3 m DN25— B1 (remoto); J —1 m DN25— B2.
        var rede = new Rede("S",
            [new No("S", 0, 0, 0), new No("J", 6, 0, 0), new No("N1", 9, 0, 0), new No("N2", 6, 1, 0)],
            [new Trecho("T1", "S", "J", D40, 6, 120), new Trecho("T2", "J", "N1", D25, 3, 120), new Trecho("T3", "J", "N2", D25, 1, 120)],
            [new Chuveiro("B1", "N1", 80, 12), new Chuveiro("B2", "N2", 80, 12)]);

        var r = new MotorHidraulico(rede).CalcularDemanda([Req("B1", 60), Req("B2", 60)]);

        // Método clássico: o remoto no mínimo, sobe até o nó, e o outro ramo descarrega com a pressão do nó.
        var pJ = 56.25 + Hidraulica.PerdaUnitaria(60, 120, D25) * 3;
        var q2 = Bissecao(q => Hidraulica.PressaoParaVazao(80, q) + Hidraulica.PerdaUnitaria(q, 120, D25) * 1 - pJ, 0, 500);
        var ps = pJ + Hidraulica.PerdaUnitaria(60 + q2, 120, D40) * 6;

        Assert.Equal("B1", r.ChuveiroCritico);
        Assert.Equal(ps, r.PressaoNecessaria, 0.01);
        Assert.Equal(60 + q2, r.VazaoNecessaria, 0.01);
        Assert.True(q2 > 60);
    }

    [Fact]
    public void Grelha_conserva_massa_e_fecha_a_perda_de_carga_na_malha()
    {
        // Malha A-B-D-C-A alimentada em A; chuveiros em B, C e D com trechos diferentes.
        var rede = new Rede("S",
            [new No("S", 0, -5, 0), new No("A", 0, 0, 0), new No("B", 4, 0, 0), new No("C", 0, 3, 0), new No("D", 4, 3, 0)],
            [
                new Trecho("SA", "S", "A", D40, 5, 120),
                new Trecho("AB", "A", "B", D25, 4, 120),
                new Trecho("AC", "A", "C", D25, 3, 120),
                new Trecho("BD", "B", "D", D25, 3, 120),
                new Trecho("CD", "C", "D", D25, 4, 120) { ComprimentoEquivalente = 2 },
            ],
            [new Chuveiro("BB", "B", 80, 12), new Chuveiro("BC", "C", 80, 12), new Chuveiro("BD", "D", 80, 12)]);

        var r = new MotorHidraulico(rede).CalcularDemanda([Req("BB", 60), Req("BC", 60), Req("BD", 60)]);
        var e = r.Estado;
        Assert.True(e.Convergiu);
        Assert.True(e.ErroBalancoMaximo < 1e-6);

        double Perda(int j) => Math.Sign(e.VazaoTrechos[j]) * Hidraulica.Resistencia(rede.Trechos[j].ComprimentoTotal, 120, D25)
                                * Math.Pow(Math.Abs(e.VazaoTrechos[j]), 1.85);
        // A→B→D menos A→C→D tem que dar zero.
        var fechamento = Perda(1) + Perda(3) - Perda(2) - Perda(4);
        Assert.True(Math.Abs(fechamento) < 1e-6, $"fechamento {fechamento}");
        Assert.Equal("BD", r.ChuveiroCritico);
    }

    [Fact]
    public void Ramos_fechados_nao_alteram_o_resultado()
    {
        var completa = new Exemplos.GalpaoEspinhaDePeixe { Ramais = 4, ChuveirosPorLado = 4 }.Criar();
        var abertos = new[] { "B-R3D3", "B-R3D2", "B-R2D3", "B-R2D2" };

        // Mesma rede só com o caminho até os abertos.
        var manter = new HashSet<string> { "COLUNA", "GERAL", "SG0-1", "SG1-2", "SG2-3", "T-R2D0", "T-R2D1", "T-R2D2", "T-R2D3", "T-R3D0", "T-R3D1", "T-R3D2", "T-R3D3" };
        var trechos = completa.Trechos.Where(t => manter.Contains(t.Id)).ToList();
        var nosUsados = new HashSet<string>(trechos.SelectMany(t => new[] { t.De, t.Para }));
        var reduzida = new Rede("VGA", completa.Nos.Where(n => nosUsados.Contains(n.Id)),
            trechos, completa.Chuveiros.Where(c => nosUsados.Contains(c.No)));

        var reqs = abertos.Select(id => Req(id, 70)).ToList();
        var a = new MotorHidraulico(completa).CalcularDemanda(reqs);
        var b = new MotorHidraulico(reduzida).CalcularDemanda(reqs);

        Assert.Equal(b.PressaoNecessaria, a.PressaoNecessaria, 1e-6);
        Assert.Equal(b.VazaoNecessaria, a.VazaoNecessaria, 1e-6);
    }

    [Fact]
    public void Demanda_deixa_o_critico_no_limite_e_os_demais_acima()
    {
        var rede = new Exemplos.GalpaoEspinhaDePeixe { Ramais = 6, ChuveirosPorLado = 6 }.Criar();
        var ids = rede.Chuveiros.Where(c => c.Ramal is "R4" or "R5").Select(c => c.Id).ToList();
        var r = new MotorHidraulico(rede).CalcularDemanda(ids.Select(id => Req(id, 85)).ToList());

        Assert.All(r.Chuveiros, c => Assert.True(c.Folga >= -1e-9));
        Assert.True(r.Chuveiros.Min(c => c.Folga) < 1e-6);
        Assert.Equal(r.VazaoChuveiros, r.VazaoNecessaria, 1e-6);
    }

    [Fact]
    public void Demanda_fixa_no_no_entra_na_vazao_da_fonte()
    {
        var rede = new Rede("S",
            [new No("S", 0, 0, 0), new No("J", 5, 0, 0) { DemandaFixa = 300 }, new No("B", 8, 0, 0)],
            [new Trecho("T1", "S", "J", D40, 5, 120), new Trecho("T2", "J", "B", D25, 3, 120)],
            [new Chuveiro("B1", "B", 80, 12)]);

        var r = new MotorHidraulico(rede).CalcularDemanda([Req("B1", 60)]);

        Assert.Equal(360, r.VazaoNecessaria, 0.01);
        var esperado = 56.25 + Hidraulica.PerdaUnitaria(60, 120, D25) * 3 + Hidraulica.PerdaUnitaria(360, 120, D40) * 5;
        Assert.Equal(esperado, r.PressaoNecessaria, 0.01);
    }

    [Fact]
    public void Rede_invalida_lista_os_erros()
    {
        var erro = Assert.Throws<ErroDeModelo>(() => new Rede("X",
            [new No("S", 0, 0, 0)],
            [new Trecho("T", "S", "Z", 0, 1, 120)],
            [new Chuveiro("B", "Q", 80, 12)]));
        Assert.True(erro.Erros.Count >= 4);
    }

    static double Bissecao(Func<double, double> f, double a, double b)
    {
        for (var i = 0; i < 200; i++)
        {
            var m = 0.5 * (a + b);
            if (f(a) * f(m) <= 0) b = m; else a = m;
        }
        return 0.5 * (a + b);
    }
}
