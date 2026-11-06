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
            var problemas = Motor.Calculo.Verificacao.Verificar(lida.Instalacao);
            var i = lida.Instalacao;
            VerificacaoJanela.Mostrar(ui, problemas, lida.Elementos, lida.Avisos,
                $"{i.Ambientes.Count} ambientes, {i.Quadros.Count} quadro(s), {i.Circuitos.Count} circuitos, {i.Pontos.Count} pontos e {i.Trechos.Count} trechos de eletroduto verificados.");
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
                VerificacaoJanela.Mostrar(ui, r.Problemas, saida.Lida.Elementos, saida.Avisos, $"{r.Circuitos.Count} circuitos calculados.");
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
        Abrir(resumo.Memorial);
    }

    public static void Abrir(string caminho)
    {
        try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            TaskDialog.Show("AutoEletrica — Memorial", $"O memorial está em {caminho}, mas não há programa associado a .md. Abra com um editor de texto.");
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
        if (vista.ViewType is not (ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.EngineeringPlan))
        {
            TaskDialog.Show("AutoEletrica — Etiquetar", "Abra uma planta (piso ou forro) para etiquetar.");
            return;
        }
        var tagEletroduto = TipoDeEtiqueta(doc, BuiltInCategory.OST_ConduitTags);
        var tagTomada = TipoDeEtiqueta(doc, BuiltInCategory.OST_ElectricalFixtureTags);
        var tagLuminaria = TipoDeEtiqueta(doc, BuiltInCategory.OST_LightingFixtureTags);
        if (tagEletroduto is null && tagTomada is null && tagLuminaria is null)
        {
            TaskDialog.Show("AutoEletrica — Etiquetar", "Nenhuma família de etiqueta de eletroduto, tomada ou luminária carregada. Carregue as etiquetas do escritório " +
                                                        "(com os rótulos AE Fiação, AE Ambiente, Circuit Number) e rode de novo.");
            return;
        }
        var jaEtiquetados = new FilteredElementCollector(doc, vista.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>()
            .SelectMany(t => t.GetTaggedLocalElementIds()).ToHashSet();
        var criadas = 0;
        using var transacao = new Transaction(doc, "AutoEletrica — Etiquetar");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);
        if (tagEletroduto is not null)
            foreach (var c in new FilteredElementCollector(doc, vista.Id).OfClass(typeof(Conduit)).Cast<Conduit>())
            {
                if (jaEtiquetados.Contains(c.Id) || string.IsNullOrEmpty(ParametrosEletrica.LerTexto(c, ParametrosEletrica.Fiacao))) continue;
                if (c.Location is not LocationCurve lc || Unidades.Metros(lc.Curve.Length) < 0.6) continue;
                var meio = lc.Curve.Evaluate(0.5, true);
                IndependentTag.Create(doc, tagEletroduto.Id, vista.Id, new Reference(c), false, TagOrientation.Horizontal, meio);
                criadas++;
            }
        foreach (var (categoria, tipo) in new[] { (BuiltInCategory.OST_ElectricalFixtures, tagTomada), (BuiltInCategory.OST_LightingFixtures, tagLuminaria) })
        {
            if (tipo is null) continue;
            foreach (var fi in new FilteredElementCollector(doc, vista.Id).OfCategory(categoria).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
            {
                if (jaEtiquetados.Contains(fi.Id) || (fi.Location as LocationPoint)?.Point is not { } p) continue;
                IndependentTag.Create(doc, tipo.Id, vista.Id, new Reference(fi), false, TagOrientation.Horizontal, p + new XYZ(Unidades.DeMetros(0.25), Unidades.DeMetros(0.25), 0));
                criadas++;
            }
        }
        transacao.Commit();
        TaskDialog.Show("AutoEletrica — Etiquetar", $"{criadas} etiqueta(s) criada(s) na vista {vista.Name}. Os textos vêm dos parâmetros AE (rode Passar circuitos e Calcular para preenchê-los). Ctrl+Z desfaz.");
    }

    static FamilySymbol? TipoDeEtiqueta(Document doc, BuiltInCategory categoria) =>
        new FilteredElementCollector(doc).OfCategory(categoria).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .OrderByDescending(s => (s.FamilyName + s.Name).Contains("AE", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
}

[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class GuiaCommand : ExternalCommand
{
    public override void Execute()
    {
        var doc = Application.ActiveUIDocument?.Document;
        var dados = doc is null ? null : DadosProjeto.Carregar(doc);
        var resumo = doc is null ? null : ArmazenamentoEletrica.Ler<ServicoCalculo.ResumoGravado>(doc, ServicoCalculo.ChaveResultado);
        var situacao = doc is null ? "Nenhum projeto aberto." :
            $"Projeto: {doc.Title}\nDados do projeto: {(dados is null ? "ainda não confirmados" : dados.Resumo())}\n" +
            $"Último cálculo: {(resumo is null ? "nenhum" : $"{resumo.Quando:dd/MM/yyyy HH:mm}, {resumo.Circuitos} circuitos, {(resumo.Atende ? "atende" : "não atende")}")}";
        var dialogo = new TaskDialog("AutoEletrica — Guia")
        {
            MainInstruction = "Passo a passo",
            MainContent =
                "1. Dados do projeto: tensões, fases, esquema de aterramento, Icc presumida, alimentação aérea, dias de trovoada, SPDA, uso.\n" +
                "2. Ambientes: confirme o uso de cada cômodo (define pontos mínimos, potências e DR).\n" +
                "3. No Revit: insira tomadas, luminárias e equipamentos (famílias com carga aparente), crie os circuitos ligados ao quadro, trace os eletrodutos do quadro aos pontos.\n" +
                "4. Passar circuitos: grava em cada eletroduto os circuitos que passam e a contagem de condutores.\n" +
                "5. Verificar modelo: o que a NBR 5410 cobra por cômodo, por circuito, no quadro e nos eletrodutos, com destaque no modelo.\n" +
                "6. Calcular: seções, disjuntores, DR, DPS, queda, curto, eletrodutos; grava nos parâmetros AE e gera o memorial.\n" +
                "7. Etiquetar: etiquetas de fiação nos eletrodutos e dos pontos na planta, com as famílias do escritório.\n\n" + situacao,
            CommonButtons = TaskDialogCommonButtons.Close,
        };
        dialogo.Show();
    }
}
