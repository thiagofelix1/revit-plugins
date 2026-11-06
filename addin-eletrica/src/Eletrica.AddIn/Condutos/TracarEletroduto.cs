using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Padrao;

namespace Eletrica.AddIn.Condutos;

/// <summary>
///     Traçar eletroduto (itens 4.1 e 4.2 do plano): entre dois elementos (ponto, quadro, caixa) ou de um elemento até um
///     eletroduto existente, pelo caminho ortogonal: sobe (ou desce) até a cota de passagem do padrão, anda em L na planta e
///     desce até o destino. Tipo e DN do padrão. As pontas se ligam aos conectores de eletroduto das famílias quando há;
///     senão o eletroduto termina no ponto da família, e o grafo o prende por proximidade. Ao chegar num eletroduto, ele é
///     partido e ganha um tê. Curvas pelas conexões do tipo; sem conexões, as pontas se ligam direto.
/// </summary>
static class TracarEletroduto
{
    public sealed record Opcoes(ConduitType Tipo, int DnMm, AlturaDePassagem Passagem, double CotaTeto, double CotaPiso);

    /// <summary>Onde o eletroduto começa ou termina num elemento: o conector de eletroduto livre, senão o ponto de inserção.</summary>
    public sealed record Extremo(Element Elemento, XYZ Ponto, Connector? Conector);

    static readonly double Curto = Unidades.DeMetros(0.02);

    /// <summary>
    ///     Menor trecho entre duas curvas (ou entre a curva e o ponto) que deixa espaço para a conexão de um DN pequeno. Abaixo
    ///     disso o Revit recusa: "não há espaço suficiente para criar as conexões necessárias".
    /// </summary>
    static readonly double Minimo = Unidades.DeMetros(0.30);
    static readonly double MargemPonta = Unidades.DeMetros(0.15);

    public static Opcoes OpcoesDoPadrao(Document doc, PadraoEletrica padrao)
    {
        var tipos = new FilteredElementCollector(doc).OfClass(typeof(ConduitType)).Cast<ConduitType>().ToList();
        if (tipos.Count == 0) throw new InvalidOperationException("O projeto não tem tipo de eletroduto (Conduit Type). Carregue o template elétrico ou crie um tipo.");
        var tipo = tipos.FirstOrDefault(t => t.Name == padrao.TipoEletroduto) ?? tipos.OrderByDescending(t => t.IsWithFitting).First();
        return new Opcoes(tipo, padrao.DnPadrao, padrao.Passagem, padrao.CotaTeto, padrao.CotaPiso);
    }

    public static Extremo? ExtremoDe(Element e)
    {
        if (e is FamilyInstance fi)
        {
            if (fi.MEPModel?.ConnectorManager is { } cm)
            {
                var livre = cm.Connectors.Cast<Connector>().FirstOrDefault(k => k.Domain == Domain.DomainCableTrayConduit && GrafoDeCondutos.TemPosicao(k) && !k.IsConnected);
                if (livre is not null) return new Extremo(e, livre.Origin, livre);
            }
            if ((fi.Location as LocationPoint)?.Point is { } p) return new Extremo(e, p, null);
            if (fi.get_BoundingBox(null) is { } caixa) return new Extremo(e, (caixa.Min + caixa.Max) / 2, null);
        }
        return null;
    }

    /// <summary>Eletrodutos entre dois extremos; devolve os criados. Lança se o Revit recusar.</summary>
    public static List<Conduit> Entre(Document doc, Opcoes o, Level nivel, Extremo de, Extremo para)
    {
        var vertices = Vertices(o, nivel, de.Ponto, para.Ponto);
        return Criar(doc, o, nivel, vertices, de.Conector, para.Conector);
    }

