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

        // Na ordem do fluxo: preparar o projeto, desenhar a rede, conferir e calcular, tirar os resultados.
        var projeto = application.CreateRibbonPanel(Aba, "Projeto");
        Botao<ImportarPadraoCommand>(projeto,
            "Padrão do\nescritório", Icones.Padrao,
            "Copia de outro projeto aberto o carimbo, a etiqueta de tubo, os templates de vista e as legendas, e define o " +
            "número e o código das folhas. As folhas passam a usar esse padrão.");
        Botao<DadosProjetoCommand>(projeto,
            "Dados do\nprojeto", Icones.DadosDoProjeto,
            "Classe de risco, base de cálculo, hidrantes, reserva, fator C e abastecimento. Escolhidos pelo engenheiro.");
        Botao<DescobrirFamiliasCommand>(projeto,
            "Famílias", Icones.Familias,
            "Para um modelo que já veio desenhado: confirma quais famílias são bicos (fator K e área) e qual válvula é a VGA de cada setor.");
        Botao<ArquiteturaCommand>(projeto,
            "Arquitetura", Icones.Arquitetura,
            "Lê a arquitetura e a estrutura, do arquivo e dos vínculos (Revit ou IFC): ambientes com pé-direito e o que forma o " +
            "teto de cada um (forro, laje ou telhado), e as vigas. Mostra o que o SPK enxerga; não muda nada no modelo.");
        Margem(projeto);

        var rede = application.CreateRibbonPanel(Aba, "Rede");
        Botao<GerarRedeCommand>(rede,
            "Gerar\nrede", Icones.GerarRede,
            "Gera a rede nos ambientes da arquitetura: bicos pela norma, subgeral com a descida, ramais, ligação dos bicos e " +
            "diâmetros da tabela, em espinha de peixe ou lateral. Usa as mesmas ferramentas dos botões ao lado. Ctrl+Z desfaz cada ambiente.");
        Botao<BicosPorAmbienteCommand>(rede,
            "Bicos por\nambiente", Icones.BicosPorAmbiente,
            "Posiciona bicos nos ambientes da arquitetura (vínculos ou o próprio arquivo) pela regra da norma: área por bico, " +
            "distância entre bicos e até a parede (Tabela 2), defletor na altura do forro, da laje ou abaixo da viga. Também " +
            "confere os bicos que já existem. Ctrl+Z desfaz tudo.");
        Botao<TracarRamalCommand>(rede,
            "Traçar\nramais", Icones.TracarRamais,
            "Traça os ramais de várias fileiras de bicos até a subgeral e liga os bicos: o SPK separa as fileiras, e o ramal fica na " +
            "altura da subgeral (tê ou cruzeta), acima ou abaixo dela (com subida) ou a uma altura fixa. Bico pendente ou em pé. " +
            "Selecione os bicos (pode arrastar uma janela) e a subgeral. Ctrl+Z desfaz tudo.");
        Botao<LigarBicosCommand>(rede,
            "Ligar\nbicos", Icones.LigarBicos,
            "Liga bicos soltos ao ramal que passa sobre eles: parte o ramal, põe o tê e faz a descida (ou o braço, o cotovelo e a " +
            "descida, se o bico está de lado). Selecione os bicos e o ramal antes, ou clique e selecione depois. Ctrl+Z desfaz tudo.");
        Botao<LigarAGeralCommand>(rede,
            "Ligar à\ngeral", Icones.LigarAGeral,
            "Junta as redes das salas num setor: desenhe a geral com a ferramenta Tubo (da VGA pelo corredor), selecione-a e clique " +
            "aqui. Cada rede solta perde a descida até o piso e ganha um tubo até a geral, com tê. Ctrl+Z desfaz tudo.");
        Pequenos(rede,
            (typeof(InserirBicoCommand), "Inserir bico", Icones.InserirBico,
                "Clique no ramal onde vai o bico novo: ele sai igual ao bico mais perto da mesma rede (tipo e altura), já com tê e " +
                "descida. Vários cliques seguidos; Esc termina. Avisa se ficou a menos de 1,8 m de outro bico."),
            (typeof(ApagarBicoCommand), "Apagar bico", Icones.ApagarBico,
                "Apaga os bicos selecionados com a descida e o braço deles e emenda a rede: o tê vira tubo, a cruzeta vira tê, e a " +
                "ponta do ramal que só servia ao bico sai."),
            (typeof(MoverBicoCommand), "Mover bico", Icones.MoverBico,
                "Leva o bico para o ponto que você clicar na planta, religado ao ramal mais perto da mesma rede (com braço, se ficar " +
                "de lado), e emenda a rede no lugar antigo."));
        Pequenos(rede,
            (typeof(LigarTubosCommand), "Ligar tubos", Icones.LigarTubos,
                "Clique no tubo que chega (o da ponta solta) e no tubo onde ele liga: no meio do segundo, o SPK parte e põe o tê " +
                "(com subida se as alturas são diferentes); na ponta dele, estica os dois até o canto e põe o cotovelo. Vários pares " +
                "seguidos; Esc termina."),
            (typeof(ConferirNaPlantaCommand), "Conferir na planta", Icones.ConferirNaPlanta,
                "Desenha a conferência da norma na planta aberta: bico fora da regra em vermelho com a nota do que falta, o alcance de " +
                "cada bico em cinza e os trechos sem cobertura com um círculo vermelho. Só anotação da vista."),
            (typeof(LimparConferenciaCommand), "Limpar conferência", Icones.LimparConferencia,
                "Apaga as marcas da conferência desta vista e tira o vermelho dos bicos."));
        Pequenos(rede,
            (typeof(GerarMalhaCommand), "Malha retangular", Icones.MalhaRetangular,
                "Cria uma malha retangular de bicos e tubos já conectados, no ponto que você clicar, com a geometria de Dados do " +
                "esboço. Para testar sem arquitetura."),
            (typeof(DadosEsbocoCommand), "Dados do esboço", Icones.DadosDoEsboco,
                "Geometria da malha gerada: arranjo, ramais, espaçamentos, altura, bico (K em L/min/bar½ e área) e diâmetros."),
            (typeof(RedeExemploCommand), "Rede de exemplo", Icones.RedeDeExemplo,
                "Cria um cenário de teste pronto (simples, espinha de peixe, grelha, 2 pavimentos, risco leve), com planta e 3D enquadrados."));
        Margem(rede);

        var calculo = application.CreateRibbonPanel(Aba, "Cálculo");
        Botao<MarcarSetorCommand>(calculo,
            "Marcar\nsetor", Icones.MarcarSetor,
            "Para rede sem a VGA desenhada: clique no tubo por onde a água chega (a descida da VGA) e dê o nome do setor. " +
            "Calcular, Diâmetros, o memorial e as folhas usam essa entrada e esse nome, em vez da ponta solta do tubo mais grosso e de \"Setor 1\". " +
            "Clique de novo na mesma rede para trocar o nome ou tirar a marca.");
        Botao<VerificarModeloCommand>(calculo,
            "Verificar\nmodelo", Icones.Verificar,
            "Confere se o SPK consegue ler a rede: peças encostadas mas não ligadas, tubo sem tê, bico solto, ponta aberta, " +
            "sistema que não é de incêndio, famílias não confirmadas e bicos sem VGA. Mostra cada item no modelo e corrige as peças encostadas.");
        Botao<DiametrosCommand>(calculo,
            "Diâmetros", Icones.Diametros,
            "Põe nos tubos do setor o DN da tabela da norma (IT-23 / NBR 10897), pelo número de bicos que cada trecho alimenta. " +
            "Mostra antes o que vai mudar e oferece calcular no fim para conferir. Risco leve e ordinário. Ctrl+Z desfaz tudo.");
        Botao<CalcularRedeCommand>(calculo,
            "Calcular", Icones.Calcular,
            "Calcula o setor: lê a rede pelas conexões, acha a área mais desfavorável e grava o resultado no modelo.");
        Margem(calculo);

        var resultados = application.CreateRibbonPanel(Aba, "Resultados");
        Botao<AbrirMemorialCommand>(resultados,
            "Memorial", Icones.Memorial,
            "Abre o último memorial de cálculo (.docx) gerado.");
        Botao<FolhasCommand>(resultados,
            "Folhas", Icones.Folhas,
            "Monta a setorização das VGAs, o isométrico geral com os quadros e as folhas do último setor calculado (planta " +
            "com a área de cálculo, 3D e corte), preenche o carimbo e exporta o PDF.");
        Pequenos(resultados,
            (typeof(CoresDaRedeCommand), "Cores da rede", Icones.CoresDaRede,
                "Pinta a rede da vista aberta por diâmetro, setor, velocidade nos tubos ou pressão nos bicos, com legenda na planta. " +
                "Só nesta vista; não muda o modelo."),
            (typeof(TirarCoresCommand), "Tirar cores", Icones.TirarCores,
                "Volta a cor normal do que Cores da rede pintou nesta vista e apaga a legenda."),
            (typeof(ListaDeMaterialCommand), "Lista de material", Icones.ListaDeMaterial,
                "Metros de tubo por diâmetro, conexões, bicos e válvulas: o resumo na hora e tabelas do Revit (SPK — ...) que se " +
                "atualizam sozinhas e vão para a folha."));
        Margem(resultados);

        var ajuda = application.CreateRibbonPanel(Aba, "Ajuda");
        Botao<GuiaCommand>(ajuda,
            "Guia", Icones.Guia,
            "O que já foi feito no projeto aberto e o que falta, com atalho para cada etapa; passo a passo, como preparar e " +
            "verificar o modelo, em que o cálculo se baseia e o que as folhas trazem.");
        Avancado(ajuda);
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

    /// <summary>Botões pequenos empilhados, para o que é secundário no painel.</summary>
    static void Pequenos(RibbonPanel painel, params (Type Tipo, string Texto, Action<DrawingContext> Icone, string Dica)[] botoes)
    {
        var dados = botoes.Select(b => (RibbonItemData)new PushButtonData(b.Tipo.Name, b.Texto, b.Tipo.Assembly.Location, b.Tipo.FullName)
        {
            ToolTip = b.Dica,
            Image = Icones.Gerar(b.Icone, 16),
            LargeImage = Icones.Gerar(b.Icone, 32),
        }).ToList();
        if (dados.Count == 2) painel.AddStackedItems(dados[0], dados[1]);
        else painel.AddStackedItems(dados[0], dados[1], dados[2]);
    }

    static void Avancado(RibbonPanel painel)
    {
        var tipo = typeof(StartupCommand);
        var menu = (PulldownButton)painel.AddItem(new PulldownButtonData("SpkAvancado", "Avançado")
        {
            ToolTip = "Ferramentas de desenvolvimento.",
            Image = Icones.Gerar(Icones.Avancado, 16),
        });
        menu.AddPushButton(new PushButtonData(tipo.Name, "Exemplo", tipo.Assembly.Location, tipo.FullName)
        {
            ToolTip = "Calcula o galpão fictício do motor, sem ler o arquivo aberto.",
            Image = Icones.Gerar(Icones.Exemplo, 16),
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
