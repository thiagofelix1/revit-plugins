using Spk.Motor.Modelo;

namespace Spk.Motor.Calculo;

public sealed class OpcoesSolver
{
    public int MaximoIteracoes { get; init; } = 200;

    /// <summary>Critério de parada: Σ|ΔQ| / Σ|Q|.</summary>
    public double ToleranciaRelativa { get; init; } = 1e-9;

    public static OpcoesSolver Padrao { get; } = new();
}

/// <summary>O que a norma exige de um chuveiro aberto: vazão mínima e pressão mínima.</summary>
public sealed record RequisitoChuveiro(string ChuveiroId, double VazaoMinima, double PressaoMinima);

/// <summary>Estado hidráulico da rede para uma pressão fixa na fonte.</summary>
public sealed class EstadoRede
{
    internal EstadoRede(double pressaoFonte, double[] pressaoNos, double[] vazaoTrechos, double[] vazaoChuveiros,
        bool[] trechoAtivo, int iteracoes, bool convergiu, double erroBalancoMaximo, double vazaoFonte)
    {
        PressaoFonte = pressaoFonte;
        PressaoNos = pressaoNos;
        VazaoTrechos = vazaoTrechos;
        VazaoChuveiros = vazaoChuveiros;
        TrechoAtivo = trechoAtivo;
        Iteracoes = iteracoes;
        Convergiu = convergiu;
        ErroBalancoMaximo = erroBalancoMaximo;
        VazaoFonte = vazaoFonte;
    }

    public double PressaoFonte { get; }

    /// <summary>Pressão por nó (mesma ordem de <see cref="Rede.Nos"/>), em kPa. NaN se o nó não está ligado à fonte.</summary>
    public IReadOnlyList<double> PressaoNos { get; }

    /// <summary>Vazão por trecho, positiva no sentido De → Para, em L/min.</summary>
    public IReadOnlyList<double> VazaoTrechos { get; }

    /// <summary>Vazão por chuveiro (mesma ordem de <see cref="Rede.Chuveiros"/>), zero nos fechados.</summary>
    public IReadOnlyList<double> VazaoChuveiros { get; }

    /// <summary>Falso para trechos podados (sem nenhum chuveiro aberto a jusante).</summary>
    public IReadOnlyList<bool> TrechoAtivo { get; }

    public int Iteracoes { get; }
    public bool Convergiu { get; }

    /// <summary>Maior desequilíbrio de vazão num nó, em L/min.</summary>
    public double ErroBalancoMaximo { get; }

    /// <summary>Vazão total que sai da fonte, em L/min.</summary>
    public double VazaoFonte { get; }
}

/// <summary>
/// Resolve a rede pelo método do gradiente global (Todini, o mesmo do EPANET): serve para árvore,
/// grelha e anel. Chuveiros abertos são emissores Q = K√P sem refluxo; trechos seguem Hazen-Williams.
/// A instância é imutável e pode ser usada em paralelo.
/// </summary>
public sealed class MotorHidraulico
{
    const double VazaoMinimaLinearizacao = 1e-3;
    const double CondutanciaFechado = 1e-12;

    readonly Rede _rede;
    readonly int _nNos;
    readonly int _fonte;
    readonly double[] _cota;
    readonly double[] _demanda;
    readonly int[] _de;
    readonly int[] _para;
    readonly double[] _resistencia;
    readonly int[] _noDoChuveiro;
    readonly double[] _kKPa;
    readonly int[] _chuveiroDoNo;
    readonly List<int>[] _incidentes;
    readonly Dictionary<string, int> _indiceChuveiro;

