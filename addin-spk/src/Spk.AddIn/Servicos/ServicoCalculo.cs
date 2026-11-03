using System.IO;
using Spk.AddIn.Modelagem;
using Spk.Motor.Calculo;
using Spk.Motor.Relatorios;

namespace Spk.AddIn.Servicos;

/// <summary>
/// O cálculo sem janelas: monta a rede, calcula, grava o memorial e o resultado no modelo. O comando Calcular põe as
/// perguntas em volta; o teste automático chama direto.
/// </summary>
static class ServicoCalculo
{
    /// <summary>Abaixo disso o número só pode ser K americano (gpm/psi½); o menor K métrico usual é 57.</summary>
    const double MenorKMetrico = 40;

    const string Atencao = "ATENÇÃO";

    public sealed record Resultado(LeitorRede.RedeLida Lida, RelatorioSpk Relatorio, string? AvisoGravacao, string PastaMemorial)
    {
        public Spk.Motor.Calculo.ResultadoChuveiro Critico =>
            Relatorio.Resultado.Chuveiros.First(c => c.Id == Relatorio.Resultado.ChuveiroCritico);

        public IEnumerable<string> Falhas =>
            Relatorio.Verificacoes.Where(v => !v.Atende).Select(v => $"{v.Descricao} ({v.Item}): {v.Detalhe}");

        public IEnumerable<string> Avisos =>
            Relatorio.Avisos.Concat(Lida.Avisos.Where(a => !a.StartsWith(Atencao))).Append(AvisoGravacao).OfType<string>();

        public string Texto()
        {
            var falhas = Falhas.Select(f => "• " + f).ToList();
            var critico = Critico;
            return $"""
                {(Relatorio.Atende ? "ATENDE" : "NÃO ATENDE")}
                {(falhas.Count > 0 ? string.Join("\n", falhas) + "\n" : "")}
                Setor: {Lida.Setor}    Bicos: {Lida.Rede.Chuveiros.Count}    Trechos: {Lida.Rede.Trechos.Count}    Áreas avaliadas: {Relatorio.Busca?.CandidatosAvaliados ?? 1}
                Pressão na fonte: {Relatorio.Resultado.PressaoNecessaria:0.0} kPa ({Relatorio.Resultado.PressaoNecessaria / 100:0.00} bar)
                Vazão dos bicos: {Relatorio.Resultado.VazaoChuveiros:0} L/min
                Bico crítico: {critico.Id} a {critico.Pressao / 100:0.00} bar ({critico.Vazao:0} L/min)

                {string.Join("\n", Avisos)}

                Memorial em:
                {PastaMemorial}
                """;
        }
    }

    public static void ValidarFatorK(IReadOnlyList<LeitorRede.BicoLido> bicos)
    {
        var kAmericano = bicos.Where(b => b.FatorK < MenorKMetrico).Select(b => b.FatorK).Distinct().ToList();
        if (kAmericano.Count > 0)
            throw new InvalidOperationException(
                $"Fator K {string.Join(", ", kAmericano.Select(k => k.ToString("0.##")))} parece estar em gpm/psi½. " +
                "O cálculo usa L/min/bar½ (K 5,6 → 80; K 25,2 → 360). Corrija em Famílias ou em Dados do esboço.");
    }

