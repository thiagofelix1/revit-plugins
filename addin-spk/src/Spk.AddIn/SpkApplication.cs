using System.Reflection;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Spk.AddIn.Commands;

namespace Spk.AddIn;

/// <summary>Cria a aba "SPK" na faixa de opções: Modelo → Cálculo → Resultados.</summary>
public class SpkApplication : IExternalApplication
{
    public const string Aba = "SPK";

    public Result OnStartup(UIControlledApplication application)
    {
        application.CreateRibbonTab(Aba);

        var projeto = application.CreateRibbonPanel(Aba, "Projeto");
        Botao<DadosProjetoCommand>(projeto,
            "Dados do\nprojeto", Icones.Formulario,
            "Classe de risco, base de cálculo, hidrantes, reserva, fator C e abastecimento. Escolhidos pelo engenheiro.");
        Botao<DadosEsbocoCommand>(projeto,
            "Dados do\nesboço", Icones.Formulario,
            "Geometria da malha: ramais, espaçamentos, altura, fator K em L/min/bar½ e área por bico.");
        Botao<DescobrirFamiliasCommand>(projeto,
            "Famílias", Icones.Lupa,
            "Para um modelo que já veio desenhado: confirma quais famílias são bicos e o fator K.");
        Botao<ImportarPadraoCommand>(projeto,
            "Padrão do\nescritório", Icones.Folha,
            "Copia de outro projeto aberto (a Nova Era, por exemplo) o carimbo, a etiqueta de tubo, os templates de vista e as " +
            "legendas. As folhas passam a usar esse padrão.");
        Margem(projeto);

        var rede = application.CreateRibbonPanel(Aba, "Rede");
        Botao<RedeExemploCommand>(rede,
            "Rede de\nexemplo", Icones.Malha,
            "Escolhe um cenário de teste (malha simples, espinha de peixe, grelha, 2 pavimentos, risco leve), apaga a malha " +
            "gerada anterior, cria a rede ligada e abre a planta e o 3D enquadrados.");
        Botao<GerarMalhaCommand>(rede,
            "Gerar", Icones.Malha,
            "Cria uma malha retangular de bicos e tubos já conectados, no ponto que você clicar.");
        Botao<VerificarModeloCommand>(rede,
            "Verificar\nmodelo", Icones.Verificar,
            "Confere se o SPK consegue ler a rede: peças encostadas mas não ligadas, tubo sem tê, bico solto, ponta aberta, " +
            "sistema que não é de incêndio, famílias não confirmadas e bicos sem VGA. Clique num item para ir até ele.");
        Botao<CalcularRedeCommand>(rede,
            "Calcular", Icones.Executar,
            "Calcula a malha gerada. Se não houver, usa as famílias confirmadas.");
        Margem(rede);

        var resultados = application.CreateRibbonPanel(Aba, "Resultados");
        Botao<AbrirMemorialCommand>(resultados,
            "Memorial", Icones.Documento,
            "Abre o último memorial de cálculo (.docx) gerado.");
        Botao<FolhasCommand>(resultados,
            "Folhas", Icones.Folha,
            "Monta a setorização das VGAs, o isométrico geral e as folhas do último setor calculado (planta com a área de " +
            "cálculo, 3D e corte), e exporta o PDF.");
        Avancado(resultados);
        Margem(resultados);

        var ajuda = application.CreateRibbonPanel(Aba, "Ajuda");
        Botao<GuiaCommand>(ajuda,
            "Guia", Icones.Livro,
            "O que já foi feito no projeto aberto e o que falta, com atalho para cada etapa; passo a passo, como preparar o " +
            "modelo, em que o cálculo se baseia e o que as folhas trazem.");
        Margem(ajuda);

        Automacao.TesteAutomatico.Registrar(application);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    static void Botao<TComando>(RibbonPanel painel, string texto, Action<DrawingContext> icone, string dica)
    {
        var tipo = typeof(TComando);
        var botao = (PushButton)painel.AddItem(new PushButtonData(tipo.Name, texto, tipo.Assembly.Location, tipo.FullName)
        {
            ToolTip = dica,
            LargeImage = Icones.Gerar(icone, 32),
            Image = Icones.Gerar(icone, 16),
        });
        Largura(botao);
    }

    static void Avancado(RibbonPanel painel)
    {
        var tipo = typeof(StartupCommand);
        var menu = (PulldownButton)painel.AddItem(new PulldownButtonData("SpkAvancado", "Avançado")
        {
            ToolTip = "Ferramentas de desenvolvimento.",
            Image = Icones.Gerar(Icones.Executar, 16),
        });
        menu.AddPushButton(new PushButtonData(tipo.Name, "Exemplo", tipo.Assembly.Location, tipo.FullName)
        {
            ToolTip = "Calcula o galpão fictício do motor, sem ler o arquivo aberto.",
            Image = Icones.Gerar(Icones.Executar, 16),
        });
    }

    /// <summary>
    /// A API pública deixa o botão grande na largura do ícone. A faixa nativa usa botões mais largos
    /// e uma folga nas laterais do painel; isso se ajusta em AdWindows.
    /// </summary>
    /// <remarks>Campos internos do Revit: se uma versão mudar o nome, o botão fica com a largura padrão em vez de derrubar a aba.</remarks>
    static void Largura(PushButton botao)
    {
        if (typeof(RibbonItem).GetField("m_RibbonItem", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(botao)
            is not Autodesk.Windows.RibbonItem item) return;
        item.MinWidth = 84;
        item.ShowText = true;
        item.Size = Autodesk.Windows.RibbonItemSize.Large;
    }

    static void Margem(RibbonPanel painel)
    {
        if (typeof(RibbonPanel).GetField("m_RibbonPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(painel)
            is not Autodesk.Windows.RibbonPanel adPanel) return;
        var itens = adPanel.Source.Items;
        itens.Insert(0, Espaco(10));
        itens.Add(Espaco(10));
    }

    static Autodesk.Windows.RibbonSeparator Espaco(double largura) => new()
    {
        SeparatorStyle = Autodesk.Windows.RibbonSeparatorStyle.Spacer,
        Width = largura,
        MinWidth = largura,
    };
}
