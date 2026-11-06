using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Normas;

/// <summary>
/// Catálogo de componentes do motor (item 1.4 do plano): o que existe para escolher, com a norma de cada família. Os
/// valores vêm das classes de norma; aqui ficam reunidos para a lista de material, as janelas e os testes. Dados de
/// fabricante (I²t por modelo, Up e Iimp de DPS específicos) entram por cima quando o escritório os informar.
/// </summary>
public static class Catalogo
{
    public sealed record ItemDisjuntor(double In, CurvaDisjuntor Curva, double IcnKA, int Polos)
    {
        public override string ToString() => $"Disjuntor {Curva}{In:0.#} A, {IcnKA:0.#} kA, {Polos}P (NBR NM 60898)";
    }

    public sealed record ItemDR(double In, double CorrenteResidualMA, TipoDr Tipo, int Polos)
    {
        public override string ToString() => $"DR {In:0} A, {CorrenteResidualMA:0} mA, tipo {Tipo}, {Polos}P (IEC 61008-1)";
    }

    public sealed record ItemCondutor(double Secao, Isolacao Isolacao, MaterialCondutor Material, bool Flexivel, double DiametroExternoMm)
    {
        public override string ToString() => $"Cabo {Secao:0.#} mm² {Material} {Isolacao}{(Flexivel ? " flexível" : "")} (NBR NM 247-3 / 7286-7288), Ø {DiametroExternoMm:0.0} mm";
    }

    public sealed record ItemEletroduto(TipoEletroduto Tipo, int Dn, double DiametroInternoMm)
    {
        public override string ToString() => $"Eletroduto {Tipo} DN {Dn} (NBR 15465), Ø interno {DiametroInternoMm:0.0} mm";
    }

    public sealed record ItemTomada(int CorrenteA, string Norma = "NBR 14136:2012");

    /// <summary>Disjuntores padronizados: In preferenciais × curvas B, C e D × Icn usuais no mercado × polos.</summary>
    public static IEnumerable<ItemDisjuntor> Disjuntores()
    {
        double[] icns = [3, 4.5, 6, 10];
        foreach (var inA in Nbr60898.CorrentesNominais)
        foreach (var curva in new[] { CurvaDisjuntor.B, CurvaDisjuntor.C, CurvaDisjuntor.D })
        foreach (var icn in icns)
        foreach (var polos in new[] { 1, 2, 3, 4 })
            yield return new ItemDisjuntor(inA, curva, icn, polos);
    }

    public static IEnumerable<ItemDR> DRs()
    {
        foreach (var inA in DispositivosDR.CorrentesNominais)
        foreach (var residual in DispositivosDR.CorrentesResiduais)
        foreach (var tipo in new[] { TipoDr.AC, TipoDr.A, TipoDr.F, TipoDr.B })
        foreach (var polos in new[] { 2, 4 })
            yield return new ItemDR(inA, residual, tipo, polos);
    }

    public static IEnumerable<ItemCondutor> Condutores()
    {
        foreach (var secao in Normas.Condutores.SecoesDeProjeto)
        foreach (var isolacao in new[] { Isolacao.Pvc70, Isolacao.Epr90 })
        foreach (var flexivel in new[] { true, false })
            yield return new ItemCondutor(secao, isolacao, MaterialCondutor.Cobre, flexivel, Normas.Condutores.DiametroExterno(secao, isolacao, flexivel));
    }

    public static IEnumerable<ItemEletroduto> Eletrodutos()
    {
        foreach (var tipo in new[] { TipoEletroduto.RigidoRoscavel, TipoEletroduto.RigidoSoldavel, TipoEletroduto.FlexivelCorrugado, TipoEletroduto.Aco })
        foreach (var t in Normas.Eletrodutos.Tamanhos(tipo))
            yield return new ItemEletroduto(tipo, t.Dn, t.DiametroInterno);
    }

    public static IEnumerable<ItemTomada> Tomadas() => [new ItemTomada(10), new ItemTomada(20)];

    /// <summary>O disjuntor do catálogo que atende: menor In ≥ pedido, curva pedida, menor Icn ≥ Icc.</summary>
    public static ItemDisjuntor? Disjuntor(double inMinimo, CurvaDisjuntor curva, double iccKA, int polos) =>
        Disjuntores().Where(d => d.Curva == curva && d.Polos == polos && d.In >= inMinimo - 1e-9 && d.IcnKA >= iccKA - 1e-9)
            .OrderBy(d => d.In).ThenBy(d => d.IcnKA).FirstOrDefault();
}
