using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Spk.Motor.Modelo;
using MechanicalFitting = Autodesk.Revit.DB.Mechanical.MechanicalFitting;
using Spk.Motor.Normas;

namespace Spk.AddIn;

/// <summary>
/// Monta o grafo do motor a partir dos bicos confirmados e dos tubos ligados a eles.
/// Cada conexão vira um nó; o comprimento equivalente dela entra no trecho ligado ao conector que desvia o fluxo.
/// </summary>
static class LeitorRede
{
    public sealed record BicoLido(FamilyInstance Instancia, double FatorK, double AreaCobertura, double? PressaoMinima = null);

    /// <param name="Bicos">Número do bico no cálculo → instância no Revit.</param>
    /// <param name="Tubos">Id do trecho no cálculo → tubo no Revit (vários trechos podem estar no mesmo tubo).</param>
    /// <param name="TotalDeSetores">Sem VGA: quantos pedaços de rede com bico existem; cada um é um setor.</param>
    public sealed record RedeLida(Rede Rede, IReadOnlyList<string> Avisos, string Setor,
        IReadOnlyDictionary<string, ElementId> Bicos, IReadOnlyDictionary<string, ElementId> Tubos, int TotalDeSetores = 1);

    /// <param name="fonte">A VGA do setor. Nula: cada pedaço de rede é um setor, com a fonte na ponta de tubo mais baixa.</param>
    /// <param name="vgas">Todas as VGAs do modelo. A leitura não passa por elas, o que separa um setor do outro.</param>
    /// <param name="elementoDoSetor">Sem VGA: um bico ou tubo do setor a calcular. Nulo: o setor com mais bicos.</param>
    /// <param name="indiceSetor">Sem VGA: o setor pela ordem (0 é o de mais bicos). Tem precedência sobre o elemento.</param>
    public static RedeLida Montar(Document doc, IReadOnlyList<BicoLido> bicos, double fatorC,
        FamilyInstance? fonte = null, IReadOnlyCollection<ElementId>? vgas = null, ElementId? elementoDoSetor = null, int? indiceSetor = null)
    {
        if (bicos.Count == 0) throw new InvalidOperationException("Nenhum bico com fator K e área de cobertura.");

        var idsBico = new HashSet<ElementId>(bicos.Select(b => b.Instancia.Id));
        var limites = new HashSet<ElementId>(vgas ?? []);
        if (fonte is not null) limites.Add(fonte.Id);
        var naRede = Alcançaveis(doc, idsBico, limites);
        // Num modelo sem conexões ligadas os conectores só alcançam os próprios bicos; a VGA é achada depois, por proximidade.
        if (fonte is not null && !naRede.Contains(fonte.Id) && naRede.Count > idsBico.Count)
            throw new InvalidOperationException("A VGA escolhida não está ligada por tubos aos bicos confirmados.");
        var avisos = new List<string>
        {
            $"Fator C = {fatorC:0} em todos os tubos (Dados do projeto, Tabela 30).",
        };

        var nos = new Dictionary<string, No>();
        foreach (var bico in bicos)
        {
            var conector = ConectorLigado(bico.Instancia) ?? Conectores(bico.Instancia).FirstOrDefault()
                ?? throw new InvalidOperationException($"O bico {bico.Instancia.Id.Value} não tem conector de tubulação.");
            Garantir(nos, IdBico(bico.Instancia), conector.Origin);
        }

        foreach (var id in naRede)
        {
            if (doc.GetElement(id) is not FamilyInstance peca || idsBico.Contains(id)) continue;
            var origens = Conectores(peca).Select(c => c.Origin).ToList();
            if (origens.Count == 0) continue;
            var medio = new XYZ(origens.Average(p => p.X), origens.Average(p => p.Y), origens.Average(p => p.Z));
            Garantir(nos, IdPeca(id), medio);
        }

        var trechos = new List<Trecho>();
        var trechosDoTubo = new Dictionary<ElementId, List<int>>();
        var seq = 0;
        var usouNominal = false;
        foreach (var id in naRede)
        {
            if (doc.GetElement(id) is not Pipe tubo) continue;
            if (tubo.Location is not LocationCurve curva) continue;
            var pontos = Pontos(tubo, curva.Curve, idsBico);
            var comprimento = Metros(curva.Curve.Length);
            var diametro = DiametroMm(tubo, ref usouNominal);
            var nominal = DiametroNominalMm(tubo);
            for (var i = 0; i < pontos.Count - 1; i++)
            {
                var a = pontos[i];
                var b = pontos[i + 1];
                if (a.No == b.No) continue;
                var fracao = b.T - a.T;
                if (fracao <= 1e-6) continue;
                Garantir(nos, a.No, a.Ponto);
                Garantir(nos, b.No, b.Ponto);
                trechos.Add(new Trecho($"T{seq++}", a.No, b.No, diametro, Math.Max(comprimento * fracao, 0.001), fatorC)
                {
                    DiametroNominal = nominal,
                });
                if (!trechosDoTubo.TryGetValue(tubo.Id, out var lista)) trechosDoTubo[tubo.Id] = lista = [];
                lista.Add(trechos.Count - 1);
            }
        }

        AplicarConexoes(doc, naRede, idsBico, trechos, trechosDoTubo, fatorC, avisos);
        var tuboDoTrecho = new Dictionary<string, ElementId>(StringComparer.Ordinal);
        foreach (var (tubo, indices) in trechosDoTubo)
            foreach (var i in indices)
                tuboDoTrecho[trechos[i].Id] = tubo;

        var ligacoesDeBico = LigarBicos(bicos, idsBico, nos, trechos, ref seq, fatorC);
        if (ligacoesDeBico > 0)
            avisos.Add($"{ligacoesDeBico} bico(s) ligam na conexão, não na ponta do tubo. Esse trecho curto entrou com diâmetro interno de 25 mm.");

        if (usouNominal)
            avisos.Add("Algum tubo não tem diâmetro interno: foi usado o diâmetro nominal.");

        var grau = new Dictionary<string, int>();
        foreach (var trecho in trechos)
        {
            grau[trecho.De] = grau.GetValueOrDefault(trecho.De) + 1;
            grau[trecho.Para] = grau.GetValueOrDefault(trecho.Para) + 1;
        }

        var ligados = bicos.Where(b => grau.GetValueOrDefault(IdBico(b.Instancia)) > 0).ToList();
        if (ligados.Count == 0)
        {
            foreach (var no in nos.Keys.Where(id => !id.StartsWith('S')).ToList()) nos.Remove(no);
            trechos.Clear();
            tuboDoTrecho.Clear();
            var diagnostico = AnexarPorProximidade(doc, bicos, nos, trechos, tuboDoTrecho, ref seq, ref usouNominal, fatorC,
                out var semClassificacao);
            grau.Clear();
            foreach (var trecho in trechos)
            {
                grau[trecho.De] = grau.GetValueOrDefault(trecho.De) + 1;
                grau[trecho.Para] = grau.GetValueOrDefault(trecho.Para) + 1;
            }
            ligados = bicos.Where(b => grau.GetValueOrDefault(IdBico(b.Instancia)) > 0).ToList();
            if (ligados.Count == 0) throw new InvalidOperationException(diagnostico);
            avisos.Add("Os bicos não estavam conectados ao tubo no Revit. Cada um entrou no tubo mais próximo, até 0,50 m.");
            if (semClassificacao)
                avisos.Add("Nenhum tubo de sistema de incêndio chegava aos bicos, então entraram tubos de qualquer sistema. " +
                           "Confira no Revit a classificação do sistema dos tubos de sprinkler (deveria ser proteção contra incêndio).");
        }

        var soltos = bicos.Count - ligados.Count;
        if (soltos > 0) avisos.Add($"{soltos} bico(s) sem tubo ligado ficaram de fora.");
        if (ligados.Count == 0) throw new InvalidOperationException("Nenhum bico confirmado está ligado a um tubo.");
        if (trechos.Count == 0) throw new InvalidOperationException("Não há tubos na rede ligada aos bicos.");

        var nosDeBico = ligados.Select(b => IdBico(b.Instancia)).ToHashSet(StringComparer.Ordinal);
        var nosDeVga = limites.Select(IdPeca).ToHashSet(StringComparer.Ordinal);
        string noFonte;
        HashSet<string> setor;
        var totalDeSetores = 1;
        var indiceEscolhido = 0;
        if (fonte is not null)
        {
            noFonte = IdPeca(fonte.Id);
            try
            {
                setor = Topologia.SetorDaVga(noFonte, trechos, nosDeBico, nosDeVga);
            }
            catch (InvalidOperationException)
            {
                throw new VgaDesligadaException(NomeDaVga(fonte), DistanciaAteARede(fonte, nos, trechos));
            }
        }
        else
        {
            var componentes = Topologia.Componentes(nos, trechos, nosDeBico);
            if (componentes.Count == 0) throw new InvalidOperationException("Os bicos não formam uma rede contínua com os tubos encontrados.");
            indiceEscolhido = IndiceDoSetor(componentes, elementoDoSetor, indiceSetor, trechos, tuboDoTrecho);
            (noFonte, setor, _) = componentes[indiceEscolhido];
            totalDeSetores = componentes.Count;
        }

        var nosSetor = nos.Values.Where(n => setor.Contains(n.Id)).ToList();
        var trechosSetor = trechos.Where(t => setor.Contains(t.De) && setor.Contains(t.Para)).ToList();
        var doSetor = ligados.Where(b => setor.Contains(IdBico(b.Instancia))).ToList();
        if (doSetor.Count < ligados.Count)
            avisos.Add(fonte is not null
                ? $"{ligados.Count - doSetor.Count} bicos são de outra VGA ou de rede solta e ficaram fora deste setor."
                : totalDeSetores > 1
                    ? $"A rede tem {totalDeSetores} setores sem ligação entre si (VGA não desenhada no modelo). Este é o setor " +
                      $"{indiceEscolhido + 1}; os outros {ligados.Count - doSetor.Count} bicos são dos demais. Para calcular outro, rode " +
                      "Calcular e clique num bico dele."
                    : $"{ligados.Count - doSetor.Count} bicos ficaram de fora: o tubo deles não encontra o restante da rede.");

        var (dx, dy) = Topologia.DirecaoDosRamais(trechosSetor, nos);
        var ramalDe = Topologia.RamalDeCadaBico(doSetor.Select(b => IdBico(b.Instancia)), trechosSetor, nos, dx, dy);
        var chuveiros = doSetor.Select(b => new Chuveiro($"E{b.Instancia.Id.Value}", IdBico(b.Instancia), b.FatorK, b.AreaCobertura)
        {
            Ramal = $"R{ramalDe[IdBico(b.Instancia)]}",
            PressaoMinima = b.PressaoMinima,
        }).ToList();

        var cotaFonte = nos[noFonte].Z;
        avisos.Add(fonte is null
            ? $"Fonte adotada: ponta de tubo mais baixa (nó {noFonte}, cota {cotaFonte:0.00} m). " +
              "Para calcular a partir da VGA, marque a família dela como Fonte em Famílias."
            : $"Fonte: VGA \"{fonte.Symbol?.Family?.Name}\" (id {fonte.Id.Value}), cota {cotaFonte:0.00} m.");
        avisos.Add($"{chuveiros.Count} bicos em {ramalDe.Values.Distinct().Count()} ramais.");

        // Números a partir da VGA, os mesmos no memorial, nos parâmetros e nas folhas.
        var numero = Topologia.Numerar(noFonte, trechosSetor);
        string N(string id) => numero.TryGetValue(id, out var n) ? n.ToString() : id;
        var idsTrecho = new HashSet<string>(StringComparer.Ordinal);
        var tubos = new Dictionary<string, ElementId>(StringComparer.Ordinal);
        var trechosNumerados = new List<Trecho>();
        foreach (var trecho in trechosSetor)
        {
            var id = $"{N(trecho.De)}-{N(trecho.Para)}";
            for (var k = 2; !idsTrecho.Add(id); k++) id = $"{N(trecho.De)}-{N(trecho.Para)}/{k}";
            trechosNumerados.Add(trecho with { Id = id, De = N(trecho.De), Para = N(trecho.Para) });
            if (tuboDoTrecho.TryGetValue(trecho.Id, out var tubo)) tubos[id] = tubo;
        }

        var bicosPorNo = doSetor.ToDictionary(b => IdBico(b.Instancia), b => b.Instancia.Id, StringComparer.Ordinal);
        var chuveirosNumerados = chuveiros.Select(c => c with { Id = N(c.No), No = N(c.No) }).ToList();
        var bicosNumerados = chuveiros.ToDictionary(c => N(c.No), c => bicosPorNo[c.No], StringComparer.Ordinal);
        var setorNome = fonte is not null ? NomeDaVga(fonte) : totalDeSetores > 1 ? $"Setor {indiceEscolhido + 1}" : "Malha";

        var rede = new Rede(N(noFonte), nosSetor.Select(n => n with { Id = N(n.Id) }), trechosNumerados, chuveirosNumerados);
        return new RedeLida(rede, avisos, setorNome, bicosNumerados, tubos, totalDeSetores);
    }

