using Spk.AddIn.Arquitetura;
using Spk.Motor.Geracao;
using Spk.Motor.Modelo;

namespace Spk.AddIn.Modelagem;

/// <summary>O que a conferência desenhou: quantos ambientes e bicos, quantos fora da regra e as linhas do relatório.</summary>
public sealed record ResultadoConferencia(int Ambientes, int Bicos, int BicosFora, int PontosDescobertos, IReadOnlyList<string> Linhas)
{
    public string Texto() =>
        $"{Ambientes} ambiente(s) e {Bicos} bico(s) conferidos nesta planta: {BicosFora} bico(s) fora da regra (em vermelho, com a nota) e " +
        $"{PontosDescobertos} trecho(s) sem cobertura (círculo vermelho)." + (Linhas.Count > 0 ? "\n" + string.Join("\n", Linhas) : "");
}

/// <summary>
///     A conferência da norma desenhada na planta, para o engenheiro ver onde mexer: a mesma do Bicos por ambiente
///     (Tabela 2, distâncias, cobertura, altura do defletor), nos ambientes do nível da vista. Bico fora da regra fica
///     vermelho, com uma nota do que falta; o alcance de cada bico vira um círculo cinza (onde não há círculo, não há
///     cobertura); ponto do piso ou trecho de parede sem cobertura ganha um círculo vermelho. Tudo é anotação da vista,
///     com estilos e tipo de texto próprios ("SPK Conferência", "SPK Cobertura"): rodar de novo ou Limpar apaga só isso.
/// </summary>
static class ConferenciaVisual
{
    const string EstiloFalha = "SPK Conferência";
    const string EstiloAlcance = "SPK Cobertura";
    const string TipoDaNota = "SPK Conferência";
    static readonly Color Vermelho = new(220, 0, 0);
    static readonly Color Cinza = new(160, 160, 160);

    public static ResultadoConferencia Executar(Document doc, ViewPlan vista, DadosProjeto dados, OpcoesBicos opcoes)
    {
        var edificio = LeituraDoEdificio.Ler(doc);
        var nivel = vista.GenLevel ?? throw new InvalidOperationException("A vista não tem nível: abra uma planta de piso.");
        var zNivel = Metros(nivel.ProjectElevation);
        var ambientes = edificio.Ambientes.Where(a => a.Piso > zNivel - 0.3 && a.Piso < zNivel + 0.6).ToList();
        var limites = BicosPorAmbiente.Limites(dados, opcoes);
        var conferidos = BicosPorAmbiente.Conferir(doc, edificio, ambientes, limites, opcoes);
        var bicos = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sprinklers).WhereElementIsNotElementType()
            .OfType<FamilyInstance>().Where(b => b.Location is LocationPoint).ToList();