    public MotorHidraulico(Rede rede)
    {
        _rede = rede;
        _nNos = rede.Nos.Count;
        var indiceNo = new Dictionary<string, int>();
        for (var i = 0; i < _nNos; i++) indiceNo[rede.Nos[i].Id] = i;

        _fonte = indiceNo[rede.NoFonte];
        _cota = rede.Nos.Select(n => n.Z).ToArray();
        _demanda = rede.Nos.Select(n => n.DemandaFixa).ToArray();

        var m = rede.Trechos.Count;
        _de = new int[m];
        _para = new int[m];
        _resistencia = new double[m];
        _incidentes = Enumerable.Range(0, _nNos).Select(_ => new List<int>()).ToArray();
        for (var j = 0; j < m; j++)
        {
            var t = rede.Trechos[j];
            _de[j] = indiceNo[t.De];
            _para[j] = indiceNo[t.Para];
            _resistencia[j] = Hidraulica.Resistencia(t.ComprimentoTotal, t.C, t.DiametroInterno);
            _incidentes[_de[j]].Add(j);
            _incidentes[_para[j]].Add(j);
        }

        var nc = rede.Chuveiros.Count;
        _noDoChuveiro = new int[nc];
        _kKPa = new double[nc];
        _chuveiroDoNo = Enumerable.Repeat(-1, _nNos).ToArray();
        _indiceChuveiro = new Dictionary<string, int>();
        for (var c = 0; c < nc; c++)
        {
            var ch = rede.Chuveiros[c];
            _noDoChuveiro[c] = indiceNo[ch.No];
            _kKPa[c] = Hidraulica.FatorKEmKPa(ch.FatorK);
            _chuveiroDoNo[_noDoChuveiro[c]] = c;
            _indiceChuveiro[ch.Id] = c;
        }
    }

    public Rede Rede => _rede;

    /// <summary>Resolve a rede com os chuveiros indicados abertos e a pressão dada na fonte.</summary>
    public EstadoRede Resolver(IEnumerable<string> chuveirosAbertos, double pressaoFonte, OpcoesSolver? opcoes = null)
    {
        var topologia = MontarTopologia(MascaraAbertos(chuveirosAbertos));
        var s = Resolver(topologia, pressaoFonte, null, opcoes ?? OpcoesSolver.Padrao);
        return Estado(topologia, s, pressaoFonte);
    }

    /// <summary>
    /// Menor pressão na fonte que faz todos os chuveiros abertos atenderem vazão e pressão mínimas
    /// (NBR 9.4.4.6 e 9.4.4.10). O chuveiro que fica exatamente no limite é o mais desfavorável.
    /// </summary>
    public ResultadoDemanda CalcularDemanda(IReadOnlyList<RequisitoChuveiro> requisitos, OpcoesSolver? opcoes = null)
    {
        if (requisitos.Count == 0) throw new ArgumentException("Nenhum chuveiro aberto.", nameof(requisitos));
        var op = opcoes ?? OpcoesSolver.Padrao;

        var indices = new int[requisitos.Count];
        var vazaoExigida = new double[requisitos.Count];
        for (var i = 0; i < requisitos.Count; i++)
        {
            var r = requisitos[i];
            if (!_indiceChuveiro.TryGetValue(r.ChuveiroId, out var c))
                throw new ArgumentException($"Chuveiro '{r.ChuveiroId}' não existe.");
            indices[i] = c;
            var pelaPressao = Hidraulica.VazaoChuveiro(_rede.Chuveiros[c].FatorK, r.PressaoMinima);
            vazaoExigida[i] = Math.Max(r.VazaoMinima, pelaPressao);
        }

        var aberto = new bool[_rede.Chuveiros.Count];
        foreach (var c in indices)
        {
            if (aberto[c]) throw new ArgumentException($"Chuveiro '{_rede.Chuveiros[c].Id}' repetido.");
            aberto[c] = true;
        }
        var topologia = MontarTopologia(aberto);

        Solucao? ultima = null;
        var avaliacoes = 0;
        double Folga(double ps, out Solucao s)
        {
            s = Resolver(topologia, ps, ultima, op);
            ultima = s;
            avaliacoes++;
            var menor = double.MaxValue;
            for (var i = 0; i < indices.Length; i++)
                menor = Math.Min(menor, s.VazaoChuveiros[indices[i]] / vazaoExigida[i] - 1.0);
            return menor;
        }

        // Abaixo desta pressão nenhum chuveiro aberto tem pressão positiva, então a folga é −1.
        var cotaMinima = indices.Min(c => _cota[_noDoChuveiro[c]]);
        var baixo = Hidraulica.PesoEspecificoAgua * (cotaMinima - _cota[_fonte]);
        var fBaixo = -1.0;
        var alto = Math.Max(baixo + 100, 100);
        var fAlto = Folga(alto, out var sAlto);
        while (fAlto < 0)
        {
            baixo = alto;
            fBaixo = fAlto;
            alto = baixo + 2 * Math.Max(alto - baixo, 100);
            if (alto > 1e5) throw new InvalidOperationException("Demanda acima de 100 MPa: verifique diâmetros e cotas.");
            fAlto = Folga(alto, out sAlto);
        }

        // Regula falsi (variante de Illinois): f é contínua e crescente na pressão da fonte.
        var lado = 0;
        for (var it = 0; it < 200 && alto - baixo > 1e-4 && fAlto > 1e-9; it++)
        {
            var ps = alto - fAlto * (alto - baixo) / (fAlto - fBaixo);
            if (!(ps > baixo && ps < alto)) ps = 0.5 * (baixo + alto);
            var f = Folga(ps, out var s);
            if (f >= 0)
            {
                alto = ps; fAlto = f; sAlto = s;
                if (lado == 1) fBaixo /= 2;
                lado = 1;
            }
            else
            {
                baixo = ps; fBaixo = f;
                if (lado == -1) fAlto /= 2;
                lado = -1;
            }
        }

        // Garante que a solução reportada é a do lado viável (todas as folgas ≥ 0).
        if (!ReferenceEquals(ultima, sAlto)) Folga(alto, out sAlto);
        var estado = Estado(topologia, sAlto, alto);

        var chuveiros = new List<ResultadoChuveiro>();
        for (var i = 0; i < indices.Length; i++)
        {
            var c = indices[i];
            var ch = _rede.Chuveiros[c];
            var q = estado.VazaoChuveiros[c];
            chuveiros.Add(new ResultadoChuveiro(ch.Id, ch.No, ch.Ramal, ch.FatorK, ch.AreaCobertura,
                estado.PressaoNos[_noDoChuveiro[c]], q, requisitos[i].VazaoMinima, requisitos[i].PressaoMinima,
                q / vazaoExigida[i] - 1.0));
        }

        var critico = chuveiros.OrderBy(c => c.Folga).First();
        return new ResultadoDemanda(estado, chuveiros, critico.Id, MontarTrechos(estado), avaliacoes);
    }

