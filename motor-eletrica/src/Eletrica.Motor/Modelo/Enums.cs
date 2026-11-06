namespace Eletrica.Motor.Modelo;

/// <summary>Esquema de aterramento (NBR 5410 4.2.2.2). Define o PE, o DR e a condição de seccionamento.</summary>
public enum EsquemaAterramento { TNS, TNC, TNCS, TT, IT }

/// <summary>Condutores vivos do circuito (NBR 5410 4.2.2.1). F = fase, N = neutro. O PE existe sempre.</summary>
public enum Ligacao { FN, FF, FFN, FFF, FFFN }

/// <summary>Tipo de carga do ponto ou do circuito: decide a curva do disjuntor, o DR, o fator de demanda e as regras de 9.5.</summary>
public enum TipoCarga
{
    Iluminacao,
    TomadaUsoGeral,
    TomadaUsoEspecifico,
    Chuveiro,
    ArCondicionado,
    Motor,
    Aquecimento,
    VeiculoEletrico,
    Alimentador,
    Outro,
}

/// <summary>Isolação do condutor: coluna das Tabelas 36 a 39 e k da Tabela 30 (NBR 5410).</summary>
public enum Isolacao { Pvc70, Epr90, Xlpe90 }

public enum MaterialCondutor { Cobre, Aluminio }

/// <summary>
/// Métodos de referência da Tabela 33 (NBR 5410 6.2.5.1.2). F e G têm duas colunas nas Tabelas 38 e 39: F é unipolares
/// justapostos (dois) ou em trifólio (três); FPlano é três no mesmo plano justapostos; G é espaçados na horizontal;
/// GVertical espaçados na vertical.
/// </summary>
public enum MetodoReferencia { A1, A2, B1, B2, C, D, E, F, FPlano, G, GVertical }

/// <summary>Curva de atuação instantânea (NBR NM 60898 4.5 e Tabela 2).</summary>
public enum CurvaDisjuntor { B, C, D }

/// <summary>Tipo do DR pela forma de onda da corrente residual (IEC 60755, 61008, 61009, 62423).</summary>
public enum TipoDr { AC, A, F, B }

/// <summary>Uso do ambiente: decide pontos mínimos (9.5.2), DR (5.1.3.2.2) e influências externas.</summary>
public enum UsoAmbiente
{
    Sala,
    Dormitorio,
    Cozinha,
    Copa,
    AreaServico,
    Lavanderia,
    Banheiro,
    Lavabo,
    Varanda,
    Circulacao,
    Garagem,
    AreaExterna,
    Deposito,
    Escritorio,
    CasaDeMaquinas,
    Outro,
}

/// <summary>Tipo de eletroduto (NBR 15465 e aço).</summary>
public enum TipoEletroduto { RigidoRoscavel, RigidoSoldavel, FlexivelCorrugado, FlexivelPlano, Aco, Eletrocalha }

/// <summary>Posição do dispositivo e da linha: situação 1 (normal) ou 2 (molhada) do Anexo C da NBR 5410.</summary>
public enum Situacao { Normal = 1, Molhada = 2 }
