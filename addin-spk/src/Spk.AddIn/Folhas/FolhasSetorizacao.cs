namespace Spk.AddIn.Folhas;

/// <summary>
/// Setorização das VGAs, no padrão da NEPN-INC-009: uma planta por nível com a hachura colorida de cada setor, todas
/// na mesma escala, e a legenda das cores. A legenda é uma vista de desenho, porque a API não cria vista de legenda.
/// </summary>
static class FolhasSetorizacao
{
    public const string Numero = PranchaSpk.PrefixoNumero + "SETORES";

    public static FolhasGeradas Gerar(ContextoFolhas contexto)
    {
        var doc = contexto.Doc;
        var setores = contexto.Setores;
        if (setores.Count == 0)
            throw new InvalidOperationException("Nenhum setor foi lido. " + string.Join(" ", contexto.AvisosDosSetores));
        var avisos = new List<string>(contexto.AvisosDosSetores);
        var niveis = LeituraSetores.Niveis(doc).Where(n => setores.Any(s => s.Contornos.ContainsKey(n.Id))).ToList();
        if (niveis.Count == 0) throw new InvalidOperationException("Os bicos dos setores não estão em nenhum nível do projeto.");

        var folha = PranchaSpk.Recriar(doc, Numero, "Sprinklers — setorização das VGAs");
        doc.Regenerate();
        var util = PranchaSpk.Util(folha);
        var areaPlantas = util.Parte(0, 0, 0.78, 1);
        var areaLegenda = util.Parte(0.80, 0.40, 1, 1);
        var colunas = niveis.Count switch { 1 => 1, <= 4 => 2, _ => 3 };
        var linhas = (int)Math.Ceiling(niveis.Count / (double)colunas);

        var plantas = new List<(ViewPlan Vista, Regiao Celula, Caixa Recorte)>();
        for (var k = 0; k < niveis.Count; k++)
        {
            var nivel = niveis[k];
            if (LeituraSetores.CaixaNoNivel(setores, nivel) is not { } caixa) continue;
            var recorte = caixa.Expandir(VistasSpk.Pes(3.0));
            int c = k % colunas, l = k / colunas;
            var celula = areaPlantas.Parte(c / (double)colunas, 1 - (l + 1) / (double)linhas, (c + 1) / (double)colunas, 1 - l / (double)linhas);
            var planta = VistasSpk.Planta(doc, $"{PranchaSpk.PrefixoVista}Setorização — {PranchaSpk.NomeSeguro(nivel.Name)}", nivel, recorte,
                VistasSpk.TemplateSetorizacao, avisos);
            VistasSpk.EsconderRede(planta);
            plantas.Add((planta, celula, recorte));
        }

        if (plantas.Count == 0) throw new InvalidOperationException("Nenhum setor tem contorno num nível do projeto.");
        // Escala comum: a maior que alguma planta precise, como as três plantas 1:300 da NEPN-INC-009.
        var escala = plantas.Max(p => p.Celula.Parte(0, 0.08, 1, 1).Escala(p.Recorte.Max.X - p.Recorte.Min.X, p.Recorte.Max.Y - p.Recorte.Min.Y));
        foreach (var (vista, _, _) in plantas)
        {
            try { vista.Scale = escala; }
            catch (Autodesk.Revit.Exceptions.ApplicationException e) { avisos.Add($"Escala de {vista.Name}: {e.Message}"); }
            var nivel = vista.GenLevel;
            foreach (var setor in setores.Where(s => s.Contornos.ContainsKey(nivel.Id)))
                LeituraSetores.Desenhar(doc, vista, setor, nivel, cheio: false, setor.Nome, avisos);
        }

        foreach (var (vista, celula, _) in plantas)
            PranchaSpk.Colocar(doc, folha, vista, celula.Parte(0, 0.08, 1, 1).Centro, avisos);
        Legenda(doc, folha, areaLegenda, setores, avisos);

        var texto = $"{Numero}  {folha.Name}\n{plantas.Count} planta(s) 1:{escala}, {setores.Count} setor(es): " +
                    string.Join(", ", setores.Select(s => $"{s.Nome} ({s.Bicos.Count} bicos)")) + ".";
        if (avisos.Count > 0) texto += "\n\nAvisos:\n" + string.Join("\n", avisos.Distinct().Take(12).Select(a => "• " + a));
        return new FolhasGeradas(texto, [folha.Id]);
    }

    const double Linha = 9, AmostraL = 12, AmostraA = 6;

    /// <summary>
    /// Legenda das cores no alto da área: direto na folha, em coordenadas de papel. Se o Revit recusar região preenchida
    /// na folha, vai numa vista de desenho colocada no mesmo lugar (vista de legenda a API não cria).
    /// </summary>
    static void Legenda(Document doc, ViewSheet folha, Regiao area, IReadOnlyList<SetorLido> setores, IList<string> avisos)
    {
        const string nome = PranchaSpk.PrefixoVista + "Legenda de setores";
        VistasSpk.ApagarSeExistir(doc, nome);
        var naFolha = new SubTransaction(doc);
        naFolha.Start();
        try
        {
            DesenharLegenda(doc, folha, setores, new XYZ(area.X0, area.Y1 - VistasSpk.Pes(0.012), 0), 1);
            naFolha.Commit();
            return;
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            naFolha.RollBack();
            avisos.Add("Legenda na folha recusada, foi para uma vista de desenho: " + e.Message);
        }

        try
        {
            var vista = ViewDrafting.Create(doc, VistasSpk.Tipo(doc, ViewFamily.Drafting).Id);
            vista.Name = nome;
            try { vista.Scale = 10; }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { }
            DesenharLegenda(doc, vista, setores, XYZ.Zero, vista.Scale);
            var largura = VistasSpk.Pes(0.070);
            var altura = VistasSpk.Pes((setores.Count * Linha + 12) / 1000);
            PranchaSpk.Colocar(doc, folha, vista, new XYZ(area.X0 + largura / 2, area.Y1 - altura / 2, 0), avisos);
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            avisos.Add("Legenda: " + e.Message);
        }
    }

    /// <summary>Título e, por setor, uma amostra da hachura com o nome. Medidas em mm no papel, a partir do canto <paramref name="origem" />.</summary>
    static void DesenharLegenda(Document doc, View vista, IReadOnlyList<SetorLido> setores, XYZ origem, int escala)
    {
        XYZ P(double xMm, double yMm) => origem + new XYZ(VistasSpk.Pes(xMm * escala / 1000), VistasSpk.Pes(yMm * escala / 1000), 0);
        PranchaSpk.Nota(doc, vista, P(0, 8), "SETORIZAÇÃO DAS VGAs");
        for (var k = 0; k < setores.Count; k++)
        {
            var setor = setores[k];
            var y = -k * Linha;
            var tipo = PranchaSpk.TipoDeRegiao(doc, $"SPK Setor — {PranchaSpk.NomeSeguro(setor.Nome)}",
                PranchaSpk.Padrao(doc, "SPK setor", 4, Math.PI / 4), setor.Cor);
            PranchaSpk.RegiaoPreenchida(doc, vista, tipo,
                [new List<XYZ> { P(0, y - AmostraA), P(AmostraL, y - AmostraA), P(AmostraL, y), P(0, y) }], null);
            PranchaSpk.Nota(doc, vista, P(AmostraL + 3, y - 1), $"{setor.Nome} — {setor.Bicos.Count} chuveiros");
        }
    }
}
