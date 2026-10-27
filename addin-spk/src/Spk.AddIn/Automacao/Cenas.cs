using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

namespace Spk.AddIn.Automacao;

/// <summary>Uma cena montada para testar uma ferramenta: os bicos e os ramais que a ferramenta deve usar.</summary>
sealed record Cena(string Resumo, List<FamilyInstance> Bicos, List<Pipe> Ramais);

/// <summary>
/// Cenas de teste das ferramentas de modelagem, montadas do zero numa cópia do projeto. Apagam antes a malha gerada
/// que a cópia tiver, para o cálculo ver só a cena.
/// </summary>
static class Cenas
{
    public static Cena Montar(Document doc, string nome) => nome switch
    {
        "ramalComBicosSoltos" => RamalComBicosSoltos(doc),
        "subgeralComBicosSoltos" => SubgeralComBicosSoltos(doc),
        _ => throw new InvalidOperationException($"Cena desconhecida: \"{nome}\"."),
    };

    /// <summary>
    /// Subgeral de 9 m no eixo Y, a 3 m de altura, com descida até o piso, e uma fileira de 5 bicos soltos em y = 4,5 m, de
    /// x = 1,5 a 13,5 m, 30 cm abaixo da subgeral; o do meio fica 0,5 m de lado (pede braço). Os ramais da cena: só a
    /// subgeral, que é o tubo que Traçar ramal recebe.
    /// </summary>
    static Cena SubgeralComBicosSoltos(Document doc)
    {
        using var transacao = new Transaction(doc, "SPK — cena de teste");
        transacao.Start();
        ApagarMalhaGerada(doc);
        var nivel = GeradorMalha.Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível.");
        var tipo = GeradorMalha.TipoComTe(doc);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var diametro = GeradorMalha.DiametroComTe(doc, tipo, 65);
        double M(double metros) => GeradorMalha.Pes(metros);
        var z = nivel.Elevation;
        var zSubgeral = z + M(3.0);

        var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, z), new XYZ(0, 0, zSubgeral), diametro);
        var subgeral = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, zSubgeral), new XYZ(0, M(9), zSubgeral), diametro);
        doc.Regenerate();
        GeradorMalha.Ligar(doc, [descida, subgeral], [], nivel);

        var bicos = Bicos(doc, nivel, zSubgeral - M(0.3),
            [(1.5, 4.5, true), (4.5, 4.5, true), (7.5, 5.0, true), (10.5, 4.5, true), (13.5, 4.5, true)]);
        transacao.Commit();
        return new Cena("Subgeral de 9 m com descida e fileira de 5 bicos soltos (1 de lado).", bicos, [subgeral]);
    }

    static void ApagarMalhaGerada(Document doc)
    {
        var anteriores = new FilteredElementCollector(doc).WhereElementIsNotElementType()
            .Where(e => e.Category?.Id.Value is (long)BuiltInCategory.OST_Sprinklers or (long)BuiltInCategory.OST_PipeCurves
                or (long)BuiltInCategory.OST_PipeFitting)
            .Where(DadosEsboco.EhGerado).Select(e => e.Id).ToList();
        if (anteriores.Count > 0) doc.Delete(anteriores);
    }

    /// <summary>Bicos soltos nas posições (em metros), com o conector na cota pedida; marcados ou não como malha gerada.</summary>
    static List<FamilyInstance> Bicos(Document doc, Level nivel, double zBico, (double X, double Y, bool Marcado)[] posicoes)
    {
        var simbolo = GeradorMalha.SimboloDeBico(doc) ?? throw new InvalidOperationException("O projeto não tem família de sprinkler.");
        if (!simbolo.IsActive) simbolo.Activate();
        var bicos = new List<FamilyInstance>();
        foreach (var (x, y, marcado) in posicoes)
        {
            var bico = doc.Create.NewFamilyInstance(new XYZ(GeradorMalha.Pes(x), GeradorMalha.Pes(y), nivel.Elevation), simbolo, nivel,
                StructuralType.NonStructural);
            if (marcado) bico.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(DadosEsboco.Marca);
            bicos.Add(bico);
        }
        doc.Regenerate();
        foreach (var bico in bicos) GeradorMalha.AjustarAltura(bico, zBico, nivel.Elevation);
        return bicos;
    }

    /// <summary>
    /// Ramal reto de 15 m a 3 m de altura, com descida até o piso na ponta de origem, e bicos soltos 30 cm abaixo: três
    /// embaixo do ramal, dois 60 cm de lado (pedem braço e cotovelo) e um 2 m depois do fim do ramal (deve ser recusado,
    /// e fica sem a marca da malha gerada para o cálculo não contar com ele).
    /// </summary>
    static Cena RamalComBicosSoltos(Document doc)
    {
        using var transacao = new Transaction(doc, "SPK — cena de teste");
        transacao.Start();
        ApagarMalhaGerada(doc);
        var nivel = GeradorMalha.Nivel(doc) ?? throw new InvalidOperationException("O projeto não tem nível.");
        var tipo = GeradorMalha.TipoComTe(doc);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var diametro = GeradorMalha.DiametroComTe(doc, tipo, 40);
        double M(double metros) => GeradorMalha.Pes(metros);
        var z = nivel.Elevation;
        var zRamal = z + M(3.0);

        var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, z), new XYZ(0, 0, zRamal), diametro);
        var ramal = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, new XYZ(0, 0, zRamal), new XYZ(M(15), 0, zRamal), diametro);
        doc.Regenerate();
        GeradorMalha.Ligar(doc, [descida, ramal], [], nivel);

        var bicos = Bicos(doc, nivel, zRamal - M(0.3),
            [(1.5, 0, true), (4.5, 0, true), (7.5, 0.6, true), (10.5, -0.6, true), (13.5, 0, true), (17, 0, false)]);
        transacao.Commit();

        return new Cena($"Ramal de 15 m com descida e {bicos.Count} bicos soltos (2 de lado, 1 fora do ramal).", bicos, [ramal]);
    }
}
