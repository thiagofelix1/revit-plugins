using Eletrica.AddIn.Ambientes;
using Eletrica.AddIn.Padrao;
using Eletrica.Motor.Modelo;

namespace Eletrica.AddIn.Lancamento;

/// <summary>
///     Lança os pontos de um ambiente pela proposta da norma (<see cref="Motor.Lancamento.PropostaDePontos" />) e pelo padrão:
///     tomadas espaçadas pelas paredes (as de 600 VA primeiro, as da média junto da bancada/lavatório), ponto de luz no centro,
///     interruptor ao lado da porta (lado da abertura, dentro do ambiente) já comandando a luz, chuveiro e ar-condicionado na
///     parede oposta à porta. Cada ambiente numa transação própria; tudo num grupo, um Ctrl+Z desfaz. O engenheiro arrasta
///     o que quiser depois: os parâmetros AE acompanham o elemento.
/// </summary>
static class PontosPorAmbiente
{
    public sealed record ResultadoAmbiente(string Nome, int Criados, IReadOnlyList<string> Avisos, string? Recusa)
    {
        public string Texto() => Recusa is not null ? $"{Nome}: não lançado — {Recusa}" : $"{Nome}: {Criados} ponto(s)" + (Avisos.Count > 0 ? "\n   • " + string.Join("\n   • ", Avisos) : "");
    }

    /// <summary>Segmento de parede com menos que isto não recebe tomada (ombreira, recorte).</summary>
    const double ParedeMinima = 0.60;

    /// <summary>Distância mínima de uma tomada à porta, em m.</summary>
    const double LongeDaPorta = 0.30;

    public static List<ResultadoAmbiente> Lancar(Document doc, IReadOnlyList<PontosPorAmbienteJanela.Linha> linhas, PadraoEletrica padrao, List<ElementId> criados)
    {
        var resultados = new List<ResultadoAmbiente>();
        var simbolos = new
        {
            Tomada = padrao.Simbolo(doc, PapelFamilia.Tomada10A) ?? padrao.Simbolo(doc, PapelFamilia.TomadaDupla),
            Luz = padrao.Simbolo(doc, PapelFamilia.Luminaria),
            Arandela = padrao.Simbolo(doc, PapelFamilia.Arandela) ?? padrao.Simbolo(doc, PapelFamilia.Luminaria),
            Interruptor = padrao.Simbolo(doc, PapelFamilia.Interruptor1),
            Chuveiro = padrao.Simbolo(doc, PapelFamilia.Chuveiro) ?? padrao.Simbolo(doc, PapelFamilia.PontoDeForca),
            Ar = padrao.Simbolo(doc, PapelFamilia.ArCondicionado) ?? padrao.Simbolo(doc, PapelFamilia.Tomada20A) ?? padrao.Simbolo(doc, PapelFamilia.PontoDeForca),
        };
        if (simbolos.Tomada is null || simbolos.Luz is null)
            throw new InvalidOperationException("Confirme em Famílias pelo menos a tomada de 10 A e a luminária antes de lançar por ambiente.");

        using var grupo = new TransactionGroup(doc, "AutoEletrica — Pontos por ambiente");
        grupo.Start();
        var proximoComando = Comandos.ProximoNumero(doc);
        foreach (var linha in linhas)
        {
            var avisos = new List<string>();
            var feitos = new List<ElementId>();
            var numeroDoComando = proximoComando;
            var motivo = TransacaoSegura.Executar(doc, $"AutoEletrica — Pontos: {linha.Nome}", () =>
            {
                if (ParametrosEletrica.Garantir(doc) is { } aviso) avisos.Add(aviso);
                LancarAmbiente(doc, linha, padrao, simbolos.Tomada, simbolos.Luz, simbolos.Arandela, simbolos.Interruptor, simbolos.Chuveiro, simbolos.Ar, feitos, avisos, ref numeroDoComando);
                return null;
            });
            if (motivo is null)
            {
                criados.AddRange(feitos);
                proximoComando = numeroDoComando;
            }
            resultados.Add(new ResultadoAmbiente(linha.Nome, feitos.Count, avisos, motivo));
        }
        grupo.Assimilate();
        return resultados;
    }

