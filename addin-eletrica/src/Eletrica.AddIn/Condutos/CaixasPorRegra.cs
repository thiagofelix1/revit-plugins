using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Structure;
using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Normas;

namespace Eletrica.AddIn.Condutos;

/// <summary>
///     Caixas de passagem por regra (item 4.3 do plano, NBR 5410 6.2.11.1.6 b e 6.2.11.1.7): anda em cada trecho entre
///     caixas somando comprimento e curvas (3 m por curva de 90°); onde o trecho passaria de 15 m (30 m externo) ou da
///     terceira curva, parte o eletroduto e põe a caixa do escritório ali. Sem família de caixa confirmada, só lista onde
///     elas faltam. Cada caixa numa transação própria; um Ctrl+Z desfaz tudo.
/// </summary>
static class CaixasPorRegra
{
    public sealed record Necessidade(GrafoDeCondutos.Cadeia Cadeia, Conduit Eletroduto, XYZ Ponto, string Motivo);

    public sealed record Resultado(IReadOnlyList<Necessidade> Necessarias, IReadOnlyList<ElementId> Criadas, IReadOnlyList<string> Avisos);

    static readonly double Margem = Unidades.DeMetros(0.30);

    public static List<Necessidade> Onde(GrafoDeCondutos grafo, bool externo = false)
    {
        var lista = new List<Necessidade>();
        var maximo = externo ? Nbr5410.TrechoMaximoExterno : Nbr5410.TrechoMaximoInterno;
        foreach (var cadeia in grafo.Cadeias)
        {
            var percurso = grafo.Percurso(cadeia);
            if (percurso.Count < 2) continue;
            double acumulado = 0; // m desde a última caixa
            var curvas = 0;
            XYZ? direcaoAnterior = null;
            for (var i = 1; i < percurso.Count; i++)
            {
                var a = percurso[i - 1];
                var b = percurso[i];
                var l = Unidades.Metros(a.DistanceTo(b));
                if (l < 1e-6) continue;
                var direcao = (b - a).Normalize();
                if (direcaoAnterior is not null && Math.Abs(direcaoAnterior.DotProduct(direcao)) < Math.Cos(30 * Math.PI / 180)) curvas++;
                direcaoAnterior = direcao;
                var eletroduto = EletrodutoDe(cadeia, a, b);
                if (eletroduto is null) { acumulado += l; continue; }

                if (curvas > Nbr5410.CurvasMaximasEntreCaixas)
                {
                    var p = a + direcao * Math.Min(Margem, a.DistanceTo(b) / 2);
                    lista.Add(new Necessidade(cadeia, eletroduto, p, $"{curvas} curvas de 90° sem caixa (máximo 3)"));
                    acumulado = Unidades.Metros(b.DistanceTo(p));
                    curvas = 0;
                    continue;
                }
                var disponivel = maximo - Nbr5410.DescontoPorCurva * curvas - acumulado;
                if (l > disponivel + 1e-9)
                {
                    // A caixa fica onde o limite é atingido, mas pelo menos a 30 cm das pontas do eletroduto.
                    var s = Math.Max(Unidades.Metros(Margem), Math.Min(l - Unidades.Metros(Margem), disponivel));
                    if (l <= 2 * Unidades.Metros(Margem)) s = l / 2;
                    var p = a + direcao * Unidades.DeMetros(s);
                    lista.Add(new Necessidade(cadeia, eletroduto, p,
                        $"{acumulado + s:0.0} m + {curvas} curva(s) × 3 m = {acumulado + s + 3 * curvas:0.0} m desde a última caixa (máximo {maximo:0} m)"));
                    acumulado = l - s;
                    curvas = 0;
                    continue;
                }
                acumulado += l;
            }
        }
        return lista;
    }

    static Conduit? EletrodutoDe(GrafoDeCondutos.Cadeia cadeia, XYZ a, XYZ b)
    {
        var meio = (a + b) / 2;
        return cadeia.Conduites.Where(c => c.Location is LocationCurve lc && lc.Curve.Distance(meio) < Unidades.DeMetros(0.02))
            .OrderBy(c => ((LocationCurve)c.Location).Curve.Distance(meio)).FirstOrDefault();
    }

    public static Resultado Executar(Document doc, GrafoDeCondutos grafo, PadraoEletrica padrao)
    {
        var necessarias = Onde(grafo);
        var avisos = new List<string>();
        var criadas = new List<ElementId>();
        if (necessarias.Count == 0) return new Resultado(necessarias, criadas, avisos);
        var simbolo = padrao.Simbolo(doc, PapelFamilia.CaixaDePassagem);
        if (simbolo is null)
        {
            avisos.Add("Nenhuma família confirmada como Caixa de passagem em Famílias: as caixas não foram inseridas, só apontadas.");
            return new Resultado(necessarias, criadas, avisos);
        }
        if (Lancamento.Colocacao.Hospedada(simbolo))
        {
            avisos.Add($"A caixa \"{PadraoEletrica.Nome(simbolo)}\" é família hospedada; para inserir por regra use uma caixa livre (nível).");
            return new Resultado(necessarias, criadas, avisos);
        }

        using var grupo = new TransactionGroup(doc, "AutoEletrica — Caixas por regra");
        grupo.Start();
        // Do fim para o começo de cada eletroduto, para que partir um não mude o ponto das outras caixas no mesmo eletroduto.
        foreach (var n in necessarias.OrderBy(n => n.Eletroduto.Id.Value).ThenByDescending(n => n.Ponto.DistanceTo(((LocationCurve)n.Eletroduto.Location).Curve.GetEndPoint(0))))
        {
            var motivo = TransacaoSegura.Executar(doc, "AutoEletrica — Caixa de passagem", () =>
            {
                var nivel = doc.GetElement(n.Eletroduto.ReferenceLevel?.Id ?? ElementId.InvalidElementId) as Level
                            ?? Lancamento.Colocacao.NivelAbaixo(doc, n.Ponto.Z) ?? throw new InvalidOperationException("sem nível");
                TracarEletroduto.Partir(doc, n.Eletroduto, n.Ponto, nivel);
                Lancamento.Colocacao.Ativar(simbolo);
                var caixa = doc.Create.NewFamilyInstance(new XYZ(n.Ponto.X, n.Ponto.Y, nivel.ProjectElevation), simbolo, nivel, StructuralType.NonStructural);
                doc.Regenerate();
                var atual = (caixa.Location as LocationPoint)?.Point.Z ?? nivel.ProjectElevation;
                if (Math.Abs(n.Ponto.Z - atual) > 1e-6) ElementTransformUtils.MoveElement(doc, caixa.Id, new XYZ(0, 0, n.Ponto.Z - atual));
                criadas.Add(caixa.Id);
                return null;
            });
            if (motivo is not null) avisos.Add($"Caixa em ({Unidades.Metros(n.Ponto.X):0.00}, {Unidades.Metros(n.Ponto.Y):0.00}) não inserida: {motivo}");
        }
        grupo.Assimilate();
        return new Resultado(necessarias, criadas, avisos);
    }
}
