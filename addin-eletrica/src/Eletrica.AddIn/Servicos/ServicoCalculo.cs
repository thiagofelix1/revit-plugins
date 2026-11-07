using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Condutos;
using Eletrica.AddIn.Leitura;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Relatorios;

namespace Eletrica.AddIn.Servicos;

/// <summary>
///     Lê o modelo, roda o motor, grava os resultados nos parâmetros AE, alinha o caminho dos circuitos do Revit ao traçado
///     e salva o memorial. Guarda a assinatura da instalação lida para o Guia dizer quando o cálculo ficou desatualizado
///     (item 7.16 do plano, sem IUpdater: a conferência é ao abrir o Guia ou o Memorial).
/// </summary>
public static class ServicoCalculo
{
    public const string ChaveResultado = "UltimoResultado";

    public sealed record Saida(ResultadoInstalacao Resultado, LeitorInstalacao.Lida Lida, string Memorial, IReadOnlyList<string> Avisos);

    public sealed record ResumoGravado(DateTime Quando, string Memorial, bool Atende, int Circuitos, string? Assinatura = null, string? Manual = null, string? MemorialMd = null);

    public static Saida Executar(Document doc, DadosProjeto dados)
    {
        var lida = LeitorInstalacao.Ler(doc, dados);
        var resultado = CalculoEletrica.Executar(lida.Instalacao);
        var avisos = new List<string>(lida.Avisos);

        Directory.CreateDirectory(PastasEletrica.Calculo);
        var nome = string.Concat(doc.Title.Split(Path.GetInvalidFileNameChars()));
        var memorialMd = Path.Combine(PastasEletrica.Calculo, $"{nome}-memorial.md");
        var memorial = Path.Combine(PastasEletrica.Calculo, $"{nome}-memorial.docx");
        var manual = Path.Combine(PastasEletrica.Calculo, $"{nome}-manual-do-usuario.docx");
        File.WriteAllText(memorialMd, Memorial.Gerar(resultado));
        File.WriteAllBytes(memorial, MemorialDocx.Gerar(resultado));
        File.WriteAllBytes(manual, ManualDoUsuario.GerarDocx(resultado));
        File.WriteAllText(Path.Combine(PastasEletrica.Calculo, $"{nome}-manual-do-usuario.md"), ManualDoUsuario.GerarMarkdown(resultado));
        var lista = ListaDeMaterial.Gerar(lida.Instalacao, resultado);
        File.WriteAllText(Path.Combine(PastasEletrica.Calculo, $"{nome}-lista-de-material.md"), ListaDeMaterial.Markdown(lista, resultado.Nome));
        File.WriteAllText(Path.Combine(PastasEletrica.Calculo, $"{nome}-lista-de-material.csv"), ListaDeMaterial.Csv(lista));
        File.WriteAllText(Path.Combine(PastasEletrica.Calculo, $"{nome}-resultado.json"),
            JsonSerializer.Serialize(resultado, new JsonSerializerOptions(ArmazenamentoEletrica.Json) { WriteIndented = true }));

        using (var transacao = new Transaction(doc, "AutoEletrica — Calcular"))
        {
            transacao.Start();
            EngolirAvisos.Aplicar(transacao);
            if (ParametrosEletrica.Garantir(doc) is { } aviso) avisos.Add(aviso);
            Gravar(doc, lida, resultado);
            var alinhados = AlinharCaminhos(doc, lida, avisos);
            if (alinhados > 0) avisos.Add($"{alinhados} circuito(s) com o caminho do Revit alinhado ao traçado dos eletrodutos (comprimento do Revit = comprimento real).");
            ArmazenamentoEletrica.Gravar(doc, ChaveResultado, new ResumoGravado(DateTime.Now, memorial, resultado.Atende, resultado.Circuitos.Count, Assinatura(lida.Instalacao), manual, memorialMd));
            transacao.Commit();
        }
        return new Saida(resultado, lida, memorial, avisos);
    }

