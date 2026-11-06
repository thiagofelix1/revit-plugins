namespace Eletrica.Motor.Modelo;

/// <summary>Dados da alimentação (NBR 5410 4.2.3): o que vem da distribuidora e do local.</summary>
public sealed record Alimentacao(double TensaoFaseNeutro, double TensaoFaseFase, int Fases, EsquemaAterramento Esquema)
{
    /// <summary>Corrente de curto-circuito presumida no ponto de entrega, em kA (trifásica simétrica). Nula: não informada.</summary>
    public double? IccPresumidaKA { get; init; }

    /// <summary>Alimentação por linha total ou parcialmente aérea (5.4.2.1.1).</summary>
    public bool LinhaAerea { get; init; }

    /// <summary>Dias de trovoada por ano (AQ1 ≤ 25, AQ2 &gt; 25; 4.2.6).</summary>
    public int DiasDeTrovoada { get; init; } = 30;

    /// <summary>Estrutura com SPDA ou exposta a descarga direta (AQ3).</summary>
    public bool TemSpda { get; init; }

    /// <summary>Temperatura ambiente de projeto para linhas ao ar, em °C (Tabela 40; 30 °C é a referência).</summary>
    public double TemperaturaAmbiente { get; init; } = 30;

    /// <summary>Edificação de uso residencial (seção 9.5) ou não (regras de tomadas e DR mudam).</summary>
    public bool Habitacao { get; init; } = true;

    // ----- opções de cálculo do projeto (entrega 0.5) -----

    /// <summary>Aplica os fatores de demanda por tipo de carga (<see cref="Normas.FatoresDeDemanda" />) na potência de demanda dos quadros (4.2.1.1).</summary>
    public bool AplicarFatoresDeDemanda { get; init; } = true;

    /// <summary>Resistência do eletrodo de aterramento RA, em Ω, para o esquema TT (RA · IΔn ≤ UL, 5.1.2.2.4.3). Nula: não avaliada.</summary>
    public double? ResistenciaDeAterramentoOhm { get; init; }

    /// <summary>Folga aplicada ao comprimento dos circuitos lido dos eletrodutos (subidas, sobras, emendas), em %.</summary>
    public double FolgaDeComprimentoPercentual { get; init; }

    /// <summary>Folga de ampliação na taxa de ocupação dos eletrodutos (4.2.5.4), em % da área dos condutores.</summary>
    public double FolgaDeOcupacaoPercentual { get; init; }

    /// <summary>Um só condutor de proteção por eletroduto, com a maior seção dos circuitos que passam (6.4.3.1.5), em vez de um PE por circuito.</summary>
    public bool PEComumNoConduto { get; init; }

    public bool EhTrifasica => Fases == 3;
}

/// <summary>Ambiente (Room do Revit) com o que as regras de 9.5 e de 5.1.3.2.2 precisam.</summary>
public sealed record Ambiente(string Id, string Nome, UsoAmbiente Uso, double Area, double Perimetro)
{
    /// <summary>Comprimento da bancada da pia, em m (cozinha): acima dela vão pelo menos duas tomadas (9.5.2.2.1).</summary>
    public double? Bancada { get; init; }

    public bool Molhado => Uso is UsoAmbiente.Cozinha or UsoAmbiente.Copa or UsoAmbiente.AreaServico or UsoAmbiente.Lavanderia
        or UsoAmbiente.Banheiro or UsoAmbiente.Lavabo or UsoAmbiente.Garagem or UsoAmbiente.AreaExterna;

    public bool Banho => Uso is UsoAmbiente.Banheiro;
}

/// <summary>Quadro de distribuição. O quadro de entrada não tem pai.</summary>
public sealed record Quadro(string Id, string Nome, int Fases)
{
    /// <summary>Quadro que alimenta este; nulo no quadro de entrada (medição/geral).</summary>
    public string? QuadroPai { get; init; }

    /// <summary>Nó onde o quadro está (para o grafo de eletrodutos). Padrão: o próprio Id.</summary>
    public string? No { get; init; }

