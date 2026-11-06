using System.IO.Compression;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Exemplos;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Relatorios;

namespace Eletrica.Motor.Testes;

/// <summary>Entrega 0.6: memorial .docx, manual do usuário, lista de material e unifilar.</summary>
public class EntregaveisTestes
{
    static (Instalacao I, ResultadoInstalacao R) Exemplo()
    {
        var i = new ApartamentoExemplo().Criar();
        return (i, CalculoEletrica.Executar(i));
    }

    static string TextoDoDocx(byte[] docx)
    {
        using var ms = new MemoryStream(docx);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void Memorial_docx_abre_e_tem_as_secoes()
    {
        var (_, r) = Exemplo();
        var docx = MemorialDocx.Gerar(r);
        Assert.True(docx.Length > 5000);
        var xml = TextoDoDocx(docx);
        Assert.Contains("Memorial de cálculo elétrico", xml);
        Assert.Contains("Quadro de cargas", xml);
        Assert.Contains("seis critérios", xml);
        Assert.Contains("Premissas", xml);
        Assert.Contains("Chuveiro elétrico", xml);
    }

    [Fact]
    public void Manual_do_usuario_fala_de_DR_advertencia_e_cada_circuito()
    {
        var (_, r) = Exemplo();
        var md = ManualDoUsuario.GerarMarkdown(r);
        Assert.Contains("Teste o DR uma vez por mês", md);
        Assert.Contains("NUNCA troque seus disjuntores", md);
        Assert.Contains("6.1.8.3", md);
        foreach (var c in r.Circuitos) Assert.Contains($"| {c.Numero} | {c.Descricao} |", md);
        var xml = TextoDoDocx(ManualDoUsuario.GerarDocx(r));
        Assert.Contains("Manual do usuário", xml);
        Assert.Contains("DPS", xml);
    }

    [Fact]
    public void Lista_de_material_soma_condutores_pelos_trechos()
    {
        var (i, r) = Exemplo();
        var itens = ListaDeMaterial.Gerar(i, r);
        Assert.Contains(itens, x => x.Grupo == "Condutores" && x.Descricao.Contains("fase"));
        Assert.Contains(itens, x => x.Grupo == "Condutores" && x.Descricao.Contains("proteção"));
        Assert.Contains(itens, x => x.Grupo == "Eletrodutos" && x.Descricao.Contains("DN 25"));
        Assert.Contains(itens, x => x.Grupo == "Proteções" && x.Descricao.StartsWith("Disjuntor B25"));
        Assert.Contains(itens, x => x.Grupo == "Proteções" && x.Descricao.StartsWith("DR "));
        Assert.Contains(itens, x => x.Grupo == "Proteções" && x.Descricao.StartsWith("DPS"));
        Assert.Contains(itens, x => x.Grupo == "Quadros");
        Assert.Equal(i.Pontos.Count(p => p.Tipo == TipoCarga.TomadaUsoGeral), itens.Where(x => x.Grupo == "Pontos" && x.Descricao.Contains("Tomada de uso geral")).Sum(x => x.Quantidade));

        // o chuveiro (C5) passa só por QD-bwc (6 m), 2 fases de 4 mm² + PE 4: fase 12 m + PE 6 m, com 10 % de folga, arredondado
        var fase4 = itens.Single(x => x.Grupo == "Condutores" && x.Descricao.Contains("4,0 mm²") && x.Descricao.Contains("fase"));
        Assert.True(fase4.Quantidade >= Math.Ceiling(12 * 1.1));
        var md = ListaDeMaterial.Markdown(itens, i.Nome);
        Assert.Contains("| Condutores |", md);
        var csv = ListaDeMaterial.Csv(itens);
        Assert.StartsWith("Grupo;Item;Unidade;Quantidade;Origem", csv);
        Assert.Equal(itens.Count + 1, csv.Trim().Split('\n').Length);
    }

    [Fact]
    public void Unifilar_tem_entrada_geral_dps_drs_e_todos_os_circuitos()
    {
        var (i, r) = Exemplo();
        var diagramas = Unifilar.Montar(i, r);
        var d = Assert.Single(diagramas);
        Assert.StartsWith("Ponto de entrega", d.Entrada);
        Assert.NotEqual("—", d.Geral);
        Assert.NotNull(d.Dps);
        Assert.NotEmpty(d.Drs);
        var todos = d.Drs.SelectMany(b => b.Ramos).Concat(d.SemDR).Select(x => x.Numero).OrderBy(n => n).ToList();
        Assert.Equal(r.Circuitos.Select(c => c.Numero).OrderBy(n => n), todos);
        Assert.Contains("DR1", Unifilar.Texto(diagramas));

        // com um quadro filho, o diagrama aninha
        var quadros = i.Quadros.Concat([new Quadro("QT", "QT — Terminal", 2) { QuadroPai = "QD", No = "cxA-circ" }]).ToList();
        var circuitos = i.Circuitos.Select(c => c.Id == "C6" ? c with { Quadro = "QT" } : c)
            .Concat([new Circuito("AL", "AL", "QD", TipoCarga.Alimentador, Ligacao.FFN) { QuadroAlimentado = "QT", Descricao = "Alimentador QT", ComprimentoManual = 10 }]).ToList();
        var trechos = i.Trechos.Select(t => t.Id == "QD-circA" ? t with { Circuitos = [.. t.Circuitos, "AL"] } : t);
        var dois = new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, quadros, circuitos, i.Pontos, trechos);
        var dd = Assert.Single(Unifilar.Montar(dois, CalculoEletrica.Executar(dois)));
        var filho = Assert.Single(dd.Filhos);
        Assert.StartsWith("Alimentador AL do QD", filho.Entrada);
        Assert.Contains(dd.SemDR.Concat(dd.Drs.SelectMany(b => b.Ramos)), x => x.QuadroAlimentado == "QT — Terminal");
    }
}
