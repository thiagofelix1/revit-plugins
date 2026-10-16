namespace Spk.Motor.Modelo;

public enum Material { Aco, Cobre, Cpvc, Outro }

/// <summary>Ponto da rede. Cota Z em metros.</summary>
public sealed record No(string Id, double X, double Y, double Z)
{
    /// <summary>Vazão fixa retirada no nó (um hidrante, por exemplo), em L/min.</summary>
    public double DemandaFixa { get; init; }
}

/// <summary>Trecho de tubo entre dois nós. O sentido De → Para é só referência; o fluxo pode inverter.</summary>
public sealed record Trecho(string Id, string De, string Para, double DiametroInterno, double Comprimento, double C)
{
    /// <summary>Soma dos comprimentos equivalentes de conexões e válvulas, em m (NBR 9.4.3.4).</summary>
    public double ComprimentoEquivalente { get; init; }

    /// <summary>Descrição das conexões que entraram no comprimento equivalente, para o memorial.</summary>
    public string Conexoes { get; init; } = "";

    public int? DiametroNominal { get; init; }

    public Material Material { get; init; } = Material.Aco;

    public double ComprimentoTotal => Comprimento + ComprimentoEquivalente;
}

/// <summary>Chuveiro instalado num nó.</summary>
public sealed record Chuveiro(string Id, string No, double FatorK, double AreaCobertura)
{
    /// <summary>Identifica a linha de chuveiros (os dois lados da subgeral na mesma linha usam o mesmo ramal).</summary>
    public string Ramal { get; init; } = "";

    /// <summary>Pressão mínima de catálogo ou da IT, quando maior que a da NBR, em kPa.</summary>
    public double? PressaoMinima { get; init; }
}

public sealed class ErroDeModelo(IReadOnlyList<string> erros)
    : Exception("Rede inválida:\n- " + string.Join("\n- ", erros))
{
    public IReadOnlyList<string> Erros { get; } = erros;
}

/// <summary>Rede de um setor (um VGA). A fonte é o ponto onde a demanda é calculada.</summary>
public sealed class Rede
{
    public string NoFonte { get; }
    public IReadOnlyList<No> Nos { get; }
    public IReadOnlyList<Trecho> Trechos { get; }
    public IReadOnlyList<Chuveiro> Chuveiros { get; }

    readonly Dictionary<string, No> _nos;
    readonly Dictionary<string, Chuveiro> _chuveiros;

    public Rede(string noFonte, IEnumerable<No> nos, IEnumerable<Trecho> trechos, IEnumerable<Chuveiro> chuveiros)
    {
        NoFonte = noFonte;
        Nos = nos.ToList();
        Trechos = trechos.ToList();
        Chuveiros = chuveiros.ToList();

        var erros = new List<string>();
        _nos = IndexarUnicos(Nos, n => n.Id, "nó", erros);
        IndexarUnicos(Trechos, t => t.Id, "trecho", erros);
        _chuveiros = IndexarUnicos(Chuveiros, c => c.Id, "chuveiro", erros);

        if (!_nos.ContainsKey(noFonte))
            erros.Add($"nó da fonte '{noFonte}' não existe");

        foreach (var t in Trechos)
        {
            if (!_nos.ContainsKey(t.De)) erros.Add($"trecho {t.Id}: nó '{t.De}' não existe");
            if (!_nos.ContainsKey(t.Para)) erros.Add($"trecho {t.Id}: nó '{t.Para}' não existe");
            if (t.De == t.Para) erros.Add($"trecho {t.Id}: começa e termina no mesmo nó");
            if (t.DiametroInterno <= 0) erros.Add($"trecho {t.Id}: diâmetro interno deve ser positivo");
            if (t.Comprimento < 0 || t.ComprimentoEquivalente < 0) erros.Add($"trecho {t.Id}: comprimento negativo");
            if (t.ComprimentoTotal <= 0) erros.Add($"trecho {t.Id}: comprimento total deve ser positivo");
            if (t.C <= 0) erros.Add($"trecho {t.Id}: fator C deve ser positivo");
        }

        var nosComChuveiro = new HashSet<string>();
        foreach (var c in Chuveiros)
        {
            if (!_nos.ContainsKey(c.No)) erros.Add($"chuveiro {c.Id}: nó '{c.No}' não existe");
            if (c.No == noFonte) erros.Add($"chuveiro {c.Id}: não pode estar no nó da fonte");
            if (!nosComChuveiro.Add(c.No)) erros.Add($"chuveiro {c.Id}: já existe outro chuveiro no nó '{c.No}'");
            if (c.FatorK <= 0) erros.Add($"chuveiro {c.Id}: fator K deve ser positivo");
            if (c.AreaCobertura <= 0) erros.Add($"chuveiro {c.Id}: área de cobertura deve ser positiva");
        }

        if (erros.Count > 0) throw new ErroDeModelo(erros);
    }

    public No ObterNo(string id) => _nos[id];
    public Chuveiro ObterChuveiro(string id) => _chuveiros[id];
    public bool ExisteChuveiro(string id) => _chuveiros.ContainsKey(id);

    static Dictionary<string, T> IndexarUnicos<T>(IEnumerable<T> itens, Func<T, string> id, string tipo, List<string> erros)
    {
        var d = new Dictionary<string, T>();
        foreach (var item in itens)
        {
            var chave = id(item);
            if (string.IsNullOrWhiteSpace(chave)) erros.Add($"{tipo} sem identificador");
            else if (d.ContainsKey(chave)) erros.Add($"{tipo} '{chave}' duplicado");
            else d[chave] = item;
        }
        return d;
    }
}
