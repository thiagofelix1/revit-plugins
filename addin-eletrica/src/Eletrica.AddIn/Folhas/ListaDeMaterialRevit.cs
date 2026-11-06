using System.IO;
using Eletrica.AddIn.Leitura;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Relatorios;

namespace Eletrica.AddIn.Folhas;

/// <summary>
///     Lista de material (item 9.9 do plano): a lista do motor (condutores por seção e função, eletrodutos por DN,
///     disjuntores, DRs, DPS, pontos, quadros) em Markdown, CSV e numa vista de desenho, mais as tabelas nativas do Revit do
///     que está modelado (eletrodutos por tipo e diâmetro, conexões, pontos por tipo), que atualizam sozinhas e vão para a
///     prancha como qualquer tabela.
/// </summary>
static class ListaDeMaterialRevit
{
    public sealed record Feito(View Vista, IReadOnlyList<ViewSchedule> Tabelas, string Markdown, string Csv, IReadOnlyList<string> Avisos);

    public const string NomeDaVista = Desenho.Prefixo + "Lista de material";

    public static Feito Gerar(Document doc, LeitorInstalacao.Lida lida, ResultadoInstalacao r)
    {
        var avisos = new List<string>();
        var itens = ListaDeMaterial.Gerar(lida.Instalacao, r);
        Directory.CreateDirectory(PastasEletrica.Calculo);
        var nome = string.Concat(doc.Title.Split(Path.GetInvalidFileNameChars()));
        var md = Path.Combine(PastasEletrica.Calculo, $"{nome}-lista-de-material.md");
        var csv = Path.Combine(PastasEletrica.Calculo, $"{nome}-lista-de-material.csv");
        File.WriteAllText(md, ListaDeMaterial.Markdown(itens, r.Nome));
        File.WriteAllText(csv, ListaDeMaterial.Csv(itens));

        using var transacao = new Transaction(doc, "AutoEletrica — Lista de material");
        transacao.Start();
        EngolirAvisos.Aplicar(transacao);
        var vista = Desenho.NovaVista(doc, NomeDaVista);
        Desenho.Texto(doc, vista, 0, 0, $"LISTA DE MATERIAL — {r.Nome}", 200, negrito: true);
        var y = -8.0;
        foreach (var grupo in itens.GroupBy(x => x.Grupo))
            y = Desenho.Tabela(doc, vista, 0, y, grupo.Key, [new("Item", 110), new("Un.", 12), new("Quantidade", 22), new("Origem", 70)],
                grupo.Select(x => (IReadOnlyList<string>)[x.Descricao, x.Unidade, x.Quantidade.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), x.Origem]), 5.5) - 2;
        Desenho.Texto(doc, vista, 0, y, $"Condutores com {ListaDeMaterial.FolgaDeCondutores:0} % de folga de corte; eletrodutos com 5 %. Quantidades do modelo e do cálculo; conferir com o levantamento em obra.", 214);

        var tabelas = new List<ViewSchedule>();
        void Tabela(string nomeTabela, BuiltInCategory categoria, IReadOnlyList<(BuiltInParameter Parametro, string Titulo, bool Agrupar, bool Somar)> campos)
        {
            try
            {
                var existente = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>().FirstOrDefault(v => v.Name == nomeTabela);
                if (existente is not null) { tabelas.Add(existente); return; }
                var tabela = ViewSchedule.CreateSchedule(doc, new ElementId(categoria));
                tabela.Name = Desenho.NomeValido(nomeTabela);
                var definicao = tabela.Definition;
                var disponiveis = definicao.GetSchedulableFields();
                foreach (var (parametro, titulo, agrupar, somar) in campos)
                {
                    if (disponiveis.FirstOrDefault(f => f.ParameterId == new ElementId(parametro)) is not { } campo) continue;
                    var adicionado = definicao.AddField(campo);
                    adicionado.ColumnHeading = titulo;
                    if (somar) adicionado.DisplayType = ScheduleFieldDisplayType.Totals;
                    if (agrupar) definicao.AddSortGroupField(new ScheduleSortGroupField(adicionado.FieldId));
                }
                if (disponiveis.FirstOrDefault(f => f.FieldType == ScheduleFieldType.Count) is { } contagem)
                    definicao.AddField(contagem).ColumnHeading = "Quantidade";
                definicao.IsItemized = false;
                definicao.ShowGrandTotal = true;
                definicao.ShowGrandTotalCount = true;
                tabelas.Add(tabela);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException e)
            {
                avisos.Add($"Tabela {nomeTabela}: {e.Message}");
            }
        }
        Tabela(Desenho.Prefixo + "Eletrodutos por diâmetro", BuiltInCategory.OST_Conduit,
            [(BuiltInParameter.ELEM_TYPE_PARAM, "Tipo", true, false), (BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM, "Diâmetro", true, false), (BuiltInParameter.CURVE_ELEM_LENGTH, "Comprimento", false, true)]);
        Tabela(Desenho.Prefixo + "Conexões de eletroduto", BuiltInCategory.OST_ConduitFitting, [(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM, "Peça", true, false)]);
        Tabela(Desenho.Prefixo + "Tomadas e pontos", BuiltInCategory.OST_ElectricalFixtures, [(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM, "Ponto", true, false)]);
        Tabela(Desenho.Prefixo + "Luminárias", BuiltInCategory.OST_LightingFixtures, [(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM, "Luminária", true, false)]);
        Tabela(Desenho.Prefixo + "Interruptores", BuiltInCategory.OST_LightingDevices, [(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM, "Interruptor", true, false)]);
        Tabela(Desenho.Prefixo + "Quadros", BuiltInCategory.OST_ElectricalEquipment, [(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM, "Quadro", true, false)]);
        transacao.Commit();
        return new Feito(vista, tabelas, md, csv, avisos);
    }
}
