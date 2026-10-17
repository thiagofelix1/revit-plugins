# revit-plugins

Pesquisa e protótipos de plugins Revit com IA e normas brasileiras. O primeiro produto em estudo é o cálculo de chuveiros automáticos (SPK).

Ainda não há add-in do Revit neste repositório. O que existe hoje é documentação da API e um motor hidráulico em .NET, sem dependência do Revit, para validar o cálculo no Linux antes de ligar o modelo.

## Pastas

| Pasta | Conteúdo |
|---|---|
| [docs/api/](docs/api/README.md) | Mapa da Revit API (2023–2027), APS Design Automation e ambiente de desenvolvimento |
| [docs/refinamentos/spk/](docs/refinamentos/spk/README.md) | Refinamento do produto SPK e o que validar no cálculo |
| [motor-spk/](motor-spk/README.md) | Motor hidráulico (NBR 10897 / IT-24): biblioteca, testes e CLI |
| `plugin-inicial-arquivos/` | Material de referência. A NBR ABNT e memoriais de cliente não entram no Git |

## Motor SPK

Precisa do .NET SDK 10.

```bash
cd motor-spk
dotnet test
dotnet run --project src/Spk.Cli -- calcular exemplos/galpao-vga1.json exemplos/saida
```
