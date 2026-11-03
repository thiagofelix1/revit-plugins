namespace Spk.Motor.Modelo;

/// <summary>Ponto em planta, em metros, nas coordenadas do projeto de incêndio.</summary>
public readonly record struct Ponto2(double X, double Y);

/// <summary>De onde veio a altura do teto de um ambiente.</summary>
public enum OrigemDoTeto { Forro, Laje, Telhado, AlturaDoAmbiente, NaoAchado }

/// <summary>
/// Um ambiente da arquitetura: contorno em planta (o primeiro é o externo, os outros são vazios), cota do piso e do
/// teto (forro, laje ou telhado acima do ambiente), em metros.
/// </summary>
public sealed record Ambiente(
    string Id,
    string Nome,
    string Numero,
    string Nivel,
    string Origem,
    IReadOnlyList<IReadOnlyList<Ponto2>> Contornos,
    double Area,
    double Piso,
    double? Teto,
    OrigemDoTeto OrigemDoTeto)
{
    /// <summary>Pé-direito até o teto, em metros.</summary>
    public double? PeDireito => Teto - Piso;

    /// <summary>Inclinação do teto medida em volta do ponto do ambiente, como fração (0,20 = 20 %). Nula: não deu para medir.</summary>
    public double? Inclinacao { get; init; }
}

/// <summary>Uma viga: eixo em planta, largura e as cotas do fundo e do topo, em metros.</summary>
public sealed record Viga(string Id, string Origem, Ponto2 A, Ponto2 B, double Largura, double Fundo, double Topo)
{
    public double Altura => Topo - Fundo;
    public double Comprimento => Math.Sqrt((B.X - A.X) * (B.X - A.X) + (B.Y - A.Y) * (B.Y - A.Y));
}

/// <summary>O que o SPK leu da arquitetura e da estrutura (do arquivo e dos vínculos).</summary>
public sealed record Edificio(IReadOnlyList<Ambiente> Ambientes, IReadOnlyList<Viga> Vigas, IReadOnlyList<string> Fontes, IReadOnlyList<string> Avisos);
