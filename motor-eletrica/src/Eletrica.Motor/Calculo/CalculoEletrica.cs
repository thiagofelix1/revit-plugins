using System.Diagnostics;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Calculo;

/// <summary>
/// Ponto de entrada do motor: verifica, dimensiona os circuitos (duas passadas quando um eletroduto reúne seções
/// dessemelhantes, 6.2.5.5.5), avalia os eletrodutos, balanceia as fases e monta os quadros (demanda, geral, DRs por
/// grupo, DPS). Cada critério reprovado vira um problema com a sugestão do que fazer (item 8.8 do plano).
/// </summary>
public static class CalculoEletrica
{
    public static ResultadoInstalacao Executar(Instalacao i)
    {
        var cronometro = Stopwatch.StartNew();
        var grafo = new Grafo(i);
        var problemas = Verificacao.Verificar(i, grafo).ToList();

        // Passada 1; se algum trecho ficou com seções fora de três valores sucessivos, passada 2 com 1/√n nesses trechos.
        var dimensionamento = new Dimensionamento(i, grafo);
        dimensionamento.DimensionarTodos();
        var impostos = TrechosComSecoesDessemelhantes(i, dimensionamento.Resultados);
        if (impostos.Count > 0)
        {
            dimensionamento = new Dimensionamento(i, grafo, impostos);
            dimensionamento.DimensionarTodos();
        }

        var trechos = Ocupacao.Avaliar(i, dimensionamento.Resultados);
        var fasesPorQuadro = i.Quadros.ToDictionary(q => q.Id, q => Fases.Distribuir(q,
            i.CircuitosDo(q).Select(c => (c, dimensionamento.Resultados[c.Id].PotenciaVA)).ToList(), i.Alimentacao.TensaoFaseNeutro));
        var circuitos = dimensionamento.DimensionarTodos()
            .Select(r => r with { Fases = fasesPorQuadro[r.Quadro].FasePorCircuito.TryGetValue(r.Id, out var f) ? f : "" }).ToList();
        var quadros = i.Quadros.Select(q => MontarQuadro(i, q, dimensionamento, fasesPorQuadro[q.Id])).ToList();

        foreach (var c in circuitos)
            foreach (var p in c.Reprovados)
                problemas.Add(new Problema("D-" + Sigla(p.Criterio), Gravidade.Erro, $"Circuito {c.Numero}: {p.Criterio} não atende ({p.Valor}; limite {p.Limite}). Sugestão: {Sugestoes.Para(p.Criterio)}.", p.Item, [c.Id]));
        foreach (var t in trechos)
            foreach (var p in t.Pareceres.Where(p => p.Reprovado))
                // 15 m e curvas já saem do Verificar (T-01, T-02) antes do cálculo; aqui só o que depende das seções.
                if (p.Criterio is not ("trecho entre caixas" or "curvas"))
                    problemas.Add(new Problema("E-" + Sigla(p.Criterio), Gravidade.Erro, $"Trecho {t.Id}: {p.Criterio} não atende ({p.Valor}; limite {p.Limite}). Sugestão: {Sugestoes.Para(p.Criterio)}.", p.Item, [t.Id]));

        cronometro.Stop();
        return new ResultadoInstalacao(i.Nome, i.Alimentacao, circuitos, quadros, trechos, problemas, cronometro.Elapsed);
    }

    /// <summary>Trechos em que as seções dos circuitos saem de três valores sucessivos: fator 1/√n no lugar da Tabela 42 (6.2.5.5.5).</summary>
    static Dictionary<string, double> TrechosComSecoesDessemelhantes(Instalacao i, IReadOnlyDictionary<string, ResultadoCircuito> resultados)
    {
        var impostos = new Dictionary<string, double>();
        foreach (var t in i.Trechos)
        {
            var ids = t.Circuitos.Distinct().Where(resultados.ContainsKey).ToList();
            if (ids.Count < 2) continue;
            var indices = ids.Select(id => Array.IndexOf(Nbr5410Capacidades.Secoes, resultados[id].SecaoFase)).Where(k => k >= 0).ToList();
            if (indices.Count > 1 && indices.Max() - indices.Min() > 2)
                impostos[t.Id] = Nbr5410.FatorDeAgrupamentoDessemelhante(ids.Count);
        }
        return impostos;
    }

