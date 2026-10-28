using Autodesk.Revit.DB.Plumbing;
using Spk.Motor.Calculo;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.AddIn.Modelagem;

/// <summary>Como gerar a rede num ambiente: o arranjo, o drop do bico até o ramal, os diâmetros e as opções dos bicos.</summary>
public sealed record OpcoesRede
{
    /// <summary>Espinha de peixe (subgeral no meio) ou lateral (subgeral junto à parede).</summary>
    public Arranjo Arranjo { get; init; } = Arranjo.EspinhaDePeixe;

    /// <summary>Do defletor até o ramal, em m.</summary>
    public double DropM { get; init; } = 0.3;

    public bool AplicarDiametros { get; init; } = true;
    public OpcoesBicos Bicos { get; init; } = new();
}

/// <summary>A rede de um ambiente: bicos, ramais, diâmetros, ou por que não foi gerada.</summary>
public sealed record RedeNoAmbiente(Ambiente Ambiente, string? Recusa, int Bicos, double? AlturaDaRede, ResultadoRamais? Ramais,
    ResultadoDiametros? Diametros, IReadOnlyList<string> Avisos)
{
    public string Texto()
    {
        var nome = $"{Ambiente.Numero} {Ambiente.Nome}".Trim();
        if (Recusa is not null) return $"{nome}: {Recusa}";
        var linhas = new List<string>
        {
            $"{nome}: {Bicos} bicos, rede a {AlturaDaRede:0.00} m, {Ramais?.Tracados ?? 0} ramal(is), {Ramais?.Ligados ?? 0} bico(s) ligado(s)" +
            (Diametros is { } d ? "; " + string.Join(", ", d.TubosPorDn.OrderBy(p => p.Key).Select(p => $"DN {p.Key}: {p.Value}")) : ""),
        };
        linhas.AddRange((Ramais?.Recusados ?? []).GroupBy(r => r).Select(g => $"   • {g.Key}{(g.Count() > 1 ? $" ({g.Count()})" : "")}"));
        linhas.AddRange(Avisos.Select(a => "   • " + a));
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Gera a rede de cada ambiente com as ferramentas de modelagem, na ordem em que o engenheiro faria: bicos pela norma
///     (<see cref="BicosPorAmbiente" />), a subgeral cruzando as fileiras (no meio, na espinha de peixe; junto à parede, no
///     lateral) com a descida até o piso na entrada, os ramais e a ligação dos bicos (<see cref="TracarRamal" />) e os
///     diâmetros pela tabela (<see cref="AplicarDiametros" />). A rede fica a <see cref="OpcoesRede.DropM" /> acima do
///     defletor, sem passar da laje ou do telhado. Cada ambiente num grupo: se uma etapa recusa, o ambiente volta inteiro.
///     Ambientes de paredes retas; num ambiente em L a subgeral pode sair dele, e isso vira aviso.
/// </summary>
static class GerarRede
{
    static readonly double FolgaDoTeto = 0.10;
    static readonly double DropMinimo = 0.15;

    public static IReadOnlyList<RedeNoAmbiente> Executar(Document doc, DadosProjeto dados, Edificio edificio, IEnumerable<Ambiente> ambientes,
        OpcoesRede opcoes, List<ElementId> criados)
    {
        var limites = BicosPorAmbiente.Limites(dados, opcoes.Bicos);
        var resultados = new List<RedeNoAmbiente>();
        foreach (var ambiente in ambientes)
        {
            using var grupo = new TransactionGroup(doc, "SPK — Gerar rede no ambiente");
            grupo.Start();
            var deste = new List<ElementId>();
            var rede = Gerar(doc, dados, edificio, ambiente, limites, opcoes, deste);
            if (rede.Recusa is not null)
            {
                grupo.RollBack();
            }
            else
            {
                grupo.Assimilate();
                criados.AddRange(deste.Where(id => doc.GetElement(id) is not null));
            }
            resultados.Add(rede);
        }
        return resultados;
    }

    static RedeNoAmbiente Gerar(Document doc, DadosProjeto dados, Edificio edificio, Ambiente ambiente, LimitesDeBico limites,
        OpcoesRede opcoes, List<ElementId> criados)
    {
        RedeNoAmbiente Recusa(string motivo) => new(ambiente, motivo, 0, null, null, null, []);

        var bicos = BicosPorAmbiente.Posicionar(doc, edificio, [ambiente], limites, opcoes.Bicos, criados).Single();
        if (bicos.Recusa is not null) return Recusa(bicos.Recusa);
        if (bicos.Posicao is not { Bicos.Count: > 0 } posicao || bicos.Defletor is not { } defletor) return Recusa("nenhum bico coube no ambiente");

        // Altura da rede: o drop acima do defletor, sem encostar na laje ou no telhado (o forro a rede atravessa por cima).
        var avisos = new List<string>();
        var zRede = defletor + opcoes.DropM;
        if (ambiente.OrigemDoTeto is OrigemDoTeto.Laje or OrigemDoTeto.Telhado && ambiente.Teto is { } teto && zRede > teto - FolgaDoTeto)
        {
            zRede = teto - FolgaDoTeto;
            if (zRede - defletor < DropMinimo)
                return Recusa($"sem espaço para a rede entre o defletor ({defletor:0.00}) e o teto ({teto:0.00}): " +
                              "a rede precisa de pelo menos 15 cm acima do defletor e 10 cm abaixo do teto");
            avisos.Add($"rede baixada para {zRede:0.00} m, 10 cm abaixo do teto");
        }
        foreach (var viga in edificio.Vigas.Where(v => v.Fundo < zRede + 0.05 && v.Topo > zRede - 0.05).Take(3))
            if (Cruza(ambiente, viga)) avisos.Add($"a rede, a {zRede:0.00} m, passa na altura da viga (fundo {viga.Fundo:0.00}): confira a travessia");

        var bicosDoAmbiente = criados.Select(doc.GetElement).OfType<FamilyInstance>().ToList();
        var subgeral = new List<Pipe>();
        var motivo = TransacaoSegura.Executar(doc, "SPK — Subgeral", () => Subgeral(doc, ambiente, posicao, zRede, opcoes.Arranjo, criados, avisos, subgeral));
        if (motivo is not null) return Recusa(motivo);

        var ramais = TracarRamal.Executar(doc, bicosDoAmbiente, subgeral[0], new OpcoesRamais());
        criados.AddRange(ramais.Criados);
        if (ramais.Tracados == 0) return Recusa("nenhum ramal traçado: " + string.Join("; ", ramais.Fileiras.Select(f => f.Recusa).Distinct()));

        ResultadoDiametros? diametros = null;
        if (opcoes.AplicarDiametros && TabelaDeDiametros.TemTabela(dados.Classe))
        {
            try
            {
                var leitura = BicosDoModelo.Ler(doc);
                // O setor é o de um bico ligado: um bico que ficou solto não está em setor nenhum.
                var ligado = bicosDoAmbiente.FirstOrDefault(b => b.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().Any(c => c.IsConnected) == true)
                             ?? bicosDoAmbiente[0];
                var lida = LeitorRede.Montar(doc, leitura.Bicos, dados.FatorC, null, [], ligado.Id);
                diametros = AplicarDiametros.Executar(doc, lida, DimensionamentoPorTabela.Calcular(lida.Rede, dados.Classe));
                avisos.AddRange(diametros.Recusados.Distinct());
            }
            catch (InvalidOperationException e)
            {
                avisos.Add("diâmetros não aplicados: " + e.Message);
            }
        }
        if (Tampar(doc, criados, zRede) is { } semTampao) avisos.Add(semTampao);
        return new RedeNoAmbiente(ambiente, null, bicosDoAmbiente.Count, zRede, ramais, diametros, avisos);
    }

    /// <summary>
    /// A subgeral cruza as fileiras ao longo do eixo entre ramais (v): na espinha de peixe, entre as duas colunas de bicos
    /// do meio; no lateral, entre a parede e a primeira coluna. Começa 30 cm da parede de menor v, onde fica a descida até o
    /// piso (a entrada do setor), e termina 20 cm depois da última fileira (o mínimo para o tê).
    /// </summary>
    static string? Subgeral(Document doc, Ambiente ambiente, Spk.Motor.Geracao.PosicaoDeBicos posicao, double zRede, Arranjo arranjo,
        List<ElementId> criados, List<string> avisos, List<Pipe> subgeral)
    {
        var u = posicao.DirecaoDosRamais;
        var v = new Ponto2(-u.Y, u.X);
        double U(Ponto2 p) => p.X * u.X + p.Y * u.Y;
        double V(Ponto2 p) => p.X * v.X + p.Y * v.Y;
        var colunas = Agrupar(posicao.Bicos.Select(U));
        var fileiras = Agrupar(posicao.Bicos.Select(V));
        var contorno = ambiente.Contornos[0];
        var (u0, v0, v1) = (contorno.Min(U), contorno.Min(V), contorno.Max(V));

        double uSub;
        if (arranjo == Arranjo.EspinhaDePeixe && colunas.Count >= 2)
        {
            var k = colunas.Count / 2;
            uSub = (colunas[k - 1] + colunas[k]) / 2;
        }
        else
        {
            if (arranjo == Arranjo.EspinhaDePeixe) avisos.Add("uma coluna de bicos só: subgeral junto à parede (lateral)");
            uSub = (u0 + colunas[0]) / 2;
        }
        var vInicio = v0 + 0.30;
        var vFim = Math.Min(fileiras[^1] + 0.20, v1 - 0.10);
        if (fileiras[0] - vInicio < 0.20) return "a primeira fileira de bicos fica colada à parede da entrada: não cabe o tê da subgeral";

        Ponto2 P(double a, double b) => new(u.X * a + v.X * b, u.Y * a + v.Y * b);
        var inicio = P(uSub, vInicio);
        var fim = P(uSub, vFim);
        for (var k = 0; k <= 20; k++)
            if (!Spk.Motor.Geracao.BicosNoAmbiente.Dentro(ambiente.Contornos,
                    new Ponto2(inicio.X + (fim.X - inicio.X) * k / 20, inicio.Y + (fim.Y - inicio.Y) * k / 20)))
            {
                avisos.Add("a subgeral sai do ambiente (ambiente que não é retângulo): confira o traçado");
                break;
            }

        var nivel = BicosPorAmbiente.Nivel(doc, Pes(ambiente.Piso));
        var tipo = GeradorMalha.TipoComTe(doc);
        var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
        var esboco = DadosEsboco.Carregar(doc);
        var diametro = GeradorMalha.DiametroComTe(doc, tipo, esboco.DnSubgeral > 0 ? esboco.DnSubgeral : esboco.DnTubo);
        XYZ Xyz(Ponto2 p, double z) => new(Pes(p.X), Pes(p.Y), Pes(z));
        var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, Xyz(inicio, ambiente.Piso), Xyz(inicio, zRede), diametro, marcar: false);
        var tubo = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, Xyz(inicio, zRede), Xyz(fim, zRede), diametro, marcar: false);
        subgeral.Add(tubo);
        criados.Add(descida.Id);
        criados.Add(tubo.Id);
        doc.Regenerate();
        GeradorMalha.Ligar(doc, [descida, tubo], [], nivel);
        return null;
    }

    /// <summary>
    /// Tampão nas pontas abertas da rede do ambiente na altura da rede (a ponta da subgeral depois da última fileira),
    /// como na obra. O pé da descida fica aberto: é a entrada do setor. O tampão vem das preferências de roteamento do
    /// tipo de tubo; sem ele, a ponta fica aberta e o aviso diz por quê.
    /// </summary>
    static string? Tampar(Document doc, List<ElementId> criados, double zRede)
    {
        var abertos = criados.Select(doc.GetElement).OfType<Pipe>()
            .Where(t => t.ConnectorManager.Connectors.Cast<Connector>()
                .Any(c => c.ConnectorType == ConnectorType.End && !c.IsConnected && c.Origin.Z > Pes(zRede - 0.05)))
            .ToList();
        if (abertos.Count == 0) return null;
        return TransacaoSegura.Executar(doc, "SPK — Tampão", () =>
        {
            foreach (var tubo in abertos) PlumbingUtils.PlaceCapOnOpenEnds(doc, tubo.Id, ElementId.InvalidElementId);
            return null;
        }) is { } erro
            ? "ponta da subgeral sem tampão (o tipo de tubo não tem tampão nas preferências de roteamento): " + erro
            : null;
    }

    /// <summary>Posições distintas (a menos de 30 cm viram uma), em ordem.</summary>
    static List<double> Agrupar(IEnumerable<double> valores)
    {
        var grupos = new List<List<double>>();
        foreach (var x in valores.OrderBy(x => x))
            if (grupos.Count > 0 && x - grupos[^1][^1] < 0.30) grupos[^1].Add(x);
            else grupos.Add([x]);
        return grupos.Select(g => g.Average()).ToList();
    }

    static bool Cruza(Ambiente ambiente, Viga viga) =>
        Enumerable.Range(0, 21).Any(k => Spk.Motor.Geracao.BicosNoAmbiente.Dentro(ambiente.Contornos,
            new Ponto2(viga.A.X + (viga.B.X - viga.A.X) * k / 20, viga.A.Y + (viga.B.Y - viga.A.Y) * k / 20)));

    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
}
