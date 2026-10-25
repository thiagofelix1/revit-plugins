namespace Spk.AddIn.Folhas;

/// <summary>Folhas, viewports e notas. Coordenadas de folha em pés, como o Revit.</summary>
static class PranchaSpk
{
    /// <summary>A1 deitado, para quando o projeto não tem carimbo.</summary>
    static readonly Regiao A1 = new(0, 0, VistasSpk.Pes(0.841), VistasSpk.Pes(0.594));

    public const string PrefixoNumero = "SPK-";
    public const string PrefixoVista = "SPK — ";

    /// <summary>Apaga a folha com este número, se existir, e cria outra com o carimbo do projeto.</summary>
    public static ViewSheet Recriar(Document doc, string numero, string nome)
    {
        var ids = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
            .Where(s => s.SheetNumber == numero).Select(s => s.Id).ToList();
        if (ids.Count > 0) doc.Delete(ids);

        var folha = ViewSheet.Create(doc, Carimbo(doc));
        folha.SheetNumber = numero;
        folha.Name = nome;
        return folha;
    }

    public static bool TemCarimbo(Document doc) => Carimbo(doc) != ElementId.InvalidElementId;

    /// <summary>O carimbo do padrão do escritório; sem ele, o primeiro carregado no projeto.</summary>
    static ElementId Carimbo(Document doc)
    {
        var doPadrao = PadraoEscritorio.Tipo(doc, BuiltInCategory.OST_TitleBlocks, PadraoEscritorio.Carregar(doc).Carimbo);
        return doPadrao != ElementId.InvalidElementId ? doPadrao : PrimeiroCarimbo(doc);
    }

    static ElementId PrimeiroCarimbo(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsElementType()
            .ToElementIds()
            .FirstOrDefault()
        ?? ElementId.InvalidElementId;

    /// <summary>A área útil da folha. Chamar depois de Regenerate, para o contorno do carimbo já valer.</summary>
    public static Regiao Area(ViewSheet folha)
    {
        var contorno = folha.Outline;
        var largura = contorno.Max.U - contorno.Min.U;
        var altura = contorno.Max.V - contorno.Min.V;
        if (largura < 0.3 || altura < 0.3) return A1;
        return new Regiao(contorno.Min.U, contorno.Min.V, contorno.Max.U, contorno.Max.V);
    }

    /// <summary>
    /// A parte da folha onde o plugin desenha: tira uma margem e a coluna da direita (16%), onde fica o carimbo do Revit
    /// e o da Nova Era. Num carimbo só no canto de baixo, sobra uma faixa livre, mas nada fica por cima dele.
    /// </summary>
    public static Regiao Util(ViewSheet folha) => Area(folha).Parte(0.03, 0.05, 0.84, 0.97);

    /// <summary>A etiqueta de tubo do padrão do escritório; sem ela, a padrão do projeto; sem esta, qualquer uma carregada.</summary>
    public static ElementId EtiquetaDeTubo(Document doc)
    {
        var doPadrao = PadraoEscritorio.Tipo(doc, BuiltInCategory.OST_PipeTags, PadraoEscritorio.Carregar(doc).EtiquetaTubo);
        if (doPadrao != ElementId.InvalidElementId) return doPadrao;
        var padrao = doc.GetDefaultFamilyTypeId(new ElementId(BuiltInCategory.OST_PipeTags));
        if (padrao != ElementId.InvalidElementId) return padrao;
        return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeTags).WhereElementIsElementType().FirstElementId()
               ?? ElementId.InvalidElementId;
    }

    /// <summary>
    /// As legendas do padrão do escritório empilhadas de cima para baixo na região, alinhadas à esquerda. Vista de legenda
    /// pode estar em várias folhas. Devolve quantas couberam.
    /// </summary>
    public static int ColocarLegendas(Document doc, ViewSheet folha, Regiao regiao, IList<string> avisos)
    {
        var topo = regiao.Y1;
        var colocadas = 0;
        foreach (var legenda in PadraoEscritorio.Carregar(doc).VistasDeLegenda(doc))
        {
            try
            {
                var viewport = Viewport.Create(doc, folha.Id, legenda.Id, regiao.Centro);
                doc.Regenerate();
                var caixa = viewport.GetBoxOutline();
                double largura = caixa.MaximumPoint.X - caixa.MinimumPoint.X, altura = caixa.MaximumPoint.Y - caixa.MinimumPoint.Y;
                if (topo - altura < regiao.Y0 || largura > regiao.Largura * 1.05)
                {
                    doc.Delete(viewport.Id);
                    avisos.Add($"A legenda {legenda.Name} não coube em {folha.SheetNumber}.");
                    continue;
                }
                viewport.SetBoxCenter(new XYZ(regiao.X0 + largura / 2, topo - altura / 2, 0));
                topo -= altura + VistasSpk.Pes(0.005);
                colocadas++;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException e)
            {
                avisos.Add($"Legenda {legenda.Name}: {e.Message}");
            }
        }
        return colocadas;
    }

