using System.IO;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Leitura;
using Eletrica.AddIn.Padrao;
using Eletrica.AddIn.Servicos;
using Eletrica.Motor.Calculo;

namespace Eletrica.AddIn.Ajuda;

/// <summary>Um botão de uma etapa: o texto e a classe do comando que ele abre.</summary>
public sealed record AcaoDaEtapa(string Texto, string Comando);

/// <summary>Uma linha de "Seu projeto agora".</summary>
public sealed record Etapa(string Titulo, string Estado, string Detalhe, IReadOnlyList<AcaoDaEtapa> Acoes)
{
    public const string Feito = "Feito";
    public const string Falta = "Falta";
    public const string Opcional = "Opcional";

    public string Cor => Estado switch
    {
        Feito => "#2E7D32",
        Falta => "#C62828",
        _ => "#78909C",
    };
}

/// <summary>
///     Lê o que já está gravado no projeto aberto e diz, etapa por etapa, o que foi feito e o que falta. Só lê: nenhuma
///     etapa muda o modelo.
/// </summary>
static class SituacaoDoProjeto
{
    public static List<Etapa> Ler(Document? doc)
    {
        if (doc is null || doc.IsFamilyDocument)
            return [new Etapa("Projeto", Etapa.Falta, "Abra o projeto elétrico (.rvt), de preferência com a arquitetura vinculada.", [])];

        var dados = DadosProjeto.Carregar(doc);
        LeitorInstalacao.Lida? lida = null;
        string? erroDeLeitura = null;
        var semQuadro = false;
        try
        {
            lida = LeitorInstalacao.Ler(doc, dados ?? new DadosProjeto());
        }
        catch (Motor.Modelo.ErroDeModelo e)
        {
            // Projeto novo, sem quadro: o motor não monta a instalação, mas para o Guia é só a etapa 4 por fazer.
            semQuadro = e.SoFaltaQuadro;
            erroDeLeitura = string.Join("; ", e.Erros);
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException or ArgumentException)
        {
            erroDeLeitura = e.Message;
        }

        return
        [
            Seguro("1. Dados do projeto", () => Dados(dados)),
            Seguro("2. Ambientes", () => Ambientes(doc)),
            Seguro("3. Padrão e famílias", () => PadraoEFamilias(doc)),
            Seguro("4. Quadros, pontos e circuitos", () => QuadrosEPontos(lida, erroDeLeitura, semQuadro)),
            Seguro("5. Eletrodutos e fiação", () => Eletrodutos(lida)),
            Seguro("6. Verificar modelo", () => Verificar(doc, dados, lida)),
            Seguro("7. Cálculo", () => Calculo(doc, dados)),
            Seguro("8. Entregáveis", () => Entregaveis(doc)),
        ];
    }

    /// <summary>
    ///     O Guia só lê, e tem de abrir em qualquer projeto: uma etapa que não consegue ler o modelo mostra o motivo em vez de
    ///     derrubar a janela inteira.
    /// </summary>
    static Etapa Seguro(string titulo, Func<Etapa> ler)
    {
        try
        {
            return ler();
        }
        catch (Exception e) when (e is not (OutOfMemoryException or StackOverflowException))
        {
            return new Etapa(titulo, Etapa.Falta, $"Não foi possível ler esta etapa no modelo: {e.Message}", []);
        }
    }

    static Etapa Dados(DadosProjeto? dados)
    {
        var acoes = new[] { new AcaoDaEtapa("Dados do projeto", "DadosProjetoCommand") };
        return dados is null
            ? new Etapa("1. Dados do projeto", Etapa.Falta,
                "Ainda não confirmados. Tensões, fases, esquema de aterramento e corrente de curto presumida são escolha do engenheiro; sem eles o AutoEletrica não verifica nem calcula.", acoes)
            : new Etapa("1. Dados do projeto", Etapa.Feito, dados.Resumo(), acoes);
    }

