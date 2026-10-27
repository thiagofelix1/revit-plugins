using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Spk.AddIn.Commands;
using Spk.AddIn.Servicos;
using Spk.AddIn.Modelagem;
using Spk.Motor.Calculo;
using Spk.AddIn.Verificacao;

namespace Spk.AddIn.Automacao;

/// <summary>
/// Teste sem cliques. Se existir <c>Documentos\SPK\testes\fila.json</c> quando o Revit abre, o plugin roda cada cenário
/// (abrir uma cópia do projeto, gravar critérios, calcular, gerar folhas), responde sozinho os diálogos do Revit, grava
/// <c>relatorio.json</c> com números, avisos e tempos, e fecha o Revit. Sem o arquivo, nada disto é ligado.
/// </summary>
static class TesteAutomatico
{
    static string Pasta => Path.Combine(PastasSpk.Raiz, "testes");
    static string ArquivoFila => Path.Combine(Pasta, "fila.json");
    static string ArquivoRelatorio => Path.Combine(Pasta, "relatorio.json");
    static string ArquivoConcluido => Path.Combine(Pasta, "concluido.txt");
    static string PastaSaida => Path.Combine(Pasta, "saida");

    static readonly JsonSerializerOptions Json = new()
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

    /// <summary>Responde os diálogos do Revit: referência não encontrada é ignorada; o resto, OK ou fechar.</summary>
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

