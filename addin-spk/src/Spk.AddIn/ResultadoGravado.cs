namespace Spk.AddIn;

/// <summary>Último cálculo, gravado no modelo. As folhas leem isto para achar o setor, a área e o bico crítico.</summary>
public sealed class ResultadoGravado
{
    public const string Chave = "Resultado";

    public string Setor { get; set; } = "";
    public string Classificacao { get; set; } = "";
    public bool Atende { get; set; }
    public double PressaoNecessariaKPa { get; set; }
    public double VazaoChuveirosLmin { get; set; }
    public double VazaoTotalLmin { get; set; }

    /// <summary>Hidrantes, duração e reserva do cálculo. Zero num resultado gravado por versão anterior.</summary>
    public double VazaoHidrantesLmin { get; set; }

    public double DuracaoMin { get; set; }
    public double ReservaM3 { get; set; }

    /// <summary>Número de cálculo do bico crítico.</summary>
    public string? ChuveiroCritico { get; set; }

    /// <summary>Todos os bicos do setor, não só os da área.</summary>
    public List<BicoGravado> Chuveiros { get; set; } = [];

    public List<long> Tubos { get; set; } = [];

    /// <summary>O que o cálculo levou em conta (<see cref="Servicos.AssinaturaDaRede" />). Nula num resultado de versão anterior.</summary>
    public string? Assinatura { get; set; }

    public IEnumerable<BicoGravado> DaArea => Chuveiros.Where(c => c.NaArea);

    public void Salvar(Document doc) => ArmazenamentoSpk.Gravar(doc, Chave, this);

    public static ResultadoGravado? Ler(Document doc) => ArmazenamentoSpk.Ler<ResultadoGravado>(doc, Chave);
}

public sealed class BicoGravado
{
    /// <summary>Número de cálculo, o mesmo do memorial e das folhas.</summary>
    public string Id { get; set; } = "";
    public long Elemento { get; set; }
    public string? Ramal { get; set; }
    public bool NaArea { get; set; }
    public double PressaoKPa { get; set; }
    public double VazaoLmin { get; set; }
}
