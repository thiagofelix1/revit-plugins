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
            }).ToList();

            if (cenario.RedeExemplo)
                Etapa(relatorio, "redeExemplo", () => RedeExemploCommand.Criar(doc!).Resumo);

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
        if (pedido.Vga is { } idVga)
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
