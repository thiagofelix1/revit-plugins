using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Lancamento;

/// <summary>
/// Divide os pontos em circuitos terminais pela NBR 5410 4.2.5.5 e 9.5.3 e pelos limites do padrão do escritório:
/// iluminação separada das tomadas; tomadas de cozinha, copa, área de serviço e lavanderia em circuitos exclusivos;
/// banheiro à parte (DR só nele); todo equipamento com mais de 10 A em circuito próprio (chuveiro, ar-condicionado,
/// aquecedor, motor, VE, TUE de 20 A); o resto agrupado por ambiente, sem passar da corrente nem do número de pontos do
/// padrão. A ligação é fase-neutro, ou fase-fase quando a corrente passa do limite do padrão e há mais de uma fase.
/// O engenheiro revisa a proposta antes de criar os circuitos no Revit.
/// </summary>
public static class DivisaoDeCircuitos
{
    public static IReadOnlyList<CircuitoProposto> Propor(IReadOnlyList<Ponto> pontos, IReadOnlyDictionary<string, Ambiente> ambientes,
        Alimentacao alimentacao, PadraoDeLancamento padrao, int numeroInicial = 1)
    {
        var uo = alimentacao.TensaoFaseNeutro;
        var uff = alimentacao.TensaoFaseFase;
        var propostas = new List<CircuitoProposto>();
        var n = numeroInicial;
        string Numero() => padrao.PrefixoCircuito + n++;
        Ambiente Am(Ponto p) => ambientes.TryGetValue(p.Ambiente, out var a) ? a : new Ambiente(p.Ambiente, p.Ambiente, UsoAmbiente.Outro, 0, 0);
        string Nomes(IEnumerable<Ponto> ps) => string.Join(", ", ps.Select(p => Am(p).Nome).Distinct());

        // 1. Equipamentos e TUEs com circuito próprio (9.5.3.1: acima de 10 A).
        var proprios = pontos.Where(p => PrecisaDeCircuitoProprio(p, uo)).OrderBy(p => Am(p).Nome).ThenBy(p => p.Id).ToList();
        foreach (var p in proprios)
        {
            var ligacao = LigacaoPara(p.PotenciaVA, alimentacao, padrao);
            var ib = Eletrotecnica.CorrenteDeProjeto(p.PotenciaVA, ligacao, uo, uff);
            propostas.Add(new CircuitoProposto(Numero(), p.Tipo, ligacao, $"{NomeDoTipo(p.Tipo)} {Am(p).Nome}", [p.Id], p.PotenciaVA, ib,
                $"{F(p.PotenciaVA / uo)} A em {F(uo)} V: acima de 10 A pede circuito independente (9.5.3.1)"));
        }
        var restantes = pontos.Except(proprios).ToList();

        // 2. Iluminação, agrupada por ambiente até o limite do padrão.
        var luz = restantes.Where(p => p.Tipo == TipoCarga.Iluminacao).ToList();
        foreach (var grupo in Agrupar(luz, Am, uo, padrao.CorrenteMaximaIluminacao, padrao.PontosMaximosPorCircuito))
            propostas.Add(Circuito(Numero(), TipoCarga.Iluminacao, grupo, "Iluminação " + Nomes(grupo), "iluminação separada das tomadas (9.5.3.3)", alimentacao));

        // 3. Tomadas: cozinha e copa; área de serviço e lavanderia; banheiros; o resto.
        var tomadas = restantes.Where(p => p.Tipo is TipoCarga.TomadaUsoGeral or TipoCarga.TomadaUsoEspecifico or TipoCarga.Outro).ToList();
        var cozinha = tomadas.Where(p => Am(p).Uso is UsoAmbiente.Cozinha or UsoAmbiente.Copa).ToList();
        var servico = tomadas.Where(p => Am(p).Uso is UsoAmbiente.AreaServico or UsoAmbiente.Lavanderia).ToList();
        var banho = tomadas.Where(p => Am(p).Uso is UsoAmbiente.Banheiro or UsoAmbiente.Lavabo).ToList();
        var outras = tomadas.Except(cozinha).Except(servico).Except(banho).ToList();
        foreach (var grupo in Agrupar(cozinha, Am, uo, padrao.CorrenteMaximaTug, padrao.PontosMaximosPorCircuito))
            propostas.Add(Circuito(Numero(), TipoCarga.TomadaUsoGeral, grupo, "TUG " + Nomes(grupo), "cozinha e copa: circuito exclusivo de tomadas (9.5.3.2)", alimentacao));
        foreach (var grupo in Agrupar(servico, Am, uo, padrao.CorrenteMaximaTug, padrao.PontosMaximosPorCircuito))
            propostas.Add(Circuito(Numero(), TipoCarga.TomadaUsoGeral, grupo, "TUG " + Nomes(grupo), "área de serviço e lavanderia: circuito exclusivo de tomadas (9.5.3.2)", alimentacao));
        foreach (var grupo in Agrupar(banho, Am, uo, padrao.CorrenteMaximaTug, padrao.PontosMaximosPorCircuito))
            propostas.Add(Circuito(Numero(), TipoCarga.TomadaUsoGeral, grupo, "TUG " + Nomes(grupo), "banheiro: DR de 30 mA obrigatório; circuito à parte para o DR não abranger o resto (5.1.3.2.2 a)", alimentacao));
        foreach (var grupo in Agrupar(outras, Am, uo, padrao.CorrenteMaximaTug, padrao.PontosMaximosPorCircuito))
            propostas.Add(Circuito(Numero(), TipoCarga.TomadaUsoGeral, grupo, "TUG " + Nomes(grupo), "tomadas de uso geral agrupadas por ambiente (4.2.5.5)", alimentacao));

        // 4. O que sobrou (motores pequenos, aquecimento leve, outros) vai cada um no seu circuito.
        foreach (var p in restantes.Except(luz).Except(tomadas))
        {
            var ligacao = LigacaoPara(p.PotenciaVA, alimentacao, padrao);
            propostas.Add(new CircuitoProposto(Numero(), p.Tipo, ligacao, $"{NomeDoTipo(p.Tipo)} {Am(p).Nome}", [p.Id], p.PotenciaVA,
                Eletrotecnica.CorrenteDeProjeto(p.PotenciaVA, ligacao, uo, uff), "equipamento de utilização em circuito próprio (4.2.5.5)"));
        }
        return propostas;
    }

