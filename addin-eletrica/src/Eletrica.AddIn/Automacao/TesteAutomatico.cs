using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Circuitos;
using Eletrica.AddIn.Commands;
using Eletrica.AddIn.Condutos;
using Eletrica.AddIn.Fiacao;
using Eletrica.AddIn.Folhas;
using Eletrica.AddIn.Lancamento;
using Eletrica.AddIn.Leitura;
using Eletrica.AddIn.Padrao;
using Eletrica.AddIn.Servicos;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Lancamento;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Automacao;

/// <summary>
///     Teste sem cliques, como o do SPK. Se existir <c>Documentos\AutoEletrica\testes\fila.json</c> quando o Revit abre,
///     o plugin roda cada cenário (abre uma cópia do projeto ou cria um do template, monta a cena, roda os passos pelo mesmo
///     código dos botões, sem janelas), responde sozinho os diálogos do Revit, grava <c>relatorio.json</c> com etapas,
///     tempos, métricas e divergências contra o esperado, e fecha o Revit. Sem o arquivo, nada disto é ligado.
/// </summary>
static class TesteAutomatico
{
    public static string Pasta => Path.Combine(PastasEletrica.Raiz, "testes");
    static string ArquivoFila => Path.Combine(Pasta, "fila.json");
    static string ArquivoRelatorio => Path.Combine(Pasta, "relatorio.json");
    static string ArquivoConcluido => Path.Combine(Pasta, "concluido.txt");
    public static string PastaSaida => Path.Combine(Pasta, "saida");

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    static UIControlledApplication? aplicacao;
    static readonly List<string> dialogos = [];

    public static void Registrar(UIControlledApplication app)
    {
        if (!File.Exists(ArquivoFila)) return;
        aplicacao = app;
        app.Idling += AoFicarOcioso;
        app.DialogBoxShowing += AoMostrarDialogo;
    }

    /// <summary>Responde os diálogos do Revit: referência não encontrada é ignorada; o resto, OK ou fechar. Cada um fica anotado.</summary>
    static void AoMostrarDialogo(object? sender, DialogBoxShowingEventArgs e)
    {
        switch (e)
        {
            case TaskDialogShowingEventArgs t when t.DialogId == "TaskDialog_Unresolved_References":
                t.OverrideResult(1002);
                dialogos.Add($"{t.DialogId}: ignorar e continuar");
                break;
            case TaskDialogShowingEventArgs t:
                t.OverrideResult((int)TaskDialogResult.Close);
                dialogos.Add($"{t.DialogId}: {t.Message}");
                break;
            case MessageBoxShowingEventArgs m:
                m.OverrideResult(1);
                dialogos.Add($"{m.DialogId}: {m.Message}");
                break;
            default:
                e.OverrideResult(1);
                dialogos.Add(e.DialogId);
                break;
        }
    }

