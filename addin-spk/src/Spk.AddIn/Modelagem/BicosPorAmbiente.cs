using Autodesk.Revit.DB.Structure;
using Spk.Motor.Geracao;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.AddIn.Modelagem;

public enum DirecaoDosRamais { LadoMaior, EixoX, EixoY }

/// <summary>Como posicionar: o tipo de bico, a distância do defletor ao teto, o tipo de teto e a direção dos ramais.</summary>
public sealed record OpcoesBicos
{
    /// <summary>"Família : Tipo" do bico; vazio usa o bico confirmado em Famílias ou o primeiro carregado.</summary>
    public string? Tipo { get; init; }

    /// <summary>Defletor abaixo do teto liso, em m (8.13.1.1.1: de 0,025 a 0,300).</summary>
    public double DefletorM { get; init; } = 0.10;

    /// <summary>Defletor abaixo do fundo da viga, em m (8.13.1.2: de 0,025 a 0,150).</summary>
    public double AbaixoDaVigaM { get; init; } = 0.05;

    public TipoDeTeto Teto { get; init; } = TipoDeTeto.IncombustivelOuNaoObstruido;
    public DirecaoDosRamais Direcao { get; init; } = DirecaoDosRamais.LadoMaior;

    /// <summary>Direção dos ramais em planta, no lugar de <see cref="Direcao" /> (Gerar rede: paralela à geral).</summary>
    public Ponto2? Rumo { get; init; }
}

/// <summary>O que aconteceu num ambiente: bicos criados (ou existentes, na conferência), falhas e o motivo de recusa.</summary>
public sealed record ResultadoAmbiente(Ambiente Ambiente, int Bicos, double? Defletor, string Altura, IReadOnlyList<FalhaDeEspacamento> Falhas, string? Recusa)
{
    /// <summary>Os bicos criados (ao posicionar) e a grade usada.</summary>
    public IReadOnlyList<ElementId> Criados { get; init; } = [];
    public PosicaoDeBicos? Posicao { get; init; }

    /// <summary>As vigas fundas do ambiente, quando os bicos ficaram nos vãos entre elas (nulo: teto liso ou abaixo da viga).</summary>
    public IReadOnlyList<Viga>? VaosEntreVigas { get; init; }

    public string Texto()
    {
        var nome = $"{Ambiente.Numero} {Ambiente.Nome}".Trim();
        if (Recusa is not null) return $"{nome}: {Recusa}";
        var linha = $"{nome}: {Bicos} bico(s), {Altura}";
        return Falhas.Count == 0 ? linha + "; espaçamento ok"
            : linha + "\n" + string.Join("\n", Falhas.GroupBy(f => f.Descricao).Select(g => $"   • {g.Key}{(g.Count() > 1 ? $" ({g.Count()})" : "")}"));
    }
}

/// <summary>
///     Posiciona bicos nos ambientes da arquitetura (<see cref="Arquitetura.LeituraDoEdificio" />) pela regra de espaçamento
///     (<see cref="BicosNoAmbiente" />), com o defletor na altura que o teto pede: sob teto liso, a <see cref="OpcoesBicos.DefletorM" />
///     do forro ou da laje; com viga no ambiente, abaixo do fundo da viga mais baixa, se ficar a até 560 mm do teto
///     (8.13.1.2); com viga mais funda, nos vãos entre as vigas, sob a laje (vigas paralelas; a grelha ainda não é
///     automática). Também confere os bicos que já
///     existem em cada ambiente. Cada ambiente numa transação própria; tudo num grupo, um Ctrl+Z desfaz.
/// </summary>
static class BicosPorAmbiente
{
    public static LimitesDeBico Limites(DadosProjeto dados, OpcoesBicos opcoes)
    {
        var densidade = dados.Base == TipoBase.DensidadeArea ? dados.DensidadeLminM2
            : Nbr10897.EhRiscoExtra(dados.Classe) ? double.MaxValue : 0; // risco extra sem densidade: os limites mais estreitos
        return EspacamentoDeBicos.Padrao(dados.Classe, opcoes.Teto, densidade: densidade);
    }

