using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Normas;

/// <summary>Valores numéricos da ABNT NBR 5410:2004 (versão corrigida 2008) usados pelo motor. Cada um cita o item de origem.</summary>
public static class Nbr5410
{
    public const string Documento = "ABNT NBR 5410:2004";

    // ---------- Queda de tensão (6.2.7) ----------

    /// <summary>6.2.7.1 c): queda total a partir do ponto de entrega em baixa tensão.</summary>
    public const double QuedaTotalMaxima = 5.0;

    /// <summary>6.2.7.1 a), b), d): a partir de transformador próprio, do trafo da distribuidora ou de gerador.</summary>
    public const double QuedaTotalMaximaComTrafoProprio = 7.0;

    /// <summary>6.2.7.2: nenhum circuito terminal pode ter mais de 4 %.</summary>
    public const double QuedaTerminalMaxima = 4.0;

    // ---------- Sobrecarga (5.3.4) ----------

    /// <summary>5.3.4.1: I2 ≤ 1,45 IZ.</summary>
    public const double FatorI2 = 1.45;

    // ---------- Eletrodutos (6.2.11.1) ----------

    /// <summary>6.2.11.1.6 a): taxa de ocupação máxima pelo número de condutores no eletroduto.</summary>
    public static double TaxaDeOcupacaoMaxima(int condutores) => condutores switch
    {
        <= 0 => throw new ArgumentOutOfRangeException(nameof(condutores)),
        1 => 0.53,
        2 => 0.31,
        _ => 0.40,
    };

    /// <summary>6.2.11.1.6 b): trecho contínuo sem caixa, em m, interno e externo.</summary>
    public const double TrechoMaximoInterno = 15;
    public const double TrechoMaximoExterno = 30;

    /// <summary>6.2.11.1.6 b): desconto por curva de 90° no trecho contínuo, em m.</summary>
    public const double DescontoPorCurva = 3;

    /// <summary>6.2.11.1.7: no máximo três curvas de 90° (270°) entre caixas.</summary>
    public const int CurvasMaximasEntreCaixas = 3;

    /// <summary>6.2.11.1.5: tamanho nominal mínimo do eletroduto em uso normal, em mm.</summary>
    public const int DnMinimo = 16;

    /// <summary>6.2.11.1.6 b) nota: a cada 6 m (ou fração) de excesso sem caixa, sobe um DN.</summary>
    public const double ExcessoPorDnAMais = 6;

    // ---------- Seccionamento automático (5.1.2.2.4) ----------

    /// <summary>Tabela 25: tempo máximo de seccionamento no esquema TN, em s, pela tensão fase-neutro e situação.</summary>
    public static double TempoMaximoTN(double uo, Situacao situacao)
    {
        var s1 = situacao == Situacao.Normal;
        return uo switch
        {
            <= 127 => s1 ? 0.8 : 0.35,
            <= 277 => s1 ? 0.4 : 0.20,
            _ => s1 ? 0.2 : 0.05,
        };
    }

    /// <summary>5.1.2.2.4.1 c): circuitos de distribuição e terminais que só alimentam equipamento fixo: até 5 s.</summary>
    public const double TempoMaximoDistribuicao = 5.0;

    /// <summary>Anexo C: tensão de contato limite UL, em V.</summary>
    public static double TensaoDeContatoLimite(Situacao situacao) => situacao == Situacao.Normal ? 50 : 25;

    // ---------- Curto-circuito (5.3.5.5) ----------

    /// <summary>Tabela 30: fator k de I²t ≤ k²S² (0,1 s a 5 s).</summary>
    public static double FatorK(MaterialCondutor material, Isolacao isolacao, double secao)
    {
        var pvc = isolacao == Isolacao.Pvc70;
        return (material, pvc, secao > 300) switch
        {
            (MaterialCondutor.Cobre, true, false) => 115,
            (MaterialCondutor.Cobre, true, true) => 103,
            (MaterialCondutor.Cobre, false, _) => 143,
            (MaterialCondutor.Aluminio, true, false) => 76,
            (MaterialCondutor.Aluminio, true, true) => 68,
            _ => 94,
        };
    }