    /// <summary>
    ///     Liga um extremo a um eletroduto existente: sobe à cota do eletroduto, anda perpendicular até o eixo dele, parte o
    ///     eletroduto ali e põe um tê (ou, perto de uma ponta livre, uma curva).
    /// </summary>
    public static List<Conduit> AteEletroduto(Document doc, Opcoes o, Level nivel, Extremo de, Conduit alvo)
    {
        if (alvo.Location is not LocationCurve { Curve: Line eixo }) throw new InvalidOperationException("o eletroduto de destino não é reto.");
        var a = eixo.GetEndPoint(0);
        var b = eixo.GetEndPoint(1);
        var d = eixo.Direction;
        // Pé da perpendicular do ponto de partida (levado à cota do eixo quando o eixo é horizontal).
        var origem = Math.Abs(d.Z) < 0.05 ? new XYZ(de.Ponto.X, de.Ponto.Y, a.Z) : de.Ponto;
        var t = (origem - a).DotProduct(d);
        var comprimento = eixo.Length;
        var conectores = alvo.ConnectorManager.Connectors.Cast<Connector>().Where(k => k.Domain == Domain.DomainCableTrayConduit).ToList();

        // Perto de uma ponta livre: entra pela ponta, com curva.
        if (t <= MargemPonta || t >= comprimento - MargemPonta)
        {
            var ponta = t <= MargemPonta ? a : b;
            var conector = conectores.OrderBy(k => k.Origin.DistanceTo(ponta)).First();
            if (!conector.IsConnected)
            {
                var caminho = Vertices(o, nivel, de.Ponto, ponta, cotaFixa: Math.Abs(d.Z) < 0.05 ? a.Z : null);
                return Criar(doc, o, nivel, caminho, de.Conector, conector);
            }
            t = Math.Max(MargemPonta, Math.Min(comprimento - MargemPonta, t));
        }
        var pe = a + d * t;

        // Parte o eletroduto em dois no pé: o original fica de a até o pé; o novo vai do pé até b, herdando a ligação da ponta b.
        var conectorB = conectores.OrderBy(k => k.Origin.DistanceTo(b)).First();
        var ligadoEmB = conectorB.AllRefs.Cast<Connector>().FirstOrDefault(k => k.Owner.Id != alvo.Id && k.ConnectorType != ConnectorType.Logical);
        if (ligadoEmB is not null) conectorB.DisconnectFrom(ligadoEmB);
        ((LocationCurve)alvo.Location).Curve = Line.CreateBound(a, pe);
        var segundo = Conduit.Create(doc, alvo.GetTypeId(), pe, b, nivel.Id);
        CopiarDiametro(alvo, segundo);
        doc.Regenerate();
        if (ligadoEmB is not null)
        {
            var pontaB = Conectores(segundo).OrderBy(k => k.Origin.DistanceTo(b)).First();
            try { pontaB.ConnectTo(ligadoEmB); }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { }
        }

        // O ramo: do ponto de partida até o pé, chegando perpendicular ao eixo.
        var ramo = Vertices(o, nivel, de.Ponto, pe, cotaFixa: Math.Abs(d.Z) < 0.05 ? pe.Z : null);
        var novos = Criar(doc, o, nivel, ramo, de.Conector, null);
        novos.Add(segundo);
        doc.Regenerate();

        var c1 = Conectores(alvo).OrderBy(k => k.Origin.DistanceTo(pe)).First();
        var c2 = Conectores(segundo).OrderBy(k => k.Origin.DistanceTo(pe)).First();
        var ultimo = novos[novos.Count - 2];
        var c3 = Conectores(ultimo).OrderBy(k => k.Origin.DistanceTo(pe)).First();
        if (!Te(doc, c1, c2, c3))
            throw new InvalidOperationException("o Revit não inseriu o tê: confira se o tipo de eletroduto tem conexão de tê nas preferências de roteamento.");
        return novos;
    }

    /// <summary>Vértices do caminho ortogonal: sobe/desce à cota de passagem, L em planta, desce/sobe ao destino.</summary>
    public static List<XYZ> Vertices(Opcoes o, Level nivel, XYZ a, XYZ b, double? cotaFixa = null)
    {
        var z = cotaFixa ?? o.Passagem switch
        {
            AlturaDePassagem.Teto => nivel.ProjectElevation + Unidades.DeMetros(o.CotaTeto),
            AlturaDePassagem.Piso => nivel.ProjectElevation + Unidades.DeMetros(o.CotaPiso),
            _ => Math.Max(a.Z, b.Z),
        };
        // Pontos na mesma vertical (tomada sob o interruptor): reto, sem subir à passagem e descer de novo por cima do
        // eletroduto que já chega ao de cima.
        if (cotaFixa is null && new XYZ(b.X - a.X, b.Y - a.Y, 0).GetLength() < Minimo && Math.Abs(b.Z - a.Z) > Curto)
            return [a, b];
        // Ponto quase na cota de passagem (luminária no teto com passagem a 10 cm dela): a descida seria curta demais para a
        // curva. O eletroduto corre na cota do próprio ponto, como na laje sobre a luminária.
        if (cotaFixa is null)
        {
            if (Math.Abs(a.Z - z) > Curto && Math.Abs(a.Z - z) < Minimo) z = a.Z;
            else if (Math.Abs(b.Z - z) > Curto && Math.Abs(b.Z - z) < Minimo) z = b.Z;
        }
        var pontos = new List<XYZ> { a };
        if (Math.Abs(a.Z - z) > Curto) pontos.Add(new XYZ(a.X, a.Y, z));
        var dx = Math.Abs(b.X - a.X);
        var dy = Math.Abs(b.Y - a.Y);
        // Em L quando as duas pernas cabem uma curva; com uma perna curta demais, reto na diagonal em planta.
        if (dx > Curto && dy > Curto && Math.Min(dx, dy) >= Minimo)
            pontos.Add(dx >= dy ? new XYZ(b.X, a.Y, z) : new XYZ(a.X, b.Y, z));
        if (dx > Curto || dy > Curto) pontos.Add(new XYZ(b.X, b.Y, z));
        if (Math.Abs(b.Z - z) > Curto || pontos[pontos.Count - 1].DistanceTo(b) > Curto) pontos.Add(b);
        return Limpar(pontos);
    }

