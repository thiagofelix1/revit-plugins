using System.IO;
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

    public sealed record Resultado(LeitorRede.RedeLida Lida, RelatorioSpk Relatorio, string? AvisoGravacao, string PastaMemorial)
    {
        public Spk.Motor.Calculo.ResultadoChuveiro Critico =>
            Relatorio.Resultado.Chuveiros.First(c => c.Id == Relatorio.Resultado.ChuveiroCritico);

        public IEnumerable<string> Falhas =>
            Relatorio.Verificacoes.Where(v => !v.Atende).Select(v => $"{v.Descricao} ({v.Item}): {v.Detalhe}");

        public IEnumerable<string> Avisos =>
            Relatorio.Avisos.Concat(Lida.Avisos).Append(AvisoGravacao).OfType<string>();

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
        var projeto = dados.CriarProjeto(leitura.Gerados ? "Malha gerada" : "Rede lida do modelo", lida.Rede) with { AlturaDoTeto = AlturasDoTeto(doc, dados, lida) };
        var relatorio = CalculoSpk.Executar(projeto);
        // O K muda a pressão inteira: o aviso vem primeiro, e vai também para o memorial.
        if (leitura.AvisoDoFatorK is { } avisoDoK) relatorio = relatorio with { Avisos = relatorio.Avisos.Prepend(avisoDoK).ToList() };

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
    /// 6,1 m (o defletor fica a no máximo 56 cm do teto) e a arquitetura nem é lida.
    /// </summary>
    static IReadOnlyDictionary<string, double>? AlturasDoTeto(Document doc, DadosProjeto dados, LeitorRede.RedeLida lida)
    {
        if (dados.Classe == Spk.Motor.Normas.ClasseRisco.Armazenamento) return null;
        var candidatos = new List<(string Id, double X, double Y, double Z, double AcimaDoNivel)>();
        foreach (var (id, elemento) in lida.Bicos)
        {
            if (doc.GetElement(elemento) is not FamilyInstance { Location: LocationPoint { Point: { } ponto } } bico
                || doc.GetElement(bico.LevelId) is not Level nivel) continue;
            var acima = Metros(ponto.Z - nivel.ProjectElevation);
            if (acima >= 5) candidatos.Add((id, Metros(ponto.X), Metros(ponto.Y), Metros(ponto.Z), acima));
        }
        if (candidatos.Count == 0) return null;

        var ambientes = Arquitetura.LeituraDoEdificio.Ler(doc).Ambientes;
        var alturas = new Dictionary<string, double>();
        foreach (var bico in candidatos)
        {
            var ponto = new Spk.Motor.Modelo.Ponto2(bico.X, bico.Y);
            var ambiente = ambientes.FirstOrDefault(a => a.PeDireito is not null && a.Piso <= bico.Z && a.Teto + 0.1 >= bico.Z
                                                         && Spk.Motor.Geracao.BicosNoAmbiente.Dentro(a.Contornos, ponto));
            alturas[bico.Id] = ambiente?.PeDireito ?? bico.AcimaDoNivel;
        }
        return alturas;
    }

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
        }.Salvar(doc);

        if (transacao.Commit() != TransactionStatus.Committed)
            return "O Revit desfez a gravação dos resultados no modelo. O memorial foi gerado, mas as folhas não vão mostrar este cálculo.";
        return aviso;
    }
}