    static void AoFicarOcioso(object? sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
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
        File.Move(ArquivoFila, Path.Combine(Pasta, "fila-executada.json"), true);
        Directory.CreateDirectory(PastaSaida);

        var relatorios = new List<RelatorioCenario>();
        foreach (var cenario in fila.Cenarios)
        {
            relatorios.Add(Rodar(ui.Application, cenario));
            GravarRelatorio(relatorios);
        }
        File.WriteAllText(ArquivoConcluido, DateTime.Now.ToString("O"));

        aplicacao.DialogBoxShowing -= AoMostrarDialogo;
        if (fila.FecharRevit)
            ui.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit));
    }

    static void GravarRelatorio(List<RelatorioCenario> relatorios)
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(ArquivoRelatorio, JsonSerializer.Serialize(relatorios, Json));
    }

    static RelatorioCenario Rodar(Autodesk.Revit.ApplicationServices.Application app, Cenario cenario)
    {
        var relatorio = new RelatorioCenario { Nome = cenario.Nome };
        dialogos.Clear();
        Document? doc = null;
        var arquivo = Path.Combine(PastaSaida, Seguro(cenario.Nome) + ".rvt");
        try
        {
            Etapa(relatorio, "abrir", () =>
            {
                if (cenario.Novo)
                {
                    var modelo = app.DefaultProjectTemplate;
                    doc = string.IsNullOrEmpty(modelo) || !File.Exists(modelo)
                        ? app.NewProjectDocument(UnitSystem.Metric)
                        : app.NewProjectDocument(modelo);
                    return $"Projeto novo ({(string.IsNullOrEmpty(modelo) ? "sem modelo" : Path.GetFileName(modelo))})";
                }
                var origem = cenario.Abrir ?? throw new InvalidOperationException("O cenário precisa de \"abrir\" ou \"novo\".");
                File.Copy(origem, arquivo, true);
                doc = app.OpenDocumentFile(arquivo);
                return $"Cópia de {origem}";
            });

            if (cenario.Dados is { } dados)
                Etapa(relatorio, "dados", () =>
                {
                    ArmazenamentoSpk.GravarComTransacao(doc!, DadosProjeto.Chave, dados, "SPK — Dados do projeto (teste)");
                    return dados.Resumo();
                });

            if (cenario.Mapeamento is { Count: > 0 } ajustes)
                Etapa(relatorio, "mapeamento", () => AjustarMapeamento(doc!, ajustes));

            if (cenario.Vincular is { } vinculos)
                Etapa(relatorio, "vincular", () => Cenas.Vincular(doc!, vinculos));

            relatorio.Vgas = MapeamentoGravado.Vgas(doc!).Select(v => new VgaLida
            {
                Id = v.Id.Value,
                Nome = LeitorRede.NomeDaVga(v),
                Familia = $"{v.Symbol?.Family?.Name} : {v.Symbol?.Name}",
                Ponto = (v.Location as LocationPoint)?.Point is { } p ? [Math.Round(p.X * 0.3048, 2), Math.Round(p.Y * 0.3048, 2), Math.Round(p.Z * 0.3048, 2)] : null,
                Conectores = Conectores(doc!, v),
            }).ToList();

            Cena? cena = null;
            if (cenario.Cena is { } nomeCena)
                Etapa(relatorio, "cena", () =>
                {
                    cena = Cenas.Montar(doc!, nomeCena);
                    return cena.Resumo;
                });

            if (cenario.TracarRamal || cenario.Ramais is not null)
                Etapa(relatorio, "tracarRamal", () =>
                {
                    if (cena is null) throw new InvalidOperationException("\"tracarRamal\" precisa de uma cena (bicos e a subgeral).");
                    var feito = TracarRamal.Executar(doc!, cena.Bicos, cena.Ramais[0], cenario.Ramais ?? new OpcoesRamais());
                    relatorio.Ligacao = new ResumoLigacao
                    {
                        Ramais = feito.Tracados,
                        Ligados = feito.Ligados,
                        Criados = feito.Criados.Count,
                        Recusados = feito.Recusados.GroupBy(r => r).Select(g => $"{g.Count()}× {g.Key}").ToList(),
                    };
                    return feito.Texto();
                });

            if (cenario.LigarBicos)
                Etapa(relatorio, "ligarBicos", () =>
                {
                    var bicos = cena?.Bicos ?? new FilteredElementCollector(doc!).OfCategory(BuiltInCategory.OST_Sprinklers)
                        .WhereElementIsNotElementType().OfType<FamilyInstance>().ToList();
                    var ramais = cena?.Ramais ?? new FilteredElementCollector(doc!).OfClass(typeof(Autodesk.Revit.DB.Plumbing.Pipe))
                        .Cast<Autodesk.Revit.DB.Plumbing.Pipe>().ToList();
                    var feito = LigarBicos.Executar(doc!, bicos, ramais);
                    relatorio.Ligacao = new ResumoLigacao
                    {
                        Ligados = feito.Ligados,
                        Criados = feito.Criados.Count,
                        Recusados = feito.Recusados.GroupBy(r => r).Select(g => $"{g.Count()}× {g.Key}").ToList(),
                    };
                    return feito.Texto();
                });

            if (cenario.RedeExemplo || cenario.Exemplo is not null)
                Etapa(relatorio, "redeExemplo", () =>
                {
                    var escolhido = cenario.Exemplo is { } nome
                        ? CenariosExemplo.PorNome(nome) ?? throw new InvalidOperationException($"Não há cenário de exemplo \"{nome}\".")
                        : CenariosExemplo.Simples;
                    return RedeExemploCommand.Criar(doc!, escolhido, gravarCriterios: cenario.Dados is null).Resumo;
                });

            if (cenario.Padrao is { } ajuste)
                Etapa(relatorio, "padrao", () =>
                {
                    var padrao = PadraoEscritorio.Carregar(doc!);
                    padrao.NumeroInicial = ajuste.NumeroInicial;
                    padrao.CodigoFolha = ajuste.CodigoFolha;
                    padrao.ParametroCodigo = ajuste.ParametroCodigo;
                    ArmazenamentoSpk.GravarComTransacao(doc!, PadraoEscritorio.Chave, padrao, "SPK — Padrão (teste)");
                    return padrao.Resumo();
                });

            if (cenario.Sabotar is { Count: > 0 } sabotagens)
                Etapa(relatorio, "sabotar", () => Sabotagem.Aplicar(doc!, sabotagens));

            if (cenario.Reconectar)
                Etapa(relatorio, "reconectar", () =>
                {
                    var encostadas = VerificacaoModelo.Verificar(doc!).Grupos.FirstOrDefault(g => g.Codigo == "encostado-sem-ligar");
                    var antes = Diagnostico.Ligacoes(doc!);
                    var feito = Reconexao.Executar(doc!, encostadas?.Ocorrencias ?? []);
                    relatorio.Reconexao = new ResumoReconexao
                    {
                        Encostadas = encostadas?.Ocorrencias.Count ?? 0,
                        Ligadas = feito.Ligadas,
                        LigacoesAntes = antes,
                        LigacoesDepois = Diagnostico.Ligacoes(doc!),
                        Recusadas = feito.Recusadas.GroupBy(r => r).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()}× {g.Key}").ToList(),
                    };
                    return feito.Texto();
                });

            if (cenario.Diametros is { } setorDiametros)
                Etapa(relatorio, "diametros", () =>
                {
                    if (cenario.ListarConexoes) relatorio.ConexoesAntes = Diagnostico.Conexoes(doc!);
                    var (dados, leitura, fonte, vgas, doSetor) = Setor(doc!, setorDiametros);
                    var lida = LeitorRede.Montar(doc!, leitura.Bicos, dados.FatorC, fonte, vgas, doSetor, setorDiametros.Setor);
                    var dimensionamento = DimensionamentoPorTabela.Calcular(lida.Rede, dados.Classe);
                    var feito = AplicarDiametros.Executar(doc!, lida, dimensionamento);
                    relatorio.Diametros = new ResumoDiametros
                    {
                        Mudados = feito.Mudados,
                        JaCertos = feito.JaCertos,
                        TubosPorDn = feito.TubosPorDn.OrderBy(p => p.Key).ToDictionary(p => p.Key.ToString(), p => p.Value),
                        Recusados = feito.Recusados.GroupBy(r => r).Select(g => $"{g.Count()}× {g.Key}").ToList(),
                        Divergentes = feito.Divergentes.ToList(),
                        Inseridas = feito.Inseridas.ToList(),
                        Avisos = dimensionamento.Avisos.ToList(),
                    };
                    if (cenario.ListarConexoes) relatorio.Conexoes = Diagnostico.Conexoes(doc!);
                    return feito.Texto();
                });

            if (cenario.Arquitetura)
                Etapa(relatorio, "arquitetura", () =>
                {
                    var edificio = Arquitetura.LeituraDoEdificio.Ler(doc!);
                    relatorio.Arquitetura = Arquitetura.ArquiteturaJanela.Relatorio(edificio).Split('\n').ToList();
                    return $"{edificio.Ambientes.Count} ambientes e {edificio.Vigas.Count} vigas";
                });

            if (cenario.BicosPorAmbiente is { } pedidoBicos)
                Etapa(relatorio, "bicosPorAmbiente", () =>
                {
                    var dados = DadosProjeto.Carregar(doc!) ?? throw new InvalidOperationException("Mande \"dados\" no cenário.");
                    var edificio = Arquitetura.LeituraDoEdificio.Ler(doc!);
                    var limites = BicosPorAmbiente.Limites(dados, pedidoBicos);
                    var criados = new List<ElementId>();
                    var feitos = BicosPorAmbiente.Posicionar(doc!, edificio, edificio.Ambientes, limites, pedidoBicos, criados);
                    var conferidos = BicosPorAmbiente.Conferir(doc!, edificio, edificio.Ambientes, limites, pedidoBicos);
                    relatorio.Bicos = feitos.Select(r => "posicionar | " + r.Texto().Replace("\n", " ")).Concat(
                        conferidos.Select(r => "conferir | " + r.Texto().Replace("\n", " "))).ToList();
                    return $"{criados.Count} bicos em {feitos.Count(r => r.Recusa is null)} ambientes";
                });

            if (cenario.GerarRede is { } pedidoRede)
                Etapa(relatorio, "gerarRede", () =>
                {
                    var dados = DadosProjeto.Carregar(doc!) ?? throw new InvalidOperationException("Mande \"dados\" no cenário.");
                    var edificio = Arquitetura.LeituraDoEdificio.Ler(doc!);
                    var criados = new List<ElementId>();
                    var feitos = Modelagem.GerarRede.Executar(doc!, dados, edificio, edificio.Ambientes, pedidoRede, criados);
                    relatorio.Rede = feitos.Select(r => r.Texto().Replace("\n", " ")).ToList();
                    return $"{criados.Count} elementos em {feitos.Count(r => r.Recusa is null)} de {feitos.Count} ambientes";
                });

            if (cenario.InspecionarVinculos)
                relatorio.Vinculos = Diagnostico.Vinculos(doc!);

            if (cenario.InspecionarCarimbo)
                relatorio.Carimbos = Diagnostico.Carimbos(doc!);

            if (cenario.Verificar)
                Etapa(relatorio, "verificar", () =>
                {
                    var verificacao = VerificacaoModelo.Verificar(doc!);
                    relatorio.Verificacao = new ResumoVerificacao
                    {
                        Bicos = verificacao.Bicos,
                        Tubos = verificacao.Tubos,
                        Pecas = verificacao.Pecas,
                        Setores = verificacao.Setores,
                        Erros = verificacao.Erros,
                        Avisos = verificacao.Avisos,
                        Grupos = verificacao.Grupos.Select(g => new GrupoVerificado
                        {
                            Codigo = g.Codigo,
                            Gravidade = g.Gravidade.ToString(),
                            Quantidade = g.Ocorrencias.Count,
                            Exemplos = g.Ocorrencias.Take(5).Select(o => o.Descricao).ToList(),
                        }).ToList(),
                    };
                    return verificacao.Resumo();
                });

            if (cenario.Calcular is { } pedido)
                Etapa(relatorio, "calcular", () =>
                {
                    var resultado = Calcular(doc!, pedido);
                    relatorio.Calculo = Resumo(resultado);
                    return resultado.Texto();
                });

            if (cenario.Folhas)
                Etapa(relatorio, "folhas", () =>
                {
                    var pasta = Path.Combine(PastaSaida, Seguro(cenario.Nome));
                    var folhas = ServicoFolhas.Gerar(doc!, pasta);
                    relatorio.Folhas = new ResumoFolhas
                    {
                        Folhas = folhas.Folhas.Select(id => doc!.GetElement(id) is ViewSheet s ? $"{s.SheetNumber}  {s.Name}" : id.ToString()).ToList(),
                        Pdf = folhas.Pdf,
                        Textos = folhas.Textos.ToList(),
                        Vistas = Diagnostico.Vistas(doc!),
                    };
                    if (cenario.InspecionarCarimbo)
                        relatorio.CarimbosSpk = Diagnostico.Carimbos(doc!, folhas.Folhas.Select(doc!.GetElement).OfType<ViewSheet>().ToList());
                    return folhas.Texto();
                });

            Etapa(relatorio, "salvar", () =>
            {
                if (cenario.Novo) doc!.SaveAs(arquivo, new SaveAsOptions { OverwriteExistingFile = true });
                else doc!.Save();
                // O Revit guarda cópias numeradas (nome.0001.rvt) a cada gravação; no teste elas só ocupam espaço.
                foreach (var copia in Directory.GetFiles(PastaSaida, Path.GetFileNameWithoutExtension(arquivo) + ".0*.rvt"))
                    File.Delete(copia);
                return arquivo;
            });
            relatorio.Arquivo = arquivo;
            relatorio.Ok = true;
            relatorio.Dialogos = dialogos.ToList();
            if (cenario.Esperado is { } esperado)
            {
                relatorio.Divergencias = Conferir(esperado, relatorio);
                relatorio.Passou = relatorio.Divergencias.Count == 0;
            }
        }
        catch (Exception ex)
        {
            relatorio.Erro ??= ex.ToString();
        }
        finally
        {
            relatorio.Dialogos = dialogos.ToList();
            try { doc?.Close(false); }
            catch (Exception ex) { relatorio.Erro ??= "Ao fechar: " + ex.Message; }
        }
        return relatorio;
    }

    /// <summary>Roda a etapa, mede o tempo e anota o texto ou o erro. Erro interrompe o cenário.</summary>
    static void Etapa(RelatorioCenario relatorio, string nome, Func<string> acao)
    {
        var relogio = Stopwatch.StartNew();
        var etapa = new EtapaExecutada { Nome = nome };
        relatorio.Etapas.Add(etapa);
        try
        {
            etapa.Texto = acao();
        }
        catch (Exception ex)
        {
            etapa.Erro = ex.Message;
            relatorio.Erro = $"{nome}: {ex}";
            throw;
        }
        finally
        {
            etapa.Segundos = Math.Round(relogio.Elapsed.TotalSeconds, 1);
        }
    }

    static ServicoCalculo.Resultado Calcular(Document doc, PedidoCalculo pedido)
    {
        var (dados, leitura, fonte, vgas, doSetor) = Setor(doc, pedido);
        return ServicoCalculo.Executar(doc, dados, leitura, fonte, vgas, doSetor, pedido.Setor);
    }

    /// <summary>O setor do pedido, como Calcular e Diâmetros o escolhem na tela.</summary>
    static (DadosProjeto Dados, BicosDoModelo.Leitura Leitura, FamilyInstance? Fonte, List<ElementId> Vgas, ElementId? DoSetor) Setor(
        Document doc, PedidoCalculo pedido)
    {
        var dados = DadosProjeto.Carregar(doc) ?? throw new InvalidOperationException("O projeto não tem Dados do projeto; mande \"dados\" no cenário.");
        var leitura = BicosDoModelo.Ler(doc);
        var vgas = leitura.Gerados ? [] : MapeamentoGravado.Vgas(doc);
        FamilyInstance? fonte = null;
        if (pedido.Setor is not null || pedido.Elemento is not null)
            vgas = [];
        else if (pedido.Vga is { } idVga)
            fonte = vgas.FirstOrDefault(v => v.Id.Value == idVga)
                    ?? throw new InvalidOperationException($"A VGA {idVga} não está entre as marcadas como Fonte.");
        else if (vgas.Count == 1)
            fonte = vgas[0];
        else if (vgas.Count > 1)
            throw new InvalidOperationException($"Há {vgas.Count} VGAs; diga qual em \"calcular.vga\" (ids no relatório, em \"vgas\").");

        var doSetor = pedido.Elemento is { } idElemento ? new ElementId(idElemento) : null;
        return (dados, leitura, fonte, vgas.Select(v => v.Id).ToList(), doSetor);
    }

    static ResumoCalculo Resumo(ServicoCalculo.Resultado r)
    {
        var critico = r.Critico;
        return new ResumoCalculo
        {
            Atende = r.Relatorio.Atende,
            Setor = r.Lida.Setor,
            TotalDeSetores = r.Lida.TotalDeSetores,
            Bicos = r.Lida.Rede.Chuveiros.Count,
            Trechos = r.Lida.Rede.Trechos.Count,
            AreasAvaliadas = r.Relatorio.Busca?.CandidatosAvaliados ?? 1,
            PressaoKPa = Math.Round(r.Relatorio.Resultado.PressaoNecessaria, 1),
            VazaoChuveirosLmin = Math.Round(r.Relatorio.Resultado.VazaoChuveiros, 1),
            VazaoTotalLmin = Math.Round(r.Relatorio.VazaoTotal, 1),
            Critico = critico.Id,
            CriticoKPa = Math.Round(critico.Pressao, 1),
            CriticoLmin = Math.Round(critico.Vazao, 1),
            Falhas = r.Falhas.ToList(),
            Avisos = r.Avisos.ToList(),
            Rede = r.Lida.Rede.Trechos.Count <= 80 ? Rede(r.Lida.Rede) : null,
        };
    }

    /// <summary>Nós e trechos da rede lida, em rede pequena: mostra como o leitor montou a geometria (ramais, braços).</summary>
    static List<string> Rede(Spk.Motor.Modelo.Rede rede)
    {
        var nos = rede.Nos.ToDictionary(n => n.Id);
        string P(string id) => nos.TryGetValue(id, out var n) ? $"{id}({n.X:0.00};{n.Y:0.00};{n.Z:0.00})" : id;
        var linhas = rede.Chuveiros.Select(c => $"bico {c.Id} no {c.No} ramal {c.Ramal}").ToList();
        linhas.AddRange(rede.Trechos.Select(t => $"{P(t.De)} -> {P(t.Para)} L={t.Comprimento:0.00}"));
        return linhas;
    }

    /// <summary>
    /// Ajusta o mapeamento gravado: cada ajuste acha as linhas pela família (e pelo tipo, se informado), por trecho do
    /// nome. Família que ainda não tem linha mas existe no modelo ganha uma.
    /// </summary>
    static string AjustarMapeamento(Document doc, List<AjusteMapeamento> ajustes)
    {
        var linhas = MapeamentoGravado.Tentar(doc)?.ToList() ?? [];
        var feitos = new List<string>();
        foreach (var ajuste in ajustes)
        {
            bool Bate(string familia, string tipo) =>
                familia.Contains(ajuste.Familia, StringComparison.OrdinalIgnoreCase)
                && (ajuste.Tipo is null || tipo.Contains(ajuste.Tipo, StringComparison.OrdinalIgnoreCase));

            var alvo = linhas.Where(l => Bate(l.Familia, l.Tipo)).ToList();
            if (alvo.Count == 0)
            {
                alvo = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol is { } s && Bate(s.Family.Name, s.Name))
                    .GroupBy(fi => (fi.Category?.Name ?? "", fi.Symbol.Family.Name, fi.Symbol.Name))
                    .Select(g => new LinhaMapeamento { Categoria = g.Key.Item1, Familia = g.Key.Item2, Tipo = g.Key.Item3 })
                    .ToList();
                linhas.AddRange(alvo);
            }
            foreach (var linha in alvo)
            {
                if (ajuste.Papel is { } papel) linha.Papel = papel;
                if (ajuste.FatorK is { } k) linha.ValorFatorK = k.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (ajuste.Area is { } area) linha.ValorAreaCobertura = area.ToString(System.Globalization.CultureInfo.InvariantCulture);
                feitos.Add($"{linha.Familia} : {linha.Tipo} → {linha.Papel} K {linha.ValorFatorK} área {linha.ValorAreaCobertura}");
            }
            if (alvo.Count == 0) feitos.Add($"Nenhuma família com \"{ajuste.Familia}\" no modelo.");
        }
        MapeamentoGravado.Salvar(doc, linhas);
        return string.Join("\n", feitos);
    }

    /// <summary>
    /// Diagnóstico de cada conector da VGA: onde está, se está ligado no Revit e a que distância fica a ponta e o corpo
    /// do tubo mais próximo. Mostra por que a leitura por proximidade não passa pela válvula.
    /// </summary>
    static List<ConectorLido> Conectores(Document doc, FamilyInstance vga)
    {
        var tubos = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Plumbing.Pipe)).Cast<Autodesk.Revit.DB.Plumbing.Pipe>()
            .Select(t => (Tubo: t, Curva: (t.Location as LocationCurve)?.Curve)).Where(t => t.Curva is not null).ToList();
        var lista = new List<ConectorLido>();
        if (vga.MEPModel?.ConnectorManager is not { } gerente) return lista;
        foreach (Connector c in gerente.Connectors)
        {
            var o = c.Origin;
            var ponta = tubos.SelectMany(t => new[] { t.Curva!.GetEndPoint(0), t.Curva.GetEndPoint(1) }.Select(e => (t.Tubo, D: e.DistanceTo(o))))
                .OrderBy(x => x.D).FirstOrDefault();
            var corpo = tubos.Select(t => (t.Tubo, D: t.Curva!.Project(o)?.Distance ?? double.MaxValue)).OrderBy(x => x.D).FirstOrDefault();
            string? ligadoA = null;
            if (c.IsConnected)
                foreach (Connector outro in c.AllRefs)
                    if (outro.Owner is { } dono && dono.Id != vga.Id) ligadoA = $"{dono.Category?.Name} {dono.Id.Value}";
            lista.Add(new ConectorLido
            {
                Ponto = [Math.Round(o.X * 0.3048, 3), Math.Round(o.Y * 0.3048, 3), Math.Round(o.Z * 0.3048, 3)],
                DiametroMm = c.Shape == ConnectorProfileType.Round ? Math.Round(c.Radius * 2 * 304.8) : 0,
                LigadoA = ligadoA,
                PontaDeTuboMaisProximaM = ponta.Tubo is null ? null : Math.Round(ponta.D * 0.3048, 3),
                TuboDaPonta = ponta.Tubo?.Id.Value,
                CorpoDeTuboMaisProximoM = corpo.Tubo is null ? null : Math.Round(corpo.D * 0.3048, 3),
                TuboDoCorpo = corpo.Tubo?.Id.Value,
                Rastreio = ponta.Tubo is null ? null : Rastrear(doc, tubos, ponta.Tubo, o),
            });
        }
        return lista;
    }

    /// <summary>
    /// A partir do tubo encostado no conector, segue pela outra ponta até o tubo ou peça mais próximo, 12 saltos no
    /// máximo. Cada linha: tubo, DN, comprimento, cota e a distância até o próximo. O primeiro salto grande mostra o vão.
    /// </summary>
    static List<string> Rastrear(Document doc, List<(Autodesk.Revit.DB.Plumbing.Pipe Tubo, Curve? Curva)> tubos,
        Autodesk.Revit.DB.Plumbing.Pipe inicio, XYZ entrada)
    {
        var pecas = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .Where(fi => fi.Category?.Id.Value is (long)BuiltInCategory.OST_PipeFitting or (long)BuiltInCategory.OST_PipeAccessory
                or (long)BuiltInCategory.OST_Sprinklers)
            .SelectMany(fi => fi.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().Select(c => (Peca: fi, c.Origin)) ?? [])
            .ToList();
        var linhas = new List<string>();
        var vistos = new HashSet<long>();
        var atual = inicio;
        var chegada = entrada;
        for (var salto = 0; salto < 12 && atual is not null && vistos.Add(atual.Id.Value); salto++)
        {
            var curva = ((LocationCurve)atual.Location).Curve;
            var (a, b) = (curva.GetEndPoint(0), curva.GetEndPoint(1));
            var saida = a.DistanceTo(chegada) < b.DistanceTo(chegada) ? b : a;
            var dn = atual.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() * 304.8 ?? 0;
            var proximoTubo = tubos.Where(t => t.Tubo.Id != atual.Id && !vistos.Contains(t.Tubo.Id.Value))
                .Select(t => (t.Tubo, D: Math.Min(t.Curva!.GetEndPoint(0).DistanceTo(saida), t.Curva.GetEndPoint(1).DistanceTo(saida)),
                    Corpo: t.Curva.Project(saida)?.Distance ?? double.MaxValue))
                .OrderBy(x => Math.Min(x.D, x.Corpo)).FirstOrDefault();
            var proximaPeca = pecas.Select(p => (p.Peca, D: p.Origin.DistanceTo(saida))).OrderBy(x => x.D).FirstOrDefault();
            linhas.Add($"tubo {atual.Id.Value} Ø{dn:0} L={curva.Length * 0.3048:0.00} m, sai em z={saida.Z * 0.3048:0.00}; " +
                       $"peça mais próxima {proximaPeca.Peca?.Category?.Name} {proximaPeca.Peca?.Id.Value} a {proximaPeca.D * 0.3048:0.000} m; " +
                       $"tubo mais próximo {proximoTubo.Tubo?.Id.Value} a {proximoTubo.D * 0.3048:0.000} m (corpo {proximoTubo.Corpo * 0.3048:0.000} m)");
            if (proximoTubo.Tubo is null || Math.Min(proximoTubo.D, proximoTubo.Corpo) * 0.3048 > 0.5) break;
            chegada = saida;
            atual = proximoTubo.Tubo;
        }
        return linhas;
    }

    /// <summary>O que o cenário esperava e não aconteceu. Lista vazia: passou.</summary>
    static List<string> Conferir(Esperado esperado, RelatorioCenario r)
    {
        var falhas = new List<string>();
        // Janela de erro do Revit é o engenheiro clicando: numa rede grande, centenas de vezes. Nunca pode aparecer.
        var janelas = r.Dialogos?.Count(d => !d.StartsWith("TaskDialog_Unresolved_References")) ?? 0;
        if (janelas > 0)
            falhas.Add($"o Revit abriu {janelas} janela(s) que o engenheiro teria que fechar: {r.Dialogos!.First(d => !d.StartsWith("TaskDialog_Unresolved_References"))}");
        var c = r.Calculo;
        if (esperado.Atende is { } atende && c?.Atende != atende) falhas.Add($"atende: esperado {atende}, veio {c?.Atende}");
        if (esperado.Bicos is { } bicos && c?.Bicos != bicos) falhas.Add($"bicos: esperado {bicos}, veio {c?.Bicos}");
        if (esperado.PressaoKPa is { } pressao && (c is null || Math.Abs(c.PressaoKPa - pressao) > 0.5))
            falhas.Add($"pressão: esperado {pressao} kPa, veio {c?.PressaoKPa}");
        if (esperado.Critico is { } critico && c?.Critico != critico) falhas.Add($"crítico: esperado {critico}, veio {c?.Critico}");
        if (esperado.Folhas is { } folhas && (r.Folhas?.Folhas.Count ?? 0) != folhas)
            falhas.Add($"folhas: esperado {folhas}, veio {r.Folhas?.Folhas.Count ?? 0}");
        foreach (var (codigo, condicao) in esperado.Verificacao ?? [])
        {
            if (r.Verificacao is null)
            {
                falhas.Add("verificação: o cenário não rodou \"verificar\"");
                break;
            }
            var quantidade = codigo switch
            {
                "erros" => r.Verificacao.Erros,
                "avisos" => r.Verificacao.Avisos,
                "setores" => r.Verificacao.Setores,
                _ => r.Verificacao.Grupos.FirstOrDefault(g => g.Codigo == codigo)?.Quantidade ?? 0,
            };
            if (!Atende(quantidade, condicao)) falhas.Add($"verificação {codigo}: esperado {condicao}, veio {quantidade}");
        }
        if (esperado.Diametros is { } porDn)
        {
            var veio = r.Diametros?.TubosPorDn ?? [];
            if (porDn.Count != veio.Count || porDn.Any(p => !veio.TryGetValue(p.Key, out var n) || n != p.Value))
                falhas.Add($"diâmetros: esperado {Dns(porDn)}, veio {Dns(veio)}");
            if (r.Diametros is { } d && d.Recusados.Count + d.Divergentes.Count + d.Inseridas.Count > 0)
                falhas.Add($"diâmetros: {d.Recusados.Count} recusa(s), {d.Divergentes.Count} divergência(s), {d.Inseridas.Count} peça(s) inserida(s)");
        }
        if (esperado.RamaisTracados is { } tracados && r.Ligacao?.Ramais != tracados)
            falhas.Add($"traçar ramais: esperado {tracados} ramais, veio {r.Ligacao?.Ramais}");
        if (esperado.BicosLigados is { } ligados && r.Ligacao?.Ligados != ligados)
            falhas.Add($"ligar bicos: esperado {ligados} ligados, veio {r.Ligacao?.Ligados}");
        if (esperado.BicosRecusados is { } recusados && (r.Ligacao?.Recusados.Sum(Quantos) ?? 0) != recusados)
            falhas.Add($"ligar bicos: esperado {recusados} recusados, veio {r.Ligacao?.Recusados.Sum(Quantos) ?? 0}");
        if (esperado.ReconexaoSemPerda == true)
        {
            if (r.Reconexao is not { } rc) falhas.Add("reconexão: o cenário não rodou \"reconectar\"");
            else if (rc.LigacoesDepois < rc.LigacoesAntes + rc.Ligadas)
                falhas.Add($"reconexão perdeu ligações: antes {rc.LigacoesAntes}, {rc.Ligadas} feitas, depois {rc.LigacoesDepois}");
        }
        foreach (var trecho in esperado.TextoDaRede ?? [])
            if (!(r.Rede ?? []).Any(t => t.Contains(trecho, StringComparison.Ordinal)))
                falhas.Add($"gerar rede: \"{trecho}\" não apareceu");
        foreach (var trecho in esperado.TextoDosBicos ?? [])
            if (!(r.Bicos ?? []).Any(t => t.Contains(trecho, StringComparison.Ordinal)))
                falhas.Add($"bicos por ambiente: \"{trecho}\" não apareceu");
        foreach (var trecho in esperado.TextoDaArquitetura ?? [])
            if (!(r.Arquitetura ?? []).Any(t => t.Contains(trecho, StringComparison.Ordinal)))
                falhas.Add($"arquitetura: \"{trecho}\" não apareceu");
        foreach (var trecho in esperado.TextoDasFolhas ?? [])
            if (!(r.Folhas?.Textos ?? []).Any(t => t.Contains(trecho, StringComparison.Ordinal)))
                falhas.Add($"folhas: texto \"{trecho}\" não apareceu");
        return falhas;
    }

    static string Dns(Dictionary<string, int> porDn) => string.Join(", ", porDn.Select(p => $"DN {p.Key}: {p.Value}"));

    /// <summary>"3× motivo" → 3.</summary>
    static int Quantos(string linha) => int.TryParse(linha.Split('×')[0], out var n) ? n : 1;

    /// <summary>"0", "3", "&gt;=1", "&lt;=2", "&gt;0".</summary>
    static bool Atende(int valor, string condicao)
    {
        condicao = condicao.Trim();
        int N(string t) => int.Parse(t.Trim(), System.Globalization.CultureInfo.InvariantCulture);
        if (condicao.StartsWith(">=")) return valor >= N(condicao[2..]);
        if (condicao.StartsWith("<=")) return valor <= N(condicao[2..]);
        if (condicao.StartsWith('>')) return valor > N(condicao[1..]);
        if (condicao.StartsWith('<')) return valor < N(condicao[1..]);
        return valor == N(condicao);
    }

    static string Seguro(string nome) => string.Concat(nome.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
}

