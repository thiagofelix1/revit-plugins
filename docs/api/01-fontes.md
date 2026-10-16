# Fontes: o que é cada uma e quando usar

Estado verificado em 16/10/2026.

## Oficiais (Autodesk)

### Revit API Developer's Guide

- URL: https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_html
- O que é: o manual conceitual da API. Explica documentos, elementos, filtros, parâmetros, transações, eventos, geometria e as APIs de cada disciplina, com exemplos em C#.
- Estrutura (2027): Introduction, Basic Interaction with Revit Elements, Revit Geometric Elements, Discipline-Specific Functionality, Advanced Topics, Appendices e FAQ. São 405 páginas; o índice completo está em [indices/revit-2027-developers-guide.md](indices/revit-2027-developers-guide.md).
- Versões: troque o ano na URL (`/RVT/2025/`, `/RVT/2026/`, `/RVT/2027/`). O 2027 tem 37 páginas a mais que o 2026 (migração para .NET 10, Extended Properties, Numbering Elements, Material Schema, Materials and Carbon Data, External Data Manager Framework, Coordination Model).
- Idioma: só inglês. `PTB` e `ESP` na URL carregam o conteúdo `ENU`.
- Armadilha: o site é uma aplicação JavaScript, então ferramentas que só leem HTML recebem página vazia. O índice real está em `https://help.autodesk.com/view/RVT/<ano>/ENU/data/toctree.json` e cada página tem HTML estático em `https://help.autodesk.com/cloudhelp/<ano>/ENU/Revit-API/files/...` (é assim que o script em `_tools/` funciona).
- Armadilha: algumas páginas do guia 2027 (Development Requirements, Hello World, Third-Party Component Versions) ainda falam em .NET 8 e Visual Studio 2022. O runtime do 2027 é .NET 10.

### Referência de classes (API Reference / RevitAPI.chm)

