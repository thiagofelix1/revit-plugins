using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Padrao;

/// <summary>Papel de uma família do escritório no lançamento (item 1.3 do plano). O plugin lança e lê pelo papel, não pelo nome.</summary>
public enum PapelFamilia
{
    Ignorar,
    Tomada10A,
    Tomada20A,
    TomadaDupla,
    Luminaria,
    Arandela,
    Interruptor1,
    Interruptor2,
    Interruptor3,
    InterruptorParalelo,
    InterruptorIntermediario,
    Chuveiro,
    ArCondicionado,
    PontoDeForca,
    Quadro,
    CaixaDePassagem,
}

/// <summary>
///     Padrão do escritório gravado no projeto: alturas, potências e limites do lançamento (do motor) e as famílias que
///     fazem cada papel ("Família : Tipo", por nome, porque o engenheiro pode recarregar a família sem perder a escolha).
/// </summary>
public sealed class PadraoEletrica
{
    public const string Chave = "Padrao";

    public PadraoDeLancamento Lancamento { get; set; } = new();

    /// <summary>Papel → "Família : Tipo".</summary>
    public Dictionary<string, string> Familias { get; set; } = new();

    /// <summary>Afastamento do centro do ponto à face da parede quando a família não é hospedada, em m.</summary>
    public double AfastamentoDaParede { get; set; } = 0.03;

    public static PadraoEletrica Carregar(Document doc) => ArmazenamentoEletrica.Ler<PadraoEletrica>(doc, Chave) ?? new PadraoEletrica();

    /// <summary>Precisa de transação aberta.</summary>
    public void Salvar(Document doc) => ArmazenamentoEletrica.Gravar(doc, Chave, this);

    public void SalvarComTransacao(Document doc) => ArmazenamentoEletrica.GravarComTransacao(doc, Chave, this, "AutoEletrica — Padrão do escritório");

    public static string Nome(FamilySymbol simbolo) => $"{simbolo.FamilyName} : {simbolo.Name}";

    public string? NomeDe(PapelFamilia papel) => Familias.TryGetValue(papel.ToString(), out var n) && !string.IsNullOrWhiteSpace(n) ? n : null;

    /// <summary>O tipo de família confirmado para o papel, ou nulo se não há ou não está mais carregado.</summary>
    public FamilySymbol? Simbolo(Document doc, PapelFamilia papel)
    {
        var nome = NomeDe(papel);
        if (nome is null) return null;
        return new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault(s => Nome(s) == nome);
    }

    /// <summary>Papel confirmado de um tipo de família, ou nulo.</summary>
    public PapelFamilia? PapelDe(FamilySymbol simbolo)
    {
        var nome = Nome(simbolo);
        foreach (var (papel, n) in Familias)
            if (n == nome && Enum.TryParse<PapelFamilia>(papel, out var p) && p != PapelFamilia.Ignorar) return p;
        return null;
    }

    public static string NomeDoPapel(PapelFamilia papel) => papel switch
    {
        PapelFamilia.Ignorar => "(não usar)",
        PapelFamilia.Tomada10A => "Tomada 10 A",
        PapelFamilia.Tomada20A => "Tomada 20 A",
        PapelFamilia.TomadaDupla => "Tomada dupla",
        PapelFamilia.Luminaria => "Luminária (teto)",
        PapelFamilia.Arandela => "Arandela (parede)",
        PapelFamilia.Interruptor1 => "Interruptor 1 tecla",
        PapelFamilia.Interruptor2 => "Interruptor 2 teclas",
        PapelFamilia.Interruptor3 => "Interruptor 3 teclas",
        PapelFamilia.InterruptorParalelo => "Interruptor paralelo",
        PapelFamilia.InterruptorIntermediario => "Interruptor intermediário",
        PapelFamilia.Chuveiro => "Chuveiro / aquecedor",
        PapelFamilia.ArCondicionado => "Ar-condicionado",
        PapelFamilia.PontoDeForca => "Ponto de força (TUE direta)",
        PapelFamilia.Quadro => "Quadro de distribuição",
        PapelFamilia.CaixaDePassagem => "Caixa de passagem",
        _ => papel.ToString(),
    };

