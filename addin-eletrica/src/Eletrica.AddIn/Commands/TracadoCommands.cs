using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Eletrica.AddIn.Condutos;
using Eletrica.AddIn.Fiacao;
using Eletrica.AddIn.Lancamento;
using Eletrica.AddIn.Leitura;
using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Calculo;
using Nice3point.Revit.Toolkit.External;

namespace Eletrica.AddIn.Commands;

/// <summary>Elementos que podem ser ponta de eletroduto: pontos, quadros, caixas e conexões; eletrodutos como destino.</summary>
sealed class PontasDeEletroduto(bool aceitaEletroduto) : ISelectionFilter
{
    public bool AllowElement(Element e) =>
        e is Conduit ? aceitaEletroduto
        : e is FamilyInstance && e.Category is { } c && c.BuiltInCategory is BuiltInCategory.OST_ElectricalFixtures or BuiltInCategory.OST_LightingFixtures
            or BuiltInCategory.OST_LightingDevices or BuiltInCategory.OST_ElectricalEquipment or BuiltInCategory.OST_ConduitFitting or BuiltInCategory.OST_MechanicalEquipment
            or BuiltInCategory.OST_GenericModel;

    public bool AllowReference(Reference r, XYZ p) => false;
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TracarEletrodutoCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Traçar eletroduto") is not { } doc || ui is null) return;
        var padrao = PadraoEletrica.Carregar(doc);
        TracarEletroduto.Opcoes opcoes;
        try { opcoes = TracarEletroduto.OpcoesDoPadrao(doc, padrao); }
        catch (InvalidOperationException e)
        {
            TaskDialog.Show("AutoEletrica — Traçar eletroduto", e.Message);
            return;
        }
        var criados = new List<ElementId>();
        var avisos = new List<string>();
        var trechos = 0;
        try
        {
            var primeiro = doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new PontasDeEletroduto(false),
                "Clique no primeiro elemento (quadro, caixa ou ponto); depois clique nos seguintes em sequência; Esc termina"));
            var de = TracarEletroduto.ExtremoDe(primeiro);
            if (de is null)
            {
                TaskDialog.Show("AutoEletrica — Traçar eletroduto", "Esse elemento não tem ponto de inserção.");
                return;
            }
            using var grupo = new TransactionGroup(doc, "AutoEletrica — Traçar eletroduto");
            grupo.Start();
            while (true)
            {
                Element alvo;
                try
                {
                    alvo = doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new PontasDeEletroduto(true),
                        $"Próximo elemento ou eletroduto a partir de {primeiro.Name} (passagem pelo {Nome(opcoes.Passagem)}, DN {opcoes.DnMm}); Esc termina"));
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { break; }
                var origem = de;
                var nivel = Colocacao.NivelAbaixo(doc, origem.Ponto.Z) ?? throw new InvalidOperationException("sem nível");
                List<Conduit> novos = [];
                var motivo = TransacaoSegura.Executar(doc, "AutoEletrica — Trecho de eletroduto", () =>
                {
                    if (alvo is Conduit eletroduto) novos = TracarEletroduto.AteEletroduto(doc, opcoes, nivel, origem, eletroduto);
                    else
                    {
                        var para = TracarEletroduto.ExtremoDe(alvo) ?? throw new InvalidOperationException("o destino não tem ponto de inserção.");
                        novos = TracarEletroduto.Entre(doc, opcoes, nivel, origem, para);
                    }
                    return null;
                });
                if (motivo is not null)
                {
                    avisos.Add($"De {primeiro.Name} a {alvo.Name}: {motivo}");
                    continue;
                }
                trechos++;
                criados.AddRange(novos.Select(c => c.Id));
                if (alvo is Conduit) break; // chegou num eletroduto existente: a sequência termina aí
                primeiro = alvo;
                de = TracarEletroduto.ExtremoDe(alvo)!;
            }
            grupo.Assimilate();
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
        if (trechos == 0 && avisos.Count == 0) return;
        if (criados.Count > 0) ui.Selection.SetElementIds(criados);
        TaskDialog.Show("AutoEletrica — Traçar eletroduto", $"{trechos} trecho(s), {criados.Count} eletroduto(s) ({opcoes.Tipo.Name}, DN {opcoes.DnMm}, pelo {Nome(opcoes.Passagem)}). " +
                                                          "Ajuste o que quiser na mão; depois Passar circuitos." + (avisos.Count > 0 ? "\n\n" + string.Join("\n", avisos) : "") + "\n\nCtrl+Z desfaz.");
    }

    static string Nome(AlturaDePassagem p) => p switch { AlturaDePassagem.Teto => "teto", AlturaDePassagem.Piso => "piso", _ => "nível dos pontos" };
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class LigarPontosCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Ligar pontos") is not { } doc || ui is null) return;
        var padrao = PadraoEletrica.Carregar(doc);
        try
        {
            var opcoes = TracarEletroduto.OpcoesDoPadrao(doc, padrao);
            var pontos = ui.Selection.PickObjects(ObjectType.Element, new PontasDeEletroduto(false), "Selecione os pontos a ligar e Concluir")
                .Select(r => doc.GetElement(r)).ToList();
            if (pontos.Count == 0) return;
            var alvo = doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new PontasDeEletroduto(true), "Clique no eletroduto, caixa ou quadro de destino"));
            var criados = new List<ElementId>();
            var avisos = new List<string>();
            var ligados = 0;
            using (var grupo = new TransactionGroup(doc, "AutoEletrica — Ligar pontos"))
            {
                grupo.Start();
                foreach (var p in pontos)
                {
                    var de = TracarEletroduto.ExtremoDe(p);
                    if (de is null) { avisos.Add($"{p.Name}: sem ponto de inserção."); continue; }
                    var nivel = Colocacao.NivelAbaixo(doc, de.Ponto.Z) ?? throw new InvalidOperationException("sem nível");
                    List<Conduit> novos = [];
                    var motivo = TransacaoSegura.Executar(doc, "AutoEletrica — Ligar ponto", () =>
                    {
                        if (alvo is Conduit eletroduto)
                        {
                            // Depois do primeiro, o eletroduto pode ter sido partido: liga ao pedaço mais perto do ponto.
                            var pedaco = MaisPerto(doc, eletroduto, de.Ponto);
                            novos = TracarEletroduto.AteEletroduto(doc, opcoes, nivel, de, pedaco);
                        }
                        else
                        {
                            var para = TracarEletroduto.ExtremoDe(alvo) ?? throw new InvalidOperationException("o destino não tem ponto de inserção.");
                            novos = TracarEletroduto.Entre(doc, opcoes, nivel, de, para);
                        }
                        return null;
                    });
                    if (motivo is not null) avisos.Add($"{p.Name}: {motivo}");
                    else { ligados++; criados.AddRange(novos.Select(c => c.Id)); }
                }
                grupo.Assimilate();
            }
            if (criados.Count > 0) ui.Selection.SetElementIds(criados);
            TaskDialog.Show("AutoEletrica — Ligar pontos", $"{ligados} de {pontos.Count} ponto(s) ligados ({criados.Count} eletrodutos). Depois, Passar circuitos." +
                                                         (avisos.Count > 0 ? "\n\n" + string.Join("\n", avisos.Take(12)) : "") + "\n\nCtrl+Z desfaz.");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
        catch (InvalidOperationException e)
        {
            TaskDialog.Show("AutoEletrica — Ligar pontos", e.Message);
        }
    }

    /// <summary>O eletroduto colinear com o original (ele mesmo ou um pedaço criado ao parti-lo) mais perto do ponto.</summary>
    static Conduit MaisPerto(Document doc, Conduit original, XYZ ponto)
    {
        if (original.Location is not LocationCurve { Curve: Line eixo }) return original;
        var candidatos = new FilteredElementCollector(doc).OfClass(typeof(Conduit)).Cast<Conduit>()
            .Where(c => c.GetTypeId() == original.GetTypeId() && c.Location is LocationCurve { Curve: Line l }
                        && Math.Abs(l.Direction.DotProduct(eixo.Direction)) > 0.999 && eixo.Distance(l.GetEndPoint(0)) < Unidades.DeMetros(0.01))
            .ToList();
        return candidatos.OrderBy(c => ((LocationCurve)c.Location).Curve.Distance(new XYZ(ponto.X, ponto.Y, eixo.GetEndPoint(0).Z))).FirstOrDefault() ?? original;
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CaixasPorRegraCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Caixas por regra") is not { } doc || ui is null) return;
        var padrao = PadraoEletrica.Carregar(doc);
        var grafo = new GrafoDeCondutos(doc);
        if (grafo.Cadeias.Count == 0)
        {
            TaskDialog.Show("AutoEletrica — Caixas por regra", "O modelo não tem eletrodutos.");
            return;
        }
        var r = CaixasPorRegra.Executar(doc, grafo, padrao);
        if (r.Necessarias.Count == 0)
        {
            TaskDialog.Show("AutoEletrica — Caixas por regra", $"Nenhum trecho passa de 15 m (menos 3 m por curva) nem de 3 curvas entre caixas, nos {grafo.Cadeias.Count} trecho(s). Nada a inserir.");
            return;
        }
        if (r.Criadas.Count > 0)
        {
            ui.Selection.SetElementIds(r.Criadas.ToList());
            try { ui.ShowElements(r.Criadas.ToList()); }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException) { }
        }
        else ui.Selection.SetElementIds(r.Necessarias.Select(n => n.Eletroduto.Id).Distinct().ToList());
        var linhas = r.Necessarias.Take(15).Select(n => $"• ({Unidades.Metros(n.Ponto.X):0.0}, {Unidades.Metros(n.Ponto.Y):0.0}): {n.Motivo}");
        TaskDialog.Show("AutoEletrica — Caixas por regra",
            $"{r.Necessarias.Count} caixa(s) necessária(s) pela NBR 5410 6.2.11.1.6 b) e 6.2.11.1.7; {r.Criadas.Count} inserida(s) (eletroduto partido no ponto; os eletrodutos selecionados mostram onde)." +
            "\n\n" + string.Join("\n", linhas) + (r.Necessarias.Count > 15 ? "\n..." : "") + (r.Avisos.Count > 0 ? "\n\n" + string.Join("\n", r.Avisos) : "") + "\n\nCtrl+Z desfaz.");
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class FiacaoManualCommand : ExternalCommand
{
    sealed class SoEletrodutos : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is Conduit;
        public bool AllowReference(Reference r, XYZ p) => false;
    }

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Fiação manual") is not { } doc || ui is null) return;
        if (Contexto.Dados(doc) is not { } dados) return;
        try
        {
            var eletrodutos = ui.Selection.PickObjects(ObjectType.Element, new SoEletrodutos(), "Selecione os eletrodutos a forçar ou evitar e Concluir")
                .Select(r => (Conduit)doc.GetElement(r)).ToList();
            if (eletrodutos.Count == 0) return;
            var lida = LeitorInstalacao.Ler(doc, dados);
            var passam = eletrodutos.SelectMany(c => Separar(ParametrosEletrica.LerTexto(c, ParametrosEletrica.Circuitos))).ToHashSet();
            var forcados = eletrodutos.SelectMany(c => Separar(ParametrosEletrica.LerTexto(c, ParametrosEletrica.Forcar))).ToHashSet();
            var evitadosBrutos = eletrodutos.SelectMany(c => Separar(ParametrosEletrica.LerTexto(c, ParametrosEletrica.Evitar))).ToHashSet();
            var janela = new FiacaoManualJanela(lida.Instalacao, eletrodutos.Count, passam, forcados, evitadosBrutos, evitadosBrutos.Contains("*"));
            if (janela.ShowDialog() != true || janela.Resultado is not { } r) return;
            using (var transacao = new Transaction(doc, "AutoEletrica — Fiação manual"))
            {
                transacao.Start();
                EngolirAvisos.Aplicar(transacao);
                ParametrosEletrica.Garantir(doc);
                var evitar = r.EvitarTodos ? "*" : string.Join(";", r.Evitar);
                foreach (var c in eletrodutos)
                {
                    ParametrosEletrica.Gravar(c, ParametrosEletrica.Forcar, string.Join(";", r.Forcar));
                    ParametrosEletrica.Gravar(c, ParametrosEletrica.Evitar, evitar);
                }
                transacao.Commit();
            }
            var feito = PassarCircuitos.Executar(doc, LeitorInstalacao.Ler(doc, dados));
            TaskDialog.Show("AutoEletrica — Fiação manual", $"{r.Forcar.Count} circuito(s) forçados e {(r.EvitarTodos ? "todos os outros" : r.Evitar.Count.ToString())} evitados nos {eletrodutos.Count} eletroduto(s). " +
                                                           $"Passar circuitos rodou de novo: {feito.Circuitos} circuito(s) por {feito.Eletrodutos} eletroduto(s)." +
                                                           (feito.Avisos.Count > 0 ? "\n\n" + string.Join("\n", feito.Avisos.Take(12)) : ""));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
        catch (Exception e) when (e is Motor.Modelo.ErroDeModelo or InvalidOperationException)
        {
            TaskDialog.Show("AutoEletrica — Fiação manual", e.Message);
        }
    }

    static IEnumerable<string> Separar(string? texto) => (texto ?? "").Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries);
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DesenharFiacaoCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Desenhar fiação") is not { } doc || ui is null) return;
        if (Contexto.Dados(doc) is not { } dados) return;
        var vista = ui.ActiveView;
        if (vista.ViewType is not (ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.EngineeringPlan))
        {
            TaskDialog.Show("AutoEletrica — Desenhar fiação", "Abra uma planta (piso ou forro): o fio do Revit só existe nelas.");
            return;
        }
        try
        {
            var lida = LeitorInstalacao.Ler(doc, dados);
            if (lida.Instalacao.Trechos.All(t => t.Circuitos.Count == 0))
            {
                TaskDialog.Show("AutoEletrica — Desenhar fiação", "Os eletrodutos ainda não têm circuitos. Rode Passar circuitos antes.");
                return;
            }
            var feito = DesenharFiacao.Executar(doc, vista, lida, PadraoEletrica.Carregar(doc));
            TaskDialog.Show("AutoEletrica — Desenhar fiação", $"{feito.Fios} fio(s) desenhados na vista {vista.Name}" + (feito.Apagados > 0 ? $" ({feito.Apagados} anteriores apagados)" : "") +
                                                             ". Os fios levam AE Circuitos e AE Fiação para a etiqueta de fio; são anotação desta vista." +
                                                             (feito.Avisos.Count > 0 ? "\n\n" + string.Join("\n", feito.Avisos.Take(10)) : "") + "\n\nCtrl+Z desfaz.");
        }
        catch (Exception e) when (e is Motor.Modelo.ErroDeModelo or InvalidOperationException)
        {
            TaskDialog.Show("AutoEletrica — Desenhar fiação", e.Message);
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class LegendaCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Legenda") is not { } doc || ui is null) return;
        if (Contexto.Dados(doc) is not { } dados) return;
        try
        {
            var lida = LeitorInstalacao.Ler(doc, dados);
            var resultado = CalculoEletrica.Executar(lida.Instalacao);
            var vista = LegendaDeFiacao.Gerar(doc, resultado, lida.Instalacao, PadraoEletrica.Carregar(doc));
            ui.ActiveView = vista;
            TaskDialog.Show("AutoEletrica — Legenda", $"Vista de desenho \"{vista.Name}\" refeita com {resultado.Circuitos.Count} circuito(s) e as cores dos condutores do padrão. " +
                                                     "Arraste-a para a prancha como qualquer vista.");
        }
        catch (Exception e) when (e is Motor.Modelo.ErroDeModelo or InvalidOperationException or ArgumentException)
        {
            TaskDialog.Show("AutoEletrica — Legenda", e.Message);
        }
    }
}
