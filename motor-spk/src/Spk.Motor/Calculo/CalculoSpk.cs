using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Calculo;

public sealed record ProjetoSpk(string Nome, Rede Rede, ClasseRisco Classe, BaseDeProjeto Base)
{
    /// <summary>Chuveiros da área de operação escolhida à mão. Se nulo, o motor procura a mais desfavorável.</summary>
    public IReadOnlyList<string>? AreaOperacaoManual { get; init; }

    /// <summary>Demanda de hidrantes em L/min. Se nula, usa a Tabela 29 (obrigatória para armazenamento).</summary>
    public double? VazaoHidrantes { get; init; }

    /// <summary>Duração do abastecimento em min. Se nula, usa a Tabela 29.</summary>
    public double? DuracaoMinutos { get; init; }

    public CurvaAbastecimento? Abastecimento { get; init; }
}

public sealed record Verificacao(string Descricao, string Item, bool Atende, string Detalhe);

public sealed record RelatorioSpk(
    ProjetoSpk Projeto,
    ResultadoDemanda Resultado,
    ResultadoBusca? Busca,
    double VazaoHidrantes,
    double DuracaoMinutos,
    double? PressaoDisponivel,
    IReadOnlyList<Verificacao> Verificacoes,
    IReadOnlyList<string> Avisos)
{
    /// <summary>Chuveiros + hidrantes (NBR 8.5.1.1), em L/min.</summary>
    public double VazaoTotal => Resultado.VazaoNecessaria + VazaoHidrantes;

    public double ReservaM3 => VazaoTotal * DuracaoMinutos / 1000.0;

    public bool Atende => Verificacoes.All(v => v.Atende);
}

public static class CalculoSpk
{
    public static RelatorioSpk Executar(ProjetoSpk projeto)
    {
        var avisos = new List<string>();
        var (vazaoHidrantes, duracao) = DemandaHidrantes(projeto, avisos);
        var motor = new MotorHidraulico(projeto.Rede);

        ResultadoBusca? busca = null;
        ResultadoDemanda resultado;
        if (projeto.AreaOperacaoManual is { Count: > 0 } manual)
        {
            resultado = motor.CalcularDemanda(manual.Select(id => projeto.Base.Requisito(projeto.Rede.ObterChuveiro(id))).ToList());
            avisos.Add("Área de operação escolhida manualmente: o motor não confirmou que é a mais desfavorável (9.4.4.4.2).");
        }
        else
        {
            busca = BuscaAreaMaisDesfavoravel.Executar(motor, projeto.Base, new OpcoesBusca
            {
                Abastecimento = projeto.Abastecimento,
                VazaoAdicionalNaFonte = vazaoHidrantes,
            });
            resultado = busca.MaisDesfavoravel;
        }

        if (projeto.Base is BaseDensidadeArea { Ajuste: { } ajuste }) avisos.AddRange(ajuste.Avisos);

        double? pressaoDisponivel = null;
        if (projeto.Abastecimento is { } curva)
        {
            var q = resultado.VazaoNecessaria + vazaoHidrantes;
            pressaoDisponivel = curva.PressaoDisponivel(q);
            if (curva.Extrapola(q)) avisos.Add("A vazão total passa do último ponto da curva de abastecimento: valor extrapolado.");
        }

        var verificacoes = Verificar(projeto, resultado, vazaoHidrantes, pressaoDisponivel);
        return new RelatorioSpk(projeto, resultado, busca, vazaoHidrantes, duracao, pressaoDisponivel, verificacoes, avisos);
    }

    static (double Vazao, double Duracao) DemandaHidrantes(ProjetoSpk p, List<string> avisos)
    {
        if (p.Classe == ClasseRisco.Armazenamento)
        {
            if (p.VazaoHidrantes is null || p.DuracaoMinutos is null)
                throw new ArgumentException("Armazenamento: informe a demanda de hidrantes e a duração da IT-24.");
            return (p.VazaoHidrantes.Value, p.DuracaoMinutos.Value);
        }
        var tabela = Nbr10897.DemandaHidrantes(p.Classe);
        if (p.VazaoHidrantes is { } v && v < tabela.Vazao)
            avisos.Add($"Demanda de hidrantes informada ({v:0} L/min) é menor que a da Tabela 29 ({tabela.Vazao:0} L/min).");
        return (p.VazaoHidrantes ?? tabela.Vazao, p.DuracaoMinutos ?? tabela.DuracaoMinutos);
    }

