namespace Spk.AddIn.Folhas;

/// <summary>
/// Isométrico geral, no padrão da NEPN-INC-013: o sistema inteiro ligado aos bicos (setores, VGAs, geral e o que mais
/// estiver conectado), só tubulação, em linha simples. A vista é travada para receber as etiquetas de diâmetro.
/// </summary>
static class FolhasIsometrico
{
    public const string Numero = PranchaSpk.PrefixoNumero + "ISO";
    const int MaximoEtiquetas = 200;

    static readonly BuiltInCategory[] CategoriasDaRede =
    [
        BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_FlexPipeCurves, BuiltInCategory.OST_PipeFitting,
        BuiltInCategory.OST_PipeAccessory, BuiltInCategory.OST_Sprinklers,
    ];

    public static FolhasGeradas Gerar(ContextoFolhas contexto)
    {
        var doc = contexto.Doc;
        var avisos = new List<string>();
        var inicio = Inicio(doc);
        if (inicio.Count == 0) throw new InvalidOperationException("O modelo não tem bicos de sprinkler.");
        var rede = Conectados(doc, inicio);
        IncluirRedeLida(contexto, rede, avisos);
        var caixa = VistasSpk.CaixaDe(rede.Select(doc.GetElement).OfType<Element>())
                    ?? throw new InvalidOperationException("Não foi possível medir a rede.");

        var folha = PranchaSpk.Recriar(doc, Numero, "Sprinklers — isométrico geral");
        doc.Regenerate();
        var util = PranchaSpk.Util(folha);
        // Notas embaixo à esquerda, como o quadro da folha do setor: o alto da coluna direita pode ser área do carimbo.
        var regiaoIso = util.Parte(0, 0.17, 0.74, 1);
        var regiaoNota = util.Parte(0, 0, 0.6, 0.15);

        var iso = VistasSpk.Isometrico(doc, $"{PranchaSpk.PrefixoVista}Isométrico geral", caixa.Expandir(VistasSpk.Pes(0.5), VistasSpk.Pes(0.5)),
            VistasSpk.Template3d);
        EsconderForaDaRede(doc, iso, rede, avisos);
        doc.Regenerate();
        PranchaSpk.Tentar(avisos, "Recorte do isométrico", () => VistasSpk.Recortar3d(iso, caixa));
        var (largura, altura) = VistasSpk.Extensao(iso);
        PranchaSpk.Tentar(avisos, "Escala do isométrico", () => iso.Scale = regiaoIso.Escala(largura, altura));
        var escala = iso.Scale;

        var etiquetas = 0;
        PranchaSpk.Tentar(avisos, "Travar o isométrico para etiquetar", () =>
        {
            iso.SaveOrientationAndLock();
            etiquetas = Etiquetar(doc, iso, rede, VistasSpk.Pes(Math.Max(1.0, 12.0 * escala / 1000)), avisos);
        });
        NomearVgas(doc, iso, avisos);

        PranchaSpk.Colocar(doc, folha, iso, regiaoIso.Centro, avisos);
        PranchaSpk.ColocarLegendas(doc, folha, util.Parte(0.76, 0, 1, 0.6), avisos);
        PranchaSpk.Tentar(avisos, "Notas do isométrico", () => PranchaSpk.Nota(doc, folha, regiaoNota.CantoSuperiorEsquerdo,
            Notas(contexto, rede.Count), regiaoNota.Largura * 0.95));

        var texto = $"{Numero}  {folha.Name}\n{rede.Count} elementos ligados aos bicos, 1:{escala}, {etiquetas} etiquetas de diâmetro.";
        if (avisos.Count > 0) texto += "\n\nAvisos:\n" + string.Join("\n", avisos.Distinct().Take(12).Select(a => "• " + a));
        return new FolhasGeradas(texto, [folha.Id]);
    }

