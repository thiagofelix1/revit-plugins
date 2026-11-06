using System.Text.Json;
using Eletrica.Cli;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Exemplos;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Relatorios;

const string Uso = """
    Uso:
      eletrica exemplo <arquivo.json>            grava a instalação de exemplo (apartamento de 2 dormitórios)
      eletrica calcular <arquivo.json> [pasta]   verifica, dimensiona e grava memorial (.md e .docx), manual do usuário, lista de material, unifilar e resultado.json
      eletrica verificar <arquivo.json>          só o Verificar modelo, no console
    """;

try
{
    switch (args)
    {
        case ["exemplo", var destino]:
            File.WriteAllText(destino, JsonSerializer.Serialize(ArquivoInstalacao.De(new ApartamentoExemplo().Criar()), ArquivoInstalacao.Json));
            Console.WriteLine($"Exemplo gravado em {destino}.");
            return 0;
        case ["calcular", var origem]:
            return Calcular(origem, Path.GetDirectoryName(Path.GetFullPath(origem))!);
        case ["calcular", var origem, var pasta]:
            return Calcular(origem, pasta);
        case ["verificar", var origem]:
            return Verificar(origem);
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

static Instalacao Ler(string origem) =>
    (JsonSerializer.Deserialize<ArquivoInstalacao>(File.ReadAllText(origem), ArquivoInstalacao.Json) ?? throw new JsonException("Arquivo vazio.")).ParaInstalacao();

static int Calcular(string origem, string pasta)
{
    var instalacao = Ler(origem);
    var resultado = CalculoEletrica.Executar(instalacao);
    Directory.CreateDirectory(pasta);
    File.WriteAllText(Path.Combine(pasta, "memorial.md"), Memorial.Gerar(resultado));
    File.WriteAllBytes(Path.Combine(pasta, "memorial.docx"), MemorialDocx.Gerar(resultado));
    File.WriteAllText(Path.Combine(pasta, "manual-do-usuario.md"), ManualDoUsuario.GerarMarkdown(resultado));
    File.WriteAllBytes(Path.Combine(pasta, "manual-do-usuario.docx"), ManualDoUsuario.GerarDocx(resultado));
    var lista = ListaDeMaterial.Gerar(instalacao, resultado);
    File.WriteAllText(Path.Combine(pasta, "lista-de-material.md"), ListaDeMaterial.Markdown(lista, resultado.Nome));
    File.WriteAllText(Path.Combine(pasta, "lista-de-material.csv"), ListaDeMaterial.Csv(lista));
    File.WriteAllText(Path.Combine(pasta, "unifilar.txt"), Unifilar.Texto(Unifilar.Montar(instalacao, resultado)));
    File.WriteAllText(Path.Combine(pasta, "resultado.json"), JsonSerializer.Serialize(resultado, ArquivoInstalacao.Json));
    Console.WriteLine($"{resultado.Nome}: {(resultado.Atende ? "ATENDE" : "NÃO ATENDE")}; {resultado.Circuitos.Count} circuitos, " +
                      $"{resultado.Problemas.Count(p => p.Gravidade == Gravidade.Erro)} erro(s), {resultado.Problemas.Count(p => p.Gravidade == Gravidade.Aviso)} aviso(s), em {resultado.Duracao.TotalMilliseconds:0} ms.");
    foreach (var c in resultado.Circuitos)
        Console.WriteLine($"  C{c.Numero} {c.Descricao}: IB {c.CorrenteDeProjeto:0.0} A, {c.SecaoFase} mm², {c.Disjuntor}, queda {c.QuedaTotal:0.00} % {(c.Atende ? "" : "— NÃO ATENDE: " + string.Join("; ", c.Reprovados.Select(p => p.Criterio)))}");
    Console.WriteLine($"Memorial em {Path.Combine(pasta, "memorial.md")}");
    return resultado.Atende ? 0 : 3;
}

static int Verificar(string origem)
{
    var instalacao = Ler(origem);
    var problemas = Verificacao.Verificar(instalacao);
    foreach (var p in problemas.OrderBy(p => p.Gravidade))
        Console.WriteLine($"[{p.Gravidade}] {p.Codigo} {p.Mensagem} ({p.Item})");
    Console.WriteLine($"{problemas.Count(p => p.Gravidade == Gravidade.Erro)} erro(s), {problemas.Count(p => p.Gravidade == Gravidade.Aviso)} aviso(s), {problemas.Count(p => p.Gravidade == Gravidade.Nota)} nota(s).");
    return problemas.Any(p => p.Gravidade == Gravidade.Erro) ? 3 : 0;
}
