using Autodesk.Revit.DB.Plumbing;

namespace Spk.AddIn.Verificacao;

public enum Gravidade
{
    Erro,
    Aviso,
    Informacao,
}

/// <summary>Um lugar do modelo onde o problema aparece, com os elementos para selecionar.</summary>
public sealed record Ocorrencia(string Descricao, IReadOnlyList<ElementId> Elementos);

/// <summary>Um tipo de problema: o que é, como resolver e onde aparece.</summary>
public sealed class Grupo
{
    public required string Codigo { get; init; }
    public required Gravidade Gravidade { get; init; }
    public required string Titulo { get; init; }
    public required string ComoResolver { get; init; }
    public List<Ocorrencia> Ocorrencias { get; } = [];
}

public sealed class ResultadoVerificacao
{
    public List<Grupo> Grupos { get; } = [];
    public int Bicos { get; set; }
    public int Tubos { get; set; }
    public int Pecas { get; set; }
    public int Setores { get; set; }

    public int Erros => Grupos.Where(g => g.Gravidade == Gravidade.Erro).Sum(g => g.Ocorrencias.Count);
    public int Avisos => Grupos.Where(g => g.Gravidade == Gravidade.Aviso).Sum(g => g.Ocorrencias.Count);

    public string Resumo() =>
        $"{Bicos} bicos, {Tubos} tubos e {Pecas} conexões e válvulas verificados; {Setores} rede(s) separada(s). " +
        (Erros == 0 && Avisos == 0 ? "Nenhum problema encontrado." : $"{Erros} erro(s) e {Avisos} aviso(s).");

