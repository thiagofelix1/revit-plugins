namespace Spk.AddIn.Folhas;

/// <summary>
/// Quadros das folhas, como os da NEPN-INC-013 (bombas, legenda): linhas e textos numa vista de desenho em escala 1:1,
/// colocada na folha. Vista de desenho e não tabela do Revit porque o conteúdo vem do cálculo, não de parâmetros.
/// </summary>
static class QuadrosSpk
{
    const double AlturaLinhaMm = 6;
    const double MargemMm = 1.5;

    /// <summary>Tipos de chuveiro da rede: K, área de cobertura e quantidade.</summary>
    public static ViewDrafting? Chuveiros(Document doc, IList<string> avisos, int escala = 1)
    {
        IReadOnlyList<LeitorRede.BicoLido> bicos;
        try
        {
            bicos = BicosDoModelo.Ler(doc).Bicos;
        }
        catch (InvalidOperationException e)
        {
            avisos.Add("Quadro de chuveiros: " + e.Message);
            return null;
        }
        var linhas = bicos
            .GroupBy(b => (Tipo: b.Instancia.Symbol?.Name ?? "?", b.FatorK, b.AreaCobertura))
            .OrderByDescending(g => g.Count())
            .Select(g => (IReadOnlyList<string>)
            [
                Curto(g.Key.Tipo, 30),
                g.Key.FatorK.ToString("0.#", Entrada.Cultura),
                g.Key.AreaCobertura.ToString("0.##", Entrada.Cultura),
                g.Count().ToString(Entrada.Cultura),
            ])
            .ToList();
        return Criar(doc, $"{PranchaSpk.PrefixoVista}Quadro de chuveiros", "QUADRO DE CHUVEIROS",
            ["TIPO", "K (L/min/bar½)", "ÁREA (m²)", "QUANT."], linhas, [62, 30, 22, 18], escala);
    }

    /// <summary>Demanda do último cálculo na entrada do setor: o que a bomba e a reserva precisam atender.</summary>
    public static ViewDrafting? Demanda(Document doc, IList<string> avisos, int escala = 1)
    {
        if (ResultadoGravado.Ler(doc) is not { } r)
        {
            avisos.Add("Quadro de demanda: rode Calcular antes.");
            return null;
        }
        string Num(double v, string formato) => v.ToString(formato, Entrada.Cultura);
        var linhas = new List<IReadOnlyList<string>>
        {
            new[] { "Setor calculado", r.Setor },
            new[] { "Vazão dos chuveiros", $"{Num(r.VazaoChuveirosLmin, "0")} L/min" },
        };
        if (r.DuracaoMin > 0)
            linhas.Add(["Vazão dos hidrantes", $"{Num(r.VazaoHidrantesLmin, "0")} L/min"]);
        linhas.Add(["Vazão total", $"{Num(r.VazaoTotalLmin, "0")} L/min"]);
        linhas.Add(["Pressão na entrada", $"{Num(r.PressaoNecessariaKPa, "0.0")} kPa ({Num(r.PressaoNecessariaKPa / 9.80665, "0.0")} mca)"]);
        if (r.DuracaoMin > 0)
        {
            linhas.Add(["Duração", $"{Num(r.DuracaoMin, "0")} min"]);
            linhas.Add(["Reserva de incêndio", $"{Num(r.ReservaM3, "0.0")} m³"]);
        }
        else
        {
            avisos.Add("Quadro de demanda sem hidrantes e reserva: o cálculo gravado é de uma versão anterior. Rode Calcular de novo.");
        }
        return Criar(doc, $"{PranchaSpk.PrefixoVista}Quadro de demanda", $"DEMANDA NA ENTRADA DO SETOR — {(r.Atende ? "ATENDE" : "NÃO ATENDE")}",
            ["ITEM", "VALOR"], linhas, [50, 52], escala);
    }

    /// <summary>
    /// Quadro com título, cabeçalho e linhas, larguras das colunas em mm no papel. Recria a vista se já existir.
    /// A origem é o canto de cima à esquerda do quadro; as linhas descem. A vista fica na escala da folha (o desenho
    /// cresce na mesma proporção, o tamanho no papel não muda): o Revit conta as vistas de desenho na escala do carimbo,
    /// e um quadro em 1:1 punha "1:1" na folha do isométrico.
    /// </summary>
    public static ViewDrafting Criar(Document doc, string nome, string titulo, IReadOnlyList<string> cabecalho,
        IReadOnlyList<IReadOnlyList<string>> linhas, IReadOnlyList<double> largurasMm, int escala = 1)
    {
        VistasSpk.ApagarSeExistir(doc, nome);
        var vista = ViewDrafting.Create(doc, VistasSpk.Tipo(doc, ViewFamily.Drafting).Id);
        vista.Name = nome;
        vista.Scale = escala;

        XYZ P(double xMm, double yMm) => new(VistasSpk.Pes(xMm * escala / 1000), VistasSpk.Pes(yMm * escala / 1000), 0);
        var largura = largurasMm.Sum();
        var todas = new List<IReadOnlyList<string>> { cabecalho };
        todas.AddRange(linhas);
        var altura = todas.Count * AlturaLinhaMm;

        void Linha(double x0, double y0, double x1, double y1) =>
            doc.Create.NewDetailCurve(vista, Line.CreateBound(P(x0, y0), P(x1, y1)));

        for (var i = 0; i <= todas.Count; i++) Linha(0, -i * AlturaLinhaMm, largura, -i * AlturaLinhaMm);
        var x = 0.0;
        Linha(0, 0, 0, -altura);
        foreach (var w in largurasMm)
        {
            x += w;
            Linha(x, 0, x, -altura);
        }

        PranchaSpk.Nota(doc, vista, P(0, AlturaLinhaMm), titulo);
        for (var i = 0; i < todas.Count; i++)
        {
            x = 0;
            for (var c = 0; c < largurasMm.Count; c++)
            {
                var texto = c < todas[i].Count ? todas[i][c] : "";
                PranchaSpk.Nota(doc, vista, P(x + MargemMm, -i * AlturaLinhaMm - MargemMm), texto);
                x += largurasMm[c];
            }
        }
        return vista;
    }

    static string Curto(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..(maximo - 1)] + "…";
}