    static void AoFicarOcioso(object? sender, IdlingEventArgs e)
    {
        aplicacao!.Idling -= AoFicarOcioso;
        if (sender is not UIApplication ui) return;

        FilaDeTestes fila;
        try
        {
            fila = JsonSerializer.Deserialize<FilaDeTestes>(File.ReadAllText(ArquivoFila), Json) ?? new FilaDeTestes();
        }
        catch (Exception ex)
        {
            fila = new FilaDeTestes();
            GravarRelatorio([new RelatorioCenario { Nome = "fila.json", Erro = "Não li a fila: " + ex.Message }]);
        }
        File.Copy(ArquivoFila, Path.Combine(Pasta, "fila-executada.json"), true);
        File.Delete(ArquivoFila);
        Directory.CreateDirectory(PastaSaida);
        if (File.Exists(ArquivoConcluido)) File.Delete(ArquivoConcluido);

        var relatorios = new List<RelatorioCenario>();
        foreach (var cenario in fila.Cenarios)
        {
            relatorios.Add(Rodar(ui.Application, cenario));
            GravarRelatorio(relatorios);
        }
        File.WriteAllText(ArquivoConcluido,
            $"{DateTime.Now:O}\n{relatorios.Count(r => r.Passou != false && r.Ok)} de {relatorios.Count} cenário(s) sem falha\n" +
            string.Join("\n", relatorios.Select(r => $"{(r.Ok && r.Passou != false ? "ok   " : "FALHA")} {r.Nome}")));

        aplicacao.DialogBoxShowing -= AoMostrarDialogo;
        if (fila.FecharRevit)
            ui.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit));
    }

    static void GravarRelatorio(List<RelatorioCenario> relatorios)
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(ArquivoRelatorio, JsonSerializer.Serialize(relatorios, Json));
    }

    // ----- um cenário -----

    static RelatorioCenario Rodar(Autodesk.Revit.ApplicationServices.Application app, Cenario cenario)
    {
        var r = new RelatorioCenario { Nome = cenario.Nome };
        dialogos.Clear();
        Document? doc = null;
        var arquivo = Path.Combine(PastaSaida, Seguro(cenario.Nome) + ".rvt");
        var ocorrencias = new Dictionary<string, int>();
        try
        {
            if (!Etapa(r, "abrir", () =>
                {
                    foreach (var origem in cenario.CopiarJunto.SelectMany(c => Expandir(app, c)))
                    {
                        var destino = Path.Combine(PastaSaida, Path.GetFileName(origem));
                        if (!File.Exists(destino) || new FileInfo(destino).Length != new FileInfo(origem).Length) Copiar(origem, destino);
                    }
                    if (cenario.Novo is { } template)
                    {
                        doc = app.NewProjectDocument(CenasEletrica.Template(app, template));
                        doc.SaveAs(arquivo, new SaveAsOptions { OverwriteExistingFile = true });
                        return $"Projeto novo pelo template {template}";
                    }
                    var fonte = Substituir(app, cenario.Abrir ?? throw new InvalidOperationException("O cenário precisa de \"abrir\" ou \"novo\"."));
                    if (!File.Exists(fonte)) throw new FileNotFoundException($"Projeto não encontrado: {fonte}");
                    Copiar(fonte, arquivo);
                    doc = app.OpenDocumentFile(arquivo);
                    return $"Cópia de {fonte}";
                }, fatal: true)) return r;

            if (cenario.Dados is { } dados)
                Etapa(r, "dados", () =>
                {
                    ArmazenamentoEletrica.GravarComTransacao(doc!, DadosProjeto.Chave, dados, "AutoEletrica — Dados do projeto (teste)");
                    return dados.Resumo();
                });

            if (cenario.Cena is { } nomeCena)
                if (!Etapa(r, "cena", () => CenasEletrica.Montar(doc!, nomeCena, DadosProjeto.Carregar(doc!) ?? new DadosProjeto(), r.Metricas), fatal: true))
                    return r;

            if (cenario.Lancamento is not null || cenario.Passagem is not null)
                Etapa(r, "padrao", () =>
                {
                    var padrao = PadraoEletrica.Carregar(doc!);
                    if (cenario.Lancamento is { } l) padrao.Lancamento = l;
                    if (cenario.Passagem is { } p) padrao.Passagem = p;
                    padrao.SalvarComTransacao(doc!);
                    return padrao.Resumo();
                });

            if (cenario.UsosSugeridos || cenario.Usos is { Count: > 0 })
                Etapa(r, "usos", () => ConfirmarUsos(doc!, cenario.Usos));

            foreach (var passo in cenario.Passos)
            {
                ocorrencias[passo] = ocorrencias.TryGetValue(passo, out var n) ? n + 1 : 1;
                var rotulo = ocorrencias[passo] == 1 ? passo : $"{passo}#{ocorrencias[passo]}";
                Etapa(r, rotulo, () => Passo(doc!, passo, rotulo, r));
            }

            if (cenario.Salvar)
                Etapa(r, "salvar", () =>
                {
                    doc!.Save();
                    return arquivo;
                });
            r.Arquivo = arquivo;
            r.Ok = r.Etapas.All(e => e.Erro is null);
        }
        catch (Exception ex)
        {
            r.Erro ??= ex.ToString();
        }
        finally
        {
            try { doc?.Close(false); }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException) { }
            r.Dialogos = dialogos.ToList();
            r.Metricas["etapasComErro"] = r.Etapas.Count(e => e.Erro is not null);
            r.Metricas["dialogos"] = dialogos.Count(d => !d.StartsWith("TaskDialog_Unresolved_References"));
            if (cenario.Esperado is { Count: > 0 } esperado)
            {
                r.Divergencias = Conferir(esperado, r);
                r.Passou = r.Divergencias.Count == 0;
            }
        }
        return r;
    }

    /// <summary>Roda a etapa, mede o tempo e anota o resultado. Erro numa etapa não fatal fica no relatório e o cenário segue.</summary>
    static bool Etapa(RelatorioCenario r, string nome, Func<string> acao, bool fatal = false)
    {
        var relogio = Stopwatch.StartNew();
        var etapa = new EtapaExecutada { Nome = nome };
        r.Etapas.Add(etapa);
        try
        {
            etapa.Texto = acao();
            return true;
        }
        catch (Exception ex)
        {
            etapa.Erro = $"{ex.GetType().Name}: {ex.Message}";
            etapa.Pilha = ex.StackTrace?.Split('\n').Take(8).Select(l => l.Trim()).ToList();
            if (fatal) r.Erro = $"{nome}: {ex}";
            return false;
        }
        finally
        {
            etapa.Segundos = Math.Round(relogio.Elapsed.TotalSeconds, 1);
        }
    }

    static string ConfirmarUsos(Document doc, Dictionary<string, UsoAmbiente>? usos)
    {
        var lidos = LeituraDeAmbientes.Ler(doc);
        var confirmados = UsosConfirmados.Carregar(doc);
        foreach (var a in lidos)
        {
            var uso = a.Uso;
            if (usos is not null)
                foreach (var (trecho, u) in usos)
                    if (a.Nome.IndexOf(trecho, StringComparison.OrdinalIgnoreCase) >= 0) { uso = u; break; }
            confirmados.Usos[a.Id] = uso;
        }
        ArmazenamentoEletrica.GravarComTransacao(doc, UsosConfirmados.Chave, confirmados, "AutoEletrica — Ambientes (teste)");
        return $"{lidos.Count} ambiente(s): " + string.Join("; ", lidos.Select(a => $"{a.Nome} = {ClassificadorDeAmbientes.Nome(confirmados.Usos[a.Id])}"));
    }

    // ----- os passos, pelo mesmo código dos botões -----

    static string Passo(Document doc, string passo, string rotulo, RelatorioCenario r)
    {
        var dados = DadosProjeto.Carregar(doc) ?? throw new InvalidOperationException("Mande \"dados\" no cenário (ou use uma cena que grave os dados).");
        var padrao = PadraoEletrica.Carregar(doc);
        void M(string nome, double valor) => r.Metricas[$"{rotulo}.{nome}"] = valor;

        switch (passo)
        {
            case "ambientes":
            {
                var lidos = LeituraDeAmbientes.Ler(doc);
                M("ambientes", lidos.Count);
                r.Ambientes = lidos.Select(a => $"{a.Nome} ({a.Origem}): {ClassificadorDeAmbientes.Nome(a.Uso)}{(a.Confirmado ? "" : " (sugerido)")}, {a.Area:0.0} m², {a.Perimetro:0.0} m").ToList();
                return $"{lidos.Count} ambiente(s)";
            }
            case "parametros":
            {
                using var t = new Transaction(doc, "AutoEletrica — Parâmetros (teste)");
                t.Start();
                var aviso = ParametrosEletrica.Garantir(doc);
                t.Commit();
                return aviso ?? "parâmetros AE garantidos";
            }
            case "pontosPorAmbiente":
            {
                var lidos = LeituraDeAmbientes.Ler(doc);
                var porId = lidos.ToDictionary(a => a.Id, a => a);
                var propostas = PropostaDePontos.Propor(lidos.Select(a => new Ambiente(a.Id, a.Nome, a.Uso, a.Area, a.Perimetro)), padrao.Lancamento, dados.Habitacao);
                var linhas = propostas.Select(p =>
                {
                    var tomadas = p.Pontos.Where(x => x.Tipo == TipoCarga.TomadaUsoGeral).ToList();
                    return new PontosPorAmbienteJanela.Linha
                    {
                        Ambiente = porId[p.Ambiente.Id],
                        Nome = porId[p.Ambiente.Id].Nome,
                        Tomadas = tomadas.Sum(x => x.Quantidade),
                        Tomadas600 = tomadas.Where(x => x.PotenciaVA >= 600).Sum(x => x.Quantidade),
                        TomadasMedia = tomadas.Where(x => x.Posicao == PosicaoDoPonto.Media).Sum(x => x.Quantidade),
                        LuzVA = p.CargaDeIluminacao,
                        Chuveiro = p.Pontos.Any(x => x.Tipo == TipoCarga.Chuveiro),
                        Ar = p.Pontos.Any(x => x.Tipo == TipoCarga.ArCondicionado),
                        Lancar = true,
                    };
                }).ToList();
                var criados = new List<ElementId>();
                var resultados = PontosPorAmbiente.Lancar(doc, linhas, padrao, criados);
                M("pontos", criados.Count);
                M("ambientes", resultados.Count(x => x.Recusa is null));
                M("recusados", resultados.Count(x => x.Recusa is not null));
                M("avisos", resultados.Sum(x => x.Avisos.Count));
                r.Pontos = resultados.Select(x => x.Texto().Replace("\n", " ")).ToList();
                return $"{criados.Count} ponto(s) em {resultados.Count(x => x.Recusa is null)} de {resultados.Count} ambiente(s)";
            }
            case "criarCircuitos":
            {
                var quadro = Quadros.Todos(doc).FirstOrDefault() ?? throw new InvalidOperationException("Nenhum quadro no modelo.");
                var proposta = CriarCircuitos.Propor(doc, dados, padrao, quadro);
                var feito = CriarCircuitos.Criar(doc, quadro, proposta.Circuitos, proposta.Elementos);
                M("propostos", proposta.Circuitos.Count);
                M("criados", feito.Criados);
                M("avisos", feito.Avisos.Count + proposta.Avisos.Count);
                r.Circuitos = proposta.Circuitos.Select(c => $"{c.Numero} {c.Descricao}: {c.Tipo}, {c.Ligacao}, {c.Pontos.Count} ponto(s), {c.PotenciaVA:0} VA, {c.CorrenteDeProjeto:0.0} A — {c.Motivo}")
                    .Concat(proposta.Avisos.Concat(feito.Avisos).Select(a => "aviso: " + a)).ToList();
                return $"{feito.Criados} de {proposta.Circuitos.Count} circuito(s) criados no {Quadros.Nome(quadro)}";
            }
            case "tracarEstrela":
            {
                var (texto, eletrodutos, recusas) = CenasEletrica.TracarEstrela(doc, padrao);
                M("eletrodutos", eletrodutos);
                M("recusas", recusas.Count);
                r.Tracado = recusas;
                return texto;
            }
            case "passarCircuitos":
            {
                var feito = PassarCircuitos.Executar(doc, LeitorInstalacao.Ler(doc, dados));
                M("circuitos", feito.Circuitos);
                M("eletrodutos", feito.Eletrodutos);
                M("avisos", feito.Avisos.Count);
                r.Passagem = feito.Avisos.Take(40).ToList();
                return $"{feito.Circuitos} circuito(s) por {feito.Eletrodutos} eletroduto(s), {feito.Avisos.Count} aviso(s)";
            }
            case "caixasPorRegra":
            {
                var feito = CaixasPorRegra.Executar(doc, new GrafoDeCondutos(doc), padrao);
                M("necessarias", feito.Necessarias.Count);
                M("criadas", feito.Criadas.Count);
                return $"{feito.Necessarias.Count} caixa(s) necessária(s), {feito.Criadas.Count} inserida(s)" + (feito.Avisos.Count > 0 ? "; " + string.Join("; ", feito.Avisos.Take(5)) : "");
            }
            case "verificar":
            {
                var lida = LeitorInstalacao.Ler(doc, dados);
                var (problemas, _) = Contexto.VerificarTudo(doc, dados, lida, null);
                M("erros", problemas.Count(p => p.Gravidade == Gravidade.Erro));
                M("avisos", problemas.Count(p => p.Gravidade == Gravidade.Aviso));
                M("notas", problemas.Count(p => p.Gravidade == Gravidade.Nota));
                foreach (var g in problemas.GroupBy(p => p.Codigo)) M(g.Key, g.Count());
                r.Problemas = problemas.OrderBy(p => p.Gravidade).ThenBy(p => p.Codigo).Take(120).Select(p => $"{p.Codigo} {p.Gravidade}: {p.Mensagem}").ToList();
                var i = lida.Instalacao;
                M("pontos", i.Pontos.Count);
                M("circuitos", i.Circuitos.Count);
                M("quadros", i.Quadros.Count);
                M("trechos", i.Trechos.Count);
                M("comandos", i.Comandos.Count);
                return $"{i.Ambientes.Count} ambientes, {i.Quadros.Count} quadro(s), {i.Circuitos.Count} circuitos, {i.Pontos.Count} pontos, {i.Trechos.Count} trechos: " +
                       $"{problemas.Count(p => p.Gravidade == Gravidade.Erro)} erro(s), {problemas.Count(p => p.Gravidade == Gravidade.Aviso)} aviso(s)";
            }
            case "calcular":
            {
                var saida = ServicoCalculo.Executar(doc, dados);
                var res = saida.Resultado;
                M("circuitos", res.Circuitos.Count);
                M("atende", res.Atende ? 1 : 0);
                M("reprovados", res.Circuitos.Count(c => !c.Atende));
                M("erros", res.Problemas.Count(p => p.Gravidade == Gravidade.Erro));
                M("quadros", res.Quadros.Count);
                r.Calculo = res.Circuitos.Select(c => $"{c.Numero} {c.Descricao}: IB {c.CorrenteDeProjeto:0.0} A, {(c.CondutoresPorFase > 1 ? $"{c.CondutoresPorFase}×" : "")}{c.SecaoFase} mm², {c.Disjuntor}, " +
                                                     $"queda {c.QuedaTotal:0.00} %, fase {c.Fases}{(c.Atende ? "" : " — NÃO ATENDE: " + string.Join("; ", c.Reprovados.Select(p => p.Criterio)))}")
                    .Concat(res.Quadros.Select(q => $"quadro {q.Nome}: {q.PotenciaInstaladaVA:0} VA instalada, {q.PotenciaDeDemandaVA:0} VA demanda, geral {q.DisjuntorGeral}, {q.GruposDR?.Count ?? 0} DR(s)")).ToList();
                return $"{res.Nome}: {(res.Atende ? "ATENDE" : "NÃO ATENDE")}, {res.Circuitos.Count} circuitos, memorial em {saida.Memorial}";
            }
            case "desenharFiacao":
            {
                var vista = CenasEletrica.PlantaDosQuadros(doc);
                var feito = DesenharFiacao.Executar(doc, vista, LeitorInstalacao.Ler(doc, dados), padrao);
                M("fios", feito.Fios);
                M("avisos", feito.Avisos.Count);
                return $"{feito.Fios} fio(s) na vista {vista.Name}" + (feito.Avisos.Count > 0 ? "; " + string.Join("; ", feito.Avisos.Take(5)) : "");
            }
            case "etiquetar":
            {
                var vista = CenasEletrica.PlantaDosQuadros(doc);
                var feito = Etiquetagem.Executar(doc, vista);
                M("etiquetas", feito.Criadas);
                return feito.Recusa ?? $"{feito.Criadas} etiqueta(s) na vista {vista.Name}";
            }
            case "legenda":
            {
                var lida = LeitorInstalacao.Ler(doc, dados);
                var vista = LegendaDeFiacao.Gerar(doc, CalculoEletrica.Executar(lida.Instalacao), lida.Instalacao, padrao);
                return vista.Name;
            }
            case "diagnosticoGrafo":
            {
                // Pedaços desconectados do traçado e, para cada ponto fora do pedaço do quadro, onde ele está e o nó mais perto.
                var g = new GrafoDeCondutos(doc);
                var componentes = g.Componentes();
                var quadro = Quadros.Todos(doc).FirstOrDefault();
                var noQuadro = quadro is null ? null : g.NoDe(quadro);
                var doQuadro = componentes.FirstOrDefault(c => noQuadro is not null && c.Contains(noQuadro)) ?? [];
                M("componentes", componentes.Count);
                string P(XYZ p) => $"({Unidades.Metros(p.X):0.00}; {Unidades.Metros(p.Y):0.00}; {Unidades.Metros(p.Z):0.00})";
                var linhas = new List<string>
                {
                    $"{g.Nos.Count} nó(s), {componentes.Count} pedaço(s): " + string.Join(", ", componentes.Take(12).Select(c => c.Count + (c == doQuadro ? " (quadro)" : ""))),
                    $"quadro em {((quadro?.Location as LocationPoint)?.Point is { } pq ? P(pq) : "?")}, nó {noQuadro}",
                };
                var pontos = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                    .Where(fi => fi.Category?.BuiltInCategory is BuiltInCategory.OST_ElectricalFixtures or BuiltInCategory.OST_LightingFixtures or BuiltInCategory.OST_LightingDevices)
                    .Where(fi => fi.Location is LocationPoint).ToList();
                var fora = 0;
                foreach (var fi in pontos)
                {
                    var p = ((LocationPoint)fi.Location).Point;
                    var no = g.NoDe(fi);
                    if (no is not null && doQuadro.Contains(no)) continue;
                    fora++;
                    var pm = new XYZ(Unidades.Metros(p.X), Unidades.Metros(p.Y), Unidades.Metros(p.Z)); // os nós do grafo ficam em metros
                    var perto = doQuadro.Select(n => (n, d: g.Nos[n].DistanceTo(pm))).OrderBy(x => x.d).FirstOrDefault();
                    var componente = no is null ? "-" : componentes.FindIndex(c => c.Contains(no)).ToString();
                    if (fora <= 25)
                        linhas.Add($"{fi.Category?.Name} {fi.Symbol.Family.Name} em {P(p)}: nó {no ?? "nenhum"} (pedaço {componente}, a {(no is null ? 0 : g.Nos[no].DistanceTo(pm)):0.00} m); nó do quadro mais perto {perto.n} a {perto.d:0.00} m");
                }
                M("pontosForaDoQuadro", fora);

                // Cadeias (trechos) e o caminho como o motor o vê: o quadro e os pontos precisam ser pontas de cadeia.
                var pontas = g.Cadeias.SelectMany(k => new[] { k.De, k.Para }).ToHashSet();
                linhas.Add($"{g.Cadeias.Count} cadeia(s); nó do quadro é ponta de cadeia: {(noQuadro is not null && pontas.Contains(noQuadro) ? "sim" : "não")}; " +
                           $"pontos cujo nó não é ponta de cadeia: {pontos.Count(fi => g.NoDe(fi) is { } n && !pontas.Contains(n))}");
                try
                {
                    var lidaG = LeitorInstalacao.Ler(doc, dados);
                    var inst = lidaG.Instalacao;
                    var grafoMotor = new Motor.Calculo.Grafo(inst);
                    linhas.Add($"motor: {inst.Quadros.Count} quadro(s) (nó {string.Join(", ", inst.Quadros.Select(q => q.NoDoQuadro))}), {inst.Trechos.Count} trecho(s), " +
                               $"trechos com circuitos {inst.Trechos.Count(t => t.Circuitos.Count > 0)}; exemplos: " +
                               string.Join(" | ", inst.Trechos.Take(4).Select(t => $"{t.Id} {t.De}→{t.Para} [{string.Join(",", t.Circuitos.Take(3))}]")));
                    foreach (var c in inst.Circuitos.Take(3))
                    {
                        var destinos = inst.PontosDo(c).Select(p => p.NoDoPonto).Distinct().ToList();
                        // Com todos os circuitos liberados em todos os trechos, como a passagem faz.
                        var aberta = new Instalacao(inst.Nome, inst.Alimentacao, inst.Ambientes, inst.Quadros, inst.Circuitos, inst.Pontos,
                            inst.Trechos.Select(t => t with { Circuitos = inst.Circuitos.Select(x => x.Id).ToList() }), comandos: inst.Comandos);
                        var achados = new Motor.Calculo.Grafo(aberta).CaminhosDe(c, destinos);
                        linhas.Add($"circuito {c.Numero}: destinos {string.Join(", ", destinos.Take(6))}; com caminho {achados.Count}");
                    }
                }
                catch (Exception e) when (e is Motor.Modelo.ErroDeModelo or InvalidOperationException) { linhas.Add("motor: " + e.Message); }
                return string.Join("\n", linhas);
            }
            case "assinatura":
            {
                // Grava o JSON que entra na assinatura do cálculo, para comparar duas leituras do mesmo modelo.
                var lidaA = LeitorInstalacao.Ler(doc, dados);
                var ia = lidaA.Instalacao;
                var opcoes = new JsonSerializerOptions(ArmazenamentoEletrica.Json) { WriteIndented = true };
                var json = JsonSerializer.Serialize(new { ia.Alimentacao, ia.Ambientes, ia.Quadros, ia.Circuitos, ia.Pontos, ia.Trechos, ia.Comandos, ia.Caixas }, opcoes);
                var n = Directory.GetFiles(PastaSaida, $"assinatura-{r.Nome}-*.json").Length + 1;
                var arquivo = Path.Combine(PastaSaida, $"assinatura-{r.Nome}-{n}.json");
                File.WriteAllText(arquivo, json);
                return $"{Servicos.ServicoCalculo.Assinatura(ia)} ({Path.GetFileName(arquivo)})";
            }
            case "sondarSistemas":
                return CenasEletrica.SondarSistemas(doc);
            case "diagnosticoQuadros":
            {
                // Os conectores das famílias de quadro do modelo (uma por família), para comparar com a família de teste.
                var linhas = Quadros.Todos(doc).GroupBy(q => q.Symbol.Family.Id).Take(3).Select(g =>
                {
                    var q = g.First();
                    var ds = doc.GetElement(q.get_Parameter(BuiltInParameter.RBS_FAMILY_CONTENT_DISTRIBUTION_SYSTEM)?.AsElementId() ?? ElementId.InvalidElementId)?.Name ?? "-";
                    return $"{Quadros.Nome(q)} (sistema {ds}): {CenasEletrica.ConectorDaFamilia(doc, q)}";
                }).ToList();
                return string.Join("\n", linhas);
            }
            case "quadros":
            {
                var lida = LeitorInstalacao.Ler(doc, dados);
                var feito = QuadrosNoRevit.Gerar(doc, lida, CalculoEletrica.Executar(lida.Instalacao));
                M("vistas", feito.Vistas.Count);
                M("avisos", feito.Avisos.Count);
                return $"{feito.Vistas.Count} vista(s): " + string.Join(", ", feito.Vistas.Select(v => v.Name)) + (feito.Avisos.Count > 0 ? "; " + string.Join("; ", feito.Avisos.Take(5)) : "");
            }
            case "lista":
            {
                var lida = LeitorInstalacao.Ler(doc, dados);
                var feito = ListaDeMaterialRevit.Gerar(doc, lida, CalculoEletrica.Executar(lida.Instalacao));
                M("tabelas", feito.Tabelas.Count);
                return $"{feito.Vista.Name}, {feito.Tabelas.Count} tabela(s), {feito.Csv}";
            }
            case "pranchas":
            {
                var lida = LeitorInstalacao.Ler(doc, dados);
                var feito = PranchasAE.Gerar(doc, lida, CalculoEletrica.Executar(lida.Instalacao), padrao);
                M("folhas", feito.Folhas.Count);
                M("vistasNasFolhas", feito.Folhas.Sum(f => f.GetAllPlacedViews().Count));
                M("avisos", feito.Avisos.Count);
                r.Folhas = feito.Folhas.Select(f => $"{f.SheetNumber} {f.Name}: {f.GetAllPlacedViews().Count} vista(s)").Concat(feito.Avisos.Take(20).Select(a => "aviso: " + a)).ToList();
                return $"{feito.Folhas.Count} folha(s): " + string.Join(", ", feito.Folhas.Select(f => f.SheetNumber));
            }
            default:
                throw new InvalidOperationException($"Passo desconhecido: \"{passo}\".");
        }
    }

    // ----- conferência -----

    static List<string> Conferir(Dictionary<string, string> esperado, RelatorioCenario r)
    {
        var falhas = new List<string>();
        foreach (var (metrica, condicao) in esperado)
        {
            if (!r.Metricas.TryGetValue(metrica, out var valor))
            {
                // Contagem de código do Verificar que não apareceu vale zero.
                if (metrica.Contains('.') && char.IsUpper(metrica[metrica.IndexOf('.') + 1])) valor = 0;
                else
                {
                    falhas.Add($"{metrica}: a métrica não foi medida (o passo não rodou?)");
                    continue;
                }
            }
            if (!Atende(valor, condicao)) falhas.Add($"{metrica}: esperado {condicao}, veio {valor:0.##}");
        }
        return falhas;
    }

    static bool Atende(double valor, string condicao)
    {
        condicao = condicao.Trim();
        if (condicao is "true") return valor >= 1;
        if (condicao is "false") return valor < 1;
        double Numero(string s) => double.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
        if (condicao.StartsWith(">=")) return valor >= Numero(condicao.Substring(2)) - 1e-9;
        if (condicao.StartsWith("<=")) return valor <= Numero(condicao.Substring(2)) + 1e-9;
        if (condicao.StartsWith(">")) return valor > Numero(condicao.Substring(1)) + 1e-9;
        if (condicao.StartsWith("<")) return valor < Numero(condicao.Substring(1)) - 1e-9;
        return Math.Abs(valor - Numero(condicao)) < 1e-6;
    }

    // ----- caminhos -----

    /// <summary>{Samples} = exemplos do Revit desta versão; {Documentos} = Documentos do usuário.</summary>
    static string Substituir(Autodesk.Revit.ApplicationServices.Application app, string caminho) => caminho
        .Replace("{Samples}", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", $"Revit {app.VersionNumber}", "Samples"))
        .Replace("{Documentos}", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

    /// <summary>Caminho com * no nome do arquivo vira a lista dos que existem.</summary>
    static IEnumerable<string> Expandir(Autodesk.Revit.ApplicationServices.Application app, string caminho)
    {
        var real = Substituir(app, caminho);
        var pasta = Path.GetDirectoryName(real)!;
        var nome = Path.GetFileName(real);
        if (!nome.Contains('*')) return File.Exists(real) ? [real] : [];
        return Directory.Exists(pasta) ? Directory.GetFiles(pasta, nome) : [];
    }

    static string Seguro(string nome) => string.Concat(nome.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));

    /// <summary>Copia tirando o somente leitura: os exemplos do Revit vêm assim, e a cópia herdaria o atributo (não salvaria nem seria sobrescrita).</summary>
    static void Copiar(string origem, string destino)
    {
        if (File.Exists(destino)) File.SetAttributes(destino, FileAttributes.Normal);
        File.Copy(origem, destino, true);
        File.SetAttributes(destino, FileAttributes.Normal);
    }
}

