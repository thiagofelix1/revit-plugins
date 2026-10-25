using System.Reflection;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Spk.AddIn.Commands;

namespace Spk.AddIn;

/// <summary>Cria a aba "SPK" na faixa de opções: Modelo → Cálculo → Resultados.</summary>
public class SpkApplication : IExternalApplication
{
    const string Aba = "SPK";

    public Result OnStartup(UIControlledApplication application)
    {
        application.CreateRibbonTab(Aba);

        var projeto = application.CreateRibbonPanel(Aba, "Projeto");
        Botao<DadosEsbocoCommand>(projeto,
            "Dados do\nesboço", Icones.Formulario,
            "Ramais, espaçamento, fator K em L/min/bar½, área, pressão mínima e C do tubo.");
        Botao<DescobrirFamiliasCommand>(projeto,
            "Famílias", Icones.Lupa,
            "Para um modelo que já veio desenhado: confirma quais famílias são bicos e o fator K.");
        Margem(projeto);

        var rede = application.CreateRibbonPanel(Aba, "Rede");
        Botao<RedeExemploCommand>(rede,
            "Rede de\nexemplo", Icones.Malha,
            "Apaga a malha de exemplo anterior, cria os 12 bicos ligados e abre a planta e o 3D enquadrados.");
        Botao<GerarMalhaCommand>(rede,
            "Gerar", Icones.Malha,
            "Cria uma malha retangular de bicos e tubos já conectados, no ponto que você clicar.");
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
            "Monta a planta do setor, o isométrico e o corte da descida da malha gerada, e exporta o PDF.");
        Avancado(resultados);
        Margem(resultados);

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
    static void Largura(PushButton botao)
    {
        var item = (Autodesk.Windows.RibbonItem)typeof(RibbonItem)
            .GetField("m_RibbonItem", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(botao)!;
        item.MinWidth = 84;
        item.ShowText = true;
        item.Size = Autodesk.Windows.RibbonItemSize.Large;
    }

    static void Margem(RibbonPanel painel)
    {
        var adPanel = (Autodesk.Windows.RibbonPanel)typeof(RibbonPanel)
            .GetField("m_RibbonPanel", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(painel)!;
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
