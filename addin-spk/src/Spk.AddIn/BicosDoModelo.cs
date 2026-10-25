namespace Spk.AddIn;

/// <summary>
/// Os bicos que entram na leitura da rede: os da malha gerada, com K e área do esboço, ou os das famílias confirmadas
/// em Famílias. Calcular e as folhas leem pelo mesmo caminho.
/// </summary>
static class BicosDoModelo
{
    public sealed record Leitura(List<LeitorRede.BicoLido> Bicos, string Resumo, bool Gerados);

    public static Leitura Ler(Document doc)
    {
        var gerados = Gerados(doc);
        if (gerados.Count > 0)
        {
            var esboco = DadosEsboco.Carregar(doc);
            var bicos = gerados.Select(fi => new LeitorRede.BicoLido(fi, esboco.FatorK, esboco.AreaM2)).ToList();
            return new Leitura(bicos,
                $"Malha gerada: {bicos.Count} bicos, K {esboco.FatorK:0.##} L/min/bar½, área {esboco.AreaM2:0.##} m².", true);
        }

        var linhas = MapeamentoGravado.Carregar(doc).Where(l => l.EhChuveiroComMedidas).ToList();
        if (linhas.Count == 0)
            throw new InvalidOperationException("Gere uma malha ou confirme em Famílias os bicos com fator K e área maiores que zero.");
        var doMapeamento = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Select(fi => (Instancia: fi, Linha: linhas.FirstOrDefault(l =>
                fi.Symbol?.Family?.Name == l.Familia && fi.Symbol.Name == l.Tipo)))
            .Where(p => p.Linha is not null)
            .Select(p => new LeitorRede.BicoLido(p.Instancia, p.Linha!.FatorK, p.Linha.AreaCobertura))
            .ToList();
        if (doMapeamento.Count == 0)
            throw new InvalidOperationException("O modelo aberto não tem as famílias confirmadas no mapeamento.");
        return new Leitura(doMapeamento,
            string.Join("\n", linhas.Select(l => $"{l.Tipo}: K {l.FatorK:0.##} L/min/bar½, área {l.AreaCobertura:0.##} m²")), false);
    }

    /// <summary>
    /// Os bicos da malha gerada. Só a categoria Sprinklers: o gerador marca também tês e cotovelos com SPK-GERADO, e sem
    /// esse filtro as 15 conexões da malha de exemplo entravam no cálculo como bicos (27 em vez de 12).
    /// </summary>
    public static List<FamilyInstance> Gerados(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Sprinklers)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(DadosEsboco.EhGerado)
            .ToList();
}
