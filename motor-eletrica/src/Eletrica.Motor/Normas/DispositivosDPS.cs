using Eletrica.Motor.Modelo;

namespace Eletrica.Motor.Normas;

/// <summary>
/// Dispositivos de proteção contra surtos: quando a NBR 5410 obriga (5.4.2.1.1) e como especificar (6.3.5.2, Tabela 49,
/// Tabela 31), com as classes da IEC 61643-11. Valores do mapa provisório <c>nr/normas/dps-iec-61643.md</c>.
/// </summary>
public static class DispositivosDPS
{
    public const string Documento = "ABNT NBR 5410 5.4.2 e 6.3.5.2; IEC 61643-11";

    public enum Classe { I, II, III }

    public sealed record Especificacao(
        Classe Classe,
        string Esquema,
        int Quantidade,
        double UcMinimo,
        double UcComercial,
        double UpMaximo,
        double InMinimoKA,
        double? IimpMinimoKA,
        double SecaoDeLigacao,
        string Motivo);

    /// <summary>5.4.2.1.1: o DPS é obrigatório com linha aérea em AQ2 (mais de 25 dias de trovoada) ou em AQ3 (SPDA, exposição direta).</summary>
    public static string? MotivoDeObrigatoriedade(Alimentacao a)
    {
        if (a.TemSpda) return "5.4.2.1.1 b) — instalação em situação AQ3 (estrutura com SPDA ou exposta a descarga direta)";
        if (a.LinhaAerea && a.DiasDeTrovoada > Nbr5410.DiasDeTrovoadaAQ2)
            return $"5.4.2.1.1 a) — alimentação por linha aérea em região AQ2 ({a.DiasDeTrovoada} dias de trovoada por ano)";
        return null;
    }

    /// <summary>Especificação do DPS do quadro de entrada (ou de distribuição) pelo esquema de aterramento.</summary>
    public static Especificacao Especificar(Alimentacao a, bool quadroDeEntrada)
    {
        var classe = quadroDeEntrada && (a.TemSpda || a.LinhaAerea) ? Classe.I : Classe.II;
        var uo = a.TensaoFaseNeutro;
        var fases = a.Fases;
        string esquema;
        int quantidade;
        double uc;
        switch (a.Esquema)
        {
            case EsquemaAterramento.TT:
                esquema = $"{fases}+1 (cada fase ao neutro, neutro ao PE por centelhador)";
                quantidade = fases + 1;
                uc = Nbr5410.UcMinimoDoDps(a.Esquema, Nbr5410.ConexaoDps.FaseNeutro, uo, a.TensaoFaseFase);
                break;
            case EsquemaAterramento.IT:
                esquema = $"{fases}+0 (cada fase ao PE)";
                quantidade = fases;
                uc = Nbr5410.UcMinimoDoDps(a.Esquema, Nbr5410.ConexaoDps.FasePE, uo, a.TensaoFaseFase);
                break;
            case EsquemaAterramento.TNC:
                esquema = $"{fases}+0 (cada fase ao PEN)";
                quantidade = fases;
                uc = Nbr5410.UcMinimoDoDps(a.Esquema, Nbr5410.ConexaoDps.FasePEN, uo, a.TensaoFaseFase);
                break;
            default:
                esquema = $"{fases}+1 (cada fase e o neutro ao PE)";
                quantidade = fases + 1;
                uc = Nbr5410.UcMinimoDoDps(a.Esquema, Nbr5410.ConexaoDps.FasePE, uo, a.TensaoFaseFase);
                break;
        }
        var comercial = UcComercial(uc);
        var up = Nbr5410.UpMaximoDps(uo);
        var inKA = Math.Max(Nbr5410.InMinimoDps, a.EhTrifasica ? 20 : 10);
        double? iimp = classe == Classe.I ? Nbr5410.IimpMinimoDps : null;
        var secao = classe == Classe.I && a.TemSpda ? Nbr5410.SecaoLigacaoDpsEntrada : Nbr5410.SecaoLigacaoDpsQuadro;
        var motivo = classe == Classe.I
            ? "classe I na entrada por exposição a descarga direta ou linha aérea (6.3.5.2.1 e NBR 5419-4)"
            : "classe II para sobretensões induzidas ou conduzidas (6.3.5.2.1)";
        return new Especificacao(classe, esquema, quantidade, uc, comercial, up, inKA, iimp, secao, motivo);
    }

    /// <summary>Valores comerciais de Uc dos catálogos (175, 275, 385, 440 V).</summary>
    public static double UcComercial(double minimo)
    {
        foreach (var v in new[] { 175.0, 275.0, 385.0, 440.0, 600.0 })
            if (v >= minimo - 1e-9) return v;
        return Math.Ceiling(minimo);
    }
}
