# Mapa da Revit API para o projeto

Mapeamento das documentações oficiais da Autodesk (e das principais referências da comunidade) feito para orientar o desenvolvimento de plugins Revit com IA e normas brasileiras. Pesquisa feita em 16/10/2026, lendo diretamente as páginas e os índices oficiais.

A versão de referência é o **Revit 2027** (a mais recente). Diferenças para 2023–2026 estão em [02-versoes.md](02-versoes.md).

## Como usar

Leia na ordem se estiver começando; depois use como consulta.

| Arquivo | Conteúdo |
|---|---|
| [01-fontes.md](01-fontes.md) | Todas as fontes (oficiais, comunidade, ferramentas): para que servem, estado atual e armadilhas |
| [02-versoes.md](02-versoes.md) | Matriz Revit 2023–2027: .NET, SDK, pastas de add-in, pacotes NuGet, novidades relevantes |
| [03-anatomia-do-addin.md](03-anatomia-do-addin.md) | Comandos, aplicações, manifesto `.addin`, atributos, isolamento de dependências, assinatura, depuração |
| [04-modelo-e-dados.md](04-modelo-e-dados.md) | Elementos, filtros, parâmetros, unidades, transações, falhas, Extensible Storage |
| [05-threading-e-servicos-externos.md](05-threading-e-servicos-externos.md) | Thread única, `ExternalEvent`, padrão para chamar backend/LLM, servidor MCP local, TLS |
| [06-apis-por-oportunidade.md](06-apis-por-oportunidade.md) | Quais APIs atendem cada oportunidade de produto (NBR 9050, NBR 9077, NBR 12721, SINAPI, cadernos BIM) |
| [07-design-automation.md](07-design-automation.md) | Revit na nuvem (APS Automation API): conceitos, restrições, limites, ciclo de vida |
| [08-ambiente-e-ferramentas.md](08-ambiente-e-ferramentas.md) | Ambiente no Windows com Cursor, templates Nice3point, RevitLookup, agent skills |
| [indices/revit-2027-developers-guide.md](indices/revit-2027-developers-guide.md) | Índice completo do Developer's Guide 2027 (405 páginas, com links) |
| [indices/design-automation-v3.md](indices/design-automation-v3.md) | Índice completo da Automation API v3 (142 entradas, com links) |

## Atualizar os índices

Os índices são gerados a partir dos TOCs oficiais, então podem ser refeitos a cada nova versão do Revit:

```bash
python docs/api/_tools/gerar_indices.py          # Revit 2027
python docs/api/_tools/gerar_indices.py 2028     # quando sair a próxima versão
```

O script usa só a biblioteca padrão do Python.

## Cinco fatos que mudam decisões

1. **Revit 2027 roda em .NET 10.** Revit 2025 e 2026 usam .NET 8; 2024 e anteriores usam .NET Framework 4.8. Um produto que atenda 2024–2027 precisa compilar para três runtimes diferentes. Algumas páginas do guia 2027 ainda citam .NET 8: são texto desatualizado da Autodesk; vale o que diz [Migrating From .NET 8 to .NET 10](https://help.autodesk.com/view/RVT/2027/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Using_the_Autodesk_Revit_API_NET10_Update_html) e o What's New 2027.
2. **Isolamento de dependências existe desde o Revit 2026.** Com `<UseRevitContext>False</UseRevitContext>` no manifesto, o add-in roda num contexto de carregamento próprio, o que resolve o conflito clássico de DLL (Newtonsoft.Json etc.). Ver [03-anatomia-do-addin.md](03-anatomia-do-addin.md).
3. **O Revit já traz ASP.NET Core desde o 2025.1.** O add-in pode subir um servidor HTTP local (por exemplo, um servidor MCP) sem empacotar o framework. Ver [05-threading-e-servicos-externos.md](05-threading-e-servicos-externos.md).
4. **Existe API nativa de rota de fuga.** `PathOfTravel` calcula caminhos mínimos evitando obstáculos e tem `FindStartsOfLongestPathsFromRooms`, que acha o ponto mais distante de cada ambiente até as saídas. É a base natural para verificar distâncias máximas a percorrer (NBR 9077 e ITs dos bombeiros). Ver [06-apis-por-oportunidade.md](06-apis-por-oportunidade.md).
5. **Add-ins no Design Automation não acessam a rede.** O job na nuvem não pode chamar uma LLM nem o seu backend; a IA tem que rodar antes ou depois do job, no seu servidor. Ver [07-design-automation.md](07-design-automation.md).

## Convenções destes documentos

- Nomes de classes e métodos ficam em inglês, como na API.
- Links para o Developer's Guide apontam para a versão 2027 em inglês (`ENU`). O guia não tem tradução: as URLs `PTB` e `ESP` carregam o mesmo conteúdo em inglês.
- Quando algo foi inferido ou precisa ser conferido no Windows, o texto diz isso explicitamente.
