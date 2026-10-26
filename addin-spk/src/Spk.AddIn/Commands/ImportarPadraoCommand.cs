using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Spk.AddIn.Commands;

/// <summary>
/// Copia de outro projeto aberto (o da Nova Era, ou o modelo do escritório) o carimbo, a etiqueta de tubo, os templates de
/// vista e as legendas, e grava a escolha no projeto. As folhas passam a usar esse padrão.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ImportarPadraoCommand : ExternalCommand
{
    const string Titulo = "SPK — Padrão do escritório";

    public override void Execute()
    {
        try
        {
            var doc = Application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("Abra um projeto antes.");
            var origem = EscolherOrigem(doc);
            if (origem is null) return;

            var janela = new PadraoJanela(Opcoes(origem));
            if (janela.ShowDialog() != true || janela.Resultado is not { } escolha) return;

            var avisos = new List<string>();
            PadraoEscritorio gravado;
            using (var transacao = new Transaction(doc, Titulo))
            {
                EngolirAvisos.Aplicar(transacao);
                transacao.Start();
                gravado = Copiar(origem, doc, escolha, avisos);
                gravado.Salvar(doc);
                if (transacao.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("O Revit desfez a importação ao confirmar. Nada foi copiado.");
            }

            TaskDialog.Show(Titulo, gravado.Resumo() +
                                    (avisos.Count > 0 ? "\n\nAvisos:\n" + string.Join("\n", avisos.Select(a => "• " + a)) : "") +
                                    "\n\nRode Folhas para aplicar.");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            TaskDialog.Show(Titulo, e.Message);
        }
    }

    /// <summary>Outro projeto aberto na mesma sessão. Com mais de um, o engenheiro escolhe.</summary>
    Document? EscolherOrigem(Document destino)
    {
        var abertos = new List<Document>();
        foreach (Document d in Application.Application.Documents)
        {
            if (d.IsFamilyDocument || d.IsLinked) continue;
            if (d.Title == destino.Title && d.PathName == destino.PathName) continue;
            abertos.Add(d);
        }

        if (abertos.Count == 0)
            throw new InvalidOperationException(
                "Abra nesta mesma sessão do Revit o projeto de onde vem o padrão (por exemplo a Nova Era), volte para este projeto e rode de novo.");
        if (abertos.Count == 1) return abertos[0];

        var dialogo = new TaskDialog(Titulo) { MainInstruction = "De qual projeto copiar o padrão?" };
        var opcoes = abertos.Take(4).ToList();
        var links = new[] { TaskDialogCommandLinkId.CommandLink1, TaskDialogCommandLinkId.CommandLink2,
            TaskDialogCommandLinkId.CommandLink3, TaskDialogCommandLinkId.CommandLink4 };
        for (var i = 0; i < opcoes.Count; i++) dialogo.AddCommandLink(links[i], opcoes[i].Title);
        dialogo.CommonButtons = TaskDialogCommonButtons.Cancel;
        var resposta = dialogo.Show();
        var indice = Array.FindIndex(links, l => (int)l == (int)resposta);
        return indice >= 0 && indice < opcoes.Count ? opcoes[indice] : null;
    }

    static OpcoesPadrao Opcoes(Document origem)
    {
        var carimbos = Simbolos(origem, BuiltInCategory.OST_TitleBlocks);
        var etiquetas = Simbolos(origem, BuiltInCategory.OST_PipeTags);
        var templates = new FilteredElementCollector(origem).OfClass(typeof(View)).Cast<View>()
            .Where(v => v.IsTemplate).Select(v => v.Name).OrderBy(n => n).ToList();
        var legendas = new FilteredElementCollector(origem).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && v.ViewType == ViewType.Legend).Select(v => v.Name).OrderBy(n => n).ToList();

        var sugestao = new PadraoEscritorio
        {
            Carimbo = MaisUsado(origem, BuiltInCategory.OST_TitleBlocks) ?? carimbos.FirstOrDefault(),
            EtiquetaTubo = MaisUsado(origem, BuiltInCategory.OST_PipeTags) ?? etiquetas.FirstOrDefault(),
            TemplatePlanta = PorNome(templates, ["PLANTA", "PISO"]),
            Template3d = PorNome(templates, ["3D", "ISOM"]),
            TemplateCorte = PorNome(templates, ["CORTE"]),
            TemplateSetorizacao = PorNome(templates, ["SETOR"]),
            Legendas = legendas.Where(n => DoSistema(n) && Contem(n, ["LEGENDA", "NOTA", "SIMBOLO", "SÍMBOLO"])).ToList(),
        };
        return new OpcoesPadrao(origem.Title, carimbos, etiquetas, templates, legendas, sugestao);
    }

    static List<string> Simbolos(Document doc, BuiltInCategory categoria) =>
        new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsElementType().OfType<FamilySymbol>()
            .Select(PadraoEscritorio.Nome).Distinct().OrderBy(n => n).ToList();

    /// <summary>O tipo mais usado em instâncias: o carimbo das folhas e a etiqueta das vistas do arquivo de origem.</summary>
    static string? MaisUsado(Document doc, BuiltInCategory categoria) =>
        new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsNotElementType()
            .Select(e => doc.GetElement(e.GetTypeId()) as FamilySymbol).OfType<FamilySymbol>()
            .GroupBy(PadraoEscritorio.Nome).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();

    /// <summary>Template de sprinkler com uma destas palavras no nome; se não houver de sprinkler, qualquer um com a palavra.</summary>
    static string? PorNome(IReadOnlyList<string> nomes, string[] palavras) =>
        nomes.FirstOrDefault(n => DoSistema(n) && Contem(n, palavras));

    static bool DoSistema(string nome) => Contem(nome, ["SPK", "SPRINK", "CHUVEIRO", "INCÊNDIO", "INCENDIO", "-INC"]);

    static bool Contem(string nome, string[] palavras) =>
        palavras.Any(p => nome.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

    /// <summary>Copia cada item escolhido e devolve o padrão com os nomes como ficaram neste projeto.</summary>
    static PadraoEscritorio Copiar(Document origem, Document destino, PadraoEscritorio escolha, List<string> avisos)
    {
        var opcoes = new CopyPasteOptions();
        opcoes.SetDuplicateTypeNamesHandler(new UsarTiposDoDestino());
        var resultado = new PadraoEscritorio { Origem = origem.Title };

        resultado.Carimbo = CopiarSimbolo(origem, destino, BuiltInCategory.OST_TitleBlocks, escolha.Carimbo, opcoes, avisos, "Carimbo");
        resultado.EtiquetaTubo = CopiarSimbolo(origem, destino, BuiltInCategory.OST_PipeTags, escolha.EtiquetaTubo, opcoes, avisos, "Etiqueta de tubo");
        resultado.TemplatePlanta = CopiarTemplate(origem, destino, escolha.TemplatePlanta, opcoes, avisos);
        resultado.Template3d = CopiarTemplate(origem, destino, escolha.Template3d, opcoes, avisos);
        resultado.TemplateCorte = CopiarTemplate(origem, destino, escolha.TemplateCorte, opcoes, avisos);
        resultado.TemplateSetorizacao = CopiarTemplate(origem, destino, escolha.TemplateSetorizacao, opcoes, avisos);

        foreach (var nome in escolha.Legendas)
        {
            try
            {
                resultado.Legendas.Add(CopiarLegenda(origem, destino, nome, opcoes));
            }
            catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
            {
                avisos.Add($"Legenda {nome}: {e.Message}");
            }
        }
        return resultado;
    }

    static string? CopiarSimbolo(Document origem, Document destino, BuiltInCategory categoria, string? nome, CopyPasteOptions opcoes,
        List<string> avisos, string papel)
    {
        if (nome is null) return null;
        if (PadraoEscritorio.Tipo(destino, categoria, nome) != ElementId.InvalidElementId) return nome;
        var id = PadraoEscritorio.Tipo(origem, categoria, nome);
        try
        {
            ElementTransformUtils.CopyElements(origem, [id], destino, Transform.Identity, opcoes);
            return nome;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e)
        {
            avisos.Add($"{papel} {nome}: {e.Message}");
            return null;
        }
    }

    /// <summary>Template com o mesmo nome já no projeto fica como está; o do projeto vale.</summary>
    static string? CopiarTemplate(Document origem, Document destino, string? nome, CopyPasteOptions opcoes, List<string> avisos)
    {
        if (nome is null) return null;
        if (Vista(destino, nome, template: true) is not null) return nome;
        var fonte = Vista(origem, nome, template: true);
        if (fonte is null) return null;
        try
        {
            var copiados = ElementTransformUtils.CopyElements(origem, [fonte.Id], destino, Transform.Identity, opcoes);
            return copiados.Select(destino.GetElement).OfType<View>().FirstOrDefault()?.Name ?? nome;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e)
        {
            avisos.Add($"Template {nome}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Copia a vista de legenda. Primeiro a vista inteira; se o Revit recusar, duplica uma legenda deste projeto (a API não
    /// cria legenda do zero) e copia o conteúdo de uma vista para a outra. Se a legenda já existe aqui, o conteúdo é trocado.
    /// </summary>
    static string CopiarLegenda(Document origem, Document destino, string nome, CopyPasteOptions opcoes)
    {
        var fonte = Vista(origem, nome, template: false, ViewType.Legend)
                    ?? throw new InvalidOperationException("não está mais no arquivo de origem.");
        var alvo = Vista(destino, nome, template: false, ViewType.Legend);
        if (alvo is null)
        {
            try
            {
                var copiados = ElementTransformUtils.CopyElements(origem, [fonte.Id], destino, Transform.Identity, opcoes);
                if (copiados.Select(destino.GetElement).OfType<View>().FirstOrDefault() is { } copiada) return copiada.Name;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
            }

            var modelo = new FilteredElementCollector(destino).OfClass(typeof(View)).Cast<View>()
                             .FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.Legend)
                         ?? throw new InvalidOperationException(
                             "o Revit não copia a vista inteira e este projeto não tem nenhuma legenda para duplicar. " +
                             "Crie uma (Vista > Legendas > Legenda) e importe de novo.");
            alvo = (View)destino.GetElement(modelo.Duplicate(ViewDuplicateOption.Duplicate));
            alvo.Name = nome;
        }
        else
        {
            var antigos = ConteudoDa(destino, alvo);
            if (antigos.Count > 0) destino.Delete(antigos);
        }

        try { alvo.Scale = fonte.Scale; }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { }
        var conteudo = ConteudoDa(origem, fonte);
        if (conteudo.Count > 0)
            ElementTransformUtils.CopyElements(fonte, conteudo, alvo, Transform.Identity, opcoes);
        return alvo.Name;
    }

    static List<ElementId> ConteudoDa(Document doc, View vista) =>
        new FilteredElementCollector(doc, vista.Id).WhereElementIsNotElementType()
            .Where(e => e.ViewSpecific && e.OwnerViewId == vista.Id && e.Category is not null)
            .Select(e => e.Id).ToList();

    static View? Vista(Document doc, string nome, bool template, ViewType? tipo = null) =>
        new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .FirstOrDefault(v => v.IsTemplate == template && v.Name == nome && (tipo is null || v.ViewType == tipo));

    sealed class UsarTiposDoDestino : IDuplicateTypeNamesHandler
    {
        public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args) => DuplicateTypeAction.UseDestinationTypes;
    }
}
