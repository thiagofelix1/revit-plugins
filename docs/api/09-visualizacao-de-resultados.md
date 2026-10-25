# Visualização de resultados: 2D, 3D e blocos posicionados pela família

Como mostrar o resultado do cálculo de volta no modelo, sem necessariamente criar elementos permanentes. Cobre do mais simples (destacar um problema) ao mais trabalhoso (geometria 3D customizada por instância de família). Fontes: Developer's Guide 2027, seção **Basic Interaction with Revit Elements → Views** e **Revit Geometric Elements**.

## Mapa rápido

| Necessidade | API | Persiste no arquivo? | Esforço |
|---|---|---|---|
| Destacar elemento(s) sem criar nada | [`TemporaryGraphicsManager`](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Views_Temporary_Graphics_html) | Não (dura a sessão/até regenerar) | Baixo |
| Colorir elementos por faixa de um parâmetro (ex.: pressão) | [`ParameterFilterElement`](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Views_View_Filters_html) + `View.SetFilterOverrides` | Sim, o filtro fica salvo na vista | Baixo-médio |
| Pintar um campo contínuo sobre uma face/área (heatmap) | [Analysis Visualization Framework](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Analysis_Analysis_Visualization_html) (`SpatialFieldManager`) | Não (resultado não é salvo) | Médio |
| Bloco/forma 3D real posicionado num ponto (ex.: um cone de alcance do chuveiro) | [`DirectShape`](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_DirectShape_html) | Sim (é um elemento de verdade) | Médio |
| Overlay 3D ao vivo, sem criar elemento nenhum (visualizador/preview) | [Displaying Graphics with DirectContext3D](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Views_Displaying_Graphics_with_DirectContext3D_html) | Não | Alto |
| Gravar o número calculado no elemento | Parâmetro compartilhado (ver [04-modelo-e-dados.md](04-modelo-e-dados.md)) | Sim | Baixo |

Regra prática: comece pelo topo da tabela. `TemporaryGraphicsManager` e `ParameterFilterElement` resolvem a maior parte do que a Fase 1 do SPK precisa (destacar a área mais desfavorável, colorir trechos por pressão/velocidade) sem exigir geometria customizada.

## Destacar sem alterar o modelo

`TemporaryGraphicsManager` desenha marcadores (caixas, esferas, setas) numa vista que desaparecem ao fechar o comando ou regenerar — não cria `Element`, não precisa de `Transaction`. Desde o Revit 2026 aceita `SetTooltip()`. É o mecanismo certo para "contornar a área de operação mais desfavorável" sem comprometer o modelo.

```csharp
var manager = TemporaryGraphicsManager.GetTemporaryGraphicsManager(doc);
var marcador = TemporaryGraphicObjectUtils.CreateBoundingBoxShape(boundingBox);
manager.AddTemporaryGraphicObject(marcador, view.Id);
```

(assinaturas exatas variam por versão — confirme no `RevitAPI.chm` instalado; o padrão de uso é sempre "pegue o manager da vista, crie a forma, adicione".)

## Colorir elementos por um valor calculado

Para "colorir os trechos de tubo por pressão/velocidade", a API mais idiomática não é a de análise — é a mesma usada pelos **filtros de vista** que o usuário já vê na interface (Visibility/Graphics → Filtros). Vantagem: fica salvo na vista, sobrevive a reabrir o modelo, e o engenheiro pode inspecionar/ajustar pela UI nativa depois, sem precisar do add-in.

```csharp
using var t = new Transaction(doc, "Colorir pressão calculada");
t.Start();

var regra = ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parametroPressaoId, 600.0, 1e-6);
var filtro = ParameterFilterElement.Create(doc, "SPK — pressão >= 600 kPa",
    [new ElementId(BuiltInCategory.OST_PipeCurves)], [new ElementParameterFilter(regra)]);

var ogs = new OverrideGraphicSettings().SetSurfaceForegroundPatternColor(new Color(255, 0, 0));
view.SetFilterOverrides(filtro.Id, ogs);
t.Commit();
```

Isso exige que a pressão já esteja gravada como parâmetro no trecho (passo anterior, via `04-modelo-e-dados.md`). Recalcular = reescrever o parâmetro; o filtro de vista recolore automaticamente, sem o add-in tocar em cor de novo.

## Pintar um campo contínuo sobre uma face (Analysis Visualization Framework)

Para um gradiente contínuo (não discreto por elemento) sobre uma face ou área — por exemplo, "mapa de calor" da densidade de água na área de operação — o `SpatialFieldManager` registra um `AnalysisResultSchema` e pinta valores por face, sem gravar nada no arquivo além do próprio desenho temporário da vista. É a mesma família de API usada para análise solar/energética nativa do Revit. Mais pesado de configurar que um filtro de vista; só vale a pena se o resultado for mesmo um campo contínuo, não um valor por elemento.

## Montar blocos 3D posicionados pela família

Aqui entra a parte "de acordo com a família": pra desenhar um bloco (caixa, cone, seta) no lugar certo, perto do chuveiro ou do trecho de tubo, primeiro é preciso saber **onde** a instância está e **como ela está orientada** — isso vem da geometria da própria `FamilyInstance`, não de um valor fixo.

