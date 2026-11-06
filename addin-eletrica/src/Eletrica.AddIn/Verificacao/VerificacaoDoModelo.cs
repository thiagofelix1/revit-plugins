using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Lancamento;
using Eletrica.AddIn.Leitura;
using Eletrica.Motor.Calculo;
using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.AddIn.Verificacao;

/// <summary>
///     As verificações que precisam do modelo do Revit, não só da <see cref="Instalacao" /> (item 8.7 do plano e a parte
///     geométrica do 8.2): sistema de distribuição do quadro diferente dos Dados do projeto, famílias sem conector elétrico
///     ou sem potência, interruptor nos volumes 0 a 2 do banheiro, chuveiro elétrico longe do chuveiro hidráulico,
///     interruptor sem comando. Códigos R-. Somadas às do motor na mesma janela.
/// </summary>
static class VerificacaoDoModelo
{
    public sealed record Saida(IReadOnlyList<Problema> Problemas, IReadOnlyDictionary<string, ElementId> Elementos);

    public static Saida Verificar(Document doc, DadosProjeto dados, LeitorInstalacao.Lida lida, VolumesDeBanheiro volumes)
    {
        var problemas = new List<Problema>();
        var mapa = new Dictionary<string, ElementId>(lida.Elementos);
        var i = lida.Instalacao;

        // R-01: sistema de distribuição do quadro × dados do projeto
        foreach (var q in Circuitos.Quadros.Todos(doc))
        {
            if (q.MEPModel is not ElectricalEquipment { DistributionSystem: { } ds })
            {
                problemas.Add(new Problema("R-01", Gravidade.Erro, $"Quadro {Circuitos.Quadros.Nome(q)} sem sistema de distribuição: o Revit não aceita circuitos nele.", "modelo", [q.UniqueId]));
                mapa[q.UniqueId] = q.Id;
                continue;
            }
            try
            {
                var fn = ds.VoltageLineToGround is { } vlg ? Unidades.Volts(vlg.ActualValue) : 0;
                var ff = ds.VoltageLineToLine is { } vll ? Unidades.Volts(vll.ActualValue) : 0;
                var fases = ds.ElectricalPhase == ElectricalPhase.ThreePhase ? 3 : ds.NumWires >= 3 ? 2 : 1;
                var diverge = Math.Abs(fn - dados.TensaoFaseNeutro) > 10 || fases > 1 && ff > 0 && Math.Abs(ff - dados.TensaoFaseFase) > 15 || fases > dados.Fases;
                if (diverge)
                {
                    problemas.Add(new Problema("R-01", Gravidade.Erro, $"Quadro {Circuitos.Quadros.Nome(q)}: sistema de distribuição {fn:0}/{ff:0} V, {fases}F, diferente dos Dados do projeto ({dados.TensaoFaseNeutro:0}/{dados.TensaoFaseFase:0} V, {dados.Fases}F). O cálculo usa os Dados; corrija um dos dois.", "4.2.2", [q.UniqueId]));
                    mapa[q.UniqueId] = q.Id;
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException) { }
        }

        // R-02 / R-03: famílias sem conector elétrico ou sem potência
        var pontosRevit = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .Where(fi => fi.Category?.BuiltInCategory is BuiltInCategory.OST_ElectricalFixtures or BuiltInCategory.OST_LightingFixtures).ToList();
        foreach (var fi in pontosRevit)
        {
            if (!InserirPontos.TemConectorEletrico(fi))
            {
                problemas.Add(new Problema("R-02", Gravidade.Aviso, $"{fi.Name} ({fi.Id}): a família não tem conector elétrico; não entra em circuito do Revit nem no cálculo.", "modelo", [fi.UniqueId]));
                mapa[fi.UniqueId] = fi.Id;
            }
            else if (LeitorInstalacao.PotenciaDoPonto(fi) <= 0)
            {
                problemas.Add(new Problema("R-03", Gravidade.Aviso, $"{fi.Name} ({fi.Id}): sem potência (AE Potência nem carga aparente); entra no cálculo com 0 VA.", "4.2.1.2", [fi.UniqueId]));
                mapa[fi.UniqueId] = fi.Id;
            }
        }

        // R-04: chuveiro elétrico longe do chuveiro hidráulico; R-05: interruptor nos volumes 0 a 2; R-06: interruptor sem comando
        foreach (var p in i.Pontos.Where(p => p.Tipo == TipoCarga.Chuveiro))
        {
            if (!mapa.TryGetValue(p.Id, out var id) || doc.GetElement(id) is not FamilyInstance fi || (fi.Location as LocationPoint)?.Point is not { } local) continue;
            if (volumes.DistanciaAoBoxe(p.Ambiente, local) is { } d && d > Nbr5410.DistanciaMaximaTUE)
                problemas.Add(new Problema("R-04", Gravidade.Aviso, $"{i.AmbienteDe(p).Nome}: o ponto do chuveiro elétrico ({fi.Id}) está a {d:0.0} m do boxe/chuveiro da arquitetura; o ponto de uso específico fica a até 1,5 m do equipamento.", "4.2.1.2.3", [p.Id]));
        }
        var comandos = Comandos.Ler(doc);
        var comandados = comandos.SelectMany(k => k.Interruptores.Select(s => s.Id)).ToHashSet();
        foreach (var sw in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_LightingDevices).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
        {
            if ((sw.Location as LocationPoint)?.Point is not { } local) continue;
            var ambiente = LeituraDeAmbientes.Em(lida.Ambientes, local);
            if (ambiente is not null && ambiente.Uso is UsoAmbiente.Banheiro or UsoAmbiente.Lavabo && volumes.Volume(ambiente.Id, local) is { } v and <= 2)
            {
                problemas.Add(new Problema("R-05", Gravidade.Erro, $"{ambiente.Nome}: interruptor {sw.Name} ({sw.Id}) dentro do volume {v} do banheiro; nenhum interruptor nos volumes 0, 1 e 2.", "9.1.4.3.1", [sw.UniqueId]));
                mapa[sw.UniqueId] = sw.Id;
            }
            if (comandos.Count > 0 && !comandados.Contains(sw.Id))
            {
                problemas.Add(new Problema("R-06", Gravidade.Nota, $"Interruptor {sw.Name} ({sw.Id}) sem comando gravado: use Comando para dizer quais luminárias ele acende.", "9.5.2.1.1", [sw.UniqueId]));
                mapa[sw.UniqueId] = sw.Id;
            }
        }

        // R-07: boxe/banheira lidos por banheiro (informativo, para o engenheiro conferir a geometria usada)
        foreach (var a in lida.Ambientes.Where(a => a.Uso is UsoAmbiente.Banheiro && volumes.TemZonas(a.Id)))
            problemas.Add(new Problema("R-07", Gravidade.Nota, $"{a.Nome}: volumes gerados a partir de {volumes.Resumo(a.Id)} (volume 1 sobre a peça até 2,25 m; 2 a 0,60 m até 3,00 m; 3 a mais 2,40 m até 2,25 m; paredes não descontadas).", "9.1.2.1", [a.Id]));

        return new Saida(problemas, mapa);
    }
}
