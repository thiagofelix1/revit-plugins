# Modelo de dados: elementos, filtros, parâmetros, unidades e transações

O que é preciso dominar antes de qualquer verificação ou orçamento. Todos os links são do Developer's Guide 2027.

## Documento e elementos

- `UIApplication` / `Application`: a sessão do Revit (UI e banco de dados).
- `UIDocument` / `Document`: o projeto aberto. `Document` é o que interessa para ler e alterar o modelo; `UIDocument` cuida de seleção e vistas.
- `Element`: tudo no modelo é um elemento (parede, porta, ambiente, nível, vista, tipo). Cada um tem `ElementId` (`.Value` é `long`), `Category`, `Name` e parâmetros.
- **Tipo e instância:** uma porta colocada (`FamilyInstance`) aponta para um tipo (`FamilySymbol`) via `GetTypeId()`. A largura de uma porta costuma ser parâmetro do tipo, não da instância. Isso decide onde procurar cada dado.
- Classificação oficial em [Element Classification](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Elements_Essentials_Element_Classification_html): elementos de modelo (instâncias de família, hospedeiros como parede e laje, estrutura), de vista, de grupo, de anotação e referência (cotas, níveis, eixos), de esboço e de informação.
- Atalhos no `Document` ([Element Retrieval](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Elements_Essentials_Element_Retrieval_html)): `ProjectInformation` (dados do projeto, onde caberiam número da licitação e órgão), `SiteLocation`, `ActiveProjectLocation`, `Phases`.

## Filtrar elementos

Fonte: [Applying Filters](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Filtering_Applying_Filters_html) e páginas vizinhas.

`FilteredElementCollector` é a única forma eficiente de consultar o modelo. Duas famílias de filtro:

- **Rápidos (`ElementQuickFilter`)**: avaliam um registro leve, sem carregar o elemento na memória. Ex.: `OfClass()`, `OfCategory()`, `WhereElementIsNotElementType()`, `BoundingBoxIntersectsFilter`.
- **Lentos (`ElementSlowFilter`)**: carregam o elemento. Ex.: `ElementParameterFilter` (por valor de parâmetro), `RoomFilter`, `FamilyInstanceFilter`. Sempre combine com pelo menos um filtro rápido antes.

```csharp
var portas = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_Doors)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .ToList();

var ambientes = new FilteredElementCollector(doc)
    .WherePasses(new RoomFilter())
    .Cast<Room>()
    .Where(r => r.Area > 0)   // descarta ambientes não colocados ou não fechados
    .ToList();
```

Detalhes que causam bug:

- `Room` não funciona com `OfClass()`; o guia manda usar `RoomFilter` ou `OfCategory(BuiltInCategory.OST_Rooms)`.
- Só use um método de extração por coletor (`ToElements()`, `ToElementIds()`, `FirstElement()` ou iterador): chamar outro reinicia o coletor ([Getting filtered elements or element ids](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Filtering_Getting_filtered_elements_or_element_ids_html)).
- Se for apagar ou mudar muito os elementos, trabalhe com `ToElementIds()` e `doc.GetElement(id)`: referências a `Element` podem ficar inválidas.
- LINQ funciona sobre o coletor ([LINQ Queries](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Filtering_LINQ_Queries_html)), mas cada `Where` em LINQ é filtro "lento" do lado do .NET. Aplique os filtros nativos primeiro.

## Parâmetros

Fontes: [Parameter](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Parameters_Parameter_html), [Built-In Parameters](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Parameters_Built_In_Parameters_html), [Shared Parameters](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Parameters_Shared_Parameters_html), [Global Parameters](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Parameters_Global_Parameters_html).

