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

    public sealed record RedeLida(Rede Rede, IReadOnlyList<string> Avisos, int Tubos);

    public static RedeLida Montar(Document doc, IReadOnlyList<BicoLido> bicos, double fatorC)
    {
        if (bicos.Count == 0) throw new InvalidOperationException("Nenhum bico com fator K e área de cobertura.");

        var idsBico = new HashSet<ElementId>(bicos.Select(b => b.Instancia.Id));
        var naRede = Alcançaveis(doc, idsBico);
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
            var diagnostico = AnexarPorProximidade(doc, bicos, nos, trechos, ref seq, ref usouNominal, fatorC);
            grau.Clear();
            foreach (var trecho in trechos)
            {
                grau[trecho.De] = grau.GetValueOrDefault(trecho.De) + 1;
                grau[trecho.Para] = grau.GetValueOrDefault(trecho.Para) + 1;
            }
            ligados = bicos.Where(b => grau.GetValueOrDefault(IdBico(b.Instancia)) > 0).ToList();
            if (ligados.Count == 0) throw new InvalidOperationException(diagnostico);
            avisos.Add("Os bicos não estavam conectados ao tubo no Revit. Cada um entrou no tubo mais próximo, até 0,50 m.");
        }

        var soltos = bicos.Count - ligados.Count;
        if (soltos > 0) avisos.Add($"{soltos} bico(s) sem tubo ligado ficaram de fora.");
        if (ligados.Count == 0) throw new InvalidOperationException("Nenhum bico confirmado está ligado a um tubo.");
        if (trechos.Count == 0) throw new InvalidOperationException("Não há tubos na rede ligada aos bicos.");

        var (dx, dy) = DirecaoDosRamais(trechos, nos);
        var chuveiros = Ramais(ligados, nos, dx, dy);
        var componente = MaiorComponente(nos, trechos, chuveiros);
        if (componente.Chuveiros.Count < chuveiros.Count)
            avisos.Add($"{chuveiros.Count - componente.Chuveiros.Count} bicos ficaram de fora: o tubo deles não encontra o restante da rede.");
        var cotaFonte = componente.Nos.First(n => n.Id == componente.Fonte).Z;
        avisos.Add($"Fonte adotada: nó {componente.Fonte}, cota {cotaFonte:0.00} m. {componente.Chuveiros.Count} bicos estão ligados a ela.");

        var rede = new Rede(componente.Fonte, componente.Nos, componente.Trechos, componente.Chuveiros);
        return new RedeLida(rede, avisos, componente.Trechos.Count);
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

    static HashSet<ElementId> Alcançaveis(Document doc, HashSet<ElementId> idsBico)
    {
        var visitados = new HashSet<ElementId>();
        var fila = new Queue<ElementId>(idsBico);
        while (fila.Count > 0)
        {
            var id = fila.Dequeue();
            if (!visitados.Add(id)) continue;
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
    /// e puxa os tubos contínuos, soldando pontas a até 8 cm e portas da mesma conexão.
    /// </summary>
    static string AnexarPorProximidade(Document doc, IReadOnlyList<BicoLido> bicos, Dictionary<string, No> nos, List<Trecho> trechos, ref int seq, ref bool usouNominal, double fatorC)
    {
        var tubos = new List<TuboPronto>();
        ColetarTubos(doc, null, null, tubos, ref usouNominal);
        foreach (var vinculo in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            var vinculado = vinculo.GetLinkDocument();
            if (vinculado is null) continue;
            ColetarTubos(vinculado, vinculo.GetTotalTransform(), vinculo.Id, tubos, ref usouNominal);
        }

        if (tubos.Count == 0)
            return "Nenhum bico confirmado está ligado a um tubo. O modelo aberto não tem tubos do Revit, nem em vínculos carregados.";

        var grade = new Dictionary<(int X, int Y, int Z), List<int>>();
        for (var i = 0; i < tubos.Count; i++) IndexarTubo(grade, tubos[i].Curva, i);

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
            return $"Nenhum bico confirmado está ligado a um tubo. Há {tubos.Count} tubos no modelo e nos vínculos. "
                   + (mm <= 0
                       ? "Nenhum tubo chegou perto dos bicos."
                       : $"O bico mais próximo de um tubo está a {mm:0} mm, além do limite de 500 mm.");
        }

        var fila = new Queue<int>(sementes.Select(s => s.Indice).Distinct());
        var visitados = new HashSet<int>();
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
        }

        foreach (var grupo in sementes.GroupBy(s => s.Indice))
            foreach (var semente in grupo.OrderByDescending(s => s.T))
                InserirBico(tubos[semente.Indice], semente.T, semente.IdBico, nos);

        foreach (var tubo in tubos)
            foreach (var parte in tubo.Partes)
                if (parte.De != parte.Para && parte.Comprimento > 1e-4)
                    trechos.Add(new Trecho($"G{seq++}", parte.De, parte.Para, parte.Diametro, parte.Comprimento, fatorC));

        return "";
    }

    static void InserirBico(TuboPronto tubo, double t, string idBico, Dictionary<string, No> nos)
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

        var existente = NoProximo(pes, nosPerto, nos, 0.08);
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

    static IEnumerable<int> VizinhosDePonta(int indice, Curve curva, List<TuboPronto> tubos, Dictionary<(int X, int Y, int Z), List<int>> grade)
    {
        var limite = Pes(0.20);
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

    static void ColetarTubos(Document origem, Transform? transformacao, ElementId? vinculo, List<TuboPronto> tubos, ref bool usouNominal)
    {
        foreach (var tubo in new FilteredElementCollector(origem).OfClass(typeof(Pipe)).Cast<Pipe>())
        {
            if (tubo.Location is not LocationCurve local) continue;
            var curva = transformacao is null ? local.Curve : local.Curve.CreateTransformed(transformacao);
            tubos.Add(new TuboPronto(curva, Metros(curva.Length), DiametroMm(tubo, ref usouNominal)));
        }
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

    sealed class TuboPronto(Curve curva, double comprimento, double diametro)
    {
        public Curve Curva { get; } = curva;
        public double Comprimento { get; } = comprimento;
        public double Diametro { get; } = diametro;
        public List<Segmento> Partes { get; } = [];
    }

    sealed record Segmento(string De, string Para, double T0, double T1, double Comprimento, double Diametro);

    sealed record ComponenteRede(string Fonte, List<No> Nos, List<Trecho> Trechos, List<Chuveiro> Chuveiros);

    /// <summary>
    /// Vários bicos podem cair em trechos soltos. O cálculo usa só o grupo que tem mais bicos
    /// e escolhe a fonte dentro desse grupo.
    /// </summary>
    static ComponenteRede MaiorComponente(Dictionary<string, No> nos, List<Trecho> trechos, List<Chuveiro> chuveiros)
    {
        var adj = new Dictionary<string, List<string>>();
        void Ligar(string a, string b)
        {
            if (!adj.TryGetValue(a, out var lista)) adj[a] = lista = [];
            lista.Add(b);
        }
        foreach (var trecho in trechos)
        {
            Ligar(trecho.De, trecho.Para);
            Ligar(trecho.Para, trecho.De);
        }
        foreach (var id in nos.Keys)
            if (!adj.ContainsKey(id)) adj[id] = [];

        var noDeBico = new HashSet<string>(chuveiros.Select(c => c.No));
        var visto = new HashSet<string>();
        List<string>? melhor = null;
        var melhorQtd = -1;
        foreach (var inicio in nos.Keys)
        {
            if (!visto.Add(inicio)) continue;
            var comp = new List<string>();
            var fila = new Queue<string>();
            fila.Enqueue(inicio);
            var qtd = 0;
            while (fila.Count > 0)
            {
                var n = fila.Dequeue();
                comp.Add(n);
                if (noDeBico.Contains(n)) qtd++;
                if (!adj.TryGetValue(n, out var vizinhos)) continue;
                foreach (var vizinho in vizinhos)
                    if (visto.Add(vizinho)) fila.Enqueue(vizinho);
            }
            if (qtd > melhorQtd) { melhorQtd = qtd; melhor = comp; }
        }

        if (melhor is null || melhorQtd == 0)
            throw new InvalidOperationException("Os bicos não formam uma rede contínua com os tubos encontrados.");

        var conjunto = new HashSet<string>(melhor);
        var nosComp = nos.Values.Where(n => conjunto.Contains(n.Id)).ToList();
        var trechosComp = trechos.Where(t => conjunto.Contains(t.De) && conjunto.Contains(t.Para)).ToList();
        var chComp = chuveiros.Where(c => conjunto.Contains(c.No)).ToList();
        var grau = new Dictionary<string, int>();
        foreach (var trecho in trechosComp)
        {
            grau[trecho.De] = grau.GetValueOrDefault(trecho.De) + 1;
            grau[trecho.Para] = grau.GetValueOrDefault(trecho.Para) + 1;
        }

        var fonte = nosComp
            .Where(n => !n.Id.StartsWith('S') && grau.GetValueOrDefault(n.Id) == 1)
            .OrderBy(n => n.Z)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .Select(n => n.Id)
            .FirstOrDefault()
            ?? nosComp.Where(n => !n.Id.StartsWith('S')).OrderBy(n => n.Z).Select(n => n.Id).FirstOrDefault()
            ?? throw new InvalidOperationException("A rede dos bicos não tem uma ponta de tubo para servir de fonte.");

        return new ComponenteRede(fonte, nosComp, trechosComp, chComp);
    }

    static (double Dx, double Dy) DirecaoDosRamais(List<Trecho> trechos, Dictionary<string, No> nos)
    {
        double dx = 0, dy = 0;
        var tem = false;
        foreach (var trecho in trechos)
        {
            if (trecho.Comprimento < 1) continue;
            var a = nos[trecho.De];
            var b = nos[trecho.Para];
            double vx = b.X - a.X, vy = b.Y - a.Y;
            var norma = Math.Sqrt(vx * vx + vy * vy);
            if (norma < 1e-6) continue;
            vx /= norma;
            vy /= norma;
            if (!tem) { dx = vx; dy = vy; tem = true; }
            else
            {
                if (vx * dx + vy * dy < 0) { vx = -vx; vy = -vy; }
                dx += vx;
                dy += vy;
            }
        }

        var n = Math.Sqrt(dx * dx + dy * dy);
        return n < 1e-6 ? (1, 0) : (dx / n, dy / n);
    }

    /// <summary>Agrupa bicos cuja posição transversal difere menos de 0,45 m: cada grupo é um ramal.</summary>
    static List<Chuveiro> Ramais(List<BicoLido> bicos, Dictionary<string, No> nos, double dx, double dy)
    {
        var ordenados = bicos
            .Select(b => (Bico: b, No: nos[IdBico(b.Instancia)]))
            .OrderBy(p => -dy * p.No.X + dx * p.No.Y)
            .ToList();

        var grupos = new List<List<(BicoLido Bico, No No)>>();
        foreach (var ponto in ordenados)
        {
            var offset = -dy * ponto.No.X + dx * ponto.No.Y;
            if (grupos.Count == 0 || offset - Offset(grupos[^1][^1].No, dx, dy) > 0.45)
                grupos.Add([]);
            grupos[^1].Add(ponto);
        }

        var chuveiros = new List<Chuveiro>();
        for (var i = 0; i < grupos.Count; i++)
        {
            foreach (var ponto in grupos[i])
            {
                chuveiros.Add(new Chuveiro($"E{ponto.Bico.Instancia.Id.Value}", ponto.No.Id, ponto.Bico.FatorK, ponto.Bico.AreaCobertura)
                {
                    Ramal = $"R{i}",
                    PressaoMinima = ponto.Bico.PressaoMinima,
                });
            }
        }
        return chuveiros;
    }

    static double Offset(No no, double dx, double dy) => -dy * no.X + dx * no.Y;

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

    static string IdBico(Element elemento) => "S" + elemento.Id.Value;
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
