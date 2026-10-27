namespace Spk.AddIn.Verificacao;

/// <summary>O que a reconexão fez: quantas ligações e, para cada uma que ficou de fora, o motivo.</summary>
public sealed record ResultadoReconexao(int Ligadas, IReadOnlyList<string> Recusadas)
{
    public string Texto(int maximo = 15)
    {
        var linhas = new List<string> { $"{Ligadas} ligação(ões) feita(s)." };
        if (Recusadas.Count > 0)
        {
            linhas.Add($"{Recusadas.Count} ficaram como estavam:");
            linhas.AddRange(Recusadas.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(maximo)
                .Select(g => $"• {g.Key}{(g.Count() > 1 ? $" ({g.Count()})" : "")}"));
        }
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Liga as peças que Verificar modelo achou encostadas mas não ligadas. Cada par é uma transação própria: se o Revit
///     recusar uma ligação (erro, peça que precisaria se deformar), só ela volta atrás, com o motivo anotado. Tudo fica
///     num grupo de transações, desfeito com um Ctrl+Z.
///     Regras, para não estragar o desenho: não se move tubo nenhum (mudar a ponta de um tubo faz o Revit arrastar as
///     conexões da outra ponta e desmontar o vizinho: na Nova Era, mover 0,5 cm partiu a rede em centenas de pedaços).
///     Liga só o que já se toca, com folga de até 1 cm; folga maior fica para o projetista, com o tamanho no aviso. Só
///     se move peça solta (bico ou conexão sem nenhuma outra ligação), que não arrasta nada. Tubo e conexão precisam ter
///     o mesmo diâmetro e estar de frente; bico pode ligar em tubo maior (a rosca do bico é menor). Dois tubos de ponta
///     a ponta ganham luva (alinhados) ou cotovelo (em ângulo), pelas preferências de roteamento do tipo de tubo.
/// </summary>
public static class Reconexao
{
    static readonly double Encosto = UnitUtils.ConvertToInternalUnits(0.05, UnitTypeId.Meters);
    static readonly double Folga = UnitUtils.ConvertToInternalUnits(0.01, UnitTypeId.Meters);
    static readonly double MesmoDiametro = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters);

    public static ResultadoReconexao Executar(Document doc, IEnumerable<Ocorrencia> encostadas)
    {
        var ligadas = 0;
        var recusadas = new List<string>();
        using var grupo = new TransactionGroup(doc, "SPK — Reconectar peças encostadas");
        grupo.Start();
        foreach (var ocorrencia in encostadas.Where(o => o.Elementos.Count == 2))
        {
            var a = doc.GetElement(ocorrencia.Elementos[0]);
            var b = doc.GetElement(ocorrencia.Elementos[1]);
            if (a is null || b is null) continue;
            var motivo = Ligar(doc, a, b);
            if (motivo is null) ligadas++;
            else recusadas.Add(motivo);
        }
        grupo.Assimilate();
        return new ResultadoReconexao(ligadas, recusadas);
    }

    /// <summary>Nulo se ligou; senão, por que não.</summary>
    static string? Ligar(Document doc, Element a, Element b)
    {
        if (Par(a, b) is not { } par) return "já estão ligadas ou não têm conector livre perto";
        var (ca, cb) = par;
        if (ca.Shape != ConnectorProfileType.Round || cb.Shape != ConnectorProfileType.Round) return "conector que não é redondo";
        var temBico = EhBico(a) || EhBico(b);
        if (!temBico && Math.Abs(ca.Radius - cb.Radius) * 2 > MesmoDiametro) return "diâmetros diferentes: precisa de redução";
        if (ca.CoordinateSystem.BasisZ.DotProduct(cb.CoordinateSystem.BasisZ) > -0.95 && !(a is MEPCurve && b is MEPCurve))
            return "conectores não estão de frente um para o outro";

        var recusa = new RecusarErros();
        using var transacao = new Transaction(doc, "SPK — Reconectar");
        var opcoes = transacao.GetFailureHandlingOptions();
        opcoes.SetFailuresPreprocessor(recusa);
        // Sem isto o Revit mostra, depois de desfazer, uma janela com o erro: uma por peça recusada, centenas num
        // projeto grande. O motivo já vai para o relatório do fim.
        opcoes.SetClearAfterRollback(true);
        transacao.SetFailureHandlingOptions(opcoes);
        transacao.Start();
        var antes = Retrato(doc, a, b);
        string? motivo;
        try
        {
            motivo = a is MEPCurve ta && b is MEPCurve tb ? TuboComTubo(doc, ta, ca, tb, cb) : PecaComPeca(doc, a, ca, b, cb);
            if (motivo is null)
            {
                doc.Regenerate();
                motivo = Estragou(doc, antes, a, b);
            }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException e)
        {
            motivo = "o Revit recusou: " + e.Message;
        }
        if (motivo is not null)
        {
            transacao.RollBack();
            return motivo;
        }
        return transacao.Commit() == TransactionStatus.Committed ? null : "o Revit desfez a ligação: " + (recusa.Mensagem ?? "erro ao confirmar");
    }

    /// <summary>Tubo e peça (ou duas peças): liga se já se tocam; peça solta pode ser trazida até o conector.</summary>
    static string? PecaComPeca(Document doc, Element a, Connector ca, Element b, Connector cb)
    {
        var distancia = ca.Origin.DistanceTo(cb.Origin);
        if (distancia > Folga)
        {
            if (a is not MEPCurve && Livre(a)) ElementTransformUtils.MoveElement(doc, a.Id, cb.Origin - ca.Origin);
            else if (b is not MEPCurve && Livre(b)) ElementTransformUtils.MoveElement(doc, b.Id, ca.Origin - cb.Origin);
            else return FolgaGrande(distancia);
            doc.Regenerate();
            if (Par(a, b) is not { } novo) return "depois de encostar, os conectores não se acharam";
            (ca, cb) = novo;
        }
        ca.ConnectTo(cb);
        return null;
    }

    /// <summary>Conectores das duas peças e de todas as ligadas a elas: posição e se estão ligados.</summary>
    static Dictionary<ElementId, List<(XYZ Origem, bool Ligado)>> Retrato(Document doc, Element a, Element b)
    {
        var elementos = new HashSet<ElementId> { a.Id, b.Id };
        foreach (var e in new[] { a, b })
        foreach (var c in Conectores(e).Where(c => c.IsConnected))
        foreach (Connector outro in c.AllRefs)
            if (outro.Owner is MEPCurve or FamilyInstance) elementos.Add(outro.Owner.Id);
        return elementos.Select(doc.GetElement).OfType<Element>()
            .ToDictionary(e => e.Id, e => Conectores(e).Select(c => (c.Origin, c.IsConnected)).ToList());
    }

    /// <summary>
    /// Ligar não pode desmontar o vizinho: nenhuma ligação que existia pode sumir, e só pode ter se mexido (mais de 1 mm)
    /// a peça solta que o SPK moveu de propósito. O Revit, ao ligar conectores com folga, às vezes arrasta peças presas a
    /// outras; na Nova Era isso partiu a rede em centenas de pedaços.
    /// </summary>
    static string? Estragou(Document doc, Dictionary<ElementId, List<(XYZ Origem, bool Ligado)>> antes, Element a, Element b)
    {
        var tolerancia = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters);
        foreach (var (id, conectores) in antes)
        {
            if (doc.GetElement(id) is not { } elemento) return "ligar apagaria uma peça vizinha";
            var agora = Conectores(elemento);
            if (agora.Count != conectores.Count) return "ligar mudaria uma peça vizinha";
            var podeMexer = (id == a.Id || id == b.Id) && elemento is not MEPCurve && conectores.All(c => !c.Ligado);
            // O Revit muda a ordem dos conectores ao ligar: cada um de antes é comparado com o mais próximo de agora.
            foreach (var (origem, ligado) in conectores)
            {
                var atual = agora.OrderBy(c => c.Origin.DistanceTo(origem)).First();
                if (ligado && !atual.IsConnected) return "ligar desligaria uma peça vizinha";
                // O conector que acabou de ser ligado pode se acomodar (o Revit encurta o tubo para caber a luva).
                if (!ligado && atual.IsConnected) continue;
                if (!podeMexer && atual.Origin.DistanceTo(origem) > tolerancia)
                    return "o Revit precisaria mover peças presas a outras: alinhe as peças à mão";
            }
        }
        return null;
    }

