# Anatomia de um add-in

Resumo da seção **Add-In Integration** e de **Using the Autodesk Revit API** do Developer's Guide 2027.

## Modelo de execução

Da página [Deployment Options](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Using_the_Autodesk_Revit_API_Deployment_Options_html):

- A API só funciona **dentro do processo do Revit**: o add-in é uma DLL carregada pelo Revit, não um executável separado.
- O acesso é **de thread única**: toda chamada à API acontece na thread principal, dentro de um ponto de entrada que o Revit chama (comando, evento, `ExternalEvent`). Ver [05-threading-e-servicos-externos.md](05-threading-e-servicos-externos.md).
- Duas DLLs de referência: `RevitAPI.dll` (banco de dados: documentos, elementos, parâmetros) e `RevitAPIUI.dll` (interface: ribbon, diálogos, seleção). Existem também `RevitAPIIFC.dll`, `RevitAPIMacros.dll` e `RevitAPIUIMacros.dll`.

## Os três tipos de ponto de entrada

| Interface | Quando roda | Uso típico | Tipo no `.addin` |
|---|---|---|---|
| `IExternalCommand` | Quando o usuário clica no botão | Uma ação: verificar, orçar, exportar | `Command` |
| `IExternalApplication` | Ao abrir e ao fechar o Revit | Criar aba e botões na ribbon, registrar eventos, painéis encaixáveis, `ExternalEvent` | `Application` |
| `IExternalDBApplication` | Ao abrir e ao fechar o Revit, sem UI | Registrar eventos e updaters; é o único tipo aceito no Design Automation | `DBApplication` |

### IExternalCommand

```csharp
[Transaction(TransactionMode.Manual)]
public class VerificarAcessibilidade : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        // ...
        return Result.Succeeded;
    }
}
```

O que a [página oficial](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Using_the_Autodesk_Revit_API_External_Commands_html) define:

- `Result.Succeeded`: as alterações viram uma operação que o usuário pode desfazer.
- `Result.Cancelled`: desfaz tudo; mostra `message` se ela foi preenchida.
- `Result.Failed`: desfaz tudo, mostra `message` e **destaca no modelo os elementos colocados em `elements`**. Serve para apontar elementos com problema sem UI própria.
- O objeto do comando é criado a cada clique e destruído ao retornar. Estado entre execuções tem que ficar fora dele: na `IExternalApplication`, em arquivo, no backend ou no próprio modelo (Extensible Storage ou parâmetros).

### IExternalApplication

```csharp
public class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        application.CreateRibbonTab("NBR Tools");
        var painel = application.CreateRibbonPanel("NBR Tools", "Verificação");
        var dados = new PushButtonData("NBR9050", "NBR 9050",
            typeof(App).Assembly.Location, typeof(VerificarAcessibilidade).FullName);
        painel.AddItem(dados);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
```

A `UIControlledApplication` dá acesso a eventos de UI (por exemplo `DialogBoxShowing`), à criação de abas e painéis e ao registro de painéis encaixáveis.

### IExternalDBApplication

Igual à anterior, mas recebe `ControlledApplication` e retorna `ExternalDBApplicationResult`. Não pode tocar em UI. Ver [DB-level External Applications](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_DB_level_External_Applications_html).

Escrever a lógica de verificação e de extração de quantitativos numa biblioteca que dependa só de `RevitAPI.dll` (sem `RevitAPIUI`) permite reaproveitá-la no Design Automation depois.

## Manifesto `.addin`

Fonte: [Add-in Registration](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Add_in_Registration_html).

### Onde fica

O Revit lê todos os `.addin` de duas pastas, uma para todos os usuários e outra por usuário, por versão. Os arquivos das duas pastas são carregados juntos em ordem alfabética; se houver o mesmo nome nas duas, o da pasta do usuário vence.

As pastas usadas na prática são:

