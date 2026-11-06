using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Exemplos;

/// <summary>
/// Apartamento de dois dormitórios, 127/220 V bifásico, TN-C-S, com os pontos pela seção 9.5 da NBR 5410 e um traçado de
/// eletrodutos como o de obra: dois troncos saindo do quadro (área social e área molhada), o chuveiro em eletroduto
/// próprio e descidas para cada ponto. Serve de caso de teste e de exemplo da linha de comando.
/// </summary>
public sealed class ApartamentoExemplo
{
    public double IccPresumidaKA { get; init; } = 6;
    public bool LinhaAerea { get; init; } = true;
    public int DiasDeTrovoada { get; init; } = 40;

    public Instalacao Criar()
    {
        var alimentacao = new Alimentacao(127, 220, 2, EsquemaAterramento.TNCS)
        {
            IccPresumidaKA = IccPresumidaKA,
            LinhaAerea = LinhaAerea,
            DiasDeTrovoada = DiasDeTrovoada,
            Habitacao = true,
        };

        var ambientes = new[]
        {
            new Ambiente("sala", "Sala de estar/jantar", UsoAmbiente.Sala, 18.0, 17.2),
            new Ambiente("coz", "Cozinha", UsoAmbiente.Cozinha, 8.5, 12.0) { Bancada = 1.8 },
            new Ambiente("as", "Área de serviço", UsoAmbiente.AreaServico, 3.6, 8.0),
            new Ambiente("bwc", "Banheiro", UsoAmbiente.Banheiro, 3.2, 7.4),
            new Ambiente("d1", "Dormitório 1", UsoAmbiente.Dormitorio, 11.0, 13.4),
            new Ambiente("d2", "Dormitório 2", UsoAmbiente.Dormitorio, 8.4, 11.8),
            new Ambiente("circ", "Circulação", UsoAmbiente.Circulacao, 2.4, 6.4),
            new Ambiente("var", "Varanda", UsoAmbiente.Varanda, 3.0, 7.0),
        };

        var quadros = new[] { new Quadro("QD", "QD — Quadro de distribuição", 2) { No = "QD" } };

        var circuitos = new[]
        {
            new Circuito("C1", "1", "QD", TipoCarga.Iluminacao, Ligacao.FN) { Descricao = "Iluminação" },
            new Circuito("C2", "2", "QD", TipoCarga.TomadaUsoGeral, Ligacao.FN) { Descricao = "TUG sala, dormitórios, circulação e varanda" },
            new Circuito("C3", "3", "QD", TipoCarga.TomadaUsoGeral, Ligacao.FN) { Descricao = "TUG cozinha" },
            new Circuito("C4", "4", "QD", TipoCarga.TomadaUsoGeral, Ligacao.FN) { Descricao = "TUG área de serviço" },
            new Circuito("C5", "5", "QD", TipoCarga.Chuveiro, Ligacao.FF) { Descricao = "Chuveiro elétrico" },
            new Circuito("C6", "6", "QD", TipoCarga.ArCondicionado, Ligacao.FF) { Descricao = "Ar-condicionado dormitório 1", FatorDePotencia = 0.85 },
            new Circuito("C7", "7", "QD", TipoCarga.TomadaUsoEspecifico, Ligacao.FF) { Descricao = "Micro-ondas (TUE cozinha)" },
            new Circuito("C8", "8", "QD", TipoCarga.TomadaUsoGeral, Ligacao.FN) { Descricao = "TUG banheiro" },
        };

        var pontos = new List<Ponto>
        {
            // iluminação (9.5.2.1.2: 100 VA até 6 m², +60 VA por 4 m² inteiros a mais)
            Luz("L-sala", "sala", 280), Luz("L-coz", "coz", 100), Luz("L-as", "as", 100), Luz("L-bwc", "bwc", 100),
            Luz("L-d1", "d1", 160), Luz("L-d2", "d2", 100), Luz("L-circ", "circ", 100), Luz("L-var", "var", 100),
            // TUG sala (17,2 m / 5 = 4 pontos), dormitórios (3 e 3), circulação (1), varanda (1)
            Tug("T-sala-1", "sala", "C2"), Tug("T-sala-2", "sala", "C2"), Tug("T-sala-3", "sala", "C2"), Tug("T-sala-4", "sala", "C2"),
            Tug("T-d1-1", "d1", "C2"), Tug("T-d1-2", "d1", "C2"), Tug("T-d1-3", "d1", "C2"),
            Tug("T-d2-1", "d2", "C2"), Tug("T-d2-2", "d2", "C2"), Tug("T-d2-3", "d2", "C2"),
            Tug("T-circ", "circ", "C2"), Tug("T-var", "var", "C2"),
            // cozinha: 12 m / 3,5 = 4 pontos, 600 VA nos três primeiros, duas sobre a bancada
            Tug("T-coz-1", "coz", "C3", 600, 1.2), Tug("T-coz-2", "coz", "C3", 600, 1.2), Tug("T-coz-3", "coz", "C3", 600, 0.3), Tug("T-coz-4", "coz", "C3", 100, 0.3),
            // área de serviço: 8 m / 3,5 = 3 pontos; banheiro: 1 junto ao lavatório (volume 3), em circuito próprio
            Tug("T-as-1", "as", "C4", 600, 1.2), Tug("T-as-2", "as", "C4", 600, 1.2), Tug("T-as-3", "as", "C4", 600, 0.3),
            Tug("T-bwc", "bwc", "C8", 600, 1.2) with { VolumeDoBanheiro = 3 },
            // equipamentos
            new Ponto("CH", "bwc", "C5", TipoCarga.Chuveiro, 5500) { Altura = 2.2, VolumeDoBanheiro = 1, No = "CH" },
            new Ponto("AC-d1", "d1", "C6", TipoCarga.ArCondicionado, 1400) { Altura = 2.1, No = "AC-d1" },
            new Ponto("MO", "coz", "C7", TipoCarga.TomadaUsoEspecifico, 1500) { Altura = 1.2, CorrenteDaTomada = 20, No = "MO" },
        };

        // Traçado: o QD fica na circulação. Tronco A pelo teto para a área social (sala, dormitórios, varanda), tronco B para a
        // área molhada (cozinha, área de serviço, banheiro); o chuveiro vai direto do QD ao banheiro. Cada ponto desce da caixa
        // de teto do seu ambiente por um trecho curto.
        var trechos = new List<Trecho>
        {
            T("QD-circA", "QD", "cxA-circ", 2.0, ["C1", "C2", "C6"], 25, 1),
            T("QD-circB", "QD", "cxB-circ", 2.0, ["C1", "C3", "C4", "C7", "C8"], 40, 1),
            T("QD-bwc", "QD", "CH", 6.0, ["C5"], 20, 2),
            T("circ-sala", "cxA-circ", "cx-sala", 3.0, ["C1", "C2"], 20, 1),
            T("circ-d1", "cxA-circ", "cx-d1", 4.0, ["C1", "C2", "C6"], 25, 1),
            T("d1-d2", "cx-d1", "cx-d2", 3.5, ["C1", "C2"], 20, 1),
            T("sala-var", "cx-sala", "cx-var", 4.5, ["C1", "C2"], 20, 1),
            T("circ-coz", "cxB-circ", "cx-coz", 3.5, ["C1", "C3", "C4", "C7", "C8"], 40, 1),
            T("coz-as", "cx-coz", "cx-as", 2.5, ["C1", "C4"], 25, 1),
            T("coz-bwc", "cx-coz", "cx-bwc", 2.0, ["C1", "C8"], 20, 0),
            T("circA-circ", "cxA-circ", "cx-circ", 0.5, ["C1", "C2"], 20, 0),
        };
        foreach (var p in pontos)
        {
            if (p.Id == "CH") continue;
            var caixa = "cx-" + p.Ambiente;
            trechos.Add(T("desc-" + p.Id, caixa, p.NoDoPonto, p.Tipo == TipoCarga.Iluminacao ? 0.5 : 3.0, [p.Circuito!], 20, p.Tipo == TipoCarga.Iluminacao ? 0 : 1));
        }

        return new Instalacao("Apartamento tipo — 2 dormitórios", alimentacao, ambientes, quadros, circuitos, pontos, trechos);
    }

    static Ponto Luz(string id, string ambiente, double va) => new(id, ambiente, "C1", TipoCarga.Iluminacao, va) { Altura = 2.6, NoTeto = true, No = id };

    static Ponto Tug(string id, string ambiente, string circuito, double va = 100, double altura = 0.3) =>
        new(id, ambiente, circuito, TipoCarga.TomadaUsoGeral, va) { Altura = altura, CorrenteDaTomada = 10, No = id };

    static Trecho T(string id, string de, string para, double l, string[] circuitos, int dn, int curvas) =>
        new(id, de, para, l, circuitos) { Dn = dn, Curvas90 = curvas, Metodo = MetodoReferencia.B1, TipoEletroduto = TipoEletroduto.FlexivelCorrugado };
}
