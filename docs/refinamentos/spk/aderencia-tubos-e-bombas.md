# Aderência do SPK às normas de tubos e bombas

Conferência de 03/11/2026, com os PDFs das normas e o código; complementa as seções de tubos e bombas da [aderência à NBR 10897](aderencia-nbr10897-2020.md). Aqui só itens e números, sem o texto das normas: as cópias usadas não são licenciadas para este projeto e não entram no repositório. A re-checar nas normas oficiais.

| Norma | Edição da cópia | Lido |
|---|---|---|
| ABNT NBR 5580 | 2002 | 1.2, 1.3, 4.6 a 7 e Anexo A. A NBR 10897 a cita com outro título, o que sugere outra edição: confirmar no catálogo da ABNT |
| ABNT NBR 5590 | 2015 | Tabelas A.4 e A.5 de DN 25 a 200, roscas |
| ABNT NBR 16704 | 2019 | 4.8, 6.2, Tabelas 1 e 2, Anexo A |
| ABNT NBR 10897 | **2014** | 5.3 a 5.5, 9.4.1 a 9.4.3. O SPK segue a de 2020, que não está à mão: conferir 5.3.2 e a Tabela 3 nela |

## Corrigido no código

| # | Item | Antes | Agora |
|---|---|---|---|
| 1 | NBR 10897 9.4.2.1.1: diâmetro interno real | Trecho entre duas peças usava o DN do conector como interno (DN 65 com 65,00 mm, real 62,71 mm em Sch 40: perda cerca de 16 % menor) | Usa o interno do mesmo DN no segmento dos tubos da rede |
| 2 | DN 90 existe nas duas normas de tubo | Sem Sch 10, Sch 40 nem comprimento equivalente: conexão em DN 90 ficava fora do cálculo, só com aviso | Sch 40 de 90,12 mm, Sch 10 de 95,50 mm; comprimentos equivalentes da coluna de 3½" da mesma tabela de literatura |
| 3 | NBR 10897 5.3.2: tubos de aço até 2,07 MPa | Nada conferia: uma rede com 56 bar passou sem reprovar | Verificação "Pressão em tubos de aço até 2 070 kPa" |
| 4 | NBR 10897 5.3.2, 5.3.2.4 e Tabela 3 | O interno do Revit entrava sem conferência | Aviso por tipo de tubo, em `TubosNormalizados` (abaixo) |
| 5 | NBR 10897 9.4.1.2 e 5.3.2 dependem do material | O leitor de rede não informava o material: toda rede contava como aço | Material pelo nome do tipo e do segmento; nome que não diz conta como aço |

### O que o aviso de tubo confere

Só em tipo cujo nome se diz de aço. Nunca troca o diâmetro: o tubo é escolha do projetista.

| Situação | Exemplo | Aviso |
|---|---|---|
| Interno fora de todas as classes | DN 50 com 50 mm | ATENÇÃO |
| Nome aponta outra norma, com interno maior que o do nome | "Schedule 40" com interno de 5580 classe média | ATENÇÃO: pressão calculada menor que a real |
| Nome aponta outra norma, com interno menor que o do nome | "BSP" com interno de Sch 40 | Informa: a favor da segurança |
| Nome diz rosca, parede fina | "BSP" em classe leve; "NPT" em Sch 10 | ATENÇÃO (5.3.2.4) |
| NBR 5590 abaixo da Tabela 3 | Sch 10 em DN 200: 3,76 mm para 4,78 mm | ATENÇÃO |

Rosca BSP segue a ISO 7-1, que é a da NBR 5580 (1.2); NPT é a da NBR 5590 (4.7.2). A 10897 aceita as duas (5.5.4.1).

### Tabela usada

Interno = externo − 2 × espessura. As normas não trazem o interno pronto.

