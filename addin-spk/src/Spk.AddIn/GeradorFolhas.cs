using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace Spk.AddIn;

/// <summary>
/// Folhas da malha marcada: planta com o retângulo da área, isométrico e corte da descida.
/// O carimbo é o que já estiver no projeto. O detalhe típico da válvula não é desenhado aqui.
/// </summary>
static class GeradorFolhas
{
    public const string NomePlanta = "SPK — Planta do setor";
    public const string NomeIso = "SPK — Isométrico";
    public const string NomeCorte = "SPK — Corte da descida";
    public const string NumeroPlanta = "SPK-010";
    public const string NumeroIso = "SPK-013";
    public const string NumeroCorte = "SPK-012";

    public static Resultado Gerar(Document doc)
    {
        var bicos = BicosMarcados(doc);
        if (bicos.Count == 0)
            throw new InvalidOperationException("Não há malha marcada. Use Rede de exemplo ou Gerar antes das folhas.");

        var tubos = TubosDaRede(doc, bicos);
        var dados = DadosEsboco.Carregar(doc);
        var calculo = CalculoDestaMalha(doc, bicos);
        var daArea = BicosDaArea(bicos, calculo);
        var elementos = bicos.Concat<Element>(tubos).ToList();
        var caixaRede = Caixa(elementos)
                        ?? throw new InvalidOperationException("Não foi possível medir a malha.");
        var caixaArea = Expandir(Caixa(daArea) ?? caixaRede, dados);
        var noPlano = CaixaNoPlano(elementos) ?? caixaRede;
        var margem = Pes(1.2);
        var contorno = new Caixa3d(
            new XYZ(noPlano.Min.X - margem, noPlano.Min.Y - margem, noPlano.Min.Z),
            new XYZ(noPlano.Max.X + margem, noPlano.Max.Y + margem, noPlano.Max.Z));
        var tudo = Unir(Unir(caixaRede, caixaArea, Pes(1.5)), contorno, Pes(0.6));

        var nivel = Nivel(doc, bicos);
        var planta = Planta(doc, nivel);
        var iso = Isometrico(doc);
        Preparar(planta);
        Preparar(iso);
        AjustarCorte(planta, nivel);
        EnquadrarPlanta(planta, tudo);
        Olhar(iso, tudo);
        doc.Regenerate();
        Enquadrar3d(iso, tudo);
        LimparPintura(planta, bicos);
        LimparPintura(iso, bicos);
        var retangulo = calculo is null ? contorno : caixaArea;
        Retangulo(doc, planta, retangulo);
        var critico = PintarCritico(doc, planta, iso, bicos, calculo);
        Nota(doc, planta, new XYZ(retangulo.Min.X, retangulo.Max.Y + Pes(1.0), 0),
            calculo is null ? "Malha gerada" : $"Área de operação — {daArea.Count} bicos");

        ViewSection? corte = null;
        string? avisoCorte = null;
        try
        {
            corte = Corte(doc, tudo);
            Preparar(corte);
            corte.Scale = 50;
            corte.DetailLevel = ViewDetailLevel.Fine;
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException e)
        {
            avisoCorte = e.Message;
        }

        doc.Regenerate();
        var carimbo = Carimbo(doc);
        var texto = Texto(calculo, daArea.Count, critico, tubos);
        var folhaPlanta = Folha(doc, NumeroPlanta, "Sprinklers — planta do setor", carimbo, planta, texto);
        var folhaIso = Folha(doc, NumeroIso, "Isométrico", carimbo, iso, texto);
        var folhas = new List<ElementId> { folhaPlanta, folhaIso };
        if (corte is not null)
            folhas.Add(Folha(doc, NumeroCorte, "Corte da descida", carimbo, corte, texto));

        var resumo = string.Join("\n", folhas.Select(id =>
        {
            var folha = (ViewSheet)doc.GetElement(id);
            return $"{folha.SheetNumber}  {folha.Name}";
        }));
        if (calculo is null)
            resumo += "\n\nRode Calcular para gravar pressão, vazão e o bico crítico nestas folhas.";
        if (avisoCorte is not null)
            resumo += "\n\nO corte da descida não foi criado: " + avisoCorte;
        if (carimbo == ElementId.InvalidElementId)
            resumo += "\n\nO projeto não tem carimbo. As folhas foram criadas sem ele.";

        return new Resultado(resumo, folhaPlanta, folhas);
    }

