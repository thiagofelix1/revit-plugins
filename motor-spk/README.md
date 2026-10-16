# Motor SPK — protótipo

Cálculo hidráulico de chuveiros automáticos pela ABNT NBR 10897:2020 (e base ESFR da IT-24/2019), sem dependência da Revit API. O que validar com o projetista está em [`docs/refinamentos/spk/motor-prototipo.md`](../docs/refinamentos/spk/motor-prototipo.md).

## Rodar

Precisa do .NET SDK 10 (no Linux: `~/.dotnet/dotnet`, instalado pelo `dotnet-install.sh`).

```bash
dotnet test                                                         # 27 testes
dotnet run --project src/Spk.Cli -- exemplo exemplos/galpao-vga1.json
dotnet run --project src/Spk.Cli -- calcular exemplos/galpao-vga1.json exemplos/saida
```

O resultado do exemplo está em [`exemplos/saida/memorial.md`](exemplos/saida/memorial.md).

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/Spk.Motor` | Biblioteca `netstandard2.0` (carrega no Revit 2023–2027). |
| `src/Spk.Motor/Modelo` | Rede: nós, trechos, chuveiros, fonte (VGA). |
| `src/Spk.Motor/Calculo` | Solver da rede, demanda, busca da área mais desfavorável, abastecimento, verificações. |
| `src/Spk.Motor/Normas` | Tabelas e regras da NBR e da IT, cada valor com o item de origem. |
| `src/Spk.Motor/Relatorios` | Memorial em Markdown. |
| `src/Spk.Cli` | Linha de comando com entrada em JSON (o formato que o add-in vai exportar). |
| `testes/Spk.Motor.Testes` | xUnit; inclui comparações com o cálculo de mão. |

## Como o cálculo funciona

1. **Rede**: grafo de nós e trechos. A perda de carga é Hazen-Williams (9.4.2.1.1) com o comprimento real mais o equivalente de conexões. Cada chuveiro aberto é um emissor Q = K√P (K em L/min/bar^½), sem refluxo.
2. **Solver**: método do gradiente global (o mesmo do EPANET). Resolve árvore, grelha e anel com o mesmo código e fecha massa e energia em cada malha muito abaixo da tolerância de 1 kPa (9.4.3.1). Os ramos sem chuveiro aberto são podados antes.
3. **Demanda**: procura a menor pressão na fonte em que todos os chuveiros abertos atendem a vazão (densidade × área coberta, 9.4.4.6) e a pressão mínima (48 kPa ou a da IT/catálogo, 9.4.4.10). O chuveiro que fica no limite é o mais desfavorável.
4. **Área mais desfavorável**: gera todas as posições da área retangular (N chuveiros, n por ramal com lado ≥ 1,2√A, 9.4.4.2), calcula cada uma em paralelo e escolhe a de maior pressão necessária. Com curva de abastecimento, escolhe a de menor folga.
5. **Projeto**: soma os hidrantes (Tabela 29), calcula a reserva, compara com a curva da bomba e roda as verificações (Tabela 8, 9.4.1.2, 9.4.4.11 etc.).

Desempenho medido: uma espinha de peixe de 448 bicos (4 704 m²) leva 0,25 s para avaliar 1 248 posições; a mesma rede em grelha leva 4,5 s.
