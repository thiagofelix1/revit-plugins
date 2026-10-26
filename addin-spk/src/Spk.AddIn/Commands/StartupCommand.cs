using System.IO;
using System.Text.Json;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Spk.Motor.Calculo;
using Spk.Motor.Exemplos;
using Spk.Motor.Normas;
using Spk.Motor.Relatorios;

namespace Spk.AddIn.Commands;

/// <summary>
///     POC: ainda não lê a rede do modelo do Revit. Calcula o galpão de exemplo do Spk.Motor
///     só para provar a ligação Revit -> Spk.Motor -> TaskDialog antes de ter um .rvt real para ler.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.ReadOnly)]
public class StartupCommand : ExternalCommand
{
    /// <inheritdoc />
    public override void Execute()
    {
        var projeto = new ProjetoSpk("Galpão de exemplo — VGA 1", new GalpaoEspinhaDePeixe().Criar(),
            ClasseRisco.Ordinario2, BaseDeProjeto.PelaFigura42(ClasseRisco.Ordinario2, 140));

        var relatorio = CalculoSpk.Executar(projeto);

        var pasta = PastasSpk.Calculo;
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "memorial.md"), Memorial.Gerar(relatorio));
        File.WriteAllBytes(Path.Combine(pasta, "memorial.docx"), MemorialDocx.Gerar(relatorio));
        File.WriteAllText(Path.Combine(pasta, "resultado.json"), JsonSerializer.Serialize(new
        {
            relatorio.Atende,
            PressaoNecessariaKPa = relatorio.Resultado.PressaoNecessaria,
            VazaoTotalLmin = relatorio.VazaoTotal,
            relatorio.ReservaM3,
            relatorio.Resultado.ChuveiroCritico,
        }, new JsonSerializerOptions { WriteIndented = true }));

        TaskDialog.Show("Spk.AddIn — POC", $"""
            {projeto.Nome}: {(relatorio.Atende ? "ATENDE" : "NÃO ATENDE")}

            Pressão na fonte: {relatorio.Resultado.PressaoNecessaria:0.0} kPa
            Vazão total: {relatorio.VazaoTotal:0} L/min
            Reserva: {relatorio.ReservaM3:0.0} m³
            Chuveiro crítico: {relatorio.Resultado.ChuveiroCritico}

            Memorial e resultado gravados em:
            {pasta}
            """);
    }
}