### Posição e orientação da instância

- `Instance.GetTransform()`: a transformação (posição + rotação + escala) de como a família foi colocada no modelo. É o que converte um ponto desenhado "no espaço da família" (0,0,0 = origem da família) para coordenadas do projeto.
- `Element.get_Geometry(Options)` devolve um `GeometryElement`; para uma `FamilyInstance` normalmente vem na forma de uma `GeometryInstance`, cuja `GetInstanceGeometry()` já devolve a geometria transformada, ou `GetSymbolGeometry()` devolve a geometria "crua", no espaço da família (precisa aplicar `GetTransform()` manualmente).
- `GeometryElement.GetBoundingBox()`: tamanho da caixa que envolve a geometria — útil pra saber o tamanho do bloco a desenhar (ex.: do tamanho do próprio chuveiro, ou um múltiplo dele).
- Fonte: [Geometry Helper Classes](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Geometry_Geometry_Helper_Classes_html).

```csharp
var opcoes = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
var geometria = chuveiroInstance.get_Geometry(opcoes);
var transformada = geometria.GetTransformed(chuveiroInstance.GetTransform());
var caixa = transformada.GetBoundingBox();
```

### Criar o bloco (DirectShape)

`DirectShape` é um elemento real, mas não precisa vir de uma família carregada — você constrói a geometria (`Solid`, via `GeometryCreationUtilities`) na hora e associa a uma categoria (normalmente `OST_GenericModel` pra um elemento de visualização que não é nada "de verdade" no projeto).

```csharp
using var t = new Transaction(doc, "SPK — marcador 3D de pressão crítica");
t.Start();

var origem = chuveiroInstance.GetTransform().Origin;
var solido = GeometryCreationUtilities.CreateExtrusionGeometry(
    [CurveLoop.Create([Line.CreateBound(origem, origem + XYZ.BasisX * 0.3)])],
    XYZ.BasisZ, 0.5);

var bloco = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
bloco.SetShape([solido]);
t.Commit();
```

Pontos de atenção:

- É um elemento comum: o usuário pode selecioná-lo, movê-lo, apagá-lo por acidente. Pra recalcular, apague os blocos antigos (guarde os `ElementId` em Extensible Storage — ver `04-modelo-e-dados.md`) antes de criar os novos.
- Diferente de `TemporaryGraphicsManager`, isso **persiste** no arquivo até alguém apagar — não é a opção certa pra algo que deve desaparecer quando o cálculo fica desatualizado, a menos que o próprio add-in se responsabilize por limpar.
- `FamilyInstance.Symbol.Family.FamilyPlacementType` diz como a família foi projetada pra ser colocada (`OneLevelBased`, `TwoLevelsBased`, `WorkPlaneBased`, `CurveBased`, etc.) — importa pra saber se "a posição" é um ponto único (chuveiro) ou uma curva (não é bem o caso de família, mas de tubo: `Pipe.Location` é uma `LocationCurve`, não `LocationPoint`).

### Overlay 3D ao vivo (DirectContext3D)

Pra algo mais parecido com "visualização de engenharia" de verdade — vazão animada, gradiente suave em tempo real, sem criar elemento nenhum — existe o `DirectContext3D`: você implementa `IDirectContext3DServer` com dois métodos (`GetBoundingBox()` e `RenderScene()`) e registra como serviço externo; o Revit injeta a geometria que você mandar (buffers de vértice/índice, no estilo OpenGL) direto no pipeline de renderização da vista, convivendo com os elementos nativos. Não cria nem altera elemento — é puramente visual, desenhado de novo a cada frame.

Isso é bem mais trabalhoso que `DirectShape` (programação gráfica de baixo nível, sem ajuda de `GeometryCreationUtilities`) e só vale o esforço se a visualização precisar ser interativa/ao vivo (ex.: arrastar um slider de vazão e ver o gradiente mudar sem reabrir transação). Pra um memorial/relatório estático, `DirectShape` ou os filtros de vista já resolvem.

## Recomendação para o SPK

| Fase 1 (calcular o modelado) | Mecanismo |
|---|---|
| Destacar a área mais desfavorável encontrada pela busca | `TemporaryGraphicsManager` (some ao fechar/recalcular, não precisa de limpeza) |
| Colorir trechos de tubo por pressão/velocidade calculada | `ParameterFilterElement` + `OverrideGraphicSettings`, sobre o parâmetro já gravado |
| Gravar o número em si (pressão, vazão, velocidade) | Parâmetro compartilhado, como já descrito em `04-modelo-e-dados.md` |

Deixar `DirectShape` (blocos 3D reais) e `DirectContext3D` (overlay ao vivo) pra depois da POC: são a ferramenta certa quando o produto evoluir para mostrar, por exemplo, o alcance de cobertura de cada chuveiro em 3D ou uma visualização mais rica no painel do add-in — mas não são bloqueio pra provar o fluxo "ler modelo → calcular → mostrar resultado" com o Ciroca.
