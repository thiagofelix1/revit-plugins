using Spk.Motor.Modelo;

namespace Spk.AddIn.Folhas;

/// <summary>Um setor (o que uma VGA alimenta): bicos, tubos, cor e o contorno em planta por nível, em pés.</summary>
sealed record SetorLido(string Nome, Color Cor, List<FamilyInstance> Bicos, List<MEPCurve> Tubos, XYZ Direcao,
    Dictionary<ElementId, List<List<XYZ>>> Contornos);

/// <summary>O que os geradores de folha compartilham numa rodada. Os setores são lidos uma vez só, quando alguém pede.</summary>
sealed class ContextoFolhas(Document doc)
{
    List<SetorLido>? setores;
    readonly List<string> avisos = [];

    public Document Doc => doc;
    public IReadOnlyList<SetorLido> Setores => setores ??= LeituraSetores.Ler(doc, avisos);
    public IReadOnlyList<string> AvisosDosSetores => avisos;
}

/// <summary>Leitura dos setores pelo mesmo leitor do cálculo, uma VGA de cada vez, e o desenho da hachura de setor.</summary>
static class LeituraSetores
{
    /// <summary>VGA 1 azul, 2 verde, 3 magenta, 4 amarelo, 5 laranja, como na NEPN-INC-009; depois outras cores distintas.</summary>
    static readonly Color[] Paleta =
    [
        new(0, 112, 192), new(0, 166, 81), new(204, 0, 204), new(230, 190, 0), new(237, 125, 49),
        new(0, 176, 200), new(150, 30, 30), new(112, 48, 160), new(140, 100, 40), new(90, 90, 90),
    ];

    const string PadraoSetor = "SPK setor";

    public static List<SetorLido> Ler(Document doc, List<string> avisos)
    {
        var leitura = BicosDoModelo.Ler(doc);
        var vgas = leitura.Gerados ? [] : MapeamentoGravado.Vgas(doc);
        var lidas = new List<LeitorRede.RedeLida>();
        if (vgas.Count == 0)
        {
            lidas.Add(LeitorRede.Montar(doc, leitura.Bicos, 120));
        }
        else
        {
            var ids = vgas.Select(v => v.Id).ToList();
            foreach (var vga in vgas)
            {
                try
                {
                    lidas.Add(LeitorRede.Montar(doc, leitura.Bicos, 120, vga, ids));
                }
                catch (InvalidOperationException e)
                {
                    avisos.Add($"{LeitorRede.NomeDaVga(vga)}: {e.Message}");
                }
            }
        }

        var niveis = Niveis(doc);
        var nomes = new HashSet<string>(StringComparer.Ordinal);
        var setores = new List<SetorLido>();
        foreach (var lida in lidas.OrderBy(l => l.Setor, ComparadorNatural.Instancia))
        {
            var nome = lida.Setor;
            for (var k = 2; !nomes.Add(nome); k++) nome = $"{lida.Setor} ({k})";
            var bicos = lida.Bicos.Values.Select(doc.GetElement).OfType<FamilyInstance>().ToList();
            var tubos = lida.Tubos.Values.Distinct().Select(doc.GetElement).OfType<MEPCurve>().ToList();
            var direcao = VistasSpk.DirecaoDosRamais(tubos);
            setores.Add(new SetorLido(nome, Paleta[setores.Count % Paleta.Length], bicos, tubos, direcao,
                ContornosPorNivel(doc, bicos, direcao, niveis)));
        }
        return setores;
    }

    public static List<Level> Niveis(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(n => n.ProjectElevation).ToList();

    /// <summary>O nível do bico no Revit; sem ele, o mais alto abaixo do bico.</summary>
    public static Level? NivelDe(Document doc, FamilyInstance bico, IReadOnlyList<Level> niveis)
    {
        if (doc.GetElement(bico.LevelId) is Level nivel) return nivel;
        if (bico.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM)?.AsElementId() is { } id
            && doc.GetElement(id) is Level doParametro) return doParametro;
        var z = (bico.Location as LocationPoint)?.Point.Z;
        if (z is null || niveis.Count == 0) return niveis.FirstOrDefault();
        return niveis.LastOrDefault(n => n.ProjectElevation <= z + VistasSpk.Pes(0.05)) ?? niveis[0];
    }

    /// <summary>O nível em que está a maioria dos bicos.</summary>
    public static Level NivelDaMaioria(Document doc, IEnumerable<FamilyInstance> bicos)
    {
        var niveis = Niveis(doc);
        return bicos.Select(b => NivelDe(doc, b, niveis)).OfType<Level>()
                   .GroupBy(n => n.Id).OrderByDescending(g => g.Count()).Select(g => g.First()).FirstOrDefault()
               ?? niveis.FirstOrDefault()
               ?? throw new InvalidOperationException("O projeto não tem nível.");
    }

