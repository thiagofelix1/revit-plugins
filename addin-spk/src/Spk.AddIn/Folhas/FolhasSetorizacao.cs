namespace Spk.AddIn.Folhas;

/// <summary>
/// Setorização das VGAs, no padrão da NEPN-INC-009: uma planta por nível com a hachura colorida de cada setor, todas
/// na mesma escala, e a legenda das cores. A legenda fica dentro da primeira planta, à direita dos setores: o Revit não
/// aceita região preenchida na folha e a API não cria vista de legenda.
/// </summary>
static class FolhasSetorizacao
{
    public const string Numero = PranchaSpk.PrefixoNumero + "SETORES";

    const double Linha = 9, AmostraL = 12, AmostraA = 6;

    /// <summary>Largura que a legenda ocupa à direita dos setores, no papel.</summary>
    const double LarguraLegendaMm = 85;

    public static FolhasGeradas Gerar(ContextoFolhas contexto)
    {
        var doc = contexto.Doc;
        var setores = contexto.Setores;
        if (setores.Count == 0)
            throw new InvalidOperationException("Nenhum setor foi lido. " + string.Join(" ", contexto.AvisosDosSetores));
        var avisos = new List<string>(contexto.AvisosDosSetores);
        var niveis = LeituraSetores.Niveis(doc).Where(n => setores.Any(s => s.Contornos.ContainsKey(n.Id))).ToList();
        if (niveis.Count == 0) throw new InvalidOperationException("Os bicos dos setores não estão em nenhum nível do projeto.");

        // Rodadas anteriores punham a legenda numa vista de desenho, que não aparecia na folha.
        VistasSpk.ApagarSeExistir(doc, PranchaSpk.PrefixoVista + "Legenda de setores");

        var folha = PranchaSpk.Recriar(doc, Numero, "Sprinklers — setorização das VGAs");
        doc.Regenerate();
        var util = PranchaSpk.Util(folha);
        var colunas = niveis.Count switch { 1 => 1, <= 4 => 2, _ => 3 };
        var linhas = (int)Math.Ceiling(niveis.Count / (double)colunas);

        var plantas = new List<(ViewPlan Vista, Regiao Celula, Caixa Recorte)>();
        for (var k = 0; k < niveis.Count; k++)
        {
            var nivel = niveis[k];
            if (LeituraSetores.CaixaNoNivel(setores, nivel) is not { } caixa) continue;
            var recorte = caixa.Expandir(VistasSpk.Pes(3.0));
            int c = k % colunas, l = k / colunas;
            var celula = util.Parte(c / (double)colunas, 1 - (l + 1) / (double)linhas, (c + 1) / (double)colunas, 1 - l / (double)linhas)
                .Parte(0, 0.08, 1, 1);
            var planta = VistasSpk.Planta(doc, $"{PranchaSpk.PrefixoVista}Setorização — {PranchaSpk.NomeSeguro(nivel.Name)}", nivel, recorte,
                VistasSpk.TemplateSetorizacao, avisos);
            VistasSpk.EsconderRede(planta);
            plantas.Add((planta, celula, recorte));
        }

        if (plantas.Count == 0) throw new InvalidOperationException("Nenhum setor tem contorno num nível do projeto.");

        // Escala comum: a maior que alguma planta precise, como as três plantas 1:300 da NEPN-INC-009. A primeira planta
        // reserva a largura da legenda.
        var escala = plantas.Select((p, i) =>
        {
            var celula = i == 0 ? p.Celula with { X1 = p.Celula.X1 - VistasSpk.Pes(LarguraLegendaMm / 1000) } : p.Celula;
            return celula.EscalaDePlanta(p.Recorte.Max.X - p.Recorte.Min.X, p.Recorte.Max.Y - p.Recorte.Min.Y);
        }).Max();

        foreach (var (vista, _, _) in plantas)
        {
            try { vista.Scale = escala; }
            catch (Autodesk.Revit.Exceptions.ApplicationException e) { avisos.Add($"Escala de {vista.Name}: {e.Message}"); }
            var nivel = vista.GenLevel;
            foreach (var setor in setores.Where(s => s.Contornos.ContainsKey(nivel.Id)))
                LeituraSetores.Desenhar(doc, vista, setor, nivel, cheio: false, setor.Nome, avisos);
        }

        Legenda(doc, plantas[0].Vista, plantas[0].Recorte, setores, avisos);

        foreach (var (vista, celula, _) in plantas)
            if (PranchaSpk.Colocar(doc, folha, vista, celula.Centro, avisos) is { } viewport)
                PranchaSpk.Centrar(doc, viewport, celula);

        var texto = $"{Numero}  {folha.Name}\n{plantas.Count} planta(s) 1:{escala}, {setores.Count} setor(es): " +
                    string.Join(", ", setores.Select(s => $"{s.Nome} ({s.Bicos.Count} bicos)")) + ".";
        if (avisos.Count > 0) texto += "\n\nAvisos:\n" + string.Join("\n", avisos.Distinct().Take(12).Select(a => "• " + a));
        return new FolhasGeradas(texto, [folha.Id]);
    }

    /// <summary>
    /// Título e, por setor, uma amostra da hachura com o nome, à direita do recorte da planta. O recorte cresce para a
    /// direita até caber a legenda. Medidas em mm no papel, convertidas pela escala da vista.
    /// </summary>
    static void Legenda(Document doc, ViewPlan planta, Caixa recorte, IReadOnlyList<SetorLido> setores, IList<string> avisos)
    {
        var escala = planta.Scale;
        double M(double mm) => VistasSpk.Pes(mm * escala / 1000);
        var origem = new XYZ(recorte.Max.X + M(8), recorte.Max.Y - M(10), 0);
        XYZ P(double xMm, double yMm) => origem + new XYZ(M(xMm), M(yMm), 0);

        PranchaSpk.Tentar(avisos, "Legenda", () =>
        {
            // Fundo branco: à direita dos setores pode haver prédio (arquitetura vinculada), e a legenda ficava ilegível.
            var fundo = -setores.Count * Linha - 3;
            PranchaSpk.Mascara(doc, planta, [P(-3, 13), P(LarguraLegendaMm - 6, 13), P(LarguraLegendaMm - 6, fundo), P(-3, fundo)], planta.GenLevel);
            PranchaSpk.Nota(doc, planta, P(0, 6), "SETORIZAÇÃO DAS VGAs");
            for (var k = 0; k < setores.Count; k++)
            {
                var setor = setores[k];
                var y = -k * Linha;
                var tipo = PranchaSpk.TipoDeRegiao(doc, $"SPK Setor — {PranchaSpk.NomeSeguro(setor.Nome)}",
                    PranchaSpk.Padrao(doc, "SPK setor", 4, Math.PI / 4), setor.Cor);
                PranchaSpk.RegiaoPreenchida(doc, planta, tipo,
                    [new List<XYZ> { P(0, y - AmostraA), P(AmostraL, y - AmostraA), P(AmostraL, y), P(0, y) }], planta.GenLevel);
                PranchaSpk.Nota(doc, planta, P(AmostraL + 3, y - 1), $"{setor.Nome} — {setor.Bicos.Count} chuveiros");
            }
        });

        var baixo = Math.Min(recorte.Min.Y, origem.Y - M(setores.Count * Linha + 6));
        PranchaSpk.Tentar(avisos, "Recorte para a legenda", () => VistasSpk.Recortar(planta,
            new Caixa(new XYZ(recorte.Min.X, baixo, recorte.Min.Z), new XYZ(recorte.Max.X + M(LarguraLegendaMm), recorte.Max.Y, recorte.Max.Z))));
    }
}