    /// <summary>Fator de demanda aplicado à soma das cargas para o alimentador (1,0 = sem demanda).</summary>
    public double FatorDeDemanda { get; init; } = 1.0;

    /// <summary>Comprimento do alimentador até o quadro pai, em m, quando não há trechos modelados.</summary>
    public double? ComprimentoAlimentador { get; init; }

    /// <summary>Isolação do alimentador (padrão EPR/XLPE 90 °C, 0,6/1 kV).</summary>
    public Isolacao IsolacaoAlimentador { get; init; } = Isolacao.Epr90;

    public MetodoReferencia MetodoAlimentador { get; init; } = MetodoReferencia.B1;

    public string NoDoQuadro => No ?? Id;
}

/// <summary>Ponto de utilização (tomada, luminária, equipamento) com a potência em VA.</summary>
public sealed record Ponto(string Id, string Ambiente, string? Circuito, TipoCarga Tipo, double PotenciaVA)
{
    /// <summary>Nó do grafo de eletrodutos onde o ponto está. Padrão: o próprio Id.</summary>
    public string? No { get; init; }

    /// <summary>Altura em relação ao piso, em m (9.1 e 5.1.3.2.2 nota: luminária ≥ 2,50 m dispensa DR em habitação).</summary>
    public double Altura { get; init; }

    /// <summary>Corrente nominal da tomada (10 ou 20 A pela NBR 14136), quando é tomada.</summary>
    public int? CorrenteDaTomada { get; init; }

    /// <summary>Volume do banheiro em que o ponto está (0 a 3; 4 = fora dos volumes), apurado pelo add-in pela geometria do boxe e da banheira (9.1.2.1).</summary>
    public int? VolumeDoBanheiro { get; init; }

    /// <summary>Segundo algarismo do grau de proteção IP (proteção contra água) da família, quando informado (9.1.4.1).</summary>
    public int? IpAgua { get; init; }

    /// <summary>Nó no ponto de luz (teto) ou na parede, para a regra 9.5.2.1.1.</summary>
    public bool NoTeto { get; init; }

    public string NoDoPonto => No ?? Id;
}

/// <summary>Circuito terminal ou de distribuição (ElectricalSystem do Revit).</summary>
public sealed record Circuito(string Id, string Numero, string Quadro, TipoCarga Tipo, Ligacao Ligacao)
{
    public string Descricao { get; init; } = "";

    /// <summary>Potência total em VA quando não há pontos (alimentador, carga única). Com pontos, é a soma deles.</summary>
    public double? PotenciaVA { get; init; }

    public double FatorDePotencia { get; init; } = 1.0;

    /// <summary>Rendimento (motores, 4.2.1.2.1); 1,0 para cargas comuns.</summary>
    public double Rendimento { get; init; } = 1.0;

    public Isolacao Isolacao { get; init; } = Isolacao.Pvc70;

    public MaterialCondutor Material { get; init; } = MaterialCondutor.Cobre;

    /// <summary>Método de referência quando o circuito não tem trechos modelados.</summary>
    public MetodoReferencia MetodoPadrao { get; init; } = MetodoReferencia.B1;

    /// <summary>Comprimento até o ponto mais distante, em m, quando não há trechos modelados.</summary>
    public double? ComprimentoManual { get; init; }

    /// <summary>Número de circuitos agrupados com este no pior trecho, quando não há trechos modelados (Tabela 42).</summary>
    public int? AgrupamentoManual { get; init; }

    /// <summary>Seção imposta pelo projetista, em mm² (o motor só confere).</summary>
    public double? SecaoImposta { get; init; }

    /// <summary>Corrente nominal do disjuntor imposta pelo projetista, em A (o motor só confere).</summary>
    public double? DisjuntorImposto { get; init; }

    public CurvaDisjuntor? CurvaImposta { get; init; }