    /// <summary>
    ///     Assinatura da instalação lida (SHA-256 do JSON): muda quando qualquer dado que entra no cálculo muda. Os números
    ///     entram arredondados a milésimos: o Revit devolve comprimentos e cargas com ruído de ponto flutuante entre duas
    ///     leituras do mesmo modelo, e isso não pode virar "cálculo desatualizado".
    /// </summary>
    public static string Assinatura(Instalacao i)
    {
        var json = JsonParaAssinatura(i, indentado: false);
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);
    }

    /// <summary>O JSON que entra na assinatura (o teste grava este texto para comparar duas leituras).</summary>
    public static string JsonParaAssinatura(Instalacao i, bool indentado)
    {
        var opcoes = new JsonSerializerOptions(ArmazenamentoEletrica.Json) { WriteIndented = indentado };
        opcoes.Converters.Add(new DoubleArredondado());
        return JsonSerializer.Serialize(new { i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, i.Pontos, i.Trechos, i.Comandos, i.Caixas }, opcoes);
    }

    sealed class DoubleArredondado : System.Text.Json.Serialization.JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDouble();

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else writer.WriteNumberValue(Math.Round(value, 3));
        }
    }

    /// <summary>O último cálculo ainda vale para o modelo como está? Nulo quando não há cálculo.</summary>
    public static bool? CalculoAtual(Document doc, DadosProjeto? dados)
    {
        var resumo = ArmazenamentoEletrica.Ler<ResumoGravado>(doc, ChaveResultado);
        if (resumo?.Assinatura is null || dados is null) return null;
        try { return Assinatura(LeitorInstalacao.Ler(doc, dados).Instalacao) == resumo.Assinatura; }
        catch (ErroDeModelo) { return false; }
    }

    static void Gravar(Document doc, LeitorInstalacao.Lida lida, ResultadoInstalacao r)
    {
        var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        foreach (var c in r.Circuitos)
        {
            if (!lida.Elementos.TryGetValue(c.Id, out var id) || doc.GetElement(id) is not { } el) continue;
            var fase = c.CondutoresPorFase > 1 ? $"{c.CondutoresPorFase}×" : "";
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Secao, $"{fase}{c.SecaoFase.ToString("0.#", pt)} / {c.SecaoNeutro.ToString("0.#", pt)} / {c.SecaoPE.ToString("0.#", pt)}");
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Disjuntor, c.Disjuntor.ToString());
            ParametrosEletrica.Gravar(el, ParametrosEletrica.CorrenteDeProjeto, c.CorrenteDeProjeto);
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Queda, c.QuedaTotal);
            ParametrosEletrica.Gravar(el, ParametrosEletrica.DR, c.DR.Obrigatorio ? $"{c.DR.CorrenteResidualMA:0} mA tipo {c.DR.Tipo}" : "—");
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Situacao, c.Atende ? "atende" : "NÃO ATENDE: " + string.Join("; ", c.Reprovados.Select(p => p.Criterio)));
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Comprimento, c.Comprimento);
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Fase, c.Fases);
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
                else if (t.RepeticoesSugeridas is { } n) ParametrosEletrica.Gravar(el, ParametrosEletrica.DnSugerido, -n); // negativo: repetir n eletrodutos do maior DN
                if (t.TaxaDeOcupacao is { } taxa) ParametrosEletrica.Gravar(el, ParametrosEletrica.Ocupacao, taxa * 100);
            }
        }
        foreach (var p in lida.Instalacao.Pontos)
        {
            if (!lida.Elementos.TryGetValue(p.Id, out var id) || doc.GetElement(id) is not { } el) continue;
            ParametrosEletrica.Gravar(el, ParametrosEletrica.Ambiente, lida.Instalacao.Ambiente(p.Ambiente)?.Nome ?? "");
        }
    }

    /// <summary>
    ///     Caminho do circuito no Revit (item 4.10 do plano): os vértices do traçado pelos eletrodutos até o ponto mais
    ///     distante, para o comprimento do Revit bater com o real. Precisa de transação aberta; o Revit recusa caminhos que
    ///     não começam no quadro ou têm vértices inválidos, e aí o circuito fica como estava.
    /// </summary>
    static int AlinharCaminhos(Document doc, LeitorInstalacao.Lida lida, List<string> avisos)
    {
        var grafo = new Grafo(lida.Instalacao);
        var cadeias = lida.Grafo.Cadeias.ToDictionary(k => k.Id, k => k);
        var alinhados = 0;
        foreach (var c in lida.Instalacao.Circuitos)
        {
            if (grafo.ComprimentoDe(c) is not { } caminho || caminho.Caminho.Count == 0) continue;
            if (!lida.Elementos.TryGetValue(c.Id, out var id) || doc.GetElement(id) is not ElectricalSystem sistema) continue;
            var vertices = new List<XYZ>();
            foreach (var t in caminho.Caminho)
            {
                if (!cadeias.TryGetValue(t.Id, out var cadeia)) continue;
                var percurso = lida.Grafo.Percurso(cadeia);
                if (vertices.Count > 0 && percurso.Count > 1 && percurso[0].DistanceTo(vertices[vertices.Count - 1]) > percurso[percurso.Count - 1].DistanceTo(vertices[vertices.Count - 1]))
                    percurso.Reverse();
                foreach (var p in percurso)
                    if (vertices.Count == 0 || vertices[vertices.Count - 1].DistanceTo(p) > Unidades.DeMetros(0.01)) vertices.Add(p);
            }
            if (vertices.Count < 2) continue;
            try
            {
                if (!sistema.IsCircuitPathValid(vertices)) continue;
                sistema.CircuitPathMode = ElectricalCircuitPathMode.Custom;
                sistema.SetCircuitPath(vertices);
                alinhados++;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException e)
            {
                avisos.Add($"Circuito {c.Numero}: caminho do Revit não alinhado ({e.Message}).");
            }
        }
        return alinhados;
    }
}