| DN | 5580 L | 5580 M | 5580 P | 5590 Sch 10 | 5590 Sch 40 | 5590 Sch 80 |
|---|---|---|---|---|---|---|
| 25 | 28,40 | 27,00 | 26,20 | 27,86 | 26,64 | 24,30 |
| 32 | 37,10 | 35,70 | 34,90 | 36,66 | 35,08 | 32,50 |
| 40 | 42,30 | 41,60 | 40,80 | 42,76 | 40,94 | 38,14 |
| 50 | 54,30 | 52,80 | 51,30 | 54,76 | 52,48 | 49,22 |
| 65 | 69,40 | 68,60 | 67,10 | 66,90 | 62,68 | 58,98 |
| 80 | 82,20 | 80,90 | 79,90 | 82,80 | 77,92 | 73,66 |
| 90 | 94,10 | 93,10 | 91,60 | 95,50 | 90,12 | 85,44 |
| 100 | 106,80 | 105,30 | 103,10 | 108,20 | 102,26 | 97,18 |
| 125 | Não há | 130,20 | 128,50 | 134,50 | 128,20 | 122,26 |
| 150 | Não há | 155,10 | 153,90 | 161,50 | 154,08 | 146,36 |
| 200 | Não há | Não há | Não há | 211,58 | 202,74 | 193,70 |

**DN 150 na NBR 5590:** a Tabela A.4 tem 15 espessuras e 14 rótulos, e por isso STD e Sch 40 caem na linha de 6,35 mm e XS e Sch 80 na de 9,52 mm. A Tabela A.5 dá 7,11 mm e 10,97 mm, os valores usados aqui, que também são os do ASME B36.10.

## Conferido e aderente

- `TubosAco`: Sch 10 e Sch 40 conferem com a NBR 5590, com até 0,05 mm de diferença.
- Hazen-Williams com diâmetro interno real no trecho de tubo (9.4.2.1.1).
- Comprimento equivalente de literatura, permitido na falta de dado do fabricante (9.4.3.4).
- Curva de abastecimento interpolada em Q^1,85.

## Oportunidades, sem código ainda

| # | O quê | Base | Valor para o projetista |
|---|---|---|---|
| 1 | **Sugerir a bomba:** Qn da Tabela 1 com Qproj de 90 a 120 % de Qn; limite de 150 %; pressão nominal de pelo menos 2,7 bar | 16704 4.8, Anexo A | Exemplo no MNEVVM: 3 378 L/min pede Qn de 2 839 L/min (119 %) |
| 2 | **Diâmetros mínimos da casa de bombas:** sucção, descarga, alívio, medidor e cabeçote | 16704 Tabela 2 | Qn de 2 839 L/min: sucção DN 150, descarga DN 150, alívio DN 100 |
| 3 | **Curva da bomba em Dados do projeto:** Qn e pressão nominal, conferindo o máximo de 140 % a vazão nula e o mínimo de 65 % a 150 % da vazão | 16704 6.2 | Hoje só entra a curva de ensaio de dois pontos |
| 4 | **Pressão a vazão nula contra 2,07 MPa:** hoje só entra a pressão de operação | 16704 4.7.5, 6.2.2; 10897 5.3.2 | A bomba pode passar do limite do tubo mesmo com a rede atendendo |
| 5 | **Pressão de CPVC:** 1,21 MPa e só risco leve | 10897 5.3.4 | Regra pronta; falta classificar CPVC, hoje junto com outros plásticos |
| 6 | **Memorial:** norma e classe do tubo por DN, gráfico em Q^1,85 com curva da bomba e demanda | 10897 9.3.4 | Item 8 da aderência à NBR 10897 |
| 7 | **Forma de união no tipo de tubo:** hoje só pelo nome | 10897 5.3.2 | Sem isso, classe leve em tubo acoplado e roscado não se distinguem quando o nome não diz |

**A decidir com o engenheiro:** a Qproj da bomba inclui os hidrantes? O Anexo A da 16704 considera a maior demanda de um único sistema de proteção. No exemplo do MNEVVM, só chuveiros dá 2 427 L/min, que cai fora dos 90 a 120 %: 85 % de 2 839 ou 128 % de 1 892.

## Normas que faltam

ABNT NBR 13206 (cobre), ABNT NBR 13714 (hidrantes), a edição de 2020 da NBR 10897, a IT-24 vigente e a IT-23 no site oficial.