    IReadOnlyList<ResultadoTrecho> MontarTrechos(EstadoRede estado)
    {
        var lista = new List<ResultadoTrecho>();
        for (var j = 0; j < _rede.Trechos.Count; j++)
        {
            if (!estado.TrechoAtivo[j]) continue;
            var t = _rede.Trechos[j];
            var q = estado.VazaoTrechos[j];
            var (montante, jusante) = q >= 0 ? (_de[j], _para[j]) : (_para[j], _de[j]);
            var aq = Math.Abs(q);
            var perdaAtrito = _resistencia[j] * Math.Pow(aq, Hidraulica.ExpoenteHazenWilliams);
            lista.Add(new ResultadoTrecho(
                t.Id, _rede.Nos[montante].Id, _rede.Nos[jusante].Id, aq, t.DiametroNominal, t.DiametroInterno, t.C,
                t.Comprimento, t.ComprimentoEquivalente, t.Conexoes,
                Hidraulica.PerdaUnitaria(aq, t.C, t.DiametroInterno), perdaAtrito,
                Hidraulica.PesoEspecificoAgua * (_cota[jusante] - _cota[montante]),
                estado.PressaoNos[montante], estado.PressaoNos[jusante],
                Hidraulica.Velocidade(aq, t.DiametroInterno)));
        }
        return lista;
    }

    bool[] MascaraAbertos(IEnumerable<string> ids)
    {
        var aberto = new bool[_rede.Chuveiros.Count];
        foreach (var id in ids)
        {
            if (!_indiceChuveiro.TryGetValue(id, out var c)) throw new ArgumentException($"Chuveiro '{id}' não existe.");
            aberto[c] = true;
        }
        return aberto;
    }

    sealed class Topologia
    {
        public bool[] Aberto = [];
        public bool[] TrechoAtivo = [];
        public bool[] NoAtivo = [];
        /// <summary>Índice da incógnita de cada nó; −1 para a fonte e para nós inativos.</summary>
        public int[] Juncao = [];
        public int NumJuncoes;
    }

