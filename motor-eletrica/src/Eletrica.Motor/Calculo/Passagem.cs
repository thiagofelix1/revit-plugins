using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Calculo;

/// <summary>Resultado da passagem: os circuitos e os retornos de cada trecho, quantos circuitos chegaram e os avisos.</summary>
public sealed record ResultadoPassagem(
    IReadOnlyDictionary<string, IReadOnlyList<string>> CircuitosPorTrecho,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RetornosPorTrecho,
    int CircuitosLevados,
    IReadOnlyList<string> Avisos);

/// <summary>
/// Passagem de fiação: leva cada circuito do quadro até os seus pontos (e até o primeiro interruptor de cada comando, para a
/// fase, 5.6.6.1.2) pelo menor caminho nos trechos em que ele pode passar, e conta os retornos dos comandos de iluminação
/// (2 entre interruptores em paralelo ou intermediário, 1 do último interruptor a cada luminária).
/// <para>
/// Trechos paralelos (mesmas pontas, comprimento até 20 % maior que o mais curto) dividem os circuitos: cada circuito vai
/// pelo paralelo com menos circuitos, e fica nele em todo o percurso. Sem isso, todos os circuitos de um quadro iam pelo
/// mesmo eletroduto e o agrupamento e a ocupação saíam piores que o modelado.
/// </para>
/// </summary>
public static class Passagem
{
    const double FolgaDeParalelo = 1.2;

    /// <param name="topologia">A instalação com, em cada trecho, os circuitos que PODEM passar por ele (todos, menos os evitados).</param>
    /// <param name="forcados">Por trecho, circuitos que DEVEM passar por ele (fiação manual), somados ao resultado.</param>
    public static ResultadoPassagem Executar(Instalacao topologia, IReadOnlyDictionary<string, IReadOnlyList<string>>? forcados = null)
    {
        var i = topologia;
        var grafo = new Grafo(i);
        var avisos = new List<string>();
        var porTrecho = new Dictionary<string, HashSet<string>>();
        var retornos = new Dictionary<string, List<string>>();
        var paralelos = i.Trechos.GroupBy(Pontas).ToDictionary(g => g.Key, g => g.ToList());

        void Marcar(string trecho, string circuito)
        {
            if (!porTrecho.TryGetValue(trecho, out var set)) porTrecho[trecho] = set = [];
            set.Add(circuito);
        }

        // Troca cada trecho do caminho pelo paralelo onde o circuito já está ou, se não está em nenhum, pelo menos ocupado.
        IReadOnlyList<Trecho> Distribuir(IReadOnlyList<Trecho> caminho, Circuito c)
        {
            var escolhidos = new List<Trecho>(caminho.Count);
            foreach (var t in caminho)
            {
                if (!paralelos.TryGetValue(Pontas(t), out var opcoes) || opcoes.Count < 2) { escolhidos.Add(t); continue; }
                var candidatos = opcoes.Where(p => p.Circuitos.Contains(c.Id) && p.Comprimento <= t.Comprimento * FolgaDeParalelo + 1e-9).ToList();
                var jaUsado = candidatos.FirstOrDefault(p => porTrecho.TryGetValue(p.Id, out var s) && s.Contains(c.Id));
                escolhidos.Add(jaUsado ?? candidatos
                    .OrderBy(p => porTrecho.TryGetValue(p.Id, out var s) ? s.Count : 0)
                    .ThenBy(p => p.Id == t.Id ? 0 : 1)
                    .ThenBy(p => p.Id, StringComparer.Ordinal)
                    .FirstOrDefault() ?? t);
            }
            return escolhidos;
        }

        var levados = 0;
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
            var semCaminho = destinos.Count(d => !caminhos.ContainsKey(d));
            if (semCaminho > 0)
                avisos.Add(semCaminho == destinos.Count
                    ? $"Circuito {c.Numero}: nenhum dos {destinos.Count} ponto(s) tem caminho pelos eletrodutos até o quadro {i.QuadroDe(c).Nome}."
                    : $"Circuito {c.Numero}: {semCaminho} de {destinos.Count} ponto(s) sem caminho pelos eletrodutos até o quadro {i.QuadroDe(c).Nome}.");
            // Do mais longe para o mais perto: o percurso mais comprido fixa os paralelos do circuito primeiro.
            foreach (var caminho in caminhos.Values.OrderByDescending(k => k.Sum(t => t.Comprimento)))
                foreach (var t in Distribuir(caminho, c))
                    Marcar(t.Id, c.Id);
            if (caminhos.Count > 0) levados++;

            foreach (var k in comandos)
            {
                var entre = Comando.RetornosEntreInterruptores(k.Tipo);
                for (var n = 0; n + 1 < k.Interruptores.Count; n++)
                    Retorno(k.NoDoInterruptor(n), k.NoDoInterruptor(n + 1), entre, $"comando {k.Id}, interruptores {n + 1} e {n + 2}");
                var ultimo = k.NoDoInterruptor(k.Interruptores.Count - 1);
                foreach (var no in k.Luminarias.Select(l => i.Ponto(l)?.NoDoPonto).OfType<string>().Distinct())
                    Retorno(ultimo, no, Comando.RetornosAteALuminaria, $"comando {k.Id}, do último interruptor à luminária");
            }

            void Retorno(string de, string para, int condutores, string descricao)
            {
                if (condutores <= 0) return;
                var caminho = grafo.CaminhoEntre(de, para, c.Id);
                if (caminho is null)
                {
                    avisos.Add($"Circuito {c.Numero}, {descricao}: não há caminho pelos eletrodutos entre o interruptor e a luminária; retorno não contado.");
                    return;
                }
                foreach (var t in Distribuir(caminho, c))
                {
                    Marcar(t.Id, c.Id);
                    if (!retornos.TryGetValue(t.Id, out var lista)) retornos[t.Id] = lista = [];
                    for (var n = 0; n < condutores; n++) lista.Add(c.Id);
                }
            }
        }

        if (forcados is not null)
            foreach (var kv in forcados)
                foreach (var id in kv.Value)
                    Marcar(kv.Key, id);

        return new ResultadoPassagem(
            porTrecho.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.ToList()),
            retornos.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
            levados,
            avisos);
    }

    static (string, string) Pontas(Trecho t) => string.CompareOrdinal(t.De, t.Para) <= 0 ? (t.De, t.Para) : (t.Para, t.De);
}
