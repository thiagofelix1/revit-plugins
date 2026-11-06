using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Condutos;
using Eletrica.AddIn.Leitura;
using Eletrica.AddIn.Servicos;
using Eletrica.AddIn.Verificacao;
using Eletrica.Motor.Calculo;
using Nice3point.Revit.Toolkit.External;

namespace Eletrica.AddIn.Commands;

static class Contexto
{
    public static Document? Projeto(UIApplication app, string titulo)
    {
        if (app.ActiveUIDocument?.Document is { IsFamilyDocument: false } doc) return doc;
        TaskDialog.Show(titulo, "Abra um projeto antes.");
        return null;
    }

    /// <summary>Os dados confirmados; se ainda não houver, pede antes de seguir.</summary>
    public static DadosProjeto? Dados(Document doc) => DadosProjeto.Carregar(doc) ?? DadosProjetoUi.Editar(doc);

    /// <summary>Verificar do motor (norma) + verificações do modelo do Revit (R-) + os problemas do cálculo, quando houver, num só relatório.</summary>
    public static (IReadOnlyList<Problema> Problemas, IReadOnlyDictionary<string, ElementId> Mapa) VerificarTudo(Document doc, DadosProjeto dados, LeitorInstalacao.Lida lida,
        IReadOnlyList<Problema>? doCalculo)
    {
        var problemas = new List<Problema>(doCalculo ?? Motor.Calculo.Verificacao.Verificar(lida.Instalacao));
        var modelo = VerificacaoDoModelo.Verificar(doc, dados, lida, lida.Volumes);
        problemas.AddRange(modelo.Problemas);
        return (problemas, modelo.Elementos);
    }
}