    /// <summary>Tipo de carga do motor para um papel; nulo para interruptores, quadros e caixas.</summary>
    public static TipoCarga? TipoDeCarga(PapelFamilia papel) => papel switch
    {
        PapelFamilia.Tomada10A or PapelFamilia.TomadaDupla => Motor.Modelo.TipoCarga.TomadaUsoGeral,
        PapelFamilia.Tomada20A or PapelFamilia.PontoDeForca => Motor.Modelo.TipoCarga.TomadaUsoEspecifico,
        PapelFamilia.Luminaria or PapelFamilia.Arandela => Motor.Modelo.TipoCarga.Iluminacao,
        PapelFamilia.Chuveiro => Motor.Modelo.TipoCarga.Chuveiro,
        PapelFamilia.ArCondicionado => Motor.Modelo.TipoCarga.ArCondicionado,
        _ => null,
    };

    public static bool EhInterruptor(PapelFamilia papel) => papel is PapelFamilia.Interruptor1 or PapelFamilia.Interruptor2 or PapelFamilia.Interruptor3
        or PapelFamilia.InterruptorParalelo or PapelFamilia.InterruptorIntermediario;

    /// <summary>Posição padrão do ponto pelo papel.</summary>
    public static PosicaoDoPonto PosicaoDe(PapelFamilia papel) => papel switch
    {
        PapelFamilia.Luminaria => PosicaoDoPonto.Teto,
        PapelFamilia.Arandela or PapelFamilia.Chuveiro or PapelFamilia.ArCondicionado => PosicaoDoPonto.Alta,
        PapelFamilia.Quadro => PosicaoDoPonto.Media,
        PapelFamilia.CaixaDePassagem => PosicaoDoPonto.Teto,
        _ when EhInterruptor(papel) => PosicaoDoPonto.Interruptor,
        _ => PosicaoDoPonto.Baixa,
    };

    /// <summary>Potência padrão do ponto pelo papel.</summary>
    public double PotenciaDe(PapelFamilia papel, UsoAmbiente? uso) => papel switch
    {
        PapelFamilia.Tomada10A or PapelFamilia.TomadaDupla => uso is UsoAmbiente.Cozinha or UsoAmbiente.Copa or UsoAmbiente.AreaServico or UsoAmbiente.Lavanderia or UsoAmbiente.Banheiro
            ? Lancamento.PotenciaTugMolhada : Lancamento.PotenciaTug,
        PapelFamilia.Tomada20A or PapelFamilia.PontoDeForca => Lancamento.PotenciaTue20A,
        PapelFamilia.Luminaria or PapelFamilia.Arandela => 100,
        PapelFamilia.Chuveiro => Lancamento.PotenciaChuveiro,
        PapelFamilia.ArCondicionado => Lancamento.PotenciaArCondicionado,
        _ => 0,
    };

    public static string NomeDaPosicao(PosicaoDoPonto p) => p switch
    {
        PosicaoDoPonto.Baixa => "Baixa",
        PosicaoDoPonto.Media => "Média",
        PosicaoDoPonto.Alta => "Alta",
        PosicaoDoPonto.Interruptor => "Interruptor",
        PosicaoDoPonto.Teto => "Teto",
        _ => "Piso",
    };

    public string Resumo()
    {
        var l = Lancamento;
        var familias = Familias.Count(f => !string.IsNullOrWhiteSpace(f.Value) && f.Key != nameof(PapelFamilia.Ignorar));
        return $"Alturas: baixa {l.AlturaBaixa:0.00} m, média {l.AlturaMedia:0.00} m, alta {l.AlturaAlta:0.00} m, interruptor {l.AlturaInterruptor:0.00} m, pé-direito {l.PeDireito:0.00} m. " +
               $"TUG {l.PotenciaTug:0} / {l.PotenciaTugMolhada:0} VA, chuveiro {l.PotenciaChuveiro:0} VA, ar {l.PotenciaArCondicionado:0} VA. " +
               $"Circuitos até {l.CorrenteMaximaTug:0} A e {l.PontosMaximosPorCircuito} pontos; prefixos {l.PrefixoCircuito} e {l.PrefixoQuadro}. {familias} papel(is) de família confirmados.";
    }
}
