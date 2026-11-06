using Autodesk.Revit.DB.Electrical;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Lancamento;

/// <summary>
///     Comando de iluminação (item 2.3 do plano): liga interruptores às luminárias que eles acendem. Grava em todos o mesmo
///     "AE Comando" (S1, S2...), nos interruptores a ordem (S1#1, S1#2) e o tipo (simples, paralelo, intermediário), e o
///     "Switch ID" do Revit onde a família o tem. Se as luminárias já estão num circuito e o interruptor não, ele entra no
///     mesmo circuito (a fase passa por ele, 5.6.6.1.2). É o dado que Passar circuitos usa para contar os retornos.
/// </summary>
public static class Comandos
{
    public const string Prefixo = "S";

    public sealed record ComandoLido(string Id, IReadOnlyList<FamilyInstance> Interruptores, IReadOnlyList<FamilyInstance> Luminarias, TipoDeComando Tipo);

    public static int ProximoNumero(Document doc)
    {
        var maior = 0;
        foreach (var fi in Pontos(doc))
        {
            var texto = ParametrosEletrica.LerTexto(fi, ParametrosEletrica.Comando);
            if (string.IsNullOrWhiteSpace(texto)) continue;
            var id = texto!.Split('#')[0].Trim();
            if (id.StartsWith(Prefixo) && int.TryParse(id.Substring(Prefixo.Length), out var n)) maior = Math.Max(maior, n);
        }
        return maior + 1;
    }

    /// <summary>Precisa de transação aberta. Os interruptores na ordem do caminho (o primeiro recebe a fase).</summary>
    public static string Gravar(Document doc, int numero, IReadOnlyList<FamilyInstance> interruptores, IReadOnlyList<FamilyInstance> luminarias, List<string> avisos)
    {
        var id = Prefixo + numero;
        var tipo = Comando.TipoPor(interruptores.Count);
        for (var i = 0; i < interruptores.Count; i++)
        {
            var sw = interruptores[i];
            ParametrosEletrica.Gravar(sw, ParametrosEletrica.Comando, $"{id}#{i + 1}");
            ParametrosEletrica.Gravar(sw, ParametrosEletrica.TipoDeComando, NomeDoTipo(tipo));
            SwitchId(sw, id);
        }
        foreach (var l in luminarias)
        {
            ParametrosEletrica.Gravar(l, ParametrosEletrica.Comando, id);
            SwitchId(l, id);
        }

        // Interruptor sem circuito entra no circuito das luminárias.
        var circuito = luminarias.Select(CircuitoDe).FirstOrDefault(c => c is not null);
        if (circuito is not null)
        {
            var soltos = new ElementSet();
            foreach (var sw in interruptores.Where(s => CircuitoDe(s) is null && InserirPontos.TemConectorEletrico(s))) soltos.Insert(sw);
            if (!soltos.IsEmpty)
            {
                try
                {
                    if (!circuito.AddToCircuit(soltos)) avisos.Add($"{id}: o Revit não aceitou o interruptor no circuito {circuito.CircuitNumber}.");
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException e)
                {
                    avisos.Add($"{id}: interruptor fora do circuito {circuito.CircuitNumber}: {e.Message}");
                }
            }
        }
        return id;
    }

    static void SwitchId(FamilyInstance fi, string id)
    {
        var p = fi.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM);
        if (p is { IsReadOnly: false, StorageType: StorageType.String }) p.Set(id);
    }

    public static ElectricalSystem? CircuitoDe(FamilyInstance fi)
    {
        try { return fi.MEPModel?.GetElectricalSystems()?.FirstOrDefault(s => s.SystemType == ElectricalSystemType.PowerCircuit); }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { return null; }
    }

    /// <summary>Todos os comandos gravados no modelo.</summary>
    public static List<ComandoLido> Ler(Document doc)
    {
        var porId = new Dictionary<string, (SortedList<int, FamilyInstance> Sw, List<FamilyInstance> Luz)>();
        foreach (var fi in Pontos(doc))
        {
            var texto = ParametrosEletrica.LerTexto(fi, ParametrosEletrica.Comando);
            if (string.IsNullOrWhiteSpace(texto)) continue;
            var partes = texto!.Split('#');
            var id = partes[0].Trim();
            if (!porId.TryGetValue(id, out var grupo)) porId[id] = grupo = (new SortedList<int, FamilyInstance>(), []);
            var ehInterruptor = fi.Category?.BuiltInCategory == BuiltInCategory.OST_LightingDevices || partes.Length > 1;
            if (ehInterruptor)
            {
                var ordem = partes.Length > 1 && int.TryParse(partes[1], out var o) ? o : grupo.Sw.Count + 1;
                while (grupo.Sw.ContainsKey(ordem)) ordem++;
                grupo.Sw.Add(ordem, fi);
            }
            else grupo.Luz.Add(fi);
        }
        return porId.Where(k => k.Value.Sw.Count > 0 && k.Value.Luz.Count > 0)
            .Select(k => new ComandoLido(k.Key, k.Value.Sw.Values.ToList(), k.Value.Luz, Comando.TipoPor(k.Value.Sw.Count))).ToList();
    }

    static IEnumerable<FamilyInstance> Pontos(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .Where(fi => fi.Category?.BuiltInCategory is BuiltInCategory.OST_LightingDevices or BuiltInCategory.OST_LightingFixtures or BuiltInCategory.OST_ElectricalFixtures);

    public static string NomeDoTipo(TipoDeComando t) => t switch
    {
        TipoDeComando.Simples => "Simples",
        TipoDeComando.Paralelo => "Paralelo",
        _ => "Intermediário",
    };
}
