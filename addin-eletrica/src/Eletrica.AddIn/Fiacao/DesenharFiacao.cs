using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Condutos;
using Eletrica.AddIn.Leitura;
using Eletrica.AddIn.Padrao;

namespace Eletrica.AddIn.Fiacao;

/// <summary>
///     Fio desenhado (item 5.3 do plano): na planta aberta, um fio do Revit (Wire) por trecho de eletroduto que leva
///     circuitos, seguindo o percurso do trecho em planta, em arcos (embutido) ou em polilinha (aparente). O fio é anotação
///     2D e por vista: é saída gráfica do traçado, não o dado mestre. Recebe os parâmetros AE do trecho (circuitos, fiação)
///     para a etiqueta de fio do escritório ler. Rodar de novo apaga os fios AE da vista e redesenha.
/// </summary>
static class DesenharFiacao
{
    public sealed record Feito(int Fios, int Apagados, IReadOnlyList<string> Avisos);

    public static Feito Executar(Document doc, View vista, LeitorInstalacao.Lida lida, PadraoEletrica padrao)
    {
        var avisos = new List<string>();
        var tipo = new FilteredElementCollector(doc).OfClass(typeof(WireType)).Cast<WireType>().FirstOrDefault()
                   ?? throw new InvalidOperationException("O projeto não tem tipo de fio (Wire Type). Carregue o template elétrico.");
        var z = (vista as ViewPlan)?.GenLevel?.ProjectElevation ?? 0;
        var circuitosPorId = lida.Instalacao.Circuitos.ToDictionary(c => c.Id, c => c);

        using var transacao = new Transaction(doc, "AutoEletrica — Desenhar fiação");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);
        if (ParametrosEletrica.Garantir(doc) is { } aviso) avisos.Add(aviso);

        // Apaga os fios que o plugin desenhou nesta vista (os que têm AE Circuitos).
        var antigos = new FilteredElementCollector(doc, vista.Id).OfClass(typeof(Wire)).Cast<Wire>()
            .Where(w => ParametrosEletrica.LerTexto(w, ParametrosEletrica.Circuitos) is { Length: > 0 }).Select(w => w.Id).ToList();
        if (antigos.Count > 0) doc.Delete(antigos);

        var fios = 0;
        foreach (var cadeia in lida.Grafo.Cadeias)
        {
            var trecho = lida.Instalacao.Trechos.FirstOrDefault(t => t.Id == cadeia.Id);
            if (trecho is null || trecho.Circuitos.Count == 0) continue;
            var percurso = lida.Grafo.Percurso(cadeia).Select(p => new XYZ(p.X, p.Y, z)).ToList();
            var planta = new List<XYZ>();
            foreach (var p in percurso)
                if (planta.Count == 0 || planta[planta.Count - 1].DistanceTo(p) > Unidades.DeMetros(0.05)) planta.Add(p);
            if (planta.Count < 2) continue; // trecho vertical: não aparece em planta

            var circuitos = trecho.Circuitos.Select(id => circuitosPorId[id]).OrderBy(c => c.Numero, StringComparer.OrdinalIgnoreCase).ToList();
            var fiacao = cadeia.Conduites.Select(c => ParametrosEletrica.LerTexto(c, ParametrosEletrica.Fiacao)).FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? "";
            var condutores = cadeia.Conduites.Select(c => c.get_Parameter(ParametrosEletrica.Condutores)?.AsInteger() ?? 0).FirstOrDefault(n => n > 0);
            foreach (var pedaco in Pedacos(planta, padrao.FioCurvo))
            {
                try
                {
                    var fio = Wire.Create(doc, tipo.Id, vista.Id, padrao.FioCurvo ? WiringType.Arc : WiringType.Chamfer, pedaco, null, null);
                    ParametrosEletrica.Gravar(fio, ParametrosEletrica.Circuitos, string.Join(";", trecho.Circuitos));
                    ParametrosEletrica.Gravar(fio, ParametrosEletrica.Fiacao, fiacao);
                    if (condutores > 0) ParametrosEletrica.Gravar(fio, ParametrosEletrica.Condutores, condutores);
                    Condutores(fio, circuitos, trecho.Retornos.Count);
                    fios++;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException e)
                {
                    avisos.Add($"Trecho {cadeia.Id.Substring(0, 8)}: fio não desenhado ({e.Message}).");
                }
            }
        }
        transacao.Commit();
        return new Feito(fios, antigos.Count, avisos);
    }

    /// <summary>Em arcos, cada fio tem até três vértices (dois: reta; três: arco); em polilinha, um fio só.</summary>
    static IEnumerable<IList<XYZ>> Pedacos(List<XYZ> pontos, bool curvo)
    {
        if (!curvo || pontos.Count <= 3)
        {
            yield return pontos;
            yield break;
        }
        for (var i = 0; i + 1 < pontos.Count; i += 2)
        {
            var fim = Math.Min(i + 2, pontos.Count - 1);
            yield return pontos.GetRange(i, fim - i + 1);
            if (fim == pontos.Count - 1) yield break;
        }
    }

    /// <summary>Tenta ajustar as marcas de condutor do fio (fases, neutros, terras) ao que passa no trecho; o Revit pode recusar.</summary>
    static void Condutores(Wire fio, IReadOnlyList<Motor.Modelo.Circuito> circuitos, int retornos)
    {
        var fases = circuitos.Sum(c => c.Fases) + retornos;
        var neutros = circuitos.Count(c => c.TemNeutro);
        var terras = circuitos.Count;
        Ajustar(fio, BuiltInParameter.RBS_ELEC_WIRE_HOT_ADJUSTMENT, fases - fio.HotConductorNum);
        Ajustar(fio, BuiltInParameter.RBS_ELEC_WIRE_NEUTRAL_ADJUSTMENT, neutros - fio.NeutralConductorNum);
        Ajustar(fio, BuiltInParameter.RBS_ELEC_WIRE_GROUND_ADJUSTMENT, terras - fio.GroundConductorNum);
    }

    static void Ajustar(Wire fio, BuiltInParameter parametro, int delta)
    {
        if (delta == 0) return;
        var p = fio.get_Parameter(parametro);
        if (p is not { IsReadOnly: false, StorageType: StorageType.Integer }) return;
        try { p.Set(p.AsInteger() + delta); }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { }
    }
}
