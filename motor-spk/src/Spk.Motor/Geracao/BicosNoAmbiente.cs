using Spk.Motor.Modelo;
using Spk.Motor.Normas;

namespace Spk.Motor.Geracao;

/// <summary>Os bicos de um ambiente e a direção dos ramais usada para posicioná-los.</summary>
public sealed record PosicaoDeBicos(IReadOnlyList<Ponto2> Bicos, Ponto2 DirecaoDosRamais, double PassoNoRamal, double PassoEntreRamais);

public enum TipoDeFalha { AreaPorBico, DistanciaEntreBicos, PertoDeOutroBico, PertoDaParede, LongeDaParede, PontoDescoberto, ForaDoAmbiente, Defletor }

/// <summary>Uma regra de espaçamento não atendida, com o ponto em planta (m) e a explicação.</summary>
public sealed record FalhaDeEspacamento(TipoDeFalha Tipo, Ponto2 Onde, string Descricao);

/// <summary>
/// Posiciona e confere bicos spray de cobertura-padrão num ambiente (<see cref="EspacamentoDeBicos" />). O ambiente é o
/// contorno em planta (o primeiro laço é o externo, os outros são vazios), em metros. Os ramais seguem a direção dada ou,
/// sem ela, o lado mais longo do ambiente.
/// </summary>
public static class BicosNoAmbiente
{
    const double Amostra = 0.25;
    const double Folga = 1e-6;

    /// <summary>
    /// Grade alinhada aos ramais com o menor número de bicos que atende a distância máxima e a área máxima por bico; os
    /// bicos ficam no meio de cada célula (meio passo da parede). Num ambiente que não é retângulo, os bicos fora dele
    /// saem e os cantos sem cobertura ganham bico, um por vez.
    /// </summary>
    public static PosicaoDeBicos Posicionar(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, LimitesDeBico limites, Ponto2? direcao = null)
    {
        var externo = contornos[0];
        var (u, v) = Eixos(externo, direcao);
        var us = externo.Select(p => Dot(p, u)).ToList();
        var vs = externo.Select(p => Dot(p, v)).ToList();
        var (u0, u1, v0, v1) = (us.Min(), us.Max(), vs.Min(), vs.Max());
        if (direcao is null && v1 - v0 > u1 - u0)
        {
            (u, v) = Eixos(externo, v);
            us = externo.Select(p => Dot(p, u)).ToList();
            vs = externo.Select(p => Dot(p, v)).ToList();
            (u0, u1, v0, v1) = (us.Min(), us.Max(), vs.Min(), vs.Max());
        }

        var (nu, nv) = Contagem(u1 - u0, v1 - v0, limites);
        var su = (u1 - u0) / nu;
        var sv = (v1 - v0) / nv;
        var bicos = new List<Ponto2>();
        bool Valido(Ponto2 p) => Dentro(contornos, p) && DistanciaAoContorno(contornos, p) >= limites.ParedeMinima;

        // Paredes em ângulo reto (a maioria): o ambiente é cortado em retângulos, e cada um ganha a sua grade, do maior
        // para o menor; um pedaço já coberto pelos bicos dos maiores fica sem. Senão, uma grade só no retângulo envolvente.
        var retangulos = Ortogonal(contornos, u, v) ? Retangulos(contornos, u, v) : [(u0, u1, v0, v1)];
        foreach (var (a0, a1, b0, b1) in retangulos.OrderByDescending(r => (r.U1 - r.U0) * (r.V1 - r.V0)))
        {
            var dentro = Amostras(contornos).Where(p => Dot(p, u) > a0 && Dot(p, u) < a1 && Dot(p, v) > b0 && Dot(p, v) < b1).ToList();
            if (bicos.Count > 0 && dentro.All(p => Coberto(contornos, bicos, p, limites))) continue;
            var (ni, nj) = Contagem(a1 - a0, b1 - b0, limites);
            for (var j = 0; j < nj; j++)
            for (var i = 0; i < ni; i++)
            {
                var p = Ponto(u, v, a0 + (a1 - a0) / ni * (i + 0.5), b0 + (b1 - b0) / nj * (j + 0.5));
                if (Valido(p)) bicos.Add(p);
            }
        }

        // Cantos sem cobertura (ambiente que não é retângulo): um bico no meio do pedaço descoberto, até cobrir.
        for (var tentativa = 0; tentativa < 200; tentativa++)
        {
            var descobertos = Amostras(contornos).Where(p => !Coberto(contornos, bicos, p, limites)).ToList();
            if (descobertos.Count == 0) break;
            var semente = descobertos[0];
            var grupo = descobertos.Where(p => Dist(p, semente) <= limites.AlcanceMaximo).ToList();
            var centro = new Ponto2(grupo.Average(p => p.X), grupo.Average(p => p.Y));
            var lugar = grupo.Append(centro)
                .Where(p => Dentro(contornos, p) && DistanciaAoContorno(contornos, p) >= limites.ParedeMinima)
                .OrderBy(p => Dist(p, centro)).Cast<Ponto2?>().FirstOrDefault();
            if (lugar is null) break;
            bicos.Add(lugar.Value);
        }
        return new PosicaoDeBicos(Alinhar(contornos, bicos, u, v, limites), u, su, sv);
    }