    static string FolgaGrande(double distancia) =>
        $"folga de {UnitUtils.ConvertFromInternalUnits(distancia, UnitTypeId.Centimeters).ToString("0", Entrada.Cultura)} cm ou mais: " +
        "arraste a ponta até o conector (o SPK não move tubo, para não desmontar o vizinho)";

    static bool EhBico(Element e) => e.Category?.Id.Value == (long)BuiltInCategory.OST_Sprinklers;

    /// <summary>Dois tubos ponta a ponta, já se tocando: luva se alinhados, cotovelo se em ângulo.</summary>
    static string? TuboComTubo(Document doc, MEPCurve a, Connector ca, MEPCurve b, Connector cb)
    {
        var distancia = ca.Origin.DistanceTo(cb.Origin);
        if (distancia > Folga) return FolgaGrande(distancia);
        var alinhados = Math.Abs(ca.CoordinateSystem.BasisZ.DotProduct(cb.CoordinateSystem.BasisZ)) > 0.999;
        var peca = alinhados ? doc.Create.NewUnionFitting(ca, cb) : doc.Create.NewElbowFitting(ca, cb);
        return peca is null ? "o tipo de tubo não tem luva ou cotovelo nas preferências de roteamento" : null;
    }

    /// <summary>Peça sem nenhuma outra ligação: pode ser movida sem desligar nada.</summary>
    static bool Livre(Element e) => Conectores(e).All(c => !c.IsConnected);

