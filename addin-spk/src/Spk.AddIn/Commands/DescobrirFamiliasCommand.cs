using System.IO;
using System.Text;
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
///     (para o caso de a busca por palavra não achar nada). Só grava no modelo o mapeamento confirmado.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DescobrirFamiliasCommand : ExternalCommand
{
    static readonly string[] PalavrasChuveiro = ["chuveiro", "sprinkler", "spk", "esfr"];
    static readonly string[] PalavrasVga = ["vga", "governo", "alarme", "alarm"];

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

        // Pela categoria Sprinklers ou pelo nome: família de bico com outro nome (o código do fabricante) também entra.
        var candidatosChuveiro = instancias
            .Where(fi => fi.Symbol is { } simbolo && (fi.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers
                                                      || PalavrasChuveiro.Any(p => simbolo.Family.Name.ToLowerInvariant().Contains(p))))
            .GroupBy(fi => (Categoria: fi.Category?.Name ?? "sem categoria", Familia: fi.Symbol.Family.Name, Tipo: fi.Symbol.Name))
            .ToList();

        var candidatosVga = instancias
            .Where(fi => fi.Symbol is { } simbolo
                         && fi.Category?.Id.Value == (long)BuiltInCategory.OST_PipeAccessory
                         && PalavrasVga.Any(p => $"{simbolo.Family.Name} {simbolo.Name}".ToLowerInvariant().Contains(p)))
            .GroupBy(fi => (Categoria: fi.Category?.Name ?? "sem categoria", Familia: fi.Symbol.Family.Name, Tipo: fi.Symbol.Name))
            .ToList();

        relatorio.AppendLine();
        relatorio.AppendLine($"Candidatos a VGA (acessório com vga/governo/alarme no nome): {candidatosVga.Count}");
        foreach (var grupo in candidatosVga.Take(10))
            relatorio.AppendLine($"  {grupo.Key.Familia} / {grupo.Key.Tipo} — {grupo.Count()} instância(s)");

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

        if (candidatosChuveiro.Count == 0 && candidatosVga.Count == 0)
        {
            // Sem nada para confirmar, a janela não abre: o projetista precisa saber por quê e o que fazer.
            TaskDialog.Show("SPK — Famílias", $"""
                Nenhum bico nem VGA no modelo para confirmar.

                Famílias lista os bicos que já estão no modelo: da categoria Sprinklers, ou com chuveiro, sprinkler, spk ou esfr no nome da família. Ponha os bicos antes (Gerar rede, Bicos por ambiente ou à mão) e rode Famílias de novo.

                Tubos: {tubos.Count}  |  Sistemas: {sistemas.Count}  |  Categorias no modelo: {categorias.Count}
                Relatório completo em:
                {caminho}
                """);
            return;
        }

        TaskDialog.Show("SPK — Famílias", $"""
            Tubos: {tubos.Count}  |  Sistemas: {sistemas.Count}  |  Candidatos a chuveiro: {candidatosChuveiro.Count}  |  VGAs: {candidatosVga.Count}  |  Categorias no modelo: {categorias.Count}

            Relatório completo gravado em:
            {caminho}

            Abrindo a janela de confirmação...
            """);

        var candidatos = candidatosChuveiro.Select(grupo =>
        {
            var elemento = grupo.First();
            var fatorK = LerMedida(elemento, SugestaoDeParametro.PalavrasFatorK);
            var area = LerMedida(elemento, SugestaoDeParametro.PalavrasArea);
            var valorK = fatorK.Valor;
            string? notaK = null;
            if (LinhaMapeamento.Numero(fatorK.Valor) is { } lido)
            {
                var (metrico, nota) = ConversaoFatorK.ParaMetrico(lido);
                notaK = nota;
                if (nota is not null) valorK = metrico.ToString("0.#", Entrada.Cultura);
            }
            return CandidatoFamilia.Criar(
                grupo.Key.Categoria, grupo.Key.Familia, grupo.Key.Tipo, grupo.Count(),
                NomesDeParametros(elemento).Split(", "),
                parametroFatorK: fatorK.Nome, valorFatorK: valorK,
                parametroAreaCobertura: area.Nome, valorAreaCobertura: area.Valor, notaFatorK: notaK);
        }).ToList();
        candidatos.AddRange(candidatosVga.Select(grupo => CandidatoFamilia.Criar(
            grupo.Key.Categoria, grupo.Key.Familia, grupo.Key.Tipo, grupo.Count(),
            NomesDeParametros(grupo.First()).Split(", "), PapelFamilia.Fonte, parametroFatorK: "", parametroAreaCobertura: "")));

        // Os outros acessórios entram ignorados: se a VGA do escritório tem outro nome, o engenheiro marca como Fonte aqui.
        var jaListados = candidatosVga.Select(g => g.Key).ToHashSet();
        candidatos.AddRange(instancias
            .Where(fi => fi.Symbol is not null && fi.Category?.Id.Value == (long)BuiltInCategory.OST_PipeAccessory)
            .GroupBy(fi => (Categoria: fi.Category?.Name ?? "sem categoria", Familia: fi.Symbol.Family.Name, Tipo: fi.Symbol.Name))
            .Where(g => !jaListados.Contains(g.Key))
            .OrderBy(g => g.Key.Familia)
            .Select(grupo => CandidatoFamilia.Criar(
                grupo.Key.Categoria, grupo.Key.Familia, grupo.Key.Tipo, grupo.Count(),
                NomesDeParametros(grupo.First()).Split(", "), PapelFamilia.Ignorar, parametroFatorK: "", parametroAreaCobertura: "")));

        var janela = new JanelaMapeamento(candidatos);
        if (janela.ShowDialog() != true || janela.Resultado is not { } confirmado) return;

        MapeamentoGravado.Salvar(doc, confirmado.Select(c => new LinhaMapeamento
        {
            Categoria = c.Categoria,
            Familia = c.Familia,
            Tipo = c.Tipo,
            Papel = c.Papel.ToString(),
            ParametroFatorK = c.ParametroFatorK,
            ValorFatorK = c.ValorFatorK,
            ParametroAreaCobertura = c.ParametroAreaCobertura,
            ValorAreaCobertura = c.ValorAreaCobertura,
        }).ToList());

        var soltos = BicosDoModelo.SoltarConfirmados(doc);
        TaskDialog.Show("Spk — Mapeamento confirmado", "Mapeamento gravado neste projeto." +
            (soltos > 0 ? $" {soltos} bico(s) gerados passam a usar o K e a área confirmados." : "") + " Agora use Calcular.");
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