/// <summary>Estado das vistas do SPK depois das folhas: o que a vista mostra das etiquetas e por quê.</summary>
static class Diagnostico
{
    /// <summary>
    /// Quantas ligações de tubulação o modelo tem (pares de conectores ligados). Corrigir só pode aumentar: cada ligação
    /// feita soma pelo menos uma, e nenhuma que existia pode sumir.
    /// </summary>
    public static int Ligacoes(Document doc)
    {
        var categorias = new ElementMulticategoryFilter(new[]
        {
            BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_FlexPipeCurves, BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PipeAccessory, BuiltInCategory.OST_Sprinklers,
        });
        var pontas = 0;
        foreach (var e in new FilteredElementCollector(doc).WherePasses(categorias).WhereElementIsNotElementType())
        {
            var gerente = e switch
            {
                MEPCurve curva => curva.ConnectorManager,
                FamilyInstance instancia => instancia.MEPModel?.ConnectorManager,
                _ => null,
            };
            if (gerente is null) continue;
            pontas += gerente.Connectors.Cast<Connector>().Count(c => c.ConnectorType == ConnectorType.End && c.IsConnected
                && c.AllRefs.Cast<Connector>().Any(r => r.Owner.Id != e.Id && r.Owner is MEPCurve or FamilyInstance));
        }
        return pontas / 2;
    }

