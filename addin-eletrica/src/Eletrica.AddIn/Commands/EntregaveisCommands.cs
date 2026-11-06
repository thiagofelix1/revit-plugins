using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Eletrica.AddIn.Folhas;
using Eletrica.AddIn.Leitura;
using Eletrica.AddIn.Padrao;
using Eletrica.AddIn.Servicos;
using Eletrica.Motor.Calculo;
using Nice3point.Revit.Toolkit.External;

namespace Eletrica.AddIn.Commands;

static class Entregaveis
{
    /// <summary>Lê o modelo e calcula (sem gravar), para os entregáveis saírem do modelo como está.</summary>
    public static (LeitorInstalacao.Lida Lida, ResultadoInstalacao Resultado)? Calcular(UIApplication app, string titulo, out Document? doc)
    {
        doc = Contexto.Projeto(app, titulo);
        if (doc is null) return null;
        if (Contexto.Dados(doc) is not { } dados) return null;
        try
        {
            var lida = LeitorInstalacao.Ler(doc, dados);
            return (lida, CalculoEletrica.Executar(lida.Instalacao));
        }
        catch (Motor.Modelo.ErroDeModelo e)
        {
            TaskDialog.Show(titulo, "O modelo não pôde ser lido:\n\n" + string.Join("\n", e.Erros.Take(20)));
            return null;
        }
    }

    public static void Abrir(string caminho, string titulo)
    {
        try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            TaskDialog.Show(titulo, $"O arquivo está em {caminho}, mas não há programa associado. Abra com o Word ou um editor de texto.");
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class QuadrosCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Entregaveis.Calcular(Application, "AutoEletrica — Quadros", out var doc) is not { } calc || doc is null || ui is null) return;
        try
        {
            var feito = QuadrosNoRevit.Gerar(doc, calc.Lida, calc.Resultado);
            if (feito.Vistas.Count > 0) ui.ActiveView = feito.Vistas[0];
            TaskDialog.Show("AutoEletrica — Quadros", $"{feito.Vistas.Count} vista(s) de desenho refeitas: quadro de cargas e unifilar de cada quadro ({calc.Resultado.Quadros.Count}) e a prumada. " +
                                                     "Arraste-as para a prancha ou use Pranchas." + (feito.Avisos.Count > 0 ? "\n\n" + string.Join("\n", feito.Avisos.Take(10)) : ""));
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException or ArgumentException)
        {
            TaskDialog.Show("AutoEletrica — Quadros", e.Message);
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ListaDeMaterialCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Entregaveis.Calcular(Application, "AutoEletrica — Lista de material", out var doc) is not { } calc || doc is null || ui is null) return;
        try
        {
            var feito = ListaDeMaterialRevit.Gerar(doc, calc.Lida, calc.Resultado);
            ui.ActiveView = feito.Vista;
            var dialogo = new TaskDialog("AutoEletrica — Lista de material")
            {
                MainInstruction = "Lista de material gerada.",
                MainContent = $"Vista de desenho \"{feito.Vista.Name}\" (condutores por seção e função, eletrodutos por DN, disjuntores, DRs, DPS, pontos e quadros, pelo cálculo) e " +
                              $"{feito.Tabelas.Count} tabela(s) nativas do Revit do que está modelado. Arquivos:\n{feito.Markdown}\n{feito.Csv}" +
                              (feito.Avisos.Count > 0 ? "\n\n" + string.Join("\n", feito.Avisos.Take(8)) : ""),
                CommonButtons = TaskDialogCommonButtons.Close,
            };
            dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Abrir o CSV");
            if (dialogo.Show() == TaskDialogResult.CommandLink1) Entregaveis.Abrir(feito.Csv, "AutoEletrica — Lista de material");
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException or ArgumentException or IOException)
        {
            TaskDialog.Show("AutoEletrica — Lista de material", e.Message);
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class PranchasCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Entregaveis.Calcular(Application, "AutoEletrica — Pranchas", out var doc) is not { } calc || doc is null || ui is null) return;
        try
        {
            var feito = PranchasAE.Gerar(doc, calc.Lida, calc.Resultado, PadraoEletrica.Carregar(doc));
            if (feito.Folhas.Count > 0) ui.ActiveView = feito.Folhas[0];
            TaskDialog.Show("AutoEletrica — Pranchas", $"{feito.Folhas.Count} prancha(s): {string.Join(", ", feito.Folhas.Select(f => f.SheetNumber))}. " +
                                                      "Planta por nível com legenda, uma folha por quadro (quadro de cargas e unifilar) e a de documentos (prumada, lista, tabelas). " +
                                                      "Rodar de novo refaz só as folhas AE-." + (feito.Avisos.Count > 0 ? "\n\n" + string.Join("\n", feito.Avisos.Take(12)) : ""));
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException or ArgumentException or IOException)
        {
            TaskDialog.Show("AutoEletrica — Pranchas", e.Message);
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class ManualCommand : ExternalCommand
{
    public override void Execute()
    {
        if (Contexto.Projeto(Application, "AutoEletrica — Manual do usuário") is not { } doc) return;
        var resumo = ArmazenamentoEletrica.Ler<ServicoCalculo.ResumoGravado>(doc, ServicoCalculo.ChaveResultado);
        var caminho = resumo?.Manual;
        if (caminho is null || !File.Exists(caminho))
        {
            TaskDialog.Show("AutoEletrica — Manual do usuário", "Ainda não há manual deste projeto: rode Calcular, que gera o memorial, o manual do usuário (NBR 5410 6.1.8.3) e a lista de material.");
            return;
        }
        Entregaveis.Abrir(caminho, "AutoEletrica — Manual do usuário");
    }
}
