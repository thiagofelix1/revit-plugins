using Spk.Motor.Calculo;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.AddIn;

public enum TipoBase { Figura42, DensidadeArea, Esfr }

/// <summary>
///     Critérios de projeto escolhidos pelo engenheiro na janela "Dados do projeto". O plugin não deduz a classe
///     de risco nem a demanda de hidrantes: sem estes dados confirmados, o cálculo não roda.
/// </summary>
public sealed class DadosProjeto
{
    public const string Chave = "DadosProjeto";

    public ClasseRisco Classe { get; set; } = ClasseRisco.Ordinario2;
    public TipoBase Base { get; set; } = TipoBase.Figura42;

    /// <summary>Área de operação escolhida na curva (Figura 42) ou da base densidade × área, em m².</summary>
    public double AreaOperacaoM2 { get; set; } = 140;

    /// <summary>Só na base densidade × área, em L/min/m².</summary>
    public double DensidadeLminM2 { get; set; }

    /// <summary>Na base ESFR, a pressão mínima da tabela da IT para a mercadoria, a altura e o K, em kPa.</summary>
    public double PressaoMinimaKPa { get; set; }

    /// <summary>Item da norma ou da IT que justifica a base (vai para o memorial).</summary>
    public string ItemNorma { get; set; } = "";

    /// <summary>Nulos: Tabela 29 da NBR. Obrigatórios em armazenamento.</summary>
    public double? VazaoHidrantesLmin { get; set; }
    public double? DuracaoMin { get; set; }

    public double FatorC { get; set; } = 120;

    /// <summary>Inclinação do teto, em %. Acima de 16,7 %, a área da Figura 42 sobe 30 % (NBR 8.5.5.4). O engenheiro informa; o cálculo avisa se o telhado medido no modelo não bater.</summary>
    public double InclinacaoTetoPct { get; set; }

    /// <summary>Ensaio do abastecimento. Os três juntos ou nenhum; sem eles o cálculo não compara com a fonte.</summary>
    public double? PressaoEstaticaKPa { get; set; }
    public double? PressaoResidualKPa { get; set; }
    public double? VazaoResidualLmin { get; set; }

    public static DadosProjeto? Carregar(Document doc) => ArmazenamentoSpk.Ler<DadosProjeto>(doc, Chave);

    public void Salvar(Document doc) => ArmazenamentoSpk.Gravar(doc, Chave, this);

    public BaseDeProjeto CriarBase() => Base switch
    {
        TipoBase.Esfr => BaseDeProjeto.Esfr(PressaoMinimaKPa, string.IsNullOrWhiteSpace(ItemNorma) ? "IT-24" : ItemNorma),
        TipoBase.DensidadeArea => new BaseDensidadeArea(DensidadeLminM2, AreaOperacaoM2) { Classe = Classe },
        _ => BaseDeProjeto.PelaFigura42(Classe, AreaOperacaoM2, new CondicoesAreaOperacao { InclinacaoTeto = InclinacaoTetoPct / 100 }),
    };

    public CurvaAbastecimento? CriarAbastecimento() =>
        PressaoEstaticaKPa is { } estatica && PressaoResidualKPa is { } residual && VazaoResidualLmin is { } vazao
            ? CurvaAbastecimento.DeEnsaio(estatica, residual, vazao)
            : null;

    public ProjetoSpk CriarProjeto(string nome, Rede rede) => new(nome, rede, Classe, CriarBase())
    {
        VazaoHidrantes = VazaoHidrantesLmin,
        DuracaoMinutos = DuracaoMin,
        Abastecimento = CriarAbastecimento(),
    };

    public string Resumo() => Base switch
    {
        TipoBase.Esfr => $"{NomeClasse(Classe)}; ESFR, pressão mínima {PressaoMinimaKPa:0} kPa ({ItemNorma})",
        TipoBase.DensidadeArea => $"{NomeClasse(Classe)}; {DensidadeLminM2:0.##} L/min/m² sobre {AreaOperacaoM2:0} m²",
        _ => $"{NomeClasse(Classe)}; Figura 42 com {AreaOperacaoM2:0} m²",
    };

    public static string NomeClasse(ClasseRisco c) => c switch
    {
        ClasseRisco.Leve => "Risco leve",
        ClasseRisco.Ordinario1 => "Ordinário grupo 1",
        ClasseRisco.Ordinario2 => "Ordinário grupo 2",
        ClasseRisco.Extraordinario1 => "Extraordinário grupo 1",
        ClasseRisco.Extraordinario2 => "Extraordinário grupo 2",
        _ => "Armazenamento",
    };
}
