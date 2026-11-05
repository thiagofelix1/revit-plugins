# Revit API para elétrica: o que existe e as armadilhas

Complementa [docs/api/06-apis-por-oportunidade.md](../../docs/api/06-apis-por-oportunidade.md). Fontes: índice do Developer's Guide 2027 (seção MEP Engineering), referência da API 2026/2027 em rvtdocs.com e revitapidocs.com, notas de API 2026, blog da Autodesk sobre condutores no 2026, código do SPK que já lida com MEP. Onde está escrito "conferir", é inferência a validar no RevitLookup e num modelo real.

## 1. O mapa do namespace `Autodesk.Revit.DB.Electrical` (2027)

| Grupo | Classes | Para quê |
|---|---|---|
| Circuitos | `ElectricalSystem`, `ElectricalSystemType` (enum: PowerCircuit, Data, Telephone, Security, FireAlarm, …), `CircuitType`, `CircuitNamingScheme`, `CircuitNamingSchemeSettings` | O circuito é um `MEPSystem` que liga o conector de alimentação dos dispositivos a um quadro. Nome do circuito por esquema de nomeação |
| Fiação | `Wire`, `WireType`, `WireSet`, `WireConduitType`, `WiringType` (Arc, Chamfer) | Fio 2D desenhado numa vista de planta ou forro |
| Condutores (2026+) | `ConductorMaterial`, `TemperatureRating`, `InsulationMaterial`, `ConductorSize`, `CableSize`, `CableType` | O novo modelo de dados de cabos e condutores; substitui `WireSize`, `WireMaterialType`, `InsulationType`, `TemperatureRatingType`, `CorrectionFactor` (todos obsoletos) |
| Eletrodutos | `Conduit`, `ConduitType`, `ConduitRun`, `ConduitSettings`, `ConduitSize`, `ConduitSizeSettings`, `CableTrayConduitBase`, `CableTrayConduitRunBase` | Eletroduto como `MEPCurve`, com corrida (`ConduitRun`) agrupando trechos e conexões |
| Eletrocalhas | `CableTray`, `CableTrayType`, `CableTrayRun`, `CableTraySettings`, `CableTraySizes` | Idem para bandeja |
| Equipamentos | `ElectricalEquipment`, `LightingFixture`, `LightingDevice` | Quadros, luminárias, interruptores (via `FamilyInstance.MEPModel`) |
| Configurações | `ElectricalSetting`, `DistributionSysType`, `VoltageType` | Sistemas de distribuição (127/220 V 1F+N, 220/380 V 3F+N), faixas de tensão |
| Quadros de cargas | `PanelScheduleView`, `PanelScheduleTemplate`, `PanelScheduleData`, `PanelScheduleSheetInstance` | Tabela do quadro, com modelo configurável e colocação em prancha |
| Cargas e demanda | `ElectricalLoadClassification`, `ElectricalDemandFactorDefinition`, `ElectricalDemandFactorValue`, `ElectricalPerPhaseData`, `ElectricalLoadAreaData`, `AreaBasedLoadData`, `AreaBasedLoadType`, `AreaBasedLoadBoundaryLineData` | Classificação de carga e fatores de demanda nativos; cargas por área (2023+) |
| Analítico (2023+) | `ElectricalAnalyticalNode`, `AnalyticalPowerSourceData`, `AnalyticalBusData`, `AnalyticalTransformerData`, `AnalyticalTransferSwitchData`, `AnalyticalEquipmentLoadData`, `AnalyticalDistributionNodePropertyData`, `AnalyticalPowerDistributableNodeData`, `ElectricalAnalyticalLoadSet` | Projeto preliminar: fonte → transformador → barramentos → cargas por área ou pontuais, sem geometria |

## 2. Circuitos: `ElectricalSystem`