    static void LancarAmbiente(Document doc, PontosPorAmbienteJanela.Linha linha, PadraoEletrica padrao, FamilySymbol tomada, FamilySymbol luz, FamilySymbol? arandela,
        FamilySymbol? interruptor, FamilySymbol? chuveiro, FamilySymbol? ar, List<ElementId> criados, List<string> avisos, ref int numeroDoComando)
    {
        var a = linha.Ambiente;
        var l = padrao.Lancamento;
        var contorno = Colocacao.Contorno(a);
        if (contorno.Count == 0) throw new InvalidOperationException("o ambiente não tem contorno fechado");
        var vinculo = Colocacao.VinculoDe(doc, a);
        var piso = a.Transformacao.OfPoint((a.Room.Location as LocationPoint)?.Point ?? contorno[0].Inicio).Z;
        var nivel = Colocacao.NivelAbaixo(doc, piso) ?? throw new InvalidOperationException("o projeto não tem níveis");
        var portas = Colocacao.Portas(a, contorno);
        var paredes = contorno.Where(s => s.Comprimento >= ParedeMinima).ToList();
        if (paredes.Count == 0) paredes = contorno;
        var uso = a.Uso;
        var molhado = uso is UsoAmbiente.Cozinha or UsoAmbiente.Copa or UsoAmbiente.AreaServico or UsoAmbiente.Lavanderia or UsoAmbiente.Banheiro or UsoAmbiente.Lavabo;

        // ----- tomadas: espaçadas pelo perímetro útil, longe das portas -----
        var total = paredes.Sum(s => s.Comprimento);
        var posicoes = new List<(Colocacao.SegmentoDeParede S, double D)>();
        if (linha.Tomadas > 0)
        {
            var passo = total / linha.Tomadas;
            for (var k = 0; k < linha.Tomadas; k++)
            {
                var alvo = passo * (k + 0.5);
                double acumulado = 0;
                foreach (var s in paredes)
                {
                    if (alvo <= acumulado + s.Comprimento + 1e-9)
                    {
                        var d = Math.Max(0.15, Math.Min(s.Comprimento - 0.15, alvo - acumulado));
                        d = LongeDasPortas(s, d, portas);
                        posicoes.Add((s, d));
                        break;
                    }
                    acumulado += s.Comprimento;
                }
            }
        }
        // As de 600 VA e as da média: as primeiras da lista, começando pela parede mais longa (bancada, tanque, lavatório ficam nela em geral).
        posicoes = posicoes.OrderByDescending(p => p.S.Comprimento).ThenBy(p => p.D).ToList();
        var feitas = 0;
        foreach (var (s, d) in posicoes)
        {
            var de600 = feitas < linha.Tomadas600;
            var naMedia = feitas < linha.TomadasMedia;
            var posicao = naMedia ? PosicaoDoPonto.Media : PosicaoDoPonto.Baixa;
            var potencia = de600 ? Math.Max(600, l.PotenciaTugMolhada) : Math.Max(molhado ? 100 : 0, l.PotenciaTug);
            var pedido = new Colocacao.Pedido(tomada, PapelFamilia.Tomada10A, posicao, l.Altura(posicao), potencia, TipoCarga.TomadaUsoGeral);
            if (NaParede(doc, a, vinculo, nivel, s, d, pedido, padrao, criados, avisos) is not null) feitas++;
        }

        // ----- luz: centro do ambiente, no teto (ou na parede nos ambientes pequenos) -----
        var centro = Centro(a, contorno);
        var luzNaParede = uso is UsoAmbiente.Varanda or UsoAmbiente.Deposito or UsoAmbiente.Lavabo && a.Area <= 6 || uso == UsoAmbiente.AreaExterna;
        FamilyInstance? luminaria = null;
        if (linha.LuzVA > 0)
        {
            if (luzNaParede && arandela is not null)
            {
                var parede = paredes.OrderByDescending(s => s.Comprimento).First();
                var pedido = new Colocacao.Pedido(arandela, PapelFamilia.Arandela, PosicaoDoPonto.Alta, l.AlturaAlta, linha.LuzVA, TipoCarga.Iluminacao);
                luminaria = NaParede(doc, a, vinculo, nivel, parede, parede.Comprimento / 2, pedido, padrao, criados, avisos);
            }
            else
            {
                var pedido = new Colocacao.Pedido(luz, PapelFamilia.Luminaria, PosicaoDoPonto.Teto, l.PeDireito, linha.LuzVA, TipoCarga.Iluminacao);
                luminaria = NoTeto(doc, a, nivel, centro, piso, pedido, padrao, criados, avisos);
            }
        }

        // ----- interruptor junto da porta, comandando a luz -----
        if (linha.Interruptor && luminaria is not null)
        {
            if (interruptor is null) avisos.Add("sem família de interruptor confirmada em Famílias: a luz ficou sem comando");
            else
            {
                var (s, d) = JuntoDaPorta(paredes, portas, centro);
                var pedido = new Colocacao.Pedido(interruptor, PapelFamilia.Interruptor1, PosicaoDoPonto.Interruptor, l.AlturaInterruptor, 0, null);
                var sw = NaParede(doc, a, vinculo, nivel, s, d, pedido, padrao, criados, avisos);
                if (sw is not null)
                {
                    Comandos.Gravar(doc, numeroDoComando, [sw], [luminaria], avisos);
                    numeroDoComando++;
                }
                if (portas.Count == 0) avisos.Add("nenhuma porta encontrada: o interruptor ficou na parede mais longa; mova-o para junto da porta");
            }
        }

        // ----- chuveiro e ar-condicionado: parede oposta à porta -----
        if (linha.Chuveiro)
        {
            if (chuveiro is null) avisos.Add("sem família de chuveiro ou ponto de força confirmada: chuveiro não lançado");
            else
            {
                var s = OpostaAPorta(paredes, portas, centro);
                var pedido = new Colocacao.Pedido(chuveiro, PapelFamilia.Chuveiro, PosicaoDoPonto.Alta, l.AlturaAlta, l.PotenciaChuveiro, TipoCarga.Chuveiro);
                NaParede(doc, a, vinculo, nivel, s, s.Comprimento / 2, pedido, padrao, criados, avisos);
            }
        }
        if (linha.Ar)
        {
            if (ar is null) avisos.Add("sem família de ar-condicionado, tomada de 20 A ou ponto de força confirmada: ar não lançado");
            else
            {
                var s = OpostaAPorta(paredes, portas, centro);
                var pedido = new Colocacao.Pedido(ar, PapelFamilia.ArCondicionado, PosicaoDoPonto.Alta, l.AlturaAlta, l.PotenciaArCondicionado, TipoCarga.ArCondicionado);
                NaParede(doc, a, vinculo, nivel, s, Math.Max(0.3, s.Comprimento / 2 - 0.6), pedido, padrao, criados, avisos);
            }
        }
    }

