using Autodesk.Revit.DB.Electrical;
using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Condutos;
using Eletrica.AddIn.Lancamento;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Leitura;

/// <summary>
/// Monta a <see cref="Instalacao" /> do motor a partir do modelo: quadros (ElectricalEquipment), circuitos (ElectricalSystem),
/// pontos (os elementos de cada circuito), ambientes (Room do arquivo e dos vínculos), trechos (cadeias de eletrodutos com os
/// circuitos gravados por Passar circuitos) e comandos de iluminação (AE Comando). Guarda o mapa id do motor → ElementId
/// para destacar no modelo.
/// </summary>
public static class LeitorInstalacao
{
    public sealed record Lida(Instalacao Instalacao, IReadOnlyDictionary<string, ElementId> Elementos, IReadOnlyList<string> Avisos,
        GrafoDeCondutos Grafo, IReadOnlyList<AmbienteLido> Ambientes);

    public static Lida Ler(Document doc, DadosProjeto dados)
    {
        var avisos = new List<string>();
        var mapa = new Dictionary<string, ElementId>();
        var alimentacao = dados.CriarAlimentacao();
        var ambientesLidos = LeituraDeAmbientes.Ler(doc);
        var grafo = new GrafoDeCondutos(doc);
        avisos.AddRange(grafo.Avisos);

        var ambientes = ambientesLidos.Select(a => new Ambiente(a.Id, a.Nome, a.Uso, a.Area, a.Perimetro)).ToList();
        foreach (var a in ambientesLidos.Where(a => a.Doc == doc)) mapa[a.Id] = a.Room.Id;
        if (ambientes.Count == 0) avisos.Add("Nenhum ambiente (Room) no arquivo nem nos vínculos: as verificações por cômodo não rodam.");

        var sistemas = new FilteredElementCollector(doc).OfClass(typeof(ElectricalSystem)).Cast<ElectricalSystem>()
            .Where(s => s.SystemType == ElectricalSystemType.PowerCircuit).ToList();

        // Quadros: todo equipamento que alimenta circuitos ou é alimentado por um.
        var quadrosRevit = new Dictionary<ElementId, FamilyInstance>();
        foreach (var s in sistemas)
            if (s.BaseEquipment is { } q) quadrosRevit[q.Id] = q;
        foreach (var s in sistemas)
        foreach (var e in s.Elements.Cast<Element>())
            if (e is FamilyInstance { MEPModel: ElectricalEquipment } fi && fi.Category?.BuiltInCategory == BuiltInCategory.OST_ElectricalEquipment)
                quadrosRevit[fi.Id] = fi;

        var alimentadorDe = new Dictionary<ElementId, ElectricalSystem>();
        foreach (var s in sistemas)
        foreach (var e in s.Elements.Cast<Element>())
            if (quadrosRevit.ContainsKey(e.Id)) alimentadorDe[e.Id] = s;

        var quadros = new List<Quadro>();
        foreach (var (id, fi) in quadrosRevit)
        {
            var fases = FasesDoQuadro(fi, dados.Fases);
            var pai = alimentadorDe.TryGetValue(id, out var al) && al.BaseEquipment is { } p && p.Id != id ? p.UniqueId : null;
            quadros.Add(new Quadro(fi.UniqueId, NomeDoQuadro(fi), fases) { QuadroPai = pai, No = grafo.NoDe(fi) ?? fi.UniqueId });
            mapa[fi.UniqueId] = fi.Id;
        }
        if (quadros.Count == 0)
            avisos.Add("Nenhum quadro com circuitos: insira o quadro (Lançamento → Quadro) e crie os circuitos (Criar circuitos).");
        if (quadros.Count(q => q.QuadroPai is null) > 1)
        {
            avisos.Add("Mais de um quadro sem alimentador: o cálculo trata o primeiro como o de entrada; use Alimentar quadro para ligar os outros a ele.");
            var entrada = quadros.First(q => q.QuadroPai is null);
            quadros = quadros.Select(q => q.QuadroPai is null && q.Id != entrada.Id ? q with { QuadroPai = entrada.Id } : q).ToList();
        }

        var circuitos = new List<Circuito>();
        var pontos = new List<Ponto>();
        var circuitoDoElemento = new Dictionary<ElementId, string>();
        foreach (var s in sistemas)
        {
            if (s.BaseEquipment is not { } quadro) continue;
            var elementos = s.Elements.Cast<Element>().ToList();
            var quadroAlimentado = elementos.FirstOrDefault(e => quadrosRevit.ContainsKey(e.Id));
            var polos = s.PolesNumber;
            var tensao = Unidades.Volts(s.Voltage);
            var temNeutro = quadroAlimentado is not null ? polos > 1 : s.NeutralConductorsNumber > 0 || polos == 1;
            var ligacao = polos switch
            {
                1 => Ligacao.FN,
                2 => temNeutro && tensao < alimentacao.TensaoFaseFase * 0.9 ? Ligacao.FFN : Ligacao.FF,
                _ => temNeutro ? Ligacao.FFFN : Ligacao.FFF,
            };
            if (polos == 2 && quadroAlimentado is not null) ligacao = Ligacao.FFN;
            var tipoTexto = ParametrosEletrica.LerTexto(s, ParametrosEletrica.TipoDeCarga);
            var pontosDoCircuito = new List<Ponto>();
            foreach (var e in elementos)
            {
                if (e is not FamilyInstance fi || quadrosRevit.ContainsKey(fi.Id)) continue;
                circuitoDoElemento[fi.Id] = s.UniqueId;
                if (fi.Category?.BuiltInCategory == BuiltInCategory.OST_LightingDevices) continue; // interruptor não é ponto de utilização
                var ponto = LerPonto(doc, fi, s.UniqueId, ambientesLidos, grafo, avisos);
                if (ponto is null) continue;
                pontosDoCircuito.Add(ponto);
                mapa[fi.UniqueId] = fi.Id;
            }
            // Potência: a soma dos pontos; se nenhum ponto tem potência, a carga aparente do circuito no Revit.
            var cargaRevit = Unidades.VoltAmperes(s.ApparentLoad);
            if (pontosDoCircuito.Count > 0 && pontosDoCircuito.All(p => p.PotenciaVA <= 0) && cargaRevit > 0)
            {
                var porPonto = cargaRevit / pontosDoCircuito.Count;
                pontosDoCircuito = pontosDoCircuito.Select(p => p with { PotenciaVA = porPonto }).ToList();
            }
            var tipoCircuito = quadroAlimentado is not null ? TipoCarga.Alimentador
                : TipoDeCarga(tipoTexto) ?? (pontosDoCircuito.Count > 0 ? pontosDoCircuito.GroupBy(p => p.Tipo).OrderByDescending(g => g.Sum(p => p.PotenciaVA)).First().Key : TipoCarga.Outro);
            circuitos.Add(new Circuito(s.UniqueId, s.CircuitNumber, quadro.UniqueId, tipoCircuito, ligacao)
            {
                Descricao = s.LoadName,
                PotenciaVA = pontosDoCircuito.Count == 0 && cargaRevit > 0 ? cargaRevit : null,
                FatorDePotencia = s.PowerFactor is > 0 and <= 1 ? s.PowerFactor : 1,
                QuadroAlimentado = quadroAlimentado?.UniqueId,
                Isolacao = quadroAlimentado is not null ? Isolacao.Epr90 : Isolacao.Pvc70,
                ComprimentoManual = Unidades.Metros(s.Length) is > 0 and var l ? l : null,
            });
            pontos.AddRange(pontosDoCircuito);
            mapa[s.UniqueId] = s.Id;
        }

        // Comandos de iluminação: interruptores (nó no grafo) e luminárias que já são pontos.
        var idsDePontos = pontos.Select(p => p.Id).ToHashSet();
        var comandos = new List<Comando>();
        foreach (var k in Comandos.Ler(doc))
        {
            var luminarias = k.Luminarias.Where(l => idsDePontos.Contains(l.UniqueId)).Select(l => l.UniqueId).ToList();
            if (luminarias.Count == 0) continue;
            var circuito = k.Luminarias.Select(l => circuitoDoElemento.TryGetValue(l.Id, out var c) ? c : null).FirstOrDefault(c => c is not null);
            comandos.Add(new Comando(k.Id, circuito, k.Interruptores.Select(s => s.UniqueId).ToList(), luminarias, k.Tipo)
            {
                NosDosInterruptores = k.Interruptores.Select(s => grafo.NoDe(s) ?? s.UniqueId).ToList(),
            });
            foreach (var s in k.Interruptores) mapa[s.UniqueId] = s.Id;
        }

        // Trechos: cadeias de eletrodutos com os circuitos e retornos gravados por Passar circuitos.
        var porUniqueId = circuitos.ToDictionary(c => c.Id, c => c);
        var trechos = new List<Trecho>();
        foreach (var cadeia in grafo.Cadeias)
        {
            var ids = cadeia.Conduites.Select(c => ParametrosEletrica.LerTexto(c, ParametrosEletrica.Circuitos)).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            var circuitosDoTrecho = Separar(ids).Where(porUniqueId.ContainsKey).Distinct().ToList();
            var retornos = cadeia.Conduites.Select(c => ParametrosEletrica.LerTexto(c, ParametrosEletrica.Retornos)).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            trechos.Add(new Trecho(cadeia.Id, cadeia.De, cadeia.Para, Math.Max(cadeia.Comprimento, 0.01), circuitosDoTrecho)
            {
                TipoEletroduto = cadeia.Tipo,
                Dn = cadeia.Dn,
                Curvas90 = cadeia.Curvas,
                Metodo = MetodoReferencia.B1,
                Retornos = Separar(retornos).Where(porUniqueId.ContainsKey).ToList(),
            });
            foreach (var c in cadeia.Conduites)
                if (!mapa.ContainsKey(cadeia.Id)) mapa[cadeia.Id] = c.Id;
        }
        if (trechos.Count > 0 && trechos.All(t => t.Circuitos.Count == 0))
            avisos.Add("Os eletrodutos ainda não têm circuitos: rode Passar circuitos para o agrupamento e a ocupação trecho a trecho.");

        var instalacao = new Instalacao(doc.Title, alimentacao, ambientes, quadros, circuitos, pontos, trechos, comandos: comandos);
        return new Lida(instalacao, mapa, avisos, grafo, ambientesLidos);
    }