    /// <summary>
    /// Qual pedaço de rede calcular: pelo índice, pelo bico ou tubo clicado, ou o de mais bicos. O tubo é achado pelos
    /// trechos que vieram dele.
    /// </summary>
    static int IndiceDoSetor(List<(string Fonte, HashSet<string> Nos, int Bicos)> componentes, ElementId? elemento, int? indice,
        List<Trecho> trechos, Dictionary<string, ElementId> tuboDoTrecho)
    {
        if (indice is { } i)
            return i >= 0 && i < componentes.Count ? i : throw new InvalidOperationException($"A rede tem {componentes.Count} setores, não {i + 1}.");
        if (elemento is null) return 0;

        var nosDoElemento = new HashSet<string>(StringComparer.Ordinal) { IdBico(elemento), IdPeca(elemento) };
        foreach (var trecho in trechos)
            if (tuboDoTrecho.TryGetValue(trecho.Id, out var tubo) && tubo == elemento)
                nosDoElemento.Add(trecho.De);
        var k = componentes.FindIndex(c => c.Nos.Overlaps(nosDoElemento));
        return k >= 0 ? k : throw new InvalidOperationException("O elemento clicado não está em nenhum setor que o plugin conseguiu ler.");
    }

    /// <summary>Distância em metros da VGA até o nó mais próximo da rede lida, ou nula se não der para medir.</summary>
    static double? DistanciaAteARede(FamilyInstance vga, Dictionary<string, No> nos, List<Trecho> trechos)
    {
        if (vga.Location is not LocationPoint local) return null;
        var p = local.Point;
        double x = Metros(p.X), y = Metros(p.Y), z = Metros(p.Z);
        var usados = trechos.SelectMany(t => new[] { t.De, t.Para }).Where(id => !id.StartsWith('S')).ToHashSet(StringComparer.Ordinal);
        if (usados.Count == 0) return null;
        return usados.Where(nos.ContainsKey).Select(id => nos[id])
            .Min(n => Math.Sqrt((n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y) + (n.Z - z) * (n.Z - z)));
    }

