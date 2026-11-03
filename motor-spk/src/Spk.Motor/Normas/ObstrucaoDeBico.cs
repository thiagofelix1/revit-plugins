using Spk.Motor.Modelo;

namespace Spk.Motor.Normas;

/// <summary>Um bico para conferir obstrução: o ponto em planta e a cota do defletor, em metros.</summary>
public sealed record BicoNoEspaco(string Id, Ponto2 Ponto, double Defletor);

/// <summary>Bico fora da Tabela 15. Em metros: a distância à lateral da viga, o defletor acima do fundo dela e o que a tabela permite.</summary>
public sealed record ObstrucaoAcusada(string Bico, Ponto2 Ponto, Viga Viga, double Distancia, double AcimaDoFundo, double Permitido)
{
    /// <summary>
    /// Peça com mais de 1 m de altura: no modelo ela é um sólido, mas costuma ser treliça ou tesoura, que tem regra própria
    /// (NBR 7.10.1.2.5 e 7.10.1.2.6). O SPK não sabe as barras dela, então avisa em vez de reprovar.
    /// </summary>
    public bool PareceTrelica => Viga.Altura > ObstrucaoDeBico.AlturaDeTrelica;

    public string Descricao => $"a {Distancia:0.00} m da viga, defletor {AcimaDoFundo:0.00} m acima do fundo dela (máximo {Permitido:0.00} m, Tabela 15)";
}

/// <summary>
/// Obstrução à descarga por viga, para bico spray em pé e pendente de cobertura-padrão (NBR 10897 7.10.1.1, Tabela 15, Figura 26).
/// Só viga cortando a altura do defletor: viga inteira abaixo do bico é outra regra (7.10.1.2, Tabela 16), ainda sem conferência.
/// </summary>
public static class ObstrucaoDeBico
{
    public const string Item = "NBR 10897 7.10.1.1, Tabela 15";
    public const double AlturaDeTrelica = 1.0;
    const double LarguraMaximaEntreBicos = 1.2;

    // Limite superior da faixa de distância (A) e a altura máxima do defletor acima do fundo da obstrução (B), em mm.
    static readonly (double Ate, double Permitido)[] Tabela15 =
    [
        (300, 0), (450, 65), (600, 90), (750, 140), (900, 190), (1050, 240), (1200, 305), (1350, 355),
        (1500, 420), (1650, 460), (1800, 510), (1950, 610), (2100, 760), (2250, 890),
    ];

    /// <summary>Quanto o defletor pode ficar acima do fundo da obstrução, em m. Nulo: a 2,25 m ou mais, a tabela não limita.</summary>
    public static double? AlturaPermitida(double distancia)
    {
        foreach (var (ate, permitido) in Tabela15)
            if (distancia * 1000 < ate) return permitido / 1000;
        return null;
    }

    /// <param name="distanciaMaxima">Distância máxima entre bicos do risco (Tabela 10), para a exceção de 7.10.1.1.2.</param>
    public static IReadOnlyList<ObstrucaoAcusada> Conferir(IReadOnlyList<BicoNoEspaco> bicos, IReadOnlyList<Viga> vigas, double distanciaMaxima)
    {
        var acusadas = new List<ObstrucaoAcusada>();
        foreach (var bico in bicos)
        {
            ObstrucaoAcusada? pior = null;
            foreach (var viga in vigas)
            {
                if (bico.Defletor <= viga.Fundo || bico.Defletor > viga.Topo || viga.Comprimento < 1e-6) continue;
                var (ao, lado) = Posicao(bico.Ponto, viga);
                var fora = ao < 0 ? -ao : Math.Max(0, ao - viga.Comprimento);
                var distancia = Math.Sqrt(Math.Pow(Math.Max(0, Math.Abs(lado) - viga.Largura / 2), 2) + fora * fora);
                if (AlturaPermitida(distancia) is not { } permitido) continue;
                var acima = bico.Defletor - viga.Fundo;
                if (acima <= permitido + 1e-6 || BicosDosDoisLados(bico, viga, ao, lado, bicos, distanciaMaxima)) continue;
                if (pior is null || acima - permitido > pior.AcimaDoFundo - pior.Permitido)
                    pior = new ObstrucaoAcusada(bico.Id, bico.Ponto, viga, distancia, acima, permitido);
            }
            if (pior is not null) acusadas.Add(pior);
        }
        return acusadas;
    }

    /// <summary>7.10.1.1.2: obstrução com menos de 1,2 m de largura e bicos dos dois lados, a até meia distância máxima do eixo dela.</summary>
    static bool BicosDosDoisLados(BicoNoEspaco bico, Viga viga, double ao, double lado, IReadOnlyList<BicoNoEspaco> bicos, double distanciaMaxima)
    {
        if (viga.Largura >= LarguraMaximaEntreBicos || Math.Abs(lado) > distanciaMaxima / 2) return false;
        return bicos.Any(outro =>
        {
            if (outro.Id == bico.Id) return false;
            var (aoOutro, ladoOutro) = Posicao(outro.Ponto, viga);
            return ladoOutro * lado < 0 && Math.Abs(ladoOutro) <= distanciaMaxima / 2 && Math.Abs(aoOutro - ao) <= distanciaMaxima;
        });
    }

    /// <summary>Onde o ponto cai no eixo da viga, a partir de A, e o afastamento lateral com sinal.</summary>
    static (double Ao, double Lado) Posicao(Ponto2 p, Viga viga)
    {
        var (dx, dy) = ((viga.B.X - viga.A.X) / viga.Comprimento, (viga.B.Y - viga.A.Y) / viga.Comprimento);
        var (px, py) = (p.X - viga.A.X, p.Y - viga.A.Y);
        return (px * dx + py * dy, dx * py - dy * px);
    }
}
