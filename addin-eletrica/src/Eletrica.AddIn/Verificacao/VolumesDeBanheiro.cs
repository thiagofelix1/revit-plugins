using Eletrica.AddIn.Ambientes;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.AddIn.Verificacao;

/// <summary>
///     Volumes do banheiro (NBR 5410 9.1.2.1, Figuras 16 a 18) gerados a partir da arquitetura: o boxe, a banheira e o
///     chuveiro (louças e metais do arquivo ou dos vínculos, pelo nome) dão a área do volume 1 (até 2,25 m); o volume 2 é
///     a faixa de 0,60 m ao redor (até 3,00 m); o volume 3, mais 2,40 m (até 2,25 m). Sem boxe, o volume 1 é um raio de
///     0,60 m ao redor do chuveiro. Paredes e divisórias não entram na medição (simplificação declarada): o engenheiro
///     confere o que ficar perto do limite.
/// </summary>
public sealed class VolumesDeBanheiro
{
    /// <summary>Área do volume 1 em planta, nas coordenadas do projeto (pés), com o piso do ambiente.</summary>
    sealed record Zona(double MinX, double MinY, double MaxX, double MaxY, double PisoZ, string Nome)
    {
        /// <summary>Distância em planta do ponto ao retângulo, em m (0 dentro).</summary>
        public double Distancia(XYZ p)
        {
            var dx = Math.Max(Math.Max(MinX - p.X, 0), p.X - MaxX);
            var dy = Math.Max(Math.Max(MinY - p.Y, 0), p.Y - MaxY);
            return Unidades.Metros(Math.Sqrt(dx * dx + dy * dy));
        }
    }

    static readonly string[] PalavrasBoxe = ["boxe", "box", "chuveiro", "ducha", "shower", "banheira", "bathtub", "tub", "hidro"];

    readonly Dictionary<string, List<Zona>> _porAmbiente = new();

    public static VolumesDeBanheiro Ler(Document doc, IReadOnlyList<AmbienteLido> ambientes)
    {
        var volumes = new VolumesDeBanheiro();
        var banheiros = ambientes.Where(a => a.Uso is UsoAmbiente.Banheiro or UsoAmbiente.Lavabo).ToList();
        if (banheiros.Count == 0) return volumes;
        foreach (var grupo in banheiros.GroupBy(a => a.Doc))
        {
            var fonte = grupo.Key;
            var transformacao = grupo.First().Transformacao;
            var pecas = new FilteredElementCollector(fonte).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(fi => fi.Category?.BuiltInCategory is BuiltInCategory.OST_PlumbingFixtures or BuiltInCategory.OST_GenericModel or BuiltInCategory.OST_SpecialityEquipment or BuiltInCategory.OST_Casework)
                .Where(fi => PalavrasBoxe.Any(p => (fi.Symbol?.FamilyName + " " + fi.Name).ToLowerInvariant().Contains(p)))
                .ToList();
            foreach (var peca in pecas)
            {
                if (peca.get_BoundingBox(null) is not { } caixa) continue;
                var centroLocal = (caixa.Min + caixa.Max) / 2;
                var ambiente = grupo.FirstOrDefault(a => Dentro(a, centroLocal));
                if (ambiente is null) continue;
                // Cantos da caixa transformados para o projeto; o retângulo em planta que os contém.
                var cantos = new[]
                {
                    new XYZ(caixa.Min.X, caixa.Min.Y, caixa.Min.Z), new XYZ(caixa.Max.X, caixa.Min.Y, caixa.Min.Z),
                    new XYZ(caixa.Min.X, caixa.Max.Y, caixa.Min.Z), new XYZ(caixa.Max.X, caixa.Max.Y, caixa.Min.Z),
                }.Select(transformacao.OfPoint).ToList();
                var minX = cantos.Min(c => c.X);
                var maxX = cantos.Max(c => c.X);
                var minY = cantos.Min(c => c.Y);
                var maxY = cantos.Max(c => c.Y);
                var ladoMenor = Unidades.Metros(Math.Min(maxX - minX, maxY - minY));
                var ladoMaior = Unidades.Metros(Math.Max(maxX - minX, maxY - minY));
                if (ladoMaior < 0.5 || ladoMenor < 0.3)
                {
                    // Só o chuveiro (ou a ducha), sem boxe: volume 1 a 0,60 m ao redor do ponto de saída da água.
                    var cx = (minX + maxX) / 2;
                    var cy = (minY + maxY) / 2;
                    var r = Unidades.DeMetros(Nbr5410.RaioDoChuveiroSemBoxe);
                    minX = cx - r; maxX = cx + r; minY = cy - r; maxY = cy + r;
                }
                var piso = transformacao.OfPoint((ambiente.Room.Location as LocationPoint)?.Point ?? caixa.Min).Z;
                if (!volumes._porAmbiente.TryGetValue(ambiente.Id, out var lista)) volumes._porAmbiente[ambiente.Id] = lista = [];
                lista.Add(new Zona(minX, minY, maxX, maxY, piso, peca.Name));
            }
        }
        return volumes;
    }

    static bool Dentro(AmbienteLido a, XYZ pontoLocal)
    {
        try { return a.Room.IsPointInRoom(pontoLocal) || a.Room.IsPointInRoom(pontoLocal + XYZ.BasisZ * Unidades.DeMetros(0.5)); }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { return false; }
    }

    public bool TemZonas(string ambienteId) => _porAmbiente.ContainsKey(ambienteId);

    /// <summary>Volume (0 a 3) em que o ponto está; 4 = fora dos volumes; nulo quando o ambiente não tem boxe, banheira nem chuveiro lido.</summary>
    public int? Volume(string ambienteId, XYZ pontoNoProjeto)
    {
        if (!_porAmbiente.TryGetValue(ambienteId, out var zonas) || zonas.Count == 0) return null;
        var melhor = 4;
        foreach (var z in zonas)
        {
            var h = Unidades.Metros(pontoNoProjeto.Z - z.PisoZ);
            var d = z.Distancia(pontoNoProjeto);
            int v;
            if (d <= 1e-6 && h <= Nbr5410.AlturaVolume1) v = 1;
            else if (d <= Nbr5410.LarguraVolume2 + 1e-6 && h <= Nbr5410.AlturaVolume2) v = 2;
            else if (d <= Nbr5410.LarguraVolume2 + Nbr5410.LarguraVolume3 + 1e-6 && h <= Nbr5410.AlturaVolume3) v = 3;
            else v = 4;
            melhor = Math.Min(melhor, v);
        }
        return melhor;
    }

    /// <summary>Distância em planta do ponto ao boxe/chuveiro mais perto do ambiente, em m; nula sem zona.</summary>
    public double? DistanciaAoBoxe(string ambienteId, XYZ pontoNoProjeto) =>
        _porAmbiente.TryGetValue(ambienteId, out var zonas) && zonas.Count > 0 ? zonas.Min(z => z.Distancia(pontoNoProjeto)) : null;

    public string Resumo(string ambienteId) =>
        _porAmbiente.TryGetValue(ambienteId, out var zonas) ? string.Join(", ", zonas.Select(z => z.Nome)) : "";
}