    /// <summary>
    /// Bicos nos vãos entre vigas (8.13.1.2, quando a viga é funda demais para o bico ficar abaixo dela): as vigas,
    /// faixas com a largura delas, cortam o ambiente em vãos; com vigas nas duas direções (a grelha, o quadro de vigas
    /// de cada painel de laje), em quadros. Cada vão ou quadro ganha a sua grade (meio passo das vigas e das paredes,
    /// como um ambiente pequeno), e nenhum bico fica em cima de viga nem a menos da distância mínima da parede. Os ramais
    /// seguem a direção da primeira viga. Nulo se há viga fora das duas direções (até 10° de folga).
    /// </summary>
    /// <param name="vigas">Eixo de cada viga em planta (A, B) e a largura dela, em m.</param>
    public static PosicaoDeBicos? PosicionarPorVaos(IReadOnlyList<IReadOnlyList<Ponto2>> contornos,
        IReadOnlyList<(Ponto2 A, Ponto2 B, double Largura)> vigas, LimitesDeBico limites)
    {
        if (vigas.Count == 0) return Posicionar(contornos, limites);
        var direcoes = vigas.Select(g => Normalizar(Sub(g.B, g.A))).ToList();
        var folga = Math.Cos(80 * Math.PI / 180);
        var paralelas = vigas.Where((_, i) => Math.Abs(Dot(direcoes[i], direcoes[0])) > Math.Cos(10 * Math.PI / 180)).ToList();
        var cruzadas = vigas.Where((_, i) => Math.Abs(Dot(direcoes[i], direcoes[0])) < folga).ToList();
        if (paralelas.Count + cruzadas.Count < vigas.Count) return null;
        var (u, v) = Eixos(contornos[0], direcoes[0]);
        var us = contornos[0].Select(p => Dot(p, u)).ToList();
        var vs = contornos[0].Select(p => Dot(p, v)).ToList();
        var (u0, u1, v0, v1) = (us.Min(), us.Max(), vs.Min(), vs.Max());

        // Vãos na direção v (entre as vigas ao longo de u) e na direção u (entre as vigas que as cruzam): os quadros.
        var vaosV = Vaos(paralelas, v, v0, v1, limites);
        var vaosU = Vaos(cruzadas, u, u0, u1, limites);

        bool Valido(Ponto2 p) => Dentro(contornos, p) && DistanciaAoContorno(contornos, p) >= limites.ParedeMinima - Folga;
        var bicos = new List<Ponto2>();
        var (su, sv) = (0.0, 0.0);
        foreach (var (a, b) in vaosV)
        foreach (var (c, d) in vaosU)
        {
            var (ni, nj) = Contagem(d - c, b - a, limites);
            (su, sv) = (Math.Max(su, (d - c) / ni), Math.Max(sv, (b - a) / nj));
            for (var j = 0; j < nj; j++)
            for (var i = 0; i < ni; i++)
            {
                var p = Ponto(u, v, c + (d - c) / ni * (i + 0.5), a + (b - a) / nj * (j + 0.5));
                if (Valido(p)) bicos.Add(p);
            }
        }
        return new PosicaoDeBicos(bicos, u, su, sv);
    }

