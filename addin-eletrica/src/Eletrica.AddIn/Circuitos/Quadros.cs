using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Eletrica.AddIn.Padrao;

namespace Eletrica.AddIn.Circuitos;

/// <summary>
///     Quadros (itens 2.6 e 3.4 do plano): inserir o quadro com nome e sistema de distribuição coerentes com os dados do
///     projeto, e alimentar um quadro por outro (circuito alimentador do pai com o filho como carga), que é a hierarquia
///     medição → geral → terminais que o motor lê.
/// </summary>
static class Quadros
{
    public sealed record Feito(IReadOnlyList<ElementId> Criados, IReadOnlyList<string> Avisos);

    public static IEnumerable<FamilyInstance> Todos(Document doc) =>
        new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ElectricalEquipment).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .Where(fi => fi.MEPModel is ElectricalEquipment);

    public static string Nome(FamilyInstance q) => q.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME)?.AsString() is { Length: > 0 } n ? n : q.Name;

    /// <summary>Coloca quadros pela colocação normal do Revit e dá nome e sistema de distribuição aos novos.</summary>
    public static Feito Inserir(UIDocument ui, FamilySymbol simbolo, PadraoEletrica padrao, DadosProjeto dados)
    {
        var doc = ui.Document;
        var antes = Todos(doc).Select(q => q.Id).ToHashSet();
        if (!simbolo.IsActive)
        {
            using var t = new Transaction(doc, "AutoEletrica — Ativar tipo");
            t.Start();
            simbolo.Activate();
            t.Commit();
        }
        try { ui.PromptForFamilyInstancePlacement(simbolo); }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }

        var novos = Todos(doc).Where(q => !antes.Contains(q.Id)).ToList();
        if (novos.Count == 0) return new Feito([], []);
        var avisos = new List<string>();
        var sistema = SistemaDeDistribuicao(doc, dados, avisos);
        var numero = ProximoNumero(doc, padrao.Lancamento.PrefixoQuadro, novos);
        using var transacao = new Transaction(doc, "AutoEletrica — Quadro: dados");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);
        foreach (var q in novos)
        {
            var nome = q.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME);
            if (nome is { IsReadOnly: false } && string.IsNullOrWhiteSpace(nome.AsString())) nome.Set($"{padrao.Lancamento.PrefixoQuadro}-{numero++}");
            if (sistema is not null)
            {
                var p = q.get_Parameter(BuiltInParameter.RBS_FAMILY_CONTENT_DISTRIBUTION_SYSTEM);
                if (p is { IsReadOnly: false })
                {
                    try { p.Set(sistema.Id); }
                    catch (Autodesk.Revit.Exceptions.ApplicationException e) { avisos.Add($"{Nome(q)}: sistema de distribuição não aplicado ({e.Message})."); }
                }
            }
        }
        transacao.Commit();
        return new Feito(novos.Select(q => q.Id).ToList(), avisos);
    }

    /// <summary>O sistema de distribuição do projeto que bate com as tensões e fases dos dados; nulo (com aviso) se não há.</summary>
    public static DistributionSysType? SistemaDeDistribuicao(Document doc, DadosProjeto dados, List<string> avisos)
    {
        var sistemas = new FilteredElementCollector(doc).OfClass(typeof(DistributionSysType)).Cast<DistributionSysType>().ToList();
        DistributionSysType? melhor = null;
        var menor = double.MaxValue;
        foreach (var s in sistemas)
        {
            try
            {
                var fases = s.ElectricalPhase == ElectricalPhase.ThreePhase ? 3 : s.NumWires >= 3 ? 2 : 1;
                if (fases != dados.Fases && !(dados.Fases == 1 && fases == 2)) continue;
                var ff = s.VoltageLineToLine is { } vll ? Unidades.Volts(vll) : 0;
                var fn = s.VoltageLineToGround is { } vlg ? Unidades.Volts(vlg) : 0;
                var erro = Math.Abs(fn - dados.TensaoFaseNeutro) + (dados.Fases > 1 ? Math.Abs(ff - dados.TensaoFaseFase) : 0);
                if (erro < menor) { menor = erro; melhor = s; }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { }
        }
        if (melhor is null || menor > 15)
        {
            avisos.Add($"Nenhum sistema de distribuição do projeto corresponde a {dados.TensaoFaseNeutro:0}/{dados.TensaoFaseFase:0} V com {dados.Fases} fase(s). " +
                       "Crie um em Gerenciar → Configurações MEP → Configurações elétricas → Sistemas de distribuição e aplique no quadro.");
            return null;
        }
        return melhor;
    }

    static int ProximoNumero(Document doc, string prefixo, List<FamilyInstance> novos)
    {
        var maior = 0;
        foreach (var q in Todos(doc).Where(q => !novos.Contains(q)))
        {
            var nome = Nome(q);
            if (nome.StartsWith(prefixo + "-") && int.TryParse(nome.Substring(prefixo.Length + 1), out var n)) maior = Math.Max(maior, n);
        }
        return maior + 1;
    }

    /// <summary>Cria o alimentador: circuito do quadro pai com o filho como único elemento.</summary>
    public static string? Alimentar(Document doc, FamilyInstance filho, FamilyInstance pai)
    {
        if (filho.Id == pai.Id) return "o quadro não pode alimentar a si mesmo.";
        string? erro = null;
        var motivo = TransacaoSegura.Executar(doc, "AutoEletrica — Alimentar quadro", () =>
        {
            if (ParametrosEletrica.Garantir(doc) is { } aviso) erro = aviso;
            var existente = Lancamento.Comandos.CircuitoDe(filho);
            if (existente is not null && existente.BaseEquipment is { } atual)
            {
                if (atual.Id == pai.Id) return "o quadro já é alimentado por esse quadro.";
                existente.DisconnectPanel();
                existente.SelectPanel(pai);
                return null;
            }
            var sistema = ElectricalSystem.Create(doc, [filho.Id], ElectricalSystemType.PowerCircuit);
            sistema.SelectPanel(pai);
            sistema.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NAME)?.Set($"Alimentador {Nome(filho)}");
            ParametrosEletrica.Gravar(sistema, ParametrosEletrica.TipoDeCarga, Motor.Modelo.TipoCarga.Alimentador.ToString());
            return null;
        });
        return motivo ?? erro;
    }
}