- Criar: `ElectricalSystem.Create(Document, IList<ElementId> elementIds, ElectricalSystemType)` (2024+; antes `Document.Create.NewElectricalSystem(connector, type)`), depois `SelectPanel(FamilyInstance quadro)`. `SelectPanel` lança `InvalidOperationException` se o quadro não tem polos livres (e não tem "feed through lugs").
- Editar: `AddToCircuit(ElementSet)`, `RemoveFromCircuit(ElementSet)`, `DisconnectPanel()`, `Elements`, `BaseEquipment`, `PanelName`, `CircuitNumber`, `LoadName`, `StartSlot`, `PolesNumber`, `Voltage`, `ApparentLoad`, `ApparentCurrent`, `TrueLoad`, `PowerFactor`, `LoadClassifications`, `Rating` (disjuntor), `Frame`, `Length`, `HotConductorsNumber`, `NeutralConductorsNumber`, `GroundConductorsNumber`, `RunsNumber`, `WireSizeString` (obsoleto), `CableType` e `CableSize` (2026+), `HotConductorSize`, `NeutralConductorSize`, `GroundConductorSize`, `OtherConductorSize`, `OtherConductorsNumber` (2026+).
- **Caminho do circuito** (2019+): `CircuitPathMode` (FarthestDevice, AllDevices, Custom), `GetCircuitPath()`, `SetCircuitPath(IList<XYZ>)` (o primeiro nó é o conector do quadro, não a origem da família; ao definir, o modo vira Custom), `MaxCircuitPathLength`? (conferir). `Length` é o comprimento do caminho: por padrão, do quadro ao dispositivo mais distante em linha reta por etapas, que é a origem da diferença de ~17 % contra o levantamento. **AE:** `SetCircuitPath` com os vértices do traçado dos eletrodutos alinha o comprimento nativo ao real.
- Conectores: `FamilyInstance.MEPModel.ConnectorManager`; conector elétrico tem `Domain == DomainElectrical`, `ElectricalSystemType`, `VoltageType`? (parâmetros de voltagem e número de polos no conector), `IsConnected`, `AllRefs`. Uma família de tomada tem um conector de alimentação; a luminária também; o interruptor "comanda" por `LightingDevice` e sistema de comutação (switch system, `RBS_ELEC_SWITCH_ID`?) (conferir).
- Verificação nativa: o Revit só liga dispositivo a quadro com `DistributionSysType` compatível (tensão e polos). Mudar 220/380 para 127/220 quebra circuitos (reclamação comum).

## 3. Fiação: `Wire`

- `Wire.Create(Document, ElementId wireTypeId, ElementId viewId, WiringType, IList<XYZ> vertices, Connector start, Connector end)`. **Só vista de planta ou forro** (outras lançam `ArgumentException`). `Arc`: 2 pontos reta, 3 arco, 4+ spline (fiação embutida); `Chamfer`: polilinha (aparente). Conector nulo deixa a ponta solta.
- O fio é **anotação 2D específica da vista**: não é geometria 3D, não entra em colisão, não sabe por qual eletroduto passa. Parâmetros: `RBS_ELEC_WIRE_TYPE`, `RBS_ELEC_WIRE_HOT_ADJUSTMENT`, `RBS_ELEC_WIRE_NEUTRAL_ADJUSTMENT`, `RBS_ELEC_WIRE_GROUND_ADJUSTMENT` (contagem de condutores), `RBS_ELEC_WIRE_ELBOW`, tick marks (as "setinhas" por condutor, `RBS_ELEC_WIRE_TICKMARK_STATE`?) (conferir nomes).
- A tag nativa de fio lista cada condutor do circuito, o que gera o formato "ruim" das reclamações. **AE:** a tag lê parâmetros compartilhados gravados por trecho (F, N, R, PE, circuito, seção), no formato brasileiro (ex.: "3#2,5(2,5) – C1"), e o fio pode ser só o símbolo.

## 4. Eletrodutos e eletrocalhas