    /// <summary>
    /// Os vãos ao longo do eixo entre as faixas destas vigas (todas atravessadas a ele) e as paredes; faixas que se
    /// encostam viram uma. Sem viga, o vão é o ambiente inteiro.
    /// </summary>
    static List<(double A, double B)> Vaos(IReadOnlyList<(Ponto2 A, Ponto2 B, double Largura)> vigas, Ponto2 eixo, double inicio, double fim,
        LimitesDeBico limites)
    {
        var faixas = vigas.Select(g => (Meio: (Dot(g.A, eixo) + Dot(g.B, eixo)) / 2, Meia: g.Largura / 2))
            .Select(f => (A: f.Meio - f.Meia, B: f.Meio + f.Meia)).OrderBy(f => f.A).ToList();
        var juntas = new List<(double A, double B)>();
        foreach (var f in faixas)
            if (juntas.Count > 0 && f.A <= juntas[juntas.Count - 1].B)
                juntas[juntas.Count - 1] = (juntas[juntas.Count - 1].A, Math.Max(juntas[juntas.Count - 1].B, f.B));
            else juntas.Add(f);
        var vaos = new List<(double A, double B)>();
        var de = inicio;
        foreach (var f in juntas.Where(f => f.B > inicio && f.A < fim))
        {
            if (f.A - de > 2 * limites.ParedeMinima) vaos.Add((de, f.A));
            de = f.B;
        }
        if (fim - de > 2 * limites.ParedeMinima) vaos.Add((de, fim));
        return vaos;
    }

    /// <summary>Se há vigas nas duas direções (grelha): a rede não cabe no vão e passa por baixo delas.</summary>
    public static bool EhGrelha(IReadOnlyList<(Ponto2 A, Ponto2 B, double Largura)> vigas)
    {
        if (vigas.Count < 2) return false;
        var d0 = Normalizar(Sub(vigas[0].B, vigas[0].A));
        return vigas.Skip(1).Any(g => Math.Abs(Dot(Normalizar(Sub(g.B, g.A)), d0)) < Math.Cos(80 * Math.PI / 180));
    }

    /// <summary>
    /// Abaixo disso, dois bicos fora da mesma linha de ramal não têm como ligar: não cabe o braço com cotovelo, e a
    /// descida reta sai torta. É o braço mínimo de Ligar bicos.
    /// </summary>
    public const double DesalinhamentoMaximo = 0.15;

    /// <summary>
    /// Bicos a menos disso um do outro na direção entre ramais são da mesma fileira: fileiras de verdade ficam a pelo menos
    /// a distância mínima entre bicos (1,8 m), e Traçar ramais junta numa linha só o que estiver a menos de 1 m.
    /// </summary>
    public const double MesmaFileira = 0.9;

