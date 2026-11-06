using System.IO;
using System.Text.Json;
using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Condutos;
using Eletrica.AddIn.Leitura;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Relatorios;

namespace Eletrica.AddIn.Servicos;

/// <summary>Lê o modelo, roda o motor, grava os resultados nos parâmetros AE e salva o memorial.</summary>
public static class ServicoCalculo
{
    public const string ChaveResultado = "UltimoResultado";

    public sealed record Saida(ResultadoInstalacao Resultado, LeitorInstalacao.Lida Lida, string Memorial, IReadOnlyList<string> Avisos);

    public static Saida Executar(Document doc, DadosProjeto dados)
    {
        var lida = LeitorInstalacao.Ler(doc, dados);
        var resultado = CalculoEletrica.Executar(lida.Instalacao);
        var avisos = new List<string>(lida.Avisos);

        Directory.CreateDirectory(PastasEletrica.Calculo);
        var nome = string.Concat(doc.Title.Split(Path.GetInvalidFileNameChars()));
        var memorial = Path.Combine(PastasEletrica.Calculo, $"{nome}-memorial.md");
        File.WriteAllText(memorial, Memorial.Gerar(resultado));
        File.WriteAllText(Path.Combine(PastasEletrica.Calculo, $"{nome}-resultado.json"),
            JsonSerializer.Serialize(resultado, new JsonSerializerOptions(ArmazenamentoEletrica.Json) { WriteIndented = true }));

        using (var transacao = new Transaction(doc, "AutoEletrica — Calcular"))
        {
            transacao.Start();
            EngolirAvisos.Aplicar(transacao);
            if (ParametrosEletrica.Garantir(doc) is { } aviso) avisos.Add(aviso);
            Gravar(doc, lida, resultado);
            ArmazenamentoEletrica.Gravar(doc, ChaveResultado, new ResumoGravado(DateTime.Now, memorial, resultado.Atende, resultado.Circuitos.Count));
            transacao.Commit();
        }
        return new Saida(resultado, lida, memorial, avisos);
    }

    public sealed record ResumoGravado(DateTime Quando, string Memorial, bool Atende, int Circuitos);

    static void Gravar(Document doc, LeitorInstalacao.Lida lida, ResultadoInstalacao r)
    {
        var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        foreach (var c in r.Circuitos)
        {
            if (!lida.Elementos.TryGetValue(c.Id, out var id) || doc.GetElement(id) is not { } el) continue;
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Secao, $"{c.SecaoFase.ToString("0.#", pt)} / {c.SecaoNeutro.ToString("0.#", pt)} / {c.SecaoPE.ToString("0.#", pt)}");
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Disjuntor, c.Disjuntor.ToString());
            ParametrosEletrica.Gravar(el, ParametrosEletrica.CorrenteDeProjeto, c.CorrenteDeProjeto);
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Queda, c.QuedaTotal);
            ParametrosEletrica.Gravar(el, ParametrosEletrica.DR, c.DR.Obrigatorio ? $"{c.DR.CorrenteResidualMA:0} mA tipo {c.DR.Tipo}" : "—");
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Situacao, c.Atende ? "atende" : "NÃO ATENDE: " + string.Join("; ", c.Reprovados.Select(p => p.Criterio)));
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Comprimento, c.Comprimento);
        }
        var resultados = r.Circuitos.ToDictionary(c => c.Id, c => c);
        var porId = lida.Instalacao.Circuitos.ToDictionary(c => c.Id, c => c);
        foreach (var t in r.Trechos)
        {
            var cadeia = lida.Grafo.Cadeias.FirstOrDefault(k => k.Id == t.Id);
            if (cadeia is null) continue;
            var trecho = lida.Instalacao.Trechos.First(x => x.Id == t.Id);
            var circuitos = trecho.Circuitos.Select(id => porId[id]).OrderBy(c => c.Numero, StringComparer.OrdinalIgnoreCase).ToList();
            var fiacao = PassarCircuitos.Etiqueta(circuitos, resultados, trecho.Retornos.Count);
            foreach (Element el in cadeia.Conduites.Cast<Element>().Concat(cadeia.Conexoes))
            {
                ParametrosEletrica.Gravar(el, ParametrosEletrica.Fiacao, fiacao);
                if (t.DnSugerido is { } dn) ParametrosEletrica.Gravar(el, ParametrosEletrica.DnSugerido, dn);
                if (t.TaxaDeOcupacao is { } taxa) ParametrosEletrica.Gravar(el, ParametrosEletrica.Ocupacao, taxa * 100);
            }
        }
        foreach (var p in lida.Instalacao.Pontos)
        {
            if (!lida.Elementos.TryGetValue(p.Id, out var id) || doc.GetElement(id) is not { } el) continue;
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Ambiente, lida.Instalacao.Ambiente(p.Ambiente)?.Nome ?? "");
        }
    }
}