    static Etapa Ambientes(Document doc)
    {
        var acoes = new[] { new AcaoDaEtapa("Ambientes", "AmbientesCommand") };
        var ambientes = LeituraDeAmbientes.Ler(doc);
        if (ambientes.Count == 0)
            return new Etapa("2. Ambientes", Etapa.Falta,
                "Nenhum ambiente (Room) no arquivo nem nos vínculos carregados. Vincule a arquitetura com os ambientes, ou recarregue o vínculo em Gerenciar vínculos.", acoes);
        var confirmados = ambientes.Count(a => a.Confirmado);
        var vinculos = ambientes.Select(a => a.Origem).Distinct().Count();
        var detalhe = $"{ambientes.Count} ambiente(s) em {vinculos} origem(ns), {confirmados} com o uso confirmado.";
        return confirmados == ambientes.Count
            ? new Etapa("2. Ambientes", Etapa.Feito, detalhe, acoes)
            : new Etapa("2. Ambientes", Etapa.Falta,
                detalhe + " Os outros usam o uso sugerido pelo nome; confirme, porque o uso define os pontos mínimos, as potências e o DR.", acoes);
    }

    static Etapa PadraoEFamilias(Document doc)
    {
        var acoes = new[]
        {
            new AcaoDaEtapa("Padrão", "PadraoCommand"),
            new AcaoDaEtapa("Famílias", "FamiliasCommand"),
            new AcaoDaEtapa("Parâmetros", "ParametrosCommand"),
        };
        var gravado = ArmazenamentoEletrica.Ler<PadraoEletrica>(doc, PadraoEletrica.Chave) is not null;
        var padrao = PadraoEletrica.Carregar(doc);
        var faltam = new List<string>();
        if (padrao.NomeDe(PapelFamilia.Quadro) is null) faltam.Add("quadro");
        if (padrao.NomeDe(PapelFamilia.Tomada10A) is null && padrao.NomeDe(PapelFamilia.TomadaDupla) is null) faltam.Add("tomada");
        if (padrao.NomeDe(PapelFamilia.Luminaria) is null) faltam.Add("luminária");
        if (padrao.NomeDe(PapelFamilia.Interruptor1) is null) faltam.Add("interruptor simples");
        var papeis = padrao.Familias.Count(kv => !string.IsNullOrWhiteSpace(kv.Value) && kv.Key != nameof(PapelFamilia.Ignorar));
        var detalhe = (gravado ? "Padrão do escritório gravado neste projeto. " : "Padrão ainda não revisado: valem as alturas e potências de fábrica. ") +
                      $"{papeis} papel(éis) de família confirmado(s).";
        return faltam.Count == 0
            ? new Etapa("3. Padrão e famílias", Etapa.Feito, detalhe, acoes)
            : new Etapa("3. Padrão e famílias", Etapa.Falta,
                detalhe + $" Falta confirmar a família de: {string.Join(", ", faltam)}. Sem o papel, o lançamento não sabe o que inserir.", acoes);
    }

