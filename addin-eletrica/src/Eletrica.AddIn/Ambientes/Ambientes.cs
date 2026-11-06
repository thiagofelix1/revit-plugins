using Autodesk.Revit.DB.Architecture;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Ambientes;

/// <summary>Um ambiente lido do arquivo ou de um vínculo, com o uso sugerido ou confirmado.</summary>
public sealed record AmbienteLido(string Id, string Nome, string Numero, string Nivel, string Origem, double Area, double Perimetro,
    UsoAmbiente Uso, bool Confirmado, Document Doc, Room Room, Transform Transformacao);

/// <summary>Uso confirmado pelo engenheiro, gravado no .rvt por identificador do ambiente.</summary>
public sealed class UsosConfirmados
{
    public const string Chave = "Ambientes";
    public Dictionary<string, UsoAmbiente> Usos { get; set; } = new();

    public static UsosConfirmados Carregar(Document doc) => ArmazenamentoEletrica.Ler<UsosConfirmados>(doc, Chave) ?? new UsosConfirmados();
}

/// <summary>Sugere o uso do ambiente pelo nome (a IA entra aqui depois; por ora, palavras-chave em português).</summary>
public static class ClassificadorDeAmbientes
{
    static readonly (string[] Palavras, UsoAmbiente Uso)[] Regras =
    [
        (["banheiro", "bwc", "wc", "sanit", "toalete", "toilet", "lavabo"], UsoAmbiente.Banheiro),
        (["lavabo"], UsoAmbiente.Lavabo),
        (["cozinha", "kitchen", "copa"], UsoAmbiente.Cozinha),
        (["serviço", "servico", "lavanderia", "laundry", "a.s.", "área de serv"], UsoAmbiente.AreaServico),
        (["dormit", "quarto", "suíte", "suite", "bedroom"], UsoAmbiente.Dormitorio),
        (["sala", "estar", "jantar", "living", "tv", "home"], UsoAmbiente.Sala),
        (["varanda", "sacada", "terraço", "terraco", "balcony"], UsoAmbiente.Varanda),
        (["circula", "hall", "corredor", "corridor", "escada", "stair"], UsoAmbiente.Circulacao),
        (["garagem", "garage", "estacion", "vaga"], UsoAmbiente.Garagem),
        (["externa", "externo", "jardim", "quintal", "pátio", "patio", "piscina", "churrasq"], UsoAmbiente.AreaExterna),
        (["depósito", "deposito", "despensa", "closet", "dml", "storage"], UsoAmbiente.Deposito),
        (["escrit", "office", "home office", "reuni", "recep"], UsoAmbiente.Escritorio),
        (["máquinas", "maquinas", "bomba", "barrilete", "casa de", "medidor", "medição", "shaft"], UsoAmbiente.CasaDeMaquinas),
    ];

    public static UsoAmbiente Sugerir(string nome)
    {
        var n = (nome ?? "").ToLowerInvariant();
        if (n.Contains("lavabo")) return UsoAmbiente.Lavabo;
        foreach (var (palavras, uso) in Regras)
            if (palavras.Any(p => n.Contains(p))) return uso;
        return UsoAmbiente.Outro;
    }

    public static string Nome(UsoAmbiente uso) => uso switch
    {
        UsoAmbiente.Sala => "Sala",
        UsoAmbiente.Dormitorio => "Dormitório",
        UsoAmbiente.Cozinha => "Cozinha",
        UsoAmbiente.Copa => "Copa",
        UsoAmbiente.AreaServico => "Área de serviço",
        UsoAmbiente.Lavanderia => "Lavanderia",
        UsoAmbiente.Banheiro => "Banheiro",
        UsoAmbiente.Lavabo => "Lavabo",
        UsoAmbiente.Varanda => "Varanda",
        UsoAmbiente.Circulacao => "Circulação",
        UsoAmbiente.Garagem => "Garagem",
        UsoAmbiente.AreaExterna => "Área externa",
        UsoAmbiente.Deposito => "Depósito",
        UsoAmbiente.Escritorio => "Escritório",
        UsoAmbiente.CasaDeMaquinas => "Casa de máquinas",
        _ => "Outro",
    };
}

/// <summary>Lê os ambientes (Room) do arquivo e dos vínculos Revit carregados, nas coordenadas do projeto elétrico.</summary>
public static class LeituraDeAmbientes
{
    public static IReadOnlyList<AmbienteLido> Ler(Document doc)
    {
        var confirmados = UsosConfirmados.Carregar(doc).Usos;
        var lista = new List<AmbienteLido>();
        Adicionar(doc, Transform.Identity, "este arquivo", "", confirmados, lista);
        foreach (var vinculo in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            if (vinculo.GetLinkDocument() is not { } vinculado) continue;
            var nome = doc.GetElement(vinculo.GetTypeId())?.Name ?? vinculo.Name;
            Adicionar(vinculado, vinculo.GetTotalTransform(), nome, vinculo.UniqueId + ":", confirmados, lista);
        }
        return lista;
    }

    static void Adicionar(Document doc, Transform transformacao, string origem, string prefixo, Dictionary<string, UsoAmbiente> confirmados, List<AmbienteLido> lista)
    {
        foreach (var room in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<Room>())
        {
            if (room.Area <= 0) continue;
            var id = prefixo + room.UniqueId;
            var nome = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? room.Name;
            var numero = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";
            var confirmado = confirmados.TryGetValue(id, out var uso);
            if (!confirmado) uso = ClassificadorDeAmbientes.Sugerir(nome);
            lista.Add(new AmbienteLido(id, nome, numero, room.Level?.Name ?? "", origem, Unidades.MetrosQuadrados(room.Area),
                Unidades.Metros(room.Perimeter), uso, confirmado, doc, room, transformacao));
        }
    }

    /// <summary>O ambiente que contém o ponto (coordenadas do projeto elétrico), ou nulo.</summary>
    /// <remarks>
    ///     O ponto de luz fica no limite de cima do Room e o ponto de piso no de baixo, onde o Revit pode responder que não há
    ///     ambiente: tenta o próprio ponto e depois o mesmo XY um pouco acima e abaixo (o Room é um prisma vertical; 1 m abaixo
    ///     do teto ainda é o mesmo pavimento).
    /// </remarks>
    public static AmbienteLido? Em(IReadOnlyList<AmbienteLido> ambientes, XYZ ponto)
    {
        foreach (var dz in Deslocamentos)
        {
            var tentativa = new XYZ(ponto.X, ponto.Y, ponto.Z + Unidades.DeMetros(dz));
            foreach (var grupo in ambientes.GroupBy(a => a.Doc))
            {
                var primeiro = grupo.First();
                var local = primeiro.Transformacao.Inverse.OfPoint(tentativa);
                Room? room;
                try { room = primeiro.Doc.GetRoomAtPoint(local); }
                catch (Autodesk.Revit.Exceptions.ApplicationException) { room = null; }
                if (room is null) continue;
                var achado = grupo.FirstOrDefault(a => a.Room.Id == room.Id);
                if (achado is not null) return achado;
            }
        }
        return null;
    }

    static readonly double[] Deslocamentos = [0, 0.2, -0.3, -1.0];
}