    static FamilyInstance? NaParede(Document doc, AmbienteLido a, RevitLinkInstance? vinculo, Level nivel, Colocacao.SegmentoDeParede s, double distancia,
        Colocacao.Pedido pedido, PadraoEletrica padrao, List<ElementId> criados, List<string> avisos)
    {
        var naLinha = s.Em(distancia);
        var z = nivel.ProjectElevation + Unidades.DeMetros(pedido.Altura);
        var afastamento = Colocacao.DeFace(pedido.Simbolo) ? 0 : padrao.AfastamentoDaParede;
        var ponto = new XYZ(naLinha.X, naLinha.Y, z) + s.ParaDentro * Unidades.DeMetros(afastamento);
        try
        {
            Reference? face = null;
            if (Colocacao.DeFace(pedido.Simbolo))
            {
                if (s.Parede is not null) face = Colocacao.FaceDaParede(doc, s.Parede, a.Transformacao, vinculo, ponto, s.ParaDentro);
                if (face is null)
                {
                    avisos.Add($"{PadraoEletrica.NomeDoPapel(pedido.Papel)}: o lado do ambiente a {distancia:0.0} m do canto não tem parede (separador de ambiente?); ponto não lançado");
                    return null;
                }
            }
            var fi = Colocacao.NaParede(doc, pedido.Simbolo, ponto, s.ParaDentro, nivel, face);
            doc.Regenerate();
            var altura = Colocacao.AjustarAltura(doc, fi, pedido.Altura, nivel);
            Colocacao.Marcar(fi, pedido, a, altura);
            criados.Add(fi.Id);
            return fi;
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            avisos.Add($"{PadraoEletrica.NomeDoPapel(pedido.Papel)} não lançado: {e.Message}");
            return null;
        }
    }

    static FamilyInstance? NoTeto(Document doc, AmbienteLido a, Level nivel, XYZ centro, double piso, Colocacao.Pedido pedido, PadraoEletrica padrao, List<ElementId> criados, List<string> avisos)
    {
        try
        {
            Reference? face = null;
            var z = piso + Unidades.DeMetros(pedido.Altura);
            var peDireito = a.Room.UnboundedHeight;
            if (peDireito > Unidades.DeMetros(1.8) && peDireito < Unidades.DeMetros(8)) z = piso + peDireito;
            if (Colocacao.DeFace(pedido.Simbolo))
            {
                var vista = Colocacao.Vista3d(doc, out var criada);
                var teto = Colocacao.FaceDoTeto(doc, vista, new XYZ(centro.X, centro.Y, piso), 8);
                if (criada) doc.Delete(vista.Id);
                if (teto is null)
                {
                    avisos.Add("luminária não lançada: não há forro, laje ou telhado acima do centro do ambiente para hospedar a família de face");
                    return null;
                }
                face = teto.Value.Face;
                z = teto.Value.Z;
            }
            var fi = Colocacao.NoTeto(doc, pedido.Simbolo, new XYZ(centro.X, centro.Y, z), nivel, face);
            doc.Regenerate();
            Colocacao.Marcar(fi, pedido, a, Unidades.Metros(z - nivel.ProjectElevation));
            criados.Add(fi.Id);
            return fi;
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            avisos.Add($"luminária não lançada: {e.Message}");
            return null;
        }
    }