    static ResultadoQuadro MontarQuadro(Instalacao i, Quadro q, Dimensionamento d, Fases.Resultado fases)
    {
        var a = i.Alimentacao;
        var circuitos = i.CircuitosDo(q).ToList();
        var resultados = circuitos.Select(c => d.Resultados[c.Id]).ToList();
        var instalada = d.PotenciaInstalada(q);
        var demanda = d.DemandaDo(q);
        var avisos = new List<string>(fases.Avisos);

        var uo = a.TensaoFaseNeutro;
        var ligacao = q.Fases switch { 1 => Ligacao.FN, 2 => Ligacao.FFN, _ => Ligacao.FFFN };
        var ibQuadro = Eletrotecnica.CorrenteDeProjeto(demanda.DemandaVA, ligacao, uo, a.TensaoFaseFase);
        var alimentador = i.AlimentadorDo(q) is { } al ? d.Resultados[al.Id] : null;
        var iccKA = CurtoNoBarramento(i, q, d);
        var geralIn = alimentador?.Disjuntor.In ?? Nbr60898.MenorInNaoInferiorA(ibQuadro) ?? 125;
        var geral = new Disjuntor(geralIn, CurvaDisjuntor.C, iccKA is { } icc ? Nbr60898.IcnMinima(icc) : 3, Dimensionamento.Polos(ligacao, a.Esquema));
        if (alimentador is null && Nbr60898.MenorInNaoInferiorA(ibQuadro) is null)
            avisos.Add($"corrente de demanda de {ibQuadro:0} A passa do maior disjuntor NM 60898 (125 A): o geral é um disjuntor em caixa moldada (NBR IEC 60947-2)");

        var motivoDps = q.QuadroPai is null ? DispositivosDPS.MotivoDeObrigatoriedade(a) : null;
        var dps = motivoDps is not null ? DispositivosDPS.Especificar(a, quadroDeEntrada: true) : null;
        var comDr = resultados.Where(r => r.DR.Obrigatorio).Select(r => r.Numero).ToList();
        var grupos = GruposDeDR(resultados, fases.FasePorCircuito);
        if (grupos.Count > 1)
            avisos.Add($"{comDr.Count} circuitos exigem DR: {grupos.Count} DRs (até {DispositivosDR.CircuitosPorDRRecomendados} circuitos cada, por tipo) ou disjuntores DR por circuito");

        return new ResultadoQuadro(q.Id, q.Nome, q.Fases, circuitos.Count, Nbr5410.ReservaDoQuadro(circuitos.Count), instalada, demanda.DemandaVA, ibQuadro, fases.Cargas,
            fases.DesequilibrioPercentual, iccKA, geral, dps, motivoDps, comDr, avisos, demanda, grupos);
    }

    /// <summary>
    /// DRs do quadro: os circuitos que exigem DR, separados por tipo (B e F cada um no seu DR; A e AC juntos), em grupos de
    /// até seis, com In do DR não inferior à soma dos In dos disjuntores do grupo (ou ao geral, se menor), nem ao maior deles.
    /// </summary>
    static List<GrupoDR> GruposDeDR(List<ResultadoCircuito> resultados, IReadOnlyDictionary<string, string> fasePorCircuito)
    {
        var grupos = new List<GrupoDR>();
        var numero = 1;
        foreach (var porTipo in resultados.Where(r => r.DR.Obrigatorio).GroupBy(r => (Tipo: r.DR.Tipo is TipoDr.AC ? TipoDr.A : r.DR.Tipo, r.DR.CorrenteResidualMA)).OrderBy(g => g.Key.Tipo))
        {
            var lista = porTipo.OrderBy(r => r.Numero, StringComparer.OrdinalIgnoreCase).ToList();
            var tamanho = porTipo.Key.Tipo is TipoDr.B ? 1 : DispositivosDR.CircuitosPorDRRecomendados;
            for (var k = 0; k < lista.Count; k += tamanho)
            {
                var grupo = lista.Skip(k).Take(tamanho).ToList();
                var soma = grupo.Sum(r => r.Disjuntor.In);
                var inDr = DispositivosDR.MenorInNaoInferiorA(Math.Max(soma, grupo.Max(r => r.Disjuntor.In))) ?? DispositivosDR.CorrentesNominais[DispositivosDR.CorrentesNominais.Length - 1];
                // Bipolar só quando todos os circuitos do grupo são fase-neutro na mesma fase; com duas ou três fases no grupo, tetrapolar.
                var fasesDoGrupo = grupo.SelectMany(r => fasePorCircuito.TryGetValue(r.Id, out var f) ? f.ToCharArray() : []).Distinct().Count();
                var polos = fasesDoGrupo > 1 || grupo.Any(r => r.Ligacao is not Ligacao.FN) ? 4 : 2;
                grupos.Add(new GrupoDR(numero++, porTipo.Key.Tipo, porTipo.Key.CorrenteResidualMA, inDr, polos, grupo.Select(r => r.Numero).ToList()));
            }
        }
        return grupos;
    }

    static double? CurtoNoBarramento(Instalacao i, Quadro q, Dimensionamento d)
    {
        if (i.Alimentacao.IccPresumidaKA is not { } icc) return null;
        var uo = i.Alimentacao.TensaoFaseNeutro;
        var z = Eletrotecnica.ImpedanciaDaRede(icc, uo);
        for (var atual = q; i.AlimentadorDo(atual) is { } al; atual = i.QuadroPai(atual)!)
        {
            var r = d.Resultados[al.Id];
            z += Eletrotecnica.ImpedanciaDoCondutor(r.Material, r.SecaoFase, r.Comprimento) / r.CondutoresPorFase;
        }
        return Eletrotecnica.CurtoMaximo(z, uo) / 1000;
    }

    static string Sigla(string criterio) => criterio switch
    {
        "capacidade de corrente" => "IZ",
        "sobrecarga" => "IN",
        "queda de tensão" => "QT",
        "capacidade de interrupção" => "ICN",
        "solicitação térmica no curto" => "I2T",
        "solicitação térmica no curto mínimo" => "I2TMIN",
        "seccionamento automático" => "ZS",
        "seção mínima" => "SMIN",
        "taxa de ocupação" => "OCUP",
        "trecho entre caixas" => "15M",
        "curvas" => "CURV",
        "mesma origem" => "ORIG",
        "seções próximas" => "SEC3",
        _ => "X",
    };
}