    static Etapa QuadrosEPontos(LeitorInstalacao.Lida? lida, string? erro, bool semQuadro)
    {
        var acoes = new[]
        {
            new AcaoDaEtapa("Quadro", "QuadroCommand"),
            new AcaoDaEtapa("Alimentar quadro", "AlimentarQuadroCommand"),
            new AcaoDaEtapa("Pontos por ambiente", "PontosPorAmbienteCommand"),
            new AcaoDaEtapa("Inserir ponto", "InserirPontoCommand"),
            new AcaoDaEtapa("Comando", "ComandoCommand"),
            new AcaoDaEtapa("Criar circuitos", "CriarCircuitosCommand"),
        };
        if (semQuadro)
            return new Etapa("4. Quadros, pontos e circuitos", Etapa.Falta,
                "Nenhum quadro no modelo. Comece pelo Quadro; depois Pontos por ambiente lança o mínimo da NBR 5410 9.5.2 em cada cômodo.", acoes);
        if (lida is null)
            return new Etapa("4. Quadros, pontos e circuitos", Etapa.Falta, $"Não foi possível ler a instalação: {erro}", acoes);

        var i = lida.Instalacao;
        var semCircuito = i.Pontos.Count(p => p.Circuito is null);
        var detalhe = $"{i.Quadros.Count} quadro(s), {i.Circuitos.Count} circuito(s), {i.Pontos.Count} ponto(s), {i.Comandos.Count} comando(s) de iluminação.";
        if (i.Pontos.Count == 0)
            return new Etapa("4. Quadros, pontos e circuitos", Etapa.Falta, detalhe + " Ainda não há pontos: use Pontos por ambiente ou Inserir ponto.", acoes);
        return semCircuito > 0
            ? new Etapa("4. Quadros, pontos e circuitos", Etapa.Falta, detalhe + $" {semCircuito} ponto(s) sem circuito: Criar circuitos divide e liga pela norma.", acoes)
            : new Etapa("4. Quadros, pontos e circuitos", Etapa.Feito, detalhe, acoes);
    }

    static Etapa Eletrodutos(LeitorInstalacao.Lida? lida)
    {
        var acoes = new[]
        {
            new AcaoDaEtapa("Traçar eletroduto", "TracarEletrodutoCommand"),
            new AcaoDaEtapa("Ligar pontos", "LigarPontosCommand"),
            new AcaoDaEtapa("Caixas por regra", "CaixasPorRegraCommand"),
            new AcaoDaEtapa("Passar circuitos", "PassarCircuitosCommand"),
            new AcaoDaEtapa("Fiação manual", "FiacaoManualCommand"),
        };
        if (lida is null) return new Etapa("5. Eletrodutos e fiação", Etapa.Falta, "Depende da etapa 4: os eletrodutos são lidos junto com os quadros e os pontos.", acoes);
        var i = lida.Instalacao;
        if (i.Trechos.Count == 0)
            return new Etapa("5. Eletrodutos e fiação", Etapa.Falta,
                "Nenhum eletroduto. Sem eles o cálculo usa o comprimento do Revit e não conta o agrupamento trecho a trecho. Trace do quadro aos pontos com Traçar eletroduto e Ligar pontos.", acoes);
        var comCircuitos = i.Trechos.Count(t => t.Circuitos.Count > 0);
        var circuitosPassados = i.Trechos.SelectMany(t => t.Circuitos).Distinct().Count();
        var detalhe = $"{i.Trechos.Count} trecho(s) de eletroduto, {comCircuitos} com circuitos gravados; {circuitosPassados} de {i.Circuitos.Count} circuito(s) passados.";
        return comCircuitos == 0 || circuitosPassados < i.Circuitos.Count(c => i.PontosDo(c).Any())
            ? new Etapa("5. Eletrodutos e fiação", Etapa.Falta, detalhe + " Rode Passar circuitos depois de traçar ou mudar circuitos.", acoes)
            : new Etapa("5. Eletrodutos e fiação", Etapa.Feito, detalhe, acoes);
    }

