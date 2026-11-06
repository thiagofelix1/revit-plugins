using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;
using static Eletrica.Motor.Calculo.Eletrotecnica;

namespace Eletrica.Motor.Calculo;

/// <summary>
/// Dimensiona um circuito pelos seis critérios de 6.2.6.1.2 da NBR 5410: (a) capacidade de corrente com os fatores por
/// trecho, (b) sobrecarga IB ≤ In ≤ IZ e I2 ≤ 1,45 IZ, (c) curto-circuito (Icn ≥ Icc e I²t ≤ k²S²), (d) seccionamento
/// automático em TN (Zs·Ia ≤ Uo) ou TT (RA·IΔn ≤ UL), (e) queda de tensão, (f) seção mínima. Devolve o menor condutor
/// que passa em todos e diz qual critério mandou. Quando nenhuma seção única atende, tenta dois condutores em paralelo
/// por fase (6.2.5.7). Aceita fatores de agrupamento impostos por trecho (seções dessemelhantes, 6.2.5.5.5).
/// </summary>
public sealed class Dimensionamento
{
    readonly Instalacao _i;
    readonly Grafo _grafo;
    readonly IReadOnlyDictionary<string, double> _fcaImposto;
    readonly Dictionary<string, double> _potenciaDoQuadro = new();
    readonly Dictionary<string, ResultadoCircuito> _resultados = new();
    readonly Dictionary<string, Impedancia> _impedanciaNoQuadro = new();
    readonly Dictionary<string, Impedancia> _impedanciaDeRetornoNoQuadro = new();
    readonly Dictionary<string, List<QuedaParcial>> _quedaAteOQuadro = new();
    readonly Dictionary<string, ResultadoDemanda> _demanda = new();

    public const int CondutoresEmParaleloMaximo = 2;

    public Dimensionamento(Instalacao instalacao, Grafo? grafo = null, IReadOnlyDictionary<string, double>? fatoresDeAgrupamentoImpostos = null)
    {
        _i = instalacao;
        _grafo = grafo ?? new Grafo(instalacao);
        _fcaImposto = fatoresDeAgrupamentoImpostos ?? new Dictionary<string, double>();
    }

    public IReadOnlyDictionary<string, ResultadoCircuito> Resultados => _resultados;

    /// <summary>Dimensiona todos os circuitos, dos quadros de entrada para as pontas (os alimentadores antes dos terminais).</summary>
    public IReadOnlyList<ResultadoCircuito> DimensionarTodos()
    {
        var entrada = _i.QuadroDeEntrada;
        var uo = _i.Alimentacao.TensaoFaseNeutro;
        _impedanciaNoQuadro[entrada.Id] = _i.Alimentacao.IccPresumidaKA is { } icc ? ImpedanciaDaRede(icc, uo) : Impedancia.Zero;
        _impedanciaDeRetornoNoQuadro[entrada.Id] = Impedancia.Zero;
        _quedaAteOQuadro[entrada.Id] = [];
        var ordem = new List<ResultadoCircuito>();
        Percorrer(entrada, ordem);
        return ordem;
    }

    void Percorrer(Quadro quadro, List<ResultadoCircuito> ordem)
    {
        foreach (var c in _i.CircuitosDo(quadro))
        {
            var r = Dimensionar(c);
            ordem.Add(r);
            if (c.QuadroAlimentado is { } filhoId && _i.Quadro(filhoId) is { } filho)
            {
                var ida = ImpedanciaDoCondutor(c.Material, r.SecaoFase, r.Comprimento) / r.CondutoresPorFase;
                _impedanciaNoQuadro[filho.Id] = _impedanciaNoQuadro[quadro.Id] + ida;
                _impedanciaDeRetornoNoQuadro[filho.Id] = _impedanciaDeRetornoNoQuadro[quadro.Id] + ImpedanciaDoCondutor(c.Material, r.SecaoPE, r.Comprimento);
                _quedaAteOQuadro[filho.Id] = [.. _quedaAteOQuadro[quadro.Id], new QuedaParcial(c.Id, r.Comprimento, r.QuedaTerminal)];
                Percorrer(filho, ordem);
            }
        }
    }

