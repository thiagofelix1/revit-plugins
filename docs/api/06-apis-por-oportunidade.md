# APIs por oportunidade de produto

Liga as oportunidades do plano de negócio às classes da Revit API que resolvem cada uma. O objetivo é mostrar o que já vem pronto e onde está o trabalho de verdade.

As regras numéricas das normas não aparecem aqui de propósito: as NBRs são pagas e protegidas por direito autoral, e os valores precisam vir do texto vigente, com o item citado, na base de regras do produto.

## Blocos comuns a todas as oportunidades

| Necessidade | API | Página do guia |
|---|---|---|
| Listar ambientes, área, perímetro, volume, pé-direito | `Room` (`Area`, `Perimeter`, `Volume`, `UnboundedHeight`, `Number`, `Name`, `Level`) | [Rooms](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Discipline_Specific_Functionality_Architecture_Rooms_html) |
| Contorno do ambiente | `SpatialElement.GetBoundarySegments(SpatialElementBoundaryOptions)`; `SpatialElementBoundaryLocation.Finish` (face de acabamento) ou `Center` (eixo) | [Rooms](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Discipline_Specific_Functionality_Architecture_Rooms_html) |
| Faces e materiais de cada lado do ambiente | `SpatialElementGeometryCalculator` | [Room and Space Geometry](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Geometry_Room_and_Space_Geometry_html) |
| Ponto dentro de ambiente | `Room.IsPointInRoom(XYZ)` | Rooms |
| Qual ambiente cada porta liga | `FamilyInstance.FromRoom` / `ToRoom` (lado para onde a porta abre é `ToRoom`; ambos podem ser `null`) | Rooms, seção "Room and FamilyInstance" |
| Destacar problemas sem alterar o modelo | `TemporaryGraphicsManager`; `Result.Failed` com `elements`; `UIDocument.Selection.SetElementIds()` | [Temporary Graphics](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Views_Temporary_Graphics_html) |
| Colorir resultados na vista | Analysis Visualization Framework (`SpatialFieldManager`); resultados não são salvos no arquivo | [Analysis Visualization](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Analysis_Analysis_Visualization_html) |
| Tabelas (quantitativos, quadro de áreas) | `ViewSchedule`, `ScheduleDefinition`, `GetTableData()` | [ViewSchedule](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Views_View_Types_TableView_ViewSchedule_html) |
| Gravar o resultado no modelo | Parâmetros compartilhados e Extensible Storage | [04-modelo-e-dados.md](04-modelo-e-dados.md) |

Armadilhas de ambientes que o guia documenta:

- `GetBoundarySegments()` devolve `null` se o ambiente não está fechado ou só existe na tabela. Filtrar `Area > 0` evita ambientes "não colocados" e "não fechados".
- `Volume` vem 0 se o cálculo de volumes estiver desligado (`AreaVolumeSettings.ComputeVolumes`).
- Depois de criar ou mover paredes numa transação, os contornos só ficam certos após regenerar.

## 1. Orçamento SINAPI/SICRO assistido por IA

O núcleo técnico é extrair quantitativos medidos do jeito que cada composição exige, e não do jeito que o modelo foi feito.

| Necessidade | API | Observação |
|---|---|---|
| Volume e área por material de cada elemento | `Element.GetMaterialIds()`, `GetMaterialVolume()`, `GetMaterialArea()` | Só para categorias com `Category.HasMaterialQuantities`: paredes, lajes, coberturas, forros, escadas e famílias com material na geometria. Cortinas de vidro não reportam: ler os painéis |
| Camadas de paredes, lajes e coberturas (alvenaria, chapisco, reboco, contrapiso) | `HostObjAttributes.GetCompoundStructure()`, `CompoundStructure.GetLayers()` (espessura, função, material) | [CompoundStructure](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Walls_Floors_Ceilings_Roofs_and_Openings_CompoundStructure_html) |
| Quantidade bruta e líquida (com e sem vãos) | Transação temporária: apagar portas e janelas, regenerar, medir, `RollBack()` | Exemplo do SDK `MaterialQuantities`; [Temporary transactions](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Transactions_Temporary_transactions_html). É aqui que entram os critérios de desconto de vãos das composições |
| Áreas de revestimento por ambiente (piso, parede, teto) | `SpatialElementGeometryCalculator` (face e material de cada lado) | Liga quantitativo a ambiente, útil para memória de cálculo |
| Contagens (portas, janelas, louças, pontos) | `FilteredElementCollector` por categoria + tipo | |
| Dividir paredes e lajes por camada | `PartUtils.CreateParts()`, `DivideParts()` | [Parts](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Construction_Modeling_Parts_html). Alternativa quando o orçamentista quer cada camada como elemento |
| Fases (existente, demolir, nova) | `Element.CreatedPhaseId`, `DemolishedPhaseId`; `Document.Phases` | Reformas: orçar só o que é novo ou demolido |
| Guardar o código da composição no elemento ou no tipo | Parâmetro compartilhado "Código SINAPI" | Visível em tabelas e exportável para IFC |

