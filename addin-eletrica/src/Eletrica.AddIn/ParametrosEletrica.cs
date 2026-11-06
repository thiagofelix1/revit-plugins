using System.IO;

namespace Eletrica.AddIn;

/// <summary>
///     Parâmetros compartilhados "AE" que o plugin cria no projeto e preenche. Os GUIDs nunca mudam: é por eles que o
///     plugin, as tabelas e as etiquetas do escritório acham o valor, em qualquer projeto e idioma.
/// </summary>
static class ParametrosEletrica
{
    // Eletrodutos
    public static readonly Guid Circuitos = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A11");
    public static readonly Guid Condutores = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A12");
    public static readonly Guid Fiacao = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A13");
    public static readonly Guid DnSugerido = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A14");
    public static readonly Guid Ocupacao = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A15");
    public static readonly Guid Retornos = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A16");
    public static readonly Guid Forcar = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A17");
    public static readonly Guid Evitar = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A18");

    // Circuitos (ElectricalSystem)
    public static readonly Guid Secao = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A21");
    public static readonly Guid Disjuntor = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A22");
    public static readonly Guid CorrenteDeProjeto = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A23");
    public static readonly Guid Queda = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A24");
    public static readonly Guid DR = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A25");
    public static readonly Guid Situacao = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A26");
    public static readonly Guid Comprimento = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A27");
    public static readonly Guid TipoDeCarga = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A28");

    // Pontos (tomadas, luminárias, equipamentos)
    public static readonly Guid Ambiente = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A31");
    public static readonly Guid Potencia = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A32");
    public static readonly Guid TipoDoPonto = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A33");
    public static readonly Guid Altura = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A34");
    public static readonly Guid Comando = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A35");
    public static readonly Guid TipoDeComando = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A36");
    public static readonly Guid Posicao = new("5E2A9C1B-7D34-4F68-A0B2-1C3D5E7F9A37");

    static readonly BuiltInCategory[] Condutos = [BuiltInCategory.OST_Conduit, BuiltInCategory.OST_ConduitFitting, BuiltInCategory.OST_Wire];
    static readonly BuiltInCategory[] Circuito = [BuiltInCategory.OST_ElectricalCircuit];

    static readonly BuiltInCategory[] Pontos =
    [
        BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
        BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_MechanicalEquipment,
    ];

    sealed record Definicao(Guid Id, string Nome, ForgeTypeId Tipo, BuiltInCategory[] Categorias);

    static readonly Definicao[] Definicoes =
    [
        new(Circuitos, "AE Circuitos", SpecTypeId.String.Text, Condutos),
        new(Condutores, "AE Condutores", SpecTypeId.Int.Integer, Condutos),
        new(Fiacao, "AE Fiação", SpecTypeId.String.Text, Condutos),
        new(DnSugerido, "AE DN sugerido", SpecTypeId.Int.Integer, Condutos),
        new(Ocupacao, "AE Ocupação (%)", SpecTypeId.Number, Condutos),
        new(Retornos, "AE Retornos", SpecTypeId.String.Text, Condutos),
        new(Forcar, "AE Forçar circuitos", SpecTypeId.String.Text, Condutos),
        new(Evitar, "AE Evitar circuitos", SpecTypeId.String.Text, Condutos),
        new(Secao, "AE Seção (F/N/PE mm²)", SpecTypeId.String.Text, Circuito),
        new(Disjuntor, "AE Disjuntor", SpecTypeId.String.Text, Circuito),
        new(CorrenteDeProjeto, "AE IB (A)", SpecTypeId.Number, Circuito),
        new(Queda, "AE Queda (%)", SpecTypeId.Number, Circuito),
        new(DR, "AE DR", SpecTypeId.String.Text, Circuito),
        new(Situacao, "AE Situação", SpecTypeId.String.Text, Circuito),
        new(Comprimento, "AE Comprimento (m)", SpecTypeId.Number, Circuito),
        new(TipoDeCarga, "AE Tipo de carga", SpecTypeId.String.Text, Circuito),
        new(Ambiente, "AE Ambiente", SpecTypeId.String.Text, Pontos),
        new(Potencia, "AE Potência (VA)", SpecTypeId.Number, Pontos),
        new(TipoDoPonto, "AE Tipo do ponto", SpecTypeId.String.Text, Pontos),
        new(Altura, "AE Altura (m)", SpecTypeId.Number, Pontos),
        new(Comando, "AE Comando", SpecTypeId.String.Text, Pontos),
        new(TipoDeComando, "AE Tipo de comando", SpecTypeId.String.Text, Pontos),
        new(Posicao, "AE Posição", SpecTypeId.String.Text, Pontos),
    ];

    /// <summary>Cria e vincula os que faltam. Precisa de transação aberta. Devolve o motivo se não conseguiu.</summary>
    public static string? Garantir(Document doc)
    {
        var faltando = Definicoes.Where(d => SharedParameterElement.Lookup(doc, d.Id) is null).ToList();
        if (faltando.Count == 0) return null;

        var app = doc.Application;
        var original = app.SharedParametersFilename;
        var arquivo = Path.Combine(PastasEletrica.Raiz, "parametros-autoeletrica.txt");
        Directory.CreateDirectory(PastasEletrica.Raiz);
        if (!File.Exists(arquivo)) File.WriteAllText(arquivo, "");
        try
        {
            app.SharedParametersFilename = arquivo;
            var definicoes = app.OpenSharedParameterFile() ?? throw new InvalidOperationException("Não abriu o arquivo de parâmetros.");
            var grupo = definicoes.Groups.get_Item("AutoEletrica") ?? definicoes.Groups.Create("AutoEletrica");
            foreach (var d in faltando)
            {
                var externa = grupo.Definitions.OfType<ExternalDefinition>().FirstOrDefault(e => e.GUID == d.Id)
                              ?? (ExternalDefinition)grupo.Definitions.Create(new ExternalDefinitionCreationOptions(d.Nome, d.Tipo) { GUID = d.Id });
                var categorias = new CategorySet();
                foreach (var c in d.Categorias)
                    if (Category.GetCategory(doc, c) is { AllowsBoundParameters: true } categoria) categorias.Insert(categoria);
                if (categorias.IsEmpty) continue;
                doc.ParameterBindings.Insert(externa, new InstanceBinding(categorias), GroupTypeId.Data);
            }
            return null;
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException or IOException)
        {
            return "Os parâmetros AE não foram criados: " + e.Message;
        }
        finally
        {
            try { app.SharedParametersFilename = original ?? ""; }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { }
        }
    }

    public static void Gravar(Element elemento, Guid id, string valor) => Definir(elemento, id, p => p.Set(valor));
    public static void Gravar(Element elemento, Guid id, double valor) => Definir(elemento, id, p => p.Set(valor));
    public static void Gravar(Element elemento, Guid id, int valor) => Definir(elemento, id, p => p.Set(valor));

    public static string? LerTexto(Element elemento, Guid id) =>
        elemento.get_Parameter(id) is { HasValue: true } p ? p.AsString() : null;

    public static double? LerNumero(Element elemento, Guid id) =>
        elemento.get_Parameter(id) is { HasValue: true } p ? p.AsDouble() : null;

    static void Definir(Element elemento, Guid id, Action<Parameter> definir)
    {
        if (elemento.get_Parameter(id) is not { IsReadOnly: false } parametro) return;
        if (elemento.GroupId != ElementId.InvalidElementId && parametro.Definition is InternalDefinition { VariesAcrossGroups: false }) return;
        definir(parametro);
    }
}