sealed class FilaDeTestes
{
    public List<Cenario> Cenarios { get; set; } = [];
    public bool FecharRevit { get; set; } = true;
}

sealed class Cenario
{
    public string Nome { get; set; } = "cenario";

    /// <summary>Projeto a abrir (aceita {Samples} e {Documentos}). O teste copia para a pasta de saída e trabalha na cópia.</summary>
    public string? Abrir { get; set; }

    /// <summary>Nome do template de projeto (ex.: Electrical-DefaultBRAPTB.rte): cria um projeto novo por ele.</summary>
    public string? Novo { get; set; }

    /// <summary>Arquivos copiados para a pasta de saída antes de abrir (vínculos de caminho relativo); aceita * no nome.</summary>
    public List<string> CopiarJunto { get; set; } = [];

    public DadosProjeto? Dados { get; set; }

    /// <summary>Cena montada do zero: "apartamento".</summary>
    public string? Cena { get; set; }

    /// <summary>Confirma o uso sugerido pelo nome de cada ambiente.</summary>
    public bool UsosSugeridos { get; set; }

    /// <summary>Trecho do nome do ambiente → uso, por cima da sugestão.</summary>
    public Dictionary<string, UsoAmbiente>? Usos { get; set; }

    public PadraoDeLancamento? Lancamento { get; set; }
    public AlturaDePassagem? Passagem { get; set; }

