using System.IO;

namespace Spk.AddIn;

/// <summary>
///     Parâmetros compartilhados que o plugin cria no projeto e preenche ao calcular. Os GUIDs nunca mudam: é por eles
///     que o plugin, as tabelas e as etiquetas do escritório acham o valor, em qualquer projeto e idioma.
/// </summary>
static class ParametrosSpk
{
    public static readonly Guid Numero = new("D4437A1D-F63A-489E-B271-DE850EEB2498");
    public static readonly Guid Setor = new("D7274AA0-F664-482F-9E4F-72FABA0C446C");
    public static readonly Guid AreaCalculo = new("BB6C7634-EFE9-458E-8324-48E150595129");
    public static readonly Guid Pressao = new("A5C0C2EA-2680-4110-B19F-37D72FD824C9");
    public static readonly Guid Vazao = new("36DC6065-DB6C-472B-8051-1012844FAA38");
    public static readonly Guid Velocidade = new("7376AF84-A952-4C41-B93C-BB2EE7BE99A7");

    sealed record Definicao(Guid Id, string Nome, ForgeTypeId Tipo, BuiltInCategory[] Categorias);

    static readonly Definicao[] Definicoes =
    [
        new(Numero, "SPK Nº", SpecTypeId.String.Text, [BuiltInCategory.OST_Sprinklers]),
        new(Setor, "SPK Setor", SpecTypeId.String.Text, [BuiltInCategory.OST_Sprinklers, BuiltInCategory.OST_PipeCurves]),
        new(AreaCalculo, "SPK Área de cálculo", SpecTypeId.Boolean.YesNo, [BuiltInCategory.OST_Sprinklers]),
        new(Pressao, "SPK Pressão (kPa)", SpecTypeId.Number, [BuiltInCategory.OST_Sprinklers]),
        new(Vazao, "SPK Vazão (L/min)", SpecTypeId.Number, [BuiltInCategory.OST_Sprinklers, BuiltInCategory.OST_PipeCurves]),
        new(Velocidade, "SPK Velocidade (m/s)", SpecTypeId.Number, [BuiltInCategory.OST_PipeCurves]),
    ];

    /// <summary>
    /// Cria e vincula os que faltam e deixa todos variarem entre grupos. Precisa de transação aberta. Devolve o motivo se
    /// não conseguiu.
    /// </summary>
    public static string? Garantir(Document doc)
    {
        var aviso = Criar(doc);
        PermitirVariarEntreGrupos(doc);
        return aviso;
    }

    /// <summary>
    /// Bico e tubo dentro de grupo só aceitam valor próprio se o parâmetro "varia entre grupos". Sem isso o Revit recusa
    /// a transação inteira ("alterações nos grupos são somente permitidas no modo de edição"), como na Nova Era.
    /// </summary>
    static void PermitirVariarEntreGrupos(Document doc)
    {
        foreach (var d in Definicoes)
        {
            if (SharedParameterElement.Lookup(doc, d.Id)?.GetDefinition() is not { VariesAcrossGroups: false } definicao) continue;
            try { definicao.SetAllowVaryBetweenGroups(doc, true); }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { }
        }
    }

    static string? Criar(Document doc)
    {
        var faltando = Definicoes.Where(d => SharedParameterElement.Lookup(doc, d.Id) is null).ToList();
        if (faltando.Count == 0) return null;

        var app = doc.Application;
        var original = app.SharedParametersFilename;
        var arquivo = Path.Combine(PastasSpk.Raiz, "parametros-spk.txt");
        Directory.CreateDirectory(PastasSpk.Raiz);
        if (!File.Exists(arquivo)) File.WriteAllText(arquivo, "");
        try
        {
            app.SharedParametersFilename = arquivo;
            var definicoes = app.OpenSharedParameterFile() ?? throw new InvalidOperationException("Não abriu o arquivo de parâmetros.");
            var grupo = definicoes.Groups.get_Item("SPK") ?? definicoes.Groups.Create("SPK");
            foreach (var d in faltando)
            {
                var externa = grupo.Definitions.OfType<ExternalDefinition>().FirstOrDefault(e => e.GUID == d.Id)
                              ?? (ExternalDefinition)grupo.Definitions.Create(new ExternalDefinitionCreationOptions(d.Nome, d.Tipo) { GUID = d.Id });
                var categorias = new CategorySet();
                foreach (var c in d.Categorias)
                    if (Category.GetCategory(doc, c) is { AllowsBoundParameters: true } categoria) categorias.Insert(categoria);
                doc.ParameterBindings.Insert(externa, new InstanceBinding(categorias), GroupTypeId.Data);
            }
            return null;
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException or IOException)
        {
            return "Os parâmetros SPK não foram criados: " + e.Message;
        }
        finally
        {
            try { app.SharedParametersFilename = original ?? ""; }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { }
        }
    }

    public static void Gravar(Element elemento, Guid id, string valor) =>
        Definir(elemento, id, p => p.Set(valor));

    public static void Gravar(Element elemento, Guid id, double valor) =>
        Definir(elemento, id, p => p.Set(valor));

    public static void Gravar(Element elemento, Guid id, bool valor) =>
        Definir(elemento, id, p => p.Set(valor ? 1 : 0));

    public static string? LerTexto(Element elemento, Guid id) => elemento.get_Parameter(id)?.AsString();

    public static bool LerSimNao(Element elemento, Guid id) => elemento.get_Parameter(id)?.AsInteger() == 1;

    /// <summary>Membro de grupo só recebe valor se o parâmetro varia entre grupos; senão o Revit derruba a transação.</summary>
    static void Definir(Element elemento, Guid id, Action<Parameter> definir)
    {
        if (elemento.get_Parameter(id) is not { IsReadOnly: false } parametro) return;
        if (elemento.GroupId != ElementId.InvalidElementId && parametro.Definition is InternalDefinition { VariesAcrossGroups: false }) return;
        definir(parametro);
    }
}
