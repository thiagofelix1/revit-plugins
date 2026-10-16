using System.Globalization;
using System.Text;
using Spk.Motor.Calculo;
using Spk.Motor.Normas;

namespace Spk.Motor.Relatorios;

/// <summary>Memorial de cálculo em Markdown, na ordem dos dados pedidos pela NBR 9.1 a 9.3.</summary>
public static class Memorial
{
    static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static string Gerar(RelatorioSpk rel)
    {
        var p = rel.Projeto;
        var r = rel.Resultado;
        var sb = new StringBuilder();

        sb.AppendLine($"# Memorial de cálculo hidráulico — {p.Nome}");
        sb.AppendLine();
        sb.AppendLine($"> Protótipo do motor SPK. Resultado {(rel.Atende ? "**ATENDE**" : "**NÃO ATENDE**")} às verificações abaixo. " +
                      "Não substitui a análise e a responsabilidade técnica do projetista.");
        sb.AppendLine();

        sb.AppendLine("## 1. Critérios de projeto");
        sb.AppendLine();
        sb.AppendLine("| Item | Valor | Referência |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| Classe de risco | {NomeClasse(p.Classe)} | NBR 10897 4 e 8.3 |");
        sb.AppendLine($"| Base de projeto | {p.Base.Descricao} | {p.Base.Referencia} |");
        if (p.Base is BaseDensidadeArea { Ajuste: { } aj })
        {
            sb.AppendLine($"| Área de operação original | {F(aj.AreaOriginal, 1)} m² | Figura 42 |");
            foreach (var a in aj.Ajustes)
                sb.AppendLine($"| Ajuste: {a.Descricao} | {(a.Percentual > 0 ? "+" : "")}{F(a.Percentual, 1)} % | {a.Item} |");
            sb.AppendLine($"| Área de operação final | {F(aj.AreaFinal, 1)} m² | 8.5.5.7 |");
        }
        if (rel.Busca is { } b)
        {
            sb.AppendLine($"| Área por chuveiro (mediana) | {F(b.AreaPorChuveiro, 2)} m² | 9.4.4.5.1 |");
            sb.AppendLine($"| Espaçamento no ramal (mediana) | {F(b.EspacamentoNoRamal, 2)} m | 7.7 |");
            sb.AppendLine($"| Chuveiros na área / por ramal | {b.ChuveirosNaArea} / {b.ChuveirosPorRamal} | 9.4.4.2, Figura 46 |");
        }
        sb.AppendLine($"| Demanda de hidrantes | {F(rel.VazaoHidrantes, 0)} L/min | 8.5.1, Tabela 29 |");
        sb.AppendLine($"| Duração do abastecimento | {F(rel.DuracaoMinutos, 0)} min | Tabela 29 |");
        sb.AppendLine("| Perda de carga | Hazen-Williams, pressão total (Pt) | 9.4.2.1.1, 9.4.4.9 |");
        sb.AppendLine();

        sb.AppendLine("## 2. Resumo");
        sb.AppendLine();
        sb.AppendLine("| Grandeza | Valor |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Pressão necessária na fonte (VGA) | **{F(r.PressaoNecessaria, 1)} kPa** ({F(r.PressaoNecessaria / Hidraulica.PesoEspecificoAgua, 1)} m.c.a.) |");
        sb.AppendLine($"| Vazão dos chuveiros | {F(r.VazaoChuveiros, 1)} L/min |");
        sb.AppendLine($"| Vazão na fonte (chuveiros + demandas na rede) | {F(r.VazaoNecessaria, 1)} L/min |");
        sb.AppendLine($"| Vazão total com hidrantes | **{F(rel.VazaoTotal, 1)} L/min** ({F(rel.VazaoTotal * 0.06, 1)} m³/h) |");
        sb.AppendLine($"| Reserva de incêndio | {F(rel.ReservaM3, 1)} m³ |");
        sb.AppendLine($"| Chuveiro mais desfavorável | {r.ChuveiroCritico} |");
        if (rel.PressaoDisponivel is { } pd)
            sb.AppendLine($"| Pressão disponível ({p.Abastecimento!.Descricao}) | {F(pd, 1)} kPa (folga {F(pd - r.PressaoNecessaria, 1)} kPa) |");
        if (rel.Busca is { } bb)
            sb.AppendLine($"| Posições da área de operação avaliadas | {bb.CandidatosAvaliados} |");
        sb.AppendLine();

        sb.AppendLine("## 3. Verificações");
        sb.AppendLine();
        sb.AppendLine("| Verificação | Item | Situação | Detalhe |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var v in rel.Verificacoes)
            sb.AppendLine($"| {v.Descricao} | {v.Item} | {(v.Atende ? "atende" : "**não atende**")} | {v.Detalhe} |");
        sb.AppendLine();

        if (rel.Avisos.Count > 0)
        {
            sb.AppendLine("### Avisos");
            sb.AppendLine();
            foreach (var a in rel.Avisos) sb.AppendLine($"- {a}");
            sb.AppendLine();
        }

        if (rel.Busca is { Ranking.Count: > 1 } busca)
        {
            sb.AppendLine("## 4. Áreas de operação mais exigentes");
            sb.AppendLine();
            var comFolga = busca.Ranking[0].FolgaAbastecimento.HasValue;
            sb.AppendLine(comFolga ? "| # | Pressão (kPa) | Vazão (L/min) | Folga (kPa) | Chuveiros |" : "| # | Pressão (kPa) | Vazão (L/min) | Chuveiros |");
            sb.AppendLine(comFolga ? "|---|---|---|---|---|" : "|---|---|---|---|");
            for (var i = 0; i < busca.Ranking.Count; i++)
            {
                var c = busca.Ranking[i];
                var lista = string.Join(", ", c.Chuveiros.Take(6)) + (c.Chuveiros.Count > 6 ? ", …" : "");
                sb.AppendLine(comFolga
                    ? $"| {i + 1} | {F(c.PressaoNecessaria, 1)} | {F(c.VazaoNecessaria, 1)} | {F(c.FolgaAbastecimento!.Value, 1)} | {lista} |"
                    : $"| {i + 1} | {F(c.PressaoNecessaria, 1)} | {F(c.VazaoNecessaria, 1)} | {lista} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 5. Chuveiros da área de operação");
        sb.AppendLine();
        sb.AppendLine("| Chuveiro | Ramal | K | Área (m²) | P (kPa) | Q (L/min) | Q mín. (L/min) | P mín. (kPa) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var c in r.Chuveiros.OrderBy(c => c.Pressao))
            sb.AppendLine($"| {c.Id}{(c.Id == r.ChuveiroCritico ? " *" : "")} | {c.Ramal} | {F(c.FatorK, 0)} | {F(c.AreaCobertura, 2)} | " +
                          $"{F(c.Pressao, 1)} | {F(c.Vazao, 1)} | {F(c.VazaoMinima, 1)} | {F(c.PressaoMinima, 0)} |");
        sb.AppendLine();
        sb.AppendLine("\\* chuveiro mais desfavorável (exatamente no limite).");
        sb.AppendLine();

        sb.AppendLine("## 6. Trechos (sentido do fluxo)");
        sb.AppendLine();
        sb.AppendLine("| Trecho | De → Para | Q (L/min) | DN | d int. (mm) | C | L (m) | Leq (m) | J (kPa/m) | Atrito (kPa) | Desnível (kPa) | P montante | P jusante | v (m/s) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var t in r.Trechos.OrderBy(t => -t.PressaoMontante))
            sb.AppendLine($"| {t.Id} | {t.Montante} → {t.Jusante} | {F(t.Vazao, 1)} | {(t.DiametroNominal?.ToString() ?? "–")} | {F(t.DiametroInterno, 2)} | " +
                          $"{F(t.C, 0)} | {F(t.Comprimento, 2)} | {F(t.ComprimentoEquivalente, 2)} | {F(t.PerdaUnitaria, 4)} | {F(t.PerdaAtrito, 2)} | " +
                          $"{F(t.PerdaElevacao, 2)} | {F(t.PressaoMontante, 1)} | {F(t.PressaoJusante, 1)} | {F(t.Velocidade, 2)} |");
        sb.AppendLine();
        sb.AppendLine("Trechos sem vazão (fora do caminho até a área de operação) foram omitidos.");

        return sb.ToString();
    }

    static string F(double v, int casas) => v.ToString("N" + casas, Br);

    static string NomeClasse(ClasseRisco c) => c switch
    {
        ClasseRisco.Leve => "Risco leve",
        ClasseRisco.Ordinario1 => "Risco ordinário grupo 1",
        ClasseRisco.Ordinario2 => "Risco ordinário grupo 2",
        ClasseRisco.Extraordinario1 => "Risco extraordinário grupo 1",
        ClasseRisco.Extraordinario2 => "Risco extraordinário grupo 2",
        _ => "Armazenamento (IT-24)",
    };
}
