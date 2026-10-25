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
        var util = PranchaSpk.Area(folha).Parte(0.03, 0.05, 0.97, 0.97);
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

        var (legenda, largura, altura) = Legenda(doc, setores, avisos);
        foreach (var (vista, celula, _) in plantas)
            PranchaSpk.Colocar(doc, folha, vista, celula.Parte(0, 0.08, 1, 1).Centro, avisos);
        if (legenda is not null)
            PranchaSpk.Colocar(doc, folha, legenda,
                new XYZ(areaLegenda.X0 + largura / 2, areaLegenda.Y1 - altura / 2, 0), avisos);

        var texto = $"{Numero}  {folha.Name}\n{plantas.Count} planta(s) 1:{escala}, {setores.Count} setor(es): " +
                    string.Join(", ", setores.Select(s => $"{s.Nome} ({s.Bicos.Count} bicos)")) + ".";
        if (avisos.Count > 0) texto += "\n\nAvisos:\n" + string.Join("\n", avisos.Distinct().Take(12).Select(a => "• " + a));
        return new FolhasGeradas(texto, [folha.Id]);
    }

    /// <summary>
    /// Vista de desenho com uma amostra da hachura de cada setor e o nome. Devolve também o tamanho no papel, em pés, para
    /// posicionar na folha.
    /// </summary>
    static (ViewDrafting? Vista, double Largura, double Altura) Legenda(Document doc, IReadOnlyList<SetorLido> setores, IList<string> avisos)
    {
        const string nome = PranchaSpk.PrefixoVista + "Legenda de setores";
        try
        {
            VistasSpk.ApagarSeExistir(doc, nome);
            var vista = ViewDrafting.Create(doc, VistasSpk.Tipo(doc, ViewFamily.Drafting).Id);
            vista.Name = nome;
            try { vista.Scale = 10; }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { }
            var escala = vista.Scale;
            double M(double mm) => VistasSpk.Pes(mm * escala / 1000);

            const double linha = 9, amostraL = 12, amostraA = 6;
            PranchaSpk.Nota(doc, vista, new XYZ(0, M(8), 0), "SETORIZAÇÃO DAS VGAs");
            for (var k = 0; k < setores.Count; k++)
            {
                var setor = setores[k];
                var y = -k * linha;
                var amostra = new List<XYZ>
                {
                    new(0, M(y - amostraA), 0), new(M(amostraL), M(y - amostraA), 0), new(M(amostraL), M(y), 0), new(0, M(y), 0),
                };
                var tipo = PranchaSpk.TipoDeRegiao(doc, $"SPK Setor — {PranchaSpk.NomeSeguro(setor.Nome)}",
                    PranchaSpk.Padrao(doc, "SPK setor", 4, Math.PI / 4), setor.Cor);
                PranchaSpk.RegiaoPreenchida(doc, vista, tipo, [amostra], null);
                PranchaSpk.Nota(doc, vista, new XYZ(M(amostraL + 3), M(y - 1), 0), $"{setor.Nome} — {setor.Bicos.Count} chuveiros");
            }
            var largura = VistasSpk.Pes(0.070);
            var altura = VistasSpk.Pes((setores.Count * linha + 12) / 1000);
            return (vista, largura, altura);
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            avisos.Add("Legenda: " + e.Message);
            return (null, 0, 0);
        }
    }
}