    /// <param name="fonte">A VGA do setor; nula sem VGA no modelo.</param>
    /// <param name="doSetor">Sem VGA: um bico ou tubo do setor. Nulo: o setor com mais bicos.</param>
    public static Resultado Executar(Document doc, DadosProjeto dados, BicosDoModelo.Leitura leitura, FamilyInstance? fonte,
        IReadOnlyCollection<ElementId> vgas, ElementId? doSetor = null, int? indiceSetor = null)
    {
        ValidarFatorK(leitura.Bicos);
        var lida = LeitorRede.Montar(doc, leitura.Bicos, dados.FatorC, fonte, vgas, doSetor, indiceSetor);
        // A Tabela 15 é de bico spray de cobertura-padrão: ESFR e armazenamento têm tabela própria (Tabela 22), ainda sem conferência.
        var edificio = dados.Classe == Spk.Motor.Normas.ClasseRisco.Armazenamento ? null : Arquitetura.LeituraDoEdificio.Ler(doc);
        var ambientes = edificio?.Ambientes ?? [];
        var projeto = dados.CriarProjeto(leitura.Gerados ? "Malha gerada" : "Rede lida do modelo", lida.Rede) with
        {
            AlturaDoTeto = AlturasDoTeto(doc, lida, ambientes),
            Obstrucoes = edificio is null || dados.Base == TipoBase.Esfr ? null : ObstrucoesDoSetor(doc, dados, lida, edificio),
        };
        var relatorio = CalculoSpk.Executar(projeto);
        // O que tira o valor do resultado vem primeiro, e vai também para o memorial.
        var atencao = lida.Avisos.Where(a => a.StartsWith(Atencao));
        if (leitura.AvisoDoFatorK is { } avisoDoK) atencao = atencao.Append(avisoDoK);
        atencao = atencao.Concat(AvisosDaInclinacao(doc, dados, lida, ambientes));
        var (fatorCAtencao, fatorCInforma) = AvisosDoFatorC(doc, dados, lida);
        atencao = atencao.Concat(fatorCAtencao);
        relatorio = relatorio with { Avisos = atencao.Concat(relatorio.Avisos).Concat(fatorCInforma).ToList() };

        var pasta = PastasSpk.Calculo;
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "memorial.md"), Memorial.Gerar(relatorio));
        File.WriteAllBytes(Path.Combine(pasta, "memorial.docx"), MemorialDocx.Gerar(relatorio));

        var aviso = GravarNoModelo(doc, lida, relatorio, dados);
        return new Resultado(lida, relatorio, aviso, pasta);
    }

    /// <summary>
    /// A altura do teto sobre os bicos que podem estar sob teto alto: a do ambiente da arquitetura em que o bico está; sem
    /// ambiente, a do próprio bico acima do nível, que é o mínimo que o teto tem. Abaixo de 5 m o bico não fica sob um teto de
    /// 6,1 m (o defletor fica a no máximo 56 cm do teto).
    /// </summary>
    static IReadOnlyDictionary<string, double>? AlturasDoTeto(Document doc, LeitorRede.RedeLida lida, IReadOnlyList<Spk.Motor.Modelo.Ambiente> ambientes)
    {
        var alturas = new Dictionary<string, double>();
        foreach (var (id, elemento) in lida.Bicos)
        {
            if (doc.GetElement(elemento) is not FamilyInstance { Location: LocationPoint { Point: { } ponto } } bico
                || doc.GetElement(bico.LevelId) is not Level nivel) continue;
            var acima = Metros(ponto.Z - nivel.ProjectElevation);
            if (acima >= 5) alturas[id] = AmbienteDoBico(ambientes, ponto)?.PeDireito ?? acima;
        }
        return alturas.Count == 0 ? null : alturas;
    }

    internal static IReadOnlyList<Spk.Motor.Normas.ObstrucaoAcusada> ObstrucoesDoSetor(Document doc, DadosProjeto dados, LeitorRede.RedeLida lida, Spk.Motor.Modelo.Edificio edificio)
    {
        var bicos = lida.Bicos.Select(b => (b.Key, Elemento: doc.GetElement(b.Value) as FamilyInstance))
            .Where(b => b.Elemento?.Location is LocationPoint)
            .Select(b =>
            {
                var p = ((LocationPoint)b.Elemento!.Location).Point;
                return new Spk.Motor.Normas.BicoNoEspaco(b.Key, new Spk.Motor.Modelo.Ponto2(Metros(p.X), Metros(p.Y)), BicosPorAmbiente.AlturaDoDefletor(b.Elemento));
            }).ToList();
        return Spk.Motor.Normas.ObstrucaoDeBico.Conferir(bicos, edificio.Vigas, BicosPorAmbiente.Limites(dados, new OpcoesBicos()).DistanciaMaxima);
    }

    static Spk.Motor.Modelo.Ambiente? AmbienteDoBico(IReadOnlyList<Spk.Motor.Modelo.Ambiente> ambientes, XYZ ponto)
    {
        var (z, emPlanta) = (Metros(ponto.Z), new Spk.Motor.Modelo.Ponto2(Metros(ponto.X), Metros(ponto.Y)));
        return ambientes.FirstOrDefault(a => a.PeDireito is not null && a.Piso <= z && a.Teto + 0.1 >= z
                                             && Spk.Motor.Geracao.BicosNoAmbiente.Dentro(a.Contornos, emPlanta));
    }

    /// <summary>
    /// A inclinação informada em Dados do projeto contra a medida no modelo e contra a base de cálculo. Só avisa: a medida é de um
    /// ponto do ambiente, e quem conhece o telhado é o engenheiro.
    /// </summary>
    static IEnumerable<string> AvisosDaInclinacao(Document doc, DadosProjeto dados, LeitorRede.RedeLida lida, IReadOnlyList<Spk.Motor.Modelo.Ambiente> ambientes)
    {
        var limite = Spk.Motor.Normas.Nbr10897.InclinacaoTetoLimite;
        var informada = dados.InclinacaoTetoPct / 100;
        var medidas = lida.Bicos.Values.Select(doc.GetElement).OfType<FamilyInstance>()
            .Select(b => b.Location is LocationPoint { Point: { } p } ? AmbienteDoBico(ambientes, p) : null)
            .OfType<Spk.Motor.Modelo.Ambiente>().Where(a => a.Inclinacao > limite).ToList();
        if (medidas.Count > 0 && informada <= limite)
        {
            var maior = medidas.OrderByDescending(a => a.Inclinacao).First();
            yield return $"ATENÇÃO: o teto sobre {medidas.Count} bico(s) tem cerca de {maior.Inclinacao * 100:0} % de inclinação (ambiente {maior.Numero} {maior.Nome}), e Dados do projeto está com " +
                         $"{dados.InclinacaoTetoPct:0.#} %. Acima de 16,7 %, a área de operação sobe 30 % (NBR 8.5.5.4): confirme no modelo e informe em Dados do projeto.";
        }
        if (informada <= limite) yield break;
        if (dados.Base == TipoBase.DensidadeArea)
            yield return "ATENÇÃO: teto com inclinação acima de 16,7 %: a área digitada em Dados do projeto precisa já incluir os 30 % da NBR 8.5.5.4; o SPK não soma sobre ela.";
        if (dados.Base == TipoBase.Esfr)
            yield return "ATENÇÃO: os critérios da IT-24 valem só para teto com inclinação de até 16,7 % (IT-24/2019 5.1.7).";
    }

    /// <summary>
    /// O C de Dados do projeto contra a Tabela 30, pelo tipo e pelo segmento de cada tubo do setor. C acima da tabela dá pressão
    /// menor que a real, e é ATENÇÃO; C abaixo é a favor da segurança, e só informa.
    /// </summary>
    static (List<string> Atencao, List<string> Informa) AvisosDoFatorC(Document doc, DadosProjeto dados, LeitorRede.RedeLida lida)
    {
        var (atencao, informa) = (new List<string>(), new List<string>());
        var tubos = lida.Tubos.Values.Distinct().Select(doc.GetElement).OfType<Autodesk.Revit.DB.Plumbing.Pipe>();
        foreach (var grupo in tubos.GroupBy(t => (Nome: GeradorMalha.NomeDoTipo(doc, t.PipeType), Seco: EmSistemaSeco(doc, t))))
        {
            if (Spk.Motor.Normas.Nbr10897.TuboPeloNome(grupo.Key.Nome, grupo.Key.Seco) is not { } linha) continue;
            var tabela = Spk.Motor.Normas.Nbr10897.FatorC(linha);
            if (dados.FatorC > tabela + 1e-6)
                atencao.Add($"ATENÇÃO: {grupo.Count()} tubo(s) {grupo.Key.Nome}{(grupo.Key.Seco ? " em sistema seco" : "")}: a Tabela 30 dá C {tabela:0}, e o cálculo usou C {dados.FatorC:0}. A pressão calculada fica menor que a real: ajuste em Dados do projeto.");
            else if (dados.FatorC < tabela - 1e-6)
                informa.Add($"{grupo.Count()} tubo(s) {grupo.Key.Nome}: a Tabela 30 dá C {tabela:0}, e o cálculo usou C {dados.FatorC:0}, a favor da segurança.");
        }
        return (atencao, informa);
    }

    static bool EmSistemaSeco(Document doc, Autodesk.Revit.DB.Plumbing.Pipe tubo) =>
        doc.GetElement(tubo.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId() ?? ElementId.InvalidElementId)
            is Autodesk.Revit.DB.Plumbing.PipingSystemType { SystemClassification: MEPSystemClassification.FireProtectDry or MEPSystemClassification.FireProtectPreaction };

    static double Metros(double pes) => UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Meters);

    /// <summary>
    /// Preenche os parâmetros SPK de bicos e tubos do setor e grava o resultado. Bicos fora da área ficam com pressão e
    /// vazão zeradas, para nenhuma etiqueta mostrar valor de um cálculo anterior.
    /// </summary>
    static string? GravarNoModelo(Document doc, LeitorRede.RedeLida lida, RelatorioSpk relatorio, DadosProjeto dados)
    {
        using var transacao = new Transaction(doc, "SPK — Resultado do cálculo");
        EngolirAvisos.Aplicar(transacao);
        transacao.Start();
        var aviso = ParametrosSpk.Garantir(doc);

        var daArea = relatorio.Resultado.Chuveiros.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var bicos = new List<BicoGravado>();
        foreach (var chuveiro in lida.Rede.Chuveiros)
        {
            var elemento = doc.GetElement(lida.Bicos[chuveiro.Id]);
            daArea.TryGetValue(chuveiro.Id, out var calculado);
            if (elemento is not null)
            {
                ParametrosSpk.Gravar(elemento, ParametrosSpk.Numero, chuveiro.Id);
                ParametrosSpk.Gravar(elemento, ParametrosSpk.Setor, lida.Setor);
                ParametrosSpk.Gravar(elemento, ParametrosSpk.AreaCalculo, calculado is not null);
                ParametrosSpk.Gravar(elemento, ParametrosSpk.Pressao, calculado?.Pressao ?? 0);
                ParametrosSpk.Gravar(elemento, ParametrosSpk.Vazao, calculado?.Vazao ?? 0);
            }
            bicos.Add(new BicoGravado
            {
                Id = chuveiro.Id,
                Elemento = lida.Bicos[chuveiro.Id].Value,
                Ramal = chuveiro.Ramal,
                NaArea = calculado is not null,
                PressaoKPa = calculado?.Pressao ?? 0,
                VazaoLmin = calculado?.Vazao ?? 0,
            });
        }

        var porTubo = relatorio.Resultado.Trechos
            .Where(t => lida.Tubos.ContainsKey(t.Id))
            .GroupBy(t => lida.Tubos[t.Id])
            .ToDictionary(g => g.Key, g => (Vazao: g.Max(t => Math.Abs(t.Vazao)), Velocidade: g.Max(t => Math.Abs(t.Velocidade))));
        foreach (var id in lida.Tubos.Values.Distinct())
        {
            if (doc.GetElement(id) is not { } tubo) continue;
            porTubo.TryGetValue(id, out var valores);
            ParametrosSpk.Gravar(tubo, ParametrosSpk.Setor, lida.Setor);
            ParametrosSpk.Gravar(tubo, ParametrosSpk.Vazao, valores.Vazao);
            ParametrosSpk.Gravar(tubo, ParametrosSpk.Velocidade, valores.Velocidade);
        }

        new ResultadoGravado
        {
            Setor = lida.Setor,
            Classificacao = dados.Resumo(),
            Atende = relatorio.Atende,
            PressaoNecessariaKPa = relatorio.Resultado.PressaoNecessaria,
            VazaoChuveirosLmin = relatorio.Resultado.VazaoChuveiros,
            VazaoTotalLmin = relatorio.VazaoTotal,
            VazaoHidrantesLmin = relatorio.VazaoHidrantes,
            DuracaoMin = relatorio.DuracaoMinutos,
            ReservaM3 = relatorio.ReservaM3,
            ChuveiroCritico = relatorio.Resultado.ChuveiroCritico,
            Chuveiros = bicos,
            Tubos = lida.Tubos.Values.Distinct().Select(i => i.Value).ToList(),
            Assinatura = AssinaturaDaRede.Calcular(doc, lida.Bicos.Values.Concat(lida.Tubos.Values)),
        }.Salvar(doc);

        if (transacao.Commit() != TransactionStatus.Committed)
            return "O Revit desfez a gravação dos resultados no modelo. O memorial foi gerado, mas as folhas não vão mostrar este cálculo.";
        return aviso;
    }
}
