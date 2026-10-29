using Autodesk.Revit.DB.Plumbing;

namespace Spk.AddIn.Modelagem;

/// <summary>A entrada de um setor marcada pelo projetista: o nome e a ponta de tubo por onde a água chega (metros).</summary>
public sealed record EntradaDeSetor(string Nome, long Tubo, double X, double Y, double Z);

/// <summary>O que a marcação fez: a entrada gravada, a que ela substituiu na mesma rede e os bicos dessa rede.</summary>
public sealed record EntradaMarcada(EntradaDeSetor Entrada, string? Substituida, int Bicos, string? Aviso);

/// <summary>
///     Setor sem VGA desenhada: o projetista clica no tubo por onde a água chega (a descida da VGA) e dá o nome. A leitura
///     da rede (<see cref="LeitorRede" />) passa a usar essa ponta como fonte e esse nome no cálculo, no memorial e nas
///     folhas, em vez da ponta escolhida sozinha (a do tubo mais grosso) e de "Setor 1". Fica gravado no .rvt; uma rede tem uma entrada só.
/// </summary>
static class EntradasDeSetor
{
    const string Chave = "EntradasDeSetor";

    /// <summary>Distância (m) entre a marca e o nó da rede para valer como a mesma ponta.</summary>
    public const double Tolerancia = 0.10;

    public static List<EntradaDeSetor> Ler(Document doc) => ArmazenamentoSpk.Ler<List<EntradaDeSetor>>(doc, Chave) ?? [];

    /// <summary>
    ///     Marca a ponta do tubo mais perto de <paramref name="perto" />, de preferência uma ponta solta ou com tampão. Tira a
    ///     marca que a mesma rede já tinha. Precisa de uma transação aberta.
    /// </summary>
    public static EntradaMarcada Marcar(Document doc, Pipe tubo, XYZ perto, string nome)
    {
        nome = nome.Trim();
        if (nome.Length == 0) throw new InvalidOperationException("Dê um nome ao setor (por exemplo, VGA 01).");
        var rede = LigarAGeral.Rede(doc, tubo);
        var lista = Ler(doc);
        var daRede = lista.Where(e => rede.Contains(new ElementId(e.Tubo))).ToList();
        if (lista.Except(daRede).FirstOrDefault(e => string.Equals(e.Nome, nome, StringComparison.OrdinalIgnoreCase)) is { } repetido)
            throw new InvalidOperationException($"Já há um setor chamado \"{repetido.Nome}\" em outra rede. Escolha outro nome.");

        var pontas = tubo.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End).ToList();
        if (pontas.Count == 0) throw new InvalidOperationException("O tubo clicado não tem pontas de tubulação.");
        // Ponta solta primeiro (onde a VGA entraria), depois a com tampão; entre elas, a mais perto do clique em planta.
        var abertas = pontas.Where(Aberta).ToList();
        var ponta = (abertas.Count > 0 ? abertas : pontas)
            .OrderBy(c => c.IsConnected).ThenBy(c => Math.Sqrt(Math.Pow(c.Origin.X - perto.X, 2) + Math.Pow(c.Origin.Y - perto.Y, 2)))
            .ThenBy(c => c.Origin.Z).First();
        var aviso = abertas.Count == 0
            ? "As duas pontas do tubo clicado estão ligadas a outras peças: a água vai entrar no meio da rede, na ponta mais perto do clique. " +
              "O comum é marcar a ponta solta da descida da VGA."
            : null;

        var o = ponta.Origin;
        var entrada = new EntradaDeSetor(nome, tubo.Id.Value, Metros(o.X), Metros(o.Y), Metros(o.Z));
        lista.RemoveAll(daRede.Contains);
        lista.Add(entrada);
        ArmazenamentoSpk.Gravar(doc, Chave, lista);
        var bicos = rede.Count(id => doc.GetElement(id)?.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers);
        return new EntradaMarcada(entrada, daRede.Select(e => e.Nome).FirstOrDefault(n => n != nome), bicos, aviso);
    }

    /// <summary>Tira a marca da rede do tubo. Devolve o nome que saiu, ou nulo. Precisa de uma transação aberta.</summary>
    public static string? Desmarcar(Document doc, Pipe tubo)
    {
        var rede = LigarAGeral.Rede(doc, tubo);
        var lista = Ler(doc);
        var saiu = lista.FirstOrDefault(e => rede.Contains(new ElementId(e.Tubo)));
        if (saiu is null) return null;
        lista.RemoveAll(e => rede.Contains(new ElementId(e.Tubo)));
        ArmazenamentoSpk.Gravar(doc, Chave, lista);
        return saiu.Nome;
    }

    /// <summary>A marca da rede deste tubo, se houver.</summary>
    public static EntradaDeSetor? DaRede(Document doc, Pipe tubo)
    {
        var rede = LigarAGeral.Rede(doc, tubo);
        return Ler(doc).FirstOrDefault(e => rede.Contains(new ElementId(e.Tubo)));
    }

    /// <summary>Ponta sem ligação, ou ligada só a um tampão (peça de um conector).</summary>
    static bool Aberta(Connector ponta) =>
        !ponta.IsConnected || ponta.AllRefs.Cast<Connector>()
            .Where(r => r.ConnectorType != ConnectorType.Logical && r.Owner is not null and not MEPSystem && r.Owner.Id != ponta.Owner.Id)
            .All(r => r.Owner is FamilyInstance { MEPModel.ConnectorManager.Connectors.Size: <= 1 });

    static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);
}