    public sealed record Resultado(string Texto, ElementId FolhaPlanta, IList<ElementId> Folhas);

    static List<FamilyInstance> BicosMarcados(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(fi =>
                fi.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers
                && (fi.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() == DadosEsboco.Marca
                    || fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() == DadosEsboco.Marca))
            .ToList();

    static List<Pipe> TubosDaRede(Document doc, IReadOnlyList<FamilyInstance> bicos)
    {
        var tubos = new Dictionary<ElementId, Pipe>();
        var fila = new Queue<Element>(bicos);
        var vistos = new HashSet<ElementId>();
        while (fila.Count > 0)
        {
            var elemento = fila.Dequeue();
            if (!vistos.Add(elemento.Id)) continue;
            if (elemento is Pipe tubo) tubos[tubo.Id] = tubo;
            var gerente = Conectores(elemento);
            if (gerente is null) continue;
            foreach (Connector conector in gerente.Connectors)
            foreach (Connector outro in conector.AllRefs)
            {
                if (outro.Owner is { } dono && Segue(dono) && !vistos.Contains(dono.Id))
                    fila.Enqueue(dono);
            }
        }

        foreach (var tubo in new FilteredElementCollector(doc).OfClass(typeof(Pipe)).Cast<Pipe>())
        {
            if (tubo.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() == DadosEsboco.Marca)
                tubos[tubo.Id] = tubo;
        }

        return tubos.Values.ToList();
    }

    static bool Segue(Element elemento) =>
        elemento is Pipe
        || (elemento is FamilyInstance fi && fi.Category?.Id.Value is long id && (
            id == (long)BuiltInCategory.OST_PipeFitting
            || id == (long)BuiltInCategory.OST_Sprinklers
            || id == (long)BuiltInCategory.OST_PipeAccessory));

    static ConnectorManager? Conectores(Element elemento) => elemento switch
    {
        MEPCurve curva => curva.ConnectorManager,
        FamilyInstance instancia => instancia.MEPModel?.ConnectorManager,
        _ => null,
    };

    /// <summary>O último cálculo, só se foi feito com estes mesmos bicos. Rede alterada depois disso pede novo cálculo.</summary>
    static ResultadoGravado? CalculoDestaMalha(Document doc, IReadOnlyList<FamilyInstance> bicos)
    {
        var gravado = ResultadoGravado.Ler(doc);
        if (gravado?.Chuveiros is not { Count: > 0 }) return null;
        var ids = bicos.Select(b => b.Id.Value).ToHashSet();
        return gravado.Chuveiros.Count == bicos.Count && gravado.Chuveiros.All(c => ids.Contains(c.Elemento)) ? gravado : null;
    }

    static List<FamilyInstance> BicosDaArea(List<FamilyInstance> bicos, ResultadoGravado? calculo)
    {
        if (calculo is null) return bicos;
        var ids = calculo.DaArea.Select(c => c.Elemento).ToHashSet();
        var daArea = bicos.Where(b => ids.Contains(b.Id.Value)).ToList();
        return daArea.Count > 0 ? daArea : bicos;
    }


    static Level Nivel(Document doc, IReadOnlyList<FamilyInstance> bicos)
    {
        foreach (var bico in bicos)
        {
            if (doc.GetElement(bico.LevelId) is Level nivel) return nivel;
        }

        return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(n => n.Elevation).FirstOrDefault()
               ?? throw new InvalidOperationException("O projeto não tem nível.");
    }

    static ViewPlan Planta(Document doc, Level nivel)
    {
        var existente = Vista<ViewPlan>(doc, NomePlanta);
        if (existente is not null)
        {
            LimparAnotacoes(doc, existente);
            Soltar(doc, existente);
            return existente;
        }

        var planta = ViewPlan.Create(doc, Tipo(doc, ViewFamily.FloorPlan).Id, nivel.Id);
        planta.Name = NomePlanta;
        return planta;
    }

    static View3D Isometrico(Document doc)
    {
        var existente = Vista<View3D>(doc, NomeIso);
        if (existente is not null)
        {
            LimparAnotacoes(doc, existente);
            Soltar(doc, existente);
            return existente;
        }

        var vista = View3D.CreateIsometric(doc, Tipo(doc, ViewFamily.ThreeDimensional).Id);
        vista.Name = NomeIso;
        return vista;
    }

    /// <summary>Olha no sentido dos ramais, para a descida e a subgeral aparecerem de perfil.</summary>
    static ViewSection Corte(Document doc, Caixa3d tudo)
    {
        var existente = Vista<ViewSection>(doc, NomeCorte);
        if (existente is not null)
        {
            Soltar(doc, existente);
            return existente;
        }

        var centro = tudo.Centro;
        var margem = Pes(1.5);
        var orientacao = Transform.Identity;
        orientacao.Origin = centro;
        orientacao.BasisX = XYZ.BasisY;
        orientacao.BasisY = XYZ.BasisZ;
        orientacao.BasisZ = -XYZ.BasisX;
        var caixa = new BoundingBoxXYZ
        {
            Enabled = true,
            Transform = orientacao,
            Min = new XYZ(-Metade(tudo.Max.Y - tudo.Min.Y, margem), -Metade(tudo.Max.Z - tudo.Min.Z, margem), -Metade(tudo.Max.X - tudo.Min.X, margem)),
            Max = new XYZ(Metade(tudo.Max.Y - tudo.Min.Y, margem), Metade(tudo.Max.Z - tudo.Min.Z, margem), Metade(tudo.Max.X - tudo.Min.X, margem)),
        };
        for (var i = 0; i < 3; i++)
        {
            caixa.set_MinEnabled(i, true);
            caixa.set_MaxEnabled(i, true);
        }

        var corte = ViewSection.CreateSection(doc, Tipo(doc, ViewFamily.Section).Id, caixa);
        corte.Name = NomeCorte;
        return corte;
    }

    static double Metade(double extensão, double margem) => Math.Max(extensão, Pes(2)) / 2 + margem;

    static void Preparar(View vista)
    {
        try { vista.ViewTemplateId = ElementId.InvalidElementId; }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }

        vista.Scale = 50;
        vista.DetailLevel = ViewDetailLevel.Fine;
        try { vista.Discipline = ViewDiscipline.Coordination; }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }

