using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Spk.Motor.Calculo;
using Spk.Motor.Normas;
using static Spk.Motor.Relatorios.Formatacao;

namespace Spk.Motor.Relatorios;

/// <summary>
///     Mesmo conteúdo do <see cref="Memorial"/>, em .docx nativo — OpenXML puro, sem precisar de Word ou
///     LibreOffice instalados em quem roda (inclusive dentro do processo do Revit). Pra PDF, por enquanto,
///     o caminho é abrir este .docx e "Salvar como PDF".
/// </summary>
public static class MemorialDocx
{
    const string CorTitulo = "2F5496";
    const string CorFundoCabecalho = "D9E2F3";
    const string CorAtende = "2E7D32";
    const string CorNaoAtende = "C62828";

    public static byte[] Gerar(RelatorioSpk rel)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            EscreverConteudo(body, rel);

            body.AppendChild(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 850, Right = 850, Bottom = 850, Left = 850 }));
        }

        return stream.ToArray();
    }

    static void EscreverConteudo(Body body, RelatorioSpk rel)
    {
        var p = rel.Projeto;
        var r = rel.Resultado;

        body.AppendChild(Titulo($"Memorial de cálculo hidráulico — {p.Nome}"));
        body.AppendChild(Paragrafo(
            $"Protótipo do motor SPK. Resultado {(rel.Atende ? "ATENDE" : "NÃO ATENDE")} às verificações abaixo. " +
            "Não substitui a análise e a responsabilidade técnica do projetista.",
            negrito: true, cor: rel.Atende ? CorAtende : CorNaoAtende));

        body.AppendChild(Subtitulo("1. Critérios de projeto"));
        var criterios = new List<string[]>
        {
            new[] { "Classe de risco", NomeClasse(p.Classe), "NBR 10897 4 e 8.3" },
            new[] { "Base de projeto", p.Base.Descricao, p.Base.Referencia },
        };
        if (p.Base is BaseDensidadeArea { Ajuste: { } aj })
        {
            criterios.Add(["Área de operação original", $"{F(aj.AreaOriginal, 1)} m²", "Figura 42"]);
            foreach (var a in aj.Ajustes)
                criterios.Add([$"Ajuste: {a.Descricao}", $"{(a.Percentual > 0 ? "+" : "")}{F(a.Percentual, 1)} %", a.Item]);
            criterios.Add(["Área de operação final", $"{F(aj.AreaFinal, 1)} m²", "8.5.5.7"]);
        }
        if (rel.Busca is { } b)
        {
            criterios.Add(["Área por chuveiro (mediana)", $"{F(b.AreaPorChuveiro, 2)} m²", "9.4.4.5.1"]);
            criterios.Add(["Espaçamento no ramal (mediana)", $"{F(b.EspacamentoNoRamal, 2)} m", "7.7"]);
            criterios.Add(["Chuveiros na área / por ramal", $"{b.ChuveirosNaArea} / {b.ChuveirosPorRamal}", "9.4.4.2, Figura 46"]);
        }
        criterios.Add(["Demanda de hidrantes", $"{F(rel.VazaoHidrantes, 0)} L/min", "8.5.1, Tabela 29"]);
        criterios.Add(["Duração do abastecimento", $"{F(rel.DuracaoMinutos, 0)} min", "Tabela 29"]);
        criterios.Add(["Perda de carga", "Hazen-Williams, pressão total (Pt)", "9.4.2.1.1, 9.4.4.9"]);
        body.AppendChild(Tabela(["Item", "Valor", "Referência"], criterios));

        body.AppendChild(Subtitulo("2. Resumo"));
        var resumo = new List<string[]>
        {
            new[] { "Pressão necessária na fonte (VGA)", $"{F(r.PressaoNecessaria, 1)} kPa ({F(r.PressaoNecessaria / Hidraulica.PesoEspecificoAgua, 1)} m.c.a.)" },
            new[] { "Vazão dos chuveiros", $"{F(r.VazaoChuveiros, 1)} L/min" },
            new[] { "Vazão na fonte (chuveiros + demandas na rede)", $"{F(r.VazaoNecessaria, 1)} L/min" },
            new[] { "Vazão total com hidrantes", $"{F(rel.VazaoTotal, 1)} L/min ({F(rel.VazaoTotal * 0.06, 1)} m³/h)" },
            new[] { "Reserva de incêndio", $"{F(rel.ReservaM3, 1)} m³" },
            new[] { "Chuveiro mais desfavorável", r.ChuveiroCritico },
        };
        if (rel.PressaoDisponivel is { } pd)
            resumo.Add([$"Pressão disponível ({p.Abastecimento!.Descricao})", $"{F(pd, 1)} kPa (folga {F(pd - r.PressaoNecessaria, 1)} kPa)"]);
        if (rel.Busca is { } bb)
            resumo.Add(["Posições da área de operação avaliadas", bb.CandidatosAvaliados.ToString()]);
        body.AppendChild(Tabela(["Grandeza", "Valor"], resumo));

        body.AppendChild(Subtitulo("3. Verificações"));
        body.AppendChild(Tabela(["Verificação", "Item", "Situação", "Detalhe"],
            rel.Verificacoes.Select(v => new[] { v.Descricao, v.Item, v.Atende ? "Atende" : "NÃO ATENDE", v.Detalhe })));

        if (rel.Avisos.Count > 0)
        {
            body.AppendChild(Subtitulo("Avisos"));
            foreach (var a in rel.Avisos) body.AppendChild(Paragrafo("• " + a));
        }

        if (rel.Busca is { Ranking.Count: > 1 } busca)
        {
            body.AppendChild(Subtitulo("4. Áreas de operação mais exigentes"));
            var comFolga = busca.Ranking[0].FolgaAbastecimento.HasValue;
            var cabecalho = comFolga
                ? new[] { "#", "Pressão (kPa)", "Vazão (L/min)", "Folga (kPa)", "Chuveiros" }
                : new[] { "#", "Pressão (kPa)", "Vazão (L/min)", "Chuveiros" };
            var linhas = new List<string[]>();
            for (var i = 0; i < busca.Ranking.Count; i++)
            {
                var c = busca.Ranking[i];
                var lista = string.Join(", ", c.Chuveiros.Take(6)) + (c.Chuveiros.Count > 6 ? ", …" : "");
                linhas.Add(comFolga
                    ? [(i + 1).ToString(), F(c.PressaoNecessaria, 1), F(c.VazaoNecessaria, 1), F(c.FolgaAbastecimento!.Value, 1), lista]
                    : [(i + 1).ToString(), F(c.PressaoNecessaria, 1), F(c.VazaoNecessaria, 1), lista]);
            }
            body.AppendChild(Tabela(cabecalho, linhas));
        }

        body.AppendChild(Subtitulo("5. Chuveiros da área de operação"));
        body.AppendChild(Tabela(["Chuveiro", "Ramal", "K", "Área (m²)", "P (kPa)", "Q (L/min)", "Q mín. (L/min)", "P mín. (kPa)"],
            r.Chuveiros.OrderBy(c => c.Pressao).Select(c => new[]
            {
                c.Id + (c.Id == r.ChuveiroCritico ? " *" : ""), c.Ramal, F(c.FatorK, 0), F(c.AreaCobertura, 2),
                F(c.Pressao, 1), F(c.Vazao, 1), F(c.VazaoMinima, 1), F(c.PressaoMinima, 0),
            })));
        body.AppendChild(Paragrafo("* chuveiro mais desfavorável (exatamente no limite).", italico: true));

        body.AppendChild(Subtitulo("6. Trechos (sentido do fluxo)"));
        body.AppendChild(Tabela(
            ["Trecho", "De → Para", "Q (L/min)", "DN", "d int. (mm)", "C", "L (m)", "Leq (m)", "J (kPa/m)", "Atrito (kPa)", "Desnível (kPa)", "P montante", "P jusante", "v (m/s)"],
            r.Trechos.OrderBy(t => -t.PressaoMontante).Select(t => new[]
            {
                t.Id, $"{t.Montante} → {t.Jusante}", F(t.Vazao, 1), t.DiametroNominal?.ToString() ?? "–", F(t.DiametroInterno, 2),
                F(t.C, 0), F(t.Comprimento, 2), F(t.ComprimentoEquivalente, 2), F(t.PerdaUnitaria, 4), F(t.PerdaAtrito, 2),
                F(t.PerdaElevacao, 2), F(t.PressaoMontante, 1), F(t.PressaoJusante, 1), F(t.Velocidade, 2),
            }), fonteMenor: true));
        body.AppendChild(Paragrafo("Trechos sem vazão (fora do caminho até a área de operação) foram omitidos.", italico: true));
    }

    static Paragraph Titulo(string texto) => new(
        new ParagraphProperties(new SpacingBetweenLines { After = "200" }),
        new Run(new RunProperties(new Bold(), new FontSize { Val = "40" }, new Color { Val = CorTitulo }), new Text(texto)));

    static Paragraph Subtitulo(string texto) => new(
        new ParagraphProperties(new SpacingBetweenLines { Before = "300", After = "120" }),
        new Run(new RunProperties(new Bold(), new FontSize { Val = "28" }, new Color { Val = CorTitulo }), new Text(texto)));

    static Paragraph Paragrafo(string texto, bool negrito = false, bool italico = false, string? cor = null)
    {
        var props = new List<OpenXmlElement>();
        if (negrito) props.Add(new Bold());
        if (italico) props.Add(new Italic());
        if (cor != null) props.Add(new Color { Val = cor });
        return new Paragraph(new Run(new RunProperties(props.ToArray()), new Text(texto)));
    }

    static Table Tabela(IReadOnlyList<string> cabecalhos, IEnumerable<string[]> linhas, bool fonteMenor = false)
    {
        var tabela = new Table(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }));

        tabela.AppendChild(LinhaTabela(cabecalhos, cabecalho: true, fonteMenor));
        foreach (var linha in linhas) tabela.AppendChild(LinhaTabela(linha, cabecalho: false, fonteMenor));
        return tabela;
    }

    static TableRow LinhaTabela(IEnumerable<string> celulas, bool cabecalho, bool fonteMenor)
    {
        var linha = new TableRow();
        foreach (var texto in celulas)
        {
            var runProps = new List<OpenXmlElement> { new FontSize { Val = fonteMenor ? "16" : "20" } };
            if (cabecalho) runProps.Add(new Bold());

            var celula = new TableCell(
                new TableCellProperties(new Shading { Fill = cabecalho ? CorFundoCabecalho : "FFFFFF", Val = ShadingPatternValues.Clear }),
                new Paragraph(new Run(new RunProperties(runProps.ToArray()), new Text(texto))));
            linha.AppendChild(celula);
        }
        return linha;
    }
}
