using Eletrica.Motor.Modelo;
using Eletrica.Motor.Normas;

namespace Eletrica.Motor.Calculo;

/// <summary>Uma parcela da demanda do quadro: categoria, potência instalada, fator aplicado e demanda resultante.</summary>
public sealed record ParcelaDeDemanda(FatoresDeDemanda.Categoria Categoria, double InstaladaVA, int Aparelhos, double Fator, double DemandaVA, string Fonte);

/// <summary>Demanda do quadro: parcelas por categoria mais os quadros filhos (já com a demanda deles) e o fator próprio do quadro.</summary>
public sealed record ResultadoDemanda(IReadOnlyList<ParcelaDeDemanda> Parcelas, double DosQuadrosFilhosVA, double FatorDoQuadro, double InstaladaVA, double DemandaVA);

/// <summary>
/// Potência de demanda por quadro (4.2.1.1): circuitos próprios agrupados por categoria de carga com os fatores de
/// <see cref="FatoresDeDemanda" /> (quando a alimentação manda aplicá-los), mais a demanda dos quadros alimentados,
/// vezes o fator próprio do quadro (1,0 por padrão). Sem fatores, demanda = instalada.
/// </summary>
public static class Demanda
{
    public static ResultadoDemanda Calcular(Instalacao i, Quadro q, Func<Quadro, double> demandaDoFilho)
    {
        var proprios = i.CircuitosDo(q).Where(c => c.QuadroAlimentado is null).ToList();
        var filhos = i.CircuitosDo(q).Where(c => c.QuadroAlimentado is not null).Select(c => i.Quadro(c.QuadroAlimentado!)).Where(f => f is not null).Sum(f => demandaDoFilho(f!));
        var parcelas = new List<ParcelaDeDemanda>();
        var aplicar = i.Alimentacao.AplicarFatoresDeDemanda;
        foreach (var grupo in proprios.GroupBy(c => FatoresDeDemanda.CategoriaDe(c.Tipo)).OrderBy(g => g.Key))
        {
            var instalada = grupo.Sum(c => i.PotenciaDo(c));
            var aparelhos = grupo.Sum(c => Math.Max(1, i.PontosDo(c).Count(p => FatoresDeDemanda.CategoriaDe(p.Tipo) == grupo.Key)));
            double fator;
            double demanda;
            if (!aplicar)
            {
                fator = 1;
                demanda = instalada;
            }
            else
                switch (grupo.Key)
                {
                    case FatoresDeDemanda.Categoria.IluminacaoETug when i.Alimentacao.Habitacao:
                        demanda = FatoresDeDemanda.DemandaIluminacaoETug(instalada);
                        fator = FatoresDeDemanda.FatorIluminacaoETug(instalada);
                        break;
                    case FatoresDeDemanda.Categoria.Aquecimento:
                        fator = FatoresDeDemanda.FatorAquecimento(aparelhos);
                        demanda = instalada * fator;
                        break;
                    case FatoresDeDemanda.Categoria.ArCondicionado:
                        fator = FatoresDeDemanda.FatorArCondicionado(aparelhos);
                        demanda = instalada * fator;
                        break;
                    default:
                        fator = FatoresDeDemanda.FatorUsoEspecifico;
                        demanda = instalada * fator;
                        break;
                }
            parcelas.Add(new ParcelaDeDemanda(grupo.Key, instalada, aparelhos, fator, demanda, aplicar ? FatoresDeDemanda.Fonte : "sem fator de demanda (opção do projeto)"));
        }
        var instaladaTotal = parcelas.Sum(p => p.InstaladaVA) + filhos;
        var total = (parcelas.Sum(p => p.DemandaVA) + filhos) * q.FatorDeDemanda;
        return new ResultadoDemanda(parcelas, filhos, q.FatorDeDemanda, instaladaTotal, total);
    }
}
