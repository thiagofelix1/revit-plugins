using Spk.Motor.Modelo;

namespace Spk.Motor.Testes;

public class ContornoTestes
{
    static List<(double U, double V)> Grade(int nu, int nv, double passo = 3, double u0 = 0, double v0 = 0) =>
        Enumerable.Range(0, nu).SelectMany(i => Enumerable.Range(0, nv).Select(j => (u0 + i * passo, v0 + j * passo))).ToList();

    [Fact]
    public void Grade_regular_vira_um_retangulo_com_meio_passo_de_folga()
    {
        var lacos = Contorno.DosBicos(Grade(4, 3));

        var laco = Assert.Single(lacos);
        Assert.Equal(4, laco.Count);
        Assert.Equal(12 * 9, Contorno.Area(laco), 6);
        Assert.Equal(-1.5, laco.Min(p => p.U), 6);
        Assert.Equal(10.5, laco.Max(p => p.U), 6);
        Assert.Equal(7.5, laco.Max(p => p.V), 6);
    }

    [Fact]
    public void Setores_afastados_ficam_em_lacos_separados()
    {
        var bicos = Grade(3, 3).Concat(Grade(3, 3, u0: 30)).ToList();

        var lacos = Contorno.DosBicos(bicos);

        Assert.Equal(2, lacos.Count);
        Assert.All(lacos, l => Assert.Equal(81, Contorno.Area(l), 6));
    }

    [Fact]
    public void Formato_em_L_tem_seis_vertices()
    {
        var bicos = Grade(4, 2).Concat(Grade(2, 2, v0: 6)).ToList();

        var laco = Assert.Single(Contorno.DosBicos(bicos));

        Assert.Equal(6, laco.Count);
        Assert.Equal(12 * 6 + 6 * 6, Contorno.Area(laco), 6);
    }

    [Fact]
    public void Vao_pequeno_entre_ramais_mais_espacados_e_fechado()
    {
        // Ramais em v = 0, 3, 6 e o último 0,6 m mais longe: a mediana fica em 3 m e sobra um vão de 0,6 m.
        var bicos = Grade(4, 3).Concat(Enumerable.Range(0, 4).Select(i => (i * 3.0, 9.6))).ToList();

        var laco = Assert.Single(Contorno.DosBicos(bicos));

        Assert.Equal(4, laco.Count);
    }

    [Fact]
    public void Anel_de_bicos_tem_furo_em_sentido_horario()
    {
        var bicos = Grade(7, 7, passo: 3).Where(b => b.Item1 is < 4 or > 14 || b.Item2 is < 4 or > 14).ToList();

        var lacos = Contorno.DosBicos(bicos, vaoMaximo: 0.5);

        Assert.Equal(2, lacos.Count);
        Assert.Single(lacos, l => Contorno.Area(l) > 0);
        Assert.Single(lacos, l => Contorno.Area(l) < 0);
    }

    [Fact]
    public void Passo_e_a_mediana_entre_posicoes_agrupadas()
    {
        Assert.Equal(3, Contorno.Passo([0, 0.02, 3, 3.05, 6, 9.6]), 1);
        Assert.Equal(3, Contorno.Passo([5, 5, 5]), 6);
    }

    [Fact]
    public void Bicos_fora_de_prumo_ainda_formam_um_contorno_so()
    {
        var desvio = new[] { 0.0, 0.2, -0.15, 0.1, -0.2 };
        var bicos = Grade(5, 3).Select((b, k) => (b.U + desvio[k % 5], b.V + desvio[(k + 2) % 5])).ToList();

        Assert.Single(Contorno.DosBicos(bicos));
    }
}