    static IEnumerable<string> Separar(string? texto) => (texto ?? "").Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Um ponto de utilização a partir do elemento do Revit: tipo (AE Tipo do ponto, senão o nome da família e a categoria),
    /// potência (AE Potência, carga aparente ou parâmetro da família), ambiente pelo Room no ponto (arquivo ou vínculo), altura
    /// pelo nível, nó no grafo de eletrodutos. Nulo (com aviso) quando está fora de qualquer ambiente.
    /// </summary>
    public static Ponto? LerPonto(Document doc, FamilyInstance fi, string? circuito, IReadOnlyList<AmbienteLido> ambientes, GrafoDeCondutos? grafo, List<string> avisos)
    {
        var categoria = fi.Category?.BuiltInCategory ?? BuiltInCategory.INVALID;
        var tipo = TipoDoPonto(fi, categoria);
        var local = (fi.Location as LocationPoint)?.Point;
        var ambiente = local is not null ? LeituraDeAmbientes.Em(ambientes, local) : null;
        if (ambiente is null)
        {
            avisos.Add($"{fi.Name} ({fi.Id}) não está dentro de nenhum ambiente: fica fora das verificações por cômodo.");
            return null;
        }
        var altura = local is not null ? Colocacao.AlturaDe(doc, fi, doc.GetElement(fi.LevelId) as Level) : 0;
        var potencia = PotenciaDoPonto(fi);
        return new Ponto(fi.UniqueId, ambiente.Id, circuito, tipo, potencia)
        {
            Altura = ParametrosEletrica.LerNumero(fi, ParametrosEletrica.Altura) is { } h and > 0 ? h : altura,
            NoTeto = tipo == TipoCarga.Iluminacao && (altura > 2.2 || fi.Host?.Category?.BuiltInCategory is BuiltInCategory.OST_Ceilings or BuiltInCategory.OST_Floors
                                                      || ParametrosEletrica.LerTexto(fi, ParametrosEletrica.Posicao) == "Teto"),
            CorrenteDaTomada = tipo is TipoCarga.TomadaUsoGeral or TipoCarga.TomadaUsoEspecifico ? CorrenteDaTomada(fi) : null,
            No = grafo?.NoDe(fi) ?? fi.UniqueId,
        };
    }