    /// <summary>Cada conector de tubo, peça e bico: posição, DN e a quem está ligado.</summary>
    public static List<string> Conexoes(Document doc)
    {
        var categorias = new ElementMulticategoryFilter(new[]
        {
            BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_Sprinklers,
        });
        var linhas = new List<string>();
        foreach (var e in new FilteredElementCollector(doc).WherePasses(categorias).WhereElementIsNotElementType().OrderBy(e => e.Id.Value))
        {
            var gerente = e switch
            {
                MEPCurve curva => curva.ConnectorManager,
                FamilyInstance instancia => instancia.MEPModel?.ConnectorManager,
                _ => null,
            };
            if (gerente is null) continue;
            var partes = gerente.Connectors.Cast<Connector>().Where(c => c.Domain == Domain.DomainPiping).Select(c =>
            {
                var o = c.Origin;
                var refs = c.AllRefs.Cast<Connector>().Where(r => r.Owner.Id != e.Id)
                    .Select(r => $"{r.Owner.Id.Value}/{r.ConnectorType}");
                var raio = c.Shape == ConnectorProfileType.Round ? c.Radius * 2 * 304.8 : 0;
                return $"[{c.ConnectorType} ({o.X * 0.3048:0.000};{o.Y * 0.3048:0.000};{o.Z * 0.3048:0.000}) Ø{raio:0} → {string.Join(",", refs)}]";
            });
            linhas.Add($"{e.Category?.Name} {e.Id.Value} {(e as FamilyInstance)?.Symbol?.FamilyName}: {string.Join(" ", partes)}");
        }
        return linhas;
    }