    sealed class Solucao
    {
        public double[] Carga = [];
        public double[] VazaoTrechos = [];
        public double[] VazaoChuveiros = [];
        public int Iteracoes;
        public bool Convergiu;
    }

    /// <summary>Poda os ramos que não levam a nenhum chuveiro aberto nem demanda fixa.</summary>
    Topologia MontarTopologia(bool[] aberto)
    {
        var m = _rede.Trechos.Count;
        var ativo = Enumerable.Repeat(true, m).ToArray();
        var grau = new int[_nNos];
        for (var j = 0; j < m; j++) { grau[_de[j]]++; grau[_para[j]]++; }

        bool Terminal(int n) => n == _fonte || _demanda[n] > 0 || (_chuveiroDoNo[n] >= 0 && aberto[_chuveiroDoNo[n]]);

        var fila = new Queue<int>();
        for (var n = 0; n < _nNos; n++)
            if (grau[n] == 1 && !Terminal(n)) fila.Enqueue(n);
        while (fila.Count > 0)
        {
            var n = fila.Dequeue();
            foreach (var j in _incidentes[n])
            {
                if (!ativo[j]) continue;
                ativo[j] = false;
                grau[n]--;
                var outro = _de[j] == n ? _para[j] : _de[j];
                if (--grau[outro] == 1 && !Terminal(outro)) fila.Enqueue(outro);
            }
        }

        // Só fica ativo o que está ligado à fonte.
        var noAtivo = new bool[_nNos];
        var pilha = new Stack<int>();
        pilha.Push(_fonte);
        noAtivo[_fonte] = true;
        while (pilha.Count > 0)
        {
            var n = pilha.Pop();
            foreach (var j in _incidentes[n])
            {
                if (!ativo[j]) continue;
                var outro = _de[j] == n ? _para[j] : _de[j];
                if (noAtivo[outro]) continue;
                noAtivo[outro] = true;
                pilha.Push(outro);
            }
        }
        for (var j = 0; j < m; j++)
            if (ativo[j] && !noAtivo[_de[j]]) ativo[j] = false;

        for (var c = 0; c < aberto.Length; c++)
            if (aberto[c] && !noAtivo[_noDoChuveiro[c]])
                throw new InvalidOperationException($"Chuveiro '{_rede.Chuveiros[c].Id}' não está ligado à fonte.");
        for (var n = 0; n < _nNos; n++)
            if (_demanda[n] > 0 && !noAtivo[n])
                throw new InvalidOperationException($"Nó '{_rede.Nos[n].Id}' tem demanda fixa mas não está ligado à fonte.");

        var juncao = Enumerable.Repeat(-1, _nNos).ToArray();
        var nj = 0;
        for (var n = 0; n < _nNos; n++)
            if (noAtivo[n] && n != _fonte) juncao[n] = nj++;

        return new Topologia { Aberto = aberto, TrechoAtivo = ativo, NoAtivo = noAtivo, Juncao = juncao, NumJuncoes = nj };
    }

