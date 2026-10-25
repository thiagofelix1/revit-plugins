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
        var caixa = VistasSpk.CaixaDe(rede.Select(doc.GetElement).OfType<Element>())
                    ?? throw new InvalidOperationException("Não foi possível medir a rede.");

        var folha = PranchaSpk.Recriar(doc, Numero, "Sprinklers — isométrico geral");
        doc.Regenerate();
        var util = PranchaSpk.Util(folha);
        var regiaoIso = util.Parte(0, 0, 0.74, 1);
        var regiaoNota = util.Parte(0.76, 0.40, 1, 1);

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
            etiquetas = Etiquetar(doc, iso, rede, VistasSpk.Pes(Math.Max(3.0, 15.0 * escala / 1000)), avisos);
        });
        NomearVgas(doc, iso, avisos);

        PranchaSpk.Colocar(doc, folha, iso, regiaoIso.Centro, avisos);
        PranchaSpk.ColocarLegendas(doc, folha, util.Parte(0.76, 0, 1, 0.38), avisos);
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
                IndependentTag.Create(doc, tipo, vista.Id, new Reference(tubo), false, TagOrientation.Horizontal, linha!.Evaluate(0.5, true));
                feitas++;
            });
        }
        return feitas;
    }

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
