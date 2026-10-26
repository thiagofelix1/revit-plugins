using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Spk.AddIn.Commands;
using Spk.AddIn.Servicos;

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

            relatorio.Vgas = MapeamentoGravado.Vgas(doc!).Select(v => new VgaLida
            {
                Id = v.Id.Value,
                Nome = LeitorRede.NomeDaVga(v),
                Familia = $"{v.Symbol?.Family?.Name} : {v.Symbol?.Name}",
                Ponto = (v.Location as LocationPoint)?.Point is { } p ? [Math.Round(p.X * 0.3048, 2), Math.Round(p.Y * 0.3048, 2), Math.Round(p.Z * 0.3048, 2)] : null,
                Conectores = Conectores(doc!, v),
            }).ToList();

            if (cenario.RedeExemplo || cenario.Exemplo is not null)
                Etapa(relatorio, "redeExemplo", () =>
                {
                    var escolhido = cenario.Exemplo is { } nome
                        ? CenariosExemplo.PorNome(nome) ?? throw new InvalidOperationException($"Não há cenário de exemplo \"{nome}\".")
                        : CenariosExemplo.Simples;
                    return RedeExemploCommand.Criar(doc!, escolhido, gravarCriterios: cenario.Dados is null).Resumo;
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
                    };
                    return folhas.Texto();
                });

            Etapa(relatorio, "salvar", () =>
            {
                if (cenario.Novo) doc!.SaveAs(arquivo, new SaveAsOptions { OverwriteExistingFile = true });
                else doc!.Save();
                return arquivo;
            });
            relatorio.Arquivo = arquivo;
            relatorio.Ok = true;
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
        return ServicoCalculo.Executar(doc, dados, leitura, fonte, vgas.Select(v => v.Id).ToList(), doSetor, pedido.Setor);
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
        };
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

    static string Seguro(string nome) => string.Concat(nome.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
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
    public List<AjusteMapeamento>? Mapeamento { get; set; }
    public bool RedeExemplo { get; set; }

    /// <summary>Cenário da Rede de exemplo pelo nome (ou começo do nome). Grava os critérios dele se não houver "dados".</summary>
    public string? Exemplo { get; set; }
    public PedidoCalculo? Calcular { get; set; }
    public bool Folhas { get; set; }
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
}

sealed class ResumoFolhas
{
    public List<string> Folhas { get; set; } = [];
    public string? Pdf { get; set; }
    public List<string> Textos { get; set; } = [];
}
