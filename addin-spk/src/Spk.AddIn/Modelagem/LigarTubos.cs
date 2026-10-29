using Autodesk.Revit.DB.Plumbing;

namespace Spk.AddIn.Modelagem;

/// <summary>O que a ligação fez: a peça que entrou (tê, cotovelo) e os tubos novos, ou por que não ligou.</summary>
public sealed record LigacaoDeTubos(string? Peca, IReadOnlyList<ElementId> Criados, string? Recusa)
{
    public string Texto() => Recusa is null ? $"ligado com {Peca}" : "não ligou: " + Recusa;
}

/// <summary>
///     Liga um tubo a outro, peça a peça, como o projetista faria à mão, mas sem deixar nada encostado sem ligar: a ponta
///     solta do primeiro vai até o segundo. Se o encontro cai no meio do segundo, ele é partido e ganha um tê (com um tubo
///     vertical, se as alturas são diferentes): a mesma ligação do <see cref="LigarAGeral" />. Se cai na ponta solta do
///     segundo, os dois são esticados até o canto e se encontram num cotovelo. Numa transação: Ctrl+Z desfaz.
/// </summary>
public static class LigarTubos
{
    /// <summary>Até quantos graus a ponta é tratada como apontando para o outro tubo (o mesmo do Ligar à geral).</summary>
    const double Alinhado = 8;

    public static LigacaoDeTubos Executar(Document doc, Pipe chega, Pipe alvo)
    {
        if (chega.Id == alvo.Id) return new LigacaoDeTubos(null, [], "clique em dois tubos diferentes");
        if (Eixo(chega) is not { } eixoChega || Eixo(alvo) is not { } eixoAlvo)
            return new LigacaoDeTubos(null, [], "os dois tubos precisam ser retos e horizontais (ramal, subgeral ou geral); a descida e a subida o SPK faz");

        // A ponta solta do primeiro mais perto do segundo.
        var ponta = Pontas(chega).Where(c => !c.IsConnected).OrderBy(c => DistanciaAoEixo(eixoAlvo, c.Origin)).FirstOrDefault();
        if (ponta is null) return new LigacaoDeTubos(null, [], "o primeiro tubo não tem ponta solta: clique primeiro no tubo que chega, depois no de destino");
        var p = ponta.Origin;
        var outra = eixoChega.GetEndPoint(0).DistanceTo(p) < eixoChega.GetEndPoint(1).DistanceTo(p) ? eixoChega.GetEndPoint(1) : eixoChega.GetEndPoint(0);
        var sentido = Horizontal(p - outra).Normalize();

        var da = Horizontal(eixoAlvo.Direction).Normalize();
        var normal = new XYZ(-da.Y, da.X, 0);
        var a = eixoAlvo.GetEndPoint(0);
        var lado = Horizontal(a - p).DotProduct(normal);
        var avanco = sentido.DotProduct(normal);
        var cruza = Math.Abs(avanco) > Math.Cos(Alinhado * Math.PI / 180) && lado / avanco > 0;
        var encontro = cruza ? p + sentido * (lado / avanco) : p + normal * lado;
        var t = Horizontal(encontro - a).DotProduct(da);
        var margem = LigarAGeral.MargemPonta;

        // No meio do tubo de destino: tê, como no Ligar à geral.
        if (t > margem && t < eixoAlvo.Length - margem)
        {
            var novos = new List<Pipe>();
            var entrada = new LigarAGeral.EntradaDaRede(p, null, null, chega, ponta);
            var recusa = TransacaoSegura.Executar(doc, "SPK — Ligar tubos", () => LigarAGeral.Ligar(doc, entrada, [alvo], novos));
            return recusa is null
                ? new LigacaoDeTubos("tê", novos.Where(n => n.IsValidObject).Select(n => n.Id).ToList(), null)
                : new LigacaoDeTubos(null, [], recusa);
        }

        // Na ponta solta do tubo de destino (ou além dela): os dois até o canto, com cotovelo.
        var extremo = eixoAlvo.GetEndPoint(t <= margem ? 0 : 1);
        var pontaAlvo = Pontas(alvo).Where(c => !c.IsConnected).FirstOrDefault(c => c.Origin.DistanceTo(extremo) < GeradorMalha.Pes(0.001));
        if (pontaAlvo is null)
            return new LigacaoDeTubos(null, [], "o encontro cai perto de uma conexão do tubo de destino (ou fora dele): clique num tubo que passe em frente à ponta");
        // Quase paralelos, o canto ficaria no infinito.
        if (Math.Abs(sentido.DotProduct(da)) > Math.Cos(Alinhado * Math.PI / 180))
            return new LigacaoDeTubos(null, [], "os tubos estão em linha: estique um até o outro com a ferramenta do Revit (alça da ponta)");
        if (Math.Abs(pontaAlvo.Origin.Z - p.Z) >= LigarAGeral.MesmaAltura)
            return new LigacaoDeTubos(null, [], "para ligar ponta com ponta (cotovelo), os dois tubos precisam estar na mesma altura");

        // O canto: o cruzamento das duas retas em planta.
        var canto = Cruzamento(p, sentido, pontaAlvo.Origin, da);
        if (canto is null || Horizontal(canto - p).DotProduct(sentido) < -margem)
            return new LigacaoDeTubos(null, [], "o canto entre os dois tubos fica para trás da ponta do primeiro");
        var nivel = alvo.ReferenceLevel;
        var motivo = TransacaoSegura.Executar(doc, "SPK — Ligar tubos", () =>
        {
            LigarAGeral.Passo("esticar o primeiro tubo", () => Esticar(doc, chega, p, canto));
            LigarAGeral.Passo("esticar o tubo de destino", () => Esticar(doc, alvo, pontaAlvo.Origin, new XYZ(canto.X, canto.Y, pontaAlvo.Origin.Z)));
            string? m = null;
            LigarAGeral.Passo("cotovelo no canto", () => m = TracarRamal.Juntar(doc, canto, nivel, [chega, alvo]));
            return m;
        });
        return motivo is null ? new LigacaoDeTubos("cotovelo", [], null) : new LigacaoDeTubos(null, [], motivo);
    }