    /// <summary>Demanda do quadro com as parcelas por categoria (4.2.1.1).</summary>
    public ResultadoDemanda DemandaDo(Quadro q)
    {
        if (_demanda.TryGetValue(q.Id, out var pronta)) return pronta;
        var d = Demanda.Calcular(_i, q, filho => DemandaDo(filho).DemandaVA);
        _demanda[q.Id] = d;
        return d;
    }

    /// <summary>Potência de demanda do quadro em VA: circuitos próprios com os fatores de demanda, mais os quadros filhos.</summary>
    public double PotenciaDeDemanda(Quadro q) => DemandaDo(q).DemandaVA;

    public double PotenciaInstalada(Quadro q)
    {
        double soma = 0;
        foreach (var c in _i.CircuitosDo(q))
        {
            if (c.QuadroAlimentado is { } filhoId && _i.Quadro(filhoId) is { } filho) soma += PotenciaInstalada(filho);
            else soma += _i.PotenciaDo(c);
        }
        return soma;
    }

    /// <summary>Potência do circuito: a dos pontos, a informada, ou a demanda do quadro que ele alimenta.</summary>
    public double PotenciaDo(Circuito c)
    {
        var p = _i.PotenciaDo(c);
        if (p > 0) return p;
        return c.QuadroAlimentado is { } q && _i.Quadro(q) is { } filho ? PotenciaDeDemanda(filho) : 0;
    }

