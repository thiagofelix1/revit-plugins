using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Fiacao;

/// <summary>
///     Legenda de fiação (itens 6.3 e 6.5 do plano): uma vista de desenho "AE — Legenda de fiação" com a tabela dos
///     circuitos (número, descrição, quadro, condutores e seções, disjuntor, DR) e as cores dos condutores do padrão
///     (6.1.5.3: neutro azul-claro, PE verde-amarelo). A API não cria vista de legenda, então é vista de desenho com
///     texto e linhas; vai para a prancha como qualquer vista. Rodar de novo refaz a vista.
/// </summary>
static class LegendaDeFiacao
{
    public const string NomeDaVista = "AE — Legenda de fiação";

    /// <summary>Colunas em mm no papel.</summary>
    static readonly (string Titulo, double Largura)[] Colunas =
    [
        ("Circuito", 18), ("Descrição", 58), ("Quadro", 22), ("Fiação (mm²)", 36), ("Disjuntor", 24), ("DR", 24),
    ];

    const double AlturaLinhaMm = 6;
    const double MargemMm = 1.2;
    const int Escala = 50;

    public static ViewDrafting Gerar(Document doc, ResultadoInstalacao r, Instalacao i, PadraoEletrica padrao)
    {
        var tipoVista = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(t => t.ViewFamily == ViewFamily.Drafting);
        var tipoTexto = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().OrderBy(t => t.Name).FirstOrDefault()
                        ?? throw new InvalidOperationException("O projeto não tem tipo de texto.");

        using var transacao = new Transaction(doc, "AutoEletrica — Legenda de fiação");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);
        var antiga = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>().FirstOrDefault(v => v.Name == NomeDaVista);
        if (antiga is not null) doc.Delete(antiga.Id);
        var vista = ViewDrafting.Create(doc, tipoVista.Id);
        vista.Name = NomeDaVista;
        vista.Scale = Escala;

        double Mm(double mm) => UnitUtils.ConvertToInternalUnits(mm * Escala, UnitTypeId.Millimeters);
        var largura = Colunas.Sum(c => c.Largura);
        var y = 0.0;
        var opcoes = new TextNoteOptions(tipoTexto.Id) { HorizontalAlignment = HorizontalTextAlignment.Left };

        void Texto(double xMm, double yMm, string texto, double larguraMm)
        {
            if (string.IsNullOrEmpty(texto)) return;
            var nota = TextNote.Create(doc, vista.Id, new XYZ(Mm(xMm + MargemMm), Mm(yMm - MargemMm), 0), Mm(Math.Max(larguraMm - 2 * MargemMm, 4)), texto, opcoes);
            _ = nota;
        }

        void Linha(double x1, double y1, double x2, double y2) =>
            doc.Create.NewDetailCurve(vista, Autodesk.Revit.DB.Line.CreateBound(new XYZ(Mm(x1), Mm(y1), 0), new XYZ(Mm(x2), Mm(y2), 0)));

        void LinhaDaTabela(IReadOnlyList<string> celulas, double yTopo)
        {
            var x = 0.0;
            for (var k = 0; k < Colunas.Length; k++)
            {
                Texto(x, yTopo, k < celulas.Count ? celulas[k] : "", Colunas[k].Largura);
                x += Colunas[k].Largura;
            }
            Linha(0, yTopo - AlturaLinhaMm, largura, yTopo - AlturaLinhaMm);
        }

        // Título e cabeçalho
        Texto(0, y, $"LEGENDA DE FIAÇÃO — {r.Nome}", largura);
        y -= AlturaLinhaMm * 1.4;
        Texto(0, y, $"{i.Alimentacao.TensaoFaseNeutro:0}/{i.Alimentacao.TensaoFaseFase:0} V, {i.Alimentacao.Fases}F, {DadosProjeto.NomeEsquema(i.Alimentacao.Esquema)}. " +
                    $"Condutores: fases {padrao.CorFases}; neutro {padrao.CorNeutro}; proteção {padrao.CorPE}; retorno {padrao.CorRetorno} (NBR 5410 6.1.5.3).", largura);
        y -= AlturaLinhaMm * 1.6;
        Linha(0, y, largura, y);
        LinhaDaTabela(Colunas.Select(c => c.Titulo).ToList(), y);
        y -= AlturaLinhaMm;

        foreach (var q in r.Quadros.OrderBy(q => q.Nome))
        foreach (var c in r.Circuitos.Where(c => c.Quadro == q.Id).OrderBy(c => c.Numero, StringComparer.OrdinalIgnoreCase))
        {
            var temNeutro = c.Ligacao is Ligacao.FN or Ligacao.FFN or Ligacao.FFFN;
            var fases = c.Ligacao switch { Ligacao.FN => 1, Ligacao.FF or Ligacao.FFN => 2, _ => 3 };
            var vivos = fases + (temNeutro ? 1 : 0);
            var fiacao = $"{vivos}#{F(c.SecaoFase)}({F(c.SecaoPE)})" + (temNeutro && Math.Abs(c.SecaoNeutro - c.SecaoFase) > 1e-9 ? $" N {F(c.SecaoNeutro)}" : "");
            LinhaDaTabela([c.Numero, c.Descricao, q.Nome, fiacao, c.Disjuntor.ToString(), c.DR.Obrigatorio ? $"{c.DR.CorrenteResidualMA:0} mA {c.DR.Tipo}" : "—"], y);
            y -= AlturaLinhaMm;
        }

        // Bordas verticais
        var xb = 0.0;
        Linha(0, 0 - AlturaLinhaMm * 3, 0, y);
        foreach (var col in Colunas)
        {
            xb += col.Largura;
            Linha(xb, 0 - AlturaLinhaMm * 3, xb, y);
        }
        y -= AlturaLinhaMm * 0.6;
        Texto(0, y, "n#S(PE): n condutores vivos de S mm² e proteção de PE mm². Fiação por trecho nas etiquetas dos eletrodutos (AE Fiação). " +
                    "Gerada pelo AutoEletrica a partir do cálculo pela NBR 5410; ver memorial.", largura);
        transacao.Commit();
        return vista;
    }

    static string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
