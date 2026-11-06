# revit-plugins

Pesquisa e protótipos de plugins Revit com IA e normas brasileiras. O primeiro produto é o cálculo de chuveiros automáticos (SPK); o segundo, em pesquisa, é o projeto elétrico predial (AutoEletrica).

Ainda não há add-in do Revit neste repositório. O que existe hoje é documentação da API e um motor hidráulico em .NET, sem dependência do Revit, para validar o cálculo no Linux antes de ligar o modelo.

## Pastas

| Pasta | Conteúdo |
|---|---|
| [docs/api/](docs/api/README.md) | Mapa da Revit API (2023–2027), APS Design Automation e ambiente de desenvolvimento |
| [docs/refinamentos/spk/](docs/refinamentos/spk/README.md) | Refinamento do produto SPK e o que validar no cálculo |
| [motor-spk/](motor-spk/README.md) | Motor hidráulico (NBR 10897 / IT-24): biblioteca, testes e CLI |
| [autoeletrica/](autoeletrica/README.md) | Segundo produto: projeto elétrico predial (NBR 5410). Pesquisa de mercado, base de regras, API, plano de desenvolvimento e normas |
| [motor-eletrica/](motor-eletrica/README.md) | Motor do AutoEletrica em .NET puro: tabelas da NBR 5410 e satélites como dados, dimensionamento pelos seis critérios, Verificar modelo, memorial, CLI e testes |
| [addin-eletrica/](addin-eletrica/README.md) | Add-in do AutoEletrica (Revit 2025–2027): dados do projeto, ambientes, passar circuitos pelos eletrodutos, verificar, calcular, etiquetar |
| `plugin-inicial-arquivos/` | Material de referência. A NBR ABNT e memoriais de cliente não entram no Git |

## Motor SPK

Precisa do .NET SDK 10.

```bash
cd motor-spk
dotnet test
dotnet run --project src/Spk.Cli -- calcular exemplos/galpao-vga1.json exemplos/saida
```