    public ResultadoCircuito Dimensionar(Circuito c)
    {
        if (_resultados.TryGetValue(c.Id, out var pronto)) return pronto;
        var a = _i.Alimentacao;
        var uo = a.TensaoFaseNeutro;
        var uff = a.TensaoFaseFase;
        var avisos = new List<string>();

        var potencia = PotenciaDo(c);
        var ib = CorrenteDeProjeto(potencia, c.Ligacao, uo, uff, c.FatorDePotencia, c.Rendimento);

        // Comprimento e caminho reais pelos eletrodutos (com a folga do projeto); sem eles, o manual.
        var caminho = _grafo.ComprimentoDe(c);
        var comprimento = caminho is { } cam ? cam.Comprimento * (1 + a.FolgaDeComprimentoPercentual / 100) : c.ComprimentoManual ?? 0;
        var estimado = caminho is null;
        if (caminho is null && c.ComprimentoManual is null)
            avisos.Add("sem eletrodutos nem comprimento informado: queda de tensão e curto no fim do circuito não foram avaliados");
        foreach (var p in _grafo.PontosSemCaminho(c))
            avisos.Add($"ponto {p.Id} sem caminho pelos eletrodutos até o quadro");

        var condutoresCarregados = Nbr5410.CondutoresCarregados(c.Ligacao, c.HarmonicasAltas);
        var colunaDaTabela = Math.Min(3, condutoresCarregados);
        var fatorNeutro = condutoresCarregados == 4 ? Nbr5410.FatorNeutroCarregado : 1.0;

        // Condições de instalação em todos os trechos por onde o circuito passa (6.2.5.8: vale a pior), não só no caminho mais longo.
        var trechos = _i.TrechosDo(c).ToList();
        var condicoes = trechos.Count > 0
            ? trechos.Select(t => new Condicao(t.Id, t.Metodo, t.Circuitos.Distinct().Count(), t.TemperaturaAmbiente ?? a.TemperaturaAmbiente)).ToList()
            : [new Condicao(null, c.MetodoPadrao, c.AgrupamentoManual ?? 1, a.TemperaturaAmbiente)];
        foreach (var cond in condicoes.Where(x => x.Trecho is not null && _fcaImposto.ContainsKey(x.Trecho)))
            avisos.Add($"trecho {cond.Trecho}: agrupamento com seções dessemelhantes, fator 1/√n = {_fcaImposto[cond.Trecho!]:0.00} (6.2.5.5.5)");

        var tensao = TensaoDoCircuito(c.Ligacao, uo, uff);
        var quedaAMontante = _quedaAteOQuadro.TryGetValue(c.Quadro, out var lista) ? lista : [];
        var quedaMontanteTotal = quedaAMontante.Sum(q => q.Percentual);
        var zRede = _impedanciaNoQuadro.TryGetValue(c.Quadro, out var zq) ? zq : Impedancia.Zero;
        var zRetornoRede = _impedanciaDeRetornoNoQuadro.TryGetValue(c.Quadro, out var zr) ? zr : Impedancia.Zero;
        var temIcc = a.IccPresumidaKA is not null;
        double? iccOrigemKA = temIcc ? CurtoMaximo(zRede, uo) / 1000 : null;
        var residualTT = a.Esquema == EsquemaAterramento.TT ? CorrenteResidualEmTT(c) : (double?)null;

        var secaoMinima = Nbr5410.SecaoMinima(c.Tipo, c.Material);
        var candidatas = c.SecaoImposta is { } imposta
            ? [imposta]
            : Condutores.SecoesDeProjeto.Where(s => s >= secaoMinima && Nbr5410Capacidades.SecoesDe(c.Material).Contains(s)).ToList();

        Tentativa? escolhida = null;
        string? criterioQueMandou = null;
        var paralelos = c.CondutoresPorFaseImpostos ?? 1;
        var primeiro = paralelos;
        for (var cpf = primeiro; cpf <= Math.Max(primeiro, CondutoresEmParaleloMaximo); cpf++)
        {
            if (cpf > primeiro && c.CondutoresPorFaseImpostos is not null) break;
            foreach (var secao in candidatas)
            {
                var t = Avaliar(c, secao, cpf, ib, comprimento, estimado, condicoes, colunaDaTabela, fatorNeutro, tensao, quedaMontanteTotal, zRede, zRetornoRede, iccOrigemKA, secaoMinima, residualTT);
                if (t.Pareceres.All(p => !p.Reprovado) || c.SecaoImposta is not null)
                {
                    escolhida = t;
                    break;
                }
                criterioQueMandou = t.Pareceres.First(p => p.Reprovado).Criterio;
                escolhida = t;
            }
            if (escolhida!.Pareceres.All(p => !p.Reprovado) || c.SecaoImposta is not null)
            {
                paralelos = cpf;
                break;
            }
            // nenhuma seção única passa: tenta condutores em paralelo (só se o que reprovou foi capacidade, sobrecarga ou queda)
            var reprovou = escolhida.Pareceres.First(p => p.Reprovado).Criterio;
            if (reprovou is not ("capacidade de corrente" or "sobrecarga" or "queda de tensão")) { paralelos = cpf; break; }
            paralelos = cpf + 1;
        }
        var e = escolhida!;
        if (e.CondutoresPorFase > 1) avisos.Add($"{e.CondutoresPorFase} condutores em paralelo por fase (6.2.5.7): nenhuma seção única atendeu; mesmo comprimento, seção e percurso para cada condutor");
        if (e.Pareceres.Any(p => p.Reprovado) && c.SecaoImposta is null)
            avisos.Add("nenhuma seção até a maior tabelada atende a todos os critérios: dividir o circuito ou rever o traçado");
        if (criterioQueMandou is null && c.SecaoImposta is null) criterioQueMandou = "seção mínima";

        var dr = EspecificarDR(c, e.Disjuntor, residualTT);
        var r = new ResultadoCircuito(c.Id, c.Numero, c.Descricao, c.Quadro, c.Tipo, c.Ligacao, potencia, ib, comprimento, estimado,
            c.Isolacao, c.Material, e.Critica.Metodo, e.Critica.Trecho, condutoresCarregados, e.FatorTemperatura, e.FatorAgrupamento,
            e.Critica.Circuitos, e.Secao, Nbr5410.SecaoDoNeutro(e.Secao, c.Ligacao, c.HarmonicasAltas), Nbr5410.SecaoDoPE(e.Secao),
            e.CapacidadeReferencia, e.CapacidadeCorrigida, e.Disjuntor, e.QuedaTerminal, quedaMontanteTotal + e.QuedaTerminal, quedaAMontante,
            iccOrigemKA, e.IccMinima, e.Zs, e.EnergiaPassante, e.EnergiaSuportada, dr, e.Pareceres, criterioQueMandou, avisos)
        {
            CondutoresPorFase = e.CondutoresPorFase,
        };
        _resultados[c.Id] = r;
        return r;
    }

