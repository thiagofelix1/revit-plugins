using Autodesk.Revit.DB.Plumbing;
using Spk.AddIn.Folhas;

namespace Spk.AddIn.Modelagem;

/// <summary>O que pintar: o diâmetro e o setor vêm do modelo; a velocidade e a pressão, do último cálculo de cada setor.</summary>
public enum CriterioDeCor { Diametro, Setor, Velocidade, Pressao }

/// <summary>Uma faixa da legenda: o texto, a cor (com o nome dela, para quem lê só o resumo) e quantos elementos ficaram nela.</summary>
public sealed record FaixaDeCor(string Texto, Color Cor, string NomeDaCor, int Elementos);

public sealed record ResultadoCores(CriterioDeCor Criterio, IReadOnlyList<FaixaDeCor> Faixas, int SemValor, bool Legenda)
{
    public string Texto()
    {
        var linhas = Faixas.Select(f => $"• {f.NomeDaCor}: {f.Texto} ({f.Elementos})").ToList();
        if (SemValor > 0)
            linhas.Add(Criterio switch
            {
                CriterioDeCor.Velocidade => $"{SemValor} tubo(s) sem vazão no cálculo (ponta morta, fora do caminho da área de cálculo ou setor não calculado) ficaram com a cor normal.",
                CriterioDeCor.Pressao => $"{SemValor} bico(s) fora da área de cálculo (ou de setor não calculado) ficaram com a cor normal.",
                CriterioDeCor.Setor => $"{SemValor} elemento(s) fora de qualquer setor ficaram com a cor normal.",
                _ => $"{SemValor} elemento(s) ficaram com a cor normal.",
            });
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Pinta a rede na vista aberta, para o projetista ver de relance o que está em cada diâmetro, setor, velocidade ou
///     pressão. É a cor do elemento só nesta vista (substituição gráfica), não um filtro: funciona também em vista com
///     template, e não muda nada no modelo. Na planta, põe uma legenda ao lado da rede. Tudo o que pintou fica anotado no
///     .rvt, para "Tirar cores" desfazer só isso. A cor não acompanha mudanças depois: rode de novo.
/// </summary>
static class CoresDaRede
{
    /// <summary>Cor fixa por DN, a mesma em qualquer projeto: o projetista aprende que DN 25 é azul.</summary>
    static readonly (int Dn, Color Cor, string Nome)[] PorDn =
    [
        (25, new Color(0, 112, 192), "azul"), (32, new Color(0, 166, 81), "verde"), (40, new Color(230, 190, 0), "amarelo"),
        (50, new Color(237, 125, 49), "laranja"), (65, new Color(200, 30, 30), "vermelho"), (80, new Color(204, 0, 204), "magenta"),
        (100, new Color(112, 48, 160), "roxo"), (125, new Color(140, 100, 40), "marrom"), (150, new Color(0, 176, 200), "ciano"),
        (200, new Color(90, 90, 90), "cinza-escuro"),
    ];

    static readonly Color Outra = new(150, 150, 150);

    /// <summary>Do menor para o maior valor.</summary>
    static readonly (Color Cor, string Nome)[] Escala =
    [
        (new Color(120, 180, 230), "azul-claro"), (new Color(0, 166, 81), "verde"), (new Color(230, 190, 0), "amarelo"),
        (new Color(237, 125, 49), "laranja"), (new Color(200, 30, 30), "vermelho"), (new Color(120, 0, 40), "vinho"),
    ];

    /// <summary>Faixas fixas de velocidade (m/s), as mesmas em qualquer projeto, para comparar um setor com outro.</summary>
    static readonly double[] LimitesDeVelocidade = [1, 2, 3, 4, 5];

    const string Chave = "CoresDaRede-";
    const double MmPorPe = 304.8;

    public static ResultadoCores Pintar(Document doc, View vista, CriterioDeCor criterio)
    {
        if (vista.IsTemplate) throw new InvalidOperationException("Abra uma planta, um corte ou um 3D para pintar.");
        var cores = Classificar(doc, vista, criterio, out var semValor);
        if (cores.Count == 0)
            throw new InvalidOperationException(criterio switch
            {
                CriterioDeCor.Velocidade => "Nenhum tubo desta vista tem velocidade calculada. Rode Calcular antes.",
                CriterioDeCor.Pressao => "Nenhum bico desta vista tem pressão calculada. Rode Calcular antes.",
                CriterioDeCor.Setor => "Nenhum bico ou tubo de setor aparece nesta vista.",
                _ => "Nenhum tubo aparece nesta vista.",
            } + (FaseDiferente(doc, vista) ?? (vista is ViewPlan { ViewType: ViewType.FloorPlan }
                ? " Numa planta de piso a rede costuma ficar acima do plano de corte: use a planta de forro ou o 3D, ou suba o corte no intervalo de vista."
                : "")));

        using var t = new Transaction(doc, "SPK — Cores da rede");
        t.Start();
        Limpar(doc, vista);
        var pinturas = new Dictionary<string, OverrideGraphicSettings>();
        foreach (var (elemento, faixa) in cores)
        {
            var chave = $"{faixa.Cor.Red},{faixa.Cor.Green},{faixa.Cor.Blue}";
            if (!pinturas.TryGetValue(chave, out var pintura)) pinturas[chave] = pintura = PranchaSpk.Pintura(doc, faixa.Cor, 6, preencher: true);
            vista.SetElementOverrides(elemento.Id, pintura);
        }
        var faixas = cores.GroupBy(c => c.Faixa.Texto)
            .Select(g => g.First().Faixa with { Elementos = g.Count() })
            .OrderBy(f => Ordem(f, criterio)).ToList();
        var legenda = vista is ViewPlan { GenLevel: not null } planta ? Legenda(doc, planta, criterio, faixas, cores.Select(c => c.Elemento)) : [];
        ArmazenamentoSpk.Gravar(doc, Chave + vista.Id.Value,
            new MarcasDeCor(cores.Select(c => c.Elemento.Id.Value).ToList(), legenda.Select(id => id.Value).ToList()));
        t.Commit();
        return new ResultadoCores(criterio, faixas, semValor, legenda.Count > 0);
    }

    /// <summary>Volta a cor normal do que esta ferramenta pintou nesta vista e apaga a legenda. Precisa de transação aberta.</summary>
    public static int Limpar(Document doc, View vista)
    {
        if (ArmazenamentoSpk.Ler<MarcasDeCor>(doc, Chave + vista.Id.Value) is not { } marcas) return 0;
        var normal = new OverrideGraphicSettings();
        var pintados = 0;
        foreach (var id in marcas.Elementos.Select(v => new ElementId(v)).Where(id => doc.GetElement(id) is not null))
        {
            vista.SetElementOverrides(id, normal);
            pintados++;
        }
        var legenda = marcas.Legenda.Select(v => new ElementId(v)).Where(id => doc.GetElement(id) is not null).ToList();
        if (legenda.Count > 0) doc.Delete(legenda);
        ArmazenamentoSpk.Gravar(doc, Chave + vista.Id.Value, new MarcasDeCor([], []));
        return pintados;
    }

    /// <summary>Os elementos pintados e os da legenda, por vista.</summary>
    public sealed record MarcasDeCor(List<long> Elementos, List<long> Legenda);

    static List<(Element Elemento, FaixaDeCor Faixa)> Classificar(Document doc, View vista, CriterioDeCor criterio, out int semValor)
    {
        var lista = new List<(Element, FaixaDeCor)>();
        semValor = 0;
        var tubos = new FilteredElementCollector(doc, vista.Id).OfClass(typeof(Pipe)).Cast<Pipe>().ToList();
        var bicos = new FilteredElementCollector(doc, vista.Id).OfCategory(BuiltInCategory.OST_Sprinklers).WhereElementIsNotElementType().ToList();
        switch (criterio)
        {
            case CriterioDeCor.Diametro:
                foreach (var tubo in tubos)
                {
                    var dn = (int)Math.Round(tubo.Diameter * MmPorPe);
                    var achou = PorDn.FirstOrDefault(p => p.Dn == dn);
                    lista.Add((tubo, achou.Dn == dn
                        ? new FaixaDeCor($"DN {dn}", achou.Cor, achou.Nome, 0)
                        : new FaixaDeCor($"DN {dn}", Outra, "cinza", 0)));
                }
                break;

            case CriterioDeCor.Velocidade:
                foreach (var tubo in tubos)
                {
                    // Sem valor, ou sem vazão no cálculo (ponta morta, trecho fora do caminho da área de cálculo): cor normal.
                    if (Numero(tubo, ParametrosSpk.Velocidade) is not { } v || v < 0.01)
                    {
                        semValor++;
                        continue;
                    }
                    var k = LimitesDeVelocidade.Count(l => v >= l);
                    var texto = k == 0 ? $"até {LimitesDeVelocidade[0]:0} m/s"
                        : k == LimitesDeVelocidade.Length ? $"{LimitesDeVelocidade[k - 1]:0} m/s ou mais"
                        : $"{LimitesDeVelocidade[k - 1]:0} a {LimitesDeVelocidade[k]:0} m/s";
                    lista.Add((tubo, new FaixaDeCor(texto, Escala[k].Cor, Escala[k].Nome, 0)));
                }
                break;

            case CriterioDeCor.Pressao:
                // Só os bicos da área de cálculo têm pressão; os outros ficam gravados com zero.
                var lidos = bicos.Select(b => (Bico: b, P: ParametrosSpk.LerSimNao(b, ParametrosSpk.AreaCalculo) ? Numero(b, ParametrosSpk.Pressao) : null)).ToList();
                semValor = lidos.Count(l => l.P is null);
                var valores = lidos.Where(l => l.P is not null).Select(l => l.P!.Value).ToList();
                if (valores.Count == 0) break;
                // Cinco faixas iguais entre a menor e a maior pressão, em dezenas de kPa.
                var lo = Math.Floor(valores.Min() / 10) * 10;
                var hi = Math.Max(Math.Ceiling(valores.Max() / 10) * 10, lo + 10);
                var passo = (hi - lo) / 5;
                foreach (var (bico, p) in lidos.Where(l => l.P is not null))
                {
                    var k = Math.Min(4, (int)((p!.Value - lo) / passo));
                    lista.Add((bico, new FaixaDeCor($"{lo + k * passo:0} a {lo + (k + 1) * passo:0} kPa", Escala[k].Cor, Escala[k].Nome, 0)));
                }
                break;

            case CriterioDeCor.Setor:
                var setores = LeituraSetores.Ler(doc, []);
                var naVista = new HashSet<ElementId>(tubos.Select(t => t.Id).Concat(bicos.Select(b => b.Id)));
                var pintados = new HashSet<ElementId>();
                foreach (var s in setores)
                {
                    var faixa = new FaixaDeCor(s.Nome, s.Cor, NomeDaCorDoSetor(s.Cor), 0);
                    foreach (var e in s.Bicos.Cast<Element>().Concat(s.Tubos).Where(e => naVista.Contains(e.Id) && pintados.Add(e.Id)))
                        lista.Add((e, faixa));
                }
                semValor = naVista.Count - pintados.Count;
                break;
        }
        return lista;
    }

    static string NomeDaCorDoSetor(Color cor) =>
        PorDn.Select(p => (Cor: p.Cor, Nome: p.Nome)).Concat(Escala).Append((Cor: new Color(150, 30, 30), Nome: "vermelho-escuro"))
            .Where(p => p.Cor.Red == cor.Red && p.Cor.Green == cor.Green && p.Cor.Blue == cor.Blue).Select(p => p.Nome).FirstOrDefault()
        ?? $"RGB {cor.Red}, {cor.Green}, {cor.Blue}";

    static double Ordem(FaixaDeCor f, CriterioDeCor criterio) => criterio switch
    {
        CriterioDeCor.Diametro => double.Parse(f.Texto[3..]),
        CriterioDeCor.Setor => 0,
        _ => Array.FindIndex(Escala, e => e.Nome == f.NomeDaCor),
    };

    /// <summary>
    ///     A rede criada numa fase posterior à que a vista mostra não aparece nela ("ainda não existe"). Diz qual fase pôr na
    ///     vista, ou nulo se não é isso.
    /// </summary>
    internal static string? FaseDiferente(Document doc, View vista)
    {
        var faseRede = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sprinklers).WhereElementIsNotElementType()
            .GroupBy(b => b.CreatedPhaseId).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        var faseVista = vista.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId();
        if (faseRede is null || faseVista is null || faseRede == faseVista) return null;
        var fases = doc.Phases.Cast<Phase>().Select(f => f.Id).ToList();
        if (fases.IndexOf(faseRede) <= fases.IndexOf(faseVista)) return null;
        return $" A rede está na fase \"{doc.GetElement(faseRede)?.Name}\" e esta vista mostra a fase \"{doc.GetElement(faseVista)?.Name}\", " +
               "anterior: troque a Fase da vista nas Propriedades.";
    }

    static double? Numero(Element e, Guid id) => e.get_Parameter(id) is { HasValue: true } p ? p.AsDouble() : null;

    /// <summary>
    ///     A legenda à direita da rede (dentro do recorte da vista, se ele estiver ligado): um quadrado cheio por faixa, com o
    ///     texto ao lado. As medidas são no papel, multiplicadas pela escala da vista.
    /// </summary>
    static List<ElementId> Legenda(Document doc, ViewPlan vista, CriterioDeCor criterio, IReadOnlyList<FaixaDeCor> faixas, IEnumerable<Element> pintados)
    {
        var ids = new List<ElementId>();
        var caixas = pintados.Select(e => e.get_BoundingBox(null)).OfType<BoundingBoxXYZ>().ToList();
        if (caixas.Count == 0) return ids;
        double Papel(double mm) => mm / MmPorPe * vista.Scale;
        var x0 = caixas.Max(c => c.Max.X) + Papel(15);
        var y0 = caixas.Max(c => c.Max.Y);
        var z = vista.GenLevel!.ProjectElevation;
        if (vista.CropBoxActive && vista.CropBox is { } recorte)
        {
            var max = recorte.Transform.OfPoint(recorte.Max);
            var min = recorte.Transform.OfPoint(recorte.Min);
            var (xMax, yMax) = (Math.Max(max.X, min.X), Math.Max(max.Y, min.Y));
            if (x0 + Papel(60) > xMax) x0 = xMax - Papel(65);
            if (y0 > yMax - Papel(5)) y0 = yMax - Papel(5);
        }

        var titulo = criterio switch
        {
            CriterioDeCor.Diametro => "Rede por diâmetro",
            CriterioDeCor.Setor => "Rede por setor",
            CriterioDeCor.Velocidade => "Velocidade nos tubos (último cálculo)",
            _ => "Pressão nos bicos (último cálculo)",
        };
        if (PranchaSpk.Nota(doc, vista, new XYZ(x0, y0, z), titulo) is { } nota) ids.Add(nota.Id);
        var solido = PranchaSpk.Solido(doc);
        for (var i = 0; i < faixas.Count; i++)
        {
            var f = faixas[i];
            var y = y0 - Papel(8) * (i + 1);
            var tipo = PranchaSpk.TipoDeRegiao(doc, $"SPK Cor {f.Cor.Red}-{f.Cor.Green}-{f.Cor.Blue}", solido, f.Cor);
            var q = Papel(4);
            var antes = new HashSet<ElementId>(new FilteredElementCollector(doc, vista.Id).OfClass(typeof(FilledRegion)).ToElementIds());
            PranchaSpk.RegiaoPreenchida(doc, vista, tipo, [[new XYZ(x0, y - q, 0), new XYZ(x0 + q, y - q, 0), new XYZ(x0 + q, y, 0), new XYZ(x0, y, 0)]], vista.GenLevel);
            ids.AddRange(new FilteredElementCollector(doc, vista.Id).OfClass(typeof(FilledRegion)).ToElementIds().Where(id => !antes.Contains(id)));
            if (PranchaSpk.Nota(doc, vista, new XYZ(x0 + q + Papel(2), y + Papel(0.5), z), $"{f.Texto} ({f.Elementos})") is { } texto) ids.Add(texto.Id);
        }
        return ids;
    }
}
