using System.Text.RegularExpressions;

namespace Spk.AddIn.Folhas;

/// <summary>
///     O carimbo das folhas do SPK, no padrão do projeto: número na sequência do escritório (09, 13...), código da prancha
///     num parâmetro da folha (o "Código de Projeto" da Nova Era) e os campos que o projeto repete em todas as folhas
///     (CNPJ, lote, registro do responsável, desenhado e verificado por), copiados das outras folhas do mesmo projeto.
///     Cada folha do SPK tem um papel fixo (SPK-ISO, SPK-SETOR1-01...), guardado no modelo junto do id da folha: é por ele
///     que o plugin reconhece e recria as suas folhas, mesmo depois de renumeradas. Folha que não é do SPK nunca é apagada.
/// </summary>
static class CarimboSpk
{
    const string Chave = "FolhasSpk";

    /// <summary>Parâmetros que mudam de folha para folha e não são copiados.</summary>
    static readonly BuiltInParameter[] PorFolha =
    [
        BuiltInParameter.SHEET_NUMBER, BuiltInParameter.SHEET_NAME, BuiltInParameter.SHEET_ISSUE_DATE,
    ];

    static Dictionary<string, string> Registro(Document doc) =>
        ArmazenamentoSpk.Ler<Dictionary<string, string>>(doc, Chave) ?? new Dictionary<string, string>();

    /// <summary>Precisa de transação aberta.</summary>
    public static void Registrar(Document doc, string papel, ViewSheet folha)
    {
        var registro = Registro(doc);
        registro[papel] = folha.UniqueId;
        ArmazenamentoSpk.Gravar(doc, Chave, registro);
    }

    /// <summary>A folha que o SPK criou para este papel numa rodada anterior, se ainda existir.</summary>
    public static ViewSheet? Registrada(Document doc, string papel) =>
        Registro(doc).TryGetValue(papel, out var unico) ? doc.GetElement(unico) as ViewSheet : null;

    public static string? Papel(Document doc, ViewSheet folha) =>
        folha.SheetNumber.StartsWith(PranchaSpk.PrefixoNumero, StringComparison.Ordinal)
            ? folha.SheetNumber
            : Registro(doc).FirstOrDefault(p => p.Value == folha.UniqueId).Key;

    public static bool EhDoSpk(Document doc, ViewSheet folha) => Papel(doc, folha) is not null;

    public static List<ViewSheet> FolhasDoSpk(Document doc)
    {
        var registradas = Registro(doc).Values.ToHashSet();
        return new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
            .Where(f => f.SheetNumber.StartsWith(PranchaSpk.PrefixoNumero, StringComparison.Ordinal) || registradas.Contains(f.UniqueId))
            .ToList();
    }

    /// <summary>
    /// Depois de geradas as folhas, na ordem do PDF: número, código e campos repetidos do projeto. Precisa de transação
    /// aberta. Devolve o que não deu para fazer.
    /// </summary>
    public static List<string> Preencher(Document doc, IReadOnlyList<ViewSheet> folhas)
    {
        var avisos = new List<string>();
        if (folhas.Count == 0) return avisos;
        var padrao = PadraoEscritorio.Carregar(doc);
        var nossas = folhas.Select(f => f.Id).ToHashSet();
        var outras = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
            .Where(f => !nossas.Contains(f.Id) && !f.IsPlaceholder && !EhDoSpk(doc, f))
            .ToList();

        Numerar(folhas, outras, padrao.NumeroInicial, avisos);
        if (padrao.CodigoFolha is { Length: > 0 } molde && padrao.ParametroCodigo is { Length: > 0 } nome)
            Codificar(doc, folhas, molde, nome, avisos);
        CopiarCampos(folhas, outras, padrao.ParametroCodigo);
        return avisos;
    }

    /// <summary>Números na sequência a partir do inicial, com os zeros dele ("09"). Se algum já for de outra folha, nenhum muda.</summary>
    static void Numerar(IReadOnlyList<ViewSheet> folhas, List<ViewSheet> outras, string? inicial, List<string> avisos)
    {
        if (string.IsNullOrWhiteSpace(inicial)) return;
        if (!int.TryParse(inicial.Trim(), out var primeiro))
        {
            avisos.Add($"Número inicial \"{inicial}\" não é um número; as folhas ficaram com SPK-….");
            return;
        }
        var largura = inicial.Trim().Length;
        var numeros = Enumerable.Range(0, folhas.Count).Select(i => (primeiro + i).ToString().PadLeft(largura, '0')).ToList();
        var ocupadas = outras.GroupBy(f => f.SheetNumber).ToDictionary(g => g.Key, g => g.First());
        var colisao = numeros.FirstOrDefault(ocupadas.ContainsKey);
        if (colisao is not null)
        {
            avisos.Add($"O número {colisao} já é da folha \"{ocupadas[colisao].Name}\". As folhas do SPK ficaram com SPK-…; " +
                       "mude o número inicial no Padrão do escritório.");
            return;
        }
        for (var i = 0; i < folhas.Count; i++) folhas[i].SheetNumber = numeros[i];
    }

