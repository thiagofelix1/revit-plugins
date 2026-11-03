namespace Spk.AddIn;

/// <summary>
/// Os bicos que entram na leitura da rede: os da malha gerada, com K e área do esboço, ou os das famílias confirmadas
/// em Famílias. Calcular e as folhas leem pelo mesmo caminho.
/// </summary>
static class BicosDoModelo
{
    /// <param name="AvisoDoFatorK">Os bicos entram com o K de Dados do esboço, não com o da família deles.</param>
    public sealed record Leitura(List<LeitorRede.BicoLido> Bicos, string Resumo, bool Gerados, string? AvisoDoFatorK = null);

    public static Leitura Ler(Document doc)
    {
        var gerados = Gerados(doc);
        if (gerados.Count > 0)
        {
            var esboco = DadosEsboco.Carregar(doc);
            var bicos = gerados.Select(fi => new LeitorRede.BicoLido(fi, esboco.FatorK, esboco.AreaM2)).ToList();
            return new Leitura(bicos,
                $"Malha gerada: {bicos.Count} bicos, K {esboco.FatorK:0.##} L/min/bar½, área {esboco.AreaM2:0.##} m².", true, AvisoDoFatorK(SemConfirmar(doc).Count, esboco));
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
    /// Bicos do Gerar rede e de Bicos por ambiente com família sem confirmar: entram com o K de Dados do esboço sem o
    /// projetista ter escolhido esse número. Na Malha retangular e na Rede de exemplo ele escolheu, e o bico leva a marca
    /// também no parâmetro Marca.
    /// </summary>
    public static List<FamilyInstance> SemConfirmar(Document doc) =>
        Gerados(doc).Where(b => b.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() != DadosEsboco.Marca).ToList();

    /// <summary>O aviso de que os números dependem de um K que ninguém confirmou para o bico real.</summary>
    public static string? AvisoDoFatorK(int bicos, DadosEsboco esboco) => bicos == 0 ? null :
        $"ATENÇÃO: {bicos} bico(s) calculados com K {esboco.FatorK:0.##} e área {esboco.AreaM2:0.##} m² de Dados do esboço, não com os da família deles. " +
        "Se o bico real for outro, a pressão e a vazão não valem: confirme o bico em Famílias e calcule de novo.";

    /// <summary>
    /// Bico gerado antes de a família ser confirmada: tira a marca da malha gerada para ele passar a valer pelo mapeamento.
    /// Sem isso, confirmar em Famílias não mudava nada, porque o bico marcado continuava com o K de Dados do esboço.
    /// </summary>
    public static int SoltarConfirmados(Document doc)
    {
        var confirmados = MapeamentoGravado.Carregar(doc).Where(l => l.EhChuveiroComMedidas).Select(l => (l.Familia, l.Tipo)).ToHashSet();
        var soltar = Gerados(doc).Where(b => confirmados.Contains((b.Symbol?.Family?.Name ?? "", b.Symbol?.Name ?? ""))).ToList();
        if (soltar.Count == 0) return 0;
        using var t = new Transaction(doc, "SPK — Bicos confirmados");
        t.Start();
        foreach (var bico in soltar)
            foreach (var parametro in new[] { BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, BuiltInParameter.ALL_MODEL_MARK })
                if (bico.get_Parameter(parametro) is { IsReadOnly: false } p && p.AsString() == DadosEsboco.Marca) p.Set("");
        t.Commit();
        return soltar.Count;
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