- Online, dentro do mesmo help: seção "Revit API Reference Guides" do TOC, com `Namespaces` e `WhatsNew`. O What's New de cada versão:
  - [2027](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-API-MainReference/files/html/f7165618-24c9-4160-a7a4-09979fe4a981.htm)
  - [2026](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/8af227f4-b765-4430-97ce-16108dfe3788.htm)
  - [2025](https://help.autodesk.com/cloudhelp/2025/ENU/Revit-API-MainReference/files/html/1593f994-fb7b-4b7d-ae1d-1c0ba3337577.htm), mais o [Revit .NET 8 Upgrade Tips](https://help.autodesk.com/cloudhelp/2025/ENU/Revit-API-MainReference/files/html/2db849bc-e193-4919-a96c-cc324cf06f66.htm)
- Offline: `RevitAPI.chm` vem no SDK, na pasta `\Revit 2027 SDK\`. O SDK também traz `RevitAddInUtility.chm` (API para ler/escrever manifestos e gerenciar add-ins) e o REX SDK.

### Revit SDK

- Como obter: segundo o guia ([Installation](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Welcome_to_the_Revit_Platform_API_Installation_html)), o SDK é instalado pela seção **Tools and Utilities** do instalador do Revit. Ou seja, precisa do instalador (licença educacional ou trial servem).
- Conteúdo: `RevitAPI.chm`, `RevitAddInUtility.chm`, dezenas de exemplos (cada um com seu `.addin`), REX SDK.
- Exemplos citados no guia que interessam a este projeto: `MaterialQuantities` (quantitativos brutos e líquidos de material) e `ModelessDialog\ModelessForm_ExternalEvent` (janela não modal com `ExternalEvent`).

### Revit Developer Center (APS)

- URL informada: https://www.autodesk.com/developer-network/platform-technologies/revit. Hoje é a mesma página que https://aps.autodesk.com/developer/overview/revit-api.
- O que tem: links para o Developer's Guide 2027, o fórum, o blog ADN AEC, o The Building Coder e o ADN (suporte pago). Não tem link direto de download do SDK; o SDK vem com o instalador do Revit.

### Fórum Revit API

- URL: https://forums.autodesk.com/t5/revit-api-forum/bd-p/160
- Para quê: dúvidas específicas e bugs. Funcionários da Autodesk respondem. Também existe a tag `autodesk-platform-services` no Stack Overflow.

### APS Automation API (Design Automation)

- URL informada: https://aps.autodesk.com/en/docs/design-automation/v3/. A API foi renomeada para "Automation API"; a documentação continua nessa URL.
- Resumo e restrições em [07-design-automation.md](07-design-automation.md); índice completo em [indices/design-automation-v3.md](indices/design-automation-v3.md).
- Armadilha: o portal também é JavaScript. A Autodesk publica em https://aps.autodesk.com/llms.txt o caminho para os TOCs em JSON (`https://developer.doc.config.autodesk.com/bPlouYTd/design-automation_v3.json`) e o HTML estático de cada página. Útil para o Cursor ler a documentação.

### ADN Revit API Labs

- URL: https://github.com/ADN-DevTech/RevitTrainingMaterial
- O que tem: o material do treinamento de dois dias da Autodesk. Exercícios em C# e VB com documentos `.docx` e slides, em três blocos:
  - Intro: Hello World, DB Element, Element Filtering, Element Modification, Model Creation, Extensible Storage, Shared Parameter.
  - UI: Ribbon, Selection, TaskDialog, Event.
  - Family API: colunas retangulares e em L, fórmulas e material, controle de visibilidade.
- Armadilha: está **desatualizado**. Último push em maio de 2021; os projetos usam .NET Framework 4.8 e referenciam o Revit 2022. Serve para estudar conceitos, não como base de projeto. Para compilar no Revit 2025 em diante, é preciso migrar para .NET 8/10 (ou recriar a partir dos templates Nice3point).

### Outros repositórios oficiais úteis

- https://github.com/Autodesk/revit-ifc: código-fonte aberto do exportador/importador IFC do Revit (ativo; último push em 23/09/2026). Referência para validação de entregáveis IFC.
- Exemplos do Design Automation para Revit (listados na documentação da APS):
  - https://github.com/autodesk-platform-services/aps-count-delete-walls (o exemplo recomendado para começar)
  - https://github.com/autodesk-platform-services/aps-checkmodels-createissues-revit (verificar modelos e criar issues no ACC)
  - https://github.com/autodesk-platform-services/aps-revit-file-parameters-exchange
  - https://github.com/autodesk-platform-services/aps-create-revit-family
  - https://github.com/autodesk-platform-services/aps-design-automation-tools

## Referência da comunidade

### revitapidocs.com

- URL: https://www.revitapidocs.com
- Referência pesquisável de classes e métodos, cobrindo de 2014 a 2027. Mais rápida que o help oficial para achar uma assinatura.

### RVTDocs

- URL: https://rvtdocs.com
- Referência de 2020 a 2027 (inclusive 2025.3). Diferenciais: relatórios de diferença entre versões ("o que mudou de 2026 para 2027"), filtro de membros novos e obsoletos por versão e tradução automática dos exemplos C# para Python (útil no pyRevit). É a melhor ferramenta para planejar a compatibilidade multiversão.

### The Building Coder

- O endereço antigo (https://thebuildingcoder.typepad.com) **não funciona mais**: redireciona para uma página da Network Solutions. O blog continua em https://blog.autodesk.io/category/thebuildingcoder/ (e o blog ADN AEC em https://blog.autodesk.io/category/revit/).
- Para quê: anos de artigos sobre problemas reais da API. Bom para pesquisar ("site:blog.autodesk.io thebuildingcoder room boundary").

## Ferramentas

### RevitLookup

- URL: https://github.com/lookup-foundation/RevitLookup (o link `jeremytammik/RevitLookup` redireciona para cá)
- O que faz: inspeciona qualquer elemento do modelo e mostra parâmetros, propriedades e relações como a API enxerga. Indispensável para descobrir qual `BuiltInParameter` ou propriedade guarda um dado.
- Instalação: WinGet (`LookupFoundation.RevitLookup.2027`, com atualização automática), AppBundle ou MSI pelas releases do GitHub.

### Templates Nice3point

- URL: https://github.com/Nice3point/RevitTemplates
- Detalhes em [08-ambiente-e-ferramentas.md](08-ambiente-e-ferramentas.md). Resumo: `dotnet new install Nice3point.Revit.Templates` instala templates que já resolvem multiversão (Revit 2023–2027), WPF/MVVM, injeção de dependência, logging, isolamento de assemblies, instalador MSI, bundle para a Autodesk App Store e CI no GitHub.
- Pacotes relacionados do mesmo autor: `Nice3point.Revit.Api.*` (referências da API no NuGet, o que permite compilar sem o Revit instalado), `Nice3point.Revit.Toolkit` (bases para comandos e `ExternalEvent` assíncrono) e `Nice3point.Revit.Extensions` (métodos de extensão, como conversão de unidades).
- https://github.com/Nice3point/revit-skills: "agent skills" para IA escrever add-ins Revit (API, UI, solução, testes, benchmark). Ver [08-ambiente-e-ferramentas.md](08-ambiente-e-ferramentas.md).

## Ordem de estudo sugerida

1. [Walkthrough: Hello World](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Walkthrough_Hello_World_html) e [Walkthrough: Add Hello World Ribbon Panel](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Walkthrough_Add_Hello_World_Ribbon_Panel_html), mas criando o projeto pelo template Nice3point em vez do Visual Studio "na mão".
2. Seção **Add-In Integration** inteira (registro, atributos, isolamento, exceções).
3. **Elements Essentials**, **Filtering**, **Parameters**, **Transactions** e **Units** (resumidos em [04-modelo-e-dados.md](04-modelo-e-dados.md)).
4. [External Events](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_External_Events_html), antes de qualquer integração com backend ou LLM.
5. As páginas do domínio escolhido em [06-apis-por-oportunidade.md](06-apis-por-oportunidade.md).
6. No dia a dia: RevitLookup + RVTDocs/revitapidocs.
