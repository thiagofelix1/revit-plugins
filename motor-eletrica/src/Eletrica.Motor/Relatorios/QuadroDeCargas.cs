using System.Text;
using Eletrica.Motor.Calculo;

namespace Eletrica.Motor.Relatorios;

/// <summary>Quadro de cargas em Markdown, uma linha por circuito, no formato que vai para a prancha.</summary>
public static class QuadroDeCargas
{
    public static string Tabela(IEnumerable<ResultadoCircuito> circuitos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Circ. | Descrição | Ligação | Potência (VA) | IB (A) | Fase / N / PE (mm²) | Disjuntor | DR | Queda (%) | Situação |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var c in circuitos)
        {
            var dr = c.DR.Obrigatorio ? $"{Memorial.F(c.DR.CorrenteResidualMA, 0)} mA {c.DR.Tipo}" : "—";
            var situacao = c.Atende ? "atende" : "**" + string.Join("; ", c.Reprovados.Select(p => p.Criterio)) + "**";
            sb.AppendLine($"| {c.Numero} | {c.Descricao} | {Memorial.Ligacao(c.Ligacao)} | {Memorial.F(c.PotenciaVA, 0)} | {Memorial.F(c.CorrenteDeProjeto, 1)} | " +
                          $"{Memorial.F(c.SecaoFase, 1)} / {Memorial.F(c.SecaoNeutro, 1)} / {Memorial.F(c.SecaoPE, 1)} | {c.Disjuntor} | {dr} | {Memorial.F(c.QuedaTotal, 2)} | {situacao} |");
        }
        return sb.ToString();
    }
}
