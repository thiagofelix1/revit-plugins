namespace Eletrica.Motor.Modelo;

/// <summary>Posição padrão do ponto na parede ou no teto (item 2.9 do plano): decide a altura e, para o cálculo, o volume e o DR.</summary>
public enum PosicaoDoPonto { Baixa, Media, Alta, Interruptor, Teto, Piso }

/// <summary>Comando de iluminação (NBR 5410 5.6.6 e 9.5.2.1.1): um interruptor simples, dois em paralelo (three-way) ou três ou mais com intermediários (four-way).</summary>
public enum TipoDeComando { Simples, Paralelo, Intermediario }

/// <summary>
/// Padrão do escritório para o lançamento (item 1.2 do plano): alturas por posição, potências padrão, limites por circuito
/// e prefixos. O engenheiro muda tudo; o motor só usa o que estiver aqui, e os mínimos da norma continuam valendo.
/// </summary>
public sealed record PadraoDeLancamento
{
    /// <summary>Alturas do centro do ponto em relação ao piso acabado, em m.</summary>
    public double AlturaBaixa { get; init; } = 0.30;
    public double AlturaMedia { get; init; } = 1.20;
    public double AlturaAlta { get; init; } = 2.20;
    public double AlturaInterruptor { get; init; } = 1.10;
    /// <summary>Pé-direito usado para o ponto de luz quando o ambiente não informa a altura, em m.</summary>
    public double PeDireito { get; init; } = 2.60;

    /// <summary>Potências padrão em VA. As tomadas obedecem aos mínimos de 9.5.2.2.2 mesmo que o padrão seja menor.</summary>
    public double PotenciaTug { get; init; } = 100;
    public double PotenciaTugMolhada { get; init; } = 600;
    public double PotenciaChuveiro { get; init; } = 5500;
    public double PotenciaArCondicionado { get; init; } = 1400;
    public double PotenciaTue20A { get; init; } = 1500;

    /// <summary>Sugere o chuveiro elétrico em todo banheiro (ligação direta, 9.5.2.3).</summary>
    public bool SugerirChuveiro { get; init; } = true;

    /// <summary>Sugere ponto de ar-condicionado nos dormitórios e salas.</summary>
    public bool SugerirArCondicionado { get; init; }

    /// <summary>Corrente de projeto máxima que a divisão automática aceita por circuito de iluminação e de TUG, em A.</summary>
    public double CorrenteMaximaIluminacao { get; init; } = 10;
    public double CorrenteMaximaTug { get; init; } = 10;

    /// <summary>Número máximo de pontos por circuito de TUG e de iluminação (prática de escritório, não norma).</summary>
    public int PontosMaximosPorCircuito { get; init; } = 12;

    /// <summary>Acima desta corrente o ponto vai em fase-fase quando a entrada tem duas ou três fases, em A.</summary>
    public double CorrenteParaFaseFase { get; init; } = 20;

    public string PrefixoCircuito { get; init; } = "C";
    public string PrefixoQuadro { get; init; } = "QD";

    public double Altura(PosicaoDoPonto posicao) => posicao switch
    {
        PosicaoDoPonto.Baixa => AlturaBaixa,
        PosicaoDoPonto.Media => AlturaMedia,
        PosicaoDoPonto.Alta => AlturaAlta,
        PosicaoDoPonto.Interruptor => AlturaInterruptor,
        PosicaoDoPonto.Teto => PeDireito,
        _ => 0,
    };
}

/// <summary>Um ponto que a regra propõe para o ambiente, antes de existir no modelo.</summary>
public sealed record PontoProposto(TipoCarga Tipo, PosicaoDoPonto Posicao, double PotenciaVA, int Quantidade, string Motivo, string Item)
{
    /// <summary>Corrente da tomada (10 ou 20 A) quando é tomada.</summary>
    public int? CorrenteDaTomada { get; init; }
}

/// <summary>O que a regra propõe para um ambiente inteiro.</summary>
public sealed record PropostaDoAmbiente(Ambiente Ambiente, IReadOnlyList<PontoProposto> Pontos)
{
    public int Tomadas => Pontos.Where(p => p.Tipo is TipoCarga.TomadaUsoGeral).Sum(p => p.Quantidade);
    public double CargaDeIluminacao => Pontos.Where(p => p.Tipo == TipoCarga.Iluminacao).Sum(p => p.PotenciaVA * p.Quantidade);
}

/// <summary>Um circuito que a divisão automática propõe: os pontos (ids) que ele reúne e por quê.</summary>
public sealed record CircuitoProposto(string Numero, TipoCarga Tipo, Ligacao Ligacao, string Descricao, IReadOnlyList<string> Pontos,
    double PotenciaVA, double CorrenteDeProjeto, string Motivo);

/// <summary>
/// Comando de iluminação: os interruptores (na ordem do caminho) e as luminárias que eles acendem. Decide os condutores de
/// retorno trecho a trecho (5.6.6.1.2: a fase passa pelo interruptor, o neutro vai direto à luminária).
/// </summary>
public sealed record Comando(string Id, string? Circuito, IReadOnlyList<string> Interruptores, IReadOnlyList<string> Luminarias, TipoDeComando Tipo)
{
    /// <summary>Nós do grafo onde estão os interruptores, na mesma ordem; padrão: os próprios ids.</summary>
    public IReadOnlyList<string>? NosDosInterruptores { get; init; }

    public string NoDoInterruptor(int i) => NosDosInterruptores is { } nos && i < nos.Count ? nos[i] : Interruptores[i];

    /// <summary>Tipo pelo número de interruptores: 1 simples, 2 paralelo, 3 ou mais intermediário.</summary>
    public static TipoDeComando TipoPor(int interruptores) => interruptores switch
    {
        <= 1 => TipoDeComando.Simples,
        2 => TipoDeComando.Paralelo,
        _ => TipoDeComando.Intermediario,
    };

    /// <summary>
    /// Condutores de retorno entre o interruptor i e o i+1 (dois em paralelo e intermediário) e do último interruptor às
    /// luminárias (um). Do quadro ao primeiro interruptor vai a fase do circuito, já contada.
    /// </summary>
    public static int RetornosEntreInterruptores(TipoDeComando tipo) => tipo == TipoDeComando.Simples ? 0 : 2;

    public const int RetornosAteALuminaria = 1;
}