static class DadosProjetoUi
{
    public static DadosProjeto? Editar(Document doc)
    {
        var janela = new DadosProjetoJanela(DadosProjeto.Carregar(doc) ?? new DadosProjeto());
        if (janela.ShowDialog() != true || janela.Resultado is not { } dados) return null;
        ArmazenamentoEletrica.GravarComTransacao(doc, DadosProjeto.Chave, dados, "AutoEletrica — Dados do projeto");
        return dados;
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DadosProjetoCommand : ExternalCommand
{
    public override void Execute()
    {
        if (Contexto.Projeto(Application, "AutoEletrica — Dados do projeto") is { } doc) DadosProjetoUi.Editar(doc);
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AmbientesCommand : ExternalCommand
{
    public override void Execute()
    {
        if (Contexto.Projeto(Application, "AutoEletrica — Ambientes") is not { } doc) return;
        var ambientes = LeituraDeAmbientes.Ler(doc);
        if (ambientes.Count == 0)
        {
            TaskDialog.Show("AutoEletrica — Ambientes", "Nenhum ambiente (Room) no arquivo nem nos vínculos carregados. Coloque os ambientes na arquitetura ou carregue o vínculo.");
            return;
        }
        var janela = new AmbientesJanela(ambientes);
        if (janela.ShowDialog() != true || janela.Resultado is not { } usos) return;
        var confirmados = UsosConfirmados.Carregar(doc);
        foreach (var (id, uso) in usos) confirmados.Usos[id] = uso;
        ArmazenamentoEletrica.GravarComTransacao(doc, UsosConfirmados.Chave, confirmados, "AutoEletrica — Ambientes");
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ParametrosCommand : ExternalCommand
{
    public override void Execute()
    {
        if (Contexto.Projeto(Application, "AutoEletrica — Parâmetros") is not { } doc) return;
        using var transacao = new Transaction(doc, "AutoEletrica — Parâmetros");
        transacao.Start();
        var aviso = ParametrosEletrica.Garantir(doc);
        transacao.Commit();
        TaskDialog.Show("AutoEletrica — Parâmetros", aviso ?? "Parâmetros AE criados ou já existentes: Circuitos, Condutores, Fiação, DN sugerido e Ocupação nos eletrodutos; " +
                                                             "Seção, Disjuntor, IB, Queda, DR, Situação, Comprimento e Tipo de carga nos circuitos; Ambiente, Potência, Tipo do ponto e Altura nos pontos.");
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class PassarCircuitosCommand : ExternalCommand
{
    public override void Execute()
    {
        if (Contexto.Projeto(Application, "AutoEletrica — Passar circuitos") is not { } doc) return;
        if (Contexto.Dados(doc) is not { } dados) return;
        try
        {
            var lida = LeitorInstalacao.Ler(doc, dados);
            if (lida.Grafo.Cadeias.Count == 0)
            {
                TaskDialog.Show("AutoEletrica — Passar circuitos", "O modelo não tem eletrodutos. Trace os eletrodutos do quadro aos pontos e rode de novo.");
                return;
            }
            var feito = PassarCircuitos.Executar(doc, lida);
            var texto = $"{feito.Circuitos} circuito(s) levados por {feito.Eletrodutos} eletroduto(s). Cada eletroduto recebeu AE Circuitos, AE Condutores e AE Fiação.";
            if (feito.Avisos.Count > 0) texto += "\n\n" + string.Join("\n", feito.Avisos.Take(15)) + (feito.Avisos.Count > 15 ? $"\n... e mais {feito.Avisos.Count - 15}" : "");
            TaskDialog.Show("AutoEletrica — Passar circuitos", texto + "\n\nCtrl+Z desfaz.");
        }
        catch (Exception e) when (e is Motor.Modelo.ErroDeModelo or InvalidOperationException or ArgumentException)
        {
            TaskDialog.Show("AutoEletrica — Passar circuitos", e.Message);
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class VerificarModeloCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Verificar modelo") is not { } doc || ui is null) return;
        if (Contexto.Dados(doc) is not { } dados) return;
        try
        {
            var lida = LeitorInstalacao.Ler(doc, dados);
            var (problemas, mapa) = Contexto.VerificarTudo(doc, dados, lida, null);
            var i = lida.Instalacao;
            VerificacaoJanela.Mostrar(ui, problemas, mapa, lida.Avisos,
                $"{i.Ambientes.Count} ambientes, {i.Quadros.Count} quadro(s), {i.Circuitos.Count} circuitos, {i.Pontos.Count} pontos, {i.Comandos.Count} comando(s), {i.Caixas.Count} caixa(s) e {i.Trechos.Count} trechos de eletroduto verificados.");
        }
        catch (Motor.Modelo.ErroDeModelo e)
        {
            TaskDialog.Show("AutoEletrica — Verificar modelo", "O modelo não pôde ser lido:\n\n" + string.Join("\n", e.Erros.Take(20)));
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CalcularCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Calcular") is not { } doc || ui is null) return;
        if (Contexto.Dados(doc) is not { } dados) return;
        try
        {
            var saida = ServicoCalculo.Executar(doc, dados);
            var r = saida.Resultado;
            var linhas = r.Circuitos.Select(c =>
                $"C{c.Numero} {c.Descricao}: IB {c.CorrenteDeProjeto:0.0} A, {c.SecaoFase:0.#} mm², {c.Disjuntor}, queda {c.QuedaTotal:0.00} %" +
                (c.Atende ? "" : " — NÃO ATENDE: " + string.Join("; ", c.Reprovados.Select(p => p.Criterio))));
            var dialogo = new TaskDialog("AutoEletrica — Calcular")
            {
                MainInstruction = $"{r.Nome}: {(r.Atende ? "ATENDE" : "NÃO ATENDE")}",
                MainContent = $"{r.Circuitos.Count} circuitos, {r.Trechos.Count} trechos, {r.Problemas.Count(p => p.Gravidade == Gravidade.Erro)} erro(s) e " +
                              $"{r.Problemas.Count(p => p.Gravidade == Gravidade.Aviso)} aviso(s). Resultados gravados nos parâmetros AE dos circuitos e eletrodutos.\n\n" +
                              string.Join("\n", linhas.Take(20)) + (r.Circuitos.Count > 20 ? "\n..." : ""),
                ExpandedContent = string.Join("\n", saida.Avisos),
                CommonButtons = TaskDialogCommonButtons.Close,
            };
            dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Abrir o memorial", saida.Memorial);
            dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Ver as verificações no modelo");
            var escolha = dialogo.Show();
            if (escolha == TaskDialogResult.CommandLink1) AbrirMemorialCommand.Abrir(saida.Memorial);
            else if (escolha == TaskDialogResult.CommandLink2)
            {
                var (problemas, mapa) = Contexto.VerificarTudo(doc, dados, saida.Lida, r.Problemas);
                VerificacaoJanela.Mostrar(ui, problemas, mapa, saida.Avisos, $"{r.Circuitos.Count} circuitos calculados e o modelo verificado.");
            }
        }
        catch (Exception e) when (e is Motor.Modelo.ErroDeModelo or InvalidOperationException or ArgumentException or IOException)
        {
            TaskDialog.Show("AutoEletrica — Calcular", e.Message);
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class AbrirMemorialCommand : ExternalCommand
{
    public override void Execute()
    {
        if (Contexto.Projeto(Application, "AutoEletrica — Memorial") is not { } doc) return;
        var resumo = ArmazenamentoEletrica.Ler<ServicoCalculo.ResumoGravado>(doc, ServicoCalculo.ChaveResultado);
        if (resumo is null || !File.Exists(resumo.Memorial))
        {
            TaskDialog.Show("AutoEletrica — Memorial", "Ainda não há memorial deste projeto. Rode Calcular.");
            return;
        }
        if (ServicoCalculo.CalculoAtual(doc, DadosProjeto.Carregar(doc)) is false)
        {
            var dialogo = new TaskDialog("AutoEletrica — Memorial")
            {
                MainInstruction = "O modelo mudou depois do último cálculo.",
                MainContent = $"O memorial é de {resumo.Quando:dd/MM/yyyy HH:mm}; pontos, circuitos, eletrodutos ou dados mudaram depois. Abrir assim mesmo ou calcular de novo?",
                CommonButtons = TaskDialogCommonButtons.Cancel,
            };
            dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Abrir o memorial desatualizado");
            dialogo.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Cancelar e rodar Calcular");
            if (dialogo.Show() != TaskDialogResult.CommandLink1) return;
        }
        Abrir(resumo.Memorial);
    }

    public static void Abrir(string caminho)
    {
        try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            var md = Path.ChangeExtension(caminho, ".md");
            if (File.Exists(md) && md != caminho)
            {
                try { Process.Start(new ProcessStartInfo(md) { UseShellExecute = true }); return; }
                catch (Exception e2) when (e2 is System.ComponentModel.Win32Exception or InvalidOperationException) { }
            }
            TaskDialog.Show("AutoEletrica — Memorial", $"O memorial está em {caminho}, mas não há programa associado. Abra com o Word ou um editor de texto.");
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class EtiquetarCommand : ExternalCommand
{
    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (Contexto.Projeto(Application, "AutoEletrica — Etiquetar") is not { } doc || ui is null) return;
        var vista = ui.ActiveView;
        var feito = Fiacao.Etiquetagem.Executar(doc, vista);
        TaskDialog.Show("AutoEletrica — Etiquetar", feito.Recusa ?? $"{feito.Criadas} etiqueta(s) criada(s) na vista {vista.Name} (eletrodutos, fios, pontos, interruptores e quadros com etiqueta carregada). " +
                                                                  "Os textos vêm dos parâmetros AE (rode Passar circuitos e Calcular para preenchê-los). Ctrl+Z desfaz.");
    }
}