    /// <summary>Energia passante (I²t, em A²s) do disjuntor na Icc presumida, do catálogo do fabricante. Sem ela, vale o limite da classe 3 do Anexo ZA.</summary>
    public double? I2tDoDisjuntor { get; init; }

    /// <summary>Taxa de terceira harmônica acima de 15 % (6.2.6.2.3, Tabela 46): neutro conta como carregado.</summary>
    public bool HarmonicasAltas { get; init; }

    /// <summary>Quadro alimentado por este circuito, quando é um alimentador.</summary>
    public string? QuadroAlimentado { get; init; }

    /// <summary>Fase(s) impostas pelo projetista ("A", "B", "C", "AB", "ABC"); nulo: o balanceamento escolhe.</summary>
    public string? FaseImposta { get; init; }

    /// <summary>Condutores em paralelo por fase impostos (6.2.5.7); nulo: um, ou dois quando nenhuma seção única atende.</summary>
    public int? CondutoresPorFaseImpostos { get; init; }

    public int Fases => Ligacao switch
    {
        Ligacao.FN => 1,
        Ligacao.FF or Ligacao.FFN => 2,
        _ => 3,
    };

    public bool TemNeutro => Ligacao is Ligacao.FN or Ligacao.FFN or Ligacao.FFFN;
}

/// <summary>Trecho de eletroduto (ou calha) entre dois nós, com os circuitos que passam por ele.</summary>
public sealed record Trecho(string Id, string De, string Para, double Comprimento, IReadOnlyList<string> Circuitos)
{
    public TipoEletroduto TipoEletroduto { get; init; } = TipoEletroduto.RigidoRoscavel;

    /// <summary>Diâmetro nominal (DN) do eletroduto; nulo quando o motor vai sugerir.</summary>
    public int? Dn { get; init; }

    public MetodoReferencia Metodo { get; init; } = MetodoReferencia.B1;

    /// <summary>Curvas de 90° no trecho (6.2.11.1.7).</summary>
    public int Curvas90 { get; init; }

    /// <summary>Temperatura ambiente do trecho, se diferente da do projeto.</summary>
    public double? TemperaturaAmbiente { get; init; }

    /// <summary>Trecho externo à edificação (30 m entre caixas em vez de 15 m).</summary>
    public bool Externo { get; init; }

    /// <summary>
    /// Condutores de retorno dos comandos de iluminação que passam por este trecho: o id do circuito, repetido uma vez por
    /// condutor. Entram na ocupação do eletroduto com a seção da fase do circuito (5.6.6.1.2).
    /// </summary>
    public IReadOnlyList<string> Retornos { get; init; } = [];
}

/// <summary>Caixa de passagem ou de derivação num nó.</summary>
public sealed record Caixa(string Id, string No);

public sealed class ErroDeModelo(IReadOnlyList<string> erros)
    : Exception("Instalação inválida:\n- " + string.Join("\n- ", erros))
{
    /// <summary>O erro de uma instalação sem quadro: no começo do projeto é o estado normal, não um defeito.</summary>
    public const string SemQuadro = "a instalação precisa de pelo menos um quadro";

    public IReadOnlyList<string> Erros { get; } = erros;

    public bool SoFaltaQuadro => Erros.Count == 1 && Erros[0] == SemQuadro;
}

/// <summary>
/// A instalação inteira, como o add-in a lê do modelo: alimentação, ambientes, quadros, circuitos, pontos e trechos.
/// O construtor valida as referências cruzadas e lança <see cref="ErroDeModelo" /> com todos os problemas de uma vez.
/// </summary>
public sealed class Instalacao
{
    public string Nome { get; }
    public Alimentacao Alimentacao { get; }
    public IReadOnlyList<Ambiente> Ambientes { get; }
    public IReadOnlyList<Quadro> Quadros { get; }
    public IReadOnlyList<Circuito> Circuitos { get; }
    public IReadOnlyList<Ponto> Pontos { get; }
    public IReadOnlyList<Trecho> Trechos { get; }
    public IReadOnlyList<Caixa> Caixas { get; }
    public IReadOnlyList<Comando> Comandos { get; }

