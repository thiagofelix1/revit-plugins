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

    /// <summary>
    /// Ambiente por onde passa a geral (o Id dele, ou parte do nome): as salas entram num setor só, com a geral pelo eixo
    /// dele. Nulo: cada sala com a sua entrada (a descida até o piso).
    /// </summary>
    public string? GeralPor { get; init; }
}

/// <summary>A geral gerada pelo corredor e a ligação das salas a ela, ou por que não foi feita.</summary>
public sealed record GeralGerada(Ambiente Corredor, string? Recusa, ResultadoGeral? Ligacao, ResultadoDiametros? Diametros, IReadOnlyList<string> Avisos)
{
    public string Texto()
    {
        var nome = $"{Corredor.Numero} {Corredor.Nome}".Trim();
        if (Recusa is not null) return $"Geral por {nome}: {Recusa}";
        var linhas = new List<string> { $"Geral por {nome}: " + (Ligacao?.Texto().Replace("\n", "\n   ") ?? "") };
        if (Diametros is { } d)
            linhas.Add("   Diâmetros do setor: " + string.Join(", ", d.TubosPorDn.OrderBy(p => p.Key).Select(p => $"DN {p.Key}: {p.Value}")));
        linhas.AddRange(Avisos.Select(a => "   • " + a));
        return string.Join("\n", linhas);
    }
}