    /// <summary>O próprio arquivo e cada vínculo: estado, transformação e quantos elementos de cada categoria de arquitetura e estrutura.</summary>
    public static List<string> Vinculos(Document doc)
    {
        var linhas = new List<string> { "Este arquivo: " + Conteudo(doc) };
        foreach (var vinculo in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            var tipo = doc.GetElement(vinculo.GetTypeId()) as RevitLinkType;
            var origem = vinculo.GetTotalTransform().Origin;
            var vinculado = vinculo.GetLinkDocument();
            linhas.Add($"Vínculo Revit {vinculo.Id.Value} \"{vinculo.Name}\" ({tipo?.GetLinkedFileStatus()}, " +
                       $"origem {origem.X * 0.3048:0.00};{origem.Y * 0.3048:0.00};{origem.Z * 0.3048:0.00} m, " +
                       $"rotação {Math.Atan2(vinculo.GetTotalTransform().BasisX.Y, vinculo.GetTotalTransform().BasisX.X) * 180 / Math.PI:0.#}°): " +
                       (vinculado is null ? "não carregado" : vinculado.PathName + " — " + Conteudo(vinculado)));
        }
        foreach (var importado in new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).Cast<ImportInstance>())
            linhas.Add($"{(importado.IsLinked ? "Vínculo" : "Importação")} CAD {importado.Id.Value} \"{importado.Category?.Name}\" " +
                       $"na vista {(importado.ViewSpecific ? doc.GetElement(importado.OwnerViewId)?.Name : "(modelo)")}");
        return linhas;
    }

    static string Conteudo(Document doc)
    {
        int Contar(BuiltInCategory categoria) =>
            new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsNotElementType().GetElementCount();
        var ambientes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType()
            .Cast<SpatialElement>().ToList();
        var comArea = ambientes.Count(a => a.Area > 0);
        var niveis = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(n => n.Elevation)
            .Select(n => $"{n.Name} {n.Elevation * 0.3048:0.00}").ToList();
        var exemplos = ambientes.Where(a => a.Area > 0).Take(4).Select(a => $"{a.Name} ({a.Area * 0.092903:0.0} m²)");
        return $"{niveis.Count} níveis [{string.Join("; ", niveis.Take(8))}], {comArea} ambientes com área de {ambientes.Count} " +
               $"[{string.Join("; ", exemplos)}], {Contar(BuiltInCategory.OST_Ceilings)} forros, {Contar(BuiltInCategory.OST_Floors)} lajes/pisos, " +
               $"{Contar(BuiltInCategory.OST_StructuralFraming)} vigas, {Contar(BuiltInCategory.OST_StructuralColumns)} pilares, " +
               $"{Contar(BuiltInCategory.OST_Walls)} paredes, {Contar(BuiltInCategory.OST_Roofs)} telhados, " +
               $"{Contar(BuiltInCategory.OST_Sprinklers)} sprinklers, {Contar(BuiltInCategory.OST_PipeCurves)} tubos";
    }

    public static List<string> Vistas(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && v.Name.StartsWith(Folhas.PranchaSpk.PrefixoVista, StringComparison.Ordinal)
                        && v.ViewType is ViewType.ThreeD or ViewType.FloorPlan)
            .Select(v => Linha(doc, v))
            .ToList();

    static string Linha(Document doc, View v)
    {
        var etiquetas = new ElementId(BuiltInCategory.OST_PipeTags);
        var donas = new FilteredElementCollector(doc).OfClass(typeof(IndependentTag)).Cast<IndependentTag>()
            .Where(t => t.OwnerViewId == v.Id).ToList();
        var visiveis = new FilteredElementCollector(doc, v.Id).OfClass(typeof(IndependentTag)).GetElementCount();
        var categorias = donas.GroupBy(t => t.Category?.Name ?? "?").Select(g => $"{g.Key}×{g.Count()}");
        var exemplo = donas.FirstOrDefault();
        var template = doc.GetElement(v.ViewTemplateId)?.Name ?? "-";
        var sobreposicao = v.GetCategoryOverrides(etiquetas);
        var filtros = v.GetFilters().Select(f => $"{doc.GetElement(f)?.Name}(vis={v.GetFilterVisibility(f)})");
        return $"{v.Name} | {v.ViewType} | template={template} | escala={v.Scale} | anotacoesEscondidas={v.AreAnnotationCategoriesHidden} " +
               $"| pipeTagsEscondida={v.GetCategoryHidden(etiquetas)} | travada={(v as View3D)?.IsLocked} | tags donas={donas.Count} visiveis={visiveis} " +
               $"[{string.Join(",", categorias)}] | texto1={exemplo?.TagText} cabeca1={Fmt(exemplo?.TagHeadPosition)} " +
               $"| cor={sobreposicao.ProjectionLineColor?.IsValid} halftone={sobreposicao.Halftone} transp={sobreposicao.Transparency} " +
               $"| estilo={v.DisplayStyle} detalhe={v.DetailLevel} | filtros=[{string.Join(",", filtros)}] " +
               $"| recorteAnot={v.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE)?.AsInteger()} recorte={v.CropBoxActive}";
    }

    static string Fmt(XYZ? p) => p is null ? "-" : $"({p.X:0.0},{p.Y:0.0},{p.Z:0.0})";

    /// <summary>
    /// Os campos do carimbo das folhas: parâmetros da folha e da instância e do tipo do carimbo, com o valor. Mostra onde
    /// o escritório guarda código, revisão, número e total de pranchas.
    /// </summary>
    public static List<string> Carimbos(Document doc, List<ViewSheet>? apenas = null)
    {
        var linhas = new List<string>();
        var folhas = apenas ?? new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
            .Where(f => !f.IsPlaceholder).OrderBy(f => f.SheetNumber).Take(8).ToList();
        foreach (var folha in folhas)
        {
            linhas.Add($"FOLHA {folha.SheetNumber} | {folha.Name}");
            linhas.Add("  folha: " + Parametros(folha));
            foreach (var carimbo in new FilteredElementCollector(doc, folha.Id).OfCategory(BuiltInCategory.OST_TitleBlocks)
                         .WhereElementIsNotElementType().Cast<FamilyInstance>())
            {
                linhas.Add($"  carimbo {carimbo.Symbol?.Family?.Name} : {carimbo.Symbol?.Name}");
                linhas.Add("    instância: " + Parametros(carimbo));
                if (carimbo.Symbol is { } tipo) linhas.Add("    tipo: " + Parametros(tipo));
            }
        }
        // RevisionNumber não vale quando a numeração é por folha (a Nova Era é assim): fica de fora.
        var revisoes = new FilteredElementCollector(doc).OfClass(typeof(Revision)).Cast<Revision>()
            .Select(r => $"{r.SequenceNumber}: \"{r.Description}\" {r.RevisionDate} por {r.IssuedBy}/{r.IssuedTo} emitida={r.Issued}");
        linhas.Add("REVISÕES: " + string.Join(" | ", revisoes));
        foreach (var folha in folhas)
        {
            try
            {
                var ids = folha.GetAllRevisionIds();
                linhas.Add($"REVISÕES DA FOLHA {folha.SheetNumber}: {ids.Count} → atual {folha.GetCurrentRevision()?.Value}; " +
                           string.Join(", ", ids.Select(id => folha.GetRevisionNumberOnSheet(id))));
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException e)
            {
                linhas.Add($"REVISÕES DA FOLHA {folha.SheetNumber}: {e.Message}");
            }
        }
        linhas.Add("INFORMAÇÕES DO PROJETO: " + Parametros(doc.ProjectInformation));
        return linhas;
    }

    static string Parametros(Element e) =>
        string.Join("; ", e.Parameters.Cast<Parameter>()
            .Select(p => (p.Definition?.Name ?? "?", Valor(p), p.IsReadOnly, p.IsShared, (p.Definition as InternalDefinition)?.BuiltInParameter))
            .Where(p => !string.IsNullOrEmpty(p.Item2))
            .OrderBy(p => p.Item1)
            .Select(p => $"{p.Item1}{(p.IsShared ? "*" : "")}{(p.IsReadOnly ? "(ro)" : "")}{(p.Item5 is { } b && b != BuiltInParameter.INVALID ? $"[{b}]" : "")}={p.Item2}"));

    static string? Valor(Parameter p)
    {
        try
        {
            return p.StorageType switch
            {
                StorageType.String => p.AsString(),
                StorageType.ElementId => p.AsElementId() == ElementId.InvalidElementId ? null : p.AsValueString() ?? p.AsElementId().Value.ToString(),
                StorageType.None => null,
                _ => p.AsValueString(),
            };
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e)
        {
            return "(erro: " + e.Message + ")";
        }
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

    /// <summary>Projeto a abrir. O teste copia para a pasta de saída e trabalha na cópia.</summary>
    public string? Abrir { get; set; }

    /// <summary>Projeto novo pelo modelo padrão do Revit.</summary>
    public bool Novo { get; set; }

    public DadosProjeto? Dados { get; set; }

    /// <summary>Vincula modelos que já existem (origem interna com origem interna), logo depois de abrir.</summary>
    public PedidoVinculos? Vincular { get; set; }
    public List<AjusteMapeamento>? Mapeamento { get; set; }
    public bool RedeExemplo { get; set; }

    /// <summary>Cenário da Rede de exemplo pelo nome (ou começo do nome). Grava os critérios dele se não houver "dados".</summary>
    public string? Exemplo { get; set; }
    public PedidoCalculo? Calcular { get; set; }
    public bool Folhas { get; set; }

    /// <summary>Estraga a rede de propósito antes de verificar: "desligarBico", "apagarTe", "sistemaAgua", "desligarTubo".</summary>
    public List<string>? Sabotar { get; set; }

    public bool Verificar { get; set; }

    /// <summary>Cena montada do zero para testar uma ferramenta de modelagem (ver Cenas): "ramalComBicosSoltos".</summary>
    public string? Cena { get; set; }

    /// <summary>Roda Traçar ramais com os bicos da cena e o primeiro tubo dela como subgeral, com as opções padrão.</summary>
    public bool TracarRamal { get; set; }

    /// <summary>Roda Traçar ramais com estas opções (altura, alturaM, toleranciaFileiraM, ligarBicos).</summary>
    public OpcoesRamais? Ramais { get; set; }

    /// <summary>Roda Ligar bicos com os bicos e ramais da cena (sem cena: todos os sprinklers e tubos).</summary>
    public bool LigarBicos { get; set; }

    /// <summary>Diâmetros por tabela no setor pedido (como "calcular"), depois das ferramentas de modelagem.</summary>
    public PedidoCalculo? Diametros { get; set; }

    /// <summary>Lista os conectores de tubos e peças antes e depois de "diametros" (diagnóstico).</summary>
    public bool ListarConexoes { get; set; }

    /// <summary>Liga as peças encostadas (o Corrigir de Verificar modelo), depois de sabotar e antes de verificar.</summary>
    public bool Reconectar { get; set; }
    public bool InspecionarCarimbo { get; set; }

    /// <summary>Lista os vínculos (Revit, IFC, DWG) e o que há em cada um: ambientes, forros, lajes, vigas...</summary>
    public bool InspecionarVinculos { get; set; }

    /// <summary>Lê a arquitetura e a estrutura (ver LeituraDoEdificio) e põe uma linha por ambiente e por viga no relatório.</summary>
    public bool Arquitetura { get; set; }

    /// <summary>Bicos por ambiente em todos os ambientes, com estas opções; depois confere os bicos que ficaram.</summary>
    public OpcoesBicos? BicosPorAmbiente { get; set; }

    /// <summary>Gerar rede em todos os ambientes da arquitetura, com estas opções.</summary>
    public OpcoesRede? GerarRede { get; set; }

    /// <summary>Numeração e código das folhas a gravar no padrão antes de gerar.</summary>
    public AjustePadrao? Padrao { get; set; }
    public Esperado? Esperado { get; set; }
}

