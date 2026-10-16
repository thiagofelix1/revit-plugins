using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Exemplos;

/// <summary>
/// Rede de exemplo de um VGA em espinha de peixe: VGA no térreo, coluna até o telhado, geral até a subgeral,
/// e ramais dos dois lados da subgeral. Serve para demonstração e testes; não é um projeto real.
/// </summary>
public sealed record GalpaoEspinhaDePeixe
{
    public int Ramais { get; init; } = 20;
    public int ChuveirosPorLado { get; init; } = 8;
    public double EspacamentoChuveiros { get; init; } = 3.0;
    public double EspacamentoRamais { get; init; } = 3.5;
    public double AlturaRede { get; init; } = 9.0;
    public double ComprimentoGeral { get; init; } = 15.0;
    public double FatorK { get; init; } = 80;
    public double C { get; init; } = 120;

    /// <summary>Liga as pontas dos ramais por tubos DN 50, transformando a espinha de peixe em grelha.</summary>
    public bool Grelha { get; init; }

    public Rede Criar()
    {
        var nos = new List<No> { new("VGA", 0, -ComprimentoGeral, 0), new("TOPO", 0, -ComprimentoGeral, AlturaRede) };
        var trechos = new List<Trecho>();
        var chuveiros = new List<Chuveiro>();

        trechos.Add(Tubo("COLUNA", "VGA", "TOPO", 150, AlturaRede,
            [(TipoConexao.ValvulaRetencao, 1), (TipoConexao.ValvulaGaveta, 1), (TipoConexao.Cotovelo90Padrao, 1)]));

        for (var i = 0; i < Ramais; i++)
        {
            var y = i * EspacamentoRamais;
            nos.Add(new No($"SG{i}", 0, y, AlturaRede));
            if (i == 0)
                trechos.Add(Tubo("GERAL", "TOPO", "SG0", 150, ComprimentoGeral, [(TipoConexao.Cotovelo90Padrao, 1)]));
            else
                trechos.Add(Tubo($"SG{i - 1}-{i}", $"SG{i - 1}", $"SG{i}", DnSubgeral(Ramais - i), EspacamentoRamais, []));

            foreach (var (lado, sinal) in new[] { ("E", -1), ("D", 1) })
            {
                var anterior = $"SG{i}";
                for (var k = 0; k < ChuveirosPorLado; k++)
                {
                    var id = $"R{i}{lado}{k}";
                    var x = sinal * (EspacamentoChuveiros / 2 + k * EspacamentoChuveiros);
                    nos.Add(new No(id, x, y, AlturaRede));
                    var dn = DnRamal(ChuveirosPorLado - k);
                    var comprimento = k == 0 ? EspacamentoChuveiros / 2 : EspacamentoChuveiros;
                    trechos.Add(Tubo($"T-{id}", anterior, id, dn, comprimento,
                        k == 0 ? [(TipoConexao.TeOuCruzetaFluxoDesviado, 1)] : []));
                    chuveiros.Add(new Chuveiro($"B-{id}", id, FatorK, EspacamentoChuveiros * EspacamentoRamais) { Ramal = $"R{i}" });
                    anterior = id;
                }

                if (Grelha && i > 0)
                {
                    var ponta = $"R{i}{lado}{ChuveirosPorLado - 1}";
                    var pontaAnterior = $"R{i - 1}{lado}{ChuveirosPorLado - 1}";
                    trechos.Add(Tubo($"G-{pontaAnterior}-{ponta}", pontaAnterior, ponta, 50, EspacamentoRamais, []));
                }
            }
        }

        return new Rede("VGA", nos, trechos, chuveiros);
    }

    Trecho Tubo(string id, string de, string para, int dn, double comprimento, (TipoConexao, int)[] conexoes)
    {
        var di = TubosAco.Schedule40(dn);
        return new Trecho(id, de, para, di, comprimento, C)
        {
            DiametroNominal = dn,
            ComprimentoEquivalente = ComprimentosEquivalentes.Somar(conexoes, dn, C, di),
            Conexoes = string.Join(", ", conexoes.Select(c => $"{c.Item2}× {c.Item1}")),
        };
    }

    static int DnRamal(int chuveirosAJusante) => chuveirosAJusante switch
    {
        <= 2 => 25,
        3 => 32,
        <= 5 => 40,
        _ => 50,
    };

    static int DnSubgeral(int ramaisAJusante) => ramaisAJusante switch
    {
        <= 2 => 65,
        <= 5 => 80,
        _ => 100,
    };
}