    /// <summary>Os bicos que o plugin conhece (malha gerada ou famílias confirmadas); sem eles, todos os sprinklers do modelo.</summary>
    static List<Element> Inicio(Document doc)
    {
        try
        {
            return BicosDoModelo.Ler(doc).Bicos.Select(b => (Element)b.Instancia).ToList();
        }
        catch (InvalidOperationException)
        {
            return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sprinklers).WhereElementIsNotElementType().ToList();
        }
    }

    /// <summary>Tudo o que está ligado aos bicos por conectores: tubos, conexões, acessórios (VGAs inclusive) e outros bicos.</summary>
    static HashSet<ElementId> Conectados(Document doc, IEnumerable<Element> inicio)
    {
        var vistos = new HashSet<ElementId>();
        var fila = new Queue<Element>(inicio);
        while (fila.Count > 0)
        {
            var elemento = fila.Dequeue();
            if (!vistos.Add(elemento.Id)) continue;
            var gerente = elemento switch
            {
                MEPCurve curva => curva.ConnectorManager,
                FamilyInstance instancia => instancia.MEPModel?.ConnectorManager,
                _ => null,
            };
            if (gerente is null) continue;
            foreach (Connector conector in gerente.Connectors)
            {
                if (!conector.IsConnected) continue;
                foreach (Connector outro in conector.AllRefs)
                    if (outro.Owner is { } dono && !vistos.Contains(dono.Id) && DaRede(dono)) fila.Enqueue(dono);
            }
        }
        return vistos;
    }

    /// <summary>
    /// Num modelo sem conexões ligadas (Nova Era), seguir conectores só acha os bicos. Os tubos que a leitura por
    /// proximidade associou a cada setor e ao último cálculo completam a rede, e os bicos ficam só os dos setores lidos.
    /// </summary>
    static void IncluirRedeLida(ContextoFolhas contexto, HashSet<ElementId> rede, IList<string> avisos)
    {
        try
        {
            var bicosLidos = new HashSet<ElementId>();
            foreach (var setor in contexto.Setores)
            {
                rede.UnionWith(setor.Tubos.Select(t => t.Id));
                bicosLidos.UnionWith(setor.Bicos.Select(b => b.Id));
            }
            // Bico que nenhum setor alcançou ficaria solto no desenho, sem tubo: sai do isométrico.
            if (bicosLidos.Count > 0)
            {
                var doc = contexto.Doc;
                rede.RemoveWhere(id => doc.GetElement(id)?.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers && !bicosLidos.Contains(id));
                rede.UnionWith(bicosLidos);
            }
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            avisos.Add("Rede lida dos setores: " + e.Message);
        }
        if (ResultadoGravado.Ler(contexto.Doc) is { } calculo)
            rede.UnionWith(calculo.Tubos.Select(id => new ElementId(id)));
    }

    static bool DaRede(Element elemento) =>
        elemento.Category?.Id.Value is long id && CategoriasDaRede.Any(c => (long)c == id);

    /// <summary>Esconde, nesta vista, os elementos de tubulação que não estão ligados aos bicos (água fria, esgoto...).</summary>
    static void EsconderForaDaRede(Document doc, View vista, HashSet<ElementId> rede, IList<string> avisos)
    {
        var fora = new List<ElementId>();
        foreach (var categoria in CategoriasDaRede)
            foreach (var elemento in new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsNotElementType())
                if (!rede.Contains(elemento.Id) && elemento.CanBeHidden(vista)) fora.Add(elemento.Id);
        if (fora.Count > 0)
            PranchaSpk.Tentar(avisos, "Esconder o que não é da rede", () => vista.HideElements(fora));
    }

    /// <summary>Etiqueta de diâmetro nos tubos de pelo menos <paramref name="minimo" />, numa vista 3D já travada.</summary>
    public static int Etiquetar(Document doc, View vista, HashSet<ElementId> rede, double minimo, IList<string> avisos)
    {
        var tipo = PranchaSpk.EtiquetaDeTubo(doc);
        if (tipo == ElementId.InvalidElementId)
        {
            avisos.Add("O projeto não tem etiqueta de tubo carregada; o isométrico ficou sem diâmetros.");
            return 0;
        }

        // Os mais longos primeiro: a geral e as subgerais são o que o isométrico precisa mostrar.
        var tubos = rede.Select(doc.GetElement).OfType<MEPCurve>()
            .Select(t => (Tubo: t, Linha: (t.Location as LocationCurve)?.Curve as Line))
            .Where(p => p.Linha is not null && p.Linha.Length >= minimo)
            .OrderByDescending(p => p.Linha!.Length)
            .ToList();
        PranchaSpk.Tentar(avisos, "Mostrar as etiquetas no 3D", () => VistasSpk.MostrarEtiquetas(doc, vista));
        if (tubos.Count == 0) return 0;
        if (ComoEtiquetar(doc, vista, tipo, tubos[0].Tubo, tubos[0].Linha!, avisos) is not { } como) return 0;
        var (aceito, chamada) = como;
        var feitas = 0;
        foreach (var (tubo, linha) in tubos)
        {
            if (feitas >= MaximoEtiquetas)
            {
                avisos.Add($"Só os {MaximoEtiquetas} tubos mais longos receberam etiqueta no isométrico.");
                break;
            }
            PranchaSpk.Tentar(avisos, "Etiqueta no isométrico", () =>
            {
                IndependentTag.Create(doc, aceito, vista.Id, new Reference(tubo), chamada, TagOrientation.Horizontal, linha!.Evaluate(0.5, true));
                feitas++;
            });
        }

        // O Revit pode apagar etiquetas de 3D sem erro nem aviso; conta o que ficou para não informar etiquetas que não existem.
        doc.Regenerate();
        var ficaram = new FilteredElementCollector(doc).OfClass(typeof(IndependentTag)).Cast<IndependentTag>()
            .Count(t => t.OwnerViewId == vista.Id);
        if (ficaram < feitas)
            avisos.Add($"O Revit apagou {feitas - ficaram} das {feitas} etiquetas do 3D \"{vista.Name}\".");
        return ficaram;
    }

    /// <summary>
    ///     Como etiquetar o 3D sem que o Revit apague as etiquetas ao regenerar, o que ele faz sem erro nem aviso. Com o
    ///     template 3D da Nova Era (vista sombreada), a etiqueta sem linha de chamada sumia; em linhas ocultas, o estilo de
    ///     desenho do isométrico, ela fica. Prova com uma etiqueta só, nesta ordem: a vista como veio do template, a vista em
    ///     linhas ocultas, a etiqueta com linha de chamada; e, se o tipo do padrão não ficar de jeito nenhum, os outros
    ///     tipos de etiqueta de tubo do projeto.
    /// </summary>
    static (ElementId Tipo, bool Chamada)? ComoEtiquetar(Document doc, View vista, ElementId preferido, MEPCurve tubo, Line linha,
        IList<string> avisos)
    {
        bool Fica(ElementId tipo, bool chamada)
        {
            try
            {
                var prova = IndependentTag.Create(doc, tipo, vista.Id, new Reference(tubo), chamada, TagOrientation.Horizontal,
                    linha.Evaluate(0.5, true)).Id;
                doc.Regenerate();
                if (doc.GetElement(prova) is null) return false;
                doc.Delete(prova);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return false;
            }
        }

        var candidatos = new[] { preferido }.Concat(new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeTags)
                .WhereElementIsElementType().ToElementIds().Where(id => id != preferido))
            .ToList();
        var recusados = new List<string>();
        foreach (var tipo in candidatos)
        {
            if (Fica(tipo, false)) return Aceito(tipo, false);
            if (vista.DisplayStyle != DisplayStyle.HLR)
            {
                var estilo = vista.DisplayStyle;
                vista.DisplayStyle = DisplayStyle.HLR;
                if (Fica(tipo, false)) return Aceito(tipo, false);
                vista.DisplayStyle = estilo;
            }
            if (Fica(tipo, true))
            {
                avisos.Add("As etiquetas do 3D saíram com linha de chamada: sem ela o Revit as apagava.");
                return Aceito(tipo, true);
            }
            recusados.Add(NomeDoTipo(doc, tipo));
        }

        avisos.Add($"Nenhuma etiqueta de tubo do projeto fica no 3D ({string.Join(", ", recusados)}); o isométrico ficou sem diâmetros.");
        return null;

        (ElementId, bool) Aceito(ElementId tipo, bool chamada)
        {
            if (tipo != preferido)
                avisos.Add($"A etiqueta de tubo {string.Join(", ", recusados)} não fica no 3D; o isométrico usou {NomeDoTipo(doc, tipo)}.");
            return (tipo, chamada);
        }
    }

    static string NomeDoTipo(Document doc, ElementId id) =>
        doc.GetElement(id) is FamilySymbol s ? $"\"{PadraoEscritorio.Nome(s)}\"" : id.ToString();

    static void NomearVgas(Document doc, View vista, IList<string> avisos)
    {
        foreach (var vga in MapeamentoGravado.Vgas(doc))
        {
            if (vga.Location is not LocationPoint ponto) continue;
            PranchaSpk.Tentar(avisos, "Nome da VGA no isométrico", () => PranchaSpk.Nota(doc, vista, ponto.Point, LeitorRede.NomeDaVga(vga)));
        }
    }

    static string Notas(ContextoFolhas contexto, int elementos)
    {
        var linhas = new List<string> { "ISOMÉTRICO GERAL — SPRINKLERS" };
        try
        {
            foreach (var setor in contexto.Setores)
                linhas.Add($"{setor.Nome}: {setor.Bicos.Count} chuveiros");
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            linhas.Add($"{elementos} elementos ligados aos bicos.");
        }

        if (DadosProjeto.Carregar(contexto.Doc) is { } dados)
        {
            linhas.Add("");
            linhas.Add(dados.Resumo());
            if (dados.PressaoEstaticaKPa is { } estatica && dados.PressaoResidualKPa is { } residual && dados.VazaoResidualLmin is { } vazao)
                linhas.Add($"Abastecimento (ensaio): estática {PranchaSpk.Bar(estatica)} bar; residual {PranchaSpk.Bar(residual)} bar com {vazao:0} L/min");
        }

        if (ResultadoGravado.Ler(contexto.Doc) is { } calculo)
            linhas.Add($"Último cálculo: {calculo.Setor}, {(calculo.Atende ? "ATENDE" : "NÃO ATENDE")}, " +
                       $"{PranchaSpk.Bar(calculo.PressaoNecessariaKPa)} bar e {calculo.VazaoTotalLmin:0} L/min na fonte");
        return string.Join("\n", linhas);
    }
}