Observações do guia sobre precisão: as áreas e volumes por material "podem ser aproximados em alguns casos", com pequenas diferenças por camada quando há perfis (sweeps) ou certas junções. Para orçamento de licitação, a memória de cálculo precisa registrar o método usado.

Onde a IA entra: sugerir a composição para cada combinação de categoria + tipo + camadas + material, explicar o critério de medição e marcar o que falta no modelo. A extração dos números continua determinística.

Sinal positivo: a pesquisa da Poli-USP citada no plano de negócio diz que o problema é o critério de medição não bater com a modelagem. A API dá acesso aos dados brutos (camadas, faces por ambiente, quantidades com e sem vãos) com que essa ponte é construída.

## 2. Verificação de normas com explicação em português

### NBR 9050 (acessibilidade)

| Verificação típica | Dados e API |
|---|---|
| Largura livre de portas | Parâmetro de largura do tipo da porta (`FamilyInstance.Symbol`); varia por família, conferir com RevitLookup. Vão livre ≠ largura nominal: pode exigir parâmetro próprio |
| Largura de circulações | Contorno dos ambientes (`GetBoundarySegments` com `Finish`) e geometria; medir a menor distância entre faces opostas exige cálculo geométrico próprio |
| Área de giro e manobra | Teste geométrico de um círculo dentro do contorno do ambiente, descontando mobiliário (`FilteredElementCollector` por `OST_Furniture`, `OST_PlumbingFixtures` e `BoundingBoxIntersectsFilter`) |
| Rampas: inclinação e largura | Rampas são categoria `OST_Ramps`; inclinação via parâmetros e geometria. Não há classe de API dedicada a rampas no guia; validar em modelos reais |
| Sanitários acessíveis | Ambiente + louças contidas (`IsPointInRoom` no ponto de inserção) + dimensões do ambiente |
| Quantidade de elementos acessíveis | Contagens por ambiente ou pavimento |

### NBR 9077 e ITs dos Corpos de Bombeiros (saídas de emergência)

A API mais valiosa deste mapeamento: [Path Of Travel](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Analysis_Path_Of_Travel_html).

- `PathOfTravel.Create()`, `CreateMultiple()`, `CreateMapped()`: criam rotas entre pontos, em vista de planta, desviando dos elementos do modelo.
- `PathOfTravel.FindStartsOfLongestPathsFromRooms(view, destinos)`: divide a planta em pequenas células, calcula a distância de cada célula dentro de ambientes até o destino mais próximo e **devolve os pontos de partida dos caminhos mais longos**. Com as saídas como destinos, isso responde "qual o ponto mais distante de uma saída neste pavimento e quanto ele percorre".
- `FindShortestPaths()` e `FindEndsOfShortestPaths()`: caminhos de vários pontos até a saída mais próxima.
- `GetCurves()` devolve a geometria da rota (comprimento = soma das curvas, em pés).
- `RouteAnalysisSettings`: categorias ignoradas como obstáculo (por exemplo, mobiliário solto), altura da faixa de análise e se ignora importações.
- Waypoints (`InsertWaypoint()`): forçar a rota a passar por pontos.

| Verificação típica | Dados e API |
|---|---|
| Distância máxima a percorrer | `FindStartsOfLongestPathsFromRooms` + `Create` + `GetCurves`, comparando com o limite da IT do estado para a ocupação |
| Largura de escadas e saídas, unidades de passagem | `Stairs` (`ActualTreadDepth`, `ActualRiserHeight`, `ActualTreadsNumber`, `Height`), `StairsRun.ActualRunWidth`, `StairsType.MinRunWidth`, `MaxRiserHeight`; portas de saída por largura ([Stairs and Railings](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Stairs_and_Railings_html)) |
| População e lotação | Área dos ambientes × densidade da ocupação (tabela da IT); exige classificar o uso de cada ambiente (parâmetro ou inferência por nome, onde a IA ajuda) |
| Corrimãos e guarda-corpos | `Railing` (mesma seção do guia) |

Limites: o guia documenta `PathOfTravel` só para **vistas de planta**, ou seja, pavimento a pavimento. A suposição de trabalho é que a rota não atravessa escadas entre pavimentos, então distâncias que somam pavimentos teriam de ser compostas pelo plugin; confirmar numa prova de conceito. Para usar a classe `Stairs`, `Stairs.IsByComponent()` precisa ser `true`.

### NBR 12721 (quadros de áreas e CUB)