    public static IReadOnlyList<ResultadoAmbiente> Posicionar(Document doc, Edificio edificio, IEnumerable<Ambiente> ambientes,
        LimitesDeBico limites, OpcoesBicos opcoes, List<ElementId> criados)
    {
        var simbolo = Simbolo(doc, opcoes.Tipo) ?? throw new InvalidOperationException("O projeto não tem família de sprinkler carregada.");
        if (Hospedado(simbolo))
            throw new InvalidOperationException($"O bico \"{Nome(simbolo)}\" é de família hospedada (presa a uma face): o SPK posiciona bicos pelo nível " +
                                                "e ela não sobe até o teto. Escolha ou carregue um bico sem hospedeiro.");
        var marcar = !EhConfirmado(doc, simbolo);
        var resultados = new List<ResultadoAmbiente>();
        using var grupo = new TransactionGroup(doc, "SPK — Bicos por ambiente");
        grupo.Start();
        foreach (var ambiente in ambientes)
        {
            var (defletor, altura, recusa, vaos) = Defletor(edificio, ambiente, limites, opcoes);
            if (recusa is not null)
            {
                resultados.Add(new ResultadoAmbiente(ambiente, 0, null, "", [], recusa));
                continue;
            }
            var posicao = vaos is null
                ? BicosNoAmbiente.Posicionar(ambiente.Contornos, limites, (opcoes.Rumo ?? Direcao(opcoes.Direcao)))
                : BicosNoAmbiente.PosicionarPorVaos(ambiente.Contornos, vaos.Select(v => (v.A, v.B, v.Largura)).ToList(), limites);
            if (posicao is null)
            {
                resultados.Add(new ResultadoAmbiente(ambiente, 0, null, "", [],
                    "vigas fundas fora de duas direções em ângulo reto (viga inclinada): os vãos entre elas ainda não são automáticos"));
                continue;
            }
            // Vãos estreitos (vigotas, terças): um bico por vão deixaria bicos a menos da distância mínima uns dos outros.
            if (vaos is not null && BicosNoAmbiente.Conferir(ambiente.Contornos, posicao.Bicos, limites, posicao.DirecaoDosRamais)
                    .FirstOrDefault(f => f.Tipo == TipoDeFalha.PertoDeOutroBico) is { } perto)
            {
                resultados.Add(new ResultadoAmbiente(ambiente, 0, null, "", [],
                    $"vigas fundas e próximas umas das outras (vigotas): um bico por vão deixaria bicos {perto.Descricao}. " +
                    "A regra para elementos estruturais próximos é outra: posicione à mão"));
                continue;
            }
            var deste = new List<ElementId>();
            var motivo = TransacaoSegura.Executar(doc, "SPK — Bicos no ambiente", () =>
                Criar(doc, simbolo, posicao.Bicos, ambiente.Piso, defletor!.Value, marcar, deste));
            if (motivo is not null)
            {
                resultados.Add(new ResultadoAmbiente(ambiente, 0, null, "", [], motivo));
                continue;
            }
            criados.AddRange(deste);
            resultados.Add(new ResultadoAmbiente(ambiente, posicao.Bicos.Count, defletor, altura,
                BicosNoAmbiente.Conferir(ambiente.Contornos, posicao.Bicos, limites, posicao.DirecaoDosRamais), null)
            {
                Criados = deste,
                Posicao = posicao,
                VaosEntreVigas = vaos,
            });
        }
        grupo.Assimilate();
        return resultados;
    }