    static Etapa Verificar(Document doc, DadosProjeto? dados, LeitorInstalacao.Lida? lida)
    {
        var acoes = new[] { new AcaoDaEtapa("Verificar modelo", "VerificarModeloCommand") };
        if (dados is null || lida is null)
            return new Etapa("6. Verificar modelo", Etapa.Falta, "Depende dos Dados do projeto (etapa 1) e de um quadro no modelo (etapa 4).", acoes);
        try
        {
            var (problemas, _) = Commands.Contexto.VerificarTudo(doc, dados, lida, null);
            var erros = problemas.Count(p => p.Gravidade == Gravidade.Erro);
            var avisos = problemas.Count(p => p.Gravidade == Gravidade.Aviso);
            if (erros == 0 && avisos == 0)
                return new Etapa("6. Verificar modelo", Etapa.Feito, "Nenhum erro nem aviso. As notas (DR, DPS, reserva) ficam no relatório do Verificar.", acoes);
            var codigos = problemas.Where(p => p.Gravidade == Gravidade.Erro).Select(p => p.Codigo).Distinct().Take(6).ToList();
            return new Etapa("6. Verificar modelo", erros > 0 ? Etapa.Falta : Etapa.Feito,
                $"{erros} erro(s) e {avisos} aviso(s)" + (codigos.Count > 0 ? $" (erros: {string.Join(", ", codigos)})" : "") +
                ". Abra Verificar modelo para ver cada item no modelo; o capítulo Verificar modelo explica cada código.", acoes);
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException or ArgumentException)
        {
            return new Etapa("6. Verificar modelo", Etapa.Falta, $"Não foi possível verificar daqui: {e.Message}", acoes);
        }
    }

    static Etapa Calculo(Document doc, DadosProjeto? dados)
    {
        var acoes = new[] { new AcaoDaEtapa("Calcular", "CalcularCommand"), new AcaoDaEtapa("Memorial", "AbrirMemorialCommand") };
        if (ArmazenamentoEletrica.Ler<ServicoCalculo.ResumoGravado>(doc, ServicoCalculo.ChaveResultado) is not { } resumo)
            return new Etapa("7. Cálculo", Etapa.Falta, "Ainda não calculado neste projeto.", acoes);
        var texto = $"Último cálculo em {resumo.Quando:dd/MM/yyyy HH:mm}: {resumo.Circuitos} circuito(s), {(resumo.Atende ? "ATENDE" : "NÃO ATENDE")}.";
        if (ServicoCalculo.CalculoAtual(doc, dados) is false)
            return new Etapa("7. Cálculo", Etapa.Falta,
                texto + " O modelo mudou depois disso (pontos, circuitos, eletrodutos ou dados): rode Calcular de novo antes de usar o memorial e as etiquetas.", acoes);
        return new Etapa("7. Cálculo", resumo.Atende ? Etapa.Feito : Etapa.Falta,
            texto + (resumo.Atende ? "" : " O Verificar modelo e o memorial dizem o que não passou e a sugestão para cada item."), acoes);
    }

    static Etapa Entregaveis(Document doc)
    {
        var acoes = new[]
        {
            new AcaoDaEtapa("Desenhar fiação", "DesenharFiacaoCommand"),
            new AcaoDaEtapa("Etiquetar", "EtiquetarCommand"),
            new AcaoDaEtapa("Legenda", "LegendaCommand"),
            new AcaoDaEtapa("Quadros", "QuadrosCommand"),
            new AcaoDaEtapa("Lista de material", "ListaDeMaterialCommand"),
            new AcaoDaEtapa("Manual do usuário", "ManualCommand"),
            new AcaoDaEtapa("Pranchas", "PranchasCommand"),
        };
        var folhas = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
            .Count(f => f.SheetNumber.StartsWith("AE-", StringComparison.OrdinalIgnoreCase));
        var resumo = ArmazenamentoEletrica.Ler<ServicoCalculo.ResumoGravado>(doc, ServicoCalculo.ChaveResultado);
        var memorial = resumo is not null && File.Exists(resumo.Memorial);
        var detalhe = (memorial ? "Memorial e manual gerados. " : "Memorial ainda não gerado (sai do Calcular). ") +
                      (folhas == 0 ? "Nenhuma prancha AE- neste projeto." : $"{folhas} prancha(s) AE-; rodar Pranchas de novo refaz só elas.");
        return folhas > 0 && memorial
            ? new Etapa("8. Entregáveis", Etapa.Feito, detalhe, acoes)
            : new Etapa("8. Entregáveis", Etapa.Falta, detalhe, acoes);
    }
}