    // ---------- Fatores de correção (6.2.5.3 a 6.2.5.6) ----------

    /// <summary>Tabela 40: fator de temperatura para linhas ao ar (referência 30 °C) por interpolação linear entre os pontos da tabela.</summary>
    public static double FatorDeTemperaturaAr(Isolacao isolacao, double temperatura)
    {
        if (isolacao == Isolacao.Pvc70)
            return Interpolar(temperatura, [10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60], [1.22, 1.17, 1.12, 1.06, 1.00, 0.94, 0.87, 0.79, 0.71, 0.61, 0.50]);
        return Interpolar(temperatura, [10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80],
            [1.15, 1.12, 1.08, 1.04, 1.00, 0.96, 0.91, 0.87, 0.82, 0.76, 0.71, 0.65, 0.58, 0.50, 0.41]);
    }

    /// <summary>Tabela 40: fator de temperatura para linhas enterradas (referência 20 °C no solo).</summary>
    public static double FatorDeTemperaturaSolo(Isolacao isolacao, double temperatura)
    {
        if (isolacao == Isolacao.Pvc70)
            return Interpolar(temperatura, [10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60], [1.10, 1.05, 1.00, 0.95, 0.89, 0.84, 0.77, 0.71, 0.63, 0.55, 0.45]);
        return Interpolar(temperatura, [10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80],
            [1.07, 1.04, 1.00, 0.96, 0.93, 0.89, 0.85, 0.80, 0.76, 0.71, 0.65, 0.60, 0.53, 0.46, 0.38]);
    }

    /// <summary>Tabela 41: resistividade térmica do solo diferente de 2,5 K·m/W.</summary>
    public static double FatorDeResistividadeDoSolo(double kmPorW) =>
        Interpolar(kmPorW, [1, 1.5, 2, 2.5, 3], [1.18, 1.10, 1.05, 1.00, 0.96]);

    /// <summary>
    /// Tabela 42, linha 1: condutores agrupados em feixe (eletroduto, conduto fechado, embutidos, ao ar livre em feixe),
    /// pelo número de circuitos ou cabos multipolares. Vale para os métodos A a F.
    /// </summary>
    public static double FatorDeAgrupamentoEmFeixe(int circuitos) => circuitos switch
    {
        <= 1 => 1.00,
        2 => 0.80,
        3 => 0.70,
        4 => 0.65,
        5 => 0.60,
        6 => 0.57,
        7 => 0.54,
        8 => 0.52,
        <= 11 => 0.50,
        <= 15 => 0.45,
        <= 19 => 0.41,
        _ => 0.38,
    };

    /// <summary>Tabela 42, linhas 2 a 5: camada única (método C: parede/piso/bandeja não perfurada e teto; E e F: bandeja perfurada e leito).</summary>
    public static double FatorDeAgrupamentoEmCamada(int circuitos, CamadaUnica onde)
    {
        double[] tabela = onde switch
        {
            CamadaUnica.ParedePisoOuBandejaNaoPerfurada => [1.00, 0.85, 0.79, 0.75, 0.73, 0.72, 0.72, 0.71, 0.70],
            CamadaUnica.Teto => [0.95, 0.81, 0.72, 0.68, 0.66, 0.64, 0.63, 0.62, 0.61],
            CamadaUnica.BandejaPerfurada => [1.00, 0.88, 0.82, 0.77, 0.75, 0.73, 0.73, 0.72, 0.72],
            _ => [1.00, 0.87, 0.82, 0.80, 0.80, 0.79, 0.79, 0.78, 0.78],
        };
        var i = Math.Max(1, Math.Min(circuitos, 9)) - 1;
        return tabela[i];
    }

    public enum CamadaUnica { ParedePisoOuBandejaNaoPerfurada, Teto, BandejaPerfurada, LeitoOuSuporte }