- Todos os usuários: `C:\ProgramData\Autodesk\Revit\Addins\2027\`
- Por usuário: `%AppData%\Autodesk\Revit\Addins\2027\` (ou seja, `C:\Users\<usuário>\AppData\Roaming\Autodesk\Revit\Addins\2027\`)

Atenção: o próprio guia é inconsistente aqui. A página de registro escreve `C:\Program Files\Autodesk\Revit\Addins\Revit 2027\` e `...\Addins\Revit 2027\`, e o Hello World fala em `{Release Year}` e "ProgramData". Confirme no Windows. O jeito seguro num instalador é perguntar à API: `RevitProductUtility.GetAllInstalledRevitProducts()` retorna `RevitProduct`, com `AllUsersAddInFolder` e `CurrentUserAddInFolder` (no `RevitAddInUtility.dll`).

Dados do add-in em tempo de execução: `Application.CurrentUsersAddinsDataFolderPath` (algo como `%AppData%\Autodesk\Revit\Autodesk Revit 2027\AddinsData`).

### Exemplo para uma aplicação

```xml
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>NBR Tools</Name>
    <Assembly>NbrTools\NbrTools.dll</Assembly>
    <AddInId>00000000-0000-0000-0000-000000000000</AddInId>
    <FullClassName>NbrTools.App</FullClassName>
    <VendorId>br.com.suaempresa</VendorId>
    <VendorDescription>Sua Empresa Ltda</VendorDescription>
  </AddIn>
  <ManifestSettings>
    <UseRevitContext>False</UseRevitContext>
    <ContextName>NbrTools</ContextName>
  </ManifestSettings>
