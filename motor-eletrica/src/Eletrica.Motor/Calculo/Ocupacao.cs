using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Calculo;

/// <summary>
/// Eletrodutos trecho a trecho: taxa de ocupação (6.2.11.1.6 a) com o diâmetro externo real de cada condutor que passa,
/// DN sugerido, trecho contínuo máximo (15 m internos, 30 m externos, menos 3 m por curva) e número de curvas.
/// </summary>
public static class Ocupacao
{
    public static IReadOnlyList<ResultadoTrecho> Avaliar(Instalacao i, IReadOnlyDictionary<string, ResultadoCircuito> circuitos)
    {
        var lista = new List<ResultadoTrecho>();
        foreach (var t in i.Trechos)
        {
            var pareceres = new List<Parecer>();
            double area = 0;
            var condutores = 0;
            foreach (var id in t.Circuitos.Distinct())
            {
                var c = i.Circuito(id)!;
                if (!circuitos.TryGetValue(id, out var r)) continue;
                var flexivel = true;
                var n = c.Fases + (c.TemNeutro ? 1 : 0);
                area += n * Condutores.AreaExterna(r.SecaoFase, c.Isolacao, flexivel);
                area += Condutores.AreaExterna(r.SecaoPE, c.Isolacao, flexivel);
                condutores += n + 1;
            }
            foreach (var id in t.Retornos)
            {
                var c = i.Circuito(id);
                if (c is null || !circuitos.TryGetValue(id, out var r)) continue;
                area += Condutores.AreaExterna(r.SecaoFase, c.Isolacao, true);
                condutores++;
            }

            int? dnSugerido = null;
            double? taxa = null;
            var taxaMaxima = Nbr5410.TaxaDeOcupacaoMaxima(Math.Max(1, condutores));
            if (t.TipoEletroduto != TipoEletroduto.Eletrocalha && condutores > 0)
            {
                dnSugerido = Eletrodutos.MenorQueComporta(t.TipoEletroduto, area, condutores)?.Dn;
                if (t.Dn is { } dn)
                {
                    var tamanho = Eletrodutos.PorDn(t.TipoEletroduto, dn);
                    if (tamanho is null)
                        pareceres.Add(new Parecer("eletroduto", false, $"DN {dn}", "tamanho da NBR 15465", "6.2.11.1.1, NBR 15465 Tabelas 4 e 5",
                            $"DN {dn} não existe para {t.TipoEletroduto}"));
                    else
                    {
                        taxa = Eletrodutos.TaxaDeOcupacao(tamanho, area);
                        pareceres.Add(new Parecer("taxa de ocupação", taxa <= taxaMaxima + 1e-9, $"{taxa * 100:0} % (DN {dn}, {condutores} condutores, {area:0} mm²)",
                            $"≤ {taxaMaxima * 100:0} %", "6.2.11.1.6 a)",
                            dnSugerido is { } s && s != dn ? $"menor DN que atende: {s}" : "soma das áreas externas dos condutores sobre a área interna do eletroduto"));
                        if (dn < Nbr5410.DnMinimo)
                            pareceres.Add(new Parecer("tamanho mínimo", false, $"DN {dn}", $"≥ DN {Nbr5410.DnMinimo}", "6.2.11.1.5", "eletroduto menor que o mínimo em uso normal"));
                    }
                }
                else
                {
                    pareceres.Add(new Parecer("taxa de ocupação", dnSugerido is not null, $"{condutores} condutores, {area:0} mm²",
                        dnSugerido is { } s ? $"DN {s} ({t.TipoEletroduto})" : "nenhum DN tabelado basta", "6.2.11.1.6 a)",
                        dnSugerido is not null ? "DN sugerido pela taxa de ocupação" : "repetir o eletroduto ou trocar por eletrocalha"));
                }
            }

            var equivalente = t.Comprimento + Nbr5410.DescontoPorCurva * t.Curvas90;
            var maximo = t.Externo ? Nbr5410.TrechoMaximoExterno : Nbr5410.TrechoMaximoInterno;
            pareceres.Add(new Parecer("trecho entre caixas", equivalente <= maximo + 1e-9, $"{t.Comprimento:0.0} m + {t.Curvas90} curva(s) × 3 m = {equivalente:0.0} m",
                $"≤ {maximo:0} m", "6.2.11.1.6 b)", equivalente > maximo ? "pôr caixa de passagem ou subir o DN (um tamanho a cada 6 m de excesso)" : "trecho contínuo dentro do limite"));
            pareceres.Add(new Parecer("curvas", t.Curvas90 <= Nbr5410.CurvasMaximasEntreCaixas, $"{t.Curvas90} curva(s) de 90°", "≤ 3", "6.2.11.1.7",
                t.Curvas90 > 3 ? "pôr caixa de passagem" : "dentro do limite"));

            lista.Add(new ResultadoTrecho(t.Id, t.De, t.Para, t.Comprimento, t.Curvas90, t.TipoEletroduto, t.Dn, dnSugerido, condutores, area, taxa, taxaMaxima,
                equivalente, maximo, pareceres));
        }
        return lista;
    }
}