        foreach (var categoria in new[]
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_Sprinklers,
            BuiltInCategory.OST_PipeAccessory,
        })
            Visibilidade(vista, categoria, false);

        foreach (var categoria in new[]
        {
            BuiltInCategory.OST_Levels,
            BuiltInCategory.OST_Grids,
            BuiltInCategory.OST_CLines,
        })
            Visibilidade(vista, categoria, true);

        if (vista is View3D iso)
        {
            var tubos = new OverrideGraphicSettings();
            tubos.SetProjectionLineColor(new Color(0, 70, 140));
            tubos.SetProjectionLineWeight(5);
            try { iso.SetCategoryOverrides(new ElementId(BuiltInCategory.OST_PipeCurves), tubos); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        }
    }

    static void Visibilidade(View vista, BuiltInCategory categoria, bool escondida)
    {
        var id = new ElementId(categoria);
        try
        {
            if (vista.CanCategoryBeHidden(id)) vista.SetCategoryHidden(id, escondida);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }
    }

    static void AjustarCorte(ViewPlan planta, Level nivel)
    {
        try
        {
            var intervalo = planta.GetViewRange();
            intervalo.SetLevelId(PlanViewPlane.CutPlane, nivel.Id);
            intervalo.SetOffset(PlanViewPlane.CutPlane, Pes(4));
            intervalo.SetLevelId(PlanViewPlane.TopClipPlane, nivel.Id);
            intervalo.SetOffset(PlanViewPlane.TopClipPlane, Pes(6));
            planta.SetViewRange(intervalo);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }
    }

    static void EnquadrarPlanta(View planta, Caixa3d caixa)
    {
        var crop = planta.CropBox;
        var inverso = crop.Transform.Inverse;
        var a = inverso.OfPoint(new XYZ(caixa.Min.X, caixa.Min.Y, 0));
        var b = inverso.OfPoint(new XYZ(caixa.Max.X, caixa.Max.Y, 0));
        crop.Min = new XYZ(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), crop.Min.Z);
        crop.Max = new XYZ(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), crop.Max.Z);
        planta.CropBox = crop;
        planta.CropBoxActive = true;
        planta.CropBoxVisible = false;
    }

    static void Olhar(View3D vista, Caixa3d caixa)
    {
        var centro = caixa.Centro;
        var distancia = Math.Max((caixa.Max - caixa.Min).GetLength(), Pes(8)) * 2;
        var olho = centro + new XYZ(1, -1, 0.75).Normalize() * distancia;
        vista.SetOrientation(OlharPara(centro, olho));
        var secao = new BoundingBoxXYZ { Min = caixa.Min, Max = caixa.Max };
        vista.SetSectionBox(secao);
        vista.IsSectionBoxActive = true;
    }

    static ViewOrientation3D OlharPara(XYZ centro, XYZ olho)
    {
        var frente = (centro - olho).Normalize();
        var cima = XYZ.BasisZ - frente * frente.DotProduct(XYZ.BasisZ);
        if (cima.GetLength() < 1e-6) cima = XYZ.BasisY;
        return new ViewOrientation3D(olho, cima.Normalize(), frente);
    }

    static void Enquadrar3d(View3D vista, Caixa3d caixa)
    {
        var crop = vista.CropBox;
        var inverso = crop.Transform.Inverse;
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
        double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        foreach (var x in new[] { caixa.Min.X, caixa.Max.X })
        foreach (var y in new[] { caixa.Min.Y, caixa.Max.Y })
        foreach (var z in new[] { caixa.Min.Z, caixa.Max.Z })
        {
            var ponto = inverso.OfPoint(new XYZ(x, y, z));
            x0 = Math.Min(x0, ponto.X);
            y0 = Math.Min(y0, ponto.Y);
            z0 = Math.Min(z0, ponto.Z);
            x1 = Math.Max(x1, ponto.X);
            y1 = Math.Max(y1, ponto.Y);
            z1 = Math.Max(z1, ponto.Z);
        }

        var folga = Pes(0.8);
        crop.Min = new XYZ(x0 - folga, y0 - folga, z0 - folga);
        crop.Max = new XYZ(x1 + folga, y1 + folga, z1 + folga);
        vista.CropBox = crop;
        vista.CropBoxActive = true;
        vista.CropBoxVisible = false;
    }

    static void Retangulo(Document doc, View planta, Caixa3d caixa)
    {
        var cantos = new[]
        {
            new XYZ(caixa.Min.X, caixa.Min.Y, 0),
            new XYZ(caixa.Max.X, caixa.Min.Y, 0),
            new XYZ(caixa.Max.X, caixa.Max.Y, 0),
            new XYZ(caixa.Min.X, caixa.Max.Y, 0),
        };
        var grafico = new OverrideGraphicSettings();
        grafico.SetProjectionLineColor(new Color(198, 40, 40));
        grafico.SetProjectionLineWeight(3);
        for (var i = 0; i < cantos.Length; i++)
        {
            var linha = doc.Create.NewDetailCurve(planta, Line.CreateBound(cantos[i], cantos[(i + 1) % cantos.Length]));
            planta.SetElementOverrides(linha.Id, grafico);
        }
    }

    static FamilyInstance? PintarCritico(Document doc, View planta, View3D iso, IReadOnlyList<FamilyInstance> bicos, ResultadoGravado? calculo)
    {
        if (calculo?.ChuveiroCritico is not { Length: > 0 } id) return null;
        var dado = calculo.Chuveiros.FirstOrDefault(c => c.Id == id);
        var bico = dado is null ? null : bicos.FirstOrDefault(b => b.Id.Value == dado.Elemento);
        if (bico is null) return null;

        var grafico = new OverrideGraphicSettings();
        grafico.SetProjectionLineColor(new Color(198, 40, 40));
        grafico.SetProjectionLineWeight(8);
        var solido = Solido(doc);
        if (solido != ElementId.InvalidElementId)
        {
            grafico.SetSurfaceForegroundPatternId(solido);
            grafico.SetSurfaceForegroundPatternColor(new Color(198, 40, 40));
        }

        planta.SetElementOverrides(bico.Id, grafico);
        iso.SetElementOverrides(bico.Id, grafico);
        if (bico.Location is LocationPoint ponto)
        {
            var texto = dado is null
                ? "Bico crítico"
                : $"Bico crítico\n{dado.PressaoKPa / 100:0.00} bar\n{dado.VazaoLmin:0} L/min";
            Nota(doc, planta, ponto.Point + new XYZ(Pes(0.4), Pes(0.4), 0), texto);
        }

        return bico;
    }

    static ElementId Solido(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
            .FirstOrDefault(p => p.GetFillPattern()?.IsSolidFill == true)?.Id
        ?? ElementId.InvalidElementId;

    static string Texto(ResultadoGravado? calculo, int bicos, FamilyInstance? critico, IReadOnlyList<Pipe> tubos)
    {
        var diametro = tubos
            .Select(t => t.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble())
            .FirstOrDefault(d => d is > 0);
        var linhaDiametro = diametro is > 0
            ? $"Diâmetro dos tubos: {UnitUtils.ConvertFromInternalUnits(diametro.Value, UnitTypeId.Millimeters):0} mm"
            : "";

        if (calculo is null)
            return $"Malha gerada: {bicos} bicos\n{linhaDiametro}\nRode Calcular para marcar a área de operação.";

        var criticoTxt = critico is null ? calculo.ChuveiroCritico : "destaque em vermelho";
        return $"""
            {(calculo.Atende ? "ATENDE" : "NÃO ATENDE")}
            Área de operação: {calculo.DaArea.Count()} bicos
            Bico crítico: {criticoTxt}
            Fonte: {calculo.PressaoNecessariaKPa / 100:0.00} bar, {calculo.VazaoChuveirosLmin:0} L/min
            {linhaDiametro}
            """;
    }

    static void Nota(Document doc, View vista, XYZ posicao, string texto)
    {
        var tipo = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        if (tipo == ElementId.InvalidElementId || string.IsNullOrWhiteSpace(texto)) return;
        var quebras = texto.Trim().Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r");
        // Largura no papel, em pés. Zero fica abaixo do mínimo interno e o Revit recusa a nota.
        TextNote.Create(doc, vista.Id, posicao, 0.4, quebras, new TextNoteOptions(tipo)
        {
            HorizontalAlignment = HorizontalTextAlignment.Left,
        });
    }

    static ElementId Folha(Document doc, string numero, string nome, ElementId carimbo, View vista, string texto)
    {
        var folha = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
            .FirstOrDefault(s => s.SheetNumber == numero);
        if (folha is null)
        {
            folha = ViewSheet.Create(doc, carimbo);
            folha.SheetNumber = numero;
        }
        else
        {
            LimparFolha(doc, folha);
        }

        folha.Name = nome;
        Soltar(doc, vista);
        doc.Regenerate();
        if (Viewport.CanAddViewToSheet(doc, folha.Id, vista.Id))
            Viewport.Create(doc, folha.Id, vista.Id, Centro(folha));
        Nota(doc, folha, Canto(folha), texto);
        return folha.Id;
    }

    static void LimparFolha(Document doc, ViewSheet folha)
    {
        var vistas = folha.GetAllViewports();
        if (vistas.Count > 0) doc.Delete(vistas);
        var notas = new FilteredElementCollector(doc, folha.Id).OfClass(typeof(TextNote)).ToElementIds();
        if (notas.Count > 0) doc.Delete(notas);
    }

    static void LimparAnotacoes(Document doc, View vista)
    {
        var curvas = new FilteredElementCollector(doc, vista.Id).OfClass(typeof(CurveElement)).Cast<CurveElement>()
            .Where(c => c.ViewSpecific).Select(c => c.Id).ToList();
        if (curvas.Count > 0) doc.Delete(curvas);
        var notas = new FilteredElementCollector(doc, vista.Id).OfClass(typeof(TextNote)).ToElementIds();
        if (notas.Count > 0) doc.Delete(notas);
    }

    static void LimparPintura(View vista, IEnumerable<Element> elementos)
    {
        var vazio = new OverrideGraphicSettings();
        foreach (var elemento in elementos)
            vista.SetElementOverrides(elemento.Id, vazio);
    }

    static void Soltar(Document doc, View vista)
    {
        var viewport = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>()
            .FirstOrDefault(v => v.ViewId == vista.Id);
        if (viewport is not null) doc.Delete(viewport.Id);
    }

    static XYZ Centro(ViewSheet folha)
    {
        var contorno = folha.Outline;
        var largura = contorno.Max.U - contorno.Min.U;
        var altura = contorno.Max.V - contorno.Min.V;
        if (largura < 0.2 || altura < 0.2) return new XYZ(1.5, 1.05, 0);
        return new XYZ((contorno.Min.U + contorno.Max.U) / 2, contorno.Min.V + altura * 0.58, 0);
    }

    static XYZ Canto(ViewSheet folha)
    {
        var contorno = folha.Outline;
        var largura = contorno.Max.U - contorno.Min.U;
        var altura = contorno.Max.V - contorno.Min.V;
        if (largura < 0.2 || altura < 0.2) return new XYZ(0.25, 0.35, 0);
        return new XYZ(contorno.Min.U + largura * 0.08, contorno.Min.V + altura * 0.28, 0);
    }

    static ElementId Carimbo(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsElementType()
            .ToElementIds()
            .FirstOrDefault()
        ?? ElementId.InvalidElementId;

    static T? Vista<T>(Document doc, string nome) where T : View =>
        new FilteredElementCollector(doc).OfClass(typeof(T)).Cast<T>()
            .FirstOrDefault(v => !v.IsTemplate && v.Name == nome);

    static ViewFamilyType Tipo(Document doc, ViewFamily familia) =>
        new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(t => t.ViewFamily == familia)
        ?? throw new InvalidOperationException($"O projeto não tem um tipo de vista {familia}.");

    static Caixa3d? Caixa(IEnumerable<Element> elementos)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
        double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        var algum = false;
        foreach (var elemento in elementos)
        {
            var caixa = elemento.get_BoundingBox(null);
            if (caixa is null) continue;
            algum = true;
            x0 = Math.Min(x0, caixa.Min.X);
            y0 = Math.Min(y0, caixa.Min.Y);
            z0 = Math.Min(z0, caixa.Min.Z);
            x1 = Math.Max(x1, caixa.Max.X);
            y1 = Math.Max(y1, caixa.Max.Y);
            z1 = Math.Max(z1, caixa.Max.Z);
        }

        return algum ? new Caixa3d(new XYZ(x0, y0, z0), new XYZ(x1, y1, z1)) : null;
    }

    static Caixa3d? CaixaNoPlano(IEnumerable<Element> elementos)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
        double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        var algum = false;
        void Acumular(XYZ ponto)
        {
            algum = true;
            x0 = Math.Min(x0, ponto.X);
            y0 = Math.Min(y0, ponto.Y);
            z0 = Math.Min(z0, ponto.Z);
            x1 = Math.Max(x1, ponto.X);
            y1 = Math.Max(y1, ponto.Y);
            z1 = Math.Max(z1, ponto.Z);
        }

        foreach (var elemento in elementos)
        {
            switch (elemento.Location)
            {
                case LocationPoint ponto:
                    Acumular(ponto.Point);
                    break;
                case LocationCurve curva:
                    Acumular(curva.Curve.GetEndPoint(0));
                    Acumular(curva.Curve.GetEndPoint(1));
                    break;
            }
        }

        return algum ? new Caixa3d(new XYZ(x0, y0, z0), new XYZ(x1, y1, z1)) : null;
    }

    static Caixa3d Expandir(Caixa3d caixa, DadosEsboco dados)
    {
        var mx = Pes(Math.Max(0.5, dados.EspacamentoRamalM / 2));
        var my = Pes(Math.Max(0.5, dados.EspacamentoEntreRamaisM / 2));
        return new Caixa3d(
            new XYZ(caixa.Min.X - mx, caixa.Min.Y - my, caixa.Min.Z),
            new XYZ(caixa.Max.X + mx, caixa.Max.Y + my, caixa.Max.Z));
    }

    static Caixa3d Unir(Caixa3d a, Caixa3d b, double margem) => new(
        new XYZ(Math.Min(a.Min.X, b.Min.X) - margem, Math.Min(a.Min.Y, b.Min.Y) - margem, Math.Min(a.Min.Z, b.Min.Z) - margem),
        new XYZ(Math.Max(a.Max.X, b.Max.X) + margem, Math.Max(a.Max.Y, b.Max.Y) + margem, Math.Max(a.Max.Z, b.Max.Z) + margem));

    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);

    readonly record struct Caixa3d(XYZ Min, XYZ Max)
    {
        public XYZ Centro => (Min + Max) / 2;
    }
}