    public static Viewport? Colocar(Document doc, ViewSheet folha, View vista, XYZ centro, IList<string> avisos)
    {
        if (!Viewport.CanAddViewToSheet(doc, folha.Id, vista.Id))
        {
            avisos.Add($"A vista {vista.Name} já está em outra folha e não entrou em {folha.SheetNumber}.");
            return null;
        }
        return Viewport.Create(doc, folha.Id, vista.Id, centro);
    }

    /// <summary>
    /// Nota de texto. Sem <paramref name="largura" /> a nota não quebra linha. A largura é no papel, em pés; zero fica abaixo
    /// do mínimo interno e o Revit recusa.
    /// </summary>
    public static TextNote? Nota(Document doc, View vista, XYZ posicao, string texto, double? largura = null, double rotacao = 0)
    {
        var tipo = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        if (tipo == ElementId.InvalidElementId || string.IsNullOrWhiteSpace(texto)) return null;
        var quebras = texto.Trim().Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r");
        var opcoes = new TextNoteOptions(tipo) { HorizontalAlignment = HorizontalTextAlignment.Left, Rotation = rotacao };
        return largura is { } l
            ? TextNote.Create(doc, vista.Id, posicao, l, quebras, opcoes)
            : TextNote.Create(doc, vista.Id, posicao, quebras, opcoes);
    }

    public static ElementId Solido(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
            .FirstOrDefault(p => p.GetFillPattern()?.IsSolidFill == true)?.Id
        ?? ElementId.InvalidElementId;

    public static OverrideGraphicSettings Pintura(Document doc, Color cor, int espessura, bool preencher)
    {
        var grafico = new OverrideGraphicSettings()
            .SetProjectionLineColor(cor)
            .SetProjectionLineWeight(espessura);
        var solido = preencher ? Solido(doc) : ElementId.InvalidElementId;
        if (solido != ElementId.InvalidElementId)
        {
            grafico.SetSurfaceForegroundPatternId(solido);
            grafico.SetSurfaceForegroundPatternColor(cor);
        }
        return grafico;
    }

    /// <summary>Hachura de desenho em linhas paralelas, com espaçamento no papel. Reaproveita a que já existir com o nome.</summary>
    public static ElementId Padrao(Document doc, string nome, double espacamentoMm, double angulo)
    {
        if (FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Drafting, nome) is { } existente)
            return existente.Id;
        var padrao = new FillPattern(nome, FillPatternTarget.Drafting, FillPatternHostOrientation.ToView, angulo,
            VistasSpk.Pes(espacamentoMm / 1000));
        return FillPatternElement.Create(doc, padrao).Id;
    }

    /// <summary>Tipo de região preenchida com este nome, padrão e cor. Se já existe, atualiza padrão e cor.</summary>
    public static ElementId TipoDeRegiao(Document doc, string nome, ElementId padrao, Color cor)
    {
        var tipos = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().ToList();
        var tipo = tipos.FirstOrDefault(t => t.Name == nome);
        if (tipo is null)
        {
            var modelo = tipos.FirstOrDefault() ?? throw new InvalidOperationException("O projeto não tem tipo de região preenchida.");
            tipo = (FilledRegionType)modelo.Duplicate(nome);
        }
        tipo.ForegroundPatternId = padrao;
        tipo.ForegroundPatternColor = cor;
        tipo.BackgroundPatternId = ElementId.InvalidElementId;
        tipo.IsMasking = false;
        return tipo.Id;
    }

