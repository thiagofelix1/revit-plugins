namespace Spk.AddIn.Ajuda;

/// <summary>Um botão de uma etapa: o texto e a classe do comando que ele abre.</summary>
public sealed record AcaoDaEtapa(string Texto, string Comando);

/// <summary>Uma linha de "Seu projeto agora".</summary>
public sealed record Etapa(string Titulo, string Estado, string Detalhe, IReadOnlyList<AcaoDaEtapa> Acoes)
{
    public const string Feito = "Feito";
    public const string Falta = "Falta";
    public const string Opcional = "Opcional";

    public string Cor => Estado switch
    {
        Feito => "#2E7D32",
        Falta => "#C62828",
        _ => "#78909C",
    };
}

/// <summary>Lê o que já está gravado no projeto aberto e diz, etapa por etapa, o que foi feito e o que falta.</summary>
static class SituacaoDoProjeto
{
    public static List<Etapa> Ler(Document? doc)
    {
        if (doc is null || doc.IsFamilyDocument)
            return [new Etapa("Projeto", Etapa.Falta, "Abra o projeto (.rvt) onde está ou vai estar a rede de sprinklers.", [])];

        return [Padrao(doc), Dados(doc), Rede(doc), Calculo(doc), Folhas(doc)];
    }

    static Etapa Padrao(Document doc)
    {
        var padrao = PadraoEscritorio.Carregar(doc);
        var acoes = new[] { new AcaoDaEtapa("Padrão do escritório", "ImportarPadraoCommand") };
        return padrao.Origem is null && padrao.Carimbo is null
            ? new Etapa("1. Padrão do escritório", Etapa.Opcional,
                "Não importado. As folhas saem com o primeiro carimbo do projeto e o estilo do SPK.", acoes)
            : new Etapa("1. Padrão do escritório", Etapa.Feito, padrao.Resumo(), acoes);
    }

    static Etapa Dados(Document doc)
    {
        var acoes = new[] { new AcaoDaEtapa("Dados do projeto", "DadosProjetoCommand") };
        return DadosProjeto.Carregar(doc) is { } dados
            ? new Etapa("2. Dados do projeto", Etapa.Feito, dados.Resumo(), acoes)
            : new Etapa("2. Dados do projeto", Etapa.Falta,
                "Ainda não preenchidos. Sem a classe de risco e a base de cálculo, o SPK não calcula.", acoes);
    }

    static Etapa Rede(Document doc)
    {
        var acoes = new[]
        {
            new AcaoDaEtapa("Famílias", "DescobrirFamiliasCommand"),
            new AcaoDaEtapa("Verificar modelo", "VerificarModeloCommand"),
            new AcaoDaEtapa("Gerar", "GerarMalhaCommand"),
            new AcaoDaEtapa("Rede de exemplo", "RedeExemploCommand"),
        };
        var gerados = BicosDoModelo.Gerados(doc).Count;
        if (gerados > 0)
            return ComVerificacao(doc, new Etapa("3. Rede", Etapa.Feito, $"Malha gerada pelo SPK: {gerados} bicos.", acoes));

        var mapeamento = MapeamentoGravado.Tentar(doc);
        if (mapeamento is null)
            return new Etapa("3. Rede", Etapa.Falta,
                "Nem malha gerada nem famílias confirmadas. Para uma rede já desenhada, confirme os bicos e as VGAs em Famílias; " +
                "para começar do zero, use Gerar ou Rede de exemplo.", acoes);

        var bicos = mapeamento.Count(l => l.EhChuveiroComMedidas);
        var vgas = MapeamentoGravado.Vgas(doc).Count;
        var detalhe = $"Famílias confirmadas: {bicos} tipo(s) de bico com fator K e área, {vgas} VGA(s) no modelo.";
        if (bicos == 0) return new Etapa("3. Rede", Etapa.Falta, detalhe + " Falta um bico com fator K e área maiores que zero.", acoes);
        return ComVerificacao(doc, new Etapa("3. Rede", Etapa.Feito,
            vgas == 0 ? detalhe + " Sem VGA, o Calcular pergunta o setor clicando num bico." : detalhe, acoes));
    }

    /// <summary>
    /// Acrescenta o resultado de Verificar modelo (rápido: menos de 1 s na Nova Era). Com erro de modelagem a etapa volta
    /// para Falta: o cálculo dependeria de ligação por proximidade.
    /// </summary>
    static Etapa ComVerificacao(Document doc, Etapa etapa)
    {
        try
        {
            var verificacao = Verificacao.VerificacaoModelo.Verificar(doc);
            if (verificacao.Erros == 0 && verificacao.Avisos == 0)
                return etapa with { Detalhe = etapa.Detalhe + " Verificar modelo: nenhum problema." };
            var texto = $" Verificar modelo: {verificacao.Erros} erro(s) e {verificacao.Avisos} aviso(s); abra Verificar modelo para ver onde.";
            return etapa with { Estado = verificacao.Erros > 0 ? Etapa.Falta : etapa.Estado, Detalhe = etapa.Detalhe + texto };
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            return etapa;
        }
    }

    static Etapa Calculo(Document doc)
    {
        var acoes = new[]
        {
            new AcaoDaEtapa("Diâmetros", "DiametrosCommand"),
            new AcaoDaEtapa("Calcular", "CalcularRedeCommand"),
            new AcaoDaEtapa("Memorial", "AbrirMemorialCommand"),
        };
        if (ResultadoGravado.Ler(doc) is not { } resultado)
            return new Etapa("4. Cálculo", Etapa.Falta, "Ainda não calculado neste projeto.", acoes);

        var situacao = resultado.Atende ? "ATENDE" : "NÃO ATENDE";
        return new Etapa("4. Cálculo", resultado.Atende ? Etapa.Feito : Etapa.Falta,
            $"Último cálculo: setor {resultado.Setor}, {situacao}. {resultado.DaArea.Count()} bicos na área, " +
            $"{resultado.PressaoNecessariaKPa:0.0} kPa e {resultado.VazaoTotalLmin:0} L/min na entrada" +
            (resultado.ChuveiroCritico is null ? "." : $", bico crítico {resultado.ChuveiroCritico}."), acoes);
    }

    static Etapa Folhas(Document doc)
    {
        var acoes = new[] { new AcaoDaEtapa("Folhas", "FolhasCommand") };
        var folhas = AddIn.Folhas.CarimboSpk.FolhasDoSpk(doc).Count;
        return folhas == 0
            ? new Etapa("5. Folhas", Etapa.Falta, "Nenhuma folha do SPK neste projeto.", acoes)
            : new Etapa("5. Folhas", Etapa.Feito, $"{folhas} folha(s) do SPK. Rodar de novo refaz todas com os dados atuais.", acoes);
    }
}
