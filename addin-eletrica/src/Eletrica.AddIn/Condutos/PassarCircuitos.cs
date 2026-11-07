using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Leitura;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Condutos;

/// <summary>
/// Leva cada circuito do quadro até cada ponto pelo menor caminho no grafo de eletrodutos e grava em cada eletroduto da
/// cadeia os circuitos que passam (AE Circuitos), quantos condutores (AE Condutores), os retornos dos comandos de iluminação
/// (AE Retornos) e a etiqueta de fiação (AE Fiação). É o dado mestre da fiação: o fio desenhado do Revit é 2D e por vista;
/// o trecho de eletroduto com a lista de circuitos é o que o cálculo e as etiquetas usam.
/// </summary>
public static class PassarCircuitos
{
    public sealed record Feito(int Circuitos, int Eletrodutos, IReadOnlyList<string> Avisos);

    public static Feito Executar(Document doc, LeitorInstalacao.Lida lida, IReadOnlyDictionary<string, ResultadoCircuito>? resultados = null)
    {
        var i = lida.Instalacao;
        var avisos = new List<string>();

        // Grafo só com a topologia: todos os trechos valem para todos os circuitos, menos os que a fiação manual manda evitar.
        var todosOsIds = i.Circuitos.Select(c => c.Id).ToList();
        var forcados = new Dictionary<string, List<string>>();
        var todos = new List<Trecho>();
        foreach (var t in i.Trechos)
        {
            var cadeia = lida.Grafo.Cadeias.FirstOrDefault(k => k.Id == t.Id);
            var evitar = Lista(cadeia, ParametrosEletrica.Evitar);
            var forcar = Lista(cadeia, ParametrosEletrica.Forcar).Where(todosOsIds.Contains).ToList();
            if (forcar.Count > 0) forcados[t.Id] = forcar;
            var permitidos = evitar.Contains("*") ? forcar : todosOsIds.Where(id => !evitar.Contains(id) || forcar.Contains(id)).ToList();
            todos.Add(t with { Circuitos = permitidos });
        }
        var topologia = new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, i.Pontos, todos, comandos: i.Comandos);
        // O caminho, os paralelos, os retornos e os avisos são do motor (Passagem), testados sem o Revit.
        var passagem = Passagem.Executar(topologia, forcados.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value));
        avisos.AddRange(passagem.Avisos);
        var circuitosLevados = passagem.CircuitosLevados;
        var porCadeia = passagem.CircuitosPorTrecho;
        var retornosPorCadeia = passagem.RetornosPorTrecho;

        using var transacao = new Transaction(doc, "AutoEletrica — Passar circuitos");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);
        var aviso = ParametrosEletrica.Garantir(doc);
        if (aviso is not null) avisos.Add(aviso);
        var eletrodutos = 0;
        foreach (var cadeia in lida.Grafo.Cadeias)
        {
            var ids = porCadeia.TryGetValue(cadeia.Id, out var set) ? set.ToList() : [];
            var circuitos = ids.Select(id => i.Circuito(id)!).OrderBy(c => c.Numero, StringComparer.OrdinalIgnoreCase).ToList();
            var retornos = retornosPorCadeia.TryGetValue(cadeia.Id, out var r) ? r : (IReadOnlyList<string>)[];
            var condutores = circuitos.Sum(c => c.Fases + (c.TemNeutro ? 1 : 0) + 1) + retornos.Count;
            var fiacao = Etiqueta(circuitos, resultados, retornos.Count);
            foreach (var el in cadeia.Conduites.Cast<Element>().Concat(cadeia.Conexoes))
            {
                ParametrosEletrica.Gravar(el, ParametrosEletrica.Circuitos, string.Join(";", ids));
                ParametrosEletrica.Gravar(el, ParametrosEletrica.Condutores, condutores);
                ParametrosEletrica.Gravar(el, ParametrosEletrica.Fiacao, fiacao);
                ParametrosEletrica.Gravar(el, ParametrosEletrica.Retornos, string.Join(";", retornos));
                if (el is Conduit) eletrodutos++;
            }
        }
        transacao.Commit();
        return new Feito(circuitosLevados, eletrodutos, avisos);
    }

    /// <summary>Ids gravados num parâmetro AE de texto em qualquer eletroduto da cadeia ("*" = todos).</summary>
    static List<string> Lista(GrafoDeCondutos.Cadeia? cadeia, Guid parametro)
    {
        if (cadeia is null) return [];
        var texto = cadeia.Conduites.Select(c => ParametrosEletrica.LerTexto(c, parametro)).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        return (texto ?? "").Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// Etiqueta de fiação no formato "3#2,5(2,5) C1 C3": condutores vivos # seção (PE) e os circuitos, agrupados por seção;
    /// retornos como "+2R". Antes do cálculo, só a contagem: "3 cond. C1 C3".
    /// </summary>
    public static string Etiqueta(IReadOnlyList<Circuito> circuitos, IReadOnlyDictionary<string, ResultadoCircuito>? resultados, int retornos = 0)
    {
        if (circuitos.Count == 0) return "";
        var sufixo = retornos > 0 ? $" +{retornos}R" : "";
        if (resultados is null || circuitos.Any(c => !resultados.ContainsKey(c.Id)))
            return $"{circuitos.Sum(c => c.Fases + (c.TemNeutro ? 1 : 0) + 1) + retornos} cond. " + string.Join(" ", circuitos.Select(c => "C" + c.Numero)) + sufixo;
        var grupos = circuitos.GroupBy(c => (resultados[c.Id].SecaoFase, resultados[c.Id].SecaoPE))
            .OrderByDescending(g => g.Key.SecaoFase)
            .Select(g =>
            {
                var vivos = g.Sum(c => c.Fases + (c.TemNeutro ? 1 : 0));
                return $"{vivos}#{F(g.Key.SecaoFase)}({F(g.Key.SecaoPE)}) " + string.Join(" ", g.Select(c => "C" + c.Numero));
            });
        return string.Join(" + ", grupos) + sufixo;
    }

    static string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
