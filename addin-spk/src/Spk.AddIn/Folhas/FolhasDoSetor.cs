namespace Spk.AddIn.Folhas;

/// <summary>
/// Folhas do último setor calculado, no padrão da NEPN-INC-014: planta com a área de cálculo hachurada, bicos da área
/// numerados, bico crítico, diâmetros e descidas; isométrico do setor; corte no sentido dos ramais. Vale para a malha
/// gerada e para rede real, porque parte do que Calcular gravou e não da marca SPK-GERADO.
/// </summary>
static class FolhasDoSetor
{
    static readonly Color Vermelho = new(204, 0, 0);
    static readonly Color Laranja = new(230, 120, 0);
    const string TipoHachura = "SPK — Área de cálculo";
    const string PadraoHachura = "SPK diagonal";
    const int MaximoEtiquetas = 300;

    public static FolhasGeradas Gerar(ContextoFolhas contexto)
    {
        var doc = contexto.Doc;
        var gravado = ResultadoGravado.Ler(doc)
                      ?? throw new InvalidOperationException("Rode Calcular antes das folhas: elas mostram o último setor calculado.");
        var bicos = gravado.Chuveiros
            .Select(c => (Dado: c, Elemento: doc.GetElement(new ElementId(c.Elemento)) as FamilyInstance))
            .ToList();
        var tubos = gravado.Tubos.Select(id => doc.GetElement(new ElementId(id))).OfType<MEPCurve>().ToList();
        if (bicos.Count == 0 || bicos.Any(b => b.Elemento is null) || tubos.Count < gravado.Tubos.Count)
            throw new InvalidOperationException("A rede mudou desde o último cálculo (bicos ou tubos apagados). Rode Calcular de novo.");

        var avisos = new List<string>();
        var elementos = bicos.Select(b => (Element)b.Elemento!).Concat(tubos).ToList();
        var rede = VistasSpk.CaixaDe(elementos) ?? throw new InvalidOperationException("Não foi possível medir a rede do setor.");
        var direcao = VistasSpk.DirecaoDosRamais(tubos);
        var setor = PranchaSpk.NomeSeguro(gravado.Setor);
        var codigo = PranchaSpk.Codigo(gravado.Setor);
        var daArea = bicos.Where(b => b.Dado.NaArea).ToList();
        var critico = bicos.FirstOrDefault(b => b.Dado.Id == gravado.ChuveiroCritico);

        // Folha 01: planta à esquerda com o quadro de resultado embaixo; 3D e mapa-chave à direita. O canto de baixo à
        // direita fica para o carimbo.
        var folha = PranchaSpk.Recriar(doc, $"{PranchaSpk.PrefixoNumero}{codigo}-01", $"Sprinklers — {setor} — planta e isométrico");
        doc.Regenerate();
        var util = PranchaSpk.Area(folha).Parte(0.03, 0.05, 0.97, 0.97);
        var regiaoPlanta = util.Parte(0, 0.17, 0.64, 1);
        var regiaoNota = util.Parte(0, 0, 0.64, 0.15);
        var lateral = util.Parte(0.66, 0, 1, 1);
        var regiao3d = lateral.Parte(0, 0.58, 1, 1);
        var regiaoChave = lateral.Parte(0, 0.30, 1, 0.56);

        var nivel = LeituraSetores.NivelDaMaioria(doc, bicos.Select(b => b.Elemento!));
        var recorte = rede.Expandir(VistasSpk.Pes(2.0));
        var planta = VistasSpk.Planta(doc, $"{PranchaSpk.PrefixoVista}{setor} — Planta", nivel, recorte, VistasSpk.TemplatePlanta, avisos);
        Tentar(avisos, "Escala da planta", () => planta.Scale = regiaoPlanta.Escala(recorte.Max.X - recorte.Min.X, recorte.Max.Y - recorte.Min.Y));
        var escala = planta.Scale;
        double NoModelo(double mmNoPapel) => VistasSpk.Pes(mmNoPapel * escala / 1000);

        var pontos = bicos.Select(b => (b.Dado, Ponto: (b.Elemento!.Location as LocationPoint)?.Point)).ToList();
        var cantos = AreaDeCalculo(pontos.Where(p => p.Dado.NaArea && p.Ponto is not null).Select(p => p.Ponto!).ToList(),
            pontos.Where(p => p.Ponto is not null).Select(p => p.Ponto!).ToList(), direcao);
        if (cantos is not null)
        {
            Tentar(avisos, "Hachura da área de cálculo", () => PranchaSpk.RegiaoPreenchida(doc, planta,
                PranchaSpk.TipoDeRegiao(doc, TipoHachura, PranchaSpk.Padrao(doc, PadraoHachura, 3, Math.PI / 4), Vermelho), [cantos], nivel));
            var topo = cantos.OrderByDescending(c => c.Y).ThenBy(c => c.X).First();
            Tentar(avisos, "Rótulo da área", () => PranchaSpk.Nota(doc, planta, topo + new XYZ(0, NoModelo(4), 0),
                $"ÁREA DE CÁLCULO — {daArea.Count} chuveiros"));
        }

        foreach (var (dado, ponto) in pontos.Where(p => p.Dado.NaArea && p.Ponto is not null))
        {
            if (dado.Id == gravado.ChuveiroCritico) continue;
            var posicao = ponto! + new XYZ(NoModelo(1.5), NoModelo(1.5), 0);
            Tentar(avisos, "Número de bico", () => PranchaSpk.Nota(doc, planta, posicao, dado.Id));
        }

        if (critico.Elemento is { } bicoCritico)
        {
            var pintura = PranchaSpk.Pintura(doc, Vermelho, 8, preencher: true);
            Tentar(avisos, "Bico crítico na planta", () => planta.SetElementOverrides(bicoCritico.Id, pintura));
            if (bicoCritico.Location is LocationPoint lp)
                Tentar(avisos, "Rótulo do bico crítico", () => PranchaSpk.Nota(doc, planta, lp.Point + new XYZ(NoModelo(1.5), NoModelo(1.5), 0),
                    $"{critico.Dado.Id} — CRÍTICO\n{Bar(critico.Dado.PressaoKPa)} bar · {critico.Dado.VazaoLmin:0} L/min"));
        }

        var etiquetas = EtiquetarTubos(doc, planta, tubos, NoModelo(12), avisos);
        var descidas = MarcarDescidas(doc, planta, tubos, pontos.Where(p => p.Ponto is not null).Select(p => p.Ponto!).ToList(),
            NoModelo(2), avisos);

        // 3D do setor: bicos da área em laranja, crítico em vermelho.
        var iso = VistasSpk.Isometrico(doc, $"{PranchaSpk.PrefixoVista}{setor} — 3D", rede.Expandir(VistasSpk.Pes(0.5), VistasSpk.Pes(0.5)),
            VistasSpk.Template3d);
        var laranja = PranchaSpk.Pintura(doc, Laranja, 6, preencher: true);
        foreach (var (dado, elemento) in daArea)
            Tentar(avisos, "Bico da área no 3D", () => iso.SetElementOverrides(elemento!.Id,
                dado.Id == gravado.ChuveiroCritico ? PranchaSpk.Pintura(doc, Vermelho, 8, true) : laranja));

        ViewSection? corte = null;
        Tentar(avisos, "Corte do setor", () => corte = VistasSpk.Corte(doc, $"{PranchaSpk.PrefixoVista}{setor} — Corte", rede, direcao,
            VistasSpk.TemplateCorte));

        doc.Regenerate();
        Tentar(avisos, "Recorte do 3D", () => VistasSpk.Recortar3d(iso, rede.Expandir(VistasSpk.Pes(0.3), VistasSpk.Pes(0.3))));
        var (largura3d, altura3d) = VistasSpk.Extensao(iso);
        Tentar(avisos, "Escala do 3D", () => iso.Scale = regiao3d.Escala(largura3d, altura3d));

        var chave = MapaChave(contexto, setor, bicos.Select(b => b.Elemento!.Id).ToHashSet(), nivel, regiaoChave, avisos);

        PranchaSpk.Colocar(doc, folha, planta, regiaoPlanta.Centro, avisos);
        PranchaSpk.Colocar(doc, folha, iso, regiao3d.Centro, avisos);
        if (chave is not null) PranchaSpk.Colocar(doc, folha, chave, regiaoChave.Centro, avisos);
        Tentar(avisos, "Quadro de resultado", () => PranchaSpk.Nota(doc, folha, regiaoNota.CantoSuperiorEsquerdo,
            Quadro(gravado, setor, daArea.Count, critico.Dado), regiaoNota.Largura * 0.95));
        var folhas = new List<ElementId> { folha.Id };

        // Folha 02: o corte, sozinho.
        if (corte is { } vistaCorte)
        {
            var folhaCorte = PranchaSpk.Recriar(doc, $"{PranchaSpk.PrefixoNumero}{codigo}-02", $"Sprinklers — {setor} — corte");
            doc.Regenerate();
            var utilCorte = PranchaSpk.Area(folhaCorte).Parte(0.03, 0.08, 0.97, 0.97);
            var regiaoCorte = utilCorte.Parte(0, 0.15, 1, 1);
            var (larguraCorte, alturaCorte) = VistasSpk.Extensao(vistaCorte);
            Tentar(avisos, "Escala do corte", () => vistaCorte.Scale = regiaoCorte.Escala(larguraCorte, alturaCorte));
            PranchaSpk.Colocar(doc, folhaCorte, vistaCorte, regiaoCorte.Centro, avisos);
            Tentar(avisos, "Nota do corte", () => PranchaSpk.Nota(doc, folhaCorte, utilCorte.Parte(0, 0, 0.6, 0.13).CantoSuperiorEsquerdo,
                $"Corte no sentido dos ramais — {setor}. Mostra a descida, a subgeral e os ramais de perfil.",
                utilCorte.Largura * 0.55));
            folhas.Add(folhaCorte.Id);
        }

        var texto = string.Join("\n", folhas.Select(id => doc.GetElement(id) is ViewSheet s ? $"{s.SheetNumber}  {s.Name}" : ""));
        texto += $"\n\nPlanta 1:{escala}, {daArea.Count} bicos na área, {etiquetas} etiquetas de diâmetro, {descidas} descidas marcadas.";
        if (!gravado.Atende) texto += "\nO último cálculo NÃO ATENDE; as folhas mostram esse resultado.";
        if (!PranchaSpk.TemCarimbo(doc)) texto += "\nO projeto não tem carimbo. As folhas foram criadas sem ele.";
        if (avisos.Count > 0) texto += "\n\nAvisos:\n" + string.Join("\n", avisos.Distinct().Take(12).Select(a => "• " + a));
        return new FolhasGeradas(texto, folhas);
    }

