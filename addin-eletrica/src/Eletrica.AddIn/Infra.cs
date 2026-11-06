using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace Eletrica.AddIn;

/// <summary>Pasta do plugin em Documentos\AutoEletrica (a temporária muda a cada sessão do Revit).</summary>
static class PastasEletrica
{
    public static string Raiz => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AutoEletrica");
    public static string Calculo => Path.Combine(Raiz, "calculo");
}

/// <summary>
///     Dados do plugin gravados dentro do .rvt: um DataStorage por chave, com o objeto em JSON. Ficam com o projeto e vão
///     junto quando o arquivo é copiado. O GUID do esquema nunca muda.
/// </summary>
static class ArmazenamentoEletrica
{
    static readonly Guid IdEsquema = new("9D1F4B6E-3C7A-4E2B-8F5D-6A0C1E9B7D34");
    const string CampoChave = "Chave";
    const string CampoJson = "Json";

    public static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public static T? Ler<T>(Document doc, string chave) where T : class
    {
        if (Schema.Lookup(IdEsquema) is not { } esquema) return null;
        if (Armazem(doc, esquema, chave)?.GetEntity(esquema) is not { } entidade || !entidade.IsValid()) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(entidade.Get<string>(CampoJson), Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Precisa de uma transação aberta.</summary>
    public static void Gravar<T>(Document doc, string chave, T valor)
    {
        var esquema = Esquema();
        var armazem = Armazem(doc, esquema, chave) ?? DataStorage.Create(doc);
        var entidade = new Entity(esquema);
        entidade.Set(CampoChave, chave);
        entidade.Set(CampoJson, JsonSerializer.Serialize(valor, Json));
        armazem.SetEntity(entidade);
    }

    public static void GravarComTransacao<T>(Document doc, string chave, T valor, string nome)
    {
        using var transacao = new Transaction(doc, nome);
        transacao.Start();
        Gravar(doc, chave, valor);
        transacao.Commit();
    }

    static DataStorage? Armazem(Document doc, Schema esquema, string chave) =>
        new FilteredElementCollector(doc).OfClass(typeof(DataStorage)).Cast<DataStorage>()
            .FirstOrDefault(d => d.GetEntity(esquema) is { } e && e.IsValid() && e.Get<string>(CampoChave) == chave);

    static Schema Esquema()
    {
        if (Schema.Lookup(IdEsquema) is { } existente) return existente;
        var construtor = new SchemaBuilder(IdEsquema);
        construtor.SetSchemaName("AutoEletricaDados");
        construtor.SetReadAccessLevel(AccessLevel.Public);
        construtor.SetWriteAccessLevel(AccessLevel.Public);
        construtor.AddSimpleField(CampoChave, typeof(string));
        construtor.AddSimpleField(CampoJson, typeof(string));
        return construtor.Finish();
    }
}

/// <summary>Conversões de unidade do Revit (pés, pés², VA, V, A internos) para as do motor (m, m², VA, V, A).</summary>
static class Unidades
{
    public static double Metros(double interno) => UnitUtils.ConvertFromInternalUnits(interno, UnitTypeId.Meters);
    public static double Milimetros(double interno) => UnitUtils.ConvertFromInternalUnits(interno, UnitTypeId.Millimeters);
    public static double MetrosQuadrados(double interno) => UnitUtils.ConvertFromInternalUnits(interno, UnitTypeId.SquareMeters);
    public static double VoltAmperes(double interno) => UnitUtils.ConvertFromInternalUnits(interno, UnitTypeId.VoltAmperes);
    public static double Volts(double interno) => UnitUtils.ConvertFromInternalUnits(interno, UnitTypeId.Volts);

    /// <summary>
    ///     Tensão de um tipo de tensão (VoltageType) do projeto, em volts. Ao contrário dos parâmetros (a tensão do conector, por
    ///     exemplo, vem em unidade interna), o VoltageType guarda volts: converter lia o 120/208 do Snowdon como 11/19 V, e o
    ///     sistema "220 V" criado com a conversão tinha 2 368 V, que nenhum quadro aceitava (06/11/2026).
    /// </summary>
    public static double Volts(Autodesk.Revit.DB.Electrical.VoltageType tipo) => tipo.ActualValue;
    public static double Amperes(double interno) => UnitUtils.ConvertFromInternalUnits(interno, UnitTypeId.Amperes);
    public static double DeMetros(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
}