        var linhas = new List<string>();
        int total = 0, fora = 0, descobertos = 0;
        using var t = new Transaction(doc, "SPK — Conferir na planta");
        t.Start();
        Limpar(doc, vista);
        var falha = Estilo(doc, EstiloFalha, Vermelho, 5);
        var alcance = Estilo(doc, EstiloAlcance, Cinza, 1);
        var tipoNota = TipoDeNota(doc);
        var pintura = Folhas.PranchaSpk.Pintura(doc, Vermelho, 8, preencher: true);
        var notas = new List<(Ponto2 Onde, string Texto)>();
        foreach (var r in conferidos)
        {
            var teto = r.Ambiente.Teto ?? r.Ambiente.Piso + 3;
            var dele = bicos.Where(b => Ponto(b) is var p && BicosNoAmbiente.Dentro(r.Ambiente.Contornos, p)
                                        && Metros(((LocationPoint)b.Location).Point.Z) is var z && z > r.Ambiente.Piso && z < teto + 0.05).ToList();
            total += dele.Count;
            foreach (var b in dele) Circulo(doc, vista, nivel, Ponto(b), limites.AlcanceMaximo, alcance);

            var porBico = new Dictionary<ElementId, List<string>>();
            foreach (var f in r.Falhas)
            {
                if (f.Tipo is TipoDeFalha.PontoDescoberto or TipoDeFalha.LongeDaParede)
                {
                    descobertos++;
                    Circulo(doc, vista, nivel, f.Onde, 0.25, falha);
                    Nota(doc, vista, nivel, tipoNota, f.Onde, f.Descricao);
                    continue;
                }
                var bico = dele.OrderBy(b => Distancia(Ponto(b), f.Onde)).FirstOrDefault();
                if (bico is null || Distancia(Ponto(bico), f.Onde) > 0.05) continue;
                if (!porBico.TryGetValue(bico.Id, out var lista)) porBico[bico.Id] = lista = [];
                if (!lista.Contains(f.Descricao)) lista.Add(f.Descricao);
            }
            foreach (var (id, descricoes) in porBico)
            {
                // O bico vermelho só aparece se a vista mostra os bicos (planta de forro, ou corte acima deles); o círculo
                // vermelho de anotação aparece em qualquer planta.
                var bico = (FamilyInstance)doc.GetElement(id);
                vista.SetElementOverrides(id, pintura);
                Circulo(doc, vista, nivel, Ponto(bico), 0.30, falha);
                var texto = string.Join("\n", descricoes);
                if (!notas.Any(n => n.Texto == texto && Distancia(n.Onde, Ponto(bico)) < 1.0))
                {
                    Nota(doc, vista, nivel, tipoNota, Ponto(bico), texto);
                    notas.Add((Ponto(bico), texto));
                }
            }
            fora += porBico.Count;
            if (porBico.Count > 0 || r.Falhas.Any(f => f.Tipo is TipoDeFalha.PontoDescoberto or TipoDeFalha.LongeDaParede))
                linhas.Add($"• {r.Ambiente.Numero} {r.Ambiente.Nome}".TrimEnd() + $": {porBico.Count} bico(s) fora, " +
                           $"{r.Falhas.Count(f => f.Tipo is TipoDeFalha.PontoDescoberto or TipoDeFalha.LongeDaParede)} sem cobertura");
        }
        t.Commit();
        return new ResultadoConferencia(conferidos.Count, total, fora, descobertos, linhas);
    }

    /// <summary>Apaga o que a conferência desenhou na vista e tira o vermelho dos bicos.</summary>
    public static int Limpar(Document doc, View vista)
    {
        var apagar = new FilteredElementCollector(doc, vista.Id).OfClass(typeof(CurveElement)).Cast<CurveElement>()
            .Where(c => c.LineStyle?.Name is EstiloFalha or EstiloAlcance).Select(c => c.Id)
            .Concat(new FilteredElementCollector(doc, vista.Id).OfClass(typeof(TextNote)).Cast<TextNote>()
                .Where(n => doc.GetElement(n.GetTypeId())?.Name == TipoDaNota).Select(n => n.Id))
            .ToList();
        if (apagar.Count > 0) doc.Delete(apagar);
        foreach (var bico in new FilteredElementCollector(doc, vista.Id).OfCategory(BuiltInCategory.OST_Sprinklers).WhereElementIsNotElementType())
        {
            var atual = vista.GetElementOverrides(bico.Id);
            if (atual.ProjectionLineColor is { IsValid: true } cor && cor.Red == Vermelho.Red && cor.Green == Vermelho.Green && cor.Blue == Vermelho.Blue)
                vista.SetElementOverrides(bico.Id, new OverrideGraphicSettings());
        }
        return apagar.Count;
    }

    /// <summary>Estilo de linha (subcategoria de Linhas) com esta cor; cria se não existir.</summary>
    static GraphicsStyle Estilo(Document doc, string nome, Color cor, int espessura)
    {
        var linhas = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
        var sub = linhas.SubCategories.Contains(nome) ? linhas.SubCategories.get_Item(nome) : doc.Settings.Categories.NewSubcategory(linhas, nome);
        sub.LineColor = cor;
        try { sub.SetLineWeight(espessura, GraphicsStyleType.Projection); }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }
        return sub.GetGraphicsStyle(GraphicsStyleType.Projection);
    }

    static TextNoteType TipoDeNota(Document doc)
    {
        if (new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().FirstOrDefault(t => t.Name == TipoDaNota) is { } existe)
            return existe;
        var padrao = (TextNoteType)doc.GetElement(doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType));
        var novo = (TextNoteType)padrao.Duplicate(TipoDaNota);
        novo.get_Parameter(BuiltInParameter.LINE_COLOR)?.Set(Vermelho.Red + Vermelho.Green * 256 + Vermelho.Blue * 65536);
        return novo;
    }

    static void Circulo(Document doc, View vista, Level nivel, Ponto2 centro, double raio, GraphicsStyle estilo)
    {
        foreach (var z in new[] { nivel.ProjectElevation, 0.0 })
        {
            try
            {
                var arco = Arc.Create(new XYZ(Pes(centro.X), Pes(centro.Y), z), Pes(raio), 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);
                doc.Create.NewDetailCurve(vista, arco).LineStyle = estilo;
                return;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
            }
        }
    }

    static void Nota(Document doc, View vista, Level nivel, TextNoteType tipo, Ponto2 onde, string texto)
    {
        var opcoes = new TextNoteOptions(tipo.Id) { HorizontalAlignment = HorizontalTextAlignment.Left };
        TextNote.Create(doc, vista.Id, new XYZ(Pes(onde.X + 0.3), Pes(onde.Y + 0.3), nivel.ProjectElevation), texto, opcoes);
    }

    static Ponto2 Ponto(FamilyInstance b)
    {
        var p = ((LocationPoint)b.Location).Point;
        return new Ponto2(Metros(p.X), Metros(p.Y));
    }

    static double Distancia(Ponto2 a, Ponto2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
    static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);
}
