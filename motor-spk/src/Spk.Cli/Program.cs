using System.Diagnostics;
using System.Text.Json;
using Spk.Cli;
using Spk.Motor.Calculo;
using Spk.Motor.Exemplos;
using Spk.Motor.Modelo;
using Spk.Motor.Normas;
using Spk.Motor.Relatorios;

const string Uso = """
    Uso:
      spk exemplo <arquivo.json>            gera um projeto de exemplo (galpão em espinha de peixe)
      spk calcular <arquivo.json> [pasta]   calcula e grava memorial.md e resultado.json
    """;

try
{
    switch (args)
    {
        case ["exemplo", var destino]:
            GerarExemplo(destino);
            return 0;
        case ["calcular", var origem]:
            return Calcular(origem, Path.GetDirectoryName(Path.GetFullPath(origem))!);
        case ["calcular", var origem, var pasta]:
            return Calcular(origem, pasta);
        default:
            Console.Error.WriteLine(Uso);
            return 2;
    }
}
catch (Exception e) when (e is ErroDeModelo or ArgumentException or InvalidOperationException or JsonException or IOException)
{
    Console.Error.WriteLine("Erro: " + e.Message);
    return 1;
}

static void GerarExemplo(string destino)
{
    var arquivo = ArquivoProjeto.DeRede("Galpão de exemplo — VGA 1", ClasseRisco.Ordinario2,
        new ArquivoBase { Tipo = "figura42", Area = 140 }, new GalpaoEspinhaDePeixe().Criar());
    arquivo.Abastecimento = new ArquivoAbastecimento
    {
        Descricao = "Bomba de exemplo",
        Pontos = [new() { Vazao = 0, Pressao = 900 }, new() { Vazao = 3000, Pressao = 780 }, new() { Vazao = 4500, Pressao = 600 }],
    };
    File.WriteAllText(destino, JsonSerializer.Serialize(arquivo, ArquivoProjeto.Json));
    Console.WriteLine($"Exemplo gravado em {destino}: {arquivo.Rede.Chuveiros.Count} chuveiros, {arquivo.Rede.Trechos.Count} trechos.");
}

static int Calcular(string origem, string pasta)
{
    var arquivo = JsonSerializer.Deserialize<ArquivoProjeto>(File.ReadAllText(origem), ArquivoProjeto.Json)
                  ?? throw new JsonException("Arquivo vazio.");
    var projeto = arquivo.ParaProjeto();

    var cronometro = Stopwatch.StartNew();
    var rel = CalculoSpk.Executar(projeto);
    cronometro.Stop();

    Directory.CreateDirectory(pasta);
    File.WriteAllText(Path.Combine(pasta, "memorial.md"), Memorial.Gerar(rel));
    File.WriteAllText(Path.Combine(pasta, "resultado.json"), JsonSerializer.Serialize(new
    {
        rel.Atende,
        PressaoNecessariaKPa = rel.Resultado.PressaoNecessaria,
        VazaoChuveirosLmin = rel.Resultado.VazaoChuveiros,
        VazaoTotalLmin = rel.VazaoTotal,
        PressaoDisponivelKPa = rel.PressaoDisponivel,
        rel.ReservaM3,
        rel.Resultado.ChuveiroCritico,
        CandidatosAvaliados = rel.Busca?.CandidatosAvaliados,
        rel.Verificacoes,
        rel.Avisos,
        rel.Resultado.Chuveiros,
        rel.Resultado.Trechos,
    }, ArquivoProjeto.Json));

    Console.WriteLine($"{projeto.Nome}: {(rel.Atende ? "ATENDE" : "NÃO ATENDE")}");
    Console.WriteLine($"  pressão na fonte {rel.Resultado.PressaoNecessaria:0.0} kPa; vazão total {rel.VazaoTotal:0} L/min; reserva {rel.ReservaM3:0.0} m³");
    Console.WriteLine($"  chuveiro crítico {rel.Resultado.ChuveiroCritico}; {rel.Busca?.CandidatosAvaliados ?? 1} áreas avaliadas em {cronometro.ElapsedMilliseconds} ms");
    foreach (var v in rel.Verificacoes.Where(v => !v.Atende)) Console.WriteLine($"  NÃO ATENDE: {v.Descricao} ({v.Item}) — {v.Detalhe}");
    Console.WriteLine($"  memorial em {Path.Combine(pasta, "memorial.md")}");
    return rel.Atende ? 0 : 3;
}
