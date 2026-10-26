using Spk.AddIn.Mapeamento;

namespace Spk.AddIn.PreviewUi;

/// <summary>
///     Roda a janela de confirmação de mapeamento com dados fictícios, pra testar a UI sem precisar
///     do Revit instalado nem de um .rvt real. Uso: dotnet run --project addin-spk/src/Spk.AddIn.PreviewUi
/// </summary>
internal static class Program
{
    [STAThread]
    static void Main()
    {
        var candidatos = new List<CandidatoFamilia>
        {
            CandidatoFamilia.Criar("Dispositivos de Proteção contra Incêndio", "Chuveiro Automático ESFR", "K80", 48,
                ["Fator K", "Área de Cobertura", "Marca", "Comentários"],
                parametroFatorK: "Fator K", valorFatorK: "360",
                parametroAreaCobertura: "Área de Cobertura", valorAreaCobertura: "9"),
            CandidatoFamilia.Criar("Equipamentos Mecânicos", "Sprinkler_Upright_Generic", "Standard", 12,
                ["K-Factor", "Coverage Area", "Manufacturer"],
                parametroFatorK: "K-Factor", valorFatorK: "5,6",
                parametroAreaCobertura: "Coverage Area", valorAreaCobertura: "12"),
            CandidatoFamilia.Criar("Sprinklers", "Sprinkler-Tyco-Pendent", "BICO SPK TY 32", 351,
                ["Área", "Comentários", "Marca"]),
            CandidatoFamilia.Criar("Tubulação — Acessório", "VGA — Válvula de Governo e Alarme", "DN100", 1,
                ["Pressão Nominal", "Diâmetro", "Fabricante"], PapelFamilia.Fonte),
        };

        var janela = new JanelaMapeamento(candidatos);
        var confirmado = janela.ShowDialog();

        Console.WriteLine(confirmado == true ? "Confirmado:" : "Cancelado.");
        if (janela.Resultado is { } resultado)
            foreach (var c in resultado)
                Console.WriteLine($"  {c.Familia} / {c.Tipo}: papel={c.Papel}, K='{c.ValorFatorK}' ({c.ParametroFatorK}), área='{c.ValorAreaCobertura}' ({c.ParametroAreaCobertura})");
    }
}
