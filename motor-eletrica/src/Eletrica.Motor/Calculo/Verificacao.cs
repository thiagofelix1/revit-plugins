using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Calculo;

/// <summary>
/// Verificar modelo: as regras da NBR 5410 que não dependem de dimensionar (pontos mínimos por ambiente, DR, divisão de
/// circuitos, banheiro, quadro, DPS) e a consistência do modelo (ponto sem circuito, circuito vazio, ponto sem caminho).
/// Cada problema tem código estável para o add-in destacar na vista e para os testes.
/// </summary>
public static class Verificacao
{
    public static IReadOnlyList<Problema> Verificar(Instalacao i, Grafo? grafo = null)
    {
        grafo ??= new Grafo(i);
        var problemas = new List<Problema>();
        var a = i.Alimentacao;
        var uo = a.TensaoFaseNeutro;
        var uff = a.TensaoFaseFase;

        // ----- modelo -----
        foreach (var p in i.Pontos.Where(p => p.Circuito is null))
            problemas.Add(new Problema("M-01", Gravidade.Erro, $"Ponto {p.Id} ({p.Tipo}) sem circuito.", "4.2.5.1", [p.Id]));
        foreach (var c in i.Circuitos)
        {
            if (!i.PontosDo(c).Any() && (c.PotenciaVA ?? 0) <= 0 && c.QuadroAlimentado is null)
                problemas.Add(new Problema("M-02", Gravidade.Aviso, $"Circuito {c.Numero} sem pontos nem potência: fica como reserva.", "Tabela 59", [c.Id]));
            foreach (var p in grafo.PontosSemCaminho(c))
                problemas.Add(new Problema("M-03", Gravidade.Erro, $"Ponto {p.Id} do circuito {c.Numero} não tem caminho pelos eletrodutos até o quadro {c.Quadro}.", "6.2.11.1", [p.Id, c.Id]));
        }
        foreach (var q in i.Quadros.Where(q => !i.CircuitosDo(q).Any()))
            problemas.Add(new Problema("M-04", Gravidade.Aviso, $"Quadro {q.Nome} sem circuitos.", "6.5.4", [q.Id]));
        foreach (var q in i.Quadros.Where(q => q.QuadroPai is not null && i.AlimentadorDo(q) is null))
            problemas.Add(new Problema("M-05", Gravidade.Erro, $"Quadro {q.Nome} não tem circuito alimentador saindo do quadro {q.QuadroPai}.", "4.2.5.7", [q.Id]));

        // ----- ambientes (9.5, só habitação) -----
        if (a.Habitacao)
        {
            var pontosMolhados = i.Ambientes.Where(am => am.Uso is UsoAmbiente.Banheiro or UsoAmbiente.Cozinha or UsoAmbiente.Copa or UsoAmbiente.AreaServico or UsoAmbiente.Lavanderia)
                .Sum(am => i.PontosDo(am).Count(EhTomada));
            foreach (var am in i.Ambientes)
            {
                var pontos = i.PontosDo(am).ToList();
                var tomadas = pontos.Count(EhTomada);
                var minimo = Nbr5410.PontosDeTomadaMinimos(am.Uso, am.Area, am.Perimetro);
                if (am.Uso is UsoAmbiente.Circulacao or UsoAmbiente.Garagem or UsoAmbiente.AreaExterna or UsoAmbiente.Deposito or UsoAmbiente.CasaDeMaquinas or UsoAmbiente.Outro)
                    minimo = am.Area > 2.25 ? minimo : 1;
                if (tomadas < minimo)
                    problemas.Add(new Problema("A-01", Gravidade.Erro,
                        $"{am.Nome}: {tomadas} ponto(s) de tomada; o mínimo para {Descrever(am.Uso)} com {am.Perimetro:0.0} m de perímetro e {am.Area:0.0} m² é {minimo}.",
                        "9.5.2.2.1", [am.Id]));

                var luzNoTeto = pontos.Any(p => p.Tipo == TipoCarga.Iluminacao && p.NoTeto);
                var luz = pontos.Any(p => p.Tipo == TipoCarga.Iluminacao);
                var aceitaParede = am.Uso is UsoAmbiente.Varanda or UsoAmbiente.Deposito or UsoAmbiente.Lavabo && am.Area <= 6;
                if (!luz)
                    problemas.Add(new Problema("A-02", Gravidade.Erro, $"{am.Nome}: sem ponto de luz fixo comandado por interruptor.", "9.5.2.1.1", [am.Id]));
                else if (!luzNoTeto && !aceitaParede && am.Uso != UsoAmbiente.AreaExterna)
                    problemas.Add(new Problema("A-03", Gravidade.Aviso, $"{am.Nome}: o ponto de luz não está no teto (admitido na parede só em varanda, depósito, lavabo e sob escada pequenos).", "9.5.2.1.1", [am.Id]));

                var cargaLuz = pontos.Where(p => p.Tipo == TipoCarga.Iluminacao).Sum(p => p.PotenciaVA);
                var cargaMinima = Nbr5410.CargaMinimaDeIluminacao(am.Area);
                if (luz && cargaLuz + 1e-9 < cargaMinima && am.Uso != UsoAmbiente.AreaExterna)
                    problemas.Add(new Problema("A-04", Gravidade.Aviso, $"{am.Nome}: carga de iluminação {cargaLuz:0} VA abaixo do mínimo de {cargaMinima:0} VA para {am.Area:0.0} m².", "9.5.2.1.2", [am.Id]));

                if (am.Uso is UsoAmbiente.Cozinha or UsoAmbiente.Copa && am.Bancada is { } b && b > 0)
                {
                    var sobreBancada = pontos.Count(p => EhTomada(p) && p.Altura is >= 1.0 and <= 1.4);
                    if (sobreBancada < Nbr5410.TomadasSobreABancada)
                        problemas.Add(new Problema("A-05", Gravidade.Aviso, $"{am.Nome}: {sobreBancada} tomada(s) na altura da bancada; a norma pede pelo menos 2 acima da bancada da pia.", "9.5.2.2.1", [am.Id]));
                }

                var minimas = Nbr5410.PotenciasMinimasDasTomadas(am.Uso, tomadas, pontosMolhados > 6).ToList();
                var declaradas = pontos.Where(EhTomada).Select(p => p.PotenciaVA).OrderByDescending(v => v).ToList();
                for (var n = 0; n < Math.Min(minimas.Count, declaradas.Count); n++)
                    if (declaradas[n] + 1e-9 < minimas[n])
                    {
                        problemas.Add(new Problema("A-06", Gravidade.Aviso, $"{am.Nome}: há ponto de tomada com {declaradas[n]:0} VA; o mínimo é {minimas[n]:0} VA.", "9.5.2.2.2", [am.Id]));
                        break;
                    }
            }
        }

        foreach (var am in i.Ambientes.Where(am => am.Uso == UsoAmbiente.CasaDeMaquinas))
        {
            var tug = i.PontosDo(am).Where(p => p.Tipo == TipoCarga.TomadaUsoGeral).ToList();
            if (tug.Count == 0)
                problemas.Add(new Problema("A-07", Gravidade.Erro, $"{am.Nome}: casa de máquinas sem tomada de uso geral (circuito de pelo menos 1 000 VA).", "4.2.1.2.3", [am.Id]));
        }

        // ----- banheiro (9.1) e chuveiro -----
        foreach (var p in i.Pontos)
        {
            var am = i.AmbienteDe(p);
            if (p.VolumeDoBanheiro is { } v && v <= 2 && (EhTomada(p) || p.Tipo == TipoCarga.Iluminacao && v <= 1))
                problemas.Add(new Problema("B-01", Gravidade.Erro, $"{am.Nome}: {p.Tipo} ({p.Id}) dentro do volume {v} do banheiro.", "9.1.4.3.1, 9.1.4.4", [p.Id]));
            if (p.Tipo == TipoCarga.Chuveiro && p.CorrenteDaTomada is not null)
                problemas.Add(new Problema("B-02", Gravidade.Erro, $"{am.Nome}: chuveiro ou aquecedor ({p.Id}) ligado por tomada; a ligação deve ser direta.", "9.5.2.3", [p.Id]));
        }

        // ----- DR (5.1.3.2.2) -----
        foreach (var c in i.Circuitos)
        {
            var motivos = i.PontosDo(c).Select(p => (p, m: DispositivosDR.MotivoDeDR(p, i.AmbienteDe(p), a.Habitacao))).Where(x => x.m is not null).ToList();
            if (motivos.Count > 0)
                problemas.Add(new Problema("DR-01", Gravidade.Nota, $"Circuito {c.Numero} exige DR de 30 mA: {motivos[0].m} ({motivos.Count} ponto(s)).", "5.1.3.2.2",
                    [c.Id, .. motivos.Select(x => x.p.Id)]));
        }

        // ----- divisão de circuitos (4.2.5.5, 9.5.3) -----
        foreach (var c in i.Circuitos)
        {
            var pontos = i.PontosDo(c).ToList();
            if (pontos.Count == 0) continue;
            var temLuz = pontos.Any(p => p.Tipo == TipoCarga.Iluminacao);
            var temTomada = pontos.Any(EhTomada);
            var potencia = i.PotenciaDo(c);
            var ib = Eletrotecnica.CorrenteDeProjeto(potencia, c.Ligacao, uo, uff, c.FatorDePotencia, c.Rendimento);
            if (temLuz && temTomada)
            {
                if (!a.Habitacao || ib > Nbr5410.CorrenteMaximaCircuitoComum)
                    problemas.Add(new Problema("C-01", Gravidade.Erro, $"Circuito {c.Numero} mistura iluminação e tomadas (IB = {ib:0.0} A).", a.Habitacao ? "9.5.3.3" : "4.2.5.5", [c.Id]));
                else
                    problemas.Add(new Problema("C-02", Gravidade.Aviso, $"Circuito {c.Numero} mistura iluminação e tomadas; admitido em habitação com IB ≤ 16 A (aqui {ib:0.0} A), desde que nem toda a iluminação nem todas as tomadas fiquem nele.", "9.5.3.3", [c.Id]));
            }
            if (a.Habitacao)
            {
                var pesados = pontos.Where(p => CorrenteDoPonto(p, c, uo, uff) > Nbr5410.CorrenteParaCircuitoProprio).ToList();
                if (pesados.Count > 0 && pontos.Count > 1)
                    problemas.Add(new Problema("C-03", Gravidade.Erro, $"Circuito {c.Numero}: {pesados[0].Tipo} ({pesados[0].Id}, {CorrenteDoPonto(pesados[0], c, uo, uff):0.0} A) divide o circuito com outros pontos; acima de 10 A pede circuito independente.", "9.5.3.1", [c.Id, .. pesados.Select(p => p.Id)]));
                var usos = pontos.Where(EhTomada).Select(p => i.AmbienteDe(p).Uso).Distinct().ToList();
                var cozinha = usos.Any(u => u is UsoAmbiente.Cozinha or UsoAmbiente.Copa or UsoAmbiente.AreaServico or UsoAmbiente.Lavanderia);
                var outros = usos.Any(u => u is not (UsoAmbiente.Cozinha or UsoAmbiente.Copa or UsoAmbiente.AreaServico or UsoAmbiente.Lavanderia));
                if (cozinha && outros)
                    problemas.Add(new Problema("C-04", Gravidade.Erro, $"Circuito {c.Numero} tem tomadas de cozinha/área de serviço junto com tomadas de outros cômodos; esses locais pedem circuitos exclusivos.", "9.5.3.2", [c.Id]));
            }
            if (c.Fases > 1 && c.TemNeutro is false && c.Ligacao == Ligacao.FF && pontos.Any(p => p.Tipo == TipoCarga.TomadaUsoGeral))
                problemas.Add(new Problema("C-05", Gravidade.Aviso, $"Circuito {c.Numero} de tomadas de uso geral em fase-fase: tomadas de 220 V num prédio 127 V precisam de marcação de tensão no espelho.", "6.5.3.2", [c.Id]));
        }

        // ----- quadros (Tabela 59, DPS) -----
        foreach (var q in i.Quadros)
        {
            var n = i.CircuitosDo(q).Count();
            if (n > 0)
                problemas.Add(new Problema("Q-01", Gravidade.Nota, $"Quadro {q.Nome}: {n} circuito(s); prever {Nbr5410.ReservaDoQuadro(n)} espaço(s) de reserva e considerar a reserva no alimentador.", "6.5.4.7, Tabela 59", [q.Id]));
        }
        if (DispositivosDPS.MotivoDeObrigatoriedade(a) is { } motivoDps)
            problemas.Add(new Problema("Q-02", Gravidade.Nota, $"DPS obrigatório no quadro de entrada: {motivoDps}.", "5.4.2.1.1, 6.3.5.2", [i.QuadroDeEntrada.Id]));
        if (a.Esquema == EsquemaAterramento.TNC)
            problemas.Add(new Problema("Q-03", Gravidade.Erro, "Esquema TN-C na instalação: a distribuidora entrega PEN, mas a 5410 obriga separar N e PE na entrada (TN-C-S) e proíbe DR em TN-C.", "5.4.3.6, 5.1.2.2.4.2 f)", [i.QuadroDeEntrada.Id]));
        if (a.Habitacao)
            problemas.Add(new Problema("Q-04", Gravidade.Nota, "Quadros residenciais saem com a advertência padrão (não trocar disjuntor por maior; não desativar o DR).", "6.5.4.10", [i.QuadroDeEntrada.Id]));

        return problemas;
    }

