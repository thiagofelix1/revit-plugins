using System.Diagnostics;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Calculo;

/// <summary>Ponto de entrada do motor: verifica, dimensiona os circuitos, avalia os eletrodutos e monta os quadros.</summary>
public static class CalculoEletrica
{
    public static ResultadoInstalacao Executar(Instalacao i)
    {
        var cronometro = Stopwatch.StartNew();
        var grafo = new Grafo(i);
        var problemas = Verificacao.Verificar(i, grafo).ToList();
        var dimensionamento = new Dimensionamento(i, grafo);
        var circuitos = dimensionamento.DimensionarTodos();
        var trechos = Ocupacao.Avaliar(i, dimensionamento.Resultados);
        var quadros = i.Quadros.Select(q => MontarQuadro(i, q, dimensionamento)).ToList();

        foreach (var c in circuitos)
            foreach (var p in c.Reprovados)
                problemas.Add(new Problema("D-" + Sigla(p.Criterio), Gravidade.Erro, $"Circuito {c.Numero}: {p.Criterio} não atende ({p.Valor}; limite {p.Limite}).", p.Item, [c.Id]));
        foreach (var t in trechos)
            foreach (var p in t.Pareceres.Where(p => p.Reprovado))
                problemas.Add(new Problema("E-" + Sigla(p.Criterio), Gravidade.Erro, $"Trecho {t.Id}: {p.Criterio} não atende ({p.Valor}; limite {p.Limite}).", p.Item, [t.Id]));

        cronometro.Stop();
        return new ResultadoInstalacao(i.Nome, i.Alimentacao, circuitos, quadros, trechos, problemas, cronometro.Elapsed);
    }

    static ResultadoQuadro MontarQuadro(Instalacao i, Quadro q, Dimensionamento d)
    {
        var a = i.Alimentacao;
        var circuitos = i.CircuitosDo(q).ToList();
        var resultados = circuitos.Select(c => d.Resultados[c.Id]).ToList();
        var instalada = d.PotenciaInstalada(q);
        var demanda = d.PotenciaDeDemanda(q);
        var avisos = new List<string>();

        // Distribuição por fase: monofásicos alternam entre as fases (balanceamento por ordem de potência decrescente),
        // bifásicos nas duas fases com menor carga, trifásicos nas três.
        string[] nomes = q.Fases switch { 1 => ["A"], 2 => ["A", "B"], _ => ["A", "B", "C"] };
        var carga = new double[nomes.Length];
        foreach (var r in resultados.OrderByDescending(r => r.PotenciaVA))
        {
            var fases = Math.Min(r.Ligacao switch { Ligacao.FN => 1, Ligacao.FF or Ligacao.FFN => 2, _ => 3 }, nomes.Length);
            var escolhidas = Enumerable.Range(0, nomes.Length).OrderBy(k => carga[k]).Take(fases).ToList();
            foreach (var k in escolhidas) carga[k] += r.PotenciaVA / fases;
        }
        var uo = a.TensaoFaseNeutro;
        var porFase = nomes.Select((n, k) => new CargaPorFase(n, carga[k], carga[k] / uo)).ToList();
        var media = carga.Average();
        var desequilibrio = media > 0 ? 100 * (carga.Max() - carga.Min()) / media : 0;
        if (desequilibrio > 20 && nomes.Length > 1) avisos.Add($"desequilíbrio entre fases de {desequilibrio:0} % (recomendado ≤ 20 %): redistribuir os circuitos monofásicos");

        var ligacao = q.Fases switch { 1 => Ligacao.FN, 2 => Ligacao.FFN, _ => Ligacao.FFFN };
        var ibQuadro = Eletrotecnica.CorrenteDeProjeto(demanda, ligacao, uo, a.TensaoFaseFase);
        var alimentador = i.AlimentadorDo(q) is { } al ? d.Resultados[al.Id] : null;
        var iccKA = CurtoNoBarramento(i, q, d);
        var geralIn = alimentador?.Disjuntor.In ?? Nbr60898.MenorInNaoInferiorA(ibQuadro) ?? 125;
        var geral = new Disjuntor(geralIn, CurvaDisjuntor.C, iccKA is { } icc ? Nbr60898.IcnMinima(icc) : 3, Dimensionamento.Polos(ligacao, a.Esquema));

        var motivoDps = q.QuadroPai is null ? DispositivosDPS.MotivoDeObrigatoriedade(a) : null;
        var dps = motivoDps is not null ? DispositivosDPS.Especificar(a, quadroDeEntrada: true) : null;
        var comDr = resultados.Where(r => r.DR.Obrigatorio).Select(r => r.Numero).ToList();
        if (comDr.Count > DispositivosDR.CircuitosPorDRRecomendados)
            avisos.Add($"{comDr.Count} circuitos exigem DR: dividir em mais de um DR de 30 mA (fuga acumulada) ou usar disjuntores DR por circuito");

        return new ResultadoQuadro(q.Id, q.Nome, q.Fases, circuitos.Count, Nbr5410.ReservaDoQuadro(circuitos.Count), instalada, demanda, ibQuadro, porFase,
            desequilibrio, iccKA, geral, dps, motivoDps, comDr, avisos);
    }

    static double? CurtoNoBarramento(Instalacao i, Quadro q, Dimensionamento d)
    {
        if (i.Alimentacao.IccPresumidaKA is not { } icc) return null;
        var uo = i.Alimentacao.TensaoFaseNeutro;
        var z = Eletrotecnica.ImpedanciaDaRede(icc, uo);
        for (var atual = q; i.AlimentadorDo(atual) is { } al; atual = i.QuadroPai(atual)!)
        {
            var r = d.Resultados[al.Id];
            z += Eletrotecnica.ImpedanciaDoCondutor(r.Material, r.SecaoFase, r.Comprimento);
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
        _ => "X",
    };
}