| Necessidade | API |
|---|---|
| Áreas por ambiente e pavimento | `Room.Area`, `Room.Level` |
| Planos de área com regras próprias (área privativa, comum, equivalente) | Áreas (`Area`) em esquemas de área (`AreaScheme`), cujo contorno são linhas da categoria `OST_AreaSchemeLines` (guia, página Rooms) |
| Quadros e tabelas | `ViewSchedule` com campos de área, ou geração externa a partir dos dados extraídos |
| Dados do empreendimento | `Document.ProjectInformation` |

Esta é a verificação de menor risco técnico: dados prontos no modelo, regra aritmética e saída tabular.

### NBR 5410 (pontos mínimos por ambiente) e NBR 15575 (parte prescritiva)

- Pontos de tomada e iluminação: contar `OST_ElectricalFixtures`, `OST_LightingFixtures`, `OST_LightingDevices` por ambiente (`FamilyInstance.Room` ou `IsPointInRoom`), comparar com o perímetro (`Room.Perimeter`) e a área.
- Pé-direito: `Room.UnboundedHeight` ou geometria do ambiente.
- Iluminação natural: janelas por ambiente via `FromRoom`/`ToRoom` e área de vão.

## 3. Validação de entregáveis para licitação (cadernos BIM)

| Necessidade | API |
|---|---|
| Parâmetros obrigatórios presentes e preenchidos | Parâmetros compartilhados por GUID; `FilterRule.CreateHasNoValueParameterRule()` para achar os vazios |
| Padrão de nomes (tipos, vistas, folhas, níveis) | Coletores por categoria + regex no `Name` |
| Classificação e mapeamento para IFC | `IFCParameterTemplate`, `IFCPropertyMappingInfo` (mapeamento de parâmetros para property sets) |
| Exportar IFC | `Document.Export(pasta, nome, IFCExportOptions)`; exportador aberto em https://github.com/Autodesk/revit-ifc ([IFC Export](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Export_IFC_Export_html)) |
| Georreferenciamento | `Document.SiteLocation`, `ProjectLocation`, `ProjectPosition` (seção Place and Locations) |
| Modelo como construído (as built), fases | `Phase`, parâmetros de status |
| Vínculos e disciplinas | `RevitLinkInstance` ([Revit Links](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Linked_Files_Revit_Links_html)) |

Este produto roda bem em lote no Design Automation ([07-design-automation.md](07-design-automation.md)): abrir o `.rvt`, checar, gerar relatório, sem precisar do Revit do cliente.

## 4. Viabilidade urbanística

| Necessidade | API |
|---|---|
| Área do terreno e projeção | Toposolid, linhas de propriedade (`PropertyLine`, com adições no 2027), massas |
| Área construída por pavimento | Ambientes ou esquemas de área |
| Altura e gabarito | `Level.Elevation`, altura dos elementos |
| Recuos | Distância entre o contorno da edificação e a linha de propriedade (cálculo geométrico próprio) |
| Localização (para buscar a legislação municipal) | `SiteLocation` (latitude e longitude; no 2027, `SetLatitudeAndLongitude()`) |

A parte difícil não é a API, é a base de legislação municipal.

## 5. Assistente de documentação em português

Tudo coberto pela API: vistas (`ViewPlan.Create`, `ViewSection`), pranchas (`ViewSheet`, `Viewport`), tags (`IndependentTag`, `RoomTag`), cotas (`Document.Create.NewDimension`), numeração (Numbering Elements, ampliado no 2027) e textos (`TextNote`). Ver as seções Views, Annotation Elements e [Numbering Elements](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Editing_Elements_Numbering_Elements_html) do guia. É a oportunidade com mais concorrência.

## Ferramentas para a LLM (tool calling)

Esboço de ferramentas que o backend expõe para a LLM e que o add-in implementa com as APIs acima. Ferramentas de leitura são seguras; as de escrita passam por prévia e confirmação.

| Ferramenta | Leitura ou escrita | Implementação |
|---|---|---|
| `resumo_modelo` | Leitura | Níveis, contagem por categoria, fases, vínculos, unidades |
| `listar_ambientes` | Leitura | `RoomFilter` → número, nome, nível, área (m²), perímetro (m), pé-direito (m) |
| `listar_elementos(categoria, filtros)` | Leitura | `FilteredElementCollector` + parâmetros em SI |
| `camadas_do_tipo(tipo_id)` | Leitura | `CompoundStructure.GetLayers()` |
| `quantitativos_material(ids)` | Leitura | `GetMaterialVolume` / `GetMaterialArea` |
| `rota_mais_longa(nivel, saidas)` | Leitura (cria elementos temporários) | `PathOfTravel` dentro de transação com `RollBack()` |
| `destacar(ids, mensagem)` | Só visual | `TemporaryGraphicsManager` + seleção |
| `definir_parametro(ids, parametro, valor)` | Escrita | `Transaction` + `Parameter.Set()` |
| `criar_tabela(categoria, campos)` | Escrita | `ViewSchedule.CreateSchedule` |