    sealed record Condicao(string? Trecho, MetodoReferencia Metodo, int Circuitos, double Temperatura);

    sealed record Tentativa(double Secao, int CondutoresPorFase, Condicao Critica, double FatorTemperatura, double FatorAgrupamento, double CapacidadeReferencia,
        double CapacidadeCorrigida, Disjuntor Disjuntor, double QuedaTerminal, double? IccMinima, double? Zs, double? EnergiaPassante,
        double EnergiaSuportada, IReadOnlyList<Parecer> Pareceres);

    Tentativa Avaliar(Circuito c, double secao, int cpf, double ib, double comprimento, bool estimado, List<Condicao> condicoes,
        int coluna, double fatorNeutro, double tensao, double quedaMontante, Impedancia zRede, Impedancia zRetornoRede, double? iccOrigemKA, double secaoMinima, double? residualTT)
    {
        var a = _i.Alimentacao;
        var uo = a.TensaoFaseNeutro;
        var pareceres = new List<Parecer>();
        var ibPorCondutor = ib / cpf;
        var paralelo = cpf > 1 ? $" ({cpf} condutores em paralelo por fase: {ibPorCondutor:0.0} A cada)" : "";

        // (a) capacidade de corrente no pior trecho; cada condutor extra em paralelo conta como circuito no agrupamento (6.2.5.7)
        Condicao critica = condicoes[0];
        double izRef = 0, iz = double.MaxValue, fct = 1, fca = 1;
        foreach (var cond in condicoes)
        {
            var referencia = Nbr5410Capacidades.Capacidade(c.Isolacao, c.Material, cond.Metodo, coluna, secao) * fatorNeutro;
            var ft = cond.Metodo == MetodoReferencia.D
                ? Nbr5410.FatorDeTemperaturaSolo(c.Isolacao, cond.Temperatura)
                : Nbr5410.FatorDeTemperaturaAr(c.Isolacao, cond.Temperatura);
            var fa = Nbr5410.FatorDeAgrupamentoEmFeixe(cond.Circuitos + cpf - 1);
            if (cond.Trecho is not null && _fcaImposto.TryGetValue(cond.Trecho, out var imposto)) fa = Math.Min(fa, imposto);
            var corrigida = referencia * ft * fa;
            if (corrigida < iz) { iz = corrigida; izRef = referencia; fct = ft; fca = fa; critica = cond; }
        }
        var izTotal = iz * cpf;
        var tabela = Nbr5410Capacidades.Tabela(c.Isolacao, critica.Metodo);
        pareceres.Add(new Parecer("capacidade de corrente", ibPorCondutor <= iz + 1e-9, $"IB = {ib:0.0} A{paralelo}", $"IZ = {izTotal:0.0} A",
            $"6.2.6.1.2 a), 6.2.5, {tabela}",
            $"{tabela}, método {critica.Metodo}, {coluna} condutores carregados: {izRef:0.0} A × FCT {fct:0.00} × FCA {fca:0.00} ({critica.Circuitos} circuito(s) no trecho {critica.Trecho ?? "padrão"})" + (cpf > 1 ? $" × {cpf} condutores" : "")));

        // (f) seção mínima
        pareceres.Add(new Parecer("seção mínima", secao >= secaoMinima, $"{secao} mm²", $"≥ {secaoMinima} mm²", "6.2.6.1.2 f), Tabela 47",
            c.Tipo == TipoCarga.Iluminacao ? "circuito de iluminação: 1,5 mm² em cobre" : "circuito de força (tomadas inclusive): 2,5 mm² em cobre"));

        // (b) sobrecarga: IB ≤ In ≤ IZ, I2 ≤ 1,45 IZ
        var curva = c.CurvaImposta ?? Nbr60898.CurvaSugerida(c.Tipo);
        // In: o menor preferencial entre IB e IZ, de preferência não abaixo do usual do tipo (10 A em luz e TUG) quando IZ permite.
        var preferido = Math.Max(ib, Nbr60898.InMinimoUsual(c.Tipo));
        var inA = c.DisjuntorImposto
                  ?? (preferido <= izTotal ? Nbr60898.MenorInNaoInferiorA(preferido) : Nbr60898.MenorInNaoInferiorA(ib))
                  ?? Nbr60898.CorrentesNominais[Nbr60898.CorrentesNominais.Length - 1];
        var icn = iccOrigemKA is { } icc ? Nbr60898.IcnMinima(icc) : 3.0;
        if (icn < 3) icn = 3;
        var disjuntor = new Disjuntor(inA, curva, icn, Polos(c.Ligacao, a.Esquema));
        var sobrecarga = ib <= inA + 1e-9 && inA <= izTotal + 1e-9;
        pareceres.Add(new Parecer("sobrecarga", sobrecarga, $"IB {ib:0.0} ≤ In {inA:0.#} ≤ IZ {izTotal:0.0} A", $"I2 = {disjuntor.I2:0.0} ≤ 1,45 IZ = {Nbr5410.FatorI2 * izTotal:0.0} A",
            "5.3.4.1, 6.3.4.2, NBR NM 60898 8.6.2.3",
            sobrecarga ? $"disjuntor {disjuntor}; I2 = 1,45 In atende automaticamente quando In ≤ IZ" : inA < ib ? "o disjuntor imposto é menor que a corrente de projeto" : "não há In entre IB e IZ: subir a seção"));

        // (e) queda de tensão
        var temperaturaOperacao = Condutores.TemperaturaDeOperacao(c.Isolacao, critica.Temperatura, ibPorCondutor, iz);
        var rPorMetro = Condutores.ResistenciaPorMetro(c.Material, secao, temperaturaOperacao) / cpf;
        var xPorMetro = Condutores.ReatanciaPorMetro / cpf;
        var quedaTerminal = comprimento > 0 ? QuedaDeTensaoPercentual(ib, comprimento, rPorMetro, xPorMetro, c.FatorDePotencia, c.Ligacao, tensao) : 0;
        var limiteTerminal = c.Tipo == TipoCarga.Alimentador ? Nbr5410.QuedaTotalMaxima : Nbr5410.QuedaTerminalMaxima;
        var quedaTotal = quedaMontante + quedaTerminal;
        bool? atendeQueda = comprimento > 0 ? quedaTerminal <= limiteTerminal + 1e-9 && quedaTotal <= Nbr5410.QuedaTotalMaxima + 1e-9 : null;
        pareceres.Add(new Parecer("queda de tensão", atendeQueda, $"{quedaTerminal:0.00} % no circuito, {quedaTotal:0.00} % acumulada",
            $"≤ {limiteTerminal:0.#} % no circuito e ≤ {Nbr5410.QuedaTotalMaxima:0.#} % do ponto de entrega", "6.2.6.1.2 e), 6.2.7.1 c), 6.2.7.2",
            comprimento > 0
                ? $"L = {comprimento:0.0} m{(estimado ? " (informado)" : $" (pelos eletrodutos{(a.FolgaDeComprimentoPercentual > 0 ? $", com folga de {a.FolgaDeComprimentoPercentual:0} %" : "")})")}, R = {rPorMetro * 1000:0.000} Ω/km a {temperaturaOperacao:0} °C, cos φ = {c.FatorDePotencia:0.00}"
                : "sem comprimento: não avaliada"));

        // (c) curto-circuito: capacidade de interrupção e I²t ≤ k²S²
        var k = Nbr5410.FatorK(c.Material, c.Isolacao, secao);
        var suportada = k * k * secao * secao * cpf * cpf;
        double? iccMinima = null, zs = null, passante = null;
        if (iccOrigemKA is { } iccKA)
        {
            pareceres.Add(new Parecer("capacidade de interrupção", icn >= iccKA - 1e-9, $"Icc presumida no quadro = {iccKA:0.00} kA", $"Icn = {icn:0.#} kA",
                "5.3.5.5 a), 6.3.4.3, NBR NM 60898 5.3.4", "corrente de curto trifásica máxima no barramento do quadro pelo método das impedâncias"));

            // energia passante: a do catálogo, se informada; senão o limite da classe 3 do Anexo ZA na Icn tabelada imediatamente acima
            var icnDaClasse = new[] { 3.0, 4.5, 6.0, 10.0 }.FirstOrDefault(v => v >= icn - 1e-9);
            var classe3 = icnDaClasse > 0 ? Nbr60898.EnergiaPassanteMaxima(3, curva, inA, icnDaClasse) : null;
            passante = c.I2tDoDisjuntor ?? classe3;
            var limiteTexto = $"k²S² = {suportada:0} A²s (k = {k}{(cpf > 1 ? $", {cpf} condutores" : "")})";
            if (c.I2tDoDisjuntor is { } catalogo)
                pareceres.Add(new Parecer("solicitação térmica no curto", catalogo <= suportada + 1e-9, $"I²t = {catalogo:0} A²s (catálogo)", limiteTexto,
                    "5.3.5.5 b), Tabela 30", "energia passante do disjuntor na Icc presumida, informada do catálogo do fabricante"));
            else if (classe3 is { } limiteClasse)
                pareceres.Add(new Parecer("solicitação térmica no curto", limiteClasse <= suportada + 1e-9 ? true : null, $"I²t ≤ {limiteClasse:0} A²s (classe 3)", limiteTexto,
                    "5.3.5.5 b), Tabela 30, NBR NM 60898 Anexo ZA",
                    limiteClasse <= suportada + 1e-9
                        ? $"limite da classe 3 de limitação de energia, curva {curva}, In {inA:0.#} A, Icn {icnDaClasse:0.#} kA (Tabela ZA): qualquer disjuntor classe 3 protege o condutor"
                        : $"não comprovado: o limite da classe 3 ({limiteClasse:0} A²s) passa de k²S²; a curva I²t real do fabricante costuma ficar abaixo. Informe o I²t de catálogo na Icc presumida para comprovar, ou use disjuntor limitador"));
            else
                pareceres.Add(new Parecer("solicitação térmica no curto", null, "sem limite tabelado", limiteTexto, "5.3.5.5 b), Tabela 30",
                    $"curva {curva}, In {inA:0.#} A ou Icn {icn:0.#} kA fora do Anexo ZA: informe o I²t do fabricante na Icc presumida"));

            if (comprimento > 0)
            {
                var ida = ImpedanciaDoCondutor(c.Material, secao, comprimento) / cpf;
                var pe = ImpedanciaDoCondutor(c.Material, Nbr5410.SecaoDoPE(secao), comprimento);
                var redeIda = zRede;
                var redeVolta = zRetornoRede;
                iccMinima = CurtoMinimo(redeIda + Impedancia.Zero, ida, pe + redeVolta, uo);
                zs = ImpedanciaDeFalta(redeIda + redeVolta, ida, pe);
                var ia = disjuntor.Ia;
                if (a.Esquema is EsquemaAterramento.TT)
                {
                    var ul = Nbr5410.TensaoDeContatoLimite(Situacao.Normal);
                    var idn = (residualTT ?? DispositivosDR.AltaSensibilidade) / 1000;
                    if (a.ResistenciaDeAterramentoOhm is { } ra)
                        pareceres.Add(new Parecer("seccionamento automático", ra * idn <= ul + 1e-9, $"RA · IΔn = {ra:0.#} Ω × {idn * 1000:0} mA = {ra * idn:0.00} V",
                            $"≤ UL = {ul:0} V", "5.1.2.2.4.3", $"esquema TT: proteção contra contatos indiretos pelo DR de {idn * 1000:0} mA com a resistência do eletrodo RA informada; Ikmín = {iccMinima:0} A"));
                    else
                        pareceres.Add(new Parecer("seccionamento automático", null, $"Ikmín = {iccMinima:0} A; DR {idn * 1000:0} mA", $"RA · IΔn ≤ UL = {ul:0} V (RA ≤ {ul / idn:0} Ω)", "5.1.2.2.4.3",
                            "esquema TT: informe a resistência do eletrodo de aterramento (RA) nos Dados do projeto para avaliar; a proteção é pelo DR"));
                }
                else
                {
                    var tempo = c.Tipo == TipoCarga.Alimentador ? Nbr5410.TempoMaximoDistribuicao : Nbr5410.TempoMaximoTN(uo, Situacao.Normal);
                    pareceres.Add(new Parecer("seccionamento automático", iccMinima >= ia - 1e-9, $"Zs = {zs:0.000} Ω, Ikmín = {iccMinima:0} A",
                        $"Ia = {ia:0} A (curva {curva}, {Nbr60898.FaixaInstantanea(curva).Superior:0} × In); Zs · Ia = {zs * ia:0} ≤ Uo = {uo:0} V",
                        "5.1.2.2.4.2, Tabela 25", $"esquema {a.Esquema}: o disjuntor deve atuar em até {tempo} s na falta fase-PE no fim do circuito; aqui pela atuação instantânea (< 0,1 s)"));
                }
            }
        }
        else
        {
            pareceres.Add(new Parecer("curto-circuito", null, "Icc presumida não informada", "—", "4.2.3, 5.3.5.1",
                "informe a corrente de curto-circuito presumida no ponto de entrega (distribuidora ou transformador) para avaliar Icn, I²t e Zs·Ia"));
        }

        return new Tentativa(secao, cpf, critica, fct, fca, izRef, izTotal, disjuntor, quedaTerminal, iccMinima, zs, passante, suportada, pareceres);
    }