    Solucao Resolver(Topologia topo, double pressaoFonte, Solucao? inicial, OpcoesSolver op)
    {
        const double g = Hidraulica.PesoEspecificoAgua;
        const double n = Hidraulica.ExpoenteHazenWilliams;
        var m = _rede.Trechos.Count;
        var nc = _rede.Chuveiros.Count;
        var nj = topo.NumJuncoes;

        var carga = new double[_nNos];
        var cargaFonte = pressaoFonte + g * _cota[_fonte];
        for (var i = 0; i < _nNos; i++)
            carga[i] = inicial != null && topo.NoAtivo[i] ? inicial.Carga[i] : cargaFonte;
        carga[_fonte] = cargaFonte;

        var q = new double[m];
        var qc = new double[nc];
        for (var j = 0; j < m; j++)
            if (topo.TrechoAtivo[j]) q[j] = inicial?.VazaoTrechos[j] ?? 50.0;
        for (var c = 0; c < nc; c++)
            if (topo.Aberto[c]) qc[c] = inicial?.VazaoChuveiros[c] ?? 50.0;

        var p = new double[m];
        var y = new double[m];
        var pc = new double[nc];
        var yc = new double[nc];
        var diag = new double[nj];
        var rhs = new double[nj];
        var x = new double[nj];
        var acoplamentos = new List<int>();
        for (var j = 0; j < m; j++)
            if (topo.TrechoAtivo[j] && topo.Juncao[_de[j]] >= 0 && topo.Juncao[_para[j]] >= 0) acoplamentos.Add(j);
        var gc = new GradienteConjugado(nj);

        var convergiu = false;
        var it = 0;
        while (it < op.MaximoIteracoes)
        {
            it++;
            Array.Clear(diag, 0, nj);
            Array.Clear(rhs, 0, nj);

            for (var j = 0; j < m; j++)
            {
                if (!topo.TrechoAtivo[j]) continue;
                var aq = Math.Abs(q[j]);
                var gr = n * _resistencia[j] * Math.Pow(Math.Max(aq, VazaoMinimaLinearizacao), n - 1);
                var h = _resistencia[j] * Math.Pow(aq, n - 1) * q[j];
                p[j] = 1.0 / gr;
                y[j] = h / gr;
                var b = q[j] - y[j];
                int ja = topo.Juncao[_de[j]], jb = topo.Juncao[_para[j]];
                if (ja >= 0) { diag[ja] += p[j]; rhs[ja] -= b; if (jb < 0) rhs[ja] += p[j] * carga[_para[j]]; }
                if (jb >= 0) { diag[jb] += p[j]; rhs[jb] += b; if (ja < 0) rhs[jb] += p[j] * carga[_de[j]]; }
            }

            for (var c = 0; c < nc; c++)
            {
                if (!topo.Aberto[c]) continue;
                var no = _noDoChuveiro[c];
                var ji = topo.Juncao[no];
                var k2 = _kKPa[c] * _kKPa[c];
                if (qc[c] <= 0)
                {
                    // Sem refluxo: o chuveiro fechado por pressão negativa vira um trecho quase sem condutância.
                    qc[c] = 0;
                    pc[c] = CondutanciaFechado;
                    yc[c] = 0;
                }
                else
                {
                    var gr = 2.0 * Math.Max(qc[c], VazaoMinimaLinearizacao) / k2;
                    pc[c] = 1.0 / gr;
                    yc[c] = qc[c] * qc[c] / k2 / gr;
                }
                diag[ji] += pc[c];
                rhs[ji] -= qc[c] - yc[c];
                rhs[ji] += pc[c] * g * _cota[no];
            }

            for (var no = 0; no < _nNos; no++)
            {
                var ji = topo.Juncao[no];
                if (ji >= 0) rhs[ji] -= _demanda[no];
            }

            for (var no = 0; no < _nNos; no++)
            {
                var ji = topo.Juncao[no];
                if (ji >= 0) x[ji] = carga[no];
            }

            gc.Resolver(diag, rhs, x, acoplamentos, p, _de, _para, topo.Juncao);

            for (var no = 0; no < _nNos; no++)
            {
                var ji = topo.Juncao[no];
                if (ji >= 0) carga[no] = x[ji];
            }

            double somaDq = 0, somaQ = 0;
            for (var j = 0; j < m; j++)
            {
                if (!topo.TrechoAtivo[j]) continue;
                var novo = q[j] - y[j] + p[j] * (carga[_de[j]] - carga[_para[j]]);
                somaDq += Math.Abs(novo - q[j]);
                somaQ += Math.Abs(novo);
                q[j] = novo;
            }
            for (var c = 0; c < nc; c++)
            {
                if (!topo.Aberto[c]) continue;
                var no = _noDoChuveiro[c];
                var novo = qc[c] - yc[c] + pc[c] * (carga[no] - g * _cota[no]);
                if (novo < 0) novo = 0;
                somaDq += Math.Abs(novo - qc[c]);
                somaQ += Math.Abs(novo);
                qc[c] = novo;
            }

            if (somaDq <= op.ToleranciaRelativa * Math.Max(somaQ, 1e-9))
            {
                convergiu = true;
                break;
            }
        }

        return new Solucao { Carga = carga, VazaoTrechos = q, VazaoChuveiros = qc, Iteracoes = it, Convergiu = convergiu };
    }

