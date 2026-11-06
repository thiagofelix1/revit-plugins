using Eletrica.Motor.Calculo;
using Eletrica.Motor.Exemplos;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Testes;

/// <summary>Entrega 0.4: volumes do banheiro (9.1), IP, DR por tipo e trechos de eletroduto antes do cálculo.</summary>
public class VerificacaoBanheiroETrechosTestes
{
    static Instalacao Com(Func<Instalacao, IEnumerable<Ponto>> pontos, Func<Instalacao, IEnumerable<Trecho>>? trechos = null)
    {
        var i = new ApartamentoExemplo().Criar();
        return new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, pontos(i), trechos?.Invoke(i) ?? i.Trechos, i.Caixas, i.Comandos);
    }

    [Fact]
    public void IP_minimo_por_volume()
    {
        Assert.Equal(7, Nbr5410.IpAguaMinimoNoBanheiro(0));
        Assert.Equal(4, Nbr5410.IpAguaMinimoNoBanheiro(1));
        Assert.Equal(3, Nbr5410.IpAguaMinimoNoBanheiro(2));
        Assert.Equal(1, Nbr5410.IpAguaMinimoNoBanheiro(3));
        Assert.Equal(5, Nbr5410.IpAguaMinimoNoBanheiro(2, publico: true));
        Assert.Equal(0, Nbr5410.IpAguaMinimoNoBanheiro(4));
    }

    [Fact]
    public void Luminaria_no_volume_2_com_IP_baixo_e_erro_e_sem_IP_e_nota()
    {
        var baixo = Com(i => i.Pontos.Select(p => p.Id == "L-bwc" ? p with { VolumeDoBanheiro = 2, IpAgua = 1 } : p));
        var problemas = Verificacao.Verificar(baixo);
        Assert.Contains(problemas, p => p.Codigo == "B-04" && p.Elementos.Contains("L-bwc") && p.Mensagem.Contains("IPX3"));
        Assert.DoesNotContain(problemas, p => p.Codigo == "B-01" && p.Elementos.Contains("L-bwc")); // luminária é admitida no volume 2

        var semIp = Com(i => i.Pontos.Select(p => p.Id == "L-bwc" ? p with { VolumeDoBanheiro = 2 } : p));
        Assert.Contains(Verificacao.Verificar(semIp), p => p.Codigo == "B-05" && p.Elementos.Contains("L-bwc"));

        var ok = Com(i => i.Pontos.Select(p => p.Id == "L-bwc" ? p with { VolumeDoBanheiro = 2, IpAgua = 4 } : p));
        Assert.DoesNotContain(Verificacao.Verificar(ok), p => p.Codigo is "B-04" or "B-05" && p.Elementos.Contains("L-bwc"));
    }

    [Fact]
    public void Chuveiro_fora_do_volume_1_avisa_e_banheiro_sem_volumes_avisa()
    {
        var fora = Com(i => i.Pontos.Select(p => p.Id == "CH" ? p with { VolumeDoBanheiro = 3 } : p));
        Assert.Contains(Verificacao.Verificar(fora), p => p.Codigo == "B-06" && p.Elementos.Contains("CH"));

        var exemplo = Verificacao.Verificar(new ApartamentoExemplo().Criar());
        Assert.Contains(exemplo, p => p.Codigo == "A-08" && p.Elementos.Contains("bwc"));
        Assert.DoesNotContain(exemplo, p => p.Codigo == "A-09");

        var semVolumes = Com(i => i.Pontos.Select(p => p.Ambiente == "bwc" ? p with { VolumeDoBanheiro = null } : p));
        Assert.Contains(Verificacao.Verificar(semVolumes), p => p.Codigo == "A-09" && p.Elementos.Contains("bwc"));
    }

    [Fact]
    public void DR_tipo_F_para_ar_condicionado()
    {
        var problemas = Verificacao.Verificar(new ApartamentoExemplo().Criar());
        Assert.Contains(problemas, p => p.Codigo == "DR-02" && p.Elementos.Contains("C6") && p.Mensagem.Contains("tipo F"));
        Assert.DoesNotContain(problemas, p => p.Codigo == "DR-02" && p.Elementos.Contains("C1"));
    }

    [Fact]
    public void Trecho_longo_curvas_demais_e_DN_pequeno_sao_erros_antes_do_calculo()
    {
        var exemplo = Verificacao.Verificar(new ApartamentoExemplo().Criar());
        Assert.DoesNotContain(exemplo, p => p.Codigo is "T-01" or "T-02" or "T-03" or "T-04");

        var longo = Com(i => i.Pontos, i => i.Trechos.Select(t => t.Id == "QD-circA" ? t with { Comprimento = 14, Curvas90 = 1 } : t));
        Assert.Contains(Verificacao.Verificar(longo), p => p.Codigo == "T-01" && p.Elementos.Contains("QD-circA") && p.Mensagem.Contains("17,0 m"));

        var curvas = Com(i => i.Pontos, i => i.Trechos.Select(t => t.Id == "QD-circA" ? t with { Curvas90 = 4 } : t));
        Assert.Contains(Verificacao.Verificar(curvas), p => p.Codigo == "T-02" && p.Elementos.Contains("QD-circA"));

        var fino = Com(i => i.Pontos, i => i.Trechos.Select(t => t.Id == "QD-circA" ? t with { Dn = 15 } : t));
        Assert.Contains(Verificacao.Verificar(fino), p => p.Codigo == "T-04" && p.Elementos.Contains("QD-circA"));

        var externo = Com(i => i.Pontos, i => i.Trechos.Select(t => t.Id == "QD-circA" ? t with { Comprimento = 20, Externo = true } : t));
        Assert.DoesNotContain(Verificacao.Verificar(externo), p => p.Codigo == "T-01");
    }

    [Fact]
    public void Eletroduto_com_ponta_solta_avisa()
    {
        var solto = Com(i => i.Pontos, i => i.Trechos.Concat([new Trecho("solto", "cxA-circ", "ninguem", 2.0, ["C1"]) { Dn = 20 }]));
        var problemas = Verificacao.Verificar(solto);
        Assert.Contains(problemas, p => p.Codigo == "T-03" && p.Elementos.Contains("solto") && p.Mensagem.Contains("ninguem"));
        // a ponta no QD e nas caixas não é solta
        Assert.DoesNotContain(problemas, p => p.Codigo == "T-03" && p.Elementos.Contains("QD-circA"));
    }
}
