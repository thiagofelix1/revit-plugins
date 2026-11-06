using Eletrica.Motor.Calculo;
using Eletrica.Motor.Normas;
using static Eletrica.Motor.Relatorios.Docx;
using static Eletrica.Motor.Relatorios.Memorial;

namespace Eletrica.Motor.Relatorios;

/// <summary>
///     Memorial descritivo e de cálculo em .docx (item 9.8 do plano; NBR 5410 6.1.8.1 d, e, f): os mesmos dados do
///     <see cref="Memorial" /> em Markdown, com a demanda, o quadro de cargas, os seis critérios de cada circuito, os
///     eletrodutos, as verificações e as premissas. OpenXML puro; para PDF, abrir e salvar como PDF.
/// </summary>
public static class MemorialDocx
{
    public static byte[] Gerar(ResultadoInstalacao r) => Documento(body =>
    {
        var a = r.Alimentacao;
        body.AppendChild(Titulo($"Memorial de cálculo elétrico — {r.Nome}"));
        body.AppendChild(Paragrafo($"Resultado {(r.Atende ? "ATENDE" : "NÃO ATENDE")} às verificações deste memorial. Norma de referência: ABNT NBR 5410:2004 (versão corrigida 2008). " +
                                   "Gerado pelo AutoEletrica a partir do modelo; não substitui a análise e a responsabilidade técnica do projetista.",
            negrito: true, cor: r.Atende ? CorAtende : CorNaoAtende));

        body.AppendChild(Subtitulo("1. Dados da alimentação"));
        body.AppendChild(Tabela(["Item", "Valor", "Referência"],
        [
            ["Tensões", $"{F(a.TensaoFaseNeutro, 0)} / {F(a.TensaoFaseFase, 0)} V, {a.Fases} fase(s)", "4.2.2.1"],
            ["Esquema de aterramento", Esquema(a.Esquema), "4.2.2.2"],
            ["Icc presumida no ponto de entrega", a.IccPresumidaKA is { } icc ? F(icc, 1) + " kA" : "não informada", "4.2.3"],
            ["Alimentação", $"{(a.LinhaAerea ? "aérea" : "subterrânea")}, {a.DiasDeTrovoada} dias de trovoada/ano ({(a.DiasDeTrovoada > Nbr5410.DiasDeTrovoadaAQ2 ? "AQ2" : "AQ1")}){(a.TemSpda ? ", com SPDA (AQ3)" : "")}", "4.2.6, 5.4.2.1"],
            ["Temperatura ambiente", $"{F(a.TemperaturaAmbiente, 0)} °C", "6.2.5.3"],
            ["Uso", a.Habitacao ? "habitação (seção 9.5)" : "não residencial", "9.5"],
            ["Demanda", a.AplicarFatoresDeDemanda ? "fatores por tipo de carga (padrão do motor; conferir com a distribuidora)" : "sem fatores (opção do projeto)", "4.2.1.1"],
        ]));

        body.AppendChild(Subtitulo("2. Quadros"));
        foreach (var q in r.Quadros)
        {
            body.AppendChild(Paragrafo(q.Nome, negrito: true));
            List<IReadOnlyList<string>> linhas =
            [
                ["Circuitos", $"{q.NumeroDeCircuitos} + {q.Reserva} de reserva", "Tabela 59"],
                ["Potência instalada / de demanda", $"{F(q.PotenciaInstaladaVA, 0)} / {F(q.PotenciaDeDemandaVA, 0)} VA", "4.2.1"],
            ];
            if (q.Demanda is { } dem)
                foreach (var p in dem.Parcelas)
                    linhas.Add([$"Demanda: {FatoresDeDemanda.Nome(p.Categoria)}", $"{F(p.InstaladaVA, 0)} VA × {F(p.Fator, 2)} = {F(p.DemandaVA, 0)} VA ({p.Aparelhos} un.)", "4.2.1.1"]);
            linhas.Add(["Corrente de demanda", $"{F(q.CorrenteDeDemanda, 1)} A", "4.2.1"]);
            linhas.Add(["Carga por fase", string.Join("; ", q.CargasPorFase.Select(f => $"{f.Fase}: {F(f.PotenciaVA, 0)} VA ({F(f.Corrente, 1)} A)")) + $"; desequilíbrio {F(q.DesequilibrioPercentual, 0)} %", "4.2.5.6"]);
            linhas.Add(["Icc no barramento", q.IccNoBarramentoKA is { } iq ? F(iq, 2) + " kA" : "não avaliada", "5.3.5.1"]);
            if (q.DisjuntorGeral is { } geral) linhas.Add(["Disjuntor geral", geral.ToString(), "5.3.4, 6.3.4"]);
            if (q.Dps is { } dps)
                linhas.Add(["DPS", $"classe {dps.Classe}, {dps.Quantidade} un., esquema {dps.Esquema}, Uc ≥ {F(dps.UcMinimo, 0)} V, Up ≤ {F(dps.UpMaximo, 1)} kV, In ≥ {F(dps.InMinimoKA, 0)} kA{(dps.IimpMinimoKA is { } ii ? $", Iimp ≥ {F(ii, 1)} kA" : "")}, ligações {F(dps.SecaoDeLigacao, 0)} mm² — {q.MotivoDoDps}", "5.4.2.1.1, 6.3.5.2"]);
            foreach (var g in q.GruposDR ?? []) linhas.Add([g.ToString(), "até 6 circuitos por DR; In ≥ soma dos In do grupo", "6.3.3.2"]);
            foreach (var av in q.Avisos) linhas.Add(["Aviso", av, ""]);
            body.AppendChild(Tabela(["Grandeza", "Valor", "Referência"], linhas));
            body.AppendChild(Paragrafo("Quadro de cargas:", italico: true));
            body.AppendChild(Tabela(["Circ.", "Descrição", "Ligação", "Fase", "VA", "IB (A)", "F / N / PE (mm²)", "Disjuntor", "DR", "Queda (%)", "Situação"],
                r.Circuitos.Where(c => c.Quadro == q.Id).Select(c => (IReadOnlyList<string>)
                [
                    c.Numero, c.Descricao, Ligacao(c.Ligacao), c.Fases, F(c.PotenciaVA, 0), F(c.CorrenteDeProjeto, 1),
                    $"{(c.CondutoresPorFase > 1 ? $"{c.CondutoresPorFase}×" : "")}{F(c.SecaoFase, 1)} / {F(c.SecaoNeutro, 1)} / {F(c.SecaoPE, 1)}",
                    c.Disjuntor.ToString(), c.DR.Obrigatorio ? $"{F(c.DR.CorrenteResidualMA, 0)} mA {c.DR.Tipo}" : "—", F(c.QuedaTotal, 2),
                    c.Atende ? "atende" : "**" + string.Join("; ", c.Reprovados.Select(p => p.Criterio)) + "**",
                ]), fonteMenor: true));
        }

        body.AppendChild(Subtitulo("3. Circuitos: os seis critérios de 6.2.6.1.2"));
        foreach (var c in r.Circuitos)
        {
            body.AppendChild(Paragrafo($"Circuito {c.Numero} — {c.Descricao} ({c.Quadro})", negrito: true));
            body.AppendChild(Paragrafo($"{F(c.PotenciaVA, 0)} VA, {Ligacao(c.Ligacao)}{(c.Fases.Length > 0 ? $" (fase {c.Fases})" : "")}, IB = {F(c.CorrenteDeProjeto, 1)} A; L = {F(c.Comprimento, 1)} m{(c.ComprimentoEstimado ? " (informado)" : " (pelos eletrodutos)")}; " +
                                       $"{Isol(c.Isolacao)} {c.Material}, método {c.MetodoCritico}{(c.TrechoCritico is not null ? $" (trecho crítico {c.TrechoCritico})" : "")}, {c.CircuitosAgrupados} circuito(s) agrupado(s). " +
                                       $"{(c.CondutoresPorFase > 1 ? $"{c.CondutoresPorFase} × " : "")}Fase {F(c.SecaoFase, 1)} mm², neutro {F(c.SecaoNeutro, 1)} mm², PE {F(c.SecaoPE, 1)} mm², disjuntor {c.Disjuntor}. " +
                                       $"Critério que definiu a seção: {c.CriterioQueMandou}." +
                                       (c.DR.Obrigatorio ? $" DR: {c.DR.Motivo}; IΔn {F(c.DR.CorrenteResidualMA, 0)} mA, tipo {c.DR.Tipo}, In ≥ {F(c.DR.In, 0)} A." : "")));
            body.AppendChild(Tabela(["Critério", "Resultado", "Valor", "Limite", "Item", "Como foi calculado"],
                c.Pareceres.Select(p => (IReadOnlyList<string>)[p.Criterio, p.Atende is null ? "não avaliado" : p.Atende.Value ? "atende" : "**NÃO ATENDE**", p.Valor, p.Limite, p.Item, p.Explicacao])
                    .Concat(c.Avisos.Select(av => (IReadOnlyList<string>)["aviso", "", av, "", "", ""])), fonteMenor: true));
        }

        if (r.Trechos.Count > 0)
        {
            body.AppendChild(Subtitulo("4. Eletrodutos (6.2.11.1)"));
            body.AppendChild(Tabela(["Trecho", "De → Para", "L (m)", "Curvas", "Condutores", "Área (mm²)", "DN", "Taxa", "DN mínimo", "Situação"],
                r.Trechos.Select(t => (IReadOnlyList<string>)
                [
                    t.Id.Length > 12 ? t.Id.Substring(0, 12) : t.Id, $"{t.De} → {t.Para}", F(t.Comprimento, 1), t.Curvas90.ToString(), t.Condutores.ToString(), F(t.AreaDosCondutores, 0),
                    t.DnInformado?.ToString() ?? "—", (t.TaxaDeOcupacao is { } tx ? F(tx * 100, 0) + " %" : "—") + $" (≤ {F(t.TaxaMaxima * 100, 0)} %)",
                    t.DnSugerido?.ToString() ?? (t.RepeticoesSugeridas is { } n ? $"{n} eletrodutos" : "—"),
                    t.Atende ? "atende" : "**" + string.Join("; ", t.Pareceres.Where(p => p.Reprovado).Select(p => p.Criterio)) + "**",
                ]), fonteMenor: true));
        }

        body.AppendChild(Subtitulo("5. Verificações"));
        if (r.Problemas.Count == 0) body.AppendChild(Paragrafo("Nenhum problema apontado."));
        else
            body.AppendChild(Tabela(["Código", "Gravidade", "Mensagem", "Item", "Elementos"],
                r.Problemas.OrderBy(p => p.Gravidade).ThenBy(p => p.Codigo).Select(p => (IReadOnlyList<string>)[p.Codigo, p.Gravidade.ToString(), p.Mensagem, p.Item, string.Join(", ", p.Elementos.Take(6))]), fonteMenor: true));

        body.AppendChild(Subtitulo("6. Premissas do cálculo"));
        foreach (var premissa in Premissas(r)) body.AppendChild(Item(premissa));
        body.AppendChild(Paragrafo($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm}.", italico: true));
    });

    /// <summary>As premissas em linhas, compartilhadas com o Markdown.</summary>
    public static IEnumerable<string> Premissas(ResultadoInstalacao r)
    {
        var a = r.Alimentacao;
        yield return "Capacidade de corrente pelas Tabelas 36 a 39, fatores de temperatura (Tabela 40) e de agrupamento em feixe (Tabela 42, linha 1) aplicados no pior trecho do traçado (6.2.5.8).";
        yield return "Disjuntores pela ABNT NBR NM 60898: In preferenciais, I2 = 1,45 In, atuação instantânea garantida no limite superior da faixa da curva (B 5 In, C 10 In, D 20 In).";
        yield return $"Queda de tensão com a resistência do condutor na temperatura de operação estimada (IEC 60364-5-52, Anexo G) e reatância de {Condutores.ReatanciaPorMetro * 1000:0.00} Ω/km.";
        yield return $"Curto-circuito pelo método das impedâncias: rede a montante pela Icc presumida com X/R = {Eletrotecnica.RelacaoXRDaRede:0}, fator de tensão {Eletrotecnica.FatorDeTensaoMaximo} (máximo) e {Eletrotecnica.FatorDeTensaoMinimo} (mínimo), resistências a quente ×{Eletrotecnica.FatorDeResistenciaAQuente} no curto mínimo.";
        yield return "Energia passante do disjuntor: classe 3 de limitação (NBR NM 60898, Anexo ZA) quando a combinação existe; quando o limite da classe passa de k²S², o critério fica \"não comprovado\" até o I²t de catálogo ser informado.";
        yield return "Diâmetros externos dos condutores isolados pela NBR NM 247-3 (Tabelas 1 e 3); diâmetros internos dos eletrodutos de catálogo (a NBR 15465 só fixa o externo).";
        yield return $"Demanda dos quadros: {(a.AplicarFatoresDeDemanda ? "fatores por tipo de carga (" + FatoresDeDemanda.Fonte + ")" : "sem fatores de demanda (opção do projeto)")}; fases balanceadas por potência decrescente, respeitando as fases impostas.";
        yield return "Em eletroduto com seções fora de três valores sucessivos, o agrupamento usa 1/√n (6.2.5.5.5) numa segunda passada; sem seção única que atenda, dois condutores em paralelo por fase (6.2.5.7).";
        if (a.FolgaDeComprimentoPercentual > 0 || a.FolgaDeOcupacaoPercentual > 0 || a.PEComumNoConduto)
            yield return $"Opções do projeto: folga de comprimento {F(a.FolgaDeComprimentoPercentual, 0)} %, folga de ocupação {F(a.FolgaDeOcupacaoPercentual, 0)} %{(a.PEComumNoConduto ? ", um PE por eletroduto com a maior seção (6.4.3.1.5)" : "")}.";
    }
}