    static void Codificar(Document doc, IReadOnlyList<ViewSheet> folhas, string molde, string nome, List<string> avisos)
    {
        foreach (var folha in folhas)
        {
            var parametro = folha.LookupParameter(nome);
            if (parametro is null || parametro.IsReadOnly || parametro.StorageType != StorageType.String)
            {
                avisos.Add($"As folhas não têm o parâmetro de texto \"{nome}\"; o código da prancha ficou em branco.");
                return;
            }
            var tipo = Papel(doc, folha) == FolhasIsometrico.Numero ? "ISO" : "PLA";
            parametro.Set(Codigo(molde, folha.SheetNumber, tipo));
        }
    }

    /// <summary>{numero} ou {numero:000} (com zeros à esquerda) e {tipo} (PLA ou ISO).</summary>
    public static string Codigo(string molde, string numero, string tipo)
    {
        var texto = Regex.Replace(molde, @"\{numero(?::(0+))?\}", m =>
            m.Groups[1].Success && int.TryParse(numero, out var n) ? n.ToString().PadLeft(m.Groups[1].Length, '0') : numero);
        return texto.Replace("{tipo}", tipo);
    }

    /// <summary>
    /// Campos de texto que as outras folhas do projeto têm com o mesmo valor (pelo menos metade delas): CNPJ, lote,
    /// responsável, desenhado e verificado por. Vêm deste projeto, nunca de outro, para não levar dado de outro cliente.
    /// </summary>
    static void CopiarCampos(IReadOnlyList<ViewSheet> folhas, List<ViewSheet> outras, string? parametroCodigo)
    {
        if (outras.Count == 0) return;
        foreach (Parameter parametro in folhas[0].Parameters)
        {
            if (parametro.IsReadOnly || parametro.StorageType != StorageType.String || parametro.Definition is not { } definicao) continue;
            if (definicao.Name == parametroCodigo) continue;
            if (definicao is InternalDefinition { BuiltInParameter: var b } && PorFolha.Contains(b)) continue;

            var valores = outras.Select(o => o.get_Parameter(definicao)?.AsString()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            var moda = valores.GroupBy(v => v).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (moda is null || moda.Count() * 2 < outras.Count) continue;
            foreach (var folha in folhas) folha.get_Parameter(definicao)?.Set(moda.Key!);
        }
    }

    /// <summary>
    /// Sugestão de numeração e código a partir das folhas que o projeto já tem: o parâmetro de texto que contém o número
    /// da folha, o molde tirado do código da última folha e o próximo número livre.
    /// </summary>
    public static (string? NumeroInicial, string? Molde, string? Parametro) Sugerir(Document doc)
    {
        var folhas = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
            .Where(f => !f.IsPlaceholder && !EhDoSpk(doc, f))
            .Select(f => (Folha: f, Numero: int.TryParse(f.SheetNumber, out var n) ? n : (int?)null))
            .Where(p => p.Numero is not null)
            .ToList();
        if (folhas.Count == 0) return (null, null, null);

        var ultima = folhas.OrderByDescending(p => p.Numero).First();
        var largura = folhas.Max(p => p.Folha.SheetNumber.Length);
        var inicial = (ultima.Numero!.Value + 1).ToString().PadLeft(largura, '0');

        string? parametro = null;
        string? molde = null;
        foreach (Parameter p in ultima.Folha.Parameters)
        {
            if (p.IsReadOnly || p.StorageType != StorageType.String || p.Definition is not { } definicao) continue;
            if (definicao is InternalDefinition { BuiltInParameter: not BuiltInParameter.INVALID }) continue;
            var comNumero = folhas.Count(f => ContemNumero(f.Folha.get_Parameter(definicao)?.AsString(), f.Numero!.Value));
            if (comNumero * 10 < folhas.Count * 6) continue;
            parametro = definicao.Name;
            molde = Molde(p.AsString() ?? "", ultima.Numero.Value);
            break;
        }
        return (inicial, molde, parametro);
    }

    static bool ContemNumero(string? texto, int numero) =>
        texto is not null && Regex.IsMatch(texto, $@"(?<!\d)0*{numero}(?!\d)");

    /// <summary>NEPN-INC-013-LG-SPK-ISO-R02 → NEPN-INC-{numero:000}-LG-SPK-{tipo}-R00.</summary>
    static string Molde(string codigo, int numero)
    {
        var texto = Regex.Replace(codigo, $@"(?<!\d)(0*{numero})(?!\d)", m => "{numero:" + new string('0', m.Groups[1].Length) + "}");
        texto = Regex.Replace(texto, @"(?<=-)(PLA|ISO|COB|DET)(?=-)", "{tipo}");
        return Regex.Replace(texto, @"R\d+$", "R00");
    }
}
