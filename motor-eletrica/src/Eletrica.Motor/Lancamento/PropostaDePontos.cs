using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Lancamento;

/// <summary>
/// O que cada ambiente de uma habitação precisa pela NBR 5410 9.5.2 (pontos de tomada pelo perímetro e uso, potências
/// mínimas, ponto de luz no teto com a carga mínima) mais o que o padrão do escritório sugere (chuveiro no banheiro,
/// ar-condicionado). É a lista que o add-in lança no modelo ou mostra para o engenheiro ajustar antes de lançar.
/// </summary>
public static class PropostaDePontos
{
    public static PropostaDoAmbiente Propor(Ambiente a, PadraoDeLancamento padrao, bool habitacao = true)
    {
        var pontos = new List<PontoProposto>();
        var uso = a.Uso;

        // ----- iluminação (9.5.2.1) -----
        var cargaLuz = Math.Max(Nbr5410.CargaMinimaDeIluminacao(a.Area), 100);
        var luzNaParede = uso is UsoAmbiente.Varanda or UsoAmbiente.Deposito or UsoAmbiente.Lavabo && a.Area <= 6;
        if (uso == UsoAmbiente.AreaExterna)
            pontos.Add(new PontoProposto(TipoCarga.Iluminacao, PosicaoDoPonto.Alta, 100, 1, "área externa: ponto de luz na parede, carga a critério do projeto", "9.5.2.1.1"));
        else
            pontos.Add(new PontoProposto(TipoCarga.Iluminacao, luzNaParede ? PosicaoDoPonto.Alta : PosicaoDoPonto.Teto, cargaLuz, 1,
                $"{F(a.Area)} m²: 100 VA até 6 m² + 60 VA por 4 m² inteiros a mais" + (luzNaParede ? " (na parede: ambiente pequeno)" : ""), "9.5.2.1.2"));

        // ----- tomadas (9.5.2.2) -----
        if (!habitacao)
        {
            pontos.Add(new PontoProposto(TipoCarga.TomadaUsoGeral, PosicaoDoPonto.Baixa, padrao.PotenciaTug, 1, "não residencial: a quantidade é do projeto (4.2.1.2.3)", "4.2.1.2.3") { CorrenteDaTomada = 10 });
            return new PropostaDoAmbiente(a, pontos);
        }

        var minimo = Nbr5410.PontosDeTomadaMinimos(uso, a.Area, a.Perimetro);
        if (uso is UsoAmbiente.Circulacao or UsoAmbiente.Garagem or UsoAmbiente.Deposito or UsoAmbiente.CasaDeMaquinas or UsoAmbiente.Outro or UsoAmbiente.Escritorio)
            minimo = a.Area > 2.25 ? minimo : 1;
        if (uso == UsoAmbiente.AreaExterna) minimo = 1;

        var minimas = Nbr5410.PotenciasMinimasDasTomadas(uso, minimo).ToList();
        var de600 = minimas.Count(v => v >= 600);
        var de100 = minimo - de600;
        var motivo = uso switch
        {
            UsoAmbiente.Banheiro or UsoAmbiente.Lavabo => "banheiro: pelo menos 1 junto ao lavatório, fora dos volumes 0, 1 e 2",
            UsoAmbiente.Varanda => "varanda: pelo menos 1",
            UsoAmbiente.Cozinha or UsoAmbiente.Copa or UsoAmbiente.AreaServico or UsoAmbiente.Lavanderia =>
                $"perímetro {F(a.Perimetro)} m: 1 a cada 3,5 m ou fração",
            UsoAmbiente.Sala or UsoAmbiente.Dormitorio => $"perímetro {F(a.Perimetro)} m: 1 a cada 5 m ou fração",
            _ => a.Area > 2.25 ? $"perímetro {F(a.Perimetro)} m: 1 a cada 5 m ou fração" : "até 2,25 m²: pelo menos 1",
        };
        var potencia600 = Math.Max(600, padrao.PotenciaTugMolhada);
        var potencia100 = Math.Max(100, padrao.PotenciaTug);
        if (de600 > 0)
        {
            // Cozinha e copa: duas sobre a bancada (9.5.2.2.1); banheiro: junto ao lavatório (média). Área de serviço: tanque e máquina.
            var naMedia = uso is UsoAmbiente.Banheiro or UsoAmbiente.Lavabo ? de600
                : Math.Min(de600, uso is UsoAmbiente.Cozinha or UsoAmbiente.Copa ? Nbr5410.TomadasSobreABancada : 1);
            if (naMedia > 0)
                pontos.Add(new PontoProposto(TipoCarga.TomadaUsoGeral, PosicaoDoPonto.Media, potencia600, naMedia,
                    motivo + "; 600 VA nos três primeiros pontos" + (uso is UsoAmbiente.Cozinha or UsoAmbiente.Copa ? "; pelo menos duas acima da bancada" : ""),
                    "9.5.2.2.1, 9.5.2.2.2") { CorrenteDaTomada = 10 });
            if (de600 - naMedia > 0)
                pontos.Add(new PontoProposto(TipoCarga.TomadaUsoGeral, PosicaoDoPonto.Baixa, potencia600, de600 - naMedia, "600 VA nos três primeiros pontos", "9.5.2.2.2") { CorrenteDaTomada = 10 });
        }
        if (de100 > 0)
            pontos.Add(new PontoProposto(TipoCarga.TomadaUsoGeral, PosicaoDoPonto.Baixa, potencia100, de100,
                de600 > 0 ? "demais pontos: 100 VA" : motivo + "; 100 VA por ponto", "9.5.2.2.1, 9.5.2.2.2") { CorrenteDaTomada = 10 });

        // ----- equipamentos sugeridos pelo padrão -----
        if (uso == UsoAmbiente.Banheiro && padrao.SugerirChuveiro)
            pontos.Add(new PontoProposto(TipoCarga.Chuveiro, PosicaoDoPonto.Alta, padrao.PotenciaChuveiro, 1,
                "chuveiro elétrico: ligação direta, circuito independente, dentro do volume 1 só com IPX5 e DR", "9.5.2.3, 9.5.3.1, 9.1.4.4"));
        if (uso is UsoAmbiente.Dormitorio or UsoAmbiente.Sala && padrao.SugerirArCondicionado)
            pontos.Add(new PontoProposto(TipoCarga.ArCondicionado, PosicaoDoPonto.Alta, padrao.PotenciaArCondicionado, 1,
                "ar-condicionado: circuito independente", "9.5.3.1"));
        if (uso == UsoAmbiente.CasaDeMaquinas && pontos.All(p => p.Tipo != TipoCarga.TomadaUsoGeral))
            pontos.Add(new PontoProposto(TipoCarga.TomadaUsoGeral, PosicaoDoPonto.Media, Nbr5410.PotenciaMinimaCasaDeMaquinas, 1,
                "casa de máquinas: pelo menos um ponto de tomada num circuito de 1 000 VA", "4.2.1.2.3") { CorrenteDaTomada = 10 });

        return new PropostaDoAmbiente(a, pontos);
    }

    public static IReadOnlyList<PropostaDoAmbiente> Propor(IEnumerable<Ambiente> ambientes, PadraoDeLancamento padrao, bool habitacao = true) =>
        ambientes.Select(a => Propor(a, padrao, habitacao)).ToList();

    static string F(double v) => v.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