    /// <summary>
    /// Fileiras quase alinhadas (os pedaços de um ambiente que não é retângulo ganham grades próprias): os bicos a menos
    /// de <see cref="MesmaFileira" /> um do outro na direção entre ramais vão para a mesma linha: a de mais bicos, ou outra
    /// das linhas, ou a média, a primeira em que os bicos continuam dentro e longe da parede e a conferência não piora.
    /// Sem isso, o ramal passava entre as linhas e deixava bicos a poucos cm dele, sem como ligar.
    /// </summary>
    static List<Ponto2> Alinhar(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, List<Ponto2> bicos, Ponto2 u, Ponto2 v, LimitesDeBico limites)
    {
        var atuais = bicos.ToList();
        var falhas = Conferir(contornos, atuais, limites, u).Count;
        var ordem = Enumerable.Range(0, atuais.Count).OrderBy(i => Dot(atuais[i], v)).ToList();
        for (var k = 0; k < ordem.Count;)
        {
            var inicio = Dot(atuais[ordem[k]], v);
            var fileira = ordem.Skip(k).TakeWhile(i => Dot(atuais[i], v) - inicio < MesmaFileira).ToList();
            k += fileira.Count;
            var linhas = fileira.GroupBy(i => Math.Round(Dot(atuais[i], v), 3)).ToList();
            if (linhas.Count < 2) continue;
            var alvos = linhas.OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key)
                .Append(fileira.Average(i => Dot(atuais[i], v)));
            foreach (var alvo in alvos)
            {
                var tentativa = atuais.ToList();
                foreach (var i in fileira) tentativa[i] = Ponto(u, v, Dot(atuais[i], u), alvo);
                if (fileira.Any(i => !Dentro(contornos, tentativa[i]) || DistanciaAoContorno(contornos, tentativa[i]) < limites.ParedeMinima - Folga))
                    continue;
                var novas = Conferir(contornos, tentativa, limites, u).Count;
                if (novas > falhas) continue;
                atuais = tentativa;
                falhas = novas;
                break;
            }
        }
        return atuais;
    }

    /// <summary>
    /// Confere bicos já posicionados: área por bico (S × L, Figura 2), distância máxima e mínima entre bicos, distância
    /// mínima e máxima à parede, e cobertura do piso (¾ da distância máxima, 8.12.2.2.2).
    /// </summary>
    public static IReadOnlyList<FalhaDeEspacamento> Conferir(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, IReadOnlyList<Ponto2> bicos,
        LimitesDeBico limites, Ponto2? direcao = null)
    {
        var falhas = new List<FalhaDeEspacamento>();
        if (bicos.Count == 0)
        {
            var c = contornos[0];
            falhas.Add(new FalhaDeEspacamento(TipoDeFalha.PontoDescoberto, new Ponto2(c.Average(p => p.X), c.Average(p => p.Y)), "ambiente sem bico"));
            return falhas;
        }
        var (u, v) = direcao is { } d ? Eixos(contornos[0], d) : EixosDosBicos(contornos[0], bicos);

        foreach (var b in bicos)
        {
            if (!Dentro(contornos, b))
            {
                falhas.Add(new FalhaDeEspacamento(TipoDeFalha.ForaDoAmbiente, b, "bico fora do ambiente"));
                continue;
            }
            var parede = DistanciaAoContorno(contornos, b);
            if (parede < limites.ParedeMinima - Folga)
                falhas.Add(new FalhaDeEspacamento(TipoDeFalha.PertoDaParede, b,
                    $"a {parede * 100:0} cm da parede (mínimo {limites.ParedeMinima * 100:0} cm, 8.12.3)"));
            var vizinho = bicos.Where(o => !o.Equals(b)).Select(o => Dist(o, b)).DefaultIfEmpty(double.MaxValue).Min();
            if (vizinho < limites.DistanciaMinima - Folga)
                falhas.Add(new FalhaDeEspacamento(TipoDeFalha.PertoDeOutroBico, b,
                    $"a {vizinho:0.00} m de outro bico (mínimo {limites.DistanciaMinima:0.0} m, 8.12.4)"));

            var s = Lado(contornos, bicos, b, u, v);
            var l = Lado(contornos, bicos, b, v, u);
            if (s > limites.DistanciaMaxima + Folga || l > limites.DistanciaMaxima + Folga)
                falhas.Add(new FalhaDeEspacamento(TipoDeFalha.DistanciaEntreBicos, b,
                    $"S = {s:0.00} m, L = {l:0.00} m (máximo {limites.DistanciaMaxima:0.0} m, contando o dobro da distância à parede, Tabela 2)"));
            if (s * l > limites.AreaMaxima + Folga)
                falhas.Add(new FalhaDeEspacamento(TipoDeFalha.AreaPorBico, b,
                    $"área por bico {s * l:0.0} m² = {s:0.00} × {l:0.00} (máximo {limites.AreaMaxima:0.0} m², Tabela 2)"));
        }

        var descobertos = Amostras(contornos).Where(p => !Coberto(contornos, bicos, p, limites)).ToList();
        var vistos = new List<Ponto2>();
        foreach (var p in descobertos)
        {
            if (vistos.Any(o => Dist(o, p) < limites.DistanciaMaxima / 2)) continue;
            vistos.Add(p);
            var naParede = DistanciaAoContorno(contornos, p) < Amostra;
            falhas.Add(new FalhaDeEspacamento(naParede ? TipoDeFalha.LongeDaParede : TipoDeFalha.PontoDescoberto, p, naParede
                ? $"parede sem bico a até {limites.ParedeMaxima:0.00} m (8.12.2.2.1)"
                : $"ponto do piso a mais de {limites.AlcanceMaximo:0.00} m do bico mais próximo (8.12.2.2.2)"));
        }
        return falhas;
    }

    /// <summary>Todas as paredes paralelas a um dos eixos.</summary>
    static bool Ortogonal(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, Ponto2 u, Ponto2 v) =>
        contornos.All(laco => Enumerable.Range(0, laco.Count).All(i =>
        {
            var e = Normalizar(Sub(laco[(i + 1) % laco.Count], laco[i]));
            return Math.Abs(Dot(e, u)) > 0.999 || Math.Abs(Dot(e, v)) > 0.999;
        }));

    /// <summary>
    /// Abaixo disso, um recorte do contorno é detalhe da parede (pilar, recuo, degrau de modelagem), não outra parte da
    /// sala: a grade não se divide por ele. Bico que cair dentro do recorte sai e o canto descoberto ganha outro, como em
    /// qualquer ambiente; a conferência continua pelo contorno exato.
    /// </summary>
    const double Detalhe = 0.60;

    /// <summary>
    /// Ambiente de paredes em ângulo reto cortado em retângulos (u0, u1, v0, v1): faixas entre os vértices ao longo de u,
    /// cada uma com os trechos de v que ficam dentro. Faixas vizinhas com os mesmos trechos (a menos de um detalhe) se juntam,
    /// e faixa estreita entra na vizinha; vazio menor que um detalhe (pilar solto) não corta a sala.
    /// </summary>
    static List<(double U0, double U1, double V0, double V1)> Retangulos(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, Ponto2 u, Ponto2 v)
    {
        var lacos = contornos.Select((l, i) => (Laco: l.Select(p => (U: Dot(p, u), V: Dot(p, v))).ToList(), Externo: i == 0))
            .Where(l => l.Externo || Math.Min(l.Laco.Max(p => p.U) - l.Laco.Min(p => p.U), l.Laco.Max(p => p.V) - l.Laco.Min(p => p.V)) >= Detalhe)
            .Select(l => l.Laco).ToList();
        var cortes = lacos.SelectMany(l => l.Select(p => Math.Round(p.U, 4))).Distinct().OrderBy(x => x).ToList();
        var faixas = new List<Faixa>();
        for (var k = 0; k + 1 < cortes.Count; k++)
        {
            var meio = (cortes[k] + cortes[k + 1]) / 2;
            var cruzamentos = new List<double>();
            foreach (var l in lacos)
                for (var i = 0; i < l.Count; i++)
                {
                    var (p, q) = (l[i], l[(i + 1) % l.Count]);
                    if ((p.U > meio) != (q.U > meio)) cruzamentos.Add(p.V + (q.V - p.V) * (meio - p.U) / (q.U - p.U));
                }
            cruzamentos.Sort();
            var trechos = new List<(double V0, double V1)>();
            for (var i = 0; i + 1 < cruzamentos.Count; i += 2)
                if (cruzamentos[i + 1] - cruzamentos[i] >= Detalhe) trechos.Add((cruzamentos[i], cruzamentos[i + 1]));
            var faixa = new Faixa(cortes[k], cortes[k + 1], trechos, trechos);
            if (faixas.Count == 0)
            {
                faixas.Add(faixa);
                continue;
            }
            var anterior = faixas[faixas.Count - 1];
            if (Parecidos(anterior.Base, trechos))
                faixas[faixas.Count - 1] = anterior with { B = faixa.B, Trechos = Envolver(anterior.Trechos, trechos) };
            else if (faixa.B - faixa.A < Detalhe)
                faixas[faixas.Count - 1] = anterior with { B = faixa.B };
            else if (anterior.B - anterior.A < Detalhe)
                faixas[faixas.Count - 1] = faixa with { A = anterior.A };
            else
                faixas.Add(faixa);
        }
        return faixas.SelectMany(f => f.Trechos.Select(t => (f.A, f.B, t.V0, t.V1))).ToList();

        static bool Parecidos(List<(double V0, double V1)> a, List<(double V0, double V1)> b) =>
            a.Count == b.Count && a.Zip(b, (x, y) => Math.Abs(x.V0 - y.V0) < Detalhe && Math.Abs(x.V1 - y.V1) < Detalhe).All(ok => ok);

        static List<(double V0, double V1)> Envolver(List<(double V0, double V1)> a, List<(double V0, double V1)> b) =>
            a.Zip(b, (x, y) => (Math.Min(x.V0, y.V0), Math.Max(x.V1, y.V1))).ToList();
    }

    /// <summary>Faixa de u com os trechos de v dentro do ambiente; Base são os trechos com que ela começou, para o desvio não se acumular.</summary>
    sealed record Faixa(double A, double B, List<(double V0, double V1)> Trechos, List<(double V0, double V1)> Base);

    /// <summary>Menor número de bicos em cada direção que atende distância e área máximas.</summary>
    static (int Nu, int Nv) Contagem(double lu, double lv, LimitesDeBico limites)
    {
        var nu = Math.Max(1, (int)Math.Ceiling(lu / limites.DistanciaMaxima - 1e-9));
        var nv = Math.Max(1, (int)Math.Ceiling(lv / limites.DistanciaMaxima - 1e-9));
        (int, int)? melhor = null;
        for (var a = nu; a <= nu + 40; a++)
        for (var b = nv; b <= nv + 40; b++)
        {
            if (lu / a * (lv / b) > limites.AreaMaxima + Folga) continue;
            if (melhor is not { } m || a * b < m.Item1 * m.Item2) melhor = (a, b);
            break;
        }
        return melhor ?? (nu, nv);
    }

    /// <summary>
    /// S (ou L) da Figura 2: em cada sentido, a distância até o próximo bico naquela direção (até 45° para o lado) ou, sem ele, o dobro da
    /// distância até a parede; vale a maior das duas.
    /// </summary>
    static double Lado(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, IReadOnlyList<Ponto2> bicos, Ponto2 b, Ponto2 eixo, Ponto2 outro)
    {
        var maior = 0.0;
        foreach (var sinal in new[] { 1, -1 })
        {
            var sentido = new Ponto2(eixo.X * sinal, eixo.Y * sinal);
            var proximo = bicos.Where(o => !o.Equals(b))
                .Select(o => (Ao: Dot(Sub(o, b), sentido), Lado: Math.Abs(Dot(Sub(o, b), outro))))
                .Where(x => x.Ao > 0.3 && x.Lado <= x.Ao)
                .Select(x => x.Ao).DefaultIfEmpty(double.MaxValue).Min();
            var parede = Raio(contornos, b, sentido);
            maior = Math.Max(maior, proximo < parede ? proximo : 2 * parede);
        }
        return maior;
    }

    /// <summary>
    /// O ponto está coberto se algum bico está a até ¾ da distância máxima dele; na parede, esse bico também tem de
    /// estar a até metade da distância máxima medida na perpendicular à parede.
    /// </summary>
    static bool Coberto(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, IReadOnlyList<Ponto2> bicos, Ponto2 p, LimitesDeBico limites)
    {
        var parede = ParedeMaisProxima(contornos, p);
        var naParede = parede.Distancia < Amostra;
        foreach (var b in bicos)
        {
            if (Dist(b, p) > limites.AlcanceMaximo + Folga) continue;
            if (!naParede) return true;
            var perpendicular = Math.Abs(Dot(Sub(b, p), parede.Normal));
            if (perpendicular <= limites.ParedeMaxima + Amostra) return true;
        }
        return false;
    }

    /// <summary>Pontos de uma grade de 25 cm dentro do ambiente e ao longo das paredes.</summary>
    static IEnumerable<Ponto2> Amostras(IReadOnlyList<IReadOnlyList<Ponto2>> contornos)
    {
        var todos = contornos.SelectMany(c => c).ToList();
        for (var x = todos.Min(p => p.X) + Amostra / 2; x < todos.Max(p => p.X); x += Amostra)
        for (var y = todos.Min(p => p.Y) + Amostra / 2; y < todos.Max(p => p.Y); y += Amostra)
        {
            var p = new Ponto2(x, y);
            if (Dentro(contornos, p)) yield return p;
        }
        foreach (var laco in contornos)
            for (var i = 0; i < laco.Count; i++)
            {
                var a = laco[i];
                var b = laco[(i + 1) % laco.Count];
                var n = Math.Max(1, (int)Math.Ceiling(Dist(a, b) / Amostra));
                for (var k = 0; k < n; k++)
                {
                    // Um pouco para dentro, para o ponto contar como dentro do ambiente.
                    var p = new Ponto2(a.X + (b.X - a.X) * (k + 0.5) / n, a.Y + (b.Y - a.Y) * (k + 0.5) / n);
                    var normal = NormalParaDentro(contornos, a, b);
                    yield return new Ponto2(p.X + normal.X * 0.01, p.Y + normal.Y * 0.01);
                }
            }
    }

    /// <summary>Eixo dos ramais: o dado, ou o da parede mais longa do contorno.</summary>
    static (Ponto2 U, Ponto2 V) Eixos(IReadOnlyList<Ponto2> laco, Ponto2? direcao)
    {
        Ponto2 u;
        if (direcao is { } d) u = Normalizar(d);
        else
        {
            var (a, b) = Enumerable.Range(0, laco.Count).Select(i => (laco[i], laco[(i + 1) % laco.Count]))
                .OrderByDescending(s => Dist(s.Item1, s.Item2)).First();
            u = Normalizar(Sub(b, a));
        }
        if (u.X < -1e-9 || (Math.Abs(u.X) <= 1e-9 && u.Y < 0)) u = new Ponto2(-u.X, -u.Y);
        return (u, new Ponto2(-u.Y, u.X));
    }

    /// <summary>Eixos para conferir bicos já existentes: a direção do par de bicos vizinhos mais comum, ou a da parede.</summary>
    static (Ponto2 U, Ponto2 V) EixosDosBicos(IReadOnlyList<Ponto2> laco, IReadOnlyList<Ponto2> bicos)
    {
        if (bicos.Count < 2) return Eixos(laco, null);
        var parede = Eixos(laco, null).U;
        var angulo = Math.Atan2(parede.Y, parede.X);
        // Ângulo módulo 90°: grade de bicos alinhada a um eixo ou ao perpendicular dá o mesmo.
        var vizinhos = bicos.Select(b => bicos.Where(o => !o.Equals(b)).OrderBy(o => Dist(o, b)).First())
            .Zip(bicos, (o, b) => Math.Atan2(o.Y - b.Y, o.X - b.X)).Select(a => ((a % (Math.PI / 2)) + Math.PI / 2) % (Math.PI / 2)).ToList();
        var mediana = vizinhos.OrderBy(a => a).ElementAt(vizinhos.Count / 2);
        var paredeMod = ((angulo % (Math.PI / 2)) + Math.PI / 2) % (Math.PI / 2);
        var usar = Math.Abs(mediana - paredeMod) < 0.05 ? paredeMod : mediana;
        return Eixos(laco, new Ponto2(Math.Cos(usar), Math.Sin(usar)));
    }

    static Ponto2 Ponto(Ponto2 u, Ponto2 v, double a, double b) => new(u.X * a + v.X * b, u.Y * a + v.Y * b);

    /// <summary>Par-ímpar: dentro do laço externo e fora dos vazios.</summary>
    public static bool Dentro(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, Ponto2 p)
    {
        var dentro = false;
        foreach (var laco in contornos)
            for (int i = 0, j = laco.Count - 1; i < laco.Count; j = i++)
                if ((laco[i].Y > p.Y) != (laco[j].Y > p.Y)
                    && p.X < (laco[j].X - laco[i].X) * (p.Y - laco[i].Y) / (laco[j].Y - laco[i].Y) + laco[i].X)
                    dentro = !dentro;
        return dentro;
    }

    public static double DistanciaAoContorno(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, Ponto2 p) => ParedeMaisProxima(contornos, p).Distancia;

    static (double Distancia, Ponto2 Normal) ParedeMaisProxima(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, Ponto2 p)
    {
        var melhor = (Distancia: double.MaxValue, Normal: new Ponto2(0, 0));
        foreach (var laco in contornos)
            for (var i = 0; i < laco.Count; i++)
            {
                var a = laco[i];
                var b = laco[(i + 1) % laco.Count];
                var ab = Sub(b, a);
                var t = Math.Max(0, Math.Min(1, Dot(Sub(p, a), ab) / Math.Max(Dot(ab, ab), 1e-12)));
                var q = new Ponto2(a.X + ab.X * t, a.Y + ab.Y * t);
                var d = Dist(p, q);
                if (d < melhor.Distancia) melhor = (d, Normalizar(new Ponto2(-ab.Y, ab.X)));
            }
        return melhor;
    }

    static Ponto2 NormalParaDentro(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, Ponto2 a, Ponto2 b)
    {
        var n = Normalizar(new Ponto2(-(b.Y - a.Y), b.X - a.X));
        var meio = new Ponto2((a.X + b.X) / 2 + n.X * 0.01, (a.Y + b.Y) / 2 + n.Y * 0.01);
        return Dentro(contornos, meio) ? n : new Ponto2(-n.X, -n.Y);
    }

    /// <summary>Distância do ponto até a parede, andando no sentido dado.</summary>
    static double Raio(IReadOnlyList<IReadOnlyList<Ponto2>> contornos, Ponto2 p, Ponto2 sentido)
    {
        var menor = double.MaxValue;
        foreach (var laco in contornos)
            for (var i = 0; i < laco.Count; i++)
            {
                var a = laco[i];
                var b = laco[(i + 1) % laco.Count];
                var e = Sub(b, a);
                var den = sentido.X * e.Y - sentido.Y * e.X;
                if (Math.Abs(den) < 1e-12) continue;
                var ap = Sub(a, p);
                var t = (ap.X * e.Y - ap.Y * e.X) / den;
                var s = (ap.X * sentido.Y - ap.Y * sentido.X) / den;
                if (t > 1e-9 && s >= -1e-9 && s <= 1 + 1e-9) menor = Math.Min(menor, t);
            }
        return menor;
    }

    static double Dot(Ponto2 a, Ponto2 b) => a.X * b.X + a.Y * b.Y;
    static Ponto2 Sub(Ponto2 a, Ponto2 b) => new(a.X - b.X, a.Y - b.Y);
    static double Dist(Ponto2 a, Ponto2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    static Ponto2 Normalizar(Ponto2 a)
    {
        var n = Math.Sqrt(Dot(a, a));
        return n < 1e-12 ? new Ponto2(1, 0) : new Ponto2(a.X / n, a.Y / n);
    }
}
