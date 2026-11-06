using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Lancamento;
using Eletrica.AddIn.Leitura;
using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Lancamento;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Circuitos;

/// <summary>
///     Criar circuitos (item 3.1 do plano): pega os pontos que ainda não têm circuito, divide pela NBR 5410 e pelo padrão
///     (<see cref="DivisaoDeCircuitos" />), mostra a proposta e cria no Revit um ElectricalSystem por circuito, ligado ao
///     quadro escolhido, com a descrição e o tipo de carga. Os interruptores dos comandos entram no circuito das luminárias.
///     Cada circuito numa transação própria; tudo num grupo, um Ctrl+Z desfaz.
/// </summary>
public static class CriarCircuitos
{
    public sealed record Proposta(IReadOnlyList<CircuitoProposto> Circuitos, IReadOnlyDictionary<string, FamilyInstance> Elementos, IReadOnlyList<string> Avisos, int Soltos);

    public sealed record Resultado(int Criados, IReadOnlyList<string> Avisos, IReadOnlyList<ElementId> Sistemas);

    public static Proposta Propor(Document doc, DadosProjeto dados, PadraoEletrica padrao, FamilyInstance quadro)
    {
        var avisos = new List<string>();
        var ambientesLidos = LeituraDeAmbientes.Ler(doc);
        var ambientes = ambientesLidos.ToDictionary(a => a.Id, a => new Ambiente(a.Id, a.Nome, a.Uso, a.Area, a.Perimetro));
        var elementos = new Dictionary<string, FamilyInstance>();
        var pontos = new List<Ponto>();
        var soltos = LeitorInstalacao.PontosSemCircuito(doc);
        foreach (var fi in soltos)
        {
            if (!InserirPontos.TemConectorEletrico(fi))
            {
                avisos.Add($"{fi.Name} ({fi.Id}): a família não tem conector elétrico; não entra em circuito do Revit.");
                continue;
            }
            var ponto = LeitorInstalacao.LerPonto(doc, fi, null, ambientesLidos, null, avisos);
            if (ponto is null) continue;
            if (ponto.PotenciaVA <= 0)
                avisos.Add($"{fi.Name} ({fi.Id}): sem potência (AE Potência nem carga aparente); entra com 0 VA.");
            pontos.Add(ponto);
            elementos[ponto.Id] = fi;
        }
        var numeroInicial = ProximoNumero(doc, quadro, padrao.Lancamento.PrefixoCircuito);
        var circuitos = DivisaoDeCircuitos.Propor(pontos, ambientes, dados.CriarAlimentacao(), padrao.Lancamento, numeroInicial);
        return new Proposta(circuitos, elementos, avisos, soltos.Count);
    }

    /// <summary>Próximo número livre do prefixo nas descrições dos circuitos já existentes no quadro.</summary>
    static int ProximoNumero(Document doc, FamilyInstance quadro, string prefixo)
    {
        var maior = 0;
        foreach (var s in new FilteredElementCollector(doc).OfClass(typeof(ElectricalSystem)).Cast<ElectricalSystem>())
        {
            if (s.BaseEquipment?.Id != quadro.Id) continue;
            var nome = s.LoadName ?? "";
            var corte = nome.IndexOf(' ');
            var cabeca = corte > 0 ? nome.Substring(0, corte) : nome;
            if (cabeca.StartsWith(prefixo) && int.TryParse(cabeca.Substring(prefixo.Length), out var n)) maior = Math.Max(maior, n);
        }
        return maior + 1;
    }

    public static Resultado Criar(Document doc, FamilyInstance quadro, IReadOnlyList<CircuitoProposto> escolhidos, IReadOnlyDictionary<string, FamilyInstance> elementos)
    {
        var avisos = new List<string>();
        var sistemas = new List<ElementId>();
        var comandos = Comandos.Ler(doc);
        using var grupo = new TransactionGroup(doc, "AutoEletrica — Criar circuitos");
        grupo.Start();
        foreach (var c in escolhidos)
        {
            var ids = c.Pontos.Where(elementos.ContainsKey).Select(p => elementos[p].Id).ToList();
            if (ids.Count == 0) continue;
            // Interruptores que comandam luminárias deste circuito e ainda não têm circuito.
            var interruptores = comandos.Where(k => k.Luminarias.Any(l => ids.Contains(l.Id)))
                .SelectMany(k => k.Interruptores).Where(s => Comandos.CircuitoDe(s) is null && InserirPontos.TemConectorEletrico(s))
                .Select(s => s.Id).Distinct().ToList();
            ElementId? criado = null;
            var motivo = TransacaoSegura.Executar(doc, $"AutoEletrica — Circuito {c.Numero}", () =>
            {
                if (ParametrosEletrica.Garantir(doc) is { } aviso) avisos.Add(aviso);
                var sistema = ElectricalSystem.Create(doc, ids, ElectricalSystemType.PowerCircuit);
                sistema.SelectPanel(quadro);
                sistema.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NAME)?.Set($"{c.Numero} {c.Descricao}");
                ParametrosEletrica.Gravar(sistema, ParametrosEletrica.TipoDeCarga, c.Tipo.ToString());
                if (interruptores.Count > 0)
                {
                    var conjunto = new ElementSet();
                    foreach (var id in interruptores) conjunto.Insert(doc.GetElement(id));
                    try
                    {
                        if (!sistema.AddToCircuit(conjunto)) avisos.Add($"{c.Numero}: o Revit não aceitou os interruptores no circuito.");
                    }
                    catch (Autodesk.Revit.Exceptions.ApplicationException e) { avisos.Add($"{c.Numero}: interruptores fora do circuito ({e.Message})."); }
                }
                doc.Regenerate();
                var polosPedidos = c.Ligacao is Ligacao.FN ? 1 : c.Ligacao is Ligacao.FF or Ligacao.FFN ? 2 : 3;
                if (sistema.PolesNumber != polosPedidos)
                    avisos.Add($"{c.Numero} {c.Descricao}: a proposta é {Nome(c.Ligacao)} ({polosPedidos} polo(s)), mas o conector da família dá {sistema.PolesNumber}; " +
                               "ajuste o conector da família (polos e tensão) ou aceite a ligação do Revit.");
                criado = sistema.Id;
                return null;
            });
            if (motivo is not null) avisos.Add($"{c.Numero} {c.Descricao}: não criado — {motivo}");
            else if (criado is not null) sistemas.Add(criado);
        }
        grupo.Assimilate();
        return new Resultado(sistemas.Count, avisos, sistemas);
    }

    public static string Nome(Ligacao l) => l switch
    {
        Ligacao.FN => "F+N",
        Ligacao.FF => "F+F",
        Ligacao.FFN => "2F+N",
        Ligacao.FFF => "3F",
        _ => "3F+N",
    };
}
