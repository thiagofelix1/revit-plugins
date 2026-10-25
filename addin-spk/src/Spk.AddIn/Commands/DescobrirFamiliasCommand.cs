using System.IO;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Mapeamento;

namespace Spk.AddIn.Commands;

/// <summary>
///     Varre o modelo aberto e relata o que existe antes de escrever qualquer extração de rede:
///     tubos (classe Pipe, confiável independente de nomenclatura), sistemas de tubulação (candidatos a
///     fonte/VGA), famílias cujo nome sugere chuveiro, e todas as categorias de instância presentes
///     (para o caso de a busca por palavra não achar nada). Não altera o modelo.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class DescobrirFamiliasCommand : ExternalCommand
{
    static readonly string[] PalavrasChuveiro = ["chuveiro", "sprinkler", "spk", "esfr"];

    public override void Execute()
    {
        var doc = Application.ActiveUIDocument.Document;
        var relatorio = new StringBuilder();

        var tubos = new FilteredElementCollector(doc).OfClass(typeof(Pipe)).Cast<Pipe>().ToList();
        relatorio.AppendLine($"Tubos (classe Pipe): {tubos.Count}");
        if (tubos.Count > 0) relatorio.AppendLine("  parâmetros do primeiro tubo: " + NomesDeParametros(tubos[0]));

        var sistemas = new FilteredElementCollector(doc).OfClass(typeof(PipingSystem)).Cast<PipingSystem>().ToList();
        relatorio.AppendLine();
        relatorio.AppendLine($"Sistemas de tubulação (PipingSystem) — candidatos a fonte/VGA: {sistemas.Count}");
        foreach (var sistema in sistemas.Take(10)) relatorio.AppendLine($"  {sistema.Name}");

        var instancias = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();

        var candidatosChuveiro = instancias
            .Where(fi => fi.Symbol is { } simbolo && PalavrasChuveiro.Any(p => simbolo.Family.Name.ToLowerInvariant().Contains(p)))
            .GroupBy(fi => (Categoria: fi.Category?.Name ?? "sem categoria", Familia: fi.Symbol.Family.Name, Tipo: fi.Symbol.Name))
            .ToList();

        relatorio.AppendLine();
        relatorio.AppendLine($"Candidatos a chuveiro (nome da família contém chuveiro/sprinkler/spk/esfr): {candidatosChuveiro.Count} combinações categoria+família+tipo");
        foreach (var grupo in candidatosChuveiro.Take(15))
        {
            relatorio.AppendLine($"  [{grupo.Key.Categoria}] {grupo.Key.Familia} / {grupo.Key.Tipo} — {grupo.Count()} instância(s)");
            relatorio.AppendLine("    parâmetros: " + NomesDeParametros(grupo.First()));
        }

        var categorias = instancias.Select(fi => fi.Category?.Name ?? "sem categoria").Distinct().OrderBy(c => c).ToList();
        relatorio.AppendLine();
        relatorio.AppendLine($"Todas as categorias de instância de família no modelo ({categorias.Count}), use esta lista se a busca por palavra não achou nada:");
        relatorio.AppendLine("  " + string.Join(", ", categorias));

        var caminho = Path.Combine(PastasSpk.Raiz, "descoberta.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, relatorio.ToString());

        TaskDialog.Show("Spk — Descobrir famílias", $"""
            Tubos: {tubos.Count}  |  Sistemas: {sistemas.Count}  |  Candidatos a chuveiro: {candidatosChuveiro.Count}  |  Categorias no modelo: {categorias.Count}

            Relatório completo gravado em:
            {caminho}

            Abrindo a janela de confirmação...
            """);

        if (candidatosChuveiro.Count == 0) return;

        var candidatos = candidatosChuveiro.Select(grupo =>
        {
            var elemento = grupo.First();
            var fatorK = LerMedida(elemento, SugestaoDeParametro.PalavrasFatorK);
            var area = LerMedida(elemento, SugestaoDeParametro.PalavrasArea);
            return CandidatoFamilia.Criar(
                grupo.Key.Categoria, grupo.Key.Familia, grupo.Key.Tipo, grupo.Count(),
                NomesDeParametros(elemento).Split(", "),
                parametroFatorK: fatorK.Nome, valorFatorK: fatorK.Valor,
                parametroAreaCobertura: area.Nome, valorAreaCobertura: area.Valor);
        }).ToList();

        var janela = new JanelaMapeamento(candidatos);
        if (janela.ShowDialog() != true || janela.Resultado is not { } confirmado) return;

        var mapeamentoPath = Path.Combine(PastasSpk.Raiz, "mapeamento.json");
        File.WriteAllText(mapeamentoPath, JsonSerializer.Serialize(confirmado.Select(c => new
        {
            c.Categoria,
            c.Familia,
            c.Tipo,
            Papel = c.Papel.ToString(),
            c.ParametroFatorK,
            c.ValorFatorK,
            c.ParametroAreaCobertura,
            c.ValorAreaCobertura,
        }), new JsonSerializerOptions { WriteIndented = true }));

        TaskDialog.Show("Spk — Mapeamento confirmado", $"Mapeamento gravado em:\n{mapeamentoPath}");
    }

    static string NomesDeParametros(Element elemento) =>
        string.Join(", ", TodosOsParametros(elemento).Select(p => p.Definition.Name).Distinct().OrderBy(n => n).Take(25));

    /// <summary>Tipo primeiro: o fator K e a área de cobertura costumam ser parâmetro de tipo, não de instância.</summary>
    static (string Nome, string Valor) LerMedida(Element elemento, string[] palavras)
    {
        var parametro = TodosOsParametros(elemento).FirstOrDefault(p =>
            palavras.Any(k => p.Definition.Name.Contains(k, StringComparison.OrdinalIgnoreCase)));
        return parametro is null ? ("", "") : (parametro.Definition.Name, ValorLegivel(parametro));
    }

    static IEnumerable<Parameter> TodosOsParametros(Element elemento)
    {
        if (elemento is FamilyInstance { Symbol: { } simbolo })
            foreach (Parameter parametro in simbolo.Parameters) yield return parametro;
        foreach (Parameter parametro in elemento.Parameters) yield return parametro;
    }

    static string ValorLegivel(Parameter parametro)
    {
        var texto = parametro.AsValueString();
        if (!string.IsNullOrWhiteSpace(texto)) return texto;
        return parametro.StorageType switch
        {
            StorageType.Double => parametro.AsDouble().ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
            StorageType.Integer => parametro.AsInteger().ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
            StorageType.String => parametro.AsString() ?? "",
            _ => "",
        };
    }
}