    static List<Verificacao> Verificar(ProjetoSpk p, ResultadoDemanda r, double vazaoHidrantes, double? pressaoDisponivel)
    {
        var v = new List<Verificacao>();
        var abertos = r.Chuveiros;

        v.Add(new Verificacao("Convergência do cálculo da rede", "9.4.3.1",
            r.Estado.Convergiu && r.Estado.ErroBalancoMaximo < 0.01,
            $"{r.Estado.Iteracoes} iterações; desequilíbrio máximo de vazão {r.Estado.ErroBalancoMaximo:0.####} L/min por nó"));

        var piorPressao = abertos.OrderBy(c => c.Pressao - c.PressaoMinima).First();
        v.Add(new Verificacao("Pressão mínima em todos os chuveiros da área", "9.4.4.10",
            abertos.All(c => c.Pressao >= c.PressaoMinima - 1e-6),
            $"menor pressão {piorPressao.Pressao:0.0} kPa ({piorPressao.Id}), mínima {piorPressao.PressaoMinima:0} kPa"));

        if (p.Base is BaseDensidadeArea densidade)
        {
            v.Add(new Verificacao("Vazão de cada chuveiro ≥ densidade × área coberta", "9.4.4.6",
                abertos.All(c => c.Vazao >= c.VazaoMinima - 1e-6),
                $"densidade {densidade.Densidade:0.##} L/min/m²"));

            var areaAberta = abertos.Sum(c => c.AreaCobertura);
            v.Add(new Verificacao("Chuveiros da área cobrem a área de operação", "9.4.4.2",
                areaAberta >= densidade.AreaOperacao - 1e-6,
                $"{abertos.Count} chuveiros cobrindo {areaAberta:0.0} m² para {densidade.AreaOperacao:0.0} m²"));

            if (densidade.Ajuste?.Ajustes.Any(a => a.Item.StartsWith("8.5.5.3")) == true)
                v.Add(new Verificacao("Resposta rápida: pelo menos 5 chuveiros na área", "8.5.5.3.2",
                    abertos.Count >= 5, $"{abertos.Count} chuveiros"));
        }

        if (p.Base is BaseNumeroFixo fixo)
        {
            var areaAberta = abertos.Sum(c => c.AreaCobertura);
            v.Add(new Verificacao($"Área mínima de operação de {fixo.AreaMinima:0} m²", fixo.Referencia,
                areaAberta >= fixo.AreaMinima - 1e-6, $"{areaAberta:0.0} m²"));
        }

        if (Nbr10897.EhRiscoExtra(p.Classe))
        {
            var maior = abertos.Max(c => c.Pressao);
            v.Add(new Verificacao("Pressão máxima de 1 210 kPa em risco extra", "9.4.4.11",
                maior <= Nbr10897.PressaoMaximaRiscoExtra, $"maior pressão {maior:0} kPa"));
        }

        var abaixo = p.Rede.Trechos
            .Where(t => t.DiametroNominal is { } dn && dn < Nbr10897.DiametroNominalMinimo(t.Material))
            .Select(t => t.Id).ToList();
        v.Add(new Verificacao("Diâmetro mínimo dos tubos", "9.4.1.2", abaixo.Count == 0,
            abaixo.Count == 0 ? "todos os trechos com DN informado atendem" : "abaixo do mínimo: " + string.Join(", ", abaixo.Take(10))));

        var areaSetor = p.Rede.Chuveiros.Sum(c => c.AreaCobertura);
        var limite = Nbr10897.AreaMaximaPorColuna(p.Classe);
        v.Add(new Verificacao("Área do setor (VGA) dentro do limite por coluna", "7.3, Tabela 8",
            areaSetor <= limite, $"{areaSetor:0} m² (soma das áreas de cobertura) para {limite:0} m²"));

        if (pressaoDisponivel is { } disponivel)
        {
            var total = r.VazaoNecessaria + vazaoHidrantes;
            v.Add(new Verificacao("Abastecimento atende a demanda", "9.3.4, Anexo B", disponivel >= r.PressaoNecessaria,
                $"disponível {disponivel:0.0} kPa a {total:0} L/min; necessário {r.PressaoNecessaria:0.0} kPa"));
        }

        return v;
    }
}