sealed class PedidoVinculos
{
    public List<string> Arquivos { get; set; } = [];

    /// <summary>Apaga antes a rede da cópia (bicos, tubos, conexões, acessórios).</summary>
    public bool ApagarRede { get; set; } = true;

    /// <summary>Cria no projeto os níveis dos vínculos que ele não tem, cada um com uma planta.</summary>
    public bool CopiarNiveis { get; set; } = true;
}

sealed class AjustePadrao
{
    public string? NumeroInicial { get; set; }
    public string? CodigoFolha { get; set; }
    public string? ParametroCodigo { get; set; }
}

/// <summary>O que o cenário deve dar. Campo nulo não é conferido.</summary>
sealed class Esperado
{
    public bool? Atende { get; set; }
    public int? Bicos { get; set; }
    public double? PressaoKPa { get; set; }
    public string? Critico { get; set; }
    public int? Folhas { get; set; }

    /// <summary>Código do grupo (ou "erros", "avisos", "setores") e a condição: "0", "&gt;=1", "&lt;=2".</summary>
    public Dictionary<string, string>? Verificacao { get; set; }

    public int? RamaisTracados { get; set; }
    public int? BicosLigados { get; set; }
    public int? BicosRecusados { get; set; }

    /// <summary>Quantos tubos ficaram com cada DN ("25": 4); sem recusa nem divergência.</summary>
    public Dictionary<string, int>? Diametros { get; set; }

