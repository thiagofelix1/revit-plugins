using Spk.Motor.Exemplos;
using Spk.Motor.Modelo;

namespace Spk.Motor.Testes;

public class TopologiaTestes
{
    static Dictionary<string, No> Indice(IEnumerable<No> nos) => nos.ToDictionary(n => n.Id, StringComparer.Ordinal);

    static Trecho T(string de, string para) => new($"{de}-{para}", de, para, 50, 1, 120);

    [Fact]
    public void Ramais_da_espinha_de_peixe_batem_com_os_do_gerador()
    {
        var rede = new GalpaoEspinhaDePeixe { Ramais = 6, ChuveirosPorLado = 4 }.Criar();
        var nos = Indice(rede.Nos);
        var (dx, dy) = Topologia.DirecaoDosRamais(rede.Trechos, nos);

        var ramal = Topologia.RamalDeCadaBico(rede.Chuveiros.Select(c => c.No), rede.Trechos, nos, dx, dy);

        Assert.Equal(6, ramal.Values.Distinct().Count());
        foreach (var grupo in rede.Chuveiros.GroupBy(c => c.Ramal))
            Assert.Single(grupo.Select(c => ramal[c.No]).Distinct());
    }

    [Fact]
    public void Ramais_na_mesma_linha_de_setores_diferentes_nao_se_juntam()
    {
        // Dois ramais em y = 0, cada um ligado à sua fonte e sem tubo entre eles.
        var nos = Indice([
            new No("FA", 0, 0, 0), new No("A1", 2, 0, 0), new No("A2", 5, 0, 0),
            new No("FB", 20, 0, 0), new No("B1", 22, 0, 0), new No("B2", 25, 0, 0),
        ]);
        Trecho[] trechos = [T("FA", "A1"), T("A1", "A2"), T("FB", "B1"), T("B1", "B2")];

        var ramal = Topologia.RamalDeCadaBico(["A1", "A2", "B1", "B2"], trechos, nos, 1, 0);

        Assert.Equal(ramal["A1"], ramal["A2"]);
        Assert.Equal(ramal["B1"], ramal["B2"]);
        Assert.NotEqual(ramal["A1"], ramal["B1"]);
    }

    [Fact]
    public void Subgeral_separa_os_ramais_e_descida_curta_junta_o_bico()
    {
        // Subgeral em x = 0 (eixo Y); ramais em y = 0 e y = 3 (eixo X); o bico R0b fica numa descida de 0,2 m.
        var nos = Indice([
            new No("SG0", 0, 0, 3), new No("SG1", 0, 3, 3),
            new No("R0a", 3, 0, 3), new No("T0", 6, 0, 3), new No("R0b", 6.1, 0, 2.7),
            new No("R1a", 3, 3, 3), new No("R1b", 6, 3, 3),
        ]);
        Trecho[] trechos = [T("SG0", "SG1"), T("SG0", "R0a"), T("R0a", "T0"), T("T0", "R0b"), T("SG1", "R1a"), T("R1a", "R1b")];

        var ramal = Topologia.RamalDeCadaBico(["R0a", "R0b", "R1a", "R1b"], trechos, nos, 1, 0);

        Assert.Equal(ramal["R0a"], ramal["R0b"]);
        Assert.Equal(ramal["R1a"], ramal["R1b"]);
        Assert.NotEqual(ramal["R0a"], ramal["R1a"]);
    }

    [Fact]
    public void Os_dois_lados_da_subgeral_na_mesma_linha_sao_um_ramal()
    {
        var nos = Indice([
            new No("SG0", 0, 0, 3), new No("SG1", 0, 3, 3),
            new No("E1", -3, 0, 3), new No("D1", 3, 0, 3),
        ]);
        Trecho[] trechos = [T("SG1", "SG0"), T("SG0", "E1"), T("SG0", "D1")];

        var ramal = Topologia.RamalDeCadaBico(["E1", "D1"], trechos, nos, 1, 0);

        Assert.Equal(ramal["E1"], ramal["D1"]);
    }

    [Fact]
    public void Setor_da_vga_fica_do_lado_dos_bicos_e_nao_atravessa_outra_vga()
    {
        // Barrilete B liga V1 e V2. V1 alimenta S1 e S2; V2 alimenta S3.
        Trecho[] trechos = [T("B", "V1"), T("V1", "a"), T("a", "S1"), T("a", "S2"), T("B", "V2"), T("V2", "c"), T("c", "S3")];
        var bicos = new HashSet<string> { "S1", "S2", "S3" };
        var vgas = new HashSet<string> { "V1", "V2" };

        var setor = Topologia.SetorDaVga("V1", trechos, bicos, vgas);

        Assert.Contains("S1", setor);
        Assert.Contains("S2", setor);
        Assert.Contains("V1", setor);
        Assert.DoesNotContain("S3", setor);
        Assert.DoesNotContain("B", setor);
    }

    [Fact]
    public void Numeracao_comeca_na_fonte_e_cresce_com_a_distancia()
    {
        Trecho[] trechos = [T("V", "a"), T("a", "b"), T("a", "c"), T("c", "d")];

        var numero = Topologia.Numerar("V", trechos);

        Assert.Equal(1, numero["V"]);
        Assert.Equal(2, numero["a"]);
        Assert.Equal([3, 4], new[] { numero["b"], numero["c"] }.Order());
        Assert.Equal(5, numero["d"]);
    }

    [Fact]
    public void Sem_vga_a_fonte_e_a_ponta_mais_baixa_do_grupo_com_mais_bicos()
    {
        var nos = Indice([
            new No("TOPO", 0, 0, 9), new No("BASE", 0, 0, 0), new No("S1", 3, 0, 9), new No("S2", 6, 0, 9),
            new No("SOLTO", 50, 0, 9), new No("S3", 53, 0, 9),
        ]);
        Trecho[] trechos = [T("BASE", "TOPO"), T("TOPO", "S1"), T("S1", "S2"), T("SOLTO", "S3")];

        var (fonte, grupo) = Topologia.MaiorComponente(nos, trechos, new HashSet<string> { "S1", "S2", "S3" });

        Assert.Equal("BASE", fonte);
        Assert.Contains("S2", grupo);
        Assert.DoesNotContain("S3", grupo);
    }

    [Fact]
    public void Subgeral_perpendicular_nao_entorta_a_direcao_dos_ramais()
    {
        // 3 ramais em X de 9 m e uma subgeral em Y de 6 m: a direção é X, sem inclinação.
        var segmentos = Enumerable.Range(0, 9).Select(_ => (3.0, 0.0)).Concat([(0.0, 3.0), (0.0, -3.0)]);

        var (dx, dy) = Topologia.EixoDominante(segmentos);

        Assert.Equal(1, dx, 9);
        Assert.Equal(0, dy, 9);
    }

    [Fact]
    public void Malha_girada_da_a_direcao_girada_com_sentido_positivo()
    {
        var a = Math.PI / 6;
        var ramal = (-3 * Math.Cos(a), -3 * Math.Sin(a));
        var subgeral = (-3 * Math.Sin(a), 3 * Math.Cos(a));
        var segmentos = Enumerable.Repeat(ramal, 8).Concat(Enumerable.Repeat(subgeral, 3));

        var (dx, dy) = Topologia.EixoDominante(segmentos);

        Assert.Equal(Math.Cos(a), dx, 9);
        Assert.Equal(Math.Sin(a), dy, 9);
    }
}