    readonly Dictionary<string, Ambiente> _ambientes;
    readonly Dictionary<string, Quadro> _quadros;
    readonly Dictionary<string, Circuito> _circuitos;
    readonly Dictionary<string, Ponto> _pontos;

    public Instalacao(string nome, Alimentacao alimentacao, IEnumerable<Ambiente> ambientes, IEnumerable<Quadro> quadros,
        IEnumerable<Circuito> circuitos, IEnumerable<Ponto> pontos, IEnumerable<Trecho>? trechos = null, IEnumerable<Caixa>? caixas = null, IEnumerable<Comando>? comandos = null)
    {
        Nome = nome;
        Alimentacao = alimentacao;
        Ambientes = ambientes.ToList();
        Quadros = quadros.ToList();
        Circuitos = circuitos.ToList();
        Pontos = pontos.ToList();
        Trechos = (trechos ?? []).ToList();
        Caixas = (caixas ?? []).ToList();
        Comandos = (comandos ?? []).ToList();

        var erros = new List<string>();
        _ambientes = IndexarUnicos(Ambientes, a => a.Id, "ambiente", erros);
        _quadros = IndexarUnicos(Quadros, q => q.Id, "quadro", erros);
        _circuitos = IndexarUnicos(Circuitos, c => c.Id, "circuito", erros);
        _pontos = IndexarUnicos(Pontos, p => p.Id, "ponto", erros);
        IndexarUnicos(Trechos, t => t.Id, "trecho", erros);

        if (alimentacao.TensaoFaseNeutro <= 0 || alimentacao.TensaoFaseFase <= 0) erros.Add("alimentação: tensões devem ser positivas");
        if (alimentacao.Fases is not (1 or 2 or 3)) erros.Add("alimentação: número de fases deve ser 1, 2 ou 3");
        if (Quadros.Count == 0) erros.Add(ErroDeModelo.SemQuadro);
        if (Quadros.Count(q => q.QuadroPai is null) > 1) erros.Add("só um quadro pode ser o de entrada (sem quadro pai)");

        foreach (var q in Quadros)
        {
            if (q.QuadroPai is { } pai && !_quadros.ContainsKey(pai)) erros.Add($"quadro {q.Id}: quadro pai '{pai}' não existe");
            if (q.QuadroPai == q.Id) erros.Add($"quadro {q.Id}: é pai de si mesmo");
            if (q.Fases is not (1 or 2 or 3)) erros.Add($"quadro {q.Id}: número de fases deve ser 1, 2 ou 3");
        }

        foreach (var c in Circuitos)
        {
            if (!_quadros.ContainsKey(c.Quadro)) erros.Add($"circuito {c.Id}: quadro '{c.Quadro}' não existe");
            else if (c.Fases > _quadros[c.Quadro].Fases) erros.Add($"circuito {c.Id}: usa {c.Fases} fases num quadro de {_quadros[c.Quadro].Fases}");
            if (c.FatorDePotencia is <= 0 or > 1) erros.Add($"circuito {c.Id}: fator de potência deve ficar entre 0 e 1");
            if (c.Rendimento is <= 0 or > 1) erros.Add($"circuito {c.Id}: rendimento deve ficar entre 0 e 1");
            if (c.QuadroAlimentado is { } qa && !_quadros.ContainsKey(qa)) erros.Add($"circuito {c.Id}: quadro alimentado '{qa}' não existe");
            if (c.Tipo == TipoCarga.Alimentador && c.QuadroAlimentado is null) erros.Add($"circuito {c.Id}: alimentador sem o quadro alimentado");
        }

        foreach (var p in Pontos)
        {
            if (!_ambientes.ContainsKey(p.Ambiente)) erros.Add($"ponto {p.Id}: ambiente '{p.Ambiente}' não existe");
            if (p.Circuito is { } ci && !_circuitos.ContainsKey(ci)) erros.Add($"ponto {p.Id}: circuito '{ci}' não existe");
            if (p.PotenciaVA < 0) erros.Add($"ponto {p.Id}: potência negativa");
        }

        foreach (var t in Trechos)
        {
            if (t.Comprimento <= 0) erros.Add($"trecho {t.Id}: comprimento deve ser positivo");
            if (t.De == t.Para) erros.Add($"trecho {t.Id}: começa e termina no mesmo nó");
            foreach (var ci in t.Circuitos)
                if (!_circuitos.ContainsKey(ci)) erros.Add($"trecho {t.Id}: circuito '{ci}' não existe");
        }

        foreach (var k in Comandos)
        {
            if (k.Interruptores.Count == 0) erros.Add($"comando {k.Id}: sem interruptor");
            if (k.Luminarias.Count == 0) erros.Add($"comando {k.Id}: sem luminária");
            if (k.Circuito is { } ci && !_circuitos.ContainsKey(ci)) erros.Add($"comando {k.Id}: circuito '{ci}' não existe");
            foreach (var l in k.Luminarias)
                if (!_pontos.ContainsKey(l)) erros.Add($"comando {k.Id}: luminária '{l}' não existe");
        }

        if (erros.Count > 0) throw new ErroDeModelo(erros);
    }

