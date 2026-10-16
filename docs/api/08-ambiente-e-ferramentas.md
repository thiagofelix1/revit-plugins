# Ambiente de desenvolvimento e ferramentas

## Linux e Windows: o que roda onde

| Tarefa | Linux | Windows |
|---|---|---|
| Escrever código e documentação | Sim | Sim |
| Compilar (`dotnet build`) com referências da API pelo NuGet | Sim | Sim |
| Testes de lógica pura (sem Revit) | Sim | Sim |
| Carregar o add-in no Revit, depurar, testar com modelo real | Não (o Revit só roda no Windows; Wine não é confiável) | Sim |
| Instalar o SDK e ler o `RevitAPI.chm` | Não (o SDK vem no instalador do Revit) | Sim |

Separar o código em uma biblioteca de lógica pura (regras, mapeamento SINAPI, cálculos) e uma camada fina que conversa com a API deixa a maior parte testável em qualquer sistema e no CI.

## Montar a máquina Windows

1. **Revit** 2025, 2026 e/ou 2027. Licença educacional ou trial servem para desenvolvimento.
2. **SDK do Revit:** no instalador do Revit, seção **Tools and Utilities**. Traz `RevitAPI.chm`, `RevitAddInUtility.chm` e os exemplos.
3. **.NET SDK 10** (compila também para .NET 8 e .NET Framework 4.8). Se o projeto tiver `global.json`, ele deve apontar para o SDK 10.
4. **RevitLookup:** `winget install LookupFoundation.RevitLookup.2027` (troque o ano conforme a versão; há também MSI nas [releases](https://github.com/lookup-foundation/RevitLookup/releases)).
5. **Templates:** `dotnet new install Nice3point.Revit.Templates`.
6. **Git** e o clone deste repositório.

## Cursor no Windows

- Abra a pasta do repositório; os documentos de `docs/api/` ficam disponíveis para o agente citar com `@docs/api/...`.
- **Agent skills do Nice3point:** o repositório https://github.com/Nice3point/revit-skills publica um marketplace de plugins para Cursor. No Cursor, abra o painel de marketplace de plugins, procure `revit` e instale. Para uma cópia local, coloque o repositório em `~/.cursor/plugins/local/revit-skills` e rode **Developer: Reload Window**. Os plugins mais úteis aqui:
  - `revit-api`: coletores, acesso a elementos e parâmetros, Extensible Storage, tratamento de falhas.
  - `revit-ui`: comandos e aplicações, ribbon, `ExternalEvent`, painel encaixável, filtros de seleção.
  - `revit-solution`: estrutura com os templates, multiversão, isolamento de dependências, depuração, publicação e bundle para a App Store.
  - `revit-testing`: testes automatizados dentro do Revit com `Nice3point.TUnit.Revit`.
- **Depuração:** o ciclo mais simples no Cursor é compilar (o SDK do Nice3point copia o add-in para a pasta de add-ins com `DeployAddin`), abrir o Revit e anexar o depurador ao processo `Revit.exe`. Para Revit 2025+ (.NET 8/10) isso depende de a extensão de C# instalada no Cursor oferecer um depurador .NET. Se não oferecer, use o Visual Studio ou o Rider só para sessões de depuração; os templates já trazem os perfis que abrem o Revit com o depurador anexado.
- Inclua o RevitLookup no fluxo: quando o agente propuser ler um parâmetro, confirme no RevitLookup se ele existe e onde (instância ou tipo).

## Templates Nice3point

Fonte: https://github.com/Nice3point/RevitTemplates e o [wiki](https://github.com/Nice3point/RevitTemplates/wiki). Versão 6.3.2 no NuGet em 16/10/2026; suportam Revit 2023 a 2027.

| Template | Para quê |
|---|---|
| Revit AddIn (`dotnet new revit-addin`) | Add-in de projeto único; o ponto de partida mais rápido |
| Revit AddIn Application | Aplicação principal de um add-in modular (entrada + `.addin`, sem lógica) |
| Revit AddIn Module | Módulo com ou sem UI para o add-in modular; sem UI vira biblioteca |
| Revit Service Defaults | Configuração compartilhada (logging, HTTP, serialização) para os módulos |
| Revit AddIn Solution | Estrutura completa: build com ModularPipelines, instalador MSI (WixSharp), CI, `.editorconfig` |
| Revit Benchmark | Benchmarks da API dentro do Revit |
| Revit Test | Testes da API dentro do Revit |

O que vem pronto: multiversão, WPF com MVVM, injeção de dependência, logging com `Microsoft.Extensions.Logging`, isolamento de assemblies para .NET Core, versionamento automático, instalador, bundle para a Autodesk App Store e CI para GitHub e Azure DevOps.

Como a multiversão funciona:

- Configurações de solução por versão: `Debug.R25`, `Debug.R26`, `Debug.R27`, `Release.R25`... Pela linha de comando: `dotnet build -c Debug.R27`.
- Constantes geradas: `REVIT2027`, `REVIT2027_OR_GREATER` etc., para `#if`.
- Referências da API pelo NuGet: `Nice3point.Revit.Api.RevitAPI` e `Nice3point.Revit.Api.RevitAPIUI` com `Version="$(RevitVersion).*"`.

Propriedades do `Nice3point.Revit.Sdk` usadas no dia a dia (segundo a skill `revit-addin-debugging`):

- `<DeployAddin>true</DeployAddin>`: cada build copia o add-in para a pasta de add-ins do Revit.
- `<LaunchRevit>true</LaunchRevit>`: iniciar a depuração abre `C:\Program Files\Autodesk\Revit $(RevitVersion)\Revit.exe` com o depurador; `StartProgram` e `StartArguments` sobrescrevem caminho e argumentos (o padrão é `/language ENG`; para testar a interface em português, troque o idioma).
- `IsRepackable`: junta as dependências num assembly só. Atrapalha o Hot Reload; a skill recomenda desligar em debug e, no Revit 2027+, usar o isolamento pelo manifesto.

Ordem sugerida para começar: criar uma solução com **Revit AddIn Solution**, depois um projeto **Revit AddIn** dentro de `source/`, compilar `Debug.R27` e abrir o Revit.

## Bibliotecas do mesmo autor

- [Nice3point.Revit.Toolkit](https://github.com/Nice3point/RevitToolkit): bases `ExternalCommand`, `AsyncExternalCommand`, `ExternalApplication`, `AsyncExternalApplication`, `ExternalDBApplication`; `ExternalEvent`, `AsyncExternalEvent`, `AsyncRequestExternalEvent<T, TResult>`; contexto da API; provedor de painel encaixável; resolução automática de dependências na pasta do plugin. Ver [05-threading-e-servicos-externos.md](05-threading-e-servicos-externos.md).
- [Nice3point.Revit.Extensions](https://github.com/Nice3point/RevitExtensions): métodos de extensão fluentes (`.FindParameter(...)`, `.ToMillimeters()`, ribbon, Extensible Storage, parâmetros globais).
- [Nice3point/RevitApi](https://github.com/Nice3point/RevitApi): os pacotes NuGet com as DLLs da API (`RevitAPI`, `RevitAPIUI`, `RevitAPIIFC`, `RevitNET`, `AdWindows`, `UIFramework`, `RevitAddInUtility`, `RevitAPIMacros`), extraídos da instalação oficial do Revit.

## Protótipo rápido sem C#: pyRevit

Para testar uma regra num modelo real antes de escrever o add-in, pyRevit (Python dentro do Revit) é mais rápido. O RVTDocs traduz os exemplos C# da referência para Python. Bom para a fase de validação com entrevistados; não para distribuir.

## Publicação

- **Assinatura de código:** necessária para não aparecer o aviso de segurança ao carregar ([03-anatomia-do-addin.md](03-anatomia-do-addin.md)).
- **Autodesk App Store:** o template de solução gera o bundle (`.bundle` com `PackageContents.xml`). O wiki do template tem as páginas "Autodesk Store bundle" e "Publishing the Release".
- **Instalação direta:** MSI gerado pelo template (WixSharp).