    /// <summary>
    /// Polos do disjuntor: um por fase; o neutro só é seccionado em TT e IT (5.6.3 e 6.3.7: em TN o neutro pode não ser
    /// seccionado). Em TN um circuito fase-neutro leva disjuntor unipolar (ou 1P+N) e um bifásico com neutro, bipolar.
    /// </summary>
    public static int Polos(Ligacao ligacao, EsquemaAterramento esquema)
    {
        var fases = ligacao switch { Ligacao.FN => 1, Ligacao.FF or Ligacao.FFN => 2, _ => 3 };
        var neutro = ligacao is Ligacao.FN or Ligacao.FFN or Ligacao.FFFN;
        return fases + (neutro && esquema is EsquemaAterramento.TT or EsquemaAterramento.IT ? 1 : 0);
    }

    /// <summary>Em TT todo circuito leva DR (5.1.2.2.4.3): 30 mA onde a proteção adicional é exigida, 300 mA nos demais.</summary>
    double CorrenteResidualEmTT(Circuito c)
    {
        var exige = _i.PontosDo(c).Any(p => DispositivosDR.MotivoDeDR(p, _i.AmbienteDe(p), _i.Alimentacao.Habitacao) is not null) || c.Tipo is TipoCarga.Chuveiro or TipoCarga.VeiculoEletrico;
        return exige ? DispositivosDR.AltaSensibilidade : 300;
    }