    /// <summary>Marca, comentário ou nome do tipo da VGA, o que estiver preenchido primeiro.</summary>
    public static string NomeDaVga(FamilyInstance vga)
    {
        foreach (var parametro in new[] { BuiltInParameter.ALL_MODEL_MARK, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS })
            if (vga.get_Parameter(parametro)?.AsString() is { Length: > 0 } texto) return texto.Trim();
        return $"VGA {vga.Id.Value}";
    }

    static readonly int[] DnsTabelados = [25, 32, 40, 50, 65, 80, 100, 125, 150, 200];

    /// <summary>
    /// Soma o comprimento equivalente das conexões e válvulas (NBR 9.4.3.4) no trecho ligado a elas.
    /// Cotovelo: no trecho de um dos lados. Tê e cruzeta: só no ramo que desvia o fluxo (9.4.4.7 e), que é o
    /// conector fora do par alinhado de maior diâmetro. Válvula: pelo nome da família.
    /// </summary>
    static void AplicarConexoes(Document doc, HashSet<ElementId> naRede, HashSet<ElementId> idsBico, List<Trecho> trechos,
        Dictionary<ElementId, List<int>> trechosDoTubo, double fatorC, List<string> avisos)
    {
        var c = Math.Clamp(fatorC, 100, 150);
        var aplicadas = 0;
        var semTabela = new SortedSet<string>(StringComparer.Ordinal);
        var desconhecidas = new SortedSet<string>(StringComparer.Ordinal);

        void Somar(Connector? conector, string no, TipoConexao tipo, string nome)
        {
            if (conector is null) return;
            var indice = TrechoDoConector(conector, no, trechos, trechosDoTubo);
            if (indice is null) return;
            var trecho = trechos[indice.Value];
            var dn = DnTabelado(trecho.DiametroNominal);
            if (dn is null)
            {
                semTabela.Add($"{nome} DN {trecho.DiametroNominal?.ToString() ?? "?"}");
                return;
            }

            double equivalente;
            try
            {
                equivalente = ComprimentosEquivalentes.Obter(tipo, dn.Value, c, trecho.DiametroInterno);
            }
            catch (ArgumentException)
            {
                semTabela.Add($"{nome} DN {dn}");
                return;
            }

            trechos[indice.Value] = trecho with
            {
                ComprimentoEquivalente = trecho.ComprimentoEquivalente + equivalente,
                Conexoes = string.IsNullOrEmpty(trecho.Conexoes) ? nome : trecho.Conexoes + ", " + nome,
            };
            aplicadas++;
        }

        foreach (var id in naRede)
        {
            if (idsBico.Contains(id) || doc.GetElement(id) is not FamilyInstance peca) continue;
            var no = IdPeca(id);
            var conectores = Conectores(peca).ToList();
            if (conectores.Count == 0) continue;
            var categoria = peca.Category?.Id.Value;

            if (categoria == (long)BuiltInCategory.OST_PipeFitting && peca.MEPModel is MechanicalFitting conexao)
            {
                if (conexao.PartType == PartType.Elbow && conectores.Count >= 2)
                {
                    var desvio = 180 - conectores[0].CoordinateSystem.BasisZ.AngleTo(conectores[1].CoordinateSystem.BasisZ) * 180 / Math.PI;
                    var tipo = desvio <= 50 ? TipoConexao.Cotovelo45 : TipoConexao.Cotovelo90Padrao;
                    Somar(conectores[0], no, tipo, desvio <= 50 ? "cotovelo 45°" : "cotovelo 90°");
                }
                else if (conexao.PartType is PartType.Tee or PartType.Cross)
                {
                    foreach (var ramo in Ramos(conectores))
                        Somar(ramo, no, TipoConexao.TeOuCruzetaFluxoDesviado, conexao.PartType == PartType.Tee ? "tê" : "cruzeta");
                }
            }
            else if (categoria == (long)BuiltInCategory.OST_PipeAccessory)
            {
                var nome = $"{peca.Symbol?.Family?.Name} {peca.Symbol?.Name}";
                var tipo = TipoDeValvula(nome);
                if (tipo is null)
                {
                    desconhecidas.Add(peca.Symbol?.Family?.Name ?? "acessório sem família");
                    continue;
                }
                Somar(conectores[0], no, tipo.Value, tipo switch
                {
                    TipoConexao.ValvulaBorboleta => "válvula borboleta",
                    TipoConexao.ValvulaGaveta => "válvula gaveta",
                    _ => "válvula de retenção",
                });
            }
        }

        avisos.Add(aplicadas > 0
            ? $"{aplicadas} conexões e válvulas entraram com comprimento equivalente (tabela de literatura, NBR 9.4.3.4). Confira com o catálogo do fabricante."
            : "Nenhuma conexão ligada aos tubos: sem comprimento equivalente nesta rede.");
        if (semTabela.Count > 0)
            avisos.Add("Sem comprimento equivalente tabelado, ficaram de fora: " + string.Join("; ", semTabela.Take(8)) + ".");
        if (desconhecidas.Count > 0)
            avisos.Add("Acessórios sem perda conhecida (fora do cálculo): " + string.Join("; ", desconhecidas.Take(8)) + ".");
    }