    /// <summary>Leva a ponta <paramref name="de" /> do tubo até <paramref name="ate" />, mantendo a ordem das pontas (invertida, o Revit não muda o tubo).</summary>
    static void Esticar(Document doc, Pipe tubo, XYZ de, XYZ ate)
    {
        var linha = (Line)((LocationCurve)tubo.Location).Curve;
        if (de.DistanceTo(ate) < GeradorMalha.Pes(0.005)) return;
        var inicio = linha.GetEndPoint(0).DistanceTo(de) < linha.GetEndPoint(1).DistanceTo(de);
        ((LocationCurve)tubo.Location).Curve = inicio ? Line.CreateBound(ate, linha.GetEndPoint(1)) : Line.CreateBound(linha.GetEndPoint(0), ate);
        doc.Regenerate();
        if (!Pontas(tubo).Any(c => c.Origin.DistanceTo(ate) < GeradorMalha.Pes(0.02)))
            throw new InvalidOperationException("o Revit não esticou o tubo até o canto");
    }

    static XYZ? Cruzamento(XYZ p, XYZ dp, XYZ q, XYZ dq)
    {
        var det = dp.X * dq.Y - dp.Y * dq.X;
        if (Math.Abs(det) < 1e-9) return null;
        var w = q - p;
        var s = (w.X * dq.Y - w.Y * dq.X) / det;
        return new XYZ(p.X + dp.X * s, p.Y + dp.Y * s, p.Z);
    }

    static Line? Eixo(Pipe t) => t.Location is LocationCurve { Curve: Line l } && Math.Abs(l.Direction.Z) < 0.05 ? l : null;

    static IEnumerable<Connector> Pontas(Pipe t) => t.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End);

    static double DistanciaAoEixo(Line eixo, XYZ p)
    {
        var d = Horizontal(eixo.Direction).Normalize();
        var w = Horizontal(p - eixo.GetEndPoint(0));
        return Math.Abs(w.X * d.Y - w.Y * d.X);
    }

    static XYZ Horizontal(XYZ v) => new(v.X, v.Y, 0);
}