    /// <summary>6.2.5.5.5: condutores de seções dessemelhantes (mais de três seções sucessivas) no mesmo grupo: F = 1/√n.</summary>
    public static double FatorDeAgrupamentoDessemelhante(int circuitos) => circuitos <= 1 ? 1.0 : 1.0 / Math.Sqrt(circuitos);

    /// <summary>
    /// Tabela 46: número de condutores carregados do circuito. Trifásico com neutro e terceira harmônica acima de 15 %
    /// conta 4 (coluna de 3 com o fator de 0,86, 6.2.5.6.1 nota).
    /// </summary>
    public static int CondutoresCarregados(Ligacao ligacao, bool harmonicasAltas = false) => ligacao switch
    {
        Ligacao.FN or Ligacao.FF => 2,
        Ligacao.FFN or Ligacao.FFF => 3,
        Ligacao.FFFN => harmonicasAltas ? 4 : 3,
        _ => throw new ArgumentOutOfRangeException(nameof(ligacao)),
    };

    /// <summary>6.2.5.6.1 nota: fator aplicado à coluna de três condutores quando o neutro conduz harmônicas (quatro carregados).</summary>
    public const double FatorNeutroCarregado = 0.86;

    // ---------- Seções mínimas e neutro e PE (6.2.6, 6.4.3) ----------

    /// <summary>Tabela 47: seção mínima de fase em cobre por utilização do circuito.</summary>
    public static double SecaoMinima(TipoCarga tipo, MaterialCondutor material)
    {
        if (material == MaterialCondutor.Aluminio) return 16;
        return tipo == TipoCarga.Iluminacao ? 1.5 : 2.5;
    }

    /// <summary>
    /// 6.2.6.2: neutro igual à fase em monofásico, bifásico e trifásico até 25 mm²; acima disso, em trifásico equilibrado sem
    /// harmônicas e com neutro protegido, pode ser a seção reduzida da Tabela 48.
    /// </summary>
    public static double SecaoDoNeutro(double fase, Ligacao ligacao, bool harmonicasAltas, bool permitirReduzido = false)
    {
        if (ligacao != Ligacao.FFFN || fase <= 25 || harmonicasAltas || !permitirReduzido) return fase;
        return fase switch
        {
            35 => 25, 50 => 25, 70 => 35, 95 => 50, 120 => 70, 150 => 70, 185 => 95, 240 => 120, 300 => 150, 400 => 185,
            _ => fase,
        };
    }

    /// <summary>Tabela 58: seção mínima do condutor de proteção (mesmo metal da fase).</summary>
    public static double SecaoDoPE(double fase)
    {
        if (fase <= 16) return fase;
        if (fase <= 35) return 16;
        var metade = fase / 2;
        foreach (var s in Nbr5410Capacidades.Secoes)
            if (s >= metade - 1e-9) return s; // S/2 arredondado à seção padronizada seguinte (120 → 70, 500 → 300)
        return metade;
    }

    /// <summary>6.4.3.1.4: PE fora do conduto das fases: 2,5 mm² com proteção mecânica, 4 mm² sem.</summary>
    public const double PEForaDoCondutoProtegido = 2.5;
    public const double PEForaDoCondutoSemProtecao = 4;

    // ---------- Quadros (6.5.4) ----------

    /// <summary>Tabela 59: espaço de reserva em número de circuitos.</summary>
    public static int ReservaDoQuadro(int circuitos) => circuitos switch
    {
        <= 6 => 2,
        <= 12 => 3,
        <= 30 => 4,
        _ => (int)Math.Ceiling(0.15 * circuitos),
    };

