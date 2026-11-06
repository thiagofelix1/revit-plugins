using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Leitura;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Condutos;

/// <summary>
/// Leva cada circuito do quadro até cada ponto pelo menor caminho no grafo de eletrodutos e grava em cada eletroduto da
/// cadeia os circuitos que passam (AE Circuitos), quantos condutores (AE Condutores) e a etiqueta de fiação (AE Fiação).
/// É o dado mestre da fiação: o fio desenhado do Revit é 2D e por vista; o trecho de eletroduto com a lista de circuitos
/// é o que o cálculo e as etiquetas usam.
/// </summary>
public static class PassarCircuitos
{
    public sealed record Feito(int Circuitos, int Eletrodutos, IReadOnlyList<string> Avisos);

    public static Feito Executar(Document doc, LeitorInstalacao.Lida lida, IReadOnlyDictionary<string, ResultadoCircuito>? resultados = null)
    {
        var i = lida.Instalacao;
        var avisos = new List<string>();
        var porCadeia = new Dictionary<string, HashSet<string>>();
        var cadeiaPorId = lida.Grafo.Cadeias.ToDictionary(c => c.Id, c => c);

        // Grafo só com a topologia (todos os trechos valem para todos os circuitos nesta fase).
        var todos = i.Trechos.Select(t => t with { Circuitos = i.Circuitos.Select(c => c.Id).ToList() }).ToList();
        var topologia = new Instalacao(i.Nome, i.Alimentacao, i.Ambientes, i.Quadros, i.Circuitos, i.Pontos, todos);
        var grafo = new Grafo(topologia);

        var circuitosLevados = 0;
        foreach (var c in i.Circuitos)
        {
            var destinos = c.QuadroAlimentado is { } qa
                ? [i.Quadro(qa)!.NoDoQuadro]
                : i.PontosDo(c).Select(p => p.NoDoPonto).Distinct().ToList();
            if (destinos.Count == 0) continue;
            var caminhos = grafo.CaminhosDe(c);
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
            var condutores = circuitos.Sum(c => c.Fases + (c.TemNeutro ? 1 : 0) + 1);
            var fiacao = Etiqueta(circuitos, resultados);
            foreach (var conduite in cadeia.Conduites)
            {
                ParametrosEletrica.Gravar(conduite, ParametrosEletrica.Circuitos, string.Join(";", ids));
                ParametrosEletrica.Gravar(conduite, ParametrosEletrica.Condutores, condutores);
                ParametrosEletrica.Gravar(conduite, ParametrosEletrica.Fiacao, fiacao);
                eletrodutos++;
            }
            foreach (var conexao in cadeia.Conexoes)
            {
                ParametrosEletrica.Gravar(conexao, ParametrosEletrica.Circuitos, string.Join(";", ids));
                ParametrosEletrica.Gravar(conexao, ParametrosEletrica.Condutores, condutores);
                ParametrosEletrica.Gravar(conexao, ParametrosEletrica.Fiacao, fiacao);
            }
        }
        transacao.Commit();
        return new Feito(circuitosLevados, eletrodutos, avisos);
    }

    /// <summary>
    /// Etiqueta de fiação no formato "3#2,5(2,5) C1 C3": condutores vivos # seção (PE) e os circuitos, agrupados por seção.
    /// Antes do cálculo, só a contagem: "3 cond. C1 C3".
    /// </summary>
    public static string Etiqueta(IReadOnlyList<Circuito> circuitos, IReadOnlyDictionary<string, ResultadoCircuito>? resultados)
    {
        if (circuitos.Count == 0) return "";
        if (resultados is null || circuitos.Any(c => !resultados.ContainsKey(c.Id)))
            return $"{circuitos.Sum(c => c.Fases + (c.TemNeutro ? 1 : 0) + 1)} cond. " + string.Join(" ", circuitos.Select(c => "C" + c.Numero));
        var grupos = circuitos.GroupBy(c => (resultados[c.Id].SecaoFase, resultados[c.Id].SecaoPE))
            .OrderByDescending(g => g.Key.SecaoFase)
            .Select(g =>
            {
                var vivos = g.Sum(c => c.Fases + (c.TemNeutro ? 1 : 0));
                return $"{vivos}#{F(g.Key.SecaoFase)}({F(g.Key.SecaoPE)}) " + string.Join(" ", g.Select(c => "C" + c.Numero));
            });
        return string.Join(" + ", grupos);
    }

    static string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
