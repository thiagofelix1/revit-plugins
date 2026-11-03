using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spk.AddIn.Servicos;

/// <summary>
/// O retrato do que o último cálculo levou em conta: os elementos ligados à rede calculada (tipo, posição e diâmetro), os
/// Dados do projeto, o K e a área dos bicos. As folhas e o memorial comparam com o modelo para não mostrar um resultado de
/// quando a rede era outra: juntar salas, mudar um DN ou mover um bico não apaga nada, e passava despercebido.
/// </summary>
static class AssinaturaDaRede
{
    /// <param name="inicio">Os bicos e tubos do setor calculado. O percurso segue as ligações, e acha o que foi ligado depois.</param>
    public static string Calcular(Document doc, IEnumerable<ElementId> inicio)
    {
        var linhas = new List<string>();
        var visitados = new HashSet<ElementId>();
        var pilha = new Stack<ElementId>(inicio);
        while (pilha.Count > 0)
        {
            var id = pilha.Pop();
            if (!visitados.Add(id)) continue;
            if (doc.GetElement(id) is not { } elemento)
            {
                linhas.Add($"{id.Value} apagado");
                continue;
            }
            linhas.Add(Linha(elemento));
            foreach (var conector in Conectores(elemento).Where(c => c.Domain == Domain.DomainPiping && c.IsConnected))
                foreach (Connector outro in conector.AllRefs)
                    if (outro.Owner is Autodesk.Revit.DB.Plumbing.Pipe or FamilyInstance) pilha.Push(outro.Owner.Id);
        }
        linhas.Sort(StringComparer.Ordinal);

        var esboco = DadosEsboco.Carregar(doc);
        linhas.Add("dados " + JsonSerializer.Serialize(DadosProjeto.Carregar(doc)));
        linhas.Add($"esboço K {esboco.FatorK} área {esboco.AreaM2}");
        linhas.AddRange((MapeamentoGravado.Tentar(doc) ?? []).Where(l => l.EhChuveiroComMedidas)
            .Select(l => $"mapeamento {l.Familia} {l.Tipo} K {l.ValorFatorK} área {l.ValorAreaCobertura}").OrderBy(l => l, StringComparer.Ordinal));

        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", linhas))));
    }

    /// <summary>A rede ou os dados mudaram depois do cálculo gravado. Resultado de versão anterior, sem assinatura: não dá para saber, e não acusa.</summary>
    public static bool Mudou(Document doc, ResultadoGravado gravado) =>
        gravado.Assinatura is { } assinatura && assinatura != Calcular(doc, Inicio(gravado));

    public static IEnumerable<ElementId> Inicio(ResultadoGravado gravado) =>
        gravado.Chuveiros.Select(c => new ElementId(c.Elemento)).Concat(gravado.Tubos.Select(t => new ElementId(t)));

    static string Linha(Element e)
    {
        static long Mm(double pes) => (long)Math.Round(pes * 304.8);
        static string Ponto(XYZ p) => $"{Mm(p.X)};{Mm(p.Y)};{Mm(p.Z)}";
        var onde = e.Location switch
        {
            LocationCurve { Curve: { } curva } => Ponto(curva.GetEndPoint(0)) + " " + Ponto(curva.GetEndPoint(1)),
            LocationPoint { Point: { } ponto } => Ponto(ponto),
            _ => "",
        };
        var diametro = e is Autodesk.Revit.DB.Plumbing.Pipe tubo ? Mm(tubo.Diameter).ToString() : "";
        return $"{e.Id.Value} {e.GetTypeId().Value} {onde} {diametro}";
    }

    static IEnumerable<Connector> Conectores(Element e) =>
        (e as MEPCurve)?.ConnectorManager?.Connectors.Cast<Connector>()
        ?? (e as FamilyInstance)?.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>()
        ?? [];
}