</RevitAddIns>
```

### Tags

| Tag | Obrigatória | Observação |
|---|---|---|
| `Assembly` | Sim | Caminho da DLL (absoluto ou relativo ao `.addin`) |
| `FullClassName` | Sim | Classe que implementa a interface |
| `AddInId` | Sim | GUID único na sessão; o Revit recusa dois add-ins com o mesmo id. Os exemplos de isolamento usam `<ClientId>`, nome antigo da mesma tag |
| `Name` | Só em aplicações | Nome da aplicação |
| `VendorId` | Sim | Identificador único do fornecedor; a Autodesk recomenda domínio invertido (`br.com.suaempresa`). É usado pelo Extensible Storage |
| `VendorDescription` | Não | Razão social e contato |
| `Text`, `Description`, `LongDescription`, `LargeImage`, `SmallImage`, `TooltipImage` | Não | Só para comandos listados em External Tools |
| `VisibilityMode` | Não | `AlwaysVisible`, `NotVisibleInProject`, `NotVisibleInFamily`, `NotVisibleWhenNoActiveDocument` |
| `Discipline` | Não | `Architecture`, `Structure`, `Mechanical`, `Electrical`, `Piping` etc. |
| `AvailabilityClassName` | Não | Classe `IExternalCommandAvailability` que acinzenta o botão conforme o contexto |
| `LanguageType` | Não | Força um idioma de recursos |
| `AllowLoadIntoExistingSession` | Não | `false` impede carregar um manifesto novo sem reiniciar o Revit |

Comando sem `VisibilityMode` aparece até sem documento aberto; use `NotVisibleWhenNoActiveDocument` ou trate `ActiveUIDocument == null`.

### `RevitAddInUtility.dll`

API para instaladores lerem e escreverem manifestos (`RevitAddInManifest`, `RevitAddInCommand`, `RevitAddInApplication`, `AddInManifestUtility`) e, desde 2025/2026, para gerenciar quais add-ins estão habilitados (`AddInsManagerSettings`, `AddInItemSettings`). Documentada no `RevitAddInUtility.chm` do SDK.

## Isolamento de dependências (Revit 2026 em diante)

Fonte: [Add-in Dependency Isolation](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Add_in_Dependency_Isolation_html).

O problema que resolve: sem isolamento, todos os add-ins e o Revit dividem as mesmas DLLs. Se outro add-in carregou `Newtonsoft.Json` 12 antes, o seu recebe a 12 mesmo que tenha sido compilado com a 13.

Configuração em `<ManifestSettings>`, valendo para todos os add-ins do manifesto:

| Configuração | Versão | Efeito |
|---|---|---|
| `UseRevitContext` | 2026+ | `False` coloca o add-in num `AssemblyLoadContext` próprio. O padrão é `True` (sem isolamento) |
| `ContextName` | 2026+ | Nome do contexto. Add-ins de pastas diferentes com o mesmo nome dividem o contexto; sem nome, cada pasta vira um contexto |
| `UnifyInAddInManager` | 2026+ | Mostra todos os itens do manifesto como uma entrada só no Add-ins Manager |
| `PublicAssemblies` | 2027+ | DLLs que este contexto aceita compartilhar com outros |
| `Dependencies` | 2027+ | `DependsOnClientId` ou `DependsOnContext`: de onde resolver DLLs compartilhadas |
| `UseAllContextsForDependencyResolution` | 2027+ | Resolver dependências procurando nas DLLs públicas de todos os contextos |

Consequência prática: com isolamento, o add-in precisa levar **todas** as suas dependências na própria pasta, porque não pode contar com o Revit ou outro add-in tê-las carregado antes.

Para um produto novo: ligar `UseRevitContext=False` no Revit 2026 e 2027. Os templates Nice3point também oferecem isolamento próprio para builds .NET Core (o que cobre o Revit 2025).

## Atributos

Fonte: [Attributes](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Attributes_html).

- `[Transaction(TransactionMode.Manual)]` é obrigatório em `IExternalCommand` (não há padrão). O Revit cria um grupo externo que desfaz tudo se o comando falhar, e você abre e fecha as suas transações, que precisam ter nome (é o texto que aparece no Desfazer). Se alguma ficar aberta ao retornar, o Revit descarta todas as mudanças.
- `[Transaction(TransactionMode.ReadOnly)]`: proíbe transações no documento ativo. É o modo certo para comandos que só leem (verificar e extrair quantitativos), porque deixa claro que nada é alterado.
- `[Journaling(...)]`: controla o que vai para o journal do Revit (replay). Raramente necessário.

## UI

- **Ribbon:** `CreateRibbonTab`, `CreateRibbonPanel`, `PushButtonData`, `SplitButton`, `PulldownButton`, `ComboBox`, `TextBox`, `ContextualHelp` (F1 abre uma URL). Ícones de 32×32 para botões grandes e 16×16 para os pequenos e a barra de acesso rápido. Ver [Ribbon Panels and Controls](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Ribbon_Panels_and_Controls_html).
- **TaskDialog:** diálogo no estilo do Revit, para confirmações e mensagens. Ver [Revit-style Task Dialogs](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Revit_style_Task_Dialogs_html).
- **Painel encaixável (dockable pane):** janela WPF que se encaixa nas bordas do Revit ou vira aba ao lado do Navegador de Projeto. É o formato natural para um chat de IA ou uma lista de não conformidades. Implementa-se `IDockablePaneProvider` e registra-se com `RegisterDockablePane()` durante o `OnStartup`. Ver [Dockable Dialog Panes](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Dockable_Dialog_Panes_html).
- **Gráficos temporários:** `TemporaryGraphicsManager` desenha marcadores na vista sem criar elementos; desde 2026 aceita tooltip. Ver [Temporary Graphics](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Views_Temporary_Graphics_html).
- **Diretrizes de UI:** o apêndice [API User Interface Guidelines](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Appendices_API_User_Interface_Guidelines_html) descreve as convenções que a Autodesk espera (e que a App Store avalia).

## Exceções

Toda exceção da API deriva de `Autodesk.Revit.Exceptions.ApplicationException`. Existem equivalentes às do .NET (`ArgumentException`, `InvalidOperationException`) e algumas próprias: `ModificationOutsideTransactionException` (alterou o modelo fora de transação), `RegenerationFailedException`, `AutoJoinFailedException`. `InternalException` indica bug do Revit e traz diagnóstico para enviar à Autodesk. Ver [Revit Exceptions](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Revit_Exceptions_html).

Exceção não tratada dentro de um comando derruba o comando e mostra erro genérico. Capture e devolva `Result.Failed` com uma mensagem útil.

## Assinatura digital

Add-in não assinado por uma autoridade certificadora confiável faz o Revit mostrar, ao abrir, um aviso de segurança com três opções: sempre carregar, carregar só desta vez ou não carregar. Para um produto comercial, compre um certificado de assinatura de código. A página [Digitally Signing Your Revit Add-in](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Digitally_Signing_Your_Revit_Add_in_html) tem subpáginas sobre como assinar e sobre certificado próprio para testes.

## Depuração

Da página [Debugging Your Application in Microsoft Visual Studio](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Using_the_Autodesk_Revit_API_Debugging_Your_Application_in_Microsoft_Visual_Studio_html): cria-se um perfil de execução que abre o `Revit.exe`, põe-se breakpoints e aciona-se o comando. A página diz que o Visual Studio Community não oferece essa opção. Os templates Nice3point já trazem os perfis de execução por versão (também no Rider). No Cursor, a alternativa é anexar o depurador ao processo `Revit.exe` (ver [08-ambiente-e-ferramentas.md](08-ambiente-e-ferramentas.md)).

DLL em pasta de rede não carrega por padrão; o guia manda adicionar `<loadFromRemoteSources enabled="true"/>` no `Revit.exe.config`. Para desenvolvimento, mantenha tudo no disco local.