    static Dictionary<ElementId, List<List<XYZ>>> ContornosPorNivel(Document doc, List<FamilyInstance> bicos, XYZ d,
        IReadOnlyList<Level> niveis)
    {
        var t = new XYZ(-d.Y, d.X, 0);
        var contornos = new Dictionary<ElementId, List<List<XYZ>>>();
        foreach (var grupo in bicos.GroupBy(b => NivelDe(doc, b, niveis)?.Id ?? ElementId.InvalidElementId))
        {
            var pontos = grupo.Select(b => (b.Location as LocationPoint)?.Point).OfType<XYZ>()
                .Select(p => (VistasSpk.Metros(p.DotProduct(d)), VistasSpk.Metros(p.DotProduct(t)))).ToList();
            contornos[grupo.Key] = Contorno.DosBicos(pontos)
                .Select(laco => laco.Select(p => d * VistasSpk.Pes(p.U) + t * VistasSpk.Pes(p.V)).ToList())
                .ToList();
        }
        return contornos;
    }

    /// <summary>
    /// Hachura do setor na planta do nível, com o nome no meio do maior laço. <paramref name="cheio" /> pinta sólido, para
    /// destacar o setor da folha no mapa-chave.
    /// </summary>
    public static void Desenhar(Document doc, View vista, SetorLido setor, Level nivel, bool cheio, string? rotulo, IList<string> avisos)
    {
        if (!setor.Contornos.TryGetValue(nivel.Id, out var lacos) || lacos.Count == 0) return;
        try
        {
            var padrao = cheio ? PranchaSpk.Solido(doc) : PranchaSpk.Padrao(doc, PadraoSetor, 4, Math.PI / 4);
            var tipo = PranchaSpk.TipoDeRegiao(doc, $"SPK Setor — {PranchaSpk.NomeSeguro(setor.Nome)}{(cheio ? " (cheio)" : "")}", padrao, setor.Cor);
            PranchaSpk.RegiaoPreenchida(doc, vista, tipo, lacos, nivel);
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            avisos.Add($"Hachura de {setor.Nome}: {e.Message}");
        }

        if (rotulo is null) return;
        var maior = lacos.OrderByDescending(l => Math.Abs(AreaEmPlanta(l))).First();
        var centro = new XYZ(maior.Average(p => p.X), maior.Average(p => p.Y), 0);
        try
        {
            PranchaSpk.Nota(doc, vista, centro, rotulo);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e)
        {
            avisos.Add($"Nome de {setor.Nome}: {e.Message}");
        }
    }

    static double AreaEmPlanta(IReadOnlyList<XYZ> laco) =>
        Contorno.Area(laco.Select(p => (p.X, p.Y)).ToList());

    /// <summary>A caixa de todos os contornos e bicos do nível, para recortar a planta.</summary>
    public static Caixa? CaixaNoNivel(IEnumerable<SetorLido> setores, Level nivel)
    {
        var pontos = setores.SelectMany(s => s.Contornos.TryGetValue(nivel.Id, out var l) ? l.SelectMany(p => p) : []).ToList();
        var bicos = setores.SelectMany(s => s.Bicos).Select(b => b.get_BoundingBox(null)).OfType<BoundingBoxXYZ>().ToList();
        if (pontos.Count == 0) return null;
        var z0 = bicos.Count > 0 ? bicos.Min(b => b.Min.Z) : nivel.ProjectElevation;
        var z1 = bicos.Count > 0 ? bicos.Max(b => b.Max.Z) : nivel.ProjectElevation + VistasSpk.Pes(3);
        return new Caixa(new XYZ(pontos.Min(p => p.X), pontos.Min(p => p.Y), z0), new XYZ(pontos.Max(p => p.X), pontos.Max(p => p.Y), z1));
    }
}

/// <summary>Ordena "VGA 2" antes de "VGA 10".</summary>
sealed class ComparadorNatural : IComparer<string>
{
    public static readonly ComparadorNatural Instancia = new();

    public int Compare(string? a, string? b)
    {
        if (a is null || b is null) return string.CompareOrdinal(a, b);
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                var ia = i; while (i < a.Length && char.IsDigit(a[i])) i++;
                var jb = j; while (j < b.Length && char.IsDigit(b[j])) j++;
                var na = a[ia..i].TrimStart('0');
                var nb = b[jb..j].TrimStart('0');
                if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                var c = string.CompareOrdinal(na, nb);
                if (c != 0) return c;
            }
            else
            {
                var c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
