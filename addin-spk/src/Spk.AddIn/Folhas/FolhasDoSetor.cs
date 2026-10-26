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
        var util = PranchaSpk.Util(folha);
        var regiaoPlanta = util.Parte(0, 0.17, 0.64, 1);
        var regiaoNota = util.Parte(0, 0, 0.64, 0.15);
        var lateral = util.Parte(0.66, 0, 1, 1);
        var regiao3d = lateral.Parte(0, 0.58, 1, 1);
        var regiaoChave = lateral.Parte(0, 0.30, 1, 0.56);

        var nivel = LeituraSetores.NivelDaMaioria(doc, bicos.Select(b => b.Elemento!));
        var recorte = rede.Expandir(VistasSpk.Pes(2.0));
        var planta = VistasSpk.Planta(doc, $"{PranchaSpk.PrefixoVista}{setor} — Planta", nivel, recorte, VistasSpk.TemplatePlanta, avisos);
        PranchaSpk.Tentar(avisos, "Escala da planta", () => planta.Scale = regiaoPlanta.Escala(recorte.Max.X - recorte.Min.X, recorte.Max.Y - recorte.Min.Y));
        var escala = planta.Scale;
        double NoModelo(double mmNoPapel) => VistasSpk.Pes(mmNoPapel * escala / 1000);

        var pontos = bicos.Select(b => (b.Dado, Ponto: (b.Elemento!.Location as LocationPoint)?.Point)).ToList();
        var lacos = AreaDeCalculo(pontos.Where(p => p.Dado.NaArea && p.Ponto is not null).Select(p => p.Ponto!).ToList(),
            pontos.Where(p => p.Ponto is not null).Select(p => p.Ponto!).ToList(), direcao);
        if (lacos.Count > 0)
        {
            PranchaSpk.Tentar(avisos, "Hachura da área de cálculo", () => PranchaSpk.RegiaoPreenchida(doc, planta,
                PranchaSpk.TipoDeRegiao(doc, TipoHachura, PranchaSpk.Padrao(doc, PadraoHachura, 3, Math.PI / 4), Vermelho), lacos, nivel));
            var topo = lacos.SelectMany(l => l).OrderByDescending(c => c.Y).ThenBy(c => c.X).First();
            PranchaSpk.Tentar(avisos, "Rótulo da área", () => PranchaSpk.Nota(doc, planta, topo + new XYZ(0, NoModelo(4), 0),
                $"ÁREA DE CÁLCULO — {daArea.Count} chuveiros"));
        }

        foreach (var (dado, ponto) in pontos.Where(p => p.Dado.NaArea && p.Ponto is not null))
        {
            if (dado.Id == gravado.ChuveiroCritico) continue;
            var posicao = ponto! + new XYZ(NoModelo(1.5), NoModelo(1.5), 0);
            PranchaSpk.Tentar(avisos, "Número de bico", () => PranchaSpk.Nota(doc, planta, posicao, dado.Id));
        }

        if (critico.Elemento is { } bicoCritico)
        {
            var pintura = PranchaSpk.Pintura(doc, Vermelho, 8, preencher: true);
            PranchaSpk.Tentar(avisos, "Bico crítico na planta", () => planta.SetElementOverrides(bicoCritico.Id, pintura));
            if (bicoCritico.Location is LocationPoint lp)
                PranchaSpk.Tentar(avisos, "Rótulo do bico crítico", () => PranchaSpk.Nota(doc, planta, lp.Point + new XYZ(NoModelo(1.5), NoModelo(1.5), 0),
                    $"{critico.Dado.Id} — CRÍTICO\n{PranchaSpk.Bar(critico.Dado.PressaoKPa)} bar · {critico.Dado.VazaoLmin:0} L/min"));
        }

        var etiquetas = EtiquetarTubos(doc, planta, tubos, NoModelo(12), avisos);
        var descidas = MarcarDescidas(doc, planta, tubos, pontos.Where(p => p.Ponto is not null).Select(p => p.Ponto!).ToList(),
            NoModelo(2), avisos);

        // 3D do setor: bicos da área em laranja, crítico em vermelho.
        var iso = VistasSpk.Isometrico(doc, $"{PranchaSpk.PrefixoVista}{setor} — 3D", rede.Expandir(VistasSpk.Pes(0.5), VistasSpk.Pes(0.5)),
            VistasSpk.Template3d);
        var laranja = PranchaSpk.Pintura(doc, Laranja, 6, preencher: true);
        foreach (var (dado, elemento) in daArea)
            PranchaSpk.Tentar(avisos, "Bico da área no 3D", () => iso.SetElementOverrides(elemento!.Id,
                dado.Id == gravado.ChuveiroCritico ? PranchaSpk.Pintura(doc, Vermelho, 8, true) : laranja));

        ViewSection? corte = null;
        PranchaSpk.Tentar(avisos, "Corte do setor", () => corte = VistasSpk.Corte(doc, $"{PranchaSpk.PrefixoVista}{setor} — Corte", rede, direcao,
            VistasSpk.TemplateCorte));

        doc.Regenerate();
        var doSetor = elementos.Select(e => e.Id).ToHashSet();
        EsconderOutrosSetores(doc, iso, doSetor, avisos);
        if (corte is not null) EsconderOutrosSetores(doc, corte, doSetor, avisos);
        EsconderOutrosCortes(doc, planta, $"{PranchaSpk.PrefixoVista}{setor} — Corte", avisos);
        PranchaSpk.Tentar(avisos, "Recorte do 3D", () => VistasSpk.Recortar3d(iso, rede.Expandir(VistasSpk.Pes(0.3), VistasSpk.Pes(0.3))));
        var (largura3d, altura3d) = VistasSpk.Extensao(iso);
        PranchaSpk.Tentar(avisos, "Escala do 3D", () => iso.Scale = regiao3d.Escala(largura3d, altura3d));
        var escala3d = iso.Scale;
        PranchaSpk.Tentar(avisos, "Travar o 3D para etiquetar", () =>
        {
            iso.SaveOrientationAndLock();
            FolhasIsometrico.Etiquetar(doc, iso, tubos.Select(t => t.Id).ToHashSet(), VistasSpk.Pes(Math.Max(1.0, 12.0 * escala3d / 1000)), avisos);
        });

        var chave = MapaChave(contexto, setor, bicos.Select(b => b.Elemento!.Id).ToHashSet(), nivel, regiaoChave, avisos);

        PranchaSpk.Colocar(doc, folha, planta, regiaoPlanta.Centro, avisos);
        PranchaSpk.Colocar(doc, folha, iso, regiao3d.Centro, avisos);
        if (chave is not null) PranchaSpk.Colocar(doc, folha, chave, regiaoChave.Centro, avisos);
        PranchaSpk.ColocarLegendas(doc, folha, lateral.Parte(0, 0, 1, 0.28), avisos);
        PranchaSpk.Tentar(avisos, "Quadro de resultado", () => PranchaSpk.Nota(doc, folha, regiaoNota.CantoSuperiorEsquerdo,
            Quadro(gravado, setor, daArea.Count, critico.Dado), regiaoNota.Largura * 0.95));
        var folhas = new List<ElementId> { folha.Id };

        // Folha 02: o corte, sozinho.
        if (corte is { } vistaCorte)
        {
            var folhaCorte = PranchaSpk.Recriar(doc, $"{PranchaSpk.PrefixoNumero}{codigo}-02", $"Sprinklers — {setor} — corte");
            doc.Regenerate();
            var utilCorte = PranchaSpk.Util(folhaCorte);
            var regiaoCorte = utilCorte.Parte(0, 0.15, 1, 1);
            var (larguraCorte, alturaCorte) = VistasSpk.Extensao(vistaCorte);
            PranchaSpk.Tentar(avisos, "Escala do corte", () => vistaCorte.Scale = regiaoCorte.Escala(larguraCorte, alturaCorte));
            PranchaSpk.Colocar(doc, folhaCorte, vistaCorte, regiaoCorte.Centro, avisos);
            PranchaSpk.Tentar(avisos, "Nota do corte", () => PranchaSpk.Nota(doc, folhaCorte, utilCorte.Parte(0, 0, 0.6, 0.13).CantoSuperiorEsquerdo,
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
            linhas.Add($"Bico crítico: nº {critico.Id} — {PranchaSpk.Bar(critico.PressaoKPa)} bar, {critico.VazaoLmin:0} L/min");
        linhas.Add($"Pressão necessária na fonte: {PranchaSpk.Bar(gravado.PressaoNecessariaKPa)} bar");
        linhas.Add($"Vazão dos chuveiros: {gravado.VazaoChuveirosLmin:0} L/min");
        if (gravado.VazaoTotalLmin > gravado.VazaoChuveirosLmin + 0.5)
            linhas.Add($"Vazão total com hidrantes: {gravado.VazaoTotalLmin:0} L/min");
        linhas.Add("Números dos bicos = nós do memorial de cálculo.");
        return string.Join("\n", linhas.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    /// <summary>
    /// Retângulo da área de operação, alinhado aos ramais: cobre os bicos da área com meio espaçamento de folga para cada
    /// lado. O espaçamento sai dos bicos do setor inteiro. Cantos em sentido anti-horário.
    /// </summary>
    /// <summary>
    /// Laços da hachura: a união da cobertura de cada bico da área (meio passo para cada lado, o passo medido em todos os
    /// bicos do setor). A área de 12 bicos em ramais de 5 fica em "L" (5 + 5 + 2), e a hachura não cobre os bicos de fora.
    /// </summary>
    static List<List<XYZ>> AreaDeCalculo(IReadOnlyList<XYZ> daArea, IReadOnlyList<XYZ> todos, XYZ d)
    {
        if (daArea.Count == 0) return [];
        var t = new XYZ(-d.Y, d.X, 0);
        double U(XYZ p) => VistasSpk.Metros(p.DotProduct(d));
        double V(XYZ p) => VistasSpk.Metros(p.DotProduct(t));
        var meioU = Spk.Motor.Modelo.Contorno.Passo(todos.Select(U)) / 2;
        var meioV = Spk.Motor.Modelo.Contorno.Passo(todos.Select(V)) / 2;
        var coberturas = daArea.Select(p => (U(p) - meioU, V(p) - meioV, U(p) + meioU, V(p) + meioV)).ToList();
        return Spk.Motor.Modelo.Contorno.DeRetangulos(coberturas, vaoMaximo: 0.3)
            .Select(laco => laco.Select(p => d * VistasSpk.Pes(p.U) + t * VistasSpk.Pes(p.V)).ToList())
            .ToList();
    }

    /// <summary>
    /// Esconde bicos e tubos que não são do setor, e as conexões e válvulas ligadas só a eles: no corte e no 3D, a rede de
    /// outro setor que passa dentro do recorte (a descida do pavimento de cima, por exemplo) não aparece. Peça sem ligação
    /// fica visível, porque num modelo lido por proximidade ela pode ser do setor.
    /// </summary>
    static void EsconderOutrosSetores(Document doc, View vista, HashSet<ElementId> doSetor, IList<string> avisos)
    {
        var fora = new List<ElementId>();
        foreach (var categoria in new[] { BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_Sprinklers })
        foreach (var elemento in new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsNotElementType())
            if (!doSetor.Contains(elemento.Id) && elemento.CanBeHidden(vista)) fora.Add(elemento.Id);
        foreach (var categoria in new[] { BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_PipeAccessory })
        foreach (var elemento in new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsNotElementType())
        {
            if (elemento is not FamilyInstance { MEPModel.ConnectorManager: { } conectores } || !elemento.CanBeHidden(vista)) continue;
            var ligadas = conectores.Connectors.Cast<Connector>().Where(c => c.IsConnected)
                .SelectMany(c => c.AllRefs.Cast<Connector>()).Select(r => r.Owner.Id).Where(id => id != elemento.Id).ToList();
            if (ligadas.Count > 0 && !ligadas.Any(doSetor.Contains)) fora.Add(elemento.Id);
        }
        if (fora.Count > 0) PranchaSpk.Tentar(avisos, "Esconder os outros setores", () => vista.HideElements(fora));
    }

    /// <summary>
    /// Marcas de corte do SPK que não são o corte deste setor (o corte da Rede de exemplo, cortes de outros setores de
    /// rodadas anteriores). Cortes do projeto, com outros nomes, ficam como estão.
    /// </summary>
    static void EsconderOutrosCortes(Document doc, View planta, string corte, IList<string> avisos)
    {
        var outros = new FilteredElementCollector(doc, planta.Id).OfCategory(BuiltInCategory.OST_Viewers).WhereElementIsNotElementType()
            .Where(e => e.Name.StartsWith(PranchaSpk.PrefixoVista, StringComparison.Ordinal) && e.Name != corte && e.CanBeHidden(planta))
            .Select(e => e.Id)
            .ToList();
        if (outros.Count > 0) PranchaSpk.Tentar(avisos, "Esconder outros cortes na planta", () => planta.HideElements(outros));
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
        PranchaSpk.Tentar(avisos, "Escala do mapa-chave", () => vista.Scale = Math.Max(500, regiao.Escala(recorte.Max.X - recorte.Min.X, recorte.Max.Y - recorte.Min.Y)));
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
        var tipo = PranchaSpk.EtiquetaDeTubo(doc);
        if (tipo == ElementId.InvalidElementId)
            avisos.Add("O projeto não tem etiqueta de tubo carregada; os diâmetros saíram como texto.");
        else
            PranchaSpk.Tentar(avisos, "Mostrar as etiquetas na planta", () => VistasSpk.MostrarEtiquetas(doc, planta));

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
                PranchaSpk.Tentar(avisos, "Etiqueta de tubo", () =>
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
                PranchaSpk.Tentar(avisos, "Diâmetro em texto", () =>
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
    /// "DESCE" no tubo vertical que sai da rede para baixo (a ponta de baixo abaixo do tubo horizontal mais baixo, rumo à
    /// VGA) e "SOBE" no que sai para cima. Trechos verticais entre tubos da própria rede, como o degrau entre subgeral e
    /// ramal num telhado inclinado, e o tubinho de cada bico não são marcados. Uma marca por prumada.
    /// </summary>
    static int MarcarDescidas(Document doc, View planta, IReadOnlyList<MEPCurve> tubos, IReadOnlyList<XYZ> bicos, double afastamento,
        IList<string> avisos)
    {
        var linhas = tubos.Select(t => t.Location).OfType<LocationCurve>().Select(l => l.Curve).OfType<Line>().ToList();
        var horizontais = linhas.Where(l => Math.Abs(l.Direction.Z) < 0.1).ToList();
        if (horizontais.Count == 0) return 0;
        var folga = VistasSpk.Pes(0.30);
        var piso = horizontais.Min(l => Math.Min(l.GetEndPoint(0).Z, l.GetEndPoint(1).Z)) - folga;
        var teto = horizontais.Max(l => Math.Max(l.GetEndPoint(0).Z, l.GetEndPoint(1).Z)) + folga;
        var raioBico = VistasSpk.Pes(0.15);
        var marcadas = new List<XYZ>();

        var feitas = 0;
        foreach (var linha in linhas)
        {
            if (Math.Abs(linha.Direction.Z) < 0.9 || linha.Length < VistasSpk.Pes(0.8)) continue;
            var a = linha.GetEndPoint(0);
            var b = linha.GetEndPoint(1);
            var baixo = a.Z < b.Z ? a : b;
            var alto = a.Z < b.Z ? b : a;
            string texto;
            if (baixo.Z < piso) texto = "DESCE";
            else if (alto.Z > teto) texto = "SOBE";
            else continue;
            if (bicos.Any(p => Math.Abs(p.X - baixo.X) < raioBico && Math.Abs(p.Y - baixo.Y) < raioBico)) continue;
            if (marcadas.Any(m => Math.Abs(m.X - baixo.X) < folga && Math.Abs(m.Y - baixo.Y) < folga)) continue;
            marcadas.Add(baixo);
            PranchaSpk.Tentar(avisos, "Marca de descida", () =>
            {
                PranchaSpk.Nota(doc, planta, new XYZ(baixo.X + afastamento, baixo.Y - afastamento, 0), texto);
                feitas++;
            });
        }
        return feitas;
    }
}
