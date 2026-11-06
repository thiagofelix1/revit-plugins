using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Circuitos;
using Eletrica.AddIn.Familias;
using Eletrica.AddIn.Lancamento;
using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Lancamento;
using Eletrica.Motor.Modelo;
using Nice3point.Revit.Toolkit.External;

namespace Eletrica.AddIn.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class PadraoCommand : ExternalCommand
{
    public override void Execute()
    {
        if (Contexto.Projeto(Application, "AutoEletrica — Padrão") is not { } doc) return;
        var janela = new PadraoJanela(doc, PadraoEletrica.Carregar(doc));
        if (janela.ShowDialog() != true || janela.Resultado is not { } padrao) return;
        padrao.SalvarComTransacao(doc);
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class FamiliasCommand : ExternalCommand
{
    public override void Execute()
    {
        if (Contexto.Projeto(Application, "AutoEletrica — Famílias") is not { } doc) return;
        var padrao = PadraoEletrica.Carregar(doc);
        var candidatos = DescobertaDeFamilias.Descobrir(doc, padrao);
        if (candidatos.Count == 0)
        {
            TaskDialog.Show("AutoEletrica — Famílias", "Nenhuma família de tomada, luminária, interruptor, quadro ou caixa carregada no projeto. " +
                                                       "Carregue as famílias elétricas do escritório (Inserir → Carregar família) e rode de novo.");
            return;
        }
        var janela = new FamiliasJanela(candidatos);
        if (janela.ShowDialog() != true || janela.Resultado is not { } confirmados) return;
        padrao.Familias = confirmados;
        padrao.SalvarComTransacao(doc);
        TaskDialog.Show("AutoEletrica — Famílias", $"{confirmados.Count} papel(is) confirmados e gravados no projeto. Inserir ponto, Pontos por ambiente e Quadro passam a usar essas famílias.");
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class InserirPontoCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Inserir ponto") is not { } doc || ui is null) return;
        var padrao = PadraoEletrica.Carregar(doc);
        var janela = new InserirPontoJanela(doc, padrao, PapelFamilia.Tomada10A);
        if (janela.ShowDialog() != true || janela.Resultado is not { } escolha) return;
        try
        {
            var feito = InserirPontos.Executar(ui, escolha, padrao);
            if (feito.Criados.Count == 0) return;
            var texto = $"{feito.Criados.Count} ponto(s) de {PadraoEletrica.NomeDoPapel(escolha.Papel)} a {escolha.Altura:0.00} m" +
                        (escolha.PotenciaVA > 0 ? $", {escolha.PotenciaVA:0} VA" : "") + (escolha.Circuito is { } c ? $", no circuito {c.CircuitNumber}" : ", sem circuito") + ".";
            if (feito.Avisos.Count > 0) texto += "\n\n" + string.Join("\n", feito.Avisos.Take(10));
            TaskDialog.Show("AutoEletrica — Inserir ponto", texto);
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            TaskDialog.Show("AutoEletrica — Inserir ponto", e.Message);
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class PontosPorAmbienteCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Pontos por ambiente") is not { } doc || ui is null) return;
        if (Contexto.Dados(doc) is not { } dados) return;
        var padrao = PadraoEletrica.Carregar(doc);
        if (padrao.Simbolo(doc, PapelFamilia.Tomada10A) is null && padrao.Simbolo(doc, PapelFamilia.TomadaDupla) is null || padrao.Simbolo(doc, PapelFamilia.Luminaria) is null)
        {
            TaskDialog.Show("AutoEletrica — Pontos por ambiente", "Antes, confirme em Famílias pelo menos a tomada de 10 A e a luminária do escritório.");
            return;
        }
        var lidos = LeituraDeAmbientes.Ler(doc);
        if (lidos.Count == 0)
        {
            TaskDialog.Show("AutoEletrica — Pontos por ambiente", "Nenhum ambiente (Room) no arquivo nem nos vínculos.");
            return;
        }
        var ambientes = lidos.Select(a => new Ambiente(a.Id, a.Nome, a.Uso, a.Area, a.Perimetro)).ToList();
        var propostas = PropostaDePontos.Propor(ambientes, padrao.Lancamento, dados.Habitacao);
        var existentes = ContarExistentes(doc, lidos);
        var janela = new PontosPorAmbienteJanela(propostas, existentes, lidos.ToDictionary(a => a.Id, a => a));
        if (janela.ShowDialog() != true || janela.Resultado is not { } linhas) return;
        try
        {
            var criados = new List<ElementId>();
            var resultados = PontosPorAmbiente.Lancar(doc, linhas, padrao, criados);
            if (criados.Count > 0)
            {
                ui.Selection.SetElementIds(criados);
                try { ui.ShowElements(criados); }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException) { }
            }
            var dialogo = new TaskDialog("AutoEletrica — Pontos por ambiente")
            {
                MainInstruction = $"{criados.Count} ponto(s) em {resultados.Count(r => r.Recusa is null)} ambiente(s).",
                MainContent = string.Join("\n", resultados.Take(12).Select(r => r.Texto())) + (resultados.Count > 12 ? "\n..." : "") +
                              "\n\nMova o que quiser na mão; depois Criar circuitos. Ctrl+Z desfaz tudo.",
                ExpandedContent = string.Join("\n", resultados.Select(r => r.Texto())),
                CommonButtons = TaskDialogCommonButtons.Close,
            };
            dialogo.Show();
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            TaskDialog.Show("AutoEletrica — Pontos por ambiente", e.Message);
        }
    }

    static Dictionary<string, int> ContarExistentes(Document doc, IReadOnlyList<AmbienteLido> lidos)
    {
        var contagem = new Dictionary<string, int>();
        foreach (var fi in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                     .Where(fi => fi.Category?.BuiltInCategory is BuiltInCategory.OST_ElectricalFixtures or BuiltInCategory.OST_LightingFixtures or BuiltInCategory.OST_LightingDevices))
        {
            if ((fi.Location as LocationPoint)?.Point is not { } p || LeituraDeAmbientes.Em(lidos, p) is not { } a) continue;
            contagem[a.Id] = contagem.TryGetValue(a.Id, out var n) ? n + 1 : 1;
        }
        return contagem;
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ComandoCommand : ExternalCommand
{
    sealed class SoCategoria(params BuiltInCategory[] categorias) : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is FamilyInstance && e.Category is { } c && categorias.Contains(c.BuiltInCategory);
        public bool AllowReference(Reference r, XYZ p) => false;
    }

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Comando") is not { } doc || ui is null) return;
        try
        {
            var interruptores = ui.Selection.PickObjects(ObjectType.Element, new SoCategoria(BuiltInCategory.OST_LightingDevices),
                    "Clique nos interruptores na ordem do caminho (1 = simples; 2 = paralelo; 3 ou mais = intermediários) e Concluir")
                .Select(r => (FamilyInstance)doc.GetElement(r)).ToList();
            if (interruptores.Count == 0) return;
            var luminarias = ui.Selection.PickObjects(ObjectType.Element, new SoCategoria(BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_ElectricalFixtures),
                    "Clique nas luminárias que esses interruptores acendem e Concluir")
                .Select(r => (FamilyInstance)doc.GetElement(r)).ToList();
            if (luminarias.Count == 0) return;
            var avisos = new List<string>();
            string id = "";
            using (var transacao = new Transaction(doc, "AutoEletrica — Comando"))
            {
                transacao.Start();
                EngolirAvisos.Aplicar(transacao);
                if (ParametrosEletrica.Garantir(doc) is { } aviso) avisos.Add(aviso);
                id = Comandos.Gravar(doc, Comandos.ProximoNumero(doc), interruptores, luminarias, avisos);
                transacao.Commit();
            }
            var tipo = Comando.TipoPor(interruptores.Count);
            TaskDialog.Show("AutoEletrica — Comando", $"Comando {id} ({Comandos.NomeDoTipo(tipo).ToLowerInvariant()}): {interruptores.Count} interruptor(es) → {luminarias.Count} luminária(s). " +
                                                      "Passar circuitos conta os retornos por este comando." + (avisos.Count > 0 ? "\n\n" + string.Join("\n", avisos) : ""));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class QuadroCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Quadro") is not { } doc || ui is null) return;
        if (Contexto.Dados(doc) is not { } dados) return;
        var padrao = PadraoEletrica.Carregar(doc);
        var simbolo = padrao.Simbolo(doc, PapelFamilia.Quadro)
                      ?? new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ElectricalEquipment).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                          .OrderByDescending(s => DescobertaDeFamilias.Sugerir(BuiltInCategory.OST_ElectricalEquipment, s.FamilyName + " " + s.Name) == PapelFamilia.Quadro)
                          .FirstOrDefault();
        if (simbolo is null)
        {
            TaskDialog.Show("AutoEletrica — Quadro", "Nenhuma família de quadro (Electrical Equipment) carregada. Carregue a do escritório e confirme em Famílias.");
            return;
        }
        var feito = Quadros.Inserir(ui, simbolo, padrao, dados);
        if (feito.Criados.Count == 0) return;
        var nomes = feito.Criados.Select(id => doc.GetElement(id) as FamilyInstance).Where(q => q is not null).Select(q => Quadros.Nome(q!));
        TaskDialog.Show("AutoEletrica — Quadro", $"Quadro(s) {string.Join(", ", nomes)} com o sistema de distribuição de {dados.TensaoFaseNeutro:0}/{dados.TensaoFaseFase:0} V. " +
                                                 "Se houver mais de um, use Alimentar quadro para ligar os terminais ao geral." +
                                                 (feito.Avisos.Count > 0 ? "\n\n" + string.Join("\n", feito.Avisos) : ""));
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AlimentarQuadroCommand : ExternalCommand
{
    sealed class SoQuadros : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is FamilyInstance { MEPModel: Autodesk.Revit.DB.Electrical.ElectricalEquipment };
        public bool AllowReference(Reference r, XYZ p) => false;
    }

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Alimentar quadro") is not { } doc || ui is null) return;
        try
        {
            var filho = (FamilyInstance)doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new SoQuadros(), "Clique no quadro que vai ser alimentado (o terminal)"));
            var pai = (FamilyInstance)doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new SoQuadros(), $"Clique no quadro que alimenta {Quadros.Nome(filho)} (o geral)"));
            var motivo = Quadros.Alimentar(doc, filho, pai);
            TaskDialog.Show("AutoEletrica — Alimentar quadro", motivo is null
                ? $"{Quadros.Nome(pai)} alimenta {Quadros.Nome(filho)}: circuito alimentador criado no {Quadros.Nome(pai)}. O cálculo dimensiona o alimentador com a soma das cargas do {Quadros.Nome(filho)}."
                : "Não feito: " + motivo);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CriarCircuitosCommand : ExternalCommand
{
    sealed class SoQuadros : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is FamilyInstance { MEPModel: Autodesk.Revit.DB.Electrical.ElectricalEquipment };
        public bool AllowReference(Reference r, XYZ p) => false;
    }

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Criar circuitos") is not { } doc || ui is null) return;
        if (Contexto.Dados(doc) is not { } dados) return;
        var padrao = PadraoEletrica.Carregar(doc);
        var quadros = Quadros.Todos(doc).ToList();
        if (quadros.Count == 0)
        {
            TaskDialog.Show("AutoEletrica — Criar circuitos", "Nenhum quadro no modelo. Insira o quadro (Lançamento → Quadro) antes.");
            return;
        }
        FamilyInstance quadro;
        if (quadros.Count == 1) quadro = quadros[0];
        else
        {
            try { quadro = (FamilyInstance)doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new SoQuadros(), "Clique no quadro que vai receber os circuitos")); }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return; }
        }
        try
        {
            var proposta = CriarCircuitos.Propor(doc, dados, padrao, quadro);
            if (proposta.Circuitos.Count == 0)
            {
                TaskDialog.Show("AutoEletrica — Criar circuitos", proposta.Soltos == 0
                    ? "Todos os pontos elétricos já estão em circuitos."
                    : "Há pontos sem circuito, mas nenhum pôde entrar:\n\n" + string.Join("\n", proposta.Avisos.Take(12)));
                return;
            }
            var janela = new CriarCircuitosJanela(proposta, Quadros.Nome(quadro), proposta.Avisos);
            if (janela.ShowDialog() != true || janela.Resultado is not { } escolhidos) return;
            var resultado = CriarCircuitos.Criar(doc, quadro, escolhidos, proposta.Elementos);
            var texto = $"{resultado.Criados} circuito(s) criados no quadro {Quadros.Nome(quadro)} com descrição e tipo de carga. " +
                        "O número do circuito é o do Revit (posição no quadro); a descrição guarda o número do plugin. Agora trace os eletrodutos e rode Passar circuitos.";
            if (resultado.Avisos.Count > 0) texto += "\n\n" + string.Join("\n", resultado.Avisos.Take(15)) + (resultado.Avisos.Count > 15 ? $"\n... e mais {resultado.Avisos.Count - 15}" : "");
            TaskDialog.Show("AutoEletrica — Criar circuitos", texto + "\n\nCtrl+Z desfaz.");
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException or Motor.Modelo.ErroDeModelo)
        {
            TaskDialog.Show("AutoEletrica — Criar circuitos", e.Message);
        }
    }
}
