using Eletrica.AddIn.Padrao;

namespace Eletrica.AddIn.Familias;

/// <summary>Um tipo de família carregado no projeto, candidato a um papel no lançamento.</summary>
public sealed class CandidatoFamilia
{
    public string Categoria { get; init; } = "";
    public string Familia { get; init; } = "";
    public string Tipo { get; init; } = "";
    public string Nome => $"{Familia} : {Tipo}";
    public int Instancias { get; init; }
    public bool Hospedada { get; init; }
    public bool TemConectorEletrico { get; init; }
    public string Conector => TemConectorEletrico ? "sim" : "não";
    public string Hospedagem => Hospedada ? "face/parede" : "livre";
    public PapelFamilia Papel { get; set; }
    public string PapelTexto
    {
        get => PadraoEletrica.NomeDoPapel(Papel);
        set => Papel = Papeis.FirstOrDefault(p => PadraoEletrica.NomeDoPapel(p) == value);
    }

    public static readonly PapelFamilia[] Papeis = Enum.GetValues<PapelFamilia>();
}

/// <summary>Varre os tipos de família das categorias elétricas e sugere o papel pelo nome; o engenheiro confirma na janela.</summary>
public static class DescobertaDeFamilias
{
    static readonly BuiltInCategory[] Categorias =
    [
        BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
        BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_ConduitFitting,
    ];

    public static List<CandidatoFamilia> Descobrir(Document doc, PadraoEletrica padrao)
    {
        var instancias = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .GroupBy(fi => fi.GetTypeId()).ToDictionary(g => g.Key, g => g.ToList());
        var lista = new List<CandidatoFamilia>();
        foreach (var categoria in Categorias)
        foreach (var s in new FilteredElementCollector(doc).OfCategory(categoria).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>())
        {
            var exemplos = instancias.TryGetValue(s.Id, out var l) ? l : [];
            var confirmado = padrao.PapelDe(s);
            var papel = confirmado ?? Sugerir(categoria, s.FamilyName + " " + s.Name);
            if (categoria == BuiltInCategory.OST_ConduitFitting && papel != PapelFamilia.CaixaDePassagem) continue; // curvas e luvas não entram
            lista.Add(new CandidatoFamilia
            {
                Categoria = s.Category?.Name ?? "",
                Familia = s.FamilyName,
                Tipo = s.Name,
                Instancias = exemplos.Count,
                Hospedada = s.Family.FamilyPlacementType is FamilyPlacementType.OneLevelBasedHosted or FamilyPlacementType.WorkPlaneBased,
                TemConectorEletrico = exemplos.Count > 0 ? TemConector(exemplos[0]) : TemConector(s.Family),
                Papel = papel,
            });
        }
        return lista.OrderBy(c => c.Categoria).ThenBy(c => c.Familia).ThenBy(c => c.Tipo).ToList();
    }

    static bool TemConector(FamilyInstance fi) =>
        fi.MEPModel?.ConnectorManager is { } cm && cm.Connectors.Cast<Connector>().Any(k => k.Domain == Domain.DomainElectrical);

    /// <summary>Sem instância, a família diz se tem conector por um parâmetro de tipo elétrico (carga aparente, tensão).</summary>
    static bool TemConector(Family f)
    {
        try
        {
            if (f.Document.GetElement(f.GetFamilySymbolIds().FirstOrDefault() ?? ElementId.InvalidElementId) is not FamilySymbol s) return false;
            return s.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD) is not null || s.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE) is not null;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { return false; }
    }

    public static PapelFamilia Sugerir(BuiltInCategory categoria, string nome)
    {
        var n = nome.ToLowerInvariant();
        switch (categoria)
        {
            case BuiltInCategory.OST_ElectricalEquipment:
                return n.Contains("quadro") || n.Contains("panel") || n.Contains("qd") || n.Contains("qgbt") || n.Contains("switchboard") ? PapelFamilia.Quadro : PapelFamilia.Ignorar;
            case BuiltInCategory.OST_ConduitFitting:
                return n.Contains("caixa") || n.Contains("box") || n.Contains("condulete") || n.Contains("passagem") ? PapelFamilia.CaixaDePassagem : PapelFamilia.Ignorar;
            case BuiltInCategory.OST_LightingFixtures:
                return n.Contains("arandela") || n.Contains("parede") || n.Contains("wall") || n.Contains("sconce") ? PapelFamilia.Arandela : PapelFamilia.Luminaria;
            case BuiltInCategory.OST_LightingDevices:
                if (n.Contains("intermedi") || n.Contains("four") || n.Contains("4 way") || n.Contains("4-way")) return PapelFamilia.InterruptorIntermediario;
                if (n.Contains("paralelo") || n.Contains("three") || n.Contains("3 way") || n.Contains("3-way")) return PapelFamilia.InterruptorParalelo;
                if (n.Contains("3 tecla") || n.Contains("tripl") || n.Contains("3t")) return PapelFamilia.Interruptor3;
                if (n.Contains("2 tecla") || n.Contains("dupl") || n.Contains("2t") || n.Contains("duas")) return PapelFamilia.Interruptor2;
                return PapelFamilia.Interruptor1;
            default:
                if (n.Contains("chuveiro") || n.Contains("aquecedor") || n.Contains("shower")) return PapelFamilia.Chuveiro;
                if (n.Contains("ar-cond") || n.Contains("ar cond") || n.Contains("split") || n.Contains("condicionad")) return PapelFamilia.ArCondicionado;
                if (n.Contains("força") || n.Contains("forca") || n.Contains("tue") || n.Contains("espera")) return PapelFamilia.PontoDeForca;
                if (n.Contains("20a") || n.Contains("20 a")) return PapelFamilia.Tomada20A;
                if (n.Contains("dupla") || n.Contains("duplex") || n.Contains("2x") || n.Contains("2 x")) return PapelFamilia.TomadaDupla;
                if (n.Contains("tomada") || n.Contains("receptacle") || n.Contains("outlet") || n.Contains("socket") || n.Contains("10a")) return PapelFamilia.Tomada10A;
                return PapelFamilia.Ignorar;
        }
    }
}