    static XYZ Centro(AmbienteLido a, List<Colocacao.SegmentoDeParede> contorno)
    {
        // Centroide do contorno; se cair fora (L, U), o ponto do ambiente do Revit.
        double sx = 0, sy = 0, sa = 0;
        for (var i = 0; i < contorno.Count; i++)
        {
            var p = contorno[i].Inicio;
            var q = contorno[i].Fim;
            var cross = p.X * q.Y - q.X * p.Y;
            sa += cross;
            sx += (p.X + q.X) * cross;
            sy += (p.Y + q.Y) * cross;
        }
        var centroide = Math.Abs(sa) < 1e-9 ? null : new XYZ(sx / (3 * sa), sy / (3 * sa), contorno[0].Inicio.Z);
        var doRevit = a.Transformacao.OfPoint((a.Room.Location as LocationPoint)?.Point ?? contorno[0].Inicio);
        if (centroide is null) return doRevit;
        try
        {
            var local = a.Transformacao.Inverse.OfPoint(centroide + XYZ.BasisZ * Unidades.DeMetros(0.5));
            return a.Room.IsPointInRoom(local) ? centroide : doRevit;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { return doRevit; }
    }

    /// <summary>Empurra a posição para fora do vão das portas que estão nesse segmento.</summary>
    static double LongeDasPortas(Colocacao.SegmentoDeParede s, double d, List<(XYZ Local, double Largura)> portas)
    {
        foreach (var (local, largura) in portas)
        {
            if (Colocacao.DistanciaAoSegmento(local, s) > 0.35) continue;
            var centro = Colocacao.ProjecaoNoSegmento(local, s);
            var meia = largura / 2 + LongeDaPorta;
            if (d > centro - meia && d < centro + meia)
                d = d < centro ? Math.Max(0.15, centro - meia) : Math.Min(s.Comprimento - 0.15, centro + meia);
        }
        return d;
    }

    /// <summary>Lugar do interruptor: ao lado da porta (do lado em que há parede), senão no começo da parede mais longa.</summary>
    static (Colocacao.SegmentoDeParede S, double D) JuntoDaPorta(List<Colocacao.SegmentoDeParede> paredes, List<(XYZ Local, double Largura)> portas, XYZ centro)
    {
        foreach (var (local, largura) in portas)
        {
            var s = paredes.OrderBy(p => Colocacao.DistanciaAoSegmento(local, p)).FirstOrDefault();
            if (s is null || Colocacao.DistanciaAoSegmento(local, s) > 0.6) continue;
            var c = Colocacao.ProjecaoNoSegmento(local, s);
            var afastado = largura / 2 + 0.20;
            // Lado da parede com mais espaço depois da porta.
            var direita = c + afastado;
            var esquerda = c - afastado;
            if (direita <= s.Comprimento - 0.10 && (s.Comprimento - direita >= esquerda || esquerda < 0.10)) return (s, direita);
            if (esquerda >= 0.10) return (s, esquerda);
            return (s, Math.Max(0.10, Math.Min(s.Comprimento - 0.10, direita)));
        }
        var maior = paredes.OrderByDescending(p => p.Comprimento).First();
        return (maior, Math.Min(0.4, maior.Comprimento / 2));
    }

    /// <summary>Parede mais longe das portas (a oposta), para chuveiro e ar; sem porta, a mais longa.</summary>
    static Colocacao.SegmentoDeParede OpostaAPorta(List<Colocacao.SegmentoDeParede> paredes, List<(XYZ Local, double Largura)> portas, XYZ centro)
    {
        if (portas.Count == 0) return paredes.OrderByDescending(p => p.Comprimento).First();
        return paredes.OrderByDescending(p => portas.Min(porta => Colocacao.DistanciaAoSegmento(porta.Local, p))).ThenByDescending(p => p.Comprimento).First();
    }
}