- Leitura: `element.get_Parameter(BuiltInParameter.X)`, `element.get_Parameter(ForgeTypeId)` ou `element.LookupParameter("nome")`. O nome muda com o idioma do Revit (um Revit em português mostra outros nomes); prefira `BuiltInParameter`/`ForgeTypeId` ou o GUID de parâmetro compartilhado.
- `StorageType` diz como ler: `Double` (`AsDouble()`, sempre em unidade interna), `Integer`, `String`, `ElementId`.
- `AsValueString()` devolve o valor formatado com as unidades do projeto; `SetValueString()` aceita texto com unidade. Úteis para mostrar ao usuário ou à LLM, nunca para cálculo.
- `Set()` retorna `true` só se o valor mudou de fato.
- **Parâmetros compartilhados** ficam definidos num arquivo de texto externo e são identificados por GUID, o que os torna estáveis entre projetos e idiomas. É assim que os cadernos BIM costumam exigir dados ("Código SINAPI", "Ambiente – Uso"). Classes: `DefinitionFile`, `DefinitionGroup`, `ExternalDefinition`, `Binding`/`BindingMap`, `SharedParameterElement`. Desde 2027, `ParameterUtils.WriteSharedParametersToFile()` exporta os usados no projeto.
- Filtrar por presença de valor: `FilterRule.CreateHasValueParameterRule()` e `CreateHasNoValueParameterRule()`. É exatamente a checagem "parâmetro obrigatório do caderno BIM preenchido?".
- Ferramenta para descobrir qual parâmetro guarda um dado: RevitLookup.

## Unidades: o Revit trabalha em pés

Fonte: [Units](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Application_and_Document_Units_html) e [ForgeTypeId](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Application_and_Document_ForgeTypeId_html).

Internamente, **comprimento é sempre em pés**, ângulo em radianos, massa em kg, tempo em segundos, temperatura em kelvin. Unidades derivadas misturam os dois sistemas (área em pés², volume em pés³, força em kg·ft/s²). As unidades de exibição do projeto não mudam isso.

```csharp
double larguraPes = porta.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();
double larguraM = UnitUtils.ConvertFromInternalUnits(larguraPes, UnitTypeId.Meters);
double areaM2 = UnitUtils.ConvertFromInternalUnits(ambiente.Area, UnitTypeId.SquareMeters);
double larguraMinimaPes = UnitUtils.ConvertToInternalUnits(0.80, UnitTypeId.Meters);
```

O parâmetro exato da largura varia por família (`DOOR_WIDTH`, `FAMILY_WIDTH_PARAM` ou um parâmetro próprio do fabricante). Confira com o RevitLookup em modelos reais antes de fixar no código.

Regra para o produto: converter para SI na fronteira (ao extrair do modelo) e nunca mandar pés para o backend ou para a LLM.

`ForgeTypeId` identifica unidades, specs, grupos, parâmetros e categorias com strings estáveis, por exemplo `autodesk.spec.aec:length-1.0.0`. Classes de acesso: `UnitTypeId`, `SpecTypeId`, `ParameterTypeId`, `GroupTypeId`, `DisciplineTypeId`, `SymbolTypeId`. `Category.GetBuiltInCategoryTypeId()` converte categorias. Essas strings são boas para serializar dados do modelo em JSON.

## Transações

Fontes: [Transaction Classes](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Transactions_Transaction_Classes_html), [Failure Handling Options](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Transactions_Failure_Handling_Options_html), [Temporary transactions](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Transactions_Temporary_transactions_html).

- Toda alteração no modelo exige uma `Transaction` aberta; fora dela, a API lança `ModificationOutsideTransactionException`.
- `Transaction`: unidade que aparece no Desfazer, com o nome que você der.
- `SubTransaction`: parte de uma transação que pode ser desfeita sozinha.
- `TransactionGroup`: agrupa várias transações. `Assimilate()` as funde numa entrada só no Desfazer; `RollBack()` desfaz todas, mesmo as já confirmadas.
- `Commit()` pode falhar (modelo inválido ou usuário cancelou o aviso). O retorno é `TransactionStatus`; confira se veio `Committed`.
- Sempre em `using`, para nunca deixar transação aberta.