    /// <summary>6.5.4.10: advertência obrigatória nos quadros residenciais.</summary>
    public const string AdvertenciaDoQuadro =
        "ADVERTÊNCIA — 1. Quando um disjuntor ou fusível atua, desligando algum circuito ou a instalação inteira, a causa pode ser " +
        "uma sobrecarga ou um curto-circuito. Desligamentos frequentes são sinal de sobrecarga. Por isso, NUNCA troque seus disjuntores " +
        "ou fusíveis por outros de maior corrente (maior amperagem) simplesmente. Como regra, a troca de um disjuntor ou fusível por " +
        "outro de maior corrente requer, antes, a troca dos fios e cabos elétricos, por outros de maior seção (bitola). " +
        "2. Da mesma forma, NUNCA desative ou remova a chave automática de proteção contra choques elétricos (dispositivo DR), mesmo " +
        "em caso de desligamentos sem causa aparente. Se os desligamentos forem frequentes e, principalmente, se as tentativas de " +
        "religar a chave não tiverem êxito, isso significa, muito provavelmente, que a instalação elétrica apresenta anomalias internas, " +
        "que só podem ser identificadas e corrigidas por profissionais qualificados. A DESATIVAÇÃO OU REMOÇÃO DA CHAVE SIGNIFICA A " +
        "ELIMINAÇÃO DE MEDIDA PROTETORA CONTRA CHOQUES ELÉTRICOS E RISCO DE VIDA PARA OS USUÁRIOS DA INSTALAÇÃO.";

    // ---------- Habitação (9.5) ----------

    /// <summary>9.5.2.1.2: carga mínima de iluminação em VA pela área do cômodo.</summary>
    public static double CargaMinimaDeIluminacao(double area)
    {
        if (area <= 0) return 0;
        if (area <= 6) return 100;
        return 100 + 60 * Math.Floor((area - 6) / 4);
    }

    /// <summary>9.5.2.2.1: número mínimo de pontos de tomada do cômodo pelo uso, área e perímetro.</summary>
    public static int PontosDeTomadaMinimos(UsoAmbiente uso, double area, double perimetro)
    {
        switch (uso)
        {
            case UsoAmbiente.Banheiro:
            case UsoAmbiente.Lavabo:
            case UsoAmbiente.Varanda:
                return 1;
            case UsoAmbiente.Cozinha:
            case UsoAmbiente.Copa:
            case UsoAmbiente.AreaServico:
            case UsoAmbiente.Lavanderia:
                return Math.Max(1, (int)Math.Ceiling(perimetro / 3.5 - 1e-9));
            case UsoAmbiente.Sala:
            case UsoAmbiente.Dormitorio:
                return Math.Max(1, (int)Math.Ceiling(perimetro / 5 - 1e-9));
            default:
                if (area <= 6) return 1;
                return Math.Max(1, (int)Math.Ceiling(perimetro / 5 - 1e-9));
        }
    }

    /// <summary>9.5.2.2.1: acima da bancada da pia, pelo menos duas tomadas.</summary>
    public const int TomadasSobreABancada = 2;

    /// <summary>9.5.2.2.2: potência mínima por ponto de tomada em VA.</summary>
    public static IEnumerable<double> PotenciasMinimasDasTomadas(UsoAmbiente uso, int pontos, bool conjuntoMolhadoComMaisDeSeis = false)
    {
        var molhado = uso is UsoAmbiente.Banheiro or UsoAmbiente.Cozinha or UsoAmbiente.Copa or UsoAmbiente.AreaServico or UsoAmbiente.Lavanderia;
        var de600 = !molhado ? 0 : conjuntoMolhadoComMaisDeSeis ? 2 : 3;
        for (var i = 0; i < pontos; i++) yield return i < de600 ? 600 : 100;
    }

    /// <summary>9.5.3.1: equipamento com corrente nominal acima disto pede circuito independente, em A.</summary>
    public const double CorrenteParaCircuitoProprio = 10;

    /// <summary>9.5.3.3: circuito comum de iluminação e tomadas só com IB até isto, em A.</summary>
    public const double CorrenteMaximaCircuitoComum = 16;

