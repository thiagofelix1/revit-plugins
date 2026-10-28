namespace Spk.AddIn.Folhas;

/// <summary>
/// Isométrico geral, no padrão da NEPN-INC-013: o sistema inteiro ligado aos bicos (setores, VGAs, geral e o que mais
/// estiver conectado), só tubulação, em nível de detalhe Fino e em vermelho. A vista é travada para receber as etiquetas de
/// diâmetro dos trechos principais.
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
        // Notas embaixo à esquerda e os quadros ao lado delas, como na NEPN-INC-013; o alto da coluna direita pode ser
        // área do carimbo.
        var regiaoIso = util.Parte(0, 0.17, 0.74, 1);
        var regiaoNota = util.Parte(0, 0, 0.34, 0.15);
        var regiaoChuveiros = util.Parte(0.35, 0, 0.55, 0.16);
        var regiaoDemanda = util.Parte(0.56, 0, 0.74, 0.16);

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
        var quadros = 0;
        PranchaSpk.Tentar(avisos, "Quadro de chuveiros", () =>
        {
            if (QuadrosSpk.Chuveiros(doc, avisos, escala) is not { } quadro || PranchaSpk.Colocar(doc, folha, quadro, regiaoChuveiros.Centro, avisos) is not { } viewport) return;
            PranchaSpk.SemTitulo(doc, viewport);
            quadros++;
        });
        PranchaSpk.Tentar(avisos, "Quadro de demanda", () =>
        {
            if (QuadrosSpk.Demanda(doc, avisos, escala) is not { } quadro || PranchaSpk.Colocar(doc, folha, quadro, regiaoDemanda.Centro, avisos) is not { } viewport) return;
            PranchaSpk.SemTitulo(doc, viewport);
            quadros++;
        });

        var texto = $"{Numero}  {folha.Name}\n{rede.Count} elementos ligados aos bicos, 1:{escala}, {etiquetas} etiquetas de diâmetro, {quadros} quadros.";
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

    /// <summary>
    ///     Etiqueta de diâmetro com linha de chamada nos trechos principais, como no isométrico da NEPN-INC-013: geral,
    ///     colunas, subgerais e descidas, uma etiqueta por trecho reto de mesmo diâmetro com pelo menos
    ///     <paramref name="minimo" />. Ramais que alimentam bicos ficam sem etiqueta no 3D; os diâmetros deles estão na
    ///     planta do setor. Numa vista 3D já travada.
    /// </summary>
    public static int Etiquetar(Document doc, View vista, HashSet<ElementId> rede, double minimo, IList<string> avisos)
    {
        var tipo = PranchaSpk.EtiquetaDeTubo(doc);
        if (tipo == ElementId.InvalidElementId)
        {
            avisos.Add("O projeto não tem etiqueta de tubo carregada; o isométrico ficou sem diâmetros.");
            return 0;
        }

        var tubos = rede.Select(doc.GetElement).OfType<MEPCurve>()
            .Select(t => (Tubo: t, Linha: (t.Location as LocationCurve)?.Curve as Line))
            .Where(p => p.Linha is not null)
            .Select(p => (p.Tubo, Linha: p.Linha!))
            .ToList();
        // O trecho todo conta para o mínimo: a subgeral picotada pelos tês tem pedaços curtos, mas é longa.
        var principais = Principais(tubos).Where(p => p.Comprimento >= minimo).OrderByDescending(p => p.Comprimento)
            .Select(p => (p.Tubo, p.Linha)).ToList();
        PranchaSpk.Tentar(avisos, "Mostrar as etiquetas no 3D", () => VistasSpk.MostrarEtiquetas(doc, vista));
        if (principais.Count == 0) return 0;

        // Texto acima e à direita do tubo, a 8 mm no papel, ligado a ele pela linha de chamada.
        var afastamento = VistasSpk.Pes(8.0 * vista.Scale / 1000);
        XYZ Ponto(Line linha, bool chamada) =>
            linha.Evaluate(0.5, true) + (chamada ? (vista.UpDirection + vista.RightDirection) * afastamento : XYZ.Zero);

        if (ComoEtiquetar(doc, vista, tipo, principais[0].Tubo, principais[0].Linha, Ponto, avisos) is not { } como) return 0;
        var (aceito, comChamada) = como;
        var feitas = 0;
        foreach (var (tubo, linha) in principais)
        {
            if (feitas >= MaximoEtiquetas)
            {
                avisos.Add($"Só os {MaximoEtiquetas} trechos mais longos receberam etiqueta no isométrico.");
                break;
            }
            PranchaSpk.Tentar(avisos, "Etiqueta no isométrico", () =>
            {
                IndependentTag.Create(doc, aceito, vista.Id, new Reference(tubo), comChamada, TagOrientation.Horizontal, Ponto(linha, comChamada));
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
    ///     Um tubo por trecho principal. Tubos ligados (direto ou por uma conexão), alinhados e de mesmo diâmetro formam um
    ///     trecho, representado pelo mais longo, com o comprimento somado do trecho. Fica de fora o trecho que alimenta bico:
    ///     com um tubo ligado a bico, ou ligado a um braço curto que vai ao bico.
    /// </summary>
    static List<(MEPCurve Tubo, Line Linha, double Comprimento)> Principais(List<(MEPCurve Tubo, Line Linha)> tubos)
    {
        var indice = new Dictionary<ElementId, int>();
        for (var i = 0; i < tubos.Count; i++) indice[tubos[i].Tubo.Id] = i;
        var pai = Enumerable.Range(0, tubos.Count).ToArray();
        int Raiz(int i)
        {
            while (pai[i] != i) i = pai[i] = pai[pai[i]];
            return i;
        }

        var vizinhos = tubos.Select(t => Vizinhos(t.Tubo)).ToList();
        var bracoCurto = VistasSpk.Pes(0.6);
        var ramal = new bool[tubos.Count];
        for (var i = 0; i < tubos.Count; i++)
        {
            if (vizinhos[i].Bicos > 0) ramal[i] = true;
            foreach (var id in vizinhos[i].Tubos)
            {
                if (!indice.TryGetValue(id, out var j)) continue;
                if (vizinhos[j].Bicos > 0 && tubos[j].Linha.Length < bracoCurto) ramal[i] = true;
                if (Alinhados(tubos[i], tubos[j])) pai[Raiz(i)] = Raiz(j);
            }
        }

        return Enumerable.Range(0, tubos.Count)
            .GroupBy(Raiz)
            .Where(g => !g.Any(i => ramal[i]))
            .Select(g =>
            {
                var maior = tubos[g.OrderByDescending(i => tubos[i].Linha.Length).First()];
                return (maior.Tubo, maior.Linha, g.Sum(i => tubos[i].Linha.Length));
            })
            .ToList();
    }

    /// <summary>Tubos e bicos ligados a este tubo, direto ou através de uma conexão ou acessório.</summary>
    static (List<ElementId> Tubos, int Bicos) Vizinhos(MEPCurve tubo)
    {
        var tubos = new List<ElementId>();
        var bicos = 0;
        void Olhar(Element dono, Element vindoDe, bool atravessar)
        {
            if (dono.Id == tubo.Id || dono.Id == vindoDe.Id) return;
            if (dono is MEPCurve) tubos.Add(dono.Id);
            else if (dono.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers) bicos++;
            else if (atravessar && dono is FamilyInstance { MEPModel.ConnectorManager: { } conectores })
                foreach (var outro in Ligados(conectores)) Olhar(outro, dono, false);
        }

        foreach (var dono in Ligados(tubo.ConnectorManager)) Olhar(dono, tubo, true);
        return (tubos, bicos);
    }

    static IEnumerable<Element> Ligados(ConnectorManager conectores) =>
        conectores.Connectors.Cast<Connector>().Where(c => c.IsConnected)
            .SelectMany(c => c.AllRefs.Cast<Connector>()).Select(r => r.Owner).OfType<Element>();

    /// <summary>Mesmo diâmetro e na mesma reta.</summary>
    static bool Alinhados((MEPCurve Tubo, Line Linha) a, (MEPCurve Tubo, Line Linha) b)
    {
        if (Math.Abs(Diametro(a.Tubo) - Diametro(b.Tubo)) > VistasSpk.Pes(0.001)) return false;
        if (a.Linha.Direction.CrossProduct(b.Linha.Direction).GetLength() > 0.02) return false;
        return Line.CreateUnbound(a.Linha.GetEndPoint(0), a.Linha.Direction).Distance(b.Linha.GetEndPoint(0)) < VistasSpk.Pes(0.03);
    }

    static double Diametro(MEPCurve tubo) => tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0;

    /// <summary>
    ///     Como etiquetar o 3D sem que o Revit apague as etiquetas ao regenerar, o que ele faz sem erro nem aviso: na vista
    ///     sombreada, a etiqueta sem linha de chamada some. Prova com uma etiqueta só, nesta ordem: com linha de chamada,
    ///     sem ela, e as duas de novo com a vista em linhas ocultas; e, se o tipo do padrão não ficar de jeito nenhum, os
    ///     outros tipos de etiqueta de tubo do projeto.
    /// </summary>
    static (ElementId Tipo, bool Chamada)? ComoEtiquetar(Document doc, View vista, ElementId preferido, MEPCurve tubo, Line linha,
        Func<Line, bool, XYZ> ponto, IList<string> avisos)
    {
        bool Fica(ElementId tipo, bool chamada)
        {
            try
            {
                var prova = IndependentTag.Create(doc, tipo, vista.Id, new Reference(tubo), chamada, TagOrientation.Horizontal,
                    ponto(linha, chamada)).Id;
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
            if (Fica(tipo, true)) return Aceito(tipo, true);
            if (Fica(tipo, false)) return Aceito(tipo, false);
            if (vista.DisplayStyle != DisplayStyle.HLR)
            {
                var estilo = vista.DisplayStyle;
                vista.DisplayStyle = DisplayStyle.HLR;
                if (Fica(tipo, true)) return Aceito(tipo, true);
                if (Fica(tipo, false)) return Aceito(tipo, false);
                vista.DisplayStyle = estilo;
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