```csharp
using var grupo = new TransactionGroup(doc, "IA: aplicar sugestões");
grupo.Start();
foreach (var acao in acoes)
{
    using var t = new Transaction(doc, acao.Descricao);
    t.Start();
    acao.Aplicar(doc);
    if (t.Commit() != TransactionStatus.Committed)
    {
        grupo.RollBack();
        return;
    }
}
grupo.Assimilate();
```

Para o assistente de IA: agrupar todas as ações de um pedido num `TransactionGroup` assimilado faz o usuário desfazer o pedido inteiro com um Ctrl+Z.

**Transação temporária:** abrir, alterar, ler e dar `RollBack()`. O guia usa isso para medir uma parede sem os vãos (apagar portas e janelas, regenerar, medir, desfazer). Serve para quantitativos brutos e líquidos no orçamento.

**Falhas e avisos:** `FailureHandlingOptions` (obtido com `GetFailureHandlingOptions()`) controla o que acontece com avisos ao confirmar. Um `IFailuresPreprocessor` pode suprimir ou resolver avisos sem mostrar diálogo, o que é necessário em processamento em lote e no Design Automation. Ver também [Failure Posting and Handling](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Failure_Posting_and_Handling_html).

## Guardar dados do plugin no modelo

| Mecanismo | Visível ao usuário | Bom para |
|---|---|---|
| Parâmetro compartilhado | Sim, nas propriedades e em tabelas | Dados que o usuário vê e edita: código SINAPI, status de conformidade |
| [Extensible Storage](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Storing_Data_in_the_Revit_model_Extensible_Storage_html) | Não | Dados internos: id do projeto no backend, data da última verificação, mapeamentos, cache |
| [Extended Properties](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Parameters_xtended_Properties_html) (2027) | Sim | Propriedades mantidas fora do Revit e ligadas ao modelo |

Extensible Storage: define-se um `Schema` (com GUID fixo) via `SchemaBuilder` e grava-se uma `Entity` em qualquer elemento (ou num `DataStorage` próprio). Leitura e escrita podem ser `Public`, `Vendor` (exige `VendorId`) ou `Application` (só o seu add-in). Um schema com GUID publicado nunca deve mudar de estrutura: para evoluir, crie um schema novo.

## Reagir a mudanças

- **Eventos** ([Events](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Events_html)): de banco (`DocumentOpened`, `DocumentChanged`, `DocumentSaving`...) e de UI (`Idling`, `ViewActivated`, `DialogBoxShowing`). Registrados no `OnStartup`. `DocumentChanged` permite marcar a verificação como desatualizada quando o modelo muda.
- **Dynamic Model Update** ([Dynamic Model Update](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Dynamic_Model_Update_html)): `IUpdater` roda dentro da transação do usuário quando elementos de um filtro mudam e pode alterar o modelo na hora. Cuidado: se o updater modifica o documento, o arquivo guarda o id dele, e quem abrir sem o add-in recebe um aviso, a menos que o updater seja marcado como opcional. Para um produto comercial, use updaters opcionais ou evite-os.

## Outros tópicos do guia que vão aparecer

- [Revit Links](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Linked_Files_Revit_Links_html): projetos grandes separam arquitetura, estrutura e instalações em arquivos vinculados. Verificar ou orçar "o projeto" exige ler os vínculos (`RevitLinkInstance.GetLinkDocument()`) e aplicar a transformação de coordenadas.
- [Worksharing Overview](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Worksharing_Worksharing_Overview_html): em modelos compartilhados, alterar um elemento exige ter a posse dele.
- [Cloud Models](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Application_and_Document_CloudFiles_html): modelos no ACC/BIM 360 são abertos por região + GUID do projeto + GUID do modelo (`ModelPathUtils.ConvertCloudGUIDsToCloudPath()`).
- [Phase](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Datum_and_Information_Elements_Phase_html) e [Design Options](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Datum_and_Information_Elements_Design_Options_html): o orçamento e a verificação precisam saber qual fase (existente, demolir, nova) e qual opção de projeto considerar.
- [Performance Adviser](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Performance_Adviser_html): regras de diagnóstico de qualidade do modelo; inspiração para o relatório de pré-requisitos de modelagem.