    /// <summary>9.1.2.1: dimensões dos volumes do banheiro, em m: volume 1 até 2,25 m sobre o boxe/banheira; volume 2 a 0,60 m ao redor até 3,00 m; volume 3 a 2,40 m além do volume 2 até 2,25 m.</summary>
    public const double AlturaVolume1 = 2.25;
    public const double AlturaVolume2 = 3.00;
    public const double LarguraVolume2 = 0.60;
    public const double LarguraVolume3 = 2.40;
    public const double AlturaVolume3 = 2.25;

    /// <summary>9.1.2.1: sem boxe, o volume 1 vai até 0,60 m ao redor do ponto de saída da água do chuveiro.</summary>
    public const double RaioDoChuveiroSemBoxe = 0.60;

    /// <summary>9.1.4.1: segundo algarismo do IP mínimo por volume do banheiro (IPX7, IPX4, IPX3, IPX1); banheiro público pede IPX5 nos volumes 2 e 3.</summary>
    public static int IpAguaMinimoNoBanheiro(int volume, bool publico = false) => volume switch
    {
        0 => 7,
        1 => 4,
        2 => publico ? 5 : 3,
        3 => publico ? 5 : 1,
        _ => 0,
    };

    /// <summary>5.1.3.2.2 nota: em habitação, luminárias a esta altura ou mais podem ficar fora do DR, em m.</summary>
    public const double AlturaQueDispensaDR = 2.5;

    /// <summary>4.2.1.2.3: TUE a no máximo esta distância do equipamento, em m.</summary>
    public const double DistanciaMaximaTUE = 1.5;

    /// <summary>4.2.1.2.3: halls de serviço, casas de máquinas, salas de bombas e barriletes: circuito de pelo menos 1 000 VA.</summary>
    public const double PotenciaMinimaCasaDeMaquinas = 1000;

    // ---------- DPS (5.4.2, 6.3.5.2) ----------

    /// <summary>4.2.6: AQ2 começa acima de 25 dias de trovoada por ano.</summary>
    public const int DiasDeTrovoadaAQ2 = 25;

    /// <summary>Tabela 49: Uc mínimo do DPS pelo esquema de aterramento e pela conexão.</summary>
    public static double UcMinimoDoDps(EsquemaAterramento esquema, ConexaoDps conexao, double uo, double u)
    {
        return (esquema, conexao) switch
        {
            (_, ConexaoDps.NeutroPE) => uo,
            (EsquemaAterramento.IT, ConexaoDps.FasePE) => Math.Sqrt(3) * uo,
            (EsquemaAterramento.IT, ConexaoDps.FaseFase) => u,
            _ => 1.1 * uo,
        };
    }

    public enum ConexaoDps { FaseNeutro, FasePE, FasePEN, NeutroPE, FaseFase }

    /// <summary>6.3.5.2.3: In mínimo (8/20 µs) e Iimp mínimo (10/350 µs) por DPS, em kA.</summary>
    public const double InMinimoDps = 5;
    public const double IimpMinimoDps = 12.5;

    /// <summary>Tabela 31, categoria II (equipamentos) e IV (entrada): suportabilidade a impulso, em kV, pela tensão fase-neutro.</summary>
    public static double UpMaximoDps(double uo, bool entrada = false) => (uo <= 127, entrada) switch
    {
        (true, false) => 1.5,
        (true, true) => 4.0,
        (false, false) => 2.5,
        _ => 6.0,
    };

    /// <summary>6.3.5.2.9: seção dos condutores de ligação do DPS, em mm² de cobre: 4 no quadro, 16 na entrada com SPDA.</summary>
    public const double SecaoLigacaoDpsQuadro = 4;
    public const double SecaoLigacaoDpsEntrada = 16;

    static double Interpolar(double x, double[] xs, double[] ys)
    {
        if (x <= xs[0]) return ys[0];
        if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
        for (var i = 1; i < xs.Length; i++)
            if (x <= xs[i])
                return ys[i - 1] + (ys[i] - ys[i - 1]) * (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
        return ys[ys.Length - 1];
    }
}
