# Em que o cálculo se baseia

O SPK calcula pela **ABNT NBR 10897:2020** e, para armazenamento, pela **IT-24/2019 do CBPMESP**. Os itens são citados no memorial; o texto das normas não é reproduzido aqui.

## Hidráulica

- **Perda de carga por Hazen-Williams** (NBR 9.4.2.1.1), com o fator C escolhido em Dados do projeto (Tabela 30) e o diâmetro interno do tubo.
- **Pressão total** (NBR 9.4.4.9): a pressão de velocidade é considerada nos nós.
- **Comprimento equivalente** de cotovelos, tês, cruzetas e válvulas somado ao trecho (NBR 9.4.3.4). No tê e na cruzeta, só no ramo que desvia o fluxo (NBR 9.4.4.7). A válvula é reconhecida pelo nome da família (gaveta, borboleta, retenção), e os valores vêm de tabela de literatura: confira com o catálogo do fabricante.
- **Vazão do bico**: Q = K × √P, com P em bar e K em L/min/bar½ (NBR 5.2.2.1).
- **Desnível**: cada metro de subida custa a pressão de uma coluna de água.
- **Rede em árvore ou em anel**: a rede inteira é resolvida de uma vez (método do gradiente global), então grelhas e malhas fechadas são calculadas como são, sem simplificar.

## Área de operação

A **base de cálculo** define a demanda:

| Base | O que exige |
|---|---|
| Densidade e área pela Figura 42 | Densidade da classe de risco para a área de operação escolhida; cada bico da área com pelo menos densidade × área coberta (NBR 8.5.5 e 9.4.4.6) |
| Densidade e área informadas | O mesmo, com os valores digitados |
| ESFR | 12 bicos, 4 em cada um de 3 ramais, com a pressão mínima da tabela da IT e área mínima de 89 m² (IT-24 5.2.2.3.3) |

O SPK **procura a área mais desfavorável**: monta todas as posições possíveis da área retangular ao longo dos ramais (NBR 9.4.4.2), calcula cada uma e escolhe a que pede mais pressão na entrada (ou a de menor folga em relação ao abastecimento, se ele foi informado). Isso substitui a escolha "no olho" do ponto mais distante e dispensa os cálculos adicionais de área vizinha (NBR 9.4.4.4.2). Se a rede não comporta a área exigida, o cálculo **reprova**; ele nunca calcula com menos bicos e aprova.

## Verificações

| Verificação | Item |
|---|---|
| Convergência do cálculo da rede | NBR 9.4.3.1 |
| Área de operação com o número de chuveiros exigido | NBR 9.4.4.2 ou IT-24 |
| Pressão mínima em todos os chuveiros da área | NBR 9.4.4.10 |
| Vazão de cada chuveiro maior ou igual a densidade × área | NBR 9.4.4.6 |
| Chuveiros da área cobrem a área de operação | NBR 9.4.4.2 |
| Pressão máxima de 1.210 kPa em risco extra | NBR 9.4.4.11 |
| Diâmetro mínimo dos tubos | NBR 9.4.1.2 |
| Área do setor (VGA) dentro do limite por coluna | NBR 7.3, Tabela 8 |
| Abastecimento atende a demanda (se informado) | NBR 9.3.4 e Anexo B |

## Hidrantes e reserva

A vazão de hidrantes e a duração vêm de Dados do projeto ou, se ficarem em branco, da Tabela 29 da NBR (em armazenamento são obrigatórias, pela IT-24). A vazão total na entrada é a dos chuveiros mais a dos hidrantes, e a reserva de incêndio é a vazão total vezes a duração.

## O papel do engenheiro

O SPK é uma ferramenta de cálculo e desenho. A classe de risco, a base de cálculo, os bicos e a conferência do resultado são decisões do engenheiro responsável, que assina o projeto.
