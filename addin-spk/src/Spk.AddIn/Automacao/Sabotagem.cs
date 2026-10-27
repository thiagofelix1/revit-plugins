using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace Spk.AddIn.Automacao;

/// <summary>
/// Estraga a malha gerada de propósito, de um jeito conhecido, para o teste provar que Verificar modelo acha o problema.
/// Escolhe sempre o mesmo elemento (o de menor id que serve), para o resultado ser repetível.
/// </summary>
static class Sabotagem
{
    public static string Aplicar(Document doc, IEnumerable<string> acoes)
    {
        using var transacao = new Transaction(doc, "SPK — sabotagem de teste");
        transacao.Start();
        var feitos = acoes.Select(acao => acao switch
        {
            "desligarBico" => DesligarBico(doc),
            "apagarTe" => ApagarTe(doc),
            "sistemaAgua" => SistemaAgua(doc),
            "desligarTubo" => DesligarTubo(doc),
            _ => throw new InvalidOperationException($"Sabotagem desconhecida: \"{acao}\"."),
        }).ToList();
        transacao.Commit();
        return string.Join("\n", feitos);
    }

    /// <summary>Solta o bico da conexão em que está: ficam encostados, sem ligação.</summary>
    static string DesligarBico(Document doc)
    {
        foreach (var bico in BicosDoModelo.Gerados(doc).OrderBy(b => b.Id.Value))
        foreach (var (conector, outro) in Ligacoes(bico))
        {
            conector.DisconnectFrom(outro);
            return $"Bico {bico.Id.Value} desligado de {outro.Owner.Category?.Name} {outro.Owner.Id.Value}.";
        }
        throw new InvalidOperationException("Nenhum bico gerado ligado para desligar.");
    }

    /// <summary>Apaga um tê entre três tubos (subgeral com ramal): a rede se parte e três pontas ficam abertas.</summary>
    static string ApagarTe(Document doc)
    {
        var te = Gerados(doc, BuiltInCategory.OST_PipeFitting).OfType<FamilyInstance>()
                     .Where(f => (f.MEPModel as MechanicalFitting)?.PartType == PartType.Tee)
                     .FirstOrDefault(f => Ligacoes(f).Count(l => l.Outro.Owner is MEPCurve) == 3)
                 ?? throw new InvalidOperationException("Nenhum tê entre três tubos na malha gerada.");
        var id = te.Id.Value;
        doc.Delete(te.Id);
        return $"Tê {id} apagado.";
    }

    /// <summary>Põe um tubo num sistema de água fria (cria o tipo de sistema se o projeto não tiver).</summary>
    static string SistemaAgua(Document doc)
    {
        var agua = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>()
                       .FirstOrDefault(t => t.SystemClassification == MEPSystemClassification.DomesticColdWater)
                   ?? PipingSystemType.Create(doc, MEPSystemClassification.DomesticColdWater, "SPK teste — água fria");
        var tubo = Gerados(doc, BuiltInCategory.OST_PipeCurves).OfType<Pipe>().First();
        tubo.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).Set(agua.Id);
        return $"Tubo {tubo.Id.Value} (e o que o Revit mudou junto) no sistema \"{agua.Name}\".";
    }

    /// <summary>Solta uma ponta do tubo de maior diâmetro (subgeral): tubo e conexão ficam encostados, sem ligação.</summary>
    static string DesligarTubo(Document doc)
    {
        var tubo = Gerados(doc, BuiltInCategory.OST_PipeCurves).OfType<Pipe>()
            .Where(t => Ligacoes(t).Count() >= 2)
            .OrderByDescending(t => t.Diameter).ThenBy(t => t.Id.Value)
            .First();
        var (conector, outro) = Ligacoes(tubo).First();
        conector.DisconnectFrom(outro);
        return $"Tubo {tubo.Id.Value} desligado de {outro.Owner.Category?.Name} {outro.Owner.Id.Value}.";
    }

    static IEnumerable<Element> Gerados(Document doc, BuiltInCategory categoria) =>
        new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsNotElementType()
            .Where(DadosEsboco.EhGerado).OrderBy(e => e.Id.Value);

    /// <summary>Pares (conector deste elemento, conector físico do vizinho), sem as ligações lógicas com o sistema.</summary>
    static IEnumerable<(Connector Conector, Connector Outro)> Ligacoes(Element e)
    {
        var gerente = e switch
        {
            MEPCurve curva => curva.ConnectorManager,
            FamilyInstance instancia => instancia.MEPModel?.ConnectorManager,
            _ => null,
        };
        if (gerente is null) yield break;
        foreach (Connector conector in gerente.Connectors)
        {
            if (!conector.IsConnected) continue;
            foreach (Connector outro in conector.AllRefs)
                if (outro.Owner.Id != e.Id && outro.Owner is MEPCurve or FamilyInstance)
                    yield return (conector, outro);
        }
    }
}