    /// <summary>Confere os bicos que já estão em cada ambiente: espaçamento, área por bico, paredes e altura do defletor.</summary>
    public static IReadOnlyList<ResultadoAmbiente> Conferir(Document doc, Edificio edificio, IEnumerable<Ambiente> ambientes,
        LimitesDeBico limites, OpcoesBicos opcoes)
    {
        var bicos = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sprinklers).WhereElementIsNotElementType()
            .OfType<FamilyInstance>().Select(b => (Bico: b, Ponto: Ponto(b), Defletor: AlturaDoDefletor(b))).Where(b => b.Ponto is not null).ToList();
        var resultados = new List<ResultadoAmbiente>();
        foreach (var ambiente in ambientes)
        {
            var teto = ambiente.Teto ?? ambiente.Piso + 3;
            var dentro = bicos.Where(b => b.Defletor > ambiente.Piso && b.Defletor < teto + 0.05
                                          && BicosNoAmbiente.Dentro(ambiente.Contornos, b.Ponto!.Value)).ToList();
            var falhas = BicosNoAmbiente.Conferir(ambiente.Contornos, dentro.Select(b => b.Ponto!.Value).ToList(), limites,
                (opcoes.Rumo ?? Direcao(opcoes.Direcao))).ToList();
            var (esperado, _, _, _) = Defletor(edificio, ambiente, limites, opcoes);
            var abaixoDaViga = Vigas(edificio, ambiente).Any();
            foreach (var b in dentro)
            {
                var distancia = teto - b.Defletor;
                var maximo = abaixoDaViga ? EspacamentoDeBicos.DefletorMaximoComViga : limites.DefletorMaximo;
                if (ambiente.Teto is not null && (distancia < limites.DefletorMinimo - 1e-3 || distancia > maximo + 1e-3))
                    falhas.Add(new FalhaDeEspacamento(TipoDeFalha.Defletor, b.Ponto!.Value,
                        $"defletor a {distancia * 1000:0} mm do teto (de {limites.DefletorMinimo * 1000:0} a {maximo * 1000:0} mm, 8.13.1)"));
            }
            var altura = dentro.Count == 0 ? "sem bicos" : $"defletor de {dentro.Min(b => b.Defletor):0.00} a {dentro.Max(b => b.Defletor):0.00}";
            resultados.Add(new ResultadoAmbiente(ambiente, dentro.Count, esperado, altura, falhas, null));
        }
        return resultados;
    }

    /// <summary>
    /// Cota do defletor: sob teto liso, o teto menos o afastamento pedido; com viga no ambiente, abaixo do fundo da viga
    /// mais baixa, desde que fique a até 560 mm do teto; com viga mais funda, nos vãos entre elas, sob a laje (as vigas
    /// voltam para posicionar os bicos por vão).
    /// </summary>
    static (double? Defletor, string Texto, string? Recusa, IReadOnlyList<Viga>? Vaos) Defletor(Edificio edificio, Ambiente ambiente,
        LimitesDeBico limites, OpcoesBicos opcoes)
    {
        if (ambiente.Teto is not { } teto)
            return (null, "", "sem forro, laje ou telhado acima: não dá para saber a altura do bico", null);
        // Sem nada acima (o teto saiu da altura do ambiente): área descoberta, como terraço ou jardim no telhado. Sem teto
        // não há onde o calor se acumular para abrir o bico; o engenheiro decide como proteger.
        if (ambiente.OrigemDoTeto == OrigemDoTeto.AlturaDoAmbiente)
            return (null, "", "sem forro, laje ou telhado acima (área descoberta): o SPK não põe bicos; confira na arquitetura", null);
        var vigas = Vigas(edificio, ambiente).ToList();
        var afastamento = Math.Max(limites.DefletorMinimo, Math.Min(limites.DefletorMaximo, opcoes.DefletorM));
        if (vigas.Count == 0)
            return (teto - afastamento, $"defletor a {teto - afastamento:0.00} ({afastamento * 1000:0} mm abaixo do {Origem(ambiente)})", null, null);
        var fundo = vigas.Min(v => v.Fundo);
        var abaixo = Math.Max(EspacamentoDeBicos.DefletorAbaixoDaVigaMinimo, Math.Min(EspacamentoDeBicos.DefletorAbaixoDaVigaMaximo, opcoes.AbaixoDaVigaM));
        var defletor = fundo - abaixo;
        if (teto - defletor > EspacamentoDeBicos.DefletorMaximoComViga + 1e-6)
            return (teto - afastamento, $"defletor a {teto - afastamento:0.00} ({afastamento * 1000:0} mm abaixo do {Origem(ambiente)}), nos vãos entre " +
                                        $"as vigas de {(teto - fundo) * 100:0} cm (abaixo delas ficaria a {(teto - defletor) * 1000:0} mm, máximo 560 mm, 8.13.1.2)",
                null, vigas);
        return (defletor, $"defletor a {defletor:0.00} ({abaixo * 1000:0} mm abaixo da viga, {(teto - defletor) * 1000:0} mm do {Origem(ambiente)})", null, null);
    }

    /// <summary>As vigas do ambiente quando são fundas e os bicos vão para os vãos entre elas; nulo, se não.</summary>
    public static IReadOnlyList<Viga>? VigasDosVaos(Edificio edificio, Ambiente ambiente, LimitesDeBico limites, OpcoesBicos opcoes) =>
        Defletor(edificio, ambiente, limites, opcoes).Vaos;

    static string Origem(Ambiente a) => a.OrigemDoTeto switch
    {
        OrigemDoTeto.Forro => "forro",
        OrigemDoTeto.Telhado => "telhado",
        _ => "teto",
    };

    /// <summary>
    /// Vigas do teto que passam pelo ambiente: com o fundo abaixo do teto e o topo até 50 cm dele (penduradas na laje). A
    /// viga no meio da altura de um ambiente alto (a do mezanino) é outra obstrução, não a do teto.
    /// </summary>
    static IEnumerable<Viga> Vigas(Edificio edificio, Ambiente ambiente) =>
        edificio.Vigas.Where(v => ambiente.Teto is { } teto && v.Fundo < teto - 0.01 && v.Topo > teto - 0.50
                                  && Enumerable.Range(0, 21).Any(k => BicosNoAmbiente.Dentro(ambiente.Contornos,
                                      new Ponto2(v.A.X + (v.B.X - v.A.X) * k / 20, v.A.Y + (v.B.Y - v.A.Y) * k / 20))));

    static string? Criar(Document doc, FamilySymbol simbolo, IReadOnlyList<Ponto2> pontos, double piso, double defletor, bool marcar,
        List<ElementId> criados)
    {
        if (!simbolo.IsActive) simbolo.Activate();
        var nivel = Nivel(doc, Pes(piso));
        foreach (var p in pontos)
        {
            var bico = doc.Create.NewFamilyInstance(new XYZ(Pes(p.X), Pes(p.Y), nivel.ProjectElevation), simbolo, nivel, StructuralType.NonStructural);
            if (marcar) bico.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(DadosEsboco.Marca);
            criados.Add(bico.Id);
        }
        doc.Regenerate();
        // O defletor é a ponta do bico do lado oposto ao tubo: embaixo no pendente, em cima no bico em pé.
        foreach (var id in criados)
        {
            var bico = (FamilyInstance)doc.GetElement(id);
            var atual = Pes(AlturaDoDefletor(bico));
            var deslocamento = Pes(defletor) - atual;
            if (Math.Abs(deslocamento) < 1e-6) continue;
            var parametro = bico.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM) ?? bico.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
            if (parametro is { IsReadOnly: false }) parametro.Set(parametro.AsDouble() + deslocamento);
            else ElementTransformUtils.MoveElement(doc, id, new XYZ(0, 0, deslocamento));
        }
        doc.Regenerate();
        return null;
    }

    /// <summary>Cota do defletor em m: a ponta da caixa do bico do lado oposto ao conector.</summary>
    static double AlturaDoDefletor(FamilyInstance bico)
    {
        var caixa = bico.get_BoundingBox(null);
        if (caixa is null) return Metros((bico.Location as LocationPoint)?.Point.Z ?? 0);
        var conector = bico.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().FirstOrDefault(c => c.Domain == Domain.DomainPiping);
        var emPe = conector is not null && conector.CoordinateSystem.BasisZ.Z < -0.7;
        return Metros(emPe ? caixa.Max.Z : caixa.Min.Z);
    }

    static Ponto2? Ponto(FamilyInstance bico) =>
        bico.Location is LocationPoint p ? new Ponto2(Metros(p.Point.X), Metros(p.Point.Y)) : null;

    /// <summary>O nível do projeto de incêndio logo abaixo do piso do ambiente (ou o mais baixo).</summary>
    internal static Level Nivel(Document doc, double piso)
    {
        var niveis = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(n => n.ProjectElevation).ToList();
        return niveis.LastOrDefault(n => n.ProjectElevation <= piso + Pes(0.05)) ?? niveis.First();
    }

    public static IEnumerable<FamilySymbol> Simbolos(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .Where(s => s.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers).OrderBy(s => s.FamilyName).ThenBy(s => s.Name);

    public static string Nome(FamilySymbol s) => $"{s.FamilyName} : {s.Name}";

    /// <summary>
    /// O tipo pedido; sem ele, o bico confirmado em Famílias; sem isso, o primeiro que não é hospedado nem ESFR (o ESFR é de
    /// armazenamento, que Bicos por ambiente não faz).
    /// </summary>
    static FamilySymbol? Simbolo(Document doc, string? tipo)
    {
        var simbolos = Simbolos(doc).ToList();
        if (tipo is not null && simbolos.FirstOrDefault(s => Nome(s) == tipo) is { } pedido) return pedido;
        return simbolos.FirstOrDefault(s => EhConfirmado(doc, s))
               ?? simbolos.FirstOrDefault(s => !Hospedado(s) && !Nome(s).Contains("ESFR", StringComparison.OrdinalIgnoreCase))
               ?? simbolos.FirstOrDefault(s => !Hospedado(s))
               ?? simbolos.FirstOrDefault();
    }

    /// <summary>Família presa a uma face ou parede: criada pelo nível, ela fica no piso sem subir até o teto, e a rede não liga nela.</summary>
    static bool Hospedado(FamilySymbol s) => s.Family?.FamilyPlacementType is FamilyPlacementType.WorkPlaneBased or FamilyPlacementType.OneLevelBasedHosted;

    /// <summary>O tipo que a janela mostra de início: o mesmo que vale sem escolha.</summary>
    public static string? TipoPadrao(Document doc) => Simbolo(doc, null) is { } s ? Nome(s) : null;

    /// <summary>
    /// A família está confirmada em Famílias (com K e área): o Calcular lê os bicos dela. Senão os bicos novos levam a
    /// marca da malha gerada e entram com o K e a área de Dados do esboço.
    /// </summary>
    static bool EhConfirmado(Document doc, FamilySymbol s) =>
        MapeamentoGravado.Tentar(doc)?.Any(l => l.EhChuveiroComMedidas && l.Familia == s.FamilyName && l.Tipo == s.Name) == true;

    static Ponto2? Direcao(DirecaoDosRamais d) => d switch
    {
        DirecaoDosRamais.EixoX => new Ponto2(1, 0),
        DirecaoDosRamais.EixoY => new Ponto2(0, 1),
        _ => null,
    };

    static double Pes(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);
    static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);
}