    EstadoRede Estado(Topologia topo, Solucao s, double pressaoFonte)
    {
        const double g = Hidraulica.PesoEspecificoAgua;
        var m = _rede.Trechos.Count;

        // Carga piezométrica nos nós podados: sem vazão, é igual à do vizinho ativo.
        var carga = (double[])s.Carga.Clone();
        var conhecida = (bool[])topo.NoAtivo.Clone();
        var fila = new Queue<int>();
        for (var i = 0; i < _nNos; i++) if (conhecida[i]) fila.Enqueue(i);
        while (fila.Count > 0)
        {
            var i = fila.Dequeue();
            foreach (var j in _incidentes[i])
            {
                var outro = _de[j] == i ? _para[j] : _de[j];
                if (conhecida[outro]) continue;
                conhecida[outro] = true;
                carga[outro] = carga[i];
                fila.Enqueue(outro);
            }
        }

        var pressao = new double[_nNos];
        for (var i = 0; i < _nNos; i++) pressao[i] = conhecida[i] ? carga[i] - g * _cota[i] : double.NaN;

        var balanco = new double[_nNos];
        for (var j = 0; j < m; j++)
        {
            balanco[_de[j]] -= s.VazaoTrechos[j];
            balanco[_para[j]] += s.VazaoTrechos[j];
        }
        for (var c = 0; c < _rede.Chuveiros.Count; c++) balanco[_noDoChuveiro[c]] -= s.VazaoChuveiros[c];
        var erro = 0.0;
        for (var i = 0; i < _nNos; i++)
            if (i != _fonte && topo.NoAtivo[i]) erro = Math.Max(erro, Math.Abs(balanco[i] - _demanda[i]));

        return new EstadoRede(pressaoFonte, pressao, s.VazaoTrechos, s.VazaoChuveiros, (bool[])topo.TrechoAtivo.Clone(),
            s.Iteracoes, s.Convergiu, erro, -balanco[_fonte]);
    }

    /// <summary>Gradiente conjugado com pré-condicionador de Jacobi; a matriz do método é simétrica e definida positiva.</summary>
    sealed class GradienteConjugado(int n)
    {
        readonly double[] _r = new double[n];
        readonly double[] _z = new double[n];
        readonly double[] _d = new double[n];
        readonly double[] _ad = new double[n];

        public void Resolver(double[] diag, double[] b, double[] x, List<int> acoplamentos, double[] p,
            int[] de, int[] para, int[] juncao)
        {
            if (n == 0) return;
            void Multiplicar(double[] v, double[] resultado)
            {
                for (var i = 0; i < n; i++) resultado[i] = diag[i] * v[i];
                foreach (var j in acoplamentos)
                {
                    int a = juncao[de[j]], c = juncao[para[j]];
                    resultado[a] -= p[j] * v[c];
                    resultado[c] -= p[j] * v[a];
                }
            }

            Multiplicar(x, _ad);
            double normaB = 0;
            for (var i = 0; i < n; i++) { _r[i] = b[i] - _ad[i]; normaB += b[i] * b[i]; }
            normaB = Math.Sqrt(normaB);
            if (normaB == 0) normaB = 1;

            double rz = 0;
            for (var i = 0; i < n; i++) { _z[i] = _r[i] / diag[i]; _d[i] = _z[i]; rz += _r[i] * _z[i]; }

            for (var it = 0; it < 10 * n + 100; it++)
            {
                double normaR = 0;
                for (var i = 0; i < n; i++) normaR += _r[i] * _r[i];
                if (Math.Sqrt(normaR) <= 1e-13 * normaB) return;

                Multiplicar(_d, _ad);
                double dAd = 0;
                for (var i = 0; i < n; i++) dAd += _d[i] * _ad[i];
                if (dAd <= 0) return;
                var alfa = rz / dAd;
                for (var i = 0; i < n; i++) { x[i] += alfa * _d[i]; _r[i] -= alfa * _ad[i]; }

                double rzNovo = 0;
                for (var i = 0; i < n; i++) { _z[i] = _r[i] / diag[i]; rzNovo += _r[i] * _z[i]; }
                var beta = rzNovo / rz;
                rz = rzNovo;
                for (var i = 0; i < n; i++) _d[i] = _z[i] + beta * _d[i];
            }
        }
    }
}
