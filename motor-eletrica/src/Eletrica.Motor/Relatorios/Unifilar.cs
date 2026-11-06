using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Relatorios;

/// <summary>
///     O diagrama unifilar como dados (item 9.3 do plano; 6.1.8.1 b): para cada quadro, a entrada (alimentador ou ponto de
///     entrega), o disjuntor geral, o DPS, os DRs com os circuitos de cada um, os circuitos sem DR e os quadros alimentados.
///     O add-in desenha isto numa vista de desenho; a CLI imprime em texto. Separar o conteúdo do desenho deixa o desenho
///     trocável (símbolos do escritório) sem mexer no que é norma.
/// </summary>
public static class Unifilar
{
    public sealed record Ramo(string Numero, string Descricao, string Disjuntor, string Condutores, string Fases, string? DR, string? QuadroAlimentado);

    public sealed record BlocoDR(string Rotulo, IReadOnlyList<Ramo> Ramos);

    public sealed record Diagrama(string Quadro, string Entrada, string Geral, string? Dps, IReadOnlyList<BlocoDR> Drs, IReadOnlyList<Ramo> SemDR, IReadOnlyList<Diagrama> Filhos);

    public static IReadOnlyList<Diagrama> Montar(Instalacao i, ResultadoInstalacao r)
    {
        var porQuadro = r.Quadros.ToDictionary(q => q.Id, q => q);
        var circuitos = r.Circuitos.ToDictionary(c => c.Id, c => c);
        Diagrama Do(Quadro q)
        {
            var rq = porQuadro[q.Id];
            var alimentador = i.AlimentadorDo(q) is { } al && circuitos.TryGetValue(al.Id, out var ral) ? ral : null;
            var entrada = alimentador is not null
                ? $"Alimentador {alimentador.Numero} do {i.QuadroPai(q)?.Nome}: {Condutores(alimentador)}"
                : $"Ponto de entrega {i.Alimentacao.TensaoFaseNeutro:0}/{i.Alimentacao.TensaoFaseFase:0} V, {i.Alimentacao.Fases}F, {Memorial.Esquema(i.Alimentacao.Esquema)}" +
                  (i.Alimentacao.IccPresumidaKA is { } icc ? $", Icc {icc:0.#} kA" : "");
            var ramos = i.CircuitosDo(q).Where(c => circuitos.ContainsKey(c.Id)).Select(c => circuitos[c.Id])
                .OrderBy(c => c.Numero.Length).ThenBy(c => c.Numero, StringComparer.OrdinalIgnoreCase)
                .Select(c => new Ramo(c.Numero, c.Descricao, c.Disjuntor.ToString(), Condutores(c), c.Fases,
                    c.DR.Obrigatorio ? $"{c.DR.CorrenteResidualMA:0} mA {c.DR.Tipo}" : null, i.Circuito(c.Id)?.QuadroAlimentado is { } qa ? i.Quadro(qa)?.Nome : null))
                .ToList();
            var emDr = new HashSet<string>();
            var blocos = new List<BlocoDR>();
            foreach (var g in rq.GruposDR ?? [])
            {
                var doGrupo = ramos.Where(x => g.Circuitos.Contains(x.Numero)).ToList();
                foreach (var x in doGrupo) emDr.Add(x.Numero);
                blocos.Add(new BlocoDR($"DR{g.Numero} {g.In:0} A / {g.CorrenteResidualMA:0} mA tipo {g.Tipo} {g.Polos}P", doGrupo));
            }
            var dps = rq.Dps is { } d ? $"DPS classe {d.Classe}, {d.Quantidade} un., Uc {d.UcComercial:0} V, In ≥ {d.InMinimoKA:0} kA" : null;
            var filhos = i.Quadros.Where(f => f.QuadroPai == q.Id).Select(Do).ToList();
            return new Diagrama(q.Nome, entrada, rq.DisjuntorGeral?.ToString() ?? "—", dps, blocos, ramos.Where(x => !emDr.Contains(x.Numero)).ToList(), filhos);
        }
        return i.Quadros.Where(q => q.QuadroPai is null).Select(Do).ToList();
    }

    static string Condutores(ResultadoCircuito c)
    {
        var fases = c.Ligacao switch { Ligacao.FN => 1, Ligacao.FF or Ligacao.FFN => 2, _ => 3 };
        var neutro = c.Ligacao is Ligacao.FN or Ligacao.FFN or Ligacao.FFFN;
        var cpf = c.CondutoresPorFase > 1 ? $"{c.CondutoresPorFase}×" : "";
        return $"{fases}×{cpf}{Memorial.F(c.SecaoFase, 1)}{(neutro ? $" + N {Memorial.F(c.SecaoNeutro, 1)}" : "")} + PE {Memorial.F(c.SecaoPE, 1)} mm²";
    }

    /// <summary>Versão em texto (árvore), para a CLI e para conferência.</summary>
    public static string Texto(IReadOnlyList<Diagrama> diagramas)
    {
        var sb = new System.Text.StringBuilder();
        void Escrever(Diagrama d, string recuo)
        {
            sb.AppendLine($"{recuo}{d.Quadro}");
            sb.AppendLine($"{recuo}  entrada: {d.Entrada}");
            sb.AppendLine($"{recuo}  geral: {d.Geral}{(d.Dps is not null ? $" | {d.Dps}" : "")}");
            foreach (var b in d.Drs)
            {
                sb.AppendLine($"{recuo}  {b.Rotulo}");
                foreach (var x in b.Ramos) sb.AppendLine($"{recuo}    ├ {x.Numero} {x.Descricao}: {x.Disjuntor}, {x.Condutores}, fase {x.Fases}");
            }
            foreach (var x in d.SemDR) sb.AppendLine($"{recuo}  ├ {x.Numero} {x.Descricao}: {x.Disjuntor}, {x.Condutores}, fase {x.Fases}{(x.QuadroAlimentado is not null ? $" → {x.QuadroAlimentado}" : "")}");
            foreach (var f in d.Filhos) Escrever(f, recuo + "      ");
        }
        foreach (var d in diagramas) Escrever(d, "");
        return sb.ToString();
    }
}