    /// <summary>A reconexão não pode desfazer ligação que existia: ligações depois ≥ antes + feitas.</summary>
    public bool? ReconexaoSemPerda { get; set; }

    /// <summary>Trechos que precisam aparecer no texto de alguma das folhas geradas.</summary>
    public List<string>? TextoDasFolhas { get; set; }

    /// <summary>Trechos que precisam aparecer em alguma linha da leitura da arquitetura.</summary>
    public List<string>? TextoDaArquitetura { get; set; }

    /// <summary>Trechos que precisam aparecer em alguma linha de Bicos por ambiente (posicionar | ... e conferir | ...).</summary>
    public List<string>? TextoDosBicos { get; set; }

    /// <summary>Trechos que precisam aparecer em alguma linha de Gerar rede (uma por ambiente).</summary>
    public List<string>? TextoDaRede { get; set; }
}

sealed class AjusteMapeamento
{
    public string Familia { get; set; } = "";
    public string? Tipo { get; set; }
    public string? Papel { get; set; }
    public double? FatorK { get; set; }
    public double? Area { get; set; }
}

sealed class PedidoCalculo
{
    /// <summary>Id da VGA (Fonte) do setor.</summary>
    public long? Vga { get; set; }

    /// <summary>Sem VGA: id de um bico ou tubo do setor.</summary>
    public long? Elemento { get; set; }