    /// <summary>O par de conectores livres, um de cada, mais próximo, a menos de 5 cm.</summary>
    static (Connector, Connector)? Par(Element a, Element b)
    {
        (Connector, Connector)? melhor = null;
        var menor = Encosto;
        foreach (var ca in Conectores(a).Where(c => !c.IsConnected))
        foreach (var cb in Conectores(b).Where(c => !c.IsConnected))
        {
            var d = ca.Origin.DistanceTo(cb.Origin);
            if (d > menor) continue;
            menor = d;
            melhor = (ca, cb);
        }
        return melhor;
    }

    static List<Connector> Conectores(Element e)
    {
        var gerente = e switch
        {
            MEPCurve curva => curva.ConnectorManager,
            FamilyInstance instancia => instancia.MEPModel?.ConnectorManager,
            _ => null,
        };
        return gerente?.Connectors.Cast<Connector>()
                   .Where(c => c.ConnectorType == ConnectorType.End && c.Domain == Domain.DomainPiping).ToList()
               ?? [];
    }

    /// <summary>Apaga avisos; com erro, desfaz a transação inteira e guarda a mensagem.</summary>
    sealed class RecusarErros : IFailuresPreprocessor
    {
        public string? Mensagem { get; private set; }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor acessor)
        {
            foreach (var falha in acessor.GetFailureMessages())
            {
                if (falha.GetSeverity() == FailureSeverity.Warning) acessor.DeleteWarning(falha);
                else Mensagem ??= falha.GetDescriptionText();
            }
            return Mensagem is null ? FailureProcessingResult.Continue : FailureProcessingResult.ProceedWithRollBack;
        }
    }
}