/// <summary>A rede de um ambiente: bicos, ramais, diâmetros, ou por que não foi gerada.</summary>
public sealed record RedeNoAmbiente(Ambiente Ambiente, string? Recusa, int Bicos, double? AlturaDaRede, ResultadoRamais? Ramais,
    ResultadoDiametros? Diametros, IReadOnlyList<string> Avisos)
{
    /// <summary>Altura da subgeral (a entrada da sala), quando não é a dos ramais: por baixo das vigas fundas.</summary>
    public double? AlturaDaSubgeral { get; init; }

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
        OpcoesRede opcoes, List<ElementId> criados) =>
        Executar(doc, dados, edificio, ambientes, opcoes, criados, null);

    /// <summary>Reta em planta (m): de A até B, na direção unitária A→B.</summary>
    internal sealed record Eixo(Ponto2 A, Ponto2 B)
    {
        public Ponto2 Direcao
        {
            get
            {
                var (dx, dy) = (B.X - A.X, B.Y - A.Y);
                var c = Math.Sqrt(dx * dx + dy * dy);
                return new Ponto2(dx / c, dy / c);
            }
        }
    }

    /// <summary>
    /// As salas num setor só: a geral pelo eixo do ambiente pedido (o trecho reto mais comprido que cabe nele, longe das
    /// paredes), com a descida da entrada na ponta A; em cada sala, os ramais paralelos à geral e a entrada na parede do
    /// lado dela; depois Ligar à geral e os diâmetros do setor inteiro. O corredor fica sem bicos (vira aviso).
    /// </summary>
    public static (IReadOnlyList<RedeNoAmbiente> Redes, GeralGerada? Geral) ExecutarComGeral(Document doc, DadosProjeto dados, Edificio edificio,
        IEnumerable<Ambiente> ambientes, OpcoesRede opcoes, List<ElementId> criados)
    {
        var corredor = opcoes.GeralPor is not { } alvo ? null
            : edificio.Ambientes.FirstOrDefault(a => a.Id == alvo)
              ?? edificio.Ambientes.FirstOrDefault(a => a.Nome.Contains(alvo, StringComparison.CurrentCultureIgnoreCase));
        var salas = ambientes.Where(a => a.Id != corredor?.Id).ToList();
        if (corredor is null) return (Executar(doc, dados, edificio, salas, opcoes, criados, null), null);
        GeralGerada Recusa(string motivo) => new(corredor, motivo, null, null, []);

        if (EixoDoCorredor(corredor) is not { } eixo)
            return (Executar(doc, dados, edificio, salas, opcoes, criados, null), Recusa("o ambiente é estreito demais para a geral (menos de 60 cm entre as paredes)"));

        var redes = Executar(doc, dados, edificio, salas, opcoes, criados, eixo);
        var feitas = redes.Where(r => r.Recusa is null && r.AlturaDaRede is not null).ToList();
        if (feitas.Count == 0) return (redes, Recusa("nenhuma sala ganhou rede"));

        // Altura: a das redes das salas (a mais alta), sem encostar na laje do corredor.
        var avisos = new List<string> { "o corredor ficou sem bicos: ponha com Bicos por ambiente e ligue à geral com Ligar bicos ou Traçar ramais" };
        var z = feitas.Max(r => r.AlturaDaSubgeral ?? r.AlturaDaRede!.Value);
        if (corredor.OrigemDoTeto is OrigemDoTeto.Laje or OrigemDoTeto.Telhado && corredor.Teto is { } teto && z > teto - FolgaDoTeto)
        {
            z = teto - FolgaDoTeto;
            avisos.Add($"geral baixada para {z:0.00} m, 10 cm abaixo do teto do corredor");
        }

        var dasSalas = feitas.Select(r => criados.Select(doc.GetElement).OfType<FamilyInstance>()
                .FirstOrDefault(b => b.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers && Dentro(r.Ambiente, b)))
            .OfType<FamilyInstance>().Select(b => LigarAGeral.Rede(doc, b)).ToList();

        // A geral vai da descida (ponta A) até 50 cm depois da entrada da última sala, e não até o fim do corredor:
        // depois do último tê não passa água.
        var d = eixo.Direcao;
        double Ao(Ponto2 p) => (p.X - eixo.A.X) * d.X + (p.Y - eixo.A.Y) * d.Y;
        var entradas = dasSalas.Select(r => LigarAGeral.Entrada(doc, r)).OfType<LigarAGeral.EntradaDaRede>()
            .Select(e => Ao(new Ponto2(Metros(e.Ponto.X), Metros(e.Ponto.Y)))).ToList();
        var comprimento = Ao(eixo.B);
        if (entradas.Count > 0 && entradas.Max() + 0.5 < comprimento)
        {
            comprimento = Math.Max(entradas.Max() + 0.5, 0.5);
            eixo = eixo with { B = new Ponto2(eixo.A.X + d.X * comprimento, eixo.A.Y + d.Y * comprimento) };
        }

        using var grupo = new TransactionGroup(doc, "SPK — Geral pelo corredor");
        grupo.Start();
        var geral = new List<Pipe>();
        var motivo = TransacaoSegura.Executar(doc, "SPK — Geral", () =>
        {
            var nivel = BicosPorAmbiente.Nivel(doc, Pes(corredor.Piso));
            var tipo = GeradorMalha.TipoComTe(doc);
            var sistema = GeradorMalha.Sistema(doc) ?? throw new InvalidOperationException("O projeto não tem sistema de tubulação.");
            var esboco = DadosEsboco.Carregar(doc);
            var diametro = GeradorMalha.DiametroComTe(doc, tipo, esboco.DnSubgeral > 0 ? esboco.DnSubgeral : esboco.DnTubo);
            XYZ Xyz(Ponto2 p, double zM) => new(Pes(p.X), Pes(p.Y), Pes(zM));
            var descida = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, Xyz(eixo.A, corredor.Piso), Xyz(eixo.A, z), diametro, marcar: false);
            var tubo = GeradorMalha.CriarTubo(doc, sistema.Id, tipo.Id, nivel.Id, Xyz(eixo.A, z), Xyz(eixo.B, z), diametro, marcar: false);
            doc.Regenerate();
            GeradorMalha.Ligar(doc, [descida, tubo], [], nivel);
            geral.Add(tubo);
            criados.Add(descida.Id);
            criados.Add(tubo.Id);
            return null;
        });
        if (motivo is not null)
        {
            grupo.RollBack();
            return (redes, Recusa(motivo));
        }

        var ligacao = LigarAGeral.Executar(doc, geral, dasSalas);
        criados.AddRange(ligacao.Criados);

        ResultadoDiametros? diametros = null;
        if (opcoes.AplicarDiametros && TabelaDeDiametros.TemTabela(dados.Classe) && ligacao.Ligadas > 0)
        {
            try
            {
                var leitura = BicosDoModelo.Ler(doc);
                var umBico = dasSalas.SelectMany(r => r).Select(doc.GetElement).OfType<FamilyInstance>()
                    .First(b => b.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers);
                var lida = LeitorRede.Montar(doc, leitura.Bicos, dados.FatorC, null, [], umBico.Id);
                diametros = AplicarDiametros.Executar(doc, lida, DimensionamentoPorTabela.Calcular(lida.Rede, dados.Classe));
                avisos.AddRange(diametros.Recusados.Distinct());
            }
            catch (InvalidOperationException e)
            {
                avisos.Add("diâmetros do setor não aplicados: " + e.Message);
            }
        }
        grupo.Assimilate();
        return (redes, new GeralGerada(corredor, null, ligacao, diametros, avisos));
    }

    static bool Dentro(Ambiente ambiente, FamilyInstance bico) =>
        bico.Location is LocationPoint lp
        && Spk.Motor.Geracao.BicosNoAmbiente.Dentro(ambiente.Contornos,
            new Ponto2(UnitUtils.ConvertFromInternalUnits(lp.Point.X, UnitTypeId.Meters), UnitUtils.ConvertFromInternalUnits(lp.Point.Y, UnitTypeId.Meters)));

    /// <summary>
    /// O eixo da geral no corredor: na direção da parede mais comprida, a reta (afastada pelo menos 30 cm das paredes)
    /// com o trecho contínuo dentro do ambiente mais comprido; no empate, a mais perto do meio. As pontas ficam 30 cm
    /// antes das paredes.
    /// </summary>
    static Eixo? EixoDoCorredor(Ambiente corredor)
    {
        var laco = corredor.Contornos[0];
        var (maior, d) = (0.0, new Ponto2(1, 0));
        for (var i = 0; i < laco.Count; i++)
        {
            var (a, b) = (laco[i], laco[(i + 1) % laco.Count]);
            var c = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            if (c > maior) (maior, d) = (c, new Ponto2((b.X - a.X) / c, (b.Y - a.Y) / c));
        }
        var n = new Ponto2(-d.Y, d.X);
        double U(Ponto2 p) => p.X * d.X + p.Y * d.Y;
        double N(Ponto2 p) => p.X * n.X + p.Y * n.Y;
        Ponto2 P(double u, double v) => new(d.X * u + n.X * v, d.Y * u + n.Y * v);
        var (u0, u1, n0, n1) = (laco.Min(U), laco.Max(U), laco.Min(N), laco.Max(N));
        bool Livre(Ponto2 p) => Spk.Motor.Geracao.BicosNoAmbiente.Dentro(corredor.Contornos, p)
                                && Spk.Motor.Geracao.BicosNoAmbiente.DistanciaAoContorno(corredor.Contornos, p) >= 0.30 - 1e-9;

        (double Comprimento, double Meio, Eixo Eixo)? melhor = null;
        for (var v = n0 + 0.30; v <= n1 - 0.30 + 1e-9; v += 0.05)
        {
            double? inicio = null;
            for (var u = u0; u <= u1 + 1e-9; u += 0.10)
            {
                var livre = Livre(P(u, v));
                if (livre && inicio is null) inicio = u;
                if ((!livre || u + 0.10 > u1 + 1e-9) && inicio is { } i0)
                {
                    var fim = livre ? u : u - 0.10;
                    var comprimento = fim - i0;
                    var meio = Math.Abs(v - (n0 + n1) / 2);
                    if (comprimento > 0.5 && (melhor is not { } m || comprimento > m.Comprimento + 0.05
                                              || Math.Abs(comprimento - m.Comprimento) <= 0.05 && meio < m.Meio))
                        melhor = (comprimento, meio, new Eixo(P(i0, v), P(fim, v)));
                    inicio = null;
                }
            }
        }
        return melhor?.Eixo;
    }

    static IReadOnlyList<RedeNoAmbiente> Executar(Document doc, DadosProjeto dados, Edificio edificio, IEnumerable<Ambiente> ambientes,
        OpcoesRede opcoes, List<ElementId> criados, Eixo? geral)
    {
        var limites = BicosPorAmbiente.Limites(dados, opcoes.Bicos);
        var resultados = new List<RedeNoAmbiente>();
        foreach (var ambiente in ambientes)
        {
            using var grupo = new TransactionGroup(doc, "SPK — Gerar rede no ambiente");
            grupo.Start();
            var deste = new List<ElementId>();
            var rede = Gerar(doc, dados, edificio, ambiente, limites, opcoes, deste, geral);
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
        OpcoesRede opcoes, List<ElementId> criados, Eixo? geral)
    {
        RedeNoAmbiente Recusa(string motivo) => new(ambiente, motivo, 0, null, null, null, []);

        // Com geral, os ramais correm paralelos a ela: a subgeral sai perpendicular e aponta direto para a geral.
        var opcoesBicos = geral is null ? opcoes.Bicos : opcoes.Bicos with { Rumo = geral.Direcao };
        // Viga funda: bicos nos vãos. Vigas numa direção só: o ramal corre no vão, entre a laje e o defletor, que desce até
        // 25 cm da laje (o máximo sob teto liso é 30 cm) para caber o ramal 10 cm abaixo da laje e 15 cm acima do bico.
        // Grelha (vigas nas duas direções): o ramal no vão bateria nas vigas que o cruzam; a rede inteira passa por baixo
        // das vigas e sobe até cada bico, que tem de ser em pé.
        var vigasDosVaos = BicosPorAmbiente.VigasDosVaos(edificio, ambiente, limites, opcoesBicos);
        var grelha = vigasDosVaos is not null && Spk.Motor.Geracao.BicosNoAmbiente.EhGrelha(vigasDosVaos.Select(v => (v.A, v.B, v.Largura)).ToList());
        if (vigasDosVaos is not null && !grelha) opcoesBicos = opcoesBicos with { DefletorM = Math.Max(opcoesBicos.DefletorM, FolgaDoTeto + DropMinimo) };
        var bicos = BicosPorAmbiente.Posicionar(doc, edificio, [ambiente], limites, opcoesBicos, criados).Single();
        if (bicos.Recusa is not null) return Recusa(bicos.Recusa);
        if (bicos.Posicao is not { Bicos.Count: > 0 } posicao || bicos.Defletor is not { } defletor) return Recusa("nenhum bico coube no ambiente");
        var bicosDoAmbiente = criados.Select(doc.GetElement).OfType<FamilyInstance>().ToList();

        var avisos = new List<string>();
        double zRede, zSubgeral;
        if (bicos.VaosEntreVigas is { Count: > 0 } vaos && grelha)
        {
            // O conector do bico em pé aponta para baixo (o tubo chega por baixo).
            var emPe = bicosDoAmbiente.All(b => b.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>()
                .Any(c => c.Domain == Domain.DomainPiping && c.CoordinateSystem.BasisZ.Z < -0.7) == true);
            if (!emPe)
                return Recusa("vigas nas duas direções (quadros de vigas): a rede passa por baixo das vigas e sobe até cada bico, o que pede " +
                              "bico em pé (upright). Escolha um tipo em pé em Bico, ou ponha os bicos com Bicos por ambiente e desenhe a rede à mão");
            zRede = zSubgeral = vaos.Min(v => v.Fundo) - 0.15;
            if (defletor - zRede < DropMinimo - 1e-6) return Recusa("as vigas são rasas demais para a rede passar por baixo delas e subir até os bicos");
            avisos.Add($"bicos em pé nos quadros entre as vigas; ramais e subgeral por baixo das vigas a {zRede:0.00} m, com subida até cada bico. " +
                       "Confira a regra de obstrução: distância do bico à viga e altura do defletor acima do fundo dela");
        }
        else
        {
            // Altura da rede: o drop acima do defletor, sem encostar na laje ou no telhado (o forro a rede atravessa por cima).
            zRede = defletor + opcoes.DropM;
            if (ambiente.OrigemDoTeto is OrigemDoTeto.Laje or OrigemDoTeto.Telhado && ambiente.Teto is { } teto && zRede > teto - FolgaDoTeto)
            {
                zRede = teto - FolgaDoTeto;
                if (zRede - defletor < DropMinimo - 1e-6)
                    return Recusa($"sem espaço para a rede entre o defletor ({defletor:0.00}) e o teto ({teto:0.00}): " +
                                  "a rede precisa de pelo menos 15 cm acima do defletor e 10 cm abaixo do teto");
                avisos.Add($"rede baixada para {zRede:0.00} m, 10 cm abaixo do teto");
            }
            // Com bicos nos vãos, a subgeral passa por baixo das vigas (15 cm abaixo do fundo da mais baixa) e cada ramal sobe
            // dela até o vão; senão, a subgeral fica na altura dos ramais.
            zSubgeral = zRede;
            if (bicos.VaosEntreVigas is { Count: > 0 } paralelas)
            {
                zSubgeral = paralelas.Min(v => v.Fundo) - 0.15;
                if (zRede - zSubgeral < DropMinimo) return Recusa("as vigas são rasas demais para a subgeral passar por baixo e subir até o vão");
                avisos.Add($"bicos nos vãos entre as vigas; ramais nos vãos a {zRede:0.00} m e subgeral por baixo das vigas a {zSubgeral:0.00} m, " +
                           "com subida até cada ramal. Confira a regra de obstrução: distância do bico à viga e altura do defletor acima do fundo dela");
            }
            else
            {
                foreach (var viga in edificio.Vigas.Where(v => v.Fundo < zRede + 0.05 && v.Topo > zRede - 0.05).Take(3))
                    if (Cruza(ambiente, viga)) avisos.Add($"a rede, a {zRede:0.00} m, passa na altura da viga (fundo {viga.Fundo:0.00}): confira a travessia");
            }
        }
        var subgeral = new List<Pipe>();
        var motivo = TransacaoSegura.Executar(doc, "SPK — Subgeral", () => Subgeral(doc, ambiente, posicao, zSubgeral, opcoes.Arranjo, criados, avisos, subgeral, geral));
        if (motivo is not null) return Recusa(motivo);

        var opcoesRamais = zSubgeral < zRede - 0.01
            ? new OpcoesRamais { Altura = AlturaDoRamal.RelativaASubgeral, AlturaM = zRede - zSubgeral }
            : new OpcoesRamais();
        var ramais = TracarRamal.Executar(doc, bicosDoAmbiente, subgeral[0], opcoesRamais);
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
        if (Tampar(doc, criados, Math.Min(zRede, zSubgeral)) is { } semTampao) avisos.Add(semTampao);
        return new RedeNoAmbiente(ambiente, null, bicosDoAmbiente.Count, zRede, ramais, diametros, avisos)
        {
            AlturaDaSubgeral = zSubgeral < zRede - 0.01 ? zSubgeral : null,
        };
    }

    /// <summary>
    /// A subgeral cruza as fileiras ao longo do eixo entre ramais (v): na espinha de peixe, entre as duas colunas de bicos
    /// do meio; no lateral, entre a parede e a primeira coluna. Começa 30 cm da parede de menor v, onde fica a descida até o
    /// piso (a entrada do setor), e termina 20 cm depois da última fileira (o mínimo para o tê).
    /// </summary>
    static string? Subgeral(Document doc, Ambiente ambiente, Spk.Motor.Geracao.PosicaoDeBicos posicao, double zRede, Arranjo arranjo,
        List<ElementId> criados, List<string> avisos, List<Pipe> subgeral, Eixo? geral)
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
        // A entrada fica na parede de menor v; com geral, na parede do lado dela.
        double vInicio, vFim;
        if (geral is not null && V(geral.A) > (v0 + v1) / 2)
        {
            vInicio = v1 - 0.30;
            vFim = Math.Max(fileiras[0] - 0.20, v0 + 0.10);
            if (vInicio - fileiras[^1] < 0.20) return "a primeira fileira de bicos fica colada à parede da entrada: não cabe o tê da subgeral";
        }
        else
        {
            vInicio = v0 + 0.30;
            vFim = Math.Min(fileiras[^1] + 0.20, v1 - 0.10);
            if (fileiras[0] - vInicio < 0.20) return "a primeira fileira de bicos fica colada à parede da entrada: não cabe o tê da subgeral";
        }

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
    static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);
}
