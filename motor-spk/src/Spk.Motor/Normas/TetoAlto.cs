using Spk.Motor.Calculo;

namespace Spk.Motor.Normas;

/// <summary>O que a Tabela 7 e a Tabela 8 (ou 10) pedem para uma altura de teto. Densidade e área nulas: só vale a Tabela 10.</summary>
public sealed record ExigenciaTetoAlto(double FatorKMinimo, double? Densidade, double? AreaOperacao);

/// <summary>
/// Áreas sem armazenagem com teto acima de 6,1 m (IT-23/2025 8.16.7, Tabelas 7, 8, 10 e 11), para chuveiros de
/// cobertura-padrão. O modelo não diz se o bico é de cobertura estendida (Tabela 9): fica com o engenheiro.
/// </summary>
public static class TetoAlto
{
    public const double AlturaLimite = 6.1;
    public const double AlturaMaxima = 30;
    const double FatorK360 = 360;
    const int ChuveirosK360 = 12;
    const double PressaoK360 = 50;
    const string Item = "IT-23/2025 8.16.7";

    /// <summary>Nulo: a altura não é teto alto, a classe é armazenamento (IT-24) ou a altura passa de 30 m, onde a IT não traz critério.</summary>
    public static ExigenciaTetoAlto? Exigencia(ClasseRisco classe, double altura)
    {
        if (classe == ClasseRisco.Armazenamento || altura <= AlturaLimite || altura > AlturaMaxima) return null;
        if (altura > 18) return new ExigenciaTetoAlto(FatorK360, null, null);
        var faixa = altura <= 9 ? 0 : altura <= 13.5 ? 1 : 2;
        return classe switch
        {
            ClasseRisco.Leve => faixa == 0 ? new ExigenciaTetoAlto(80, 4, 140) : new ExigenciaTetoAlto(115, 8, 230),
            ClasseRisco.Ordinario1 or ClasseRisco.Ordinario2 => new ExigenciaTetoAlto(115, 8, 230),
            _ => faixa switch
            {
                0 => new ExigenciaTetoAlto(160, 12, 230),
                1 => new ExigenciaTetoAlto(160, 12, 340),
                _ => new ExigenciaTetoAlto(160, 20, 280),
            },
        };
    }

    /// <summary>Tabela 11, em minutos.</summary>
    public static double DuracaoMinima(ClasseRisco classe) => classe switch
    {
        ClasseRisco.Leve => 30,
        ClasseRisco.Ordinario1 or ClasseRisco.Ordinario2 => 60,
        _ => 90,
    };

    /// <summary>
    /// Confere os chuveiros sob teto alto contra as tabelas. O projeto tem um critério só, e ele é comparado com a exigência
    /// do teto mais alto do setor; os dados do projeto continuam escolha do engenheiro, o motor só diz se bastam.
    /// </summary>
    public static IReadOnlyList<Verificacao> Verificar(ProjetoSpk projeto, double duracao)
    {
        var verificacoes = new List<Verificacao>();
        if (projeto.AlturaDoTeto is null || projeto.Classe == ClasseRisco.Armazenamento) return verificacoes;
        var altos = projeto.Rede.Chuveiros
            .Where(c => projeto.AlturaDoTeto.TryGetValue(c.Id, out var h) && h > AlturaLimite)
            .Select(c => (Chuveiro: c, Altura: projeto.AlturaDoTeto[c.Id])).ToList();
        if (altos.Count == 0) return verificacoes;

        var maior = altos.Max(a => a.Altura);
        if (maior > AlturaMaxima)
        {
            verificacoes.Add(new Verificacao("Teto alto: critério da IT", Item, false,
                $"teto de {maior:0.0} m: a IT só traz critério até {AlturaMaxima:0} m. Decisão do engenheiro."));
            return verificacoes;
        }

        var exigencia = Exigencia(projeto.Classe, maior)!;
        var abaixo = altos.Where(a => a.Chuveiro.FatorK < Exigencia(projeto.Classe, a.Altura)!.FatorKMinimo - 1e-9).ToList();
        verificacoes.Add(new Verificacao("Teto alto: fator K mínimo", Item + ", Tabela 7", abaixo.Count == 0,
            abaixo.Count == 0
                ? $"{altos.Count} chuveiros sob teto de até {maior:0.0} m, K mínimo {exigencia.FatorKMinimo:0}"
                : $"{abaixo.Count} de {altos.Count} chuveiros sob teto de até {maior:0.0} m com K " +
                  string.Join(", ", abaixo.Select(a => a.Chuveiro.FatorK.ToString("0.#")).Distinct()) +
                  $": a tabela pede K {exigencia.FatorKMinimo:0} ou maior"));

        verificacoes.Add(Demanda(projeto.Base, exigencia, maior, altos.All(a => a.Chuveiro.FatorK >= FatorK360 - 1e-9)));

        var duracaoMinima = DuracaoMinima(projeto.Classe);
        verificacoes.Add(new Verificacao("Teto alto: duração da reserva", Item + ", Tabela 11", duracao >= duracaoMinima - 1e-9,
            $"{duracao:0} min para {duracaoMinima:0} min"));
        return verificacoes;
    }

    static Verificacao Demanda(BaseDeProjeto criterio, ExigenciaTetoAlto exigencia, double altura, bool todosK360)
    {
        var k360Basta = todosK360 && criterio is BaseNumeroFixo { Total: >= ChuveirosK360, PressaoMinima: >= PressaoK360 };
        if (k360Basta)
            return new Verificacao("Teto alto: demanda", Item + ", Tabela 10", true, $"teto de {altura:0.0} m: chuveiros K 360 de cobertura-padrão");
        var pedeK360 = $"{ChuveirosK360} chuveiros K 360 a {PressaoK360 / 100:0.#} bar";
        if (exigencia is not { Densidade: { } densidade, AreaOperacao: { } area })
            return new Verificacao("Teto alto: demanda", Item + ", Tabela 10", false, $"teto de {altura:0.0} m: a tabela pede {pedeK360}");

        var atual = criterio is BaseDensidadeArea d ? $"{d.Densidade:0.##} L/min/m² sobre {d.AreaOperacao:0.#} m²" : criterio.Descricao;
        var atende = criterio is BaseDensidadeArea c && c.Densidade >= densidade - 1e-9 && c.AreaOperacao >= area - 1e-9;
        return new Verificacao("Teto alto: densidade e área de operação", Item + ", Tabela 8", atende,
            $"teto de {altura:0.0} m: a tabela pede {densidade:0.##} L/min/m² sobre {area:0} m² (ou {pedeK360}); o projeto usa {atual}" +
            (atende ? "" : ". Ajuste em Dados do projeto"));
    }
}