    static bool EhTomada(Ponto p) => p.Tipo is TipoCarga.TomadaUsoGeral or TipoCarga.TomadaUsoEspecifico;

    static double CorrenteDoPonto(Ponto p, Circuito c, double uo, double uff) =>
        Eletrotecnica.CorrenteDeProjeto(p.PotenciaVA, c.Ligacao, uo, uff, c.FatorDePotencia, c.Rendimento);

    static string Descrever(UsoAmbiente uso) => uso switch
    {
        UsoAmbiente.Sala => "sala",
        UsoAmbiente.Dormitorio => "dormitório",
        UsoAmbiente.Cozinha => "cozinha",
        UsoAmbiente.Copa => "copa",
        UsoAmbiente.AreaServico => "área de serviço",
        UsoAmbiente.Lavanderia => "lavanderia",
        UsoAmbiente.Banheiro => "banheiro",
        UsoAmbiente.Lavabo => "lavabo",
        UsoAmbiente.Varanda => "varanda",
        UsoAmbiente.Circulacao => "circulação",
        UsoAmbiente.Garagem => "garagem",
        UsoAmbiente.AreaExterna => "área externa",
        UsoAmbiente.Deposito => "depósito",
        UsoAmbiente.Escritorio => "escritório",
        UsoAmbiente.CasaDeMaquinas => "casa de máquinas",
        _ => "cômodo",
    };
}