    /// <summary>
    ///     Passos em ordem: ambientes, parametros, pontosPorAmbiente, criarCircuitos, tracarEstrela, passarCircuitos,
    ///     caixasPorRegra, verificar, calcular, desenharFiacao, etiquetar, legenda, quadros, lista, pranchas. Um passo repetido
    ///     ganha #2, #3 nas métricas.
    /// </summary>
    public List<string> Passos { get; set; } = [];

    public bool Salvar { get; set; } = true;

    /// <summary>Métrica → condição ("0", "3", ">=1", "<=2", "true"). Ex.: "verificar#2.M-03": "0", "pranchas.folhas": ">=3".</summary>
    public Dictionary<string, string>? Esperado { get; set; }
}

sealed class EtapaExecutada
{
    public string Nome { get; set; } = "";
    public double Segundos { get; set; }
    public string? Texto { get; set; }
    public string? Erro { get; set; }
    public List<string>? Pilha { get; set; }
}

sealed class RelatorioCenario
{
    public string Nome { get; set; } = "";
    public bool Ok { get; set; }
    public bool? Passou { get; set; }
    public List<string>? Divergencias { get; set; }
    public string? Erro { get; set; }
    public List<EtapaExecutada> Etapas { get; set; } = [];
    public Dictionary<string, double> Metricas { get; set; } = new();
    public List<string>? Dialogos { get; set; }
    public string? Arquivo { get; set; }
    public List<string>? Ambientes { get; set; }
    public List<string>? Pontos { get; set; }
    public List<string>? Circuitos { get; set; }
    public List<string>? Tracado { get; set; }
    public List<string>? Passagem { get; set; }
    public List<string>? Problemas { get; set; }
    public List<string>? Calculo { get; set; }
    public List<string>? Folhas { get; set; }
}