    EspecificacaoDR EspecificarDR(Circuito c, Disjuntor d, double? residualTT)
    {
        string? motivo = null;
        var tipo = TipoDr.AC;
        foreach (var p in _i.PontosDo(c))
        {
            motivo ??= DispositivosDR.MotivoDeDR(p, _i.AmbienteDe(p), _i.Alimentacao.Habitacao);
            var sugerido = DispositivosDR.TipoSugerido(p.Tipo);
            if (sugerido > tipo) tipo = sugerido;
        }
        if (motivo is null && c.Tipo == TipoCarga.Chuveiro) motivo = "5.1.3.2.2 a) — chuveiro ou aquecedor em banheiro";
        if (c.Tipo == TipoCarga.VeiculoEletrico) { motivo ??= "NBR 17019 — ponto de recarga de veículo elétrico"; tipo = TipoDr.B; }
        var residual = DispositivosDR.AltaSensibilidade;
        if (motivo is null && residualTT is { } idn)
        {
            motivo = "5.1.2.2.4.3 — esquema TT: seccionamento automático pelo DR";
            residual = idn;
        }
        var inDr = DispositivosDR.MenorInNaoInferiorA(d.In) ?? DispositivosDR.CorrentesNominais[DispositivosDR.CorrentesNominais.Length - 1];
        return new EspecificacaoDR(motivo is not null, motivo, residual, motivo is null ? TipoDr.AC : (tipo < TipoDr.A ? TipoDr.A : tipo), inDr);
    }
}
