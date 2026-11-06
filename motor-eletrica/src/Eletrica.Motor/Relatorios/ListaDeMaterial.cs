using System.Globalization;
using System.Text;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Relatorios;

/// <summary>Um item da lista de material: grupo, descrição, unidade, quantidade e de onde veio.</summary>
public sealed record ItemDeMaterial(string Grupo, string Descricao, string Unidade, double Quantidade, string Origem);

/// <summary>
///     Lista de material (item 9.9 do plano) a partir do cálculo: condutores por seção e função, com o comprimento de cada
///     trecho vezes os condutores que passam nele (mais os retornos e a folga de corte), eletrodutos por tipo e DN, disjuntores,
///     DRs, DPS, pontos por tipo, caixas e quadros. Em Markdown e CSV; o add-in soma as tabelas nativas do Revit para o que
///     está modelado (eletrodutos, conexões, pontos).
/// </summary>
public static class ListaDeMaterial
{
    static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Folga de corte e emendas sobre o comprimento dos condutores, em %.</summary>
    public const double FolgaDeCondutores = 10;

    public static IReadOnlyList<ItemDeMaterial> Gerar(Instalacao i, ResultadoInstalacao r)
    {
        var itens = new List<ItemDeMaterial>();
        var porId = r.Circuitos.ToDictionary(c => c.Id, c => c);
        var circuitos = i.Circuitos.ToDictionary(c => c.Id, c => c);

        // ----- condutores: por trecho, cada circuito leva fases + neutro + PE (ou PE comum), mais retornos -----
        var metros = new Dictionary<(double Secao, string Funcao, Isolacao Isolacao), double>();
        void Somar(double secao, string funcao, Isolacao isolacao, double m)
        {
            var chave = (secao, funcao, isolacao);
            metros[chave] = (metros.TryGetValue(chave, out var atual) ? atual : 0) + m;
        }
        var comTrechos = new HashSet<string>();
        foreach (var t in i.Trechos)
        {
            double maiorPE = 0;
            var isolPE = Isolacao.Pvc70;
            foreach (var id in t.Circuitos.Distinct())
            {
                if (!porId.TryGetValue(id, out var rc) || !circuitos.TryGetValue(id, out var c)) continue;
                comTrechos.Add(id);
                Somar(rc.SecaoFase, "fase", c.Isolacao, t.Comprimento * c.Fases * rc.CondutoresPorFase);
                if (c.TemNeutro) Somar(rc.SecaoNeutro, "neutro", c.Isolacao, t.Comprimento * rc.CondutoresPorFase);
                if (i.Alimentacao.PEComumNoConduto) { if (rc.SecaoPE > maiorPE) { maiorPE = rc.SecaoPE; isolPE = c.Isolacao; } }
                else Somar(rc.SecaoPE, "proteção (PE)", c.Isolacao, t.Comprimento);
            }
            if (maiorPE > 0) Somar(maiorPE, "proteção (PE)", isolPE, t.Comprimento);
            foreach (var id in t.Retornos)
                if (porId.TryGetValue(id, out var rc) && circuitos.TryGetValue(id, out var c)) Somar(rc.SecaoFase, "retorno", c.Isolacao, t.Comprimento);
        }
        // Circuitos sem eletrodutos modelados: pelo comprimento do cálculo (informado), fases + neutro + PE.
        foreach (var rc in r.Circuitos.Where(c => !comTrechos.Contains(c.Id) && c.Comprimento > 0))
        {
            if (!circuitos.TryGetValue(rc.Id, out var c)) continue;
            Somar(rc.SecaoFase, "fase", c.Isolacao, rc.Comprimento * c.Fases * rc.CondutoresPorFase);
            if (c.TemNeutro) Somar(rc.SecaoNeutro, "neutro", c.Isolacao, rc.Comprimento * rc.CondutoresPorFase);
            Somar(rc.SecaoPE, "proteção (PE)", c.Isolacao, rc.Comprimento);
        }
        foreach (var kv in metros.OrderBy(k => k.Key.Secao).ThenBy(k => k.Key.Funcao))
            itens.Add(new ItemDeMaterial("Condutores", $"Cabo {F(kv.Key.Secao, 1)} mm² {Memorial.Isol(kv.Key.Isolacao)} — {kv.Key.Funcao}", "m",
                Math.Ceiling(kv.Value * (1 + FolgaDeCondutores / 100)), $"comprimento dos trechos × condutores, com {FolgaDeCondutores:0} % de folga (NBR NM 247-3 / 7286)"));

        // ----- eletrodutos por tipo e DN; caixas -----
        foreach (var g in i.Trechos.Where(t => t.Circuitos.Count > 0 || t.Retornos.Count > 0).GroupBy(t => (t.TipoEletroduto, Dn: t.Dn ?? 0)).OrderBy(g => g.Key.TipoEletroduto).ThenBy(g => g.Key.Dn))
            itens.Add(new ItemDeMaterial("Eletrodutos", $"Eletroduto {Nome(g.Key.TipoEletroduto)} DN {(g.Key.Dn > 0 ? g.Key.Dn.ToString() : "?")}", "m", Math.Ceiling(g.Sum(t => t.Comprimento) * 1.05), "trechos com circuitos, 5 % de folga (NBR 15465)"));
        if (i.Caixas.Count > 0) itens.Add(new ItemDeMaterial("Eletrodutos", "Caixa de passagem", "un", i.Caixas.Count, "caixas do modelo"));
        var repetir = r.Trechos.Where(t => t.RepeticoesSugeridas is > 1).ToList();
        if (repetir.Count > 0) itens.Add(new ItemDeMaterial("Eletrodutos", "Trechos a repetir (nenhum DN basta)", "trecho", repetir.Count, "ver memorial, seção 4"));

        // ----- proteções -----
        foreach (var g in r.Circuitos.GroupBy(c => c.Disjuntor.ToString()).OrderBy(g => g.First().Disjuntor.Polos).ThenBy(g => g.First().Disjuntor.In))
            itens.Add(new ItemDeMaterial("Proteções", $"Disjuntor {g.Key}", "un", g.Count(), "NBR NM 60898; circuitos " + string.Join(", ", g.Select(c => c.Numero))));
        foreach (var q in r.Quadros)
        {
            if (q.DisjuntorGeral is { } geral) itens.Add(new ItemDeMaterial("Proteções", $"Disjuntor geral {geral} — {q.Nome}", "un", 1, "disjuntor geral do quadro"));
            foreach (var g in q.GruposDR ?? [])
                itens.Add(new ItemDeMaterial("Proteções", $"DR {g.In:0} A / {g.CorrenteResidualMA:0} mA tipo {g.Tipo} {g.Polos}P — {q.Nome}", "un", 1, "circuitos " + string.Join(", ", g.Circuitos) + " (IEC 61008-1)"));
            if (q.Dps is { } dps)
                itens.Add(new ItemDeMaterial("Proteções", $"DPS classe {dps.Classe}, Uc ≥ {dps.UcComercial:0} V, Up ≤ {dps.UpMaximo:0.#} kV, In ≥ {dps.InMinimoKA:0} kA{(dps.IimpMinimoKA is { } ii ? $", Iimp ≥ {ii:0.#} kA" : "")} — {q.Nome}", "un", dps.Quantidade, "5.4.2.1.1, 6.3.5.2"));
            itens.Add(new ItemDeMaterial("Quadros", $"Quadro de distribuição {q.Nome}: {q.NumeroDeCircuitos + q.Reserva} posições{(q.DisjuntorGeral is { } gg ? $", barramento {gg.In:0} A" : "")}", "un", 1, "Tabela 59 (reserva incluída); NBR IEC 61439"));
        }

        // ----- pontos -----
        foreach (var g in i.Pontos.GroupBy(p => (p.Tipo, p.CorrenteDaTomada)).OrderBy(g => g.Key.Tipo))
            itens.Add(new ItemDeMaterial("Pontos", NomeDoPonto(g.Key.Tipo, g.Key.CorrenteDaTomada), "un", g.Count(), "pontos do modelo"));
        var interruptores = i.Comandos.SelectMany(k => k.Interruptores).Distinct().Count();
        if (interruptores > 0) itens.Add(new ItemDeMaterial("Pontos", "Interruptor (nos comandos gravados)", "un", interruptores, "comandos de iluminação"));

        return itens;
    }

    public static string Markdown(IReadOnlyList<ItemDeMaterial> itens, string nome)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Lista de material — {nome}");
        sb.AppendLine();
        sb.AppendLine("| Grupo | Item | Un. | Quantidade | Origem |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var it in itens) sb.AppendLine($"| {it.Grupo} | {it.Descricao} | {it.Unidade} | {F(it.Quantidade, it.Unidade == "m" ? 0 : 0)} | {it.Origem} |");
        sb.AppendLine();
        sb.AppendLine($"Condutores com {FolgaDeCondutores:0} % de folga de corte; eletrodutos com 5 %. Quantidades do modelo e do cálculo; conferir com o levantamento em obra. Gerada pelo AutoEletrica em {DateTime.Now:dd/MM/yyyy}.");
        return sb.ToString();
    }

    public static string Csv(IReadOnlyList<ItemDeMaterial> itens)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Grupo;Item;Unidade;Quantidade;Origem");
        foreach (var it in itens) sb.AppendLine($"{it.Grupo};{it.Descricao.Replace(';', ',')};{it.Unidade};{it.Quantidade.ToString("0.##", Pt)};{it.Origem.Replace(';', ',')}");
        return sb.ToString();
    }

    static string F(double v, int casas) => v.ToString("N" + casas, Pt);

    public static string Nome(TipoEletroduto t) => t switch
    {
        TipoEletroduto.RigidoRoscavel => "PVC rígido roscável",
        TipoEletroduto.RigidoSoldavel => "PVC rígido soldável",
        TipoEletroduto.FlexivelCorrugado => "flexível corrugado",
        TipoEletroduto.FlexivelPlano => "flexível plano",
        TipoEletroduto.Aco => "de aço",
        _ => "eletrocalha",
    };

    static string NomeDoPonto(TipoCarga tipo, int? corrente) => tipo switch
    {
        TipoCarga.Iluminacao => "Ponto de luz",
        TipoCarga.TomadaUsoGeral => corrente is { } a ? $"Tomada de uso geral {a} A (NBR 14136)" : "Tomada de uso geral",
        TipoCarga.TomadaUsoEspecifico => corrente is { } a ? $"Tomada de uso específico {a} A (NBR 14136)" : "Ponto de uso específico",
        TipoCarga.Chuveiro => "Ponto de chuveiro / aquecedor",
        TipoCarga.ArCondicionado => "Ponto de ar-condicionado",
        TipoCarga.Motor => "Ponto de motor",
        TipoCarga.Aquecimento => "Ponto de aquecimento",
        TipoCarga.VeiculoEletrico => "Ponto de recarga de VE",
        _ => "Outro ponto",
    };
}
