using System.Globalization;
using System.Text;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Relatorios;

/// <summary>Memorial de cálculo em Markdown: critérios, quadro de cargas, circuito por circuito com os seis critérios, eletrodutos e verificações.</summary>
public static class Memorial
{
    static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    public static string F(double v, int casas = 1) => v.ToString("N" + casas, Pt);

    public static string Gerar(ResultadoInstalacao r)
    {
        var sb = new StringBuilder();
        var a = r.Alimentacao;
        sb.AppendLine($"# Memorial de cálculo elétrico — {r.Nome}");
        sb.AppendLine();
        sb.AppendLine($"> Protótipo do motor AutoEletrica. Resultado {(r.Atende ? "**ATENDE**" : "**NÃO ATENDE**")} às verificações abaixo. " +
                      "Não substitui a análise e a responsabilidade técnica do projetista. Norma de referência: ABNT NBR 5410:2004 (versão corrigida 2008).");
        sb.AppendLine();

        sb.AppendLine("## 1. Dados da alimentação");
        sb.AppendLine();
        sb.AppendLine("| Item | Valor | Referência |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| Tensões | {F(a.TensaoFaseNeutro, 0)} / {F(a.TensaoFaseFase, 0)} V, {a.Fases} fase(s) | 4.2.2.1 |");
        sb.AppendLine($"| Esquema de aterramento | {Esquema(a.Esquema)} | 4.2.2.2 |");
        sb.AppendLine($"| Icc presumida no ponto de entrega | {(a.IccPresumidaKA is { } icc ? F(icc, 1) + " kA" : "não informada")} | 4.2.3 |");
        sb.AppendLine($"| Alimentação | {(a.LinhaAerea ? "aérea" : "subterrânea")}, {a.DiasDeTrovoada} dias de trovoada/ano ({(a.DiasDeTrovoada > Nbr5410.DiasDeTrovoadaAQ2 ? "AQ2" : "AQ1")}){(a.TemSpda ? ", com SPDA (AQ3)" : "")} | 4.2.6, 5.4.2.1 |");
        sb.AppendLine($"| Temperatura ambiente | {F(a.TemperaturaAmbiente, 0)} °C | 6.2.5.3 |");
        sb.AppendLine($"| Uso | {(a.Habitacao ? "habitação (seção 9.5)" : "não residencial")} | 9.5 |");
        sb.AppendLine();

        sb.AppendLine("## 2. Quadros");
        sb.AppendLine();
        foreach (var q in r.Quadros)
        {
            sb.AppendLine($"### {q.Nome}");
            sb.AppendLine();
            sb.AppendLine("| Grandeza | Valor | Referência |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| Circuitos | {q.NumeroDeCircuitos} + {q.Reserva} de reserva | Tabela 59 |");
            sb.AppendLine($"| Potência instalada / de demanda | {F(q.PotenciaInstaladaVA, 0)} / {F(q.PotenciaDeDemandaVA, 0)} VA | 4.2.1 |");
            if (q.Demanda is { } dem)
            {
                foreach (var p in dem.Parcelas)
                    sb.AppendLine($"| Demanda: {FatoresDeDemanda.Nome(p.Categoria)} | {F(p.InstaladaVA, 0)} VA × {F(p.Fator, 2)} = {F(p.DemandaVA, 0)} VA ({p.Aparelhos} un.) | {p.Fonte} |");
                if (dem.DosQuadrosFilhosVA > 0) sb.AppendLine($"| Demanda: quadros alimentados | {F(dem.DosQuadrosFilhosVA, 0)} VA | 4.2.1.1 |");
                if (Math.Abs(dem.FatorDoQuadro - 1) > 1e-9) sb.AppendLine($"| Fator próprio do quadro | {F(dem.FatorDoQuadro, 2)} | projeto |");
            }
            sb.AppendLine($"| Corrente de demanda | {F(q.CorrenteDeDemanda, 1)} A | 4.2.1 |");
            sb.AppendLine($"| Carga por fase | {string.Join(", ", q.CargasPorFase.Select(f => $"{f.Fase}: {F(f.PotenciaVA, 0)} VA ({F(f.Corrente, 1)} A)"))}; desequilíbrio {F(q.DesequilibrioPercentual, 0)} % | 4.2.5.6 |");
            sb.AppendLine($"| Icc no barramento | {(q.IccNoBarramentoKA is { } iccQ ? F(iccQ, 2) + " kA" : "não avaliada")} | 5.3.5.1 |");
            if (q.DisjuntorGeral is { } geral) sb.AppendLine($"| Disjuntor geral | {geral} | 5.3.4, 6.3.4 |");
            if (q.Dps is { } dps)
                sb.AppendLine($"| DPS | classe {dps.Classe}, {dps.Quantidade} un., esquema {dps.Esquema}, Uc ≥ {F(dps.UcMinimo, 0)} V (comercial {F(dps.UcComercial, 0)} V), Up ≤ {F(dps.UpMaximo, 1)} kV, In ≥ {F(dps.InMinimoKA, 0)} kA{(dps.IimpMinimoKA is { } ii ? $", Iimp ≥ {F(ii, 1)} kA" : "")}, ligações {F(dps.SecaoDeLigacao, 0)} mm² — {q.MotivoDoDps} | 5.4.2.1.1, 6.3.5.2, Tabela 49 |");
            else if (q.MotivoDoDps is null && r.Quadros[0].Id == q.Id)
                sb.AppendLine("| DPS | não obrigatório pela 5.4.2.1.1 (avaliar pela NBR 5419-2) | 5.4.2.1.1 |");
            sb.AppendLine($"| Circuitos com DR | {(q.CircuitosComDR.Count == 0 ? "nenhum" : string.Join(", ", q.CircuitosComDR))} | 5.1.3.2.2 |");
            foreach (var g in q.GruposDR ?? [])
                sb.AppendLine($"| {g} | até {DispositivosDR.CircuitosPorDRRecomendados} circuitos por DR; In ≥ soma dos In do grupo | 6.3.3.2, IEC 61008-1 |");
            sb.AppendLine("| Seccionador geral, barramentos e identificação | disjuntor geral como seccionador; barramento para o In do geral; circuitos identificados e advertência fixada | 5.6.3, 6.5.4.9, 6.5.4.10 |");
            foreach (var av in q.Avisos) sb.AppendLine($"| Aviso | {av} | |");
            sb.AppendLine();
            sb.AppendLine("Quadro de cargas:");
            sb.AppendLine();
            sb.AppendLine(QuadroDeCargas.Tabela(r.Circuitos.Where(c => c.Quadro == q.Id)));
        }

        sb.AppendLine("## 3. Circuitos: os seis critérios de 6.2.6.1.2");
        sb.AppendLine();
        foreach (var c in r.Circuitos)
        {
            sb.AppendLine($"### Circuito {c.Numero} — {c.Descricao} ({c.Quadro})");
            sb.AppendLine();
            sb.AppendLine($"{F(c.PotenciaVA, 0)} VA, {Ligacao(c.Ligacao)}{(c.Fases.Length > 0 ? $" (fase {c.Fases})" : "")}, IB = **{F(c.CorrenteDeProjeto, 1)} A**; L = {F(c.Comprimento, 1)} m{(c.ComprimentoEstimado ? " (informado)" : " (pelos eletrodutos)")}; " +
                          $"{Isol(c.Isolacao)} {c.Material}, método {c.MetodoCritico}{(c.TrechoCritico is not null ? $" (trecho crítico {c.TrechoCritico})" : "")}, {c.CircuitosAgrupados} circuito(s) agrupado(s). " +
                          $"**{(c.CondutoresPorFase > 1 ? $"{c.CondutoresPorFase} × " : "")}Fase {F(c.SecaoFase, 1)} mm², neutro {F(c.SecaoNeutro, 1)} mm², PE {F(c.SecaoPE, 1)} mm², disjuntor {c.Disjuntor}.** " +
                          $"Critério que definiu a seção: {c.CriterioQueMandou}.");
            if (c.DR.Obrigatorio) sb.AppendLine($"DR obrigatório: {c.DR.Motivo}; IΔn {F(c.DR.CorrenteResidualMA, 0)} mA, tipo {c.DR.Tipo}, In ≥ {F(c.DR.In, 0)} A.");
            sb.AppendLine();
            sb.AppendLine("| Critério | Resultado | Valor | Limite | Item | Como foi calculado |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var p in c.Pareceres)
                sb.AppendLine($"| {p.Criterio} | {(p.Atende is null ? "não avaliado" : p.Atende.Value ? "atende" : "**não atende**")} | {p.Valor} | {p.Limite} | {p.Item} | {p.Explicacao} |");
            foreach (var av in c.Avisos) sb.AppendLine($"| aviso | | {av} | | | |");
            sb.AppendLine();
        }

        if (r.Trechos.Count > 0)
        {
            sb.AppendLine("## 4. Eletrodutos (6.2.11.1)");
            sb.AppendLine();
            sb.AppendLine("| Trecho | De → Para | L (m) | Curvas | Condutores | Área (mm²) | DN | Taxa | DN mínimo | Situação |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var t in r.Trechos)
                sb.AppendLine($"| {t.Id} | {t.De} → {t.Para} | {F(t.Comprimento, 1)} | {t.Curvas90} | {t.Condutores} | {F(t.AreaDosCondutores, 0)} | {(t.DnInformado is { } dn ? dn.ToString() : "—")} | " +
                              $"{(t.TaxaDeOcupacao is { } tx ? F(tx * 100, 0) + " %" : "—")} (≤ {F(t.TaxaMaxima * 100, 0)} %) | {(t.DnSugerido is { } s ? s.ToString() : "—")} | " +
                              $"{(t.Atende ? "atende" : "**" + string.Join("; ", t.Pareceres.Where(p => p.Reprovado).Select(p => p.Criterio)) + "**")} |");
            sb.AppendLine();
        }

        sb.AppendLine("## 5. Verificações");
        sb.AppendLine();
        if (r.Problemas.Count == 0) sb.AppendLine("Nenhum problema apontado.");
        else
        {
            sb.AppendLine("| Código | Gravidade | Mensagem | Item | Elementos |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var p in r.Problemas.OrderBy(p => p.Gravidade).ThenBy(p => p.Codigo))
                sb.AppendLine($"| {p.Codigo} | {p.Gravidade} | {p.Mensagem} | {p.Item} | {string.Join(", ", p.Elementos)} |");
        }
        sb.AppendLine();
        sb.AppendLine("## 6. Premissas do cálculo");
        sb.AppendLine();
        sb.AppendLine("- Capacidade de corrente pelas Tabelas 36 a 39, fatores de temperatura (Tabela 40) e de agrupamento em feixe (Tabela 42, linha 1) aplicados no pior trecho do traçado (6.2.5.8).");
        sb.AppendLine("- Disjuntores pela ABNT NBR NM 60898: In preferenciais, I2 = 1,45 In, atuação instantânea garantida no limite superior da faixa da curva (B 5 In, C 10 In, D 20 In).");
        sb.AppendLine($"- Queda de tensão com a resistência do condutor na temperatura de operação estimada (IEC 60364-5-52, Anexo G) e reatância de {Condutores.ReatanciaPorMetro * 1000:0.00} Ω/km.");
        sb.AppendLine($"- Curto-circuito pelo método das impedâncias: rede a montante pela Icc presumida com X/R = {Eletrotecnica.RelacaoXRDaRede:0}, fator de tensão {Eletrotecnica.FatorDeTensaoMaximo} (máximo) e {Eletrotecnica.FatorDeTensaoMinimo} (mínimo), resistências a quente ×{Eletrotecnica.FatorDeResistenciaAQuente} no curto mínimo.");
        sb.AppendLine("- Energia passante do disjuntor: classe 3 de limitação (NBR NM 60898, Anexo ZA) quando a combinação existe; quando o limite da classe passa de k²S², o critério fica \"não comprovado\" até o I²t de catálogo ser informado.");
        sb.AppendLine($"- Demanda dos quadros: {(a.AplicarFatoresDeDemanda ? "fatores por tipo de carga (" + FatoresDeDemanda.Fonte + ")" : "sem fatores de demanda (opção do projeto)")}; fases balanceadas por potência decrescente, respeitando as fases impostas.");
        sb.AppendLine("- Em eletroduto com seções fora de três valores sucessivos, o agrupamento usa 1/√n (6.2.5.5.5) numa segunda passada; sem seção única que atenda, dois condutores em paralelo por fase (6.2.5.7).");
        if (a.FolgaDeComprimentoPercentual > 0 || a.FolgaDeOcupacaoPercentual > 0 || a.PEComumNoConduto)
            sb.AppendLine($"- Opções do projeto: folga de comprimento {F(a.FolgaDeComprimentoPercentual, 0)} %, folga de ocupação {F(a.FolgaDeOcupacaoPercentual, 0)} %{(a.PEComumNoConduto ? ", um PE por eletroduto com a maior seção (6.4.3.1.5)" : "")}.");
        sb.AppendLine("- Diâmetros externos dos condutores isolados pela NBR NM 247-3 (Tabelas 1 e 3); diâmetros internos dos eletrodutos de catálogo (a NBR 15465 só fixa o externo).");
        sb.AppendLine($"- Gerado em {DateTime.Now:dd/MM/yyyy HH:mm} em {r.Duracao.TotalMilliseconds:0} ms.");
        return sb.ToString();
    }

    public static string Esquema(EsquemaAterramento e) => e switch
    {
        EsquemaAterramento.TNS => "TN-S",
        EsquemaAterramento.TNC => "TN-C",
        EsquemaAterramento.TNCS => "TN-C-S",
        EsquemaAterramento.TT => "TT",
        _ => "IT",
    };

    public static string Ligacao(Ligacao l) => l switch
    {
        Modelo.Ligacao.FN => "F+N+PE",
        Modelo.Ligacao.FF => "2F+PE",
        Modelo.Ligacao.FFN => "2F+N+PE",
        Modelo.Ligacao.FFF => "3F+PE",
        _ => "3F+N+PE",
    };

    public static string Isol(Isolacao i) => i switch
    {
        Isolacao.Pvc70 => "PVC 70 °C",
        Isolacao.Epr90 => "EPR 90 °C",
        _ => "XLPE 90 °C",
    };
}
