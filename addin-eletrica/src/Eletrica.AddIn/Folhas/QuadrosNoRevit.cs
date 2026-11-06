using Eletrica.AddIn.Leitura;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;
using Eletrica.Motor.Relatorios;

namespace Eletrica.AddIn.Folhas;

/// <summary>
///     Quadro de cargas, demanda e unifilar de cada quadro, e a prumada, em vistas de desenho (itens 9.1, 9.2, 9.3 e 9.5
///     do plano). Refeitas a cada chamada. O quadro de cargas nativo do Revit (PanelScheduleView) também é criado quando o
///     projeto tem o modelo, como alternativa no formato do Revit.
/// </summary>
static class QuadrosNoRevit
{
    public sealed record Feito(IReadOnlyList<View> Vistas, IReadOnlyList<string> Avisos);

    static readonly Desenho.Coluna[] ColunasDoQuadro =
    [
        new("Circ.", 12), new("Descrição", 46), new("Ligação", 16), new("Fase", 10), new("VA", 14), new("IB (A)", 12), new("F / N / PE (mm²)", 26),
        new("Disjuntor", 26), new("DR", 20), new("Queda (%)", 14), new("Situação", 22),
    ];

    /// <summary>
    ///     Nome de cada quadro nas vistas e folhas, sem repetição: modelos de terceiros (o Snowdon) têm quadros com o mesmo nome, e
    ///     a vista do segundo apagava a do primeiro. O segundo em diante leva "(2)", "(3)"...
    /// </summary>
    public static Dictionary<string, string> NomesUnicos(IEnumerable<ResultadoQuadro> quadros)
    {
        var nomes = new Dictionary<string, string>();
        var vezes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in quadros)
        {
            vezes[q.Nome] = vezes.TryGetValue(q.Nome, out var k) ? k + 1 : 1;
            nomes[q.Id] = vezes[q.Nome] == 1 ? q.Nome : $"{q.Nome} ({vezes[q.Nome]})";
        }
        return nomes;
    }

    public static Feito Gerar(Document doc, LeitorInstalacao.Lida lida, ResultadoInstalacao r)
    {
        var avisos = new List<string>();
        var vistas = new List<View>();
        var i = lida.Instalacao;
        using var transacao = new Transaction(doc, "AutoEletrica — Quadros");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);
        var nomes = NomesUnicos(r.Quadros);
        foreach (var q in r.Quadros)
        {
            vistas.Add(QuadroDeCargas(doc, i, r, q, nomes[q.Id]));
            vistas.Add(UnifilarDoQuadro(doc, i, r, q, nomes[q.Id], avisos));
            PainelNativo(doc, lida, q, avisos);
        }
        vistas.Add(Prumada(doc, lida, r, avisos));
        transacao.Commit();
        return new Feito(vistas, avisos);
    }

    static View QuadroDeCargas(Document doc, Instalacao i, ResultadoInstalacao r, ResultadoQuadro q, string nome)
    {
        var vista = Desenho.NovaVista(doc, $"{Desenho.Prefixo}Quadro de cargas — {nome}");
        var y = 0.0;
        Desenho.Texto(doc, vista, 0, y, $"QUADRO DE CARGAS — {q.Nome}", 200, negrito: true);
        y -= 8;
        Desenho.Texto(doc, vista, 0, y, $"{i.Alimentacao.TensaoFaseNeutro:0}/{i.Alimentacao.TensaoFaseFase:0} V, {q.Fases}F, {DadosProjeto.NomeEsquema(i.Alimentacao.Esquema)}. " +
                                      $"Instalada {Memorial.F(q.PotenciaInstaladaVA, 0)} VA, demanda {Memorial.F(q.PotenciaDeDemandaVA, 0)} VA ({Memorial.F(q.CorrenteDeDemanda, 1)} A). " +
                                      (q.DisjuntorGeral is { } g ? $"Geral {g}. " : "") + (q.IccNoBarramentoKA is { } icc ? $"Icc no barramento {Memorial.F(icc, 2)} kA. " : "") +
                                      $"{q.NumeroDeCircuitos} circuitos + {q.Reserva} de reserva (Tabela 59).", 218);
        y -= 12;
        var circuitos = r.Circuitos.Where(c => c.Quadro == q.Id).OrderBy(c => c.Numero.Length).ThenBy(c => c.Numero, StringComparer.OrdinalIgnoreCase).ToList();
        y = Desenho.Tabela(doc, vista, 0, y, null, ColunasDoQuadro, circuitos.Select(c => (IReadOnlyList<string>)
        [
            c.Numero, c.Descricao, Memorial.Ligacao(c.Ligacao), c.Fases, Memorial.F(c.PotenciaVA, 0), Memorial.F(c.CorrenteDeProjeto, 1),
            $"{(c.CondutoresPorFase > 1 ? $"{c.CondutoresPorFase}×" : "")}{Memorial.F(c.SecaoFase, 1)} / {Memorial.F(c.SecaoNeutro, 1)} / {Memorial.F(c.SecaoPE, 1)}",
            c.Disjuntor.ToString(), c.DR.Obrigatorio ? $"{c.DR.CorrenteResidualMA:0} mA {c.DR.Tipo}" : "—", Memorial.F(c.QuedaTotal, 2),
            c.Atende ? "atende" : "NÃO ATENDE",
        ]));

        // Demanda e fases
        if (q.Demanda is { } dem)
        {
            var linhas = dem.Parcelas.Select(p => (IReadOnlyList<string>)[FatoresDeDemanda.Nome(p.Categoria), Memorial.F(p.InstaladaVA, 0), p.Aparelhos.ToString(), Memorial.F(p.Fator, 2), Memorial.F(p.DemandaVA, 0)]).ToList();
            if (dem.DosQuadrosFilhosVA > 0) linhas.Add(["quadros alimentados", Memorial.F(dem.DosQuadrosFilhosVA, 0), "", "1,00", Memorial.F(dem.DosQuadrosFilhosVA, 0)]);
            linhas.Add(["**Total**", Memorial.F(dem.InstaladaVA, 0), "", "", $"**{Memorial.F(dem.DemandaVA, 0)}**"]);
            y = Desenho.Tabela(doc, vista, 0, y - 2, "Demanda (4.2.1.1; fatores: padrão do motor, conferir com a distribuidora)",
                [new("Categoria", 70), new("Instalada (VA)", 26), new("Un.", 10), new("Fator", 14), new("Demanda (VA)", 26)], linhas);
        }
        y = Desenho.Tabela(doc, vista, 0, y - 2, "Cargas por fase (4.2.5.6)",
            q.CargasPorFase.Select(f => new Desenho.Coluna($"Fase {f.Fase}", 30)).Concat([new Desenho.Coluna("Desequilíbrio", 30)]).ToList(),
            [q.CargasPorFase.Select(f => $"{Memorial.F(f.PotenciaVA, 0)} VA / {Memorial.F(f.Corrente, 1)} A").Concat([$"{Memorial.F(q.DesequilibrioPercentual, 0)} %"]).ToList()]);

        // Proteções do quadro
        var protecoes = new List<IReadOnlyList<string>>();
        if (q.DisjuntorGeral is { } geral) protecoes.Add(["Disjuntor geral", geral.ToString(), "5.3.4, 5.6.3"]);
        foreach (var grupo in q.GruposDR ?? []) protecoes.Add([$"DR{grupo.Numero}", $"{grupo.In:0} A / {grupo.CorrenteResidualMA:0} mA tipo {grupo.Tipo} {grupo.Polos}P — circuitos {string.Join(", ", grupo.Circuitos)}", "5.1.3.2.2, 6.3.3.2"]);
        if (q.Dps is { } dps) protecoes.Add(["DPS", $"classe {dps.Classe}, {dps.Quantidade} un., Uc {dps.UcComercial:0} V, Up ≤ {dps.UpMaximo:0.#} kV, In ≥ {dps.InMinimoKA:0} kA{(dps.IimpMinimoKA is { } ii ? $", Iimp ≥ {ii:0.#} kA" : "")}, ligações {dps.SecaoDeLigacao:0} mm²", "5.4.2.1.1, 6.3.5.2"]);
        protecoes.Add(["Identificação", "circuitos identificados no quadro; advertência 6.5.4.10 fixada na porta", "6.5.4.9, 6.5.4.10"]);
        Desenho.Tabela(doc, vista, 0, y - 2, "Proteções e identificação", [new("Dispositivo", 30), new("Especificação", 130), new("Item", 30)], protecoes);
        return vista;
    }

    static View UnifilarDoQuadro(Document doc, Instalacao i, ResultadoInstalacao r, ResultadoQuadro q, string nome, List<string> avisos)
    {
        var vista = Desenho.NovaVista(doc, $"{Desenho.Prefixo}Unifilar — {nome}");
        var diagrama = Procurar(Unifilar.Montar(i, r), q.Nome);
        if (diagrama is null)
        {
            avisos.Add($"{q.Nome}: unifilar não montado.");
            return vista;
        }
        DesenharDiagrama(doc, vista, diagrama, 0, 0);
        return vista;
    }

    static Unifilar.Diagrama? Procurar(IReadOnlyList<Unifilar.Diagrama> lista, string quadro)
    {
        foreach (var d in lista)
        {
            if (d.Quadro == quadro) return d;
            if (Procurar(d.Filhos, quadro) is { } f) return f;
        }
        return null;
    }

    /// <summary>Barramento vertical à esquerda; entrada em cima; cada ramo sai à direita com o disjuntor e o texto. Devolve a cota final.</summary>
    static double DesenharDiagrama(Document doc, View vista, Unifilar.Diagrama d, double x, double yTopo)
    {
        const double passo = 8;
        const double xBarra = 20;
        var y = yTopo;
        Desenho.Texto(doc, vista, x, y, $"DIAGRAMA UNIFILAR — {d.Quadro}", 200, negrito: true);
        y -= 8;
        Desenho.Texto(doc, vista, x, y, d.Entrada, 200);
        y -= 8;
        // entrada: seta vertical até o geral
        Desenho.Linha(doc, vista, x + xBarra, y + 2, x + xBarra, y - 4);
        Desenho.Linha(doc, vista, x + xBarra - 1.5, y - 2, x + xBarra, y - 4);
        Desenho.Linha(doc, vista, x + xBarra + 1.5, y - 2, x + xBarra, y - 4);
        y -= 6;
        // geral (vertical: desenha o símbolo deitado à direita da barra para ficar legível)
        Desenho.Disjuntor(doc, vista, x + xBarra, y, 6);
        Desenho.Texto(doc, vista, x + xBarra + 8, y + 1.5, $"Geral {d.Geral}", 120);
        if (d.Dps is not null)
        {
            Desenho.Linha(doc, vista, x + xBarra + 6, y, x + xBarra + 6, y - 5);
            Desenho.Caixa(doc, vista, x + xBarra + 1, y - 9, 10, 6, "DPS");
            Desenho.Texto(doc, vista, x + xBarra + 13, y - 6, d.Dps, 150);
        }
        y -= 14;
        var topoDaBarra = y;
        foreach (var bloco in d.Drs)
        {
            // sub-barramento do DR: caixa na barra e os ramos à direita
            Desenho.Linha(doc, vista, x + xBarra, y, x + xBarra + 4, y);
            Desenho.Caixa(doc, vista, x + xBarra + 4, y, 10, 6, "DR");
            Desenho.Texto(doc, vista, x + xBarra + 16, y + 1.5, bloco.Rotulo, 120);
            y -= passo;
            var xSub = x + xBarra + 14;
            var topoSub = y + passo - 3;
            foreach (var ramo in bloco.Ramos)
            {
                Desenho.Linha(doc, vista, xSub, y, xSub + 6, y);
                Desenho.Disjuntor(doc, vista, xSub + 6, y, 6);
                Desenho.Linha(doc, vista, xSub + 12, y, xSub + 20, y);
                Desenho.Texto(doc, vista, xSub + 22, y + 1.8, Rotulo(ramo), 150);
                y -= passo;
            }
            Desenho.Linha(doc, vista, xSub, topoSub, xSub, y + passo);
        }
        foreach (var ramo in d.SemDR)
        {
            Desenho.Linha(doc, vista, x + xBarra, y, x + xBarra + 6, y);
            Desenho.Disjuntor(doc, vista, x + xBarra + 6, y, 6);
            Desenho.Linha(doc, vista, x + xBarra + 12, y, x + xBarra + 20, y);
            Desenho.Texto(doc, vista, x + xBarra + 22, y + 1.8, Rotulo(ramo), 150);
            y -= passo;
        }
        Desenho.Linha(doc, vista, x + xBarra, topoDaBarra + 2, x + xBarra, y + passo);
        // quadros alimentados, abaixo, recuados
        foreach (var filho in d.Filhos)
        {
            y -= 6;
            y = DesenharDiagrama(doc, vista, filho, x + 10, y);
        }
        return y;
    }

    static string Rotulo(Unifilar.Ramo r) =>
        $"{r.Numero} — {r.Descricao}: {r.Disjuntor}; {r.Condutores}; fase {r.Fases}" + (r.DR is not null ? $"; DR {r.DR}" : "") + (r.QuadroAlimentado is not null ? $" → {r.QuadroAlimentado}" : "");

    /// <summary>Prumada: níveis como linhas horizontais, quadros no seu nível, alimentadores entre pai e filho com os condutores.</summary>
    static View Prumada(Document doc, LeitorInstalacao.Lida lida, ResultadoInstalacao r, List<string> avisos)
    {
        var vista = Desenho.NovaVista(doc, $"{Desenho.Prefixo}Prumada elétrica");
        var i = lida.Instalacao;
        var niveis = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation).ToList();
        Desenho.Texto(doc, vista, 0, 6, "PRUMADA ELÉTRICA — quadros e alimentadores", 200, negrito: true);
        const double passoNivel = 22;
        const double largura = 180;
        var cotaDoNivel = new Dictionary<ElementId, double>();
        for (var k = 0; k < niveis.Count; k++)
        {
            var y = -k * passoNivel - 10;
            cotaDoNivel[niveis[k].Id] = y;
            Desenho.Linha(doc, vista, 0, y, largura, y);
            Desenho.Texto(doc, vista, 0, y - 0.5, $"{niveis[k].Name} ({Unidades.Metros(niveis[k].ProjectElevation):0.00} m)", 60);
        }
        // quadros por nível, lado a lado
        var posicao = new Dictionary<string, (double X, double Y)>();
        var porNivel = new Dictionary<ElementId, int>();
        foreach (var q in i.Quadros)
        {
            if (!lida.Elementos.TryGetValue(q.Id, out var id) || doc.GetElement(id) is not FamilyInstance fi) continue;
            var nivelId = fi.LevelId != ElementId.InvalidElementId ? fi.LevelId : (Lancamento.Colocacao.NivelAbaixo(doc, (fi.Location as LocationPoint)?.Point.Z ?? 0)?.Id ?? ElementId.InvalidElementId);
            if (!cotaDoNivel.TryGetValue(nivelId, out var yNivel)) continue;
            var n = porNivel.TryGetValue(nivelId, out var c) ? c : 0;
            porNivel[nivelId] = n + 1;
            var x = 70 + n * 40;
            var y = yNivel + 10;
            Desenho.Caixa(doc, vista, x, y, 24, 7, q.Nome);
            posicao[q.Id] = (x, y);
            var rq = r.Quadros.FirstOrDefault(z => z.Id == q.Id);
            if (rq?.DisjuntorGeral is { } g) Desenho.Texto(doc, vista, x, y - 4.5, $"geral {g.In:0} A", 40);
        }
        foreach (var q in i.Quadros.Where(q => q.QuadroPai is not null))
        {
            if (!posicao.TryGetValue(q.Id, out var filho) || !posicao.TryGetValue(q.QuadroPai!, out var pai)) continue;
            // sai da caixa do pai, sobe/desce até a cota do filho e entra pela esquerda
            var xSaida = pai.X + 24;
            Desenho.Linha(doc, vista, xSaida, pai.Y, xSaida + 4, pai.Y);
            Desenho.Linha(doc, vista, xSaida + 4, pai.Y, xSaida + 4, filho.Y);
            Desenho.Linha(doc, vista, xSaida + 4, filho.Y, filho.X, filho.Y);
            if (i.AlimentadorDo(q) is { } al && r.Circuitos.FirstOrDefault(c => c.Id == al.Id) is { } ral)
                Desenho.Texto(doc, vista, xSaida + 5.5, (pai.Y + filho.Y) / 2 + 2, $"{ral.Numero}: {ral.Disjuntor.In:0} A, {Memorial.F(ral.SecaoFase, 1)} mm², {Memorial.F(ral.Comprimento, 1)} m", 50);
        }
        var entrada = i.QuadroDeEntrada;
        if (posicao.TryGetValue(entrada.Id, out var pe))
        {
            Desenho.Linha(doc, vista, pe.X - 20, pe.Y, pe.X, pe.Y);
            Desenho.Texto(doc, vista, pe.X - 20, pe.Y + 5, $"entrada {i.Alimentacao.TensaoFaseNeutro:0}/{i.Alimentacao.TensaoFaseFase:0} V", 40);
        }
        if (posicao.Count == 0) avisos.Add("Prumada: nenhum quadro com nível no modelo.");
        return vista;
    }

    /// <summary>Quadro de cargas nativo do Revit (PanelScheduleView), quando o projeto tem modelo de quadro; o padrão brasileiro é a vista de desenho.</summary>
    static void PainelNativo(Document doc, LeitorInstalacao.Lida lida, ResultadoQuadro q, List<string> avisos)
    {
        if (!lida.Elementos.TryGetValue(q.Id, out var id)) return;
        try
        {
            var existente = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Electrical.PanelScheduleView))
                .Cast<Autodesk.Revit.DB.Electrical.PanelScheduleView>().Any(p => p.GetPanel() == id);
            if (!existente) Autodesk.Revit.DB.Electrical.PanelScheduleView.CreateInstanceView(doc, id);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e)
        {
            avisos.Add($"{q.Nome}: quadro de cargas nativo do Revit não criado ({e.Message}); a vista de desenho AE vale.");
        }
    }
}
