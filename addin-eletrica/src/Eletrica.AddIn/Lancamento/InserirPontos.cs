using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Padrao;

namespace Eletrica.AddIn.Lancamento;

/// <summary>
///     Inserir ponto (item 2.1 do plano): o engenheiro escolhe papel, posição, potência e circuito; o Revit faz a colocação
///     normal da família (com hospedagem, alinhamento e repetição); ao sair, o plugin pega o que foi colocado e completa:
///     altura, parâmetros AE, ambiente e circuito. Assistido de verdade: o clique é do projetista.
/// </summary>
static class InserirPontos
{
    public sealed record Feito(IReadOnlyList<ElementId> Criados, IReadOnlyList<string> Avisos);

    public static Feito Executar(UIDocument ui, InserirPontoJanela.Escolha escolha, PadraoEletrica padrao)
    {
        var doc = ui.Document;
        var simbolo = escolha.Simbolo;
        var antes = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .Where(fi => fi.GetTypeId() == simbolo.Id).Select(fi => fi.Id).ToHashSet();

        if (!simbolo.IsActive)
        {
            using var t = new Transaction(doc, "AutoEletrica — Ativar tipo");
            t.Start();
            simbolo.Activate();
            t.Commit();
        }

        try
        {
            ui.PromptForFamilyInstancePlacement(simbolo);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // Esc: o normal ao terminar de colocar.
        }

        var novos = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .Where(fi => fi.GetTypeId() == simbolo.Id && !antes.Contains(fi.Id)).ToList();
        if (novos.Count == 0) return new Feito([], []);

        var avisos = new List<string>();
        var ambientes = LeituraDeAmbientes.Ler(doc);
        var pedido = new Colocacao.Pedido(simbolo, escolha.Papel, escolha.Posicao, escolha.Altura, escolha.PotenciaVA, escolha.Tipo);
        using var transacao = new Transaction(doc, "AutoEletrica — Inserir ponto: dados");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);
        if (ParametrosEletrica.Garantir(doc) is { } aviso) avisos.Add(aviso);
        foreach (var fi in novos)
        {
            var p = (fi.Location as LocationPoint)?.Point;
            var nivel = p is null ? null : Colocacao.NivelDe(doc, fi, p);
            var altura = escolha.Altura;
            // No teto a família fica onde o clique a pôs (face do forro); nas outras posições o plugin leva à altura do padrão.
            if (nivel is not null && escolha.Posicao != Motor.Modelo.PosicaoDoPonto.Teto) altura = Colocacao.AjustarAltura(doc, fi, escolha.Altura, nivel);
            else if (nivel is not null) altura = Colocacao.AlturaDe(doc, fi, nivel);
            var ambiente = p is null ? null : LeituraDeAmbientes.Em(ambientes, p);
            if (ambiente is null) avisos.Add($"{fi.Name} ({fi.Id}): fora de qualquer ambiente; as regras por cômodo não o veem.");
            Colocacao.Marcar(fi, pedido, ambiente, altura);
        }
        if (escolha.Circuito is { } circuito)
        {
            var conjunto = new ElementSet();
            foreach (var fi in novos.Where(TemConectorEletrico)) conjunto.Insert(fi);
            if (conjunto.IsEmpty) avisos.Add("A família não tem conector elétrico: não entra em circuito do Revit (fica só com os parâmetros AE).");
            else
            {
                try
                {
                    if (!circuito.AddToCircuit(conjunto)) avisos.Add($"O Revit não aceitou os pontos no circuito {circuito.CircuitNumber} (tensão ou polos do conector diferentes do circuito).");
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException e)
                {
                    avisos.Add($"Circuito {circuito.CircuitNumber}: {e.Message}");
                }
            }
        }
        transacao.Commit();
        return new Feito(novos.Select(fi => fi.Id).ToList(), avisos);
    }

    public static bool TemConectorEletrico(FamilyInstance fi) =>
        fi.MEPModel?.ConnectorManager is { } cm && cm.Connectors.Cast<Connector>().Any(k => k.Domain == Domain.DomainElectrical);

    public static bool SemCircuito(FamilyInstance fi)
    {
        try { return fi.MEPModel?.GetElectricalSystems() is not { Count: > 0 }; }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { return true; }
    }
}