- `Conduit.Create(Document, ElementId conduitTypeId, XYZ start, XYZ end, ElementId levelId)`; `Conduit.CreatePlaceholder`? (não existe para conduíte; placeholders são para duto e tubo). `CableTray.Create(Document, ElementId typeId, XYZ start, XYZ end, ElementId levelId)`.
- Conexões: `Document.Create.NewElbowFitting(Connector, Connector)`, `NewTeeFitting`, `NewUnionFitting`, `NewCrossFitting`, `NewTransitionFitting`; o tipo de conexão vem das preferências do `ConduitType` (parâmetros Elbow, Tee, Cross, Transition, Union; conferir se expõe `RoutingPreferenceManager` como `PipeType`).
- `ConduitRun`: agrupa os trechos conectados entre duas extremidades; tem `Length`, `OutsideDiameter`, `InsideDiameter`? (conferir), e é o que o Revit usa na tabela "conduit runs". **AE:** o "trecho" do plugin (entre duas caixas) coincide com a corrida ou com um subconjunto dela; guardar a lista de circuitos e a contagem de condutores por trecho em parâmetros compartilhados no `Conduit`.
- Diâmetros disponíveis: `ConduitSizeSettings` por padrão de eletroduto (norma de tamanhos; o Revit traz EMT/RMC americanos; criar o padrão NBR 6150/15465 com DN 16, 20, 25, 32, 40, 50, 60, 75, 85 e diâmetros internos reais para a ocupação).
- Caixas: famílias de `OST_ElectricalFixtures` ou `OST_ConduitFitting` com conectores de conduíte; o SPK já tem a lógica de "ligar peças encostadas" e "derivação sem tê" (`Verificacao/Reconexao.cs`, `Derivacoes.cs`) que vale adaptar.
- Verificação de interferência com estrutura: `BoundingBoxIntersectsFilter` e `ElementIntersectsSolidFilter` contra vigas e pilares dos vínculos (o SPK já lê vigas em `LeituraDoEdificio`).

## 5. Quadros e quadro de cargas

- Quadro é `FamilyInstance` de `OST_ElectricalEquipment` com `MEPModel` do tipo `ElectricalEquipment`; parâmetros de sistema de distribuição (`RBS_FAMILY_CONTENT_DISTRIBUTION_SYSTEM`), número de polos/slots (`RBS_ELEC_MAX_POLE_BREAKERS`), disjuntor geral (`RBS_ELEC_MAINS`), alimentação (`RBS_ELEC_PANEL_FEED_THRU_LUGS_PARAM`), `ElectricalEquipment.DistributionSystem` (2024+ é propriedade? conferir).
- `PanelScheduleView.CreateInstanceView(Document, ElementId panelId)` cria o quadro de cargas com o modelo padrão; `PanelScheduleTemplate` define colunas; `PanelScheduleData` configura; `PanelScheduleView.GetTableData()` para ler/escrever células; `PanelScheduleSheetInstance.Create` coloca na prancha. Balanceamento nativo: `PanelScheduleView.Rebalance()`? (há `RebalanceLoads`; conferir). **AE:** gerar o quadro de cargas no padrão brasileiro (circuito, descrição, potência por fase, IB, seção, disjuntor, DR) pelo modelo de template, ou como `ViewDrafting` desenhada quando o template não alcança.
- Unifilar: não há gerador nativo; é `ViewDrafting` com `DetailCurve`, famílias de símbolo (`FamilyInstance` de detalhe) e `TextNote`, igual aos quadros e isométricos que o SPK já desenha em `Folhas/`.

## 6. Condutores e cabos no Revit 2026 e 2027

O que mudou (notas de API 2026):