    public Ambiente AmbienteDe(Ponto p) => _ambientes[p.Ambiente];
    public Quadro QuadroDe(Circuito c) => _quadros[c.Quadro];
    public Quadro? QuadroPai(Quadro q) => q.QuadroPai is { } pai ? _quadros[pai] : null;
    public Quadro QuadroDeEntrada => Quadros.First(q => q.QuadroPai is null);
    public Circuito? Circuito(string id) => _circuitos.TryGetValue(id, out var c) ? c : null;
    public Quadro? Quadro(string id) => _quadros.TryGetValue(id, out var q) ? q : null;
    public Ambiente? Ambiente(string id) => _ambientes.TryGetValue(id, out var a) ? a : null;
    public Ponto? Ponto(string id) => _pontos.TryGetValue(id, out var p) ? p : null;

    public IEnumerable<Ponto> PontosDo(Circuito c) => Pontos.Where(p => p.Circuito == c.Id);
    public IEnumerable<Ponto> PontosDo(Ambiente a) => Pontos.Where(p => p.Ambiente == a.Id);
    public IEnumerable<Circuito> CircuitosDo(Quadro q) => Circuitos.Where(c => c.Quadro == q.Id);
    public IEnumerable<Trecho> TrechosDo(Circuito c) => Trechos.Where(t => t.Circuitos.Contains(c.Id));

    /// <summary>Comandos que acendem esta luminária.</summary>
    public IEnumerable<Comando> ComandosDe(Ponto luminaria) => Comandos.Where(k => k.Luminarias.Contains(luminaria.Id));

    /// <summary>O circuito que alimenta o quadro (Tipo Alimentador com QuadroAlimentado = q), se houver.</summary>
    public Circuito? AlimentadorDo(Quadro q) => Circuitos.FirstOrDefault(c => c.QuadroAlimentado == q.Id);

    /// <summary>Potência total do circuito em VA: a soma dos pontos, ou a potência informada.</summary>
    public double PotenciaDo(Circuito c)
    {
        var pontos = PontosDo(c).Sum(p => p.PotenciaVA);
        return pontos > 0 ? pontos : c.PotenciaVA ?? 0;
    }

    static Dictionary<string, T> IndexarUnicos<T>(IEnumerable<T> itens, Func<T, string> id, string nome, List<string> erros)
    {
        var dicionario = new Dictionary<string, T>();
        foreach (var item in itens)
        {
            var chave = id(item);
            if (string.IsNullOrWhiteSpace(chave)) erros.Add($"{nome} sem identificador");
            else if (dicionario.ContainsKey(chave)) erros.Add($"{nome} '{chave}' repetido");
            else dicionario[chave] = item;
        }
        return dicionario;
    }
}