    /// <summary>Texto para copiar: cada grupo com as primeiras ocorrências.</summary>
    public string Texto(int porGrupo = 20)
    {
        var linhas = new List<string> { Resumo() };
        foreach (var g in Grupos)
        {
            linhas.Add("");
            linhas.Add($"[{g.Gravidade}] {g.Titulo} ({g.Ocorrencias.Count})");
            linhas.Add(g.ComoResolver);
            linhas.AddRange(g.Ocorrencias.Take(porGrupo).Select(o => $"  - {o.Descricao}"));
            if (g.Ocorrencias.Count > porGrupo) linhas.Add($"  ... e mais {g.Ocorrencias.Count - porGrupo}");
        }
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Confere, antes de calcular, o que impede o SPK de ler a rede pelas conexões do Revit: peças encostadas mas não
///     ligadas, tubos que terminam no meio de outro sem tê, bicos soltos, pontas abertas, tubos fora de sistema de
///     incêndio, famílias de bico não confirmadas e bicos que não chegam a nenhuma VGA. Só lê o modelo; não altera nada.
/// </summary>
public static class VerificacaoModelo
{
    /// <summary>Peças a menos que isto parecem ligadas no desenho. 5 cm: bem acima do erro de modelagem com snap.</summary>
    static readonly double Encosto = UnitUtils.ConvertToInternalUnits(0.05, UnitTypeId.Meters);

    const double Celula = 1.0;

    static readonly BuiltInCategory[] Categorias =
    [
        BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_FlexPipeCurves, BuiltInCategory.OST_PipeFitting,
        BuiltInCategory.OST_PipeAccessory, BuiltInCategory.OST_Sprinklers,
    ];

    sealed record Porta(Element Dono, Connector Conector, XYZ Ponto);

    public static ResultadoVerificacao Verificar(Document doc)
    {
        var resultado = new ResultadoVerificacao();
        var mapeamento = MapeamentoGravado.Tentar(doc);
        // Tipos de "sprinkler" marcados como Ignorar em Famílias (símbolos 2D, bicos fora do cálculo) não entram.
        var ignorados = (mapeamento ?? []).Where(l => l.Papel == "Ignorar").Select(l => (l.Familia, l.Tipo)).ToHashSet();
        var elementos = new FilteredElementCollector(doc).WherePasses(new ElementMulticategoryFilter(Categorias))
            .WhereElementIsNotElementType().ToElements()
            .Where(e => !(EhBico(e) && e is FamilyInstance fi && ignorados.Contains((fi.Symbol?.Family?.Name ?? "", fi.Symbol?.Name ?? ""))))
            .ToList();
        var bicos = elementos.Where(EhBico).ToList();
        var gerados = BicosDoModelo.Gerados(doc);
        var niveis = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderByDescending(l => l.ProjectElevation).ToList();
        string Onde(XYZ p) => VerificacaoModelo.Onde(niveis, p);
        var vgas = gerados.Count > 0 ? [] : MapeamentoGravado.Vgas(doc);

        // A rede do SPK: tudo o que se liga aos bicos pelos conectores, mais os tubos de sistema de incêndio soltos.
        var ligados = Ligados(bicos);
        var escopo = new HashSet<ElementId>(ligados);
        foreach (var tubo in elementos.OfType<Pipe>())
            if (EhIncendio(doc, tubo)) escopo.Add(tubo.Id);
        foreach (var vga in vgas) escopo.Add(vga.Id);

        var portas = elementos.SelectMany(e => Conectores(e).Select(c => new Porta(e, c, c.Origin))).ToList();
        var abertas = portas.Where(p => !p.Conector.IsConnected).ToList();
        var grade = new Dictionary<(int, int, int), List<Porta>>();
        foreach (var p in abertas)
        {
            var chave = Chave(p.Ponto);
            if (!grade.TryGetValue(chave, out var lista)) grade[chave] = lista = [];
            lista.Add(p);
        }
        var segmentos = Segmentos(elementos.OfType<MEPCurve>());

        var encostados = Novo(resultado, "encostado-sem-ligar", Gravidade.Erro, "Peças encostadas, mas não ligadas",
            "No desenho as peças se tocam, mas o Revit não as conectou, e o cálculo não passa por aí. Selecione e reconecte " +
            "(arrastar a ponta até o conector, ou apagar e redesenhar o trecho com a ferramenta de tubo).");
        var derivacoes = Novo(resultado, "derivacao-sem-conexao", Gravidade.Erro, "Tubo que termina no meio de outro, sem tê",
            "A ponta do tubo encosta no corpo de outro tubo, sem conexão. Para a água passar, a derivação precisa de um tê. " +
            "Corrigir põe o tê (ou a cruzeta) onde o tubo chega no eixo do outro; o que chega acima, abaixo ou de lado precisa " +
            "de subida e fica para Ligar tubos.");
        var bicosSoltos = Novo(resultado, "bico-sem-tubo", Gravidade.Erro, "Bico sem tubo",
            "O bico não está ligado a nenhum tubo nem conexão, e nada encosta nele. Ligue-o ao ramal (ferramenta Conectar a).");
        var semConector = Novo(resultado, "bico-sem-conector", Gravidade.Erro, "Família de bico sem conector de tubulação",
            "A família não tem conector de tubo, então o Revit não consegue ligá-la à rede. Troque por uma família de sprinkler com conector.");
        var pontas = Novo(resultado, "ponta-aberta", Gravidade.Aviso, "Tubo ou conexão com saída livre",
            "Uma ponta ficou sem continuação, tampão ou bico. O cálculo trata como ponta morta; confira se falta um pedaço de rede.");
        var entradas = Novo(resultado, "entrada-sem-vga", Gravidade.Informacao, "Entrada da rede sem VGA",
            "A ponta mais baixa de cada rede é tratada como entrada, porque não há VGA marcada como Fonte em Famílias.");

        var pares = new HashSet<(long, long)>();
        var derivados = new HashSet<(long, long)>();
        var comBicoLivre = new HashSet<ElementId>();
        foreach (var porta in abertas)
        {
            var dono = porta.Dono;
            // Ponta no corpo de um tubo é derivação, mesmo que encoste também na ponta de outro tubo: dois tubos chegando de
            // lados opostos ao mesmo ponto do tubo de passagem são uma cruzeta, não duas peças encostadas. Um tê com duas
            // bocas no mesmo tubo é uma derivação só.
            if (escopo.Contains(dono.Id) && Corpo(segmentos, porta) is { } passagem)
            {
                if (derivados.Add((dono.Id.Value, passagem.Id.Value)))
                    derivacoes.Ocorrencias.Add(new Ocorrencia($"{Nome(dono)} encosta no corpo de {Nome(passagem)}, em {Onde(porta.Ponto)}",
                        [dono.Id, passagem.Id]));
                continue;
            }
            var vizinha = Vizinha(grade, porta);
            if (vizinha is not null)
            {
                if (!escopo.Contains(dono.Id) && !escopo.Contains(vizinha.Dono.Id)) continue;
                var par = dono.Id.Value < vizinha.Dono.Id.Value ? (dono.Id.Value, vizinha.Dono.Id.Value) : (vizinha.Dono.Id.Value, dono.Id.Value);
                if (pares.Add(par))
                    encostados.Ocorrencias.Add(new Ocorrencia(
                        $"{Nome(dono)} e {Nome(vizinha.Dono)} a {Cm(porta.Ponto.DistanceTo(vizinha.Ponto))} cm, em {Onde(porta.Ponto)}",
                        [dono.Id, vizinha.Dono.Id]));
                continue;
            }

            if (!escopo.Contains(dono.Id)) continue;

            if (EhBico(dono))
            {
                if (Conectores(dono).All(c => !c.IsConnected) && comBicoLivre.Add(dono.Id))
                    bicosSoltos.Ocorrencias.Add(new Ocorrencia($"{Nome(dono)}, em {Onde(porta.Ponto)}", [dono.Id]));
                continue;
            }
            pontas.Ocorrencias.Add(new Ocorrencia($"{Nome(dono)}, ponta em {Onde(porta.Ponto)}", [dono.Id]));
        }

        foreach (var bico in bicos.Where(b => Conectores(b).Count == 0))
            semConector.Ocorrencias.Add(new Ocorrencia($"{Nome(bico)}", [bico.Id]));

        // Redes separadas (componentes ligados com bico) e a entrada de cada uma quando não há VGA: a ponta de baixo de um
        // tubo vertical (a descida), a mais baixa da rede. Ponta aberta em tubo deitado é falta de rede, não entrada.
        var componentes = Componentes(bicos.Where(b => Conectores(b).Count > 0)).Where(c => c.Count > 1).ToList();
        resultado.Setores = componentes.Count;
        if (vgas.Count == 0)
        {
            foreach (var componente in componentes)
            {
                var maisBaixa = pontas.Ocorrencias
                    .Select(o => (Ocorrencia: o, Elemento: o.Elementos[0]))
                    .Where(p => componente.Contains(p.Elemento) && PontaDeBaixoDeDescida(doc.GetElement(p.Elemento)))
                    .Select(p => (p.Ocorrencia, Z: PontoMaisBaixo(doc.GetElement(p.Elemento))))
                    .OrderBy(p => p.Z)
                    .FirstOrDefault();
                if (maisBaixa.Ocorrencia is null) continue;
                pontas.Ocorrencias.Remove(maisBaixa.Ocorrencia);
                entradas.Ocorrencias.Add(maisBaixa.Ocorrencia);
            }
        }
        else
        {
            var semVga = Novo(resultado, "bico-sem-vga", Gravidade.Erro, "Bicos que não chegam a nenhuma VGA",
                "Seguindo as conexões a partir destes bicos, não se chega a nenhuma válvula marcada como Fonte. Falta ligação " +
                "entre a rede e a VGA (veja os itens \"encostadas\" e \"sem tê\").");
            var comVga = vgas.Select(v => v.Id).ToHashSet();
            foreach (var componente in componentes.Where(c => !c.Overlaps(comVga)))
            {
                var bicosDoPedaco = componente.Where(id => doc.GetElement(id) is { } e && EhBico(e)).ToList();
                semVga.Ocorrencias.Add(new Ocorrencia($"{bicosDoPedaco.Count} bico(s) numa rede sem VGA", bicosDoPedaco));
            }
        }

        var sistemas = Novo(resultado, "sistema-nao-incendio", Gravidade.Erro, "Tubos ligados aos bicos fora de sistema de incêndio",
            "Tubos de sprinkler precisam de um tipo de sistema de proteção contra incêndio (molhado, seco ou pré-ação). " +
            "Selecione e troque o Tipo de sistema nas propriedades.");
        foreach (var grupo in elementos.OfType<Pipe>().Where(t => ligados.Contains(t.Id) && !EhIncendio(doc, t) && TipoDeSistema(doc, t) is not null)
                     .GroupBy(t => TipoDeSistema(doc, t)!.Name))
            sistemas.Ocorrencias.Add(new Ocorrencia($"{grupo.Count()} tubo(s) no sistema \"{grupo.Key}\"", grupo.Select(t => t.Id).ToList()));

        if (gerados.Count == 0)
        {
            var familias = Novo(resultado, "bico-nao-mapeado", Gravidade.Erro, "Bicos sem fator K e área confirmados",
                "Em Famílias, marque estes tipos como Chuveiro e informe o fator K (L/min/bar½) e a área de cobertura (m²).");
            var confirmados = (mapeamento ?? []).Where(l => l.EhChuveiroComMedidas).Select(l => (l.Familia, l.Tipo)).ToHashSet();
            foreach (var tipo in bicos.OfType<FamilyInstance>()
                         .GroupBy(b => (b.Symbol?.Family?.Name ?? "?", b.Symbol?.Name ?? "?"))
                         .Where(g => !confirmados.Contains(g.Key)))
                familias.Ocorrencias.Add(new Ocorrencia($"{tipo.Key.Item1} : {tipo.Key.Item2} ({tipo.Count()} no modelo)",
                    tipo.Select(b => b.Id).ToList()));

            if (vgas.Count == 0)
                Novo(resultado, "sem-vga", Gravidade.Aviso, "Nenhuma VGA marcada como Fonte",
                    "Sem VGA, o cálculo começa na ponta de tubo mais baixa de cada rede. Em Famílias, marque a válvula de cada setor como Fonte.")
                    .Ocorrencias.Add(new Ocorrencia("Nenhuma família com o papel Fonte no mapeamento", []));
        }

        resultado.Bicos = bicos.Count;
        resultado.Tubos = elementos.Count(e => e is MEPCurve && escopo.Contains(e.Id));
        resultado.Pecas = elementos.Count(e => e is FamilyInstance && !EhBico(e) && escopo.Contains(e.Id));
        resultado.Grupos.RemoveAll(g => g.Ocorrencias.Count == 0);
        resultado.Grupos.Sort((a, b) => a.Gravidade.CompareTo(b.Gravidade));
        return resultado;
    }

    static Grupo Novo(ResultadoVerificacao resultado, string codigo, Gravidade gravidade, string titulo, string comoResolver)
    {
        var grupo = new Grupo { Codigo = codigo, Gravidade = gravidade, Titulo = titulo, ComoResolver = comoResolver };
        resultado.Grupos.Add(grupo);
        return grupo;
    }

    static bool EhBico(Element e) => e.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers;

    /// <summary>Conectores de tubulação de ponta (não os de "curva" que os tubos têm para derivações).</summary>
    static List<Connector> Conectores(Element e)
    {
        var gerente = e switch
        {
            MEPCurve curva => curva.ConnectorManager,
            FamilyInstance instancia => instancia.MEPModel?.ConnectorManager,
            _ => null,
        };
        if (gerente is null) return [];
        return gerente.Connectors.Cast<Connector>()
            .Where(c => c.ConnectorType == ConnectorType.End && c.Domain == Domain.DomainPiping)
            .ToList();
    }

    static IEnumerable<Element> Vizinhos(Element e) =>
        Conectores(e).Where(c => c.IsConnected).SelectMany(c => c.AllRefs.Cast<Connector>())
            .Select(r => r.Owner).Where(o => o is not null && o.Id != e.Id && o.Category is not null
                                              && Categorias.Any(c => (long)c == o.Category.Id.Value));

    static HashSet<ElementId> Ligados(IEnumerable<Element> inicio)
    {
        var vistos = new HashSet<ElementId>();
        var fila = new Queue<Element>(inicio);
        while (fila.Count > 0)
        {
            var e = fila.Dequeue();
            if (!vistos.Add(e.Id)) continue;
            foreach (var v in Vizinhos(e))
                if (!vistos.Contains(v.Id)) fila.Enqueue(v);
        }
        return vistos;
    }

    /// <summary>Cada rede separada que tem bico: o conjunto de tudo o que se liga a ela.</summary>
    static List<HashSet<ElementId>> Componentes(IEnumerable<Element> bicos)
    {
        var lista = new List<HashSet<ElementId>>();
        var vistos = new HashSet<ElementId>();
        foreach (var bico in bicos)
        {
            if (vistos.Contains(bico.Id)) continue;
            var componente = Ligados([bico]);
            vistos.UnionWith(componente);
            lista.Add(componente);
        }
        return lista;
    }

    static (int, int, int) Chave(XYZ p) => ((int)Math.Floor(p.X / Celula), (int)Math.Floor(p.Y / Celula), (int)Math.Floor(p.Z / Celula));

    /// <summary>A porta aberta de outro elemento mais próxima, a menos de <see cref="Encosto" />.</summary>
    static Porta? Vizinha(Dictionary<(int, int, int), List<Porta>> grade, Porta porta)
    {
        var (x, y, z) = Chave(porta.Ponto);
        Porta? melhor = null;
        var menor = Encosto;
        for (var i = -1; i <= 1; i++)
        for (var j = -1; j <= 1; j++)
        for (var k = -1; k <= 1; k++)
        {
            if (!grade.TryGetValue((x + i, y + j, z + k), out var lista)) continue;
            foreach (var outra in lista)
            {
                if (outra.Dono.Id == porta.Dono.Id) continue;
                var d = outra.Ponto.DistanceTo(porta.Ponto);
                if (d <= menor)
                {
                    menor = d;
                    melhor = outra;
                }
            }
        }
        return melhor;
    }

    sealed record Segmento(MEPCurve Tubo, XYZ A, XYZ B, double Raio);

    static Dictionary<(int, int, int), List<Segmento>> Segmentos(IEnumerable<MEPCurve> tubos)
    {
        var grade = new Dictionary<(int, int, int), List<Segmento>>();
        foreach (var tubo in tubos)
        {
            if ((tubo.Location as LocationCurve)?.Curve is not Line linha) continue;
            var raio = (tubo.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER)?.AsDouble()
                        ?? tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0) / 2;
            var s = new Segmento(tubo, linha.GetEndPoint(0), linha.GetEndPoint(1), raio);
            var (a, b) = (Chave(s.A), Chave(s.B));
            for (var i = Math.Min(a.Item1, b.Item1); i <= Math.Max(a.Item1, b.Item1); i++)
            for (var j = Math.Min(a.Item2, b.Item2); j <= Math.Max(a.Item2, b.Item2); j++)
            for (var k = Math.Min(a.Item3, b.Item3); k <= Math.Max(a.Item3, b.Item3); k++)
            {
                if (!grade.TryGetValue((i, j, k), out var lista)) grade[(i, j, k)] = lista = [];
                lista.Add(s);
            }
        }
        return grade;
    }

    /// <summary>Tubo em cujo corpo (longe das pontas) a porta encosta.</summary>
    static MEPCurve? Corpo(Dictionary<(int, int, int), List<Segmento>> grade, Porta porta)
    {
        var (x, y, z) = Chave(porta.Ponto);
        for (var i = -1; i <= 1; i++)
        for (var j = -1; j <= 1; j++)
        for (var k = -1; k <= 1; k++)
        {
            if (!grade.TryGetValue((x + i, y + j, z + k), out var lista)) continue;
            foreach (var s in lista)
            {
                if (s.Tubo.Id == porta.Dono.Id) continue;
                var ab = s.B - s.A;
                var comprimento = ab.GetLength();
                if (comprimento < 1e-6) continue;
                var t = (porta.Ponto - s.A).DotProduct(ab) / (comprimento * comprimento);
                var aoLongo = t * comprimento;
                if (aoLongo <= Encosto || comprimento - aoLongo <= Encosto) continue;
                var projecao = s.A + ab * t;
                if (projecao.DistanceTo(porta.Ponto) <= s.Raio + Encosto) return s.Tubo;
            }
        }
        return null;
    }

    static PipingSystemType? TipoDeSistema(Document doc, Pipe tubo) =>
        doc.GetElement(tubo.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId() ?? ElementId.InvalidElementId)
            as PipingSystemType;

    static bool EhIncendio(Document doc, Pipe tubo) =>
        TipoDeSistema(doc, tubo)?.SystemClassification is MEPSystemClassification.FireProtectWet or MEPSystemClassification.FireProtectDry
            or MEPSystemClassification.FireProtectPreaction or MEPSystemClassification.FireProtectOther;

    /// <summary>Tubo quase vertical com a ponta de baixo aberta.</summary>
    static bool PontaDeBaixoDeDescida(Element? e)
    {
        if (e is not MEPCurve { Location: LocationCurve { Curve: Line linha } } tubo || Math.Abs(linha.Direction.Z) < 0.9) return false;
        var baixo = linha.GetEndPoint(0).Z < linha.GetEndPoint(1).Z ? linha.GetEndPoint(0) : linha.GetEndPoint(1);
        return Conectores(tubo).Any(c => !c.IsConnected && c.Origin.DistanceTo(baixo) < Encosto);
    }

    static double PontoMaisBaixo(Element? e) =>
        e is null ? double.MaxValue : Conectores(e).Where(c => !c.IsConnected).Select(c => c.Origin.Z).DefaultIfEmpty(double.MaxValue).Min();

    static string Nome(Element e) =>
        e switch
        {
            Pipe t => $"tubo Ø{UnitUtils.ConvertFromInternalUnits(t.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0, UnitTypeId.Millimeters):0} ({e.Id.Value})",
            FamilyInstance fi when EhBico(fi) => $"bico {fi.Symbol?.Name} ({e.Id.Value})",
            FamilyInstance fi => $"{fi.Symbol?.Family?.Name} ({e.Id.Value})",
            _ => $"{e.Category?.Name} ({e.Id.Value})",
        };

    static string Cm(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Centimeters).ToString("0.0", Entrada.Cultura);

    /// <summary>Coordenadas em metros e o nível mais próximo abaixo, para achar o ponto no modelo.</summary>
    static string Onde(List<Level> niveis, XYZ p)
    {
        string M(double v) => UnitUtils.ConvertFromInternalUnits(v, UnitTypeId.Meters).ToString("0.00", Entrada.Cultura);
        var nivel = niveis.FirstOrDefault(l => l.ProjectElevation <= p.Z + 0.01);
        return $"x {M(p.X)}, y {M(p.Y)}, z {M(p.Z)} m{(nivel is null ? "" : $" ({nivel.Name})")}";
    }
}
