using System.Reflection;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Eletrica.AddIn.Commands;

namespace Eletrica.AddIn;

/// <summary>Cria a aba "AutoEletrica": Projeto → Fiação → Cálculo → Resultados → Ajuda.</summary>
public class EletricaApplication : IExternalApplication
{
    public const string Aba = "AutoEletrica";

    public Result OnStartup(UIControlledApplication application)
    {
        application.CreateRibbonTab(Aba);

        var projeto = application.CreateRibbonPanel(Aba, "Projeto");
        Botao<DadosProjetoCommand>(projeto,
            "Dados do\nprojeto", Icones.Formulario,
            "Tensões, fases, esquema de aterramento, corrente de curto-circuito presumida, alimentação aérea ou subterrânea, dias de " +
            "trovoada, SPDA, temperatura e uso da edificação. Escolhidos pelo engenheiro; o cálculo usa exatamente o que estiver aqui.");
        Botao<AmbientesCommand>(projeto,
            "Ambientes", Icones.Edificio,
            "Lê os ambientes do arquivo e dos vínculos e sugere o uso de cada um pelo nome (sala, cozinha, banheiro, área de serviço...). " +
            "O uso define os pontos mínimos, a potência das tomadas e onde o DR é obrigatório. Confirme ou corrija antes de verificar.");
        Botao<PadraoCommand>(projeto,
            "Padrão", Icones.Regua,
            "Padrão do escritório: alturas (baixa, média, alta, interruptor, pé-direito), potências padrão das tomadas, chuveiro e ar, corrente e " +
            "número de pontos por circuito, prefixos de circuito e quadro. O lançamento usa isto; os mínimos da norma valem por cima.");
        Botao<FamiliasCommand>(projeto,
            "Famílias", Icones.Familias,
            "Lista as famílias elétricas carregadas e confirma o papel de cada uma (tomada 10 A, 20 A, luminária, arandela, interruptor simples, " +
            "paralelo, intermediário, chuveiro, ar-condicionado, quadro, caixa). O plugin lança e lê pelo papel, não pelo nome.");
        Botao<ParametrosCommand>(projeto,
            "Parâmetros", Icones.Lupa,
            "Cria no projeto os parâmetros compartilhados AE (circuitos e fiação dos eletrodutos, seção, disjuntor, queda, DR e situação dos " +
            "circuitos, ambiente, potência, posição e comando dos pontos). As etiquetas e tabelas do escritório podem usá-los.");
        Margem(projeto);

        var lancamento = application.CreateRibbonPanel(Aba, "Lançamento");
        Botao<QuadroCommand>(lancamento,
            "Quadro", Icones.Quadro,
            "Insere o quadro de distribuição com a família do escritório, já com nome (QD-1, QD-2...) e o sistema de distribuição das tensões e " +
            "fases dos Dados do projeto.");
        Botao<AlimentarQuadroCommand>(lancamento,
            "Alimentar\nquadro", Icones.Alimentador,
            "Liga um quadro terminal ao quadro que o alimenta: cria o circuito alimentador no quadro geral com o terminal como carga. É a hierarquia " +
            "medição → geral → terminais que o cálculo dimensiona.");
        Botao<InserirPontoCommand>(lancamento,
            "Inserir\nponto", Icones.Tomada,
            "Tomada, luminária, interruptor, chuveiro, ar-condicionado ou ponto de força com altura, potência, tipo de carga e circuito já definidos; " +
            "o Revit faz a colocação normal (clique onde quiser, Esc termina) e o plugin completa os dados de cada ponto colocado.");
        Botao<PontosPorAmbienteCommand>(lancamento,
            "Pontos por\nambiente", Icones.BicosAmbiente,
            "Para cada ambiente, o que a NBR 5410 9.5.2 pede (tomadas pelo perímetro e uso, 600 VA nas áreas molhadas, luz no teto com a carga " +
            "mínima) mais o padrão (chuveiro, ar): revise as quantidades e lance; tomadas pelas paredes, luz no centro, interruptor junto da porta já " +
            "comandando a luz. Ctrl+Z desfaz.");
        Botao<ComandoCommand>(lancamento,
            "Comando", Icones.Interruptor,
            "Liga interruptores às luminárias que eles acendem (simples, paralelo ou intermediário). Passar circuitos conta os retornos por trecho e o " +
            "Verificar acusa luminária sem interruptor.");
        Botao<CriarCircuitosCommand>(lancamento,
            "Criar\ncircuitos", Icones.Circuito,
            "Divide os pontos sem circuito pela NBR 5410 (luz separada das tomadas, cozinha e área de serviço exclusivas, banheiro à parte, cada " +
            "equipamento acima de 10 A sozinho, o resto por ambiente até o limite do padrão), mostra a proposta e cria os circuitos no quadro.");
        Margem(lancamento);

        var fiacao = application.CreateRibbonPanel(Aba, "Fiação");
        Botao<PassarCircuitosCommand>(fiacao,
            "Passar\ncircuitos", Icones.TracarRamal,
            "Leva cada circuito do quadro até os seus pontos pelo menor caminho nos eletrodutos e grava em cada eletroduto quais circuitos " +
            "passam e quantos condutores leva. É a contagem trecho a trecho que o cálculo usa para o agrupamento e a ocupação. Ctrl+Z desfaz.");
        Botao<EtiquetarCommand>(fiacao,
            "Etiquetar", Icones.Folha,
            "Põe na vista aberta a etiqueta de fiação de cada eletroduto (circuitos, número de condutores e seções, quando já calculado) e a " +
            "etiqueta dos pontos, com as famílias de etiqueta carregadas no projeto.");
        Margem(fiacao);

        var calculo = application.CreateRibbonPanel(Aba, "Cálculo");
        Botao<VerificarModeloCommand>(calculo,
            "Verificar\nmodelo", Icones.Verificar,
            "Confere o modelo pela NBR 5410: pontos de tomada e de luz por cômodo, carga de iluminação, banheiro, chuveiro, DR obrigatório, " +
            "divisão de circuitos, DPS, reserva do quadro, ponto sem circuito e ponto sem caminho pelos eletrodutos. Mostra cada item no modelo.");
        Botao<CalcularCommand>(calculo,
            "Calcular", Icones.Executar,
            "Dimensiona todos os circuitos pelos seis critérios da NBR 5410 (capacidade com agrupamento trecho a trecho, sobrecarga, curto, " +
            "seccionamento, queda, seção mínima), os eletrodutos e os quadros; grava o resultado nos parâmetros AE e gera o memorial.");
        Margem(calculo);

        var resultados = application.CreateRibbonPanel(Aba, "Resultados");
        Botao<AbrirMemorialCommand>(resultados,
            "Memorial", Icones.Documento,
            "Abre o último memorial de cálculo gerado (Markdown).");
        Margem(resultados);

        var ajuda = application.CreateRibbonPanel(Aba, "Ajuda");
        Botao<GuiaCommand>(ajuda,
            "Guia", Icones.Livro,
            "Passo a passo: dados do projeto, ambientes, padrão e famílias, quadro, pontos, comandos, circuitos, eletrodutos, passar circuitos, verificar, calcular, etiquetar.");
        Margem(ajuda);

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
