using Spk.Motor.Normas;

namespace Spk.AddIn;

/// <param name="XM">Posição da malha em planta, em metros a partir da origem do projeto.</param>
/// <param name="CotaNivelM">Cota do nível da malha acima do nível térreo, em metros. O nível é criado se não existir.</param>
public sealed record MalhaDoCenario(DadosEsboco Esboco, double XM, double YM, double CotaNivelM = 0);

/// <summary>
/// Um cenário de teste: uma ou mais malhas geradas e os critérios de projeto que fazem sentido para elas. Todas as
/// malhas de um cenário usam o mesmo bico (K e área), porque Calcular lê o bico da malha gerada no esboço gravado.
/// </summary>
public sealed record CenarioExemplo(string Nome, string Descricao, IReadOnlyList<MalhaDoCenario> Malhas, DadosProjeto Criterios)
{
    public int TotalDeBicos => Malhas.Sum(m => m.Esboco.Ramais * m.Esboco.BicosPorRamal * (m.Esboco.Arranjo == Arranjo.EspinhaDePeixe ? 2 : 1));
}

/// <summary>Os cenários do botão Rede de exemplo, do mais simples ao mais complexo.</summary>
public static class CenariosExemplo
{
    static DadosProjeto Esfr() => new()
    {
        Classe = ClasseRisco.Armazenamento,
        Base = TipoBase.Esfr,
        PressaoMinimaKPa = 283,
        ItemNorma = "IT-24 Tabela 5.3.4.3.1",
        VazaoHidrantesLmin = 1900,
        DuracaoMin = 60,
        FatorC = 120,
    };

    static DadosProjeto Figura42(ClasseRisco classe, double area) => new()
    {
        Classe = classe,
        Base = TipoBase.Figura42,
        AreaOperacaoM2 = area,
        FatorC = 120,
    };

    public static CenarioExemplo Simples { get; } = new(
        "Simples — 12 bicos ESFR",
        "3 ramais de 4 bicos ESFR K 360, subgeral lateral, DN 65. O teste de regressão de sempre.",
        [new MalhaDoCenario(new DadosEsboco(), 0, 0)],
        Esfr());

    public static IReadOnlyList<CenarioExemplo> Todos { get; } =
    [
        Simples,
        new(
            "Depósito — espinha de peixe, 2 setores",
            "Dois setores ESFR K 360 a 9 m de altura, cada um com subgeral central DN 150 e 8 ramais DN 65 de 5 bicos para cada " +
            "lado (80 bicos por setor), separados entre si. Testa busca de área em rede grande, setorização e mapa-chave.",
            [
                new MalhaDoCenario(Esboco(Arranjo.EspinhaDePeixe, ramais: 8, bicos: 5, sx: 3.0, sy: 3.0, altura: 9, k: 360, area: 9, dn: 65, dnSub: 150), 0, 0),
                new MalhaDoCenario(Esboco(Arranjo.EspinhaDePeixe, ramais: 8, bicos: 5, sx: 3.0, sy: 3.0, altura: 9, k: 360, area: 9, dn: 65, dnSub: 150), 50, 0),
            ],
            Esfr()),
        new(
            "Galpão — grelha, ordinário grupo 2",
            "Grelha com subgeral DN 80 nas duas pontas e 7 ramais DN 40 de 8 bicos K 80 (56 bicos), espaçamento 3,4 m. " +
            "A rede fecha em anéis: testa o solver em malha fechada e a base pela Figura 42.",
            [new MalhaDoCenario(Esboco(Arranjo.Grelha, ramais: 7, bicos: 8, sx: 3.4, sy: 3.4, altura: 4.5, k: 80, area: 12, dn: 40, dnSub: 80), 0, 0)],
            Figura42(ClasseRisco.Ordinario2, 140)),
        new(
            "Edifício de 2 pavimentos — ordinário grupo 1",
            "Lojas no térreo e escritórios no pavimento de cima (+4,00 m), cada pavimento com 5 ramais DN 40 de 6 bicos K 80 e " +
            "subgeral lateral DN 65, cada um com a sua descida. Testa setores em níveis diferentes e a setorização por pavimento.",
            [
                new MalhaDoCenario(Esboco(Arranjo.Lateral, ramais: 5, bicos: 6, sx: 3.4, sy: 3.4, altura: 3.0, k: 80, area: 12, dn: 40, dnSub: 65), 0, 0),
                new MalhaDoCenario(Esboco(Arranjo.Lateral, ramais: 5, bicos: 6, sx: 3.4, sy: 3.4, altura: 3.0, k: 80, area: 12, dn: 40, dnSub: 65), 0, 0, 4.0),
            ],
            Figura42(ClasseRisco.Ordinario1, 140)),
        new(
            "Residência grande / escritório — risco leve",
            "6 ramais DN 32 de 5 bicos K 80, espaçamento 4,0 m (16 m² por bico), subgeral lateral DN 50, forro a 2,8 m. " +
            "Testa risco leve, com área de operação de 140 m² pela Figura 42.",
            [new MalhaDoCenario(Esboco(Arranjo.Lateral, ramais: 6, bicos: 5, sx: 4.0, sy: 4.0, altura: 2.8, k: 80, area: 16, dn: 32, dnSub: 50), 0, 0)],
            Figura42(ClasseRisco.Leve, 140)),
    ];

    public static CenarioExemplo? PorNome(string nome) =>
        Todos.FirstOrDefault(c => c.Nome.StartsWith(nome, StringComparison.OrdinalIgnoreCase)
                                  || c.Nome.Contains(nome, StringComparison.OrdinalIgnoreCase));

    static DadosEsboco Esboco(Arranjo arranjo, int ramais, int bicos, double sx, double sy, double altura, double k, double area,
        int dn, int dnSub) => new()
    {
        Arranjo = arranjo,
        Ramais = ramais,
        BicosPorRamal = bicos,
        EspacamentoRamalM = sx,
        EspacamentoEntreRamaisM = sy,
        AlturaRedeM = altura,
        DropM = 0.3,
        FatorK = k,
        AreaM2 = area,
        DnTubo = dn,
        DnSubgeral = dnSub,
    };
}