- Removido o dimensionamento automático de fio e `ElectricalSystem.VoltageDrop` (sem substituto); `CorrectionFactor` sem substituto.
- Novos elementos: `ConductorMaterial.Create()`, `TemperatureRating`, `InsulationMaterial`, `ConductorSize`, `CableSize` (combina condutores com papéis fase/neutro/terra), `CableType` (empacota tamanhos); `ElectricalSystem.CableType`, `CableSize`, `HotConductorSize`, `NeutralConductorSize`, `GroundConductorSize`, `OtherConductorSize`, `OtherConductorsNumber` são de leitura e escrita.
- `WireType.WireMaterial`, `TemperatureRating`, `Insulation` viraram `ElementId`; `WireType.MaxSize` virou string; `WireSize` substituído por `ConductorSize`.
- Não há importação/exportação dessas configurações: o template do escritório precisa trazê-las, ou o plugin as cria por código (NBR NM 247-3 450/750 V PVC 70 °C: 1,5 a 300 mm²; 0,6/1 kV EPR/XLPE 90 °C; LSZH).
- O Revit 2027 não trouxe de volta o cálculo; mudanças de 2027 são .NET 10 e nuvem.

**AE:** camada `Condutores` com duas implementações: 2025 grava em parâmetros compartilhados; 2026+ grava também em `CableType`/`ConductorSize` do circuito, de modo que tabelas e tags nativas vejam a seção. O motor não sabe de nenhuma das duas.

## 7. Cargas, demanda e análise preliminar

- `ElectricalLoadClassification` (Iluminação, Tomadas, Motor, HVAC, …) com `ElectricalDemandFactorDefinition` (regras por faixa de carga ou quantidade) alimenta a demanda nativa do quadro. **AE:** os fatores de demanda brasileiros (da distribuidora, ou da prática: tomadas 100 % até 1 kVA…) cabem nessa estrutura; a demanda do padrão de entrada, porém, é da norma da distribuidora e fica no motor.
- Análise preliminar (2023+): `ElectricalAnalyticalNode` com fonte, transformador, barramentos, cargas por área (`AreaBasedLoadData` com densidade W/m² sobre um contorno) e cargas pontuais, para estimar a entrada antes de modelar. Útil na Fase 4 para "estudo de demanda" sem pontos.

## 8. Leitura do edifício (reaproveitar do SPK)

`Room`/`Space` (`Area`, `Perimeter`, `UnboundedHeight`, `GetBoundarySegments`), portas com `FromRoom`/`ToRoom` (interruptor junto da porta, lado de abertura), paredes (hospedar tomadas e eletrodutos), forros e lajes (`LeituraDoEdificio` do SPK já lê ambientes, tetos e vigas do arquivo e dos vínculos), louças (`OST_PlumbingFixtures`: banheira, boxe, pia) para volumes de banheiro e tomadas sobre a bancada, equipamentos (`OST_MechanicalEquipment`: ar-condicionado) para TUEs.

## 9. Armadilhas conhecidas

1. Fio só em planta ou forro, e por vista: duplicar em outra vista é redesenhar. Tratar como saída gráfica.
2. Comprimento do circuito é lógico; usar `SetCircuitPath` ou o traçado dos eletrodutos.
3. Dispositivo com dois circuitos (tomada dupla em circuitos diferentes) exige família com dois conectores; tag de circuito só mostra um. Resolver com parâmetro nosso na tag.
4. Tensão e polos do sistema de distribuição do quadro travam a ligação: o mapeamento de famílias deve conferir `DistributionSysType` e `VoltageType` antes de criar circuitos.
5. `ConduitSizeSettings` padrão é americano: criar o padrão brasileiro com diâmetros internos corretos antes de calcular ocupação.
6. Tudo que escreve no modelo passa por `Transaction` e `TransactionGroup` por ambiente, com `FailureHandlingOptions` para engolir avisos de conexão, como o SPK faz em `TransacaoSegura` e `EngolirAvisos`.
7. 2025 (.NET 8) e 2026/2027 têm APIs de condutor diferentes: `#if` ou interface por versão.
8. Nomes de parâmetros built-in citados com "conferir" precisam ser validados no RevitLookup; o SPK tem o comando Famílias que lista o que cada família traz; replicar para tomadas, luminárias, interruptores, quadros e caixas.