    /// <summary>Sem VGA: o setor pela ordem, 0 = o de mais bicos.</summary>
    public int? Setor { get; set; }
}

sealed class RelatorioCenario
{
    public string Nome { get; set; } = "";
    public bool Ok { get; set; }
    public string? Erro { get; set; }
    public List<EtapaExecutada> Etapas { get; set; } = [];
    public List<VgaLida>? Vgas { get; set; }
    public ResumoCalculo? Calculo { get; set; }
    public ResumoFolhas? Folhas { get; set; }
    public List<string>? Dialogos { get; set; }
    public string? Arquivo { get; set; }
    public ResumoVerificacao? Verificacao { get; set; }
    public ResumoReconexao? Reconexao { get; set; }
    public ResumoLigacao? Ligacao { get; set; }
    public ResumoDiametros? Diametros { get; set; }
    public List<string>? ConexoesAntes { get; set; }
    public List<string>? Conexoes { get; set; }
    public List<string>? Carimbos { get; set; }
    public List<string>? Vinculos { get; set; }
    public List<string>? Arquitetura { get; set; }
    public List<string>? Bicos { get; set; }
    public List<string>? Rede { get; set; }
    public List<string>? CarimbosSpk { get; set; }

    /// <summary>Só com "esperado" no cenário: se tudo bateu, e o que não bateu.</summary>
    public bool? Passou { get; set; }
    public List<string>? Divergencias { get; set; }
}

sealed class ResumoVerificacao
{
    public int Bicos { get; set; }
    public int Tubos { get; set; }
    public int Pecas { get; set; }
    public int Setores { get; set; }
    public int Erros { get; set; }
    public int Avisos { get; set; }
    public List<GrupoVerificado> Grupos { get; set; } = [];
}

sealed class ResumoDiametros
{
    public int Mudados { get; set; }
    public int JaCertos { get; set; }
    public Dictionary<string, int> TubosPorDn { get; set; } = [];
    public List<string> Recusados { get; set; } = [];
    public List<string> Divergentes { get; set; } = [];
    public List<string> Inseridas { get; set; } = [];
    public List<string> Avisos { get; set; } = [];
}

sealed class ResumoLigacao
{
    public int? Ramais { get; set; }
    public int Ligados { get; set; }
    public int Criados { get; set; }
    public List<string> Recusados { get; set; } = [];
}

sealed class ResumoReconexao
{
    public int Encostadas { get; set; }
    public int Ligadas { get; set; }
    public int LigacoesAntes { get; set; }
    public int LigacoesDepois { get; set; }
    public List<string> Recusadas { get; set; } = [];
}

sealed class GrupoVerificado
{
    public string Codigo { get; set; } = "";
    public string Gravidade { get; set; } = "";
    public int Quantidade { get; set; }
    public List<string> Exemplos { get; set; } = [];
}

sealed class EtapaExecutada
{
    public string Nome { get; set; } = "";
    public double Segundos { get; set; }
    public string? Texto { get; set; }
    public string? Erro { get; set; }
}

sealed class VgaLida
{
    public long Id { get; set; }
    public string Nome { get; set; } = "";
    public string Familia { get; set; } = "";

    /// <summary>Em metros.</summary>
    public double[]? Ponto { get; set; }

    public List<ConectorLido>? Conectores { get; set; }
}

sealed class ConectorLido
{
    public double[] Ponto { get; set; } = [];
    public double DiametroMm { get; set; }
    public string? LigadoA { get; set; }
    public double? PontaDeTuboMaisProximaM { get; set; }
    public long? TuboDaPonta { get; set; }
    public double? CorpoDeTuboMaisProximoM { get; set; }
    public long? TuboDoCorpo { get; set; }
    public List<string>? Rastreio { get; set; }
}

sealed class ResumoCalculo
{
    public bool Atende { get; set; }
    public string Setor { get; set; } = "";
    public int TotalDeSetores { get; set; }
    public int Bicos { get; set; }
    public int Trechos { get; set; }
    public int AreasAvaliadas { get; set; }
    public double PressaoKPa { get; set; }
    public double VazaoChuveirosLmin { get; set; }
    public double VazaoTotalLmin { get; set; }
    public string Critico { get; set; } = "";
    public double CriticoKPa { get; set; }
    public double CriticoLmin { get; set; }
    public List<string> Falhas { get; set; } = [];
    public List<string> Avisos { get; set; } = [];
    public List<string>? Rede { get; set; }
}

sealed class ResumoFolhas
{
    public List<string> Folhas { get; set; } = [];
    public string? Pdf { get; set; }
    public List<string> Textos { get; set; } = [];
    public List<string>? Vistas { get; set; }
}