    /// <summary>9.5.3.1 e a prática: chuveiro, ar-condicionado, aquecedor, motor, VE, tomada de 20 A e qualquer ponto acima de 10 A em fase-neutro.</summary>
    public static bool PrecisaDeCircuitoProprio(Ponto p, double uo) =>
        p.Tipo is TipoCarga.Chuveiro or TipoCarga.ArCondicionado or TipoCarga.Aquecimento or TipoCarga.Motor or TipoCarga.VeiculoEletrico
        || p.CorrenteDaTomada is 20
        || p.PotenciaVA / uo > Nbr5410.CorrenteParaCircuitoProprio + 1e-9;

    /// <summary>Fase-fase quando a corrente em fase-neutro passa do limite do padrão e a entrada tem mais de uma fase.</summary>
    public static Ligacao LigacaoPara(double potenciaVA, Alimentacao a, PadraoDeLancamento padrao) =>
        a.Fases >= 2 && potenciaVA / a.TensaoFaseNeutro > padrao.CorrenteParaFaseFase ? Ligacao.FF : Ligacao.FN;

    static CircuitoProposto Circuito(string numero, TipoCarga tipo, List<Ponto> pontos, string descricao, string motivo, Alimentacao a)
    {
        var potencia = pontos.Sum(p => p.PotenciaVA);
        var ib = Eletrotecnica.CorrenteDeProjeto(potencia, Ligacao.FN, a.TensaoFaseNeutro, a.TensaoFaseFase);
        return new CircuitoProposto(numero, tipo, Ligacao.FN, descricao, pontos.Select(p => p.Id).ToList(), potencia, ib, motivo);
    }

    /// <summary>Agrupa por ambiente, na ordem dos ambientes, sem passar da corrente nem do número de pontos; um ambiente grande pode virar mais de um circuito.</summary>
    static IEnumerable<List<Ponto>> Agrupar(List<Ponto> pontos, Func<Ponto, Ambiente> am, double uo, double correnteMaxima, int pontosMaximos)
    {
        var vaMaximo = correnteMaxima * uo;
        var atual = new List<Ponto>();
        double va = 0;
        foreach (var grupoAmbiente in pontos.GroupBy(p => p.Ambiente).OrderBy(g => am(g.First()).Nome, StringComparer.OrdinalIgnoreCase))
        {
            var doAmbiente = grupoAmbiente.OrderByDescending(p => p.PotenciaVA).ThenBy(p => p.Id).ToList();
            var vaAmbiente = doAmbiente.Sum(p => p.PotenciaVA);
            // O ambiente inteiro cabe no circuito atual? Senão, fecha o atual e começa outro com ele.
            if (atual.Count > 0 && (va + vaAmbiente > vaMaximo + 1e-9 || atual.Count + doAmbiente.Count > pontosMaximos))
            {
                yield return atual;
                atual = [];
                va = 0;
            }
            foreach (var p in doAmbiente)
            {
                if (atual.Count > 0 && (va + p.PotenciaVA > vaMaximo + 1e-9 || atual.Count >= pontosMaximos))
                {
                    yield return atual;
                    atual = [];
                    va = 0;
                }
                atual.Add(p);
                va += p.PotenciaVA;
            }
        }
        if (atual.Count > 0) yield return atual;
    }

    public static string NomeDoTipo(TipoCarga tipo) => tipo switch
    {
        TipoCarga.Iluminacao => "Iluminação",
        TipoCarga.TomadaUsoGeral => "TUG",
        TipoCarga.TomadaUsoEspecifico => "TUE",
        TipoCarga.Chuveiro => "Chuveiro",
        TipoCarga.ArCondicionado => "Ar-condicionado",
        TipoCarga.Motor => "Motor",
        TipoCarga.Aquecimento => "Aquecimento",
        TipoCarga.VeiculoEletrico => "Carregador de VE",
        TipoCarga.Alimentador => "Alimentador",
        _ => "Carga",
    };

    static string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
