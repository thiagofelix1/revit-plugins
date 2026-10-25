namespace Spk.AddIn;

/// <summary>Último cálculo, gravado no modelo. As folhas leem isto para achar a área e o bico crítico.</summary>
public sealed class ResultadoGravado
{
    public const string Chave = "Resultado";

    public bool Atende { get; set; }
    public double PressaoNecessariaKPa { get; set; }
    public double VazaoChuveirosLmin { get; set; }
    public string? ChuveiroCritico { get; set; }
    public int Bicos { get; set; }
    public int Tubos { get; set; }

    /// <summary>Ids do motor de todos os bicos calculados (não só os da área), para saber se a rede mudou.</summary>
    public List<string> BicosDaRede { get; set; } = [];

    public List<BicoGravado> Chuveiros { get; set; } = [];

    public void Salvar(Document doc) => ArmazenamentoSpk.Gravar(doc, Chave, this);

    public static ResultadoGravado? Ler(Document doc) => ArmazenamentoSpk.Ler<ResultadoGravado>(doc, Chave);
}

public sealed class BicoGravado
{
    public string Id { get; set; } = "";
    public string? Ramal { get; set; }
    public double PressaoKPa { get; set; }
    public double VazaoLmin { get; set; }
}