    static string Quadro(ResultadoGravado gravado, string setor, int naArea, BicoGravado? critico)
    {
        var linhas = new List<string>
        {
            $"SPK — {setor}",
            gravado.Atende ? "ATENDE" : "NÃO ATENDE",
            gravado.Classificacao,
            $"Área de cálculo: {naArea} chuveiros (hachura)",
        };
        if (critico is not null)
            linhas.Add($"Bico crítico: nº {critico.Id} — {Bar(critico.PressaoKPa)} bar, {critico.VazaoLmin:0} L/min");
        linhas.Add($"Pressão necessária na fonte: {Bar(gravado.PressaoNecessariaKPa)} bar");
        linhas.Add($"Vazão dos chuveiros: {gravado.VazaoChuveirosLmin:0} L/min");
        if (gravado.VazaoTotalLmin > gravado.VazaoChuveirosLmin + 0.5)
            linhas.Add($"Vazão total com hidrantes: {gravado.VazaoTotalLmin:0} L/min");
        linhas.Add("Números dos bicos = nós do memorial de cálculo.");
        return string.Join("\n", linhas.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    static string Bar(double kPa) => (kPa / 100).ToString("0.00", Entrada.Cultura);

    /// <summary>
    /// Retângulo da área de operação, alinhado aos ramais: cobre os bicos da área com meio espaçamento de folga para cada
    /// lado. O espaçamento sai dos bicos do setor inteiro. Cantos em sentido anti-horário.
    /// </summary>
    static List<XYZ>? AreaDeCalculo(IReadOnlyList<XYZ> daArea, IReadOnlyList<XYZ> todos, XYZ d)
    {
        if (daArea.Count == 0) return null;
        var t = new XYZ(-d.Y, d.X, 0);
        var meioU = Passo(todos.Select(p => p.DotProduct(d))) / 2;
        var meioV = Passo(todos.Select(p => p.DotProduct(t))) / 2;
        var u0 = daArea.Min(p => p.DotProduct(d)) - meioU;
        var u1 = daArea.Max(p => p.DotProduct(d)) + meioU;
        var v0 = daArea.Min(p => p.DotProduct(t)) - meioV;
        var v1 = daArea.Max(p => p.DotProduct(t)) + meioV;
        XYZ P(double u, double v) => d * u + t * v;
        return [P(u0, v0), P(u1, v0), P(u1, v1), P(u0, v1)];
    }

    /// <summary>Mediana das distâncias entre posições vizinhas (agrupadas a 10 cm). Sem vizinhos, 3 m.</summary>
    static double Passo(IEnumerable<double> valores)
    {
        var tolerancia = VistasSpk.Pes(0.10);
        var grupos = new List<double>();
        foreach (var v in valores.OrderBy(v => v))
            if (grupos.Count == 0 || v - grupos[^1] > tolerancia) grupos.Add(v);
        var passos = grupos.Zip(grupos.Skip(1), (a, b) => b - a).OrderBy(p => p).ToList();
        return passos.Count == 0 ? VistasSpk.Pes(3.0) : passos[passos.Count / 2];
    }

    /// <summary>
    /// Planta do nível em escala pequena com a hachura de todos os setores e o deste em cor cheia, como o mapa-chave das
    /// folhas da Nova Era. Sem setores lidos (rede sem VGA mapeada) não há mapa-chave.
    /// </summary>
    static ViewPlan? MapaChave(ContextoFolhas contexto, string setor, HashSet<ElementId> bicosDoSetor, Level nivel, Regiao regiao,
        IList<string> avisos)
    {
        IReadOnlyList<SetorLido> setores;
        try
        {
            setores = contexto.Setores;
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            avisos.Add("Mapa-chave: " + e.Message);
            return null;
        }
        var noNivel = setores.Where(s => s.Contornos.ContainsKey(nivel.Id)).ToList();
        if (noNivel.Count < 2 || LeituraSetores.CaixaNoNivel(noNivel, nivel) is not { } caixa) return null;

        var doc = contexto.Doc;
        var recorte = caixa.Expandir(VistasSpk.Pes(3.0));
        var vista = VistasSpk.Planta(doc, $"{PranchaSpk.PrefixoVista}{setor} — Mapa-chave", nivel, recorte, VistasSpk.TemplateSetorizacao, avisos);
        VistasSpk.EsconderRede(vista);
        Tentar(avisos, "Escala do mapa-chave", () => vista.Scale = Math.Max(500, regiao.Escala(recorte.Max.X - recorte.Min.X, recorte.Max.Y - recorte.Min.Y)));
        foreach (var s in noNivel)
        {
            var esteSetor = s.Bicos.Any(b => bicosDoSetor.Contains(b.Id));
            LeituraSetores.Desenhar(doc, vista, s, nivel, cheio: esteSetor, esteSetor ? s.Nome.ToUpperInvariant() : s.Nome, avisos);
        }
        return vista;
    }

    /// <summary>
    /// Etiqueta de diâmetro nos tubos horizontais com pelo menos <paramref name="minimo" /> de comprimento. Usa a etiqueta de
    /// tubo padrão do projeto; sem ela, uma nota "Ø DN" no sentido do tubo.
    /// </summary>
    static int EtiquetarTubos(Document doc, View planta, IEnumerable<MEPCurve> tubos, double minimo, IList<string> avisos)
    {
        var tipo = doc.GetDefaultFamilyTypeId(new ElementId(BuiltInCategory.OST_PipeTags));
        if (tipo == ElementId.InvalidElementId)
            tipo = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeTags).WhereElementIsElementType().FirstElementId()
                   ?? ElementId.InvalidElementId;
        if (tipo == ElementId.InvalidElementId)
            avisos.Add("O projeto não tem etiqueta de tubo carregada; os diâmetros saíram como texto.");

        var feitas = 0;
        foreach (var tubo in tubos)
        {
            if (feitas >= MaximoEtiquetas)
            {
                avisos.Add($"Só os primeiros {MaximoEtiquetas} tubos receberam etiqueta.");
                break;
            }
            if (tubo.Location is not LocationCurve { Curve: Line linha }) continue;
            var v = linha.GetEndPoint(1) - linha.GetEndPoint(0);
            var h = new XYZ(v.X, v.Y, 0);
            if (h.GetLength() < minimo || Math.Abs(v.Z) > 0.1 * v.GetLength()) continue;
            var meio = linha.Evaluate(0.5, true);
            var ok = false;
            if (tipo != ElementId.InvalidElementId)
            {
                Tentar(avisos, "Etiqueta de tubo", () =>
                {
                    IndependentTag.Create(doc, tipo, planta.Id, new Reference(tubo), false, TagOrientation.Horizontal, meio);
                    ok = true;
                });
            }
            else
            {
                var dn = tubo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0;
                if (dn <= 0) continue;
                var angulo = Math.Atan2(h.Y, h.X);
                if (angulo > Math.PI / 2) angulo -= Math.PI;
                if (angulo <= -Math.PI / 2) angulo += Math.PI;
                Tentar(avisos, "Diâmetro em texto", () =>
                {
                    PranchaSpk.Nota(doc, planta, meio, $"Ø{VistasSpk.Milimetros(dn):0}", rotacao: angulo);
                    ok = true;
                });
            }
            if (ok) feitas++;
        }
        return feitas;
    }

    /// <summary>
    /// "DESCE" ou "SOBE" nos tubos verticais de 0,8 m ou mais que não são o tubinho de um bico: abaixo da cota média dos
    /// horizontais é descida para a rede de baixo, acima é subida.
    /// </summary>
    static int MarcarDescidas(Document doc, View planta, IReadOnlyList<MEPCurve> tubos, IReadOnlyList<XYZ> bicos, double afastamento,
        IList<string> avisos)
    {
        var linhas = tubos.Select(t => t.Location).OfType<LocationCurve>().Select(l => l.Curve).OfType<Line>().ToList();
        var cotas = linhas.Where(l => Math.Abs(l.Direction.Z) < 0.1).Select(l => l.Evaluate(0.5, true).Z).OrderBy(z => z).ToList();
        if (cotas.Count == 0) return 0;
        var cotaRede = cotas[cotas.Count / 2];
        var raioBico = VistasSpk.Pes(0.15);

        var feitas = 0;
        foreach (var linha in linhas)
        {
            if (Math.Abs(linha.Direction.Z) < 0.9 || linha.Length < VistasSpk.Pes(0.8)) continue;
            var a = linha.GetEndPoint(0);
            var b = linha.GetEndPoint(1);
            var baixo = a.Z < b.Z ? a : b;
            if (bicos.Any(p => Math.Abs(p.X - baixo.X) < raioBico && Math.Abs(p.Y - baixo.Y) < raioBico)) continue;
            var texto = linha.Evaluate(0.5, true).Z < cotaRede ? "DESCE" : "SOBE";
            Tentar(avisos, "Marca de descida", () =>
            {
                PranchaSpk.Nota(doc, planta, new XYZ(baixo.X + afastamento, baixo.Y - afastamento, 0), texto);
                feitas++;
            });
        }
        return feitas;
    }

    static void Tentar(IList<string> avisos, string oQue, Action acao)
    {
        try
        {
            acao();
        }
        catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
        {
            avisos.Add($"{oQue}: {e.Message}");
        }
    }
}