    /// <summary>Pontos elétricos do modelo que ainda não estão em circuito algum (tomadas, luminárias, equipamentos; interruptores à parte).</summary>
    public static List<FamilyInstance> PontosSemCircuito(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .Where(fi => fi.Category?.BuiltInCategory is BuiltInCategory.OST_ElectricalFixtures or BuiltInCategory.OST_LightingFixtures or BuiltInCategory.OST_MechanicalEquipment)
            .Where(InserirPontos.SemCircuito)
            .ToList();

    static int FasesDoQuadro(FamilyInstance fi, int padrao)
    {
        try
        {
            if (fi.MEPModel is ElectricalEquipment { DistributionSystem: { } ds })
            {
                if (ds.ElectricalPhase == ElectricalPhase.ThreePhase) return 3;
                return ds.NumWires >= 3 ? 2 : 1;
            }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { }
        return padrao;
    }

    static string NomeDoQuadro(FamilyInstance fi) =>
        fi.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME)?.AsString() is { Length: > 0 } nome ? nome : fi.Name;

    public static TipoCarga TipoDoPonto(FamilyInstance fi, BuiltInCategory categoria)
    {
        if (TipoDeCarga(ParametrosEletrica.LerTexto(fi, ParametrosEletrica.TipoDoPonto)) is { } informado) return informado;
        var nome = (fi.Symbol?.FamilyName + " " + fi.Name).ToLowerInvariant();
        if (nome.Contains("chuveiro") || nome.Contains("aquecedor") || nome.Contains("shower")) return TipoCarga.Chuveiro;
        if (nome.Contains("ar-cond") || nome.Contains("ar cond") || nome.Contains("split") || nome.Contains("condicionad") || nome.Contains("air")) return TipoCarga.ArCondicionado;
        if (nome.Contains("motor") || nome.Contains("bomba") || nome.Contains("pump")) return TipoCarga.Motor;
        if (nome.Contains("carregador") || nome.Contains("veículo") || nome.Contains("veiculo") || nome.Contains("ev ")) return TipoCarga.VeiculoEletrico;
        return categoria switch
        {
            BuiltInCategory.OST_LightingFixtures => TipoCarga.Iluminacao,
            BuiltInCategory.OST_ElectricalFixtures => nome.Contains("tue") || nome.Contains("específic") || nome.Contains("especific") || nome.Contains("20a") || nome.Contains("20 a")
                ? TipoCarga.TomadaUsoEspecifico : TipoCarga.TomadaUsoGeral,
            BuiltInCategory.OST_MechanicalEquipment => TipoCarga.Motor,
            _ => TipoCarga.Outro,
        };
    }

    static TipoCarga? TipoDeCarga(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return Enum.TryParse<TipoCarga>(texto!.Trim(), true, out var t) ? t : null;
    }

    public static double PotenciaDoPonto(FamilyInstance fi)
    {
        if (ParametrosEletrica.LerNumero(fi, ParametrosEletrica.Potencia) is { } ae and > 0) return ae;
        if (fi.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD) is { HasValue: true } p && p.AsDouble() > 0) return Unidades.VoltAmperes(p.AsDouble());
        foreach (var nome in new[] { "Apparent Load", "Carga aparente", "Potência aparente", "Potência (VA)", "Potencia" })
            if (fi.LookupParameter(nome) is { HasValue: true, StorageType: StorageType.Double } q && q.AsDouble() > 0) return Unidades.VoltAmperes(q.AsDouble());
        return 0;
    }

    static int? CorrenteDaTomada(FamilyInstance fi)
    {
        var nome = (fi.Symbol?.FamilyName + " " + fi.Name).ToLowerInvariant();
        if (nome.Contains("20a") || nome.Contains("20 a")) return 20;
        if (nome.Contains("10a") || nome.Contains("10 a")) return 10;
        return null;
    }
}