    /// <summary>Conectores de tê ou cruzeta que desviam o fluxo: todos menos o par alinhado de maior diâmetro.</summary>
    static IEnumerable<Connector> Ramos(List<Connector> conectores)
    {
        (Connector A, Connector B)? passagem = null;
        var maior = -1.0;
        for (var i = 0; i < conectores.Count; i++)
        for (var j = i + 1; j < conectores.Count; j++)
        {
            var a = conectores[i];
            var b = conectores[j];
            if (a.CoordinateSystem.BasisZ.DotProduct(b.CoordinateSystem.BasisZ) > -0.95) continue;
            var raio = Raio(a) + Raio(b);
            if (raio <= maior) continue;
            maior = raio;
            passagem = (a, b);
        }

        return passagem is { } p
            ? conectores.Where(c => !ReferenceEquals(c, p.A) && !ReferenceEquals(c, p.B))
            : conectores.OrderBy(Raio).Take(1);
    }

    static double Raio(Connector conector)
    {
        try { return conector.Radius; }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException) { return 0; }
    }

    /// <summary>O trecho do tubo ligado a este conector que encosta no nó da conexão.</summary>
    static int? TrechoDoConector(Connector conector, string no, List<Trecho> trechos, Dictionary<ElementId, List<int>> trechosDoTubo)
    {
        foreach (Connector outro in conector.AllRefs)
        {
            if (outro.Owner is not Pipe tubo || !trechosDoTubo.TryGetValue(tubo.Id, out var indices)) continue;
            foreach (var i in indices)
                if (trechos[i].De == no || trechos[i].Para == no)
                    return i;
        }
        return null;
    }

    static int? DnTabelado(int? nominal)
    {
        if (nominal is not { } n) return null;
        var perto = DnsTabelados.OrderBy(d => Math.Abs(d - n)).First();
        return Math.Abs(perto - n) <= 3 ? perto : null;
    }

    static TipoConexao? TipoDeValvula(string nome)
    {
        var n = nome.ToLowerInvariant();
        if (n.Contains("borboleta") || n.Contains("butterfly")) return TipoConexao.ValvulaBorboleta;
        if (n.Contains("gaveta") || n.Contains("gate")) return TipoConexao.ValvulaGaveta;
        if (n.Contains("retenção") || n.Contains("retencao") || n.Contains("check")) return TipoConexao.ValvulaRetencao;
        return null;
    }

    /// <summary>Tudo que os bicos alcançam pelos conectores. Os limites (VGAs) entram, mas a busca não passa por eles.</summary>
    static HashSet<ElementId> Alcançaveis(Document doc, HashSet<ElementId> idsBico, HashSet<ElementId> limites)
    {
        var visitados = new HashSet<ElementId>();
        var fila = new Queue<ElementId>(idsBico);
        while (fila.Count > 0)
        {
            var id = fila.Dequeue();
            if (!visitados.Add(id)) continue;
            if (limites.Contains(id)) continue;
            var elemento = doc.GetElement(id);
            if (elemento is null) continue;
            foreach (var conector in Conectores(elemento))
            {
                foreach (Connector outro in conector.AllRefs)
                {
                    if (outro.Owner is null || outro.Owner.Id == id) continue;
                    if (!EhFisico(outro.ConnectorType)) continue;
                    if (!visitados.Contains(outro.Owner.Id)) fila.Enqueue(outro.Owner.Id);
                }
            }
        }
        return visitados;
    }

    static List<(double T, string No, XYZ Ponto)> Pontos(Pipe tubo, Curve curva, HashSet<ElementId> idsBico)
    {
        var extremos = Conectores(tubo).Where(c => (c.ConnectorType & ConnectorType.End) == ConnectorType.End).Take(2).ToList();
        if (extremos.Count < 2) return [];

        var inicio = curva.GetEndParameter(0);
        var fim = curva.GetEndParameter(1);
        var pontos = new List<(double T, string No, XYZ Ponto)>
        {
            (0, NoDoConector(extremos[0], tubo, idsBico), extremos[0].Origin),
            (1, NoDoConector(extremos[1], tubo, idsBico), extremos[1].Origin),
        };

        foreach (var conector in Conectores(tubo).Where(c => (c.ConnectorType & ConnectorType.Curve) == ConnectorType.Curve))
        {
            var no = NoDoConector(conector, tubo, idsBico);
            var projecao = curva.Project(conector.Origin);
            var t = projecao is null || Math.Abs(fim - inicio) < 1e-9
                ? 0.5
                : Math.Clamp((projecao.Parameter - inicio) / (fim - inicio), 0, 1);
            pontos.Add((t, no, conector.Origin));
        }

        pontos.Sort((a, b) => a.T.CompareTo(b.T));
        return pontos;
    }

    static string NoDoConector(Connector conector, Element dono, HashSet<ElementId> idsBico)
    {
        foreach (Connector outro in conector.AllRefs)
        {
            if (outro.Owner is null || outro.Owner.Id == dono.Id) continue;
            if (!EhFisico(outro.ConnectorType)) continue;
            if (idsBico.Contains(outro.Owner.Id)) return IdBico(outro.Owner);
            if (outro.Owner is FamilyInstance) return IdPeca(outro.Owner.Id);
        }
        return Chave(conector.Origin);
    }

    /// <summary>
    /// O bico quase sempre liga num cotovelo ou tê, não na ponta do tubo. Sem este trecho o nó do bico fica solto.
    /// </summary>
    static int LigarBicos(IReadOnlyList<BicoLido> bicos, HashSet<ElementId> idsBico, Dictionary<string, No> nos, List<Trecho> trechos, ref int seq, double fatorC)
    {
        var pares = new HashSet<string>(trechos.Select(t => Par(t.De, t.Para)));
        var ligados = 0;
        foreach (var bico in bicos)
        {
            var idS = IdBico(bico.Instancia);
            if (!nos.ContainsKey(idS)) continue;
            var entrou = false;
            foreach (var conector in Conectores(bico.Instancia))
            {
                foreach (Connector outro in conector.AllRefs)
                {
                    if (outro.Owner is null || outro.Owner.Id == bico.Instancia.Id) continue;
                    if (!EhFisico(outro.ConnectorType)) continue;
                    var destino = outro.Owner switch
                    {
                        Pipe => NoDoConector(outro, outro.Owner, idsBico),
                        FamilyInstance => IdPeca(outro.Owner.Id),
                        _ => Chave(outro.Origin),
                    };
                    if (destino == idS) continue;
                    if (!nos.ContainsKey(destino)) Garantir(nos, destino, outro.Origin);
                    if (!pares.Add(Par(idS, destino))) continue;
                    var a = nos[idS];
                    var b = nos[destino];
                    var distancia = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
                    trechos.Add(new Trecho($"L{seq++}", idS, destino, 25, Math.Max(distancia, 0.001), fatorC) { DiametroNominal = 25 });
                    entrou = true;
                }
            }
            if (entrou) ligados++;
        }
        return ligados;
    }

    static string Par(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

    static bool EhFisico(ConnectorType tipo) =>
        (tipo & ConnectorType.End) == ConnectorType.End || (tipo & ConnectorType.Curve) == ConnectorType.Curve;

    /// <summary>
    /// Quando o conector do bico não está ligado, encosta o bico no tubo mais próximo (até 0,50 m)
    /// e puxa os tubos contínuos, soldando pontas a até 8 cm e portas da mesma conexão. Tenta primeiro só os tubos de
    /// sistema de incêndio; se nenhum chega aos bicos (modelo com os tubos de sprinkler classificados como outro
    /// sistema, como na Nova Era), usa todos e devolve <paramref name="semClassificacao" />.
    /// </summary>
    static string AnexarPorProximidade(Document doc, IReadOnlyList<BicoLido> bicos, Dictionary<string, No> nos, List<Trecho> trechos,
        Dictionary<string, ElementId> tuboDoTrecho, ref int seq, ref bool usouNominal, double fatorC, out bool semClassificacao)
    {
        semClassificacao = false;
        var tubos = Tubos(doc, soIncendio: true, ref usouNominal);
        var grade = Grade(tubos);
        var tubosDeIncendio = tubos.Count;
        if (!AlgumPerto(bicos, nos, tubos, grade))
        {
            var todos = Tubos(doc, soIncendio: false, ref usouNominal);
            var gradeTodos = Grade(todos);
            if (AlgumPerto(bicos, nos, todos, gradeTodos))
            {
                tubos = todos;
                grade = gradeTodos;
                semClassificacao = true;
            }
        }

        if (tubos.Count == 0)
            return "Nenhum bico confirmado está ligado a um tubo. O modelo aberto não tem tubos, nem em vínculos carregados.";

        var portas = new Dictionary<(int X, int Y, int Z), List<(string No, XYZ Ponto)>>();
        ColetarPortas(doc, null, null, portas, nos);
        foreach (var vinculo in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            var vinculado = vinculo.GetLinkDocument();
            if (vinculado is null) continue;
            ColetarPortas(vinculado, vinculo.GetTotalTransform(), vinculo.Id, portas, nos);
        }

        var nosPerto = new Dictionary<(int X, int Y, int Z), List<string>>();
        var menor = double.PositiveInfinity;
        var sementes = new List<(int Indice, double T, string IdBico)>();
        foreach (var bico in bicos)
        {
            var idS = IdBico(bico.Instancia);
            if (!nos.ContainsKey(idS)) continue;
            var melhor = TuboMaisProximo(grade, tubos, EmPes(nos[idS]));
            if (melhor is null) continue;
            if (melhor.Value.Distancia < menor) menor = melhor.Value.Distancia;
            if (melhor.Value.Distancia <= Pes(0.50))
                sementes.Add((melhor.Value.Indice, melhor.Value.T, idS));
        }

        if (sementes.Count == 0)
        {
            var mm = double.IsPositiveInfinity(menor) ? 0 : Milimetros(menor);
            return $"Nenhum bico confirmado está ligado a um tubo. Há {tubos.Count} tubos no modelo e nos vínculos ({tubosDeIncendio} de incêndio). "
                   + (mm <= 0
                       ? "Nenhum tubo chegou perto dos bicos."
                       : $"O bico mais próximo de um tubo está a {mm:0} mm, além do limite de 500 mm.");
        }

        // Tubos cuja ponta encosta em cada peça (conexão ou válvula): a leitura atravessa a peça, porque de um lado e do
        // outro dela as pontas ficam longe demais para se juntarem sozinhas (válvula de 4", tê de 150).
        var tubosDaPeca = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < tubos.Count; i++)
            foreach (var ponta in new[] { tubos[i].Curva.GetEndPoint(0), tubos[i].Curva.GetEndPoint(1) })
                if (PortaProxima(ponta, portas, 0.15) is { } peca)
                {
                    if (!tubosDaPeca.TryGetValue(peca, out var lista)) tubosDaPeca[peca] = lista = [];
                    lista.Add(i);
                }

        var fila = new Queue<int>(sementes.Select(s => s.Indice).Distinct());
        var visitados = new HashSet<int>();
        var derivacoes = new List<(int Indice, double T, string No)>();
        while (fila.Count > 0)
        {
            var i = fila.Dequeue();
            if (!visitados.Add(i)) continue;
            var tubo = tubos[i];
            var n0 = NoDaPonta(tubo.Curva.GetEndPoint(0), portas, nosPerto, nos);
            var n1 = NoDaPonta(tubo.Curva.GetEndPoint(1), portas, nosPerto, nos);
            if (n0 == n1) continue;
            tubo.Partes.Add(new Segmento(n0, n1, 0, 1, Math.Max(tubo.Comprimento, 0.001), tubo.Diametro));
            foreach (var vizinho in VizinhosDePonta(i, tubo.Curva, tubos, grade))
                if (!visitados.Contains(vizinho)) fila.Enqueue(vizinho);
            foreach (var no in new[] { n0, n1 })
                if (tubosDaPeca.TryGetValue(no, out var doOutroLado))
                    foreach (var j in doOutroLado)
                        if (!visitados.Contains(j)) fila.Enqueue(j);
            foreach (var (ponta, no) in new[] { (tubo.Curva.GetEndPoint(0), n0), (tubo.Curva.GetEndPoint(1), n1) })
            foreach (var (j, t) in DerivacoesNaPonta(i, ponta, tubos, grade))
            {
                derivacoes.Add((j, t, no));
                if (!visitados.Contains(j)) fila.Enqueue(j);
            }
        }

        // Primeiro as derivações (ramal que encosta no meio da subgeral), depois os bicos: os dois cortam o tubo em
        // partes, e cada um acha a parte que contém o seu ponto.
        foreach (var derivacao in derivacoes.Distinct())
            InserirNo(tubos[derivacao.Indice], derivacao.T, derivacao.No, nos);
        foreach (var grupo in sementes.GroupBy(s => s.Indice))
            foreach (var semente in grupo.OrderByDescending(s => s.T))
                InserirNo(tubos[semente.Indice], semente.T, semente.IdBico, nos);

        // Cada trecho guarda o tubo de onde veio: as folhas etiquetam e o cálculo grava vazão e velocidade nele.
        foreach (var tubo in tubos)
            foreach (var parte in tubo.Partes)
            {
                if (parte.De == parte.Para || parte.Comprimento <= 1e-4) continue;
                var id = $"G{seq++}";
                trechos.Add(new Trecho(id, parte.De, parte.Para, parte.Diametro, parte.Comprimento, fatorC));
                if (tubo.Elemento is { } elemento) tuboDoTrecho[id] = elemento;
            }

        return "";
    }

    /// <summary>Corta o tubo no ponto <paramref name="t" /> (0 a 1) e liga ali o nó. Perto das pontas, liga o nó à ponta.</summary>
    static void InserirNo(TuboPronto tubo, double t, string idBico, Dictionary<string, No> nos)
    {
        if (!nos.ContainsKey(idBico) || tubo.Partes.Count == 0) return;
        var i = tubo.Partes.FindIndex(p => t >= p.T0 - 1e-4 && t <= p.T1 + 1e-4);
        if (i < 0) return;
        var parte = tubo.Partes[i];
        var intervalo = parte.T1 - parte.T0;
        if (intervalo < 1e-6) return;
        var frac = Math.Clamp((t - parte.T0) / intervalo, 0, 1);
        if (frac <= 0.02 || frac >= 0.98)
        {
            var destino = frac <= 0.02 ? parte.De : parte.Para;
            if (!nos.ContainsKey(destino)) return;
            var a = nos[idBico];
            var b = nos[destino];
            var distancia = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
            tubo.Partes.Add(new Segmento(idBico, destino, t, t, Math.Max(distancia, 0.001), parte.Diametro));
            return;
        }

        var compA = Math.Max(parte.Comprimento * frac, 0.001);
        var compB = Math.Max(parte.Comprimento - parte.Comprimento * frac, 0.001);
        var tMeio = parte.T0 + intervalo * frac;
        tubo.Partes[i] = parte with { Para = idBico, T1 = tMeio, Comprimento = compA };
        tubo.Partes.Insert(i + 1, new Segmento(idBico, parte.Para, tMeio, parte.T1, compB, parte.Diametro));
    }

    static string NoDaPonta(XYZ pes, Dictionary<(int X, int Y, int Z), List<(string No, XYZ Ponto)>> portas,
        Dictionary<(int X, int Y, int Z), List<string>> nosPerto, Dictionary<string, No> nos)
    {
        var porta = PortaProxima(pes, portas, 0.15);
        if (porta is not null)
        {
            Garantir(nos, porta, pes);
            IndexarNo(nosPerto, porta, pes);
            return porta;
        }

        var existente = NoProximo(pes, nosPerto, nos, ToleranciaPonta);
        if (existente is not null) return existente;

        var chave = Chave(pes);
        Garantir(nos, chave, pes);
        IndexarNo(nosPerto, chave, pes);
        return chave;
    }

    static string? PortaProxima(XYZ pes, Dictionary<(int X, int Y, int Z), List<(string No, XYZ Ponto)>> grade, double toleranciaMetros)
    {
        string? melhor = null;
        var menor = Pes(toleranciaMetros);
        foreach (var celula in Vizinhanca(pes))
        {
            if (!grade.TryGetValue(celula, out var lista)) continue;
            foreach (var item in lista)
            {
                var d = pes.DistanceTo(item.Ponto);
                if (d > menor) continue;
                menor = d;
                melhor = item.No;
            }
        }
        return melhor;
    }

    static string? NoProximo(XYZ pes, Dictionary<(int X, int Y, int Z), List<string>> grade, Dictionary<string, No> nos, double toleranciaMetros)
    {
        string? melhor = null;
        var menor = Pes(toleranciaMetros);
        foreach (var celula in Vizinhanca(pes))
        {
            if (!grade.TryGetValue(celula, out var lista)) continue;
            foreach (var id in lista)
            {
                if (!nos.TryGetValue(id, out var no)) continue;
                var d = pes.DistanceTo(EmPes(no));
                if (d > menor) continue;
                menor = d;
                melhor = id;
            }
        }
        return melhor;
    }

    static (int Indice, double T, double Distancia)? TuboMaisProximo(Dictionary<(int X, int Y, int Z), List<int>> grade, List<TuboPronto> tubos, XYZ origem)
    {
        (int Indice, double T, double Distancia)? melhor = null;
        foreach (var celula in Vizinhanca(origem))
        {
            if (!grade.TryGetValue(celula, out var lista)) continue;
            foreach (var indice in lista)
            {
                var proj = tubos[indice].Curva.Project(origem);
                if (proj is null) continue;
                if (melhor is not null && proj.Distance >= melhor.Value.Distancia) continue;
                melhor = (indice, ParametroNormalizado(tubos[indice].Curva, proj.Parameter), proj.Distance);
            }
        }
        return melhor;
    }

    /// <summary>
    /// Tubos em que esta ponta encosta no meio, a até a tolerância mais o raio do outro tubo: derivação sem cortar o
    /// tubo, como os ramais na subgeral de 71 m da Nova Era. Ponta com ponta fica com <see cref="VizinhosDePonta" />.
    /// </summary>
    static IEnumerable<(int Indice, double T)> DerivacoesNaPonta(int indice, XYZ ponta, List<TuboPronto> tubos,
        Dictionary<(int X, int Y, int Z), List<int>> grade)
    {
        var vistos = new HashSet<int>();
        foreach (var celula in Vizinhanca(ponta))
        {
            if (!grade.TryGetValue(celula, out var lista)) continue;
            foreach (var outro in lista)
            {
                if (outro == indice || !vistos.Add(outro)) continue;
                var alvo = tubos[outro];
                var projecao = alvo.Curva.Project(ponta);
                if (projecao is null || projecao.Distance > Pes(ToleranciaPonta + alvo.Diametro / 2000)) continue;
                var t = ParametroNormalizado(alvo.Curva, projecao.Parameter);
                var aoLongo = t * alvo.Comprimento;
                if (aoLongo <= ToleranciaPonta || alvo.Comprimento - aoLongo <= ToleranciaPonta) continue;
                yield return (outro, t);
            }
        }
    }

    static IEnumerable<int> VizinhosDePonta(int indice, Curve curva, List<TuboPronto> tubos, Dictionary<(int X, int Y, int Z), List<int>> grade)
    {
        var limite = Pes(ToleranciaPonta);
        var vistos = new HashSet<int>();
        foreach (var ponta in new[] { curva.GetEndPoint(0), curva.GetEndPoint(1) })
        {
            foreach (var celula in Vizinhanca(ponta))
            {
                if (!grade.TryGetValue(celula, out var lista)) continue;
                foreach (var outro in lista)
                {
                    if (outro == indice || !vistos.Add(outro)) continue;
                    var outra = tubos[outro].Curva;
                    if (ponta.DistanceTo(outra.GetEndPoint(0)) <= limite || ponta.DistanceTo(outra.GetEndPoint(1)) <= limite)
                        yield return outro;
                }
            }
        }
    }

    /// <summary>Pontas a até esta distância são tratadas como ligadas, em m. A mesma para seguir e para fundir nós.</summary>
    const double ToleranciaPonta = 0.15;

    static List<TuboPronto> Tubos(Document doc, bool soIncendio, ref bool usouNominal)
    {
        var tubos = new List<TuboPronto>();
        ColetarTubos(doc, null, null, tubos, soIncendio, ref usouNominal);
        foreach (var vinculo in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            var vinculado = vinculo.GetLinkDocument();
            if (vinculado is null) continue;
            ColetarTubos(vinculado, vinculo.GetTotalTransform(), vinculo.Id, tubos, soIncendio, ref usouNominal);
        }
        return tubos;
    }

    static Dictionary<(int X, int Y, int Z), List<int>> Grade(List<TuboPronto> tubos)
    {
        var grade = new Dictionary<(int X, int Y, int Z), List<int>>();
        for (var i = 0; i < tubos.Count; i++) IndexarTubo(grade, tubos[i].Curva, i);
        return grade;
    }

    static bool AlgumPerto(IReadOnlyList<BicoLido> bicos, Dictionary<string, No> nos, List<TuboPronto> tubos,
        Dictionary<(int X, int Y, int Z), List<int>> grade) =>
        bicos.Any(b => nos.TryGetValue(IdBico(b.Instancia), out var no)
                       && TuboMaisProximo(grade, tubos, EmPes(no)) is { } perto && perto.Distancia <= Pes(0.50));

    /// <summary>
    /// Com <paramref name="soIncendio" />, só tubos de sistema de incêndio: água fria, esgoto e outros vínculos não entram
    /// na rede por proximidade.
    /// </summary>
    static void ColetarTubos(Document origem, Transform? transformacao, ElementId? vinculo, List<TuboPronto> tubos, bool soIncendio,
        ref bool usouNominal)
    {
        foreach (var tubo in new FilteredElementCollector(origem).OfClass(typeof(Pipe)).Cast<Pipe>())
        {
            if (tubo.Location is not LocationCurve local) continue;
            if (soIncendio && !EhIncendio(origem, tubo)) continue;
            var curva = transformacao is null ? local.Curve : local.Curve.CreateTransformed(transformacao);
            tubos.Add(new TuboPronto(curva, Metros(curva.Length), DiametroMm(tubo, ref usouNominal), vinculo is null ? tubo.Id : null));
        }
    }

    static bool EhIncendio(Document doc, Pipe tubo)
    {
        var tipo = tubo.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId();
        if (tipo is null || doc.GetElement(tipo) is not PipingSystemType sistema) return false;
        return sistema.SystemClassification is MEPSystemClassification.FireProtectWet or MEPSystemClassification.FireProtectDry
            or MEPSystemClassification.FireProtectPreaction or MEPSystemClassification.FireProtectOther;
    }

    static void ColetarPortas(Document origem, Transform? transformacao, ElementId? vinculo,
        Dictionary<(int X, int Y, int Z), List<(string No, XYZ Ponto)>> portas, Dictionary<string, No> nos)
    {
        var filtro = new ElementMulticategoryFilter(new[]
        {
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PipeAccessory,
        });
        foreach (var peca in new FilteredElementCollector(origem).WherePasses(filtro).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
        {
            var id = vinculo is null ? IdPeca(peca.Id) : $"F{vinculo.Value}:{peca.Id.Value}";
            foreach (var conector in Conectores(peca))
            {
                var ponto = transformacao is null ? conector.Origin : transformacao.OfPoint(conector.Origin);
                var celula = Celula(ponto);
                if (!portas.TryGetValue(celula, out var lista)) portas[celula] = lista = [];
                lista.Add((id, ponto));
                Garantir(nos, id, ponto);
            }
        }
    }

    static void IndexarTubo(Dictionary<(int X, int Y, int Z), List<int>> grade, Curve curva, int indice)
    {
        var pontos = new[] { curva.GetEndPoint(0), curva.Evaluate(0.5, true), curva.GetEndPoint(1) };
        var min = pontos[0];
        var max = pontos[0];
        foreach (var ponto in pontos)
        {
            min = new XYZ(Math.Min(min.X, ponto.X), Math.Min(min.Y, ponto.Y), Math.Min(min.Z, ponto.Z));
            max = new XYZ(Math.Max(max.X, ponto.X), Math.Max(max.Y, ponto.Y), Math.Max(max.Z, ponto.Z));
        }

        var folga = Pes(0.50);
        var c0 = Celula(min - new XYZ(folga, folga, folga));
        var c1 = Celula(max + new XYZ(folga, folga, folga));
        if (c1.X - c0.X > 400 || c1.Y - c0.Y > 400 || c1.Z - c0.Z > 80)
        {
            foreach (var ponto in pontos) AdicionarCelula(grade, Celula(ponto), indice);
            return;
        }
        for (var x = c0.X; x <= c1.X; x++)
        for (var y = c0.Y; y <= c1.Y; y++)
        for (var z = c0.Z; z <= c1.Z; z++)
        {
            var celula = (x, y, z);
            if (!grade.TryGetValue(celula, out var lista)) grade[celula] = lista = [];
            lista.Add(indice);
        }
    }

    static void AdicionarCelula(Dictionary<(int X, int Y, int Z), List<int>> grade, (int X, int Y, int Z) celula, int indice)
    {
        if (!grade.TryGetValue(celula, out var lista)) grade[celula] = lista = [];
        lista.Add(indice);
    }

    static IEnumerable<(int X, int Y, int Z)> Vizinhanca(XYZ ponto)
    {
        var c = Celula(ponto);
        for (var x = c.X - 1; x <= c.X + 1; x++)
        for (var y = c.Y - 1; y <= c.Y + 1; y++)
        for (var z = c.Z - 1; z <= c.Z + 1; z++)
            yield return (x, y, z);
    }

    static (int X, int Y, int Z) Celula(XYZ ponto)
    {
        const double passo = 2.0;
        return ((int)Math.Floor(ponto.X / passo), (int)Math.Floor(ponto.Y / passo), (int)Math.Floor(ponto.Z / passo));
    }

    static double ParametroNormalizado(Curve curva, double parametro)
    {
        var inicio = curva.GetEndParameter(0);
        var fim = curva.GetEndParameter(1);
        if (Math.Abs(fim - inicio) < 1e-9) return 0;
        return Math.Clamp((parametro - inicio) / (fim - inicio), 0, 1);
    }

    static void IndexarNo(Dictionary<(int X, int Y, int Z), List<string>> grade, string id, XYZ pes)
    {
        var celula = Celula(pes);
        if (!grade.TryGetValue(celula, out var lista)) grade[celula] = lista = [];
        lista.Add(id);
    }

    /// <param name="elemento">O tubo no modelo aberto; nulo se vem de vínculo, onde não dá para gravar nem etiquetar.</param>
    sealed class TuboPronto(Curve curva, double comprimento, double diametro, ElementId? elemento)
    {
        public Curve Curva { get; } = curva;
        public ElementId? Elemento { get; } = elemento;
        public double Comprimento { get; } = comprimento;
        public double Diametro { get; } = diametro;
        public List<Segmento> Partes { get; } = [];
    }

    sealed record Segmento(string De, string Para, double T0, double T1, double Comprimento, double Diametro);

    static double DiametroMm(Pipe tubo, ref bool usouNominal)
    {
        var interno = tubo.get_Parameter(BuiltInParameter.RBS_PIPE_INNER_DIAM_PARAM);
        if (interno is { HasValue: true } && interno.AsDouble() > 1e-6)
            return Milimetros(interno.AsDouble());

        usouNominal = true;
        var nominal = tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
        var pes = nominal is { HasValue: true } && nominal.AsDouble() > 1e-6 ? nominal.AsDouble() : tubo.Diameter;
        return Milimetros(pes);
    }

    static int? DiametroNominalMm(Pipe tubo)
    {
        var nominal = tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
        var pes = nominal is { HasValue: true } && nominal.AsDouble() > 1e-6 ? nominal.AsDouble() : tubo.Diameter;
        return pes > 1e-6 ? (int)Math.Round(Milimetros(pes)) : null;
    }

    static void Garantir(Dictionary<string, No> nos, string id, XYZ ponto)
    {
        if (!nos.ContainsKey(id))
            nos[id] = new No(id, Metros(ponto.X), Metros(ponto.Y), Metros(ponto.Z));
    }

    static Connector? ConectorLigado(Element elemento) =>
        Conectores(elemento).FirstOrDefault(c => c.IsConnected);

    static IEnumerable<Connector> Conectores(Element elemento)
    {
        ConnectorManager? gerente = elemento switch
        {
            Pipe tubo => tubo.ConnectorManager,
            FamilyInstance { MEPModel: { } modelo } => modelo.ConnectorManager,
            _ => null,
        };
        if (gerente is null) yield break;
        foreach (Connector conector in gerente.Connectors)
            if (EhFisico(conector.ConnectorType))
                yield return conector;
    }

    static string IdBico(Element elemento) => IdBico(elemento.Id);
    static string IdBico(ElementId id) => "S" + id.Value;
    static string IdPeca(ElementId id) => "F" + id.Value;

    static string Chave(XYZ ponto) =>
        $"{Math.Round(Metros(ponto.X) * 1000)}:{Math.Round(Metros(ponto.Y) * 1000)}:{Math.Round(Metros(ponto.Z) * 1000)}";

    static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);
    static double Milimetros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Millimeters);
    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);

    static XYZ EmPes(No no) => new(
        UnitUtils.ConvertToInternalUnits(no.X, UnitTypeId.Meters),
        UnitUtils.ConvertToInternalUnits(no.Y, UnitTypeId.Meters),
        UnitUtils.ConvertToInternalUnits(no.Z, UnitTypeId.Meters));
}

/// <summary>
/// A VGA escolhida não alcança nenhum bico pela rede lida: no modelo há um vão entre ela e a descida do setor, ou ela
/// não é a válvula de um setor. Quem chama pode cair para a leitura por pedaço de rede.
/// </summary>
sealed class VgaDesligadaException(string vga, double? distancia) : InvalidOperationException(
    $"A VGA {vga} não está ligada a nenhum bico pela rede do modelo" +
    (distancia is { } d ? $" (o tubo mais próximo da rede lida está a {d:0.0} m dela)" : "") +
    ". Provavelmente há um vão entre a válvula e a descida do setor no modelo.")
{
    public string Vga { get; } = vga;
}
