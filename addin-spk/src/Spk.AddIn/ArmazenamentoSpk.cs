using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace Spk.AddIn;

/// <summary>
///     Dados do plugin gravados dentro do .rvt: um DataStorage por chave, com o objeto em JSON.
///     Ficam com o projeto e vão junto quando o arquivo é copiado. O GUID do esquema nunca muda.
/// </summary>
static class ArmazenamentoSpk
{
    static readonly Guid IdEsquema = new("2FC63760-8FB0-4513-A2D8-B6FB0A48989C");
    const string CampoChave = "Chave";
    const string CampoJson = "Json";

    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

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

    /// <summary>Grava numa transação própria. Para comandos que não têm outra transação aberta.</summary>
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
        construtor.SetSchemaName("SpkDados");
        construtor.SetReadAccessLevel(AccessLevel.Public);
        construtor.SetWriteAccessLevel(AccessLevel.Public);
        construtor.AddSimpleField(CampoChave, typeof(string));
        construtor.AddSimpleField(CampoJson, typeof(string));
        return construtor.Finish();
    }
}
