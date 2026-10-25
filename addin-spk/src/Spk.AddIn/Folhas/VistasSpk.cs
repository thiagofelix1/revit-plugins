namespace Spk.AddIn.Folhas;

/// <summary>Criação e preparo de vistas para as folhas. Tudo em pés (unidade interna do Revit), exceto onde o nome diz metros.</summary>
static class VistasSpk
{
    public const string TemplatePlanta = "SPK Planta";
    public const string Template3d = "SPK 3D";
    public const string TemplateCorte = "SPK Corte";
    public const string TemplateSetorizacao = "SPK Setorização";

    static readonly int[] Escalas = [50, 75, 100, 125, 150, 200, 250, 300, 400, 500, 750, 1000, 1500, 2000];

    static readonly BuiltInCategory[] CategoriasDaRede =
    [
        BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting,
        BuiltInCategory.OST_PipeAccessory, BuiltInCategory.OST_Sprinklers,
    ];

    public static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
    public static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);
    public static double Milimetros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Millimeters);

    /// <summary>Apaga a vista do plugin com este nome, se existir, para recriar do zero.</summary>
    public static void ApagarSeExistir(Document doc, string nome)
    {
        var ids = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && v.Name == nome).Select(v => v.Id).ToList();
        if (ids.Count > 0) doc.Delete(ids);
    }

    public static ViewFamilyType Tipo(Document doc, ViewFamily familia) =>
        new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(t => t.ViewFamily == familia)
        ?? throw new InvalidOperationException($"O projeto não tem um tipo de vista {familia}.");

    /// <summary>Aplica o template do escritório, se o projeto tiver um com este nome. Devolve se aplicou.</summary>
    public static bool AplicarTemplate(Document doc, View vista, string nome)
    {
        var template = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .FirstOrDefault(v => v.IsTemplate && v.Name == nome);
        if (template is null)
        {
            try { vista.ViewTemplateId = ElementId.InvalidElementId; }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
            return false;
        }

        try
        {
            vista.ViewTemplateId = template.Id;
            return true;
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Estilo padrão quando não há template: tubulação em vermelho, só a rede no 3D.</summary>
    public static void EstiloPadrao(Document doc, View vista, bool soARede)
    {
        try { vista.Discipline = ViewDiscipline.Coordination; }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }

        if (soARede)
        {
            foreach (Category categoria in doc.Settings.Categories)
            {
                if (categoria.CategoryType != CategoryType.Model) continue;
                var mostrar = CategoriasDaRede.Any(c => categoria.Id.Value == (long)c);
                Esconder(vista, categoria.Id, !mostrar);
            }
            // Isométrico de desenho: sem o contorno da caixa de corte, sem níveis e eixos atravessando a rede.
            foreach (var c in new[] { BuiltInCategory.OST_SectionBox, BuiltInCategory.OST_Levels, BuiltInCategory.OST_Grids })
                Esconder(vista, new ElementId(c), true);
        }
        else
        {
            foreach (var c in CategoriasDaRede) Esconder(vista, new ElementId(c), false);
        }

        var vermelho = new OverrideGraphicSettings().SetProjectionLineColor(new Color(204, 0, 0));
        foreach (var c in new[] { BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting })
        {
            try { vista.SetCategoryOverrides(new ElementId(c), vermelho); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        }
    }

    /// <summary>
    /// Esconde tubos, conexões, acessórios, bicos e as marcas de corte, elevação e chamada: o mapa-chave e a setorização
    /// mostram só a hachura sobre a arquitetura.
    /// </summary>
    public static void EsconderRede(View vista)
    {
        if (vista.ViewTemplateId != ElementId.InvalidElementId) return;
        foreach (var c in CategoriasDaRede.Concat([BuiltInCategory.OST_Sections, BuiltInCategory.OST_Elev, BuiltInCategory.OST_Callouts]))
            Esconder(vista, new ElementId(c), true);
    }

    static void Esconder(View vista, ElementId categoria, bool esconder)
    {
        try
        {
            if (vista.CanCategoryBeHidden(categoria)) vista.SetCategoryHidden(categoria, esconder);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }
    }

    /// <summary>A menor escala da lista em que a extensão (em pés) cabe no espaço de papel (em mm).</summary>
    public static int EscalaQueCabe(double larguraPes, double alturaPes, double larguraPapelMm, double alturaPapelMm)
    {
        var larguraMm = Milimetros(larguraPes);
        var alturaMm = Milimetros(alturaPes);
        foreach (var escala in Escalas)
            if (larguraMm / escala <= larguraPapelMm && alturaMm / escala <= alturaPapelMm)
                return escala;
        return Escalas[^1];
    }

    /// <summary>Planta no nível, recortada na caixa (em planta) e com o plano de corte logo acima da rede.</summary>
    public static ViewPlan Planta(Document doc, string nome, Level nivel, Caixa caixa, string template, IList<string> avisos)
    {
        ApagarSeExistir(doc, nome);
        var planta = ViewPlan.Create(doc, Tipo(doc, ViewFamily.FloorPlan).Id, nivel.Id);
        planta.Name = nome;
        if (!AplicarTemplate(doc, planta, template))
        {
            EstiloPadrao(doc, planta, soARede: false);
            planta.DetailLevel = ViewDetailLevel.Medium;
        }

        AjustarIntervalo(planta, nivel, caixa, avisos);
        Recortar(planta, caixa);
        return planta;
    }

    /// <summary>
    /// Corte da planta 0,30 m acima do ponto mais alto da rede, contado a partir do nível da vista. Se a rede fica abaixo
    /// do nível (bico lançado no nível de cima com deslocamento negativo), o fundo desce até ela.
    /// </summary>
    static void AjustarIntervalo(ViewPlan planta, Level nivel, Caixa caixa, IList<string> avisos)
    {
        try
        {
            var corte = Math.Max(caixa.Max.Z - nivel.ProjectElevation + Pes(0.30), Pes(1.2));
            var intervalo = planta.GetViewRange();
            intervalo.SetLevelId(PlanViewPlane.TopClipPlane, nivel.Id);
            intervalo.SetOffset(PlanViewPlane.TopClipPlane, corte + Pes(1.0));
            intervalo.SetLevelId(PlanViewPlane.CutPlane, nivel.Id);
            intervalo.SetOffset(PlanViewPlane.CutPlane, corte);
            intervalo.SetLevelId(PlanViewPlane.BottomClipPlane, nivel.Id);
            var fundo = Math.Min(0, caixa.Min.Z - nivel.ProjectElevation - Pes(0.30));
            intervalo.SetOffset(PlanViewPlane.BottomClipPlane, fundo);
            intervalo.SetLevelId(PlanViewPlane.ViewDepthPlane, nivel.Id);
            intervalo.SetOffset(PlanViewPlane.ViewDepthPlane, fundo);
            planta.SetViewRange(intervalo);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e)
        {
            avisos.Add("O intervalo de vista da planta ficou o do tipo de vista: " + e.Message);
        }
    }

    public static void Recortar(View vista, Caixa caixa)
    {
        var crop = vista.CropBox;
        var inverso = crop.Transform.Inverse;
        var a = inverso.OfPoint(new XYZ(caixa.Min.X, caixa.Min.Y, 0));
        var b = inverso.OfPoint(new XYZ(caixa.Max.X, caixa.Max.Y, 0));
        crop.Min = new XYZ(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), crop.Min.Z);
        crop.Max = new XYZ(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), crop.Max.Z);
        vista.CropBox = crop;
        vista.CropBoxActive = true;
        vista.CropBoxVisible = false;
    }

    /// <summary>Isométrico com caixa de corte em volta da rede, só tubulação e bicos, em linha simples.</summary>
    public static View3D Isometrico(Document doc, string nome, Caixa caixa, string template)
    {
        ApagarSeExistir(doc, nome);
        var vista = View3D.CreateIsometric(doc, Tipo(doc, ViewFamily.ThreeDimensional).Id);
        vista.Name = nome;
        if (!AplicarTemplate(doc, vista, template))
        {
            EstiloPadrao(doc, vista, soARede: true);
            // Médio: tubo continua em linha simples, e o bico aparece; muitas famílias de sprinkler escondem a geometria no Baixo.
            vista.DetailLevel = ViewDetailLevel.Medium;
        }

        var centro = caixa.Centro;
        var distancia = Math.Max((caixa.Max - caixa.Min).GetLength(), Pes(8)) * 2;
        var olho = centro + new XYZ(1, -1, 0.75).Normalize() * distancia;
        vista.SetOrientation(OlharPara(centro, olho));
        vista.SetSectionBox(new BoundingBoxXYZ { Min = caixa.Min, Max = caixa.Max });
        vista.IsSectionBoxActive = true;
        return vista;
    }

    /// <summary>Recorta o 3D em volta da caixa. Chamar depois de Regenerate, quando a orientação já valeu.</summary>
    public static void Recortar3d(View3D vista, Caixa caixa)
    {
        var crop = vista.CropBox;
        var inverso = crop.Transform.Inverse;
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
        double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        foreach (var x in new[] { caixa.Min.X, caixa.Max.X })
        foreach (var y in new[] { caixa.Min.Y, caixa.Max.Y })
        foreach (var z in new[] { caixa.Min.Z, caixa.Max.Z })
        {
            var p = inverso.OfPoint(new XYZ(x, y, z));
            x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z);
            x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
        }

        var folga = Pes(0.8);
        crop.Min = new XYZ(x0 - folga, y0 - folga, z0 - folga);
        crop.Max = new XYZ(x1 + folga, y1 + folga, z1 + folga);
        vista.CropBox = crop;
        vista.CropBoxActive = true;
        vista.CropBoxVisible = false;
    }

    /// <summary>A extensão do recorte do 3D na própria vista (largura, altura), em pés.</summary>
    public static (double Largura, double Altura) Extensao(View vista)
    {
        var crop = vista.CropBox;
        return (crop.Max.X - crop.Min.X, crop.Max.Y - crop.Min.Y);
    }

    public static ViewOrientation3D OlharPara(XYZ centro, XYZ olho)
    {
        var frente = (centro - olho).Normalize();
        var cima = XYZ.BasisZ - frente * frente.DotProduct(XYZ.BasisZ);
        if (cima.GetLength() < 1e-6) cima = XYZ.BasisY;
        return new ViewOrientation3D(olho, cima.Normalize(), frente);
    }

    /// <summary>
    /// Corte que olha no sentido dos ramais (direção <paramref name="d" />, horizontal), para descidas e subgeral
    /// aparecerem de perfil. A caixa cobre o setor inteiro em profundidade. Eixos da caixa em regra da mão direita.
    /// </summary>
    public static ViewSection Corte(Document doc, string nome, Caixa caixa, XYZ d, string template)
    {
        ApagarSeExistir(doc, nome);
        var direcao = new XYZ(d.X, d.Y, 0).Normalize();
        var basisZ = direcao.Negate();
        var basisY = XYZ.BasisZ;
        var basisX = basisY.CrossProduct(basisZ);

        var cantos = caixa.Cantos().ToList();
        double Proj(XYZ p, XYZ eixo) => p.DotProduct(eixo);
        var transversal = cantos.Select(p => Proj(p, basisX)).ToList();
        var longitudinal = cantos.Select(p => Proj(p, direcao)).ToList();
        var margem = Pes(1.5);

        var origem = basisX * ((transversal.Min() + transversal.Max()) / 2)
                     + direcao * (longitudinal.Min() - margem)
                     + XYZ.BasisZ * ((caixa.Min.Z + caixa.Max.Z) / 2);
        var transformacao = Transform.Identity;
        transformacao.Origin = origem;
        transformacao.BasisX = basisX;
        transformacao.BasisY = basisY;
        transformacao.BasisZ = basisZ;

        var meiaLargura = (transversal.Max() - transversal.Min()) / 2 + margem;
        var meiaAltura = (caixa.Max.Z - caixa.Min.Z) / 2 + margem;
        var profundidade = longitudinal.Max() - longitudinal.Min() + 2 * margem;
        var caixaCorte = new BoundingBoxXYZ
        {
            Enabled = true,
            Transform = transformacao,
            Min = new XYZ(-meiaLargura, -meiaAltura, -profundidade),
            Max = new XYZ(meiaLargura, meiaAltura, 0),
        };

        var corte = ViewSection.CreateSection(doc, Tipo(doc, ViewFamily.Section).Id, caixaCorte);
        corte.Name = nome;
        if (!AplicarTemplate(doc, corte, template))
        {
            EstiloPadrao(doc, corte, soARede: false);
            corte.DetailLevel = ViewDetailLevel.Medium;
        }
        return corte;
    }

    /// <summary>Direção dos ramais: o eixo horizontal com mais comprimento de tubo (1 m ou mais), o mesmo critério do cálculo.</summary>
    public static XYZ DirecaoDosRamais(IEnumerable<Element> tubos)
    {
        var segmentos = tubos
            .Select(t => (t.Location as LocationCurve)?.Curve as Line)
            .OfType<Line>()
            .Select(l => l.GetEndPoint(1) - l.GetEndPoint(0))
            .Where(v => Math.Sqrt(v.X * v.X + v.Y * v.Y) >= Pes(1))
            .Select(v => (v.X, v.Y));
        var (dx, dy) = Spk.Motor.Modelo.Topologia.EixoDominante(segmentos);
        return new XYZ(dx, dy, 0);
    }

    public static Caixa? CaixaDe(IEnumerable<Element> elementos)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
        double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        var algum = false;
        foreach (var elemento in elementos)
        {
            if (elemento.get_BoundingBox(null) is not { } c) continue;
            algum = true;
            x0 = Math.Min(x0, c.Min.X); y0 = Math.Min(y0, c.Min.Y); z0 = Math.Min(z0, c.Min.Z);
            x1 = Math.Max(x1, c.Max.X); y1 = Math.Max(y1, c.Max.Y); z1 = Math.Max(z1, c.Max.Z);
        }
        return algum ? new Caixa(new XYZ(x0, y0, z0), new XYZ(x1, y1, z1)) : null;
    }
}

readonly record struct Caixa(XYZ Min, XYZ Max)
{
    public XYZ Centro => (Min + Max) / 2;

    public Caixa Expandir(double folga, double folgaZ = 0) => new(
        new XYZ(Min.X - folga, Min.Y - folga, Min.Z - folgaZ),
        new XYZ(Max.X + folga, Max.Y + folga, Max.Z + folgaZ));

    public IEnumerable<XYZ> Cantos()
    {
        foreach (var x in new[] { Min.X, Max.X })
        foreach (var y in new[] { Min.Y, Max.Y })
        foreach (var z in new[] { Min.Z, Max.Z })
            yield return new XYZ(x, y, z);
    }
}