    static List<XYZ> Limpar(List<XYZ> pontos)
    {
        var limpos = new List<XYZ>();
        foreach (var p in pontos)
            if (limpos.Count == 0 || limpos[limpos.Count - 1].DistanceTo(p) > Curto) limpos.Add(p);
        // tira vértices colineares
        for (var i = limpos.Count - 2; i >= 1; i--)
        {
            var u = (limpos[i] - limpos[i - 1]).Normalize();
            var v = (limpos[i + 1] - limpos[i]).Normalize();
            if (u.DotProduct(v) > 0.9999) limpos.RemoveAt(i);
        }
        return limpos;
    }

    /// <summary>Cria os eletrodutos da polilinha, liga uns aos outros (curva da conexão ou direto) e às pontas pedidas.</summary>
    public static List<Conduit> Criar(Document doc, Opcoes o, Level nivel, List<XYZ> vertices, Connector? inicio, Connector? fim)
    {
        if (vertices.Count < 2) throw new InvalidOperationException("os dois pontos estão no mesmo lugar.");
        var conduites = new List<Conduit>();
        for (var i = 1; i < vertices.Count; i++)
        {
            var c = Conduit.Create(doc, o.Tipo.Id, vertices[i - 1], vertices[i], nivel.Id);
            Diametro(c, o.DnMm);
            conduites.Add(c);
        }
        doc.Regenerate();
        for (var i = 1; i < conduites.Count; i++)
        {
            var p = vertices[i];
            var c1 = Conectores(conduites[i - 1]).OrderBy(k => k.Origin.DistanceTo(p)).First();
            var c2 = Conectores(conduites[i]).OrderBy(k => k.Origin.DistanceTo(p)).First();
            Curva(doc, c1, c2);
        }
        if (inicio is not null) Ligar(Conectores(conduites[0]).OrderBy(k => k.Origin.DistanceTo(vertices[0])).First(), inicio);
        if (fim is not null) Ligar(Conectores(conduites[conduites.Count - 1]).OrderBy(k => k.Origin.DistanceTo(vertices[vertices.Count - 1])).First(), fim);
        return conduites;
    }

    static void Curva(Document doc, Connector c1, Connector c2)
    {
        try { doc.Create.NewElbowFitting(c1, c2); }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            Ligar(c1, c2);
        }
    }

    static bool Te(Document doc, Connector a, Connector b, Connector ramo)
    {
        (Connector, Connector, Connector)[] ordens = [(a, b, ramo), (b, a, ramo), (a, ramo, b), (ramo, a, b), (b, ramo, a), (ramo, b, a)];
        foreach (var (x, y, z) in ordens)
        {
            try
            {
                doc.Create.NewTeeFitting(x, y, z);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { }
        }
        return false;
    }

    static void Ligar(Connector a, Connector b)
    {
        try { a.ConnectTo(b); }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { }
    }

    public static IEnumerable<Connector> Conectores(Conduit c) =>
        c.ConnectorManager.Connectors.Cast<Connector>().Where(k => k.Domain == Domain.DomainCableTrayConduit && GrafoDeCondutos.TemPosicao(k));

    static void Diametro(Conduit c, int dnMm)
    {
        var p = c.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
        if (p is not { IsReadOnly: false }) return;
        try { p.Set(UnitUtils.ConvertToInternalUnits(dnMm, UnitTypeId.Millimeters)); }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { }
    }

    static void CopiarDiametro(Conduit de, Conduit para)
    {
        var p = para.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
        if (p is not { IsReadOnly: false }) return;
        try { p.Set(de.Diameter); }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { }
    }

    /// <summary>Parte um eletroduto no ponto (dois eletrodutos ponta com ponta, sem ligar); devolve o segundo.</summary>
    public static Conduit Partir(Document doc, Conduit alvo, XYZ ponto, Level nivel)
    {
        if (alvo.Location is not LocationCurve { Curve: Line eixo }) throw new InvalidOperationException("o eletroduto não é reto.");
        var a = eixo.GetEndPoint(0);
        var b = eixo.GetEndPoint(1);
        var conectorB = Conectores(alvo).OrderBy(k => k.Origin.DistanceTo(b)).First();
        var ligadoEmB = conectorB.AllRefs.Cast<Connector>().FirstOrDefault(k => k.Owner.Id != alvo.Id && k.ConnectorType != ConnectorType.Logical);
        if (ligadoEmB is not null) conectorB.DisconnectFrom(ligadoEmB);
        ((LocationCurve)alvo.Location).Curve = Line.CreateBound(a, ponto);
        var segundo = Conduit.Create(doc, alvo.GetTypeId(), ponto, b, nivel.Id);
        CopiarDiametro(alvo, segundo);
        doc.Regenerate();
        if (ligadoEmB is not null)
        {
            var pontaB = Conectores(segundo).OrderBy(k => k.Origin.DistanceTo(b)).First();
            Ligar(pontaB, ligadoEmB);
        }
        return segundo;
    }
}