    /// <summary>
    /// Região preenchida com estes laços (externos e furos). O Revit exige as curvas no plano da vista e esse plano varia
    /// com o projeto, então tenta a cota do nível e depois zero. Se os laços juntos forem recusados, cria um por um.
    /// </summary>
    public static void RegiaoPreenchida(Document doc, View vista, ElementId tipo, IReadOnlyList<IReadOnlyList<XYZ>> lacos, Level? nivel)
    {
        var cotas = nivel is null ? [0.0] : new[] { nivel.ProjectElevation, 0.0 };
        Autodesk.Revit.Exceptions.ApplicationException? erro = null;
        foreach (var z in cotas)
        {
            try
            {
                FilledRegion.Create(doc, tipo, vista.Id, lacos.Select(l => Laco(l, z)).ToList());
                return;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException e)
            {
                erro = e;
            }
        }

        if (lacos.Count > 1)
        {
            var feitos = 0;
            foreach (var laco in lacos.Where(l => Motor.Modelo.Contorno.Area(l.Select(p => (p.X, p.Y)).ToList()) > 0))
            foreach (var z in cotas)
            {
                try
                {
                    FilledRegion.Create(doc, tipo, vista.Id, [Laco(laco, z)]);
                    feitos++;
                    break;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException e)
                {
                    erro = e;
                }
            }
            if (feitos > 0) return;
        }
        throw erro!;
    }

    static CurveLoop Laco(IReadOnlyList<XYZ> pontos, double z)
    {
        var laco = new CurveLoop();
        for (var i = 0; i < pontos.Count; i++)
        {
            var a = pontos[i];
            var b = pontos[(i + 1) % pontos.Count];
            laco.Append(Line.CreateBound(new XYZ(a.X, a.Y, z), new XYZ(b.X, b.Y, z)));
        }
        return laco;
    }

    /// <summary>Roda uma anotação ou ajuste que pode falhar sem derrubar a folha: a falha vira aviso para o engenheiro.</summary>
    public static void Tentar(IList<string> avisos, string oQue, Action acao)
    {
        try
        {
            acao();
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            avisos.Add($"{oQue}: {e.Message}");
        }
    }

    /// <summary>kPa em bar com vírgula, como nas folhas e no memorial.</summary>
    public static string Bar(double kPa) => (kPa / 100).ToString("0.00", Entrada.Cultura);

    /// <summary>Nome de vista ou folha sem os caracteres que o Revit recusa.</summary>
    public static string NomeSeguro(string nome)
    {
        const string proibidos = "\\:{}[]|;<>?`~";
        var limpo = new string(nome.Select(c => proibidos.Contains(c) ? '-' : c).ToArray()).Trim();
        return limpo.Length > 0 ? limpo : "Setor";
    }

    /// <summary>Código curto do setor para o número da folha: só letras e algarismos, no máximo 12.</summary>
    public static string Codigo(string setor)
    {
        var limpo = new string(RemoverAcentos(setor).Where(char.IsLetterOrDigit).Take(12).ToArray()).ToUpperInvariant();
        return limpo.Length > 0 ? limpo : "SETOR";
    }

    static string RemoverAcentos(string texto)
    {
        var decomposto = texto.Normalize(System.Text.NormalizationForm.FormD);
        return new string(decomposto.Where(c =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }
}

/// <summary>O que um gerador de folhas fez: o texto para o engenheiro e as folhas, na ordem do PDF.</summary>
sealed record FolhasGeradas(string Texto, IList<ElementId> Folhas);

/// <summary>Retângulo na folha, em pés. <see cref="Parte" /> recorta por frações da largura e da altura.</summary>
readonly record struct Regiao(double X0, double Y0, double X1, double Y1)
{
    public double Largura => X1 - X0;
    public double Altura => Y1 - Y0;
    public double LarguraMm => VistasSpk.Milimetros(Largura);
    public double AlturaMm => VistasSpk.Milimetros(Altura);
    public XYZ Centro => new((X0 + X1) / 2, (Y0 + Y1) / 2, 0);
    public XYZ CantoSuperiorEsquerdo => new(X0, Y1, 0);

    public Regiao Parte(double fx0, double fy0, double fx1, double fy1) => new(
        X0 + Largura * fx0, Y0 + Altura * fy0, X0 + Largura * fx1, Y0 + Altura * fy1);

    /// <summary>A menor escala em que a extensão (pés de modelo) cabe aqui, com 5% de folga.</summary>
    public int Escala(double larguraModelo, double alturaModelo) =>
        VistasSpk.EscalaQueCabe(larguraModelo, alturaModelo, LarguraMm * 0.95, AlturaMm * 0.95);
}
