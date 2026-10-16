# Versões do Revit, .NET e compatibilidade

O Revit lança uma versão por ano (em abril, segundo as notas do Design Automation) e cada uma tem sua própria DLL da API. Um add-in é compilado para uma versão específica; para atender várias, compila-se uma DLL por versão.

## Matriz de runtime

| Revit | Runtime .NET | Target framework do projeto | Engine no Design Automation | Engine fica obsoleta | Engine é removida |
|---|---|---|---|---|---|
| 2023 | .NET Framework 4.8 | `net48` | `Autodesk.Revit.2023` | 29/03/2026 | 29/03/2028 |
| 2024 | .NET Framework 4.8 | `net48` | `Autodesk.Revit.2024` | 29/03/2027 | 29/03/2029 |
| 2025 | .NET 8 | `net8.0-windows` | `Autodesk.Revit.2025` | 05/04/2028 | 05/04/2030 |
| 2026 | .NET 8 | `net8.0-windows` | `Autodesk.Revit.2026` | 29/03/2029 | 29/03/2031 |
| 2027 | .NET 10 | `net10.0-windows` | `Autodesk.Revit.2027` | 29/03/2030 | 29/03/2032 |

Fontes: [Migrating From .NET 4.8 to .NET 8](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Using_the_Autodesk_Revit_API_NET8_Update_html), [Migrating From .NET 8 to .NET 10](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Using_the_Autodesk_Revit_API_NET10_Update_html) e [Engine Lifecycle Policy](https://aps.autodesk.com/en/docs/design-automation/v3/developers_guide/engine-lifecycle/).

Política do Design Automation: cada engine tem suporte por 4 anos após o lançamento do desktop, fica mais 2 anos como obsoleta (quem já usa continua funcionando, mas não dá para criar AppBundle ou Activity novos com ela) e depois é removida.

### Recomendação de alvo para o produto

- **MVP:** Revit 2025, 2026 e 2027. São só dois runtimes (.NET 8 e .NET 10) e o código fica moderno (C# atual, `async`, `HttpClient`, ASP.NET Core disponível).
- **Se a validação mostrar muitos escritórios em 2023/2024** (comum em escritórios que não renovam todo ano), adicionar `net48`. Isso exige evitar APIs do .NET moderno no código compartilhado ou isolá-las com `#if`.
- Pergunta a incluir nas entrevistas: "qual versão do Revit vocês usam hoje e quando atualizam?".

## Migração para .NET 10 (Revit 2027)

O que a página oficial pede:

- Instalar o SDK do .NET 10 e compilar com ele.
- Conferir as quebras de compatibilidade do [.NET 9](https://learn.microsoft.com/en-us/dotnet/core/compatibility/9.0) e do [.NET 10](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10.0).
- Se houver `global.json`, apontar para o SDK 10:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "minor"
  }
}
```

Para compilar várias versões no mesmo repositório, o `global.json` precisa apontar para o SDK mais novo (o SDK 10 compila `net8.0-windows` e `net48` também).

## Migração de .NET Framework 4.8 para .NET 8 (Revit 2025)

Pontos da página oficial que mais afetam um projeto novo ou portado:

- Projeto no formato SDK-style; `TargetFramework` `net8.0-windows`; `<UseWPF>true</UseWPF>` para WPF ou `<UseWindowsForms>true</UseWindowsForms>` para WinForms.
- Aviso MSB3277 ao referenciar a RevitAPI: resolve-se adicionando a referência ao framework Windows Desktop.
- Erro CA1416 (API só de Windows): resolve-se com `[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]`.
- `BinaryFormatter` não existe mais (afeta recursos `.resx` com imagens e `ImageList` do WinForms).
- `Process.Start(url)` deixou de abrir o navegador por padrão: usar `UseShellExecute = true`.
- `Encoding.Default` passou a ser sempre UTF-8.
- `double.ToString()` mudou a quantidade de casas; usar `ToString("G15")` se testes comparam strings.
- Nullable reference types vêm ligados por padrão em projetos novos.

## Componentes que o próprio Revit carrega

A página [Third-Party Component Versions](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Welcome_to_the_Revit_Platform_API_Component_Versions_html) lista as versões dos pacotes que o Revit usa. A lista publicada ainda é a do .NET 8 (Microsoft.Extensions.* 7.0.0, `Newtonsoft.Json` 13.0.1, `Microsoft.Web.WebView2` 1.0.2045.28, `System.Data.SQLite` 1.0.118.0 etc.).

Isso importa porque, sem isolamento, o add-in recebe a versão que o Revit (ou outro add-in) carregou primeiro. Três saídas, da melhor para a pior:

1. Ligar o isolamento de dependências no manifesto (Revit 2026 em diante; ver [03-anatomia-do-addin.md](03-anatomia-do-addin.md)).
2. Usar as mesmas versões que o Revit usa.
3. Evitar a dependência (por exemplo, `System.Text.Json` no lugar de `Newtonsoft.Json`).

Histórico do CefSharp (navegador embutido): no 2025 o Revit carregava o CefSharp 119.4.30 antes de qualquer add-in; no 2026 a Autodesk removeu o CefSharp da instalação. Para UI web dentro do add-in, prefira WebView2.

## Novidades por versão que interessam ao produto

### 2025

- Runtime .NET 8; todos os add-ins precisam ser recompilados.
- **2025.1:** referência de framework para `Microsoft.AspNetCore.App`. Add-ins podem usar ASP.NET Core (servidor HTTP local) sem empacotá-lo.
- **2025.2:** `Wall.GetAttachmentIds()`, `Wall.AddAttachment()` e afins (paredes presas a lajes ou forros); `Document.IsInEditMode()`.
- APIs de gerenciamento de add-ins (`AddInsManagerSettings`, `AddInItemSettings`) no `RevitAddInUtility`: o usuário passa a poder desligar add-ins pelo Add-ins Manager.

### 2026

- **Isolamento de dependências:** `UseRevitContext` e `ContextName` no manifesto.
- `UnifyInAddInManager` no manifesto: agrupa todos os itens do manifesto numa entrada só no Add-ins Manager (o usuário não desliga comandos individuais).
- `SpatialElement.GetDefaultLocation()`, `SpatialElement.Recenter()` e `SpatialElementTag.MoveToReferenceLocation()` (ambientes e tags).
- `ParameterUtils.GetBuiltInParameterGroupTypeId()`.
- `TemporaryGraphicsManager.SetTooltip()`: tooltip em gráficos temporários, útil para marcar não conformidades na vista.
- `ModelPathUtils.CloudRegionUS` e `CloudRegionEMEA` ficam obsoletos; usar `ModelPathUtils.GetAllCloudRegions()`.
- CefSharp removido.
- **Removidos** `ElementId(int)` e `ElementId.IntegerValue`. Use `ElementId.Value` (`long`) e `new ElementId(long)`. Ids de 32 bits salvos em banco ou JSON por versões antigas precisam ser lidos como `long`.

### 2027

- Runtime .NET 10.
- Isolamento ampliado: `PublicAssemblies`, `Dependencies` (`DependsOnClientId`, `DependsOnContext`) e `UseAllContextsForDependencyResolution` no manifesto.
- **Numbering Elements:** o sistema de numeração, antes só de armaduras, passa a valer para qualquer elemento, com esquemas, filtros de vista e correspondência por parâmetros de elementos associados (por exemplo, a porta lendo parâmetros do ambiente).
- **Extended Properties:** propriedades criadas fora do Revit e ligadas ao modelo (`ExtendedPropertiesLink`). Candidata a canal para levar dados do backend (código SINAPI, status de verificação) para dentro do modelo; precisa de prova de conceito.
- **Materials and Carbon Data:** `EmbodiedCarbonAsset` e `Material.EmbodiedCarbonAssetId`.
- `ParameterUtils.WriteSharedParametersToFile()`: gera o arquivo de parâmetros compartilhados a partir do projeto.
- `UnitUtils.GetForgeStorageUnit(ForgeTypeId)`.
- `Category.GetBuiltInCategoryDisciplines()`.
- `SiteLocation.SetLatitudeAndLongitude()` substitui os métodos antigos de latitude e longitude.
- A partir do 2027.1, `ActiveAddInId` retorna o GUID nativo do Revit quando não há add-in ativo; `AddInId.RevitApplicationId` expõe esse GUID.
- Importação AXM (FormIt) obsoleta.

## Compilar várias versões no mesmo projeto

O caminho mais curto é o template Nice3point (detalhes em [08-ambiente-e-ferramentas.md](08-ambiente-e-ferramentas.md)):

- Uma configuração de solução por versão: `Debug.R25`, `Debug.R26`, `Debug.R27`, `Release.R25` etc.
- Constantes de compilação geradas automaticamente: `REVIT2027`, `REVIT2027_OR_GREATER`, e assim por diante.
- Referências da API pelo NuGet, sem precisar do Revit instalado para compilar:

```xml
<PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="$(RevitVersion).*" />
<PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="$(RevitVersion).*" />
```

Exemplo de código condicional:

```csharp
#if REVIT2026_OR_GREATER
    var regioes = ModelPathUtils.GetAllCloudRegions();
#else
    var regioes = new[] { ModelPathUtils.CloudRegionUS, ModelPathUtils.CloudRegionEMEA };
#endif
```

Para descobrir o que mudou entre duas versões, use os relatórios de diferença do [RVTDocs](https://rvtdocs.com) ou o What's New de cada ano (links em [01-fontes.md](01-fontes.md)).

## Versões de pacotes conferidas no NuGet (16/10/2026)

| Pacote | Última versão |
|---|---|
| `Nice3point.Revit.Api.RevitAPI` | 2027.3.0 |
| `Nice3point.Revit.Templates` | 6.3.2 |
| `Nice3point.Revit.Sdk` | 6.3.2 |
| `Autodesk.Forge.DesignAutomation.Revit` | 2027.0.0 (há versões 2023 a 2027) |
