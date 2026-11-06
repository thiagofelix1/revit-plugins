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
        var porCadeia = new Dictionary<string, HashSet<string>>();
        var retornosPorCadeia = new Dictionary<string, List<string>>();

        // Grafo só com a topologia (todos os trechos valem para todos os circuitos nesta fase).
        var todos = i.Trechos.Select(t => t with { Circuitos = i.Circuitos.Select(c => c.Id).ToList() }).ToList();
        var topologia = new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, i.Pontos, todos, comandos: i.Comandos);
        var grafo = new Grafo(topologia);

        var circuitosLevados = 0;
        foreach (var c in i.Circuitos)
        {
            var comandos = i.Comandos.Where(k => k.Circuito == c.Id).ToList();
            var destinos = c.QuadroAlimentado is { } qa
                ? [i.Quadro(qa)!.NoDoQuadro]
                : i.PontosDo(c).Select(p => p.NoDoPonto).Distinct().ToList();
            // A fase do circuito de iluminação vai até o primeiro interruptor de cada comando (5.6.6.1.2).
            foreach (var k in comandos) destinos.Add(k.NoDoInterruptor(0));
            destinos = destinos.Distinct().ToList();
            if (destinos.Count == 0) continue;
            var caminhos = grafo.CaminhosDe(c, destinos);
            var levou = false;
            foreach (var destino in destinos)
            {
                if (!caminhos.TryGetValue(destino, out var caminho))
                {
                    avisos.Add($"Circuito {c.Numero}: o ponto em {destino} não tem caminho pelos eletrodutos até o quadro {i.QuadroDe(c).Nome}.");
                    continue;
                }
                levou = true;
                foreach (var t in caminho)
                {
                    if (!porCadeia.TryGetValue(t.Id, out var set)) porCadeia[t.Id] = set = [];
                    set.Add(c.Id);
                }
            }
            if (levou) circuitosLevados++;

            // Retornos: entre interruptores em série (2 por trecho no paralelo e no intermediário) e do último interruptor a cada luminária (1).
            foreach (var k in comandos)
            {
                var entre = Comando.RetornosEntreInterruptores(k.Tipo);
                for (var n = 0; n + 1 < k.Interruptores.Count; n++)
                    Retorno(grafo, k.NoDoInterruptor(n), k.NoDoInterruptor(n + 1), c, entre, $"comando {k.Id}, interruptores {n + 1} e {n + 2}", porCadeia, retornosPorCadeia, avisos);
                var ultimo = k.NoDoInterruptor(k.Interruptores.Count - 1);
                foreach (var no in k.Luminarias.Select(l => i.Ponto(l)?.NoDoPonto).Where(n => n is not null).Distinct())
                    Retorno(grafo, ultimo, no!, c, Comando.RetornosAteALuminaria, $"comando {k.Id}, do último interruptor à luminária", porCadeia, retornosPorCadeia, avisos);
            }
        }

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
            var retornos = retornosPorCadeia.TryGetValue(cadeia.Id, out var r) ? r : [];
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

    static void Retorno(Grafo grafo, string de, string para, Circuito c, int condutores, string descricao,
        Dictionary<string, HashSet<string>> porCadeia, Dictionary<string, List<string>> retornosPorCadeia, List<string> avisos)
    {
        if (condutores <= 0) return;
        var caminho = grafo.CaminhoEntre(de, para, c.Id);
        if (caminho is null)
        {
            avisos.Add($"Circuito {c.Numero}, {descricao}: não há caminho pelos eletrodutos entre {de} e {para}; retorno não contado.");
            return;
        }
        foreach (var t in caminho)
        {
            if (!porCadeia.TryGetValue(t.Id, out var set)) porCadeia[t.Id] = set = [];
            set.Add(c.Id);
            if (!retornosPorCadeia.TryGetValue(t.Id, out var lista)) retornosPorCadeia[t.Id] = lista = [];
            for (var n = 0; n < condutores; n++) lista.Add(c.Id);
        }
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
