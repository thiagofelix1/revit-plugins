using Eletrica.Motor.Calculo;
using Eletrica.Motor.Exemplos;
using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Testes;

/// <summary>6.2.10.2: o que pode dividir o mesmo eletroduto.</summary>
public class CompartilhamentoTestes
{
    [Fact]
    public void Exemplo_nao_tem_problema_de_compartilhamento()
    {
        var r = CalculoEletrica.Executar(new ApartamentoExemplo().Criar());
        Assert.DoesNotContain(r.Problemas, p => p.Codigo is "E-ORIG" or "E-SEC3");
        var tronco = r.Trechos.Single(t => t.Id == "QD-circB");
        Assert.Contains(tronco.Pareceres, p => p.Criterio == "mesma origem" && p.Atende == true);
        Assert.Contains(tronco.Pareceres, p => p.Criterio == "seções próximas" && p.Atende == true);
    }

    [Fact]
    public void Alimentador_e_circuito_terminal_no_mesmo_eletroduto_e_erro()
    {
        var ex = new ApartamentoExemplo().Criar();
        var quadros = ex.Quadros.Concat([new Quadro("QT", "QT — Quadro terminal", 2) { QuadroPai = "QD", No = "cxA-circ" }]).ToList();
        var circuitos = ex.Circuitos.Concat([new Circuito("AL", "AL", "QD", TipoCarga.Alimentador, Ligacao.FFN) { QuadroAlimentado = "QT", Descricao = "Alimentador QT" }]).ToList();
        // C2 passa a sair do QT; o tronco QD-circA leva o alimentador do QD e o C2 do QT juntos.
        circuitos = circuitos.Select(c => c.Id == "C2" ? c with { Quadro = "QT" } : c).ToList();
        var trechos = ex.Trechos.Select(t => t.Id == "QD-circA" ? t with { Circuitos = [.. t.Circuitos, "AL"] } : t).ToList();
        var i = new Instalacao(ex.Nome, ex.Alimentacao, ex.Ambientes, quadros, circuitos, ex.Pontos, trechos);
        var r = CalculoEletrica.Executar(i);
        Assert.Contains(r.Problemas, p => p.Codigo == "E-ORIG" && p.Elementos.Contains("QD-circA"));
    }

    [Fact]
    public void Quadro_sem_alimentador_e_calculado_e_acusado()
    {
        // Como no Snowdon: um quadro filho sem o circuito alimentador modelado. O cálculo não pode parar; o M-05 avisa.
        var ex = new ApartamentoExemplo().Criar();
        var quadros = ex.Quadros.Concat([new Quadro("QT", "QT — Quadro terminal", 2) { QuadroPai = "QD", No = "cxA-circ" }]).ToList();
        var circuitos = ex.Circuitos.Select(c => c.Id == "C2" ? c with { Quadro = "QT" } : c).ToList();
        var i = new Instalacao(ex.Nome, ex.Alimentacao, ex.Ambientes, quadros, circuitos, ex.Pontos, ex.Trechos);
        var r = CalculoEletrica.Executar(i);
        Assert.Contains(r.Circuitos, c => c.Id == "C2");
        Assert.Equal(circuitos.Count, r.Circuitos.Count);
        Assert.Contains(r.Problemas, p => p.Codigo == "M-05");
    }

    [Fact]
    public void Secoes_fora_de_tres_valores_sucessivos_e_erro()
    {
        var ex = new ApartamentoExemplo().Criar();
        // Força 16 mm² na iluminação (C1), que divide o tronco QD-circB com circuitos de 2,5 e 4 mm².
        var circuitos = ex.Circuitos.Select(c => c.Id == "C1" ? c with { SecaoImposta = 16 } : c).ToList();
        var i = new Instalacao(ex.Nome, ex.Alimentacao, ex.Ambientes, ex.Quadros, circuitos, ex.Pontos, ex.Trechos);
        var r = CalculoEletrica.Executar(i);
        Assert.Contains(r.Problemas, p => p.Codigo == "E-SEC3" && p.Elementos.Contains("QD-circB"));
    }
}
