# Aderência do SPK à ABNT NBR 10897:2020

Conferência feita em 03/11/2026, lendo a norma e comparando com o código. Aqui só itens e números, sem o texto da norma: a cópia usada não é licenciada para este projeto e não entra no repositório. Complementa a [aderência à IT-23/2025](aderencia-it23-2025.md); a IT-23 reproduz boa parte da NBR com outra numeração (as Tabelas 7 a 11 da IT são as 23 a 27 da NBR).

Lido no detalhe: seções 3 a 10 e Anexo A. Lido por cima, por estarem fora do que o SPK faz: Anexo B (reservatórios) e Anexo C (inspeção). As figuras só foram conferidas onde pesam no cálculo.

## Divergências, da mais grave para a menor

Corrigidos em novembro de 2026, e mantidos na tabela como registro: 1 (campo de inclinação em Dados do projeto e aviso quando o teto medido não bate), 2 (aviso quando o C não bate com a Tabela 30 pelo nome do tubo; o C continua digitado) e 6 (Figura 42). O 3 foi feito para viga e bico de cobertura-padrão (Tabela 15 e a exceção de 7.10.1.1.2): reprova no Calcular e aparece em Bicos por ambiente, Gerar rede e Conferir na planta; peça com mais de 1 m de altura, que parece treliça, só avisa. Faltam pilar, duto e viga inteira abaixo do bico (Tabela 16 e Figura 28), porque o SPK ainda não lê pilar nem duto.

| # | Item da NBR | O que a norma pede | O que o SPK faz | Efeito |
|---|---|---|---|---|
| 1 | 8.5.5.4 | Teto com inclinação acima de 16,7 %: área de operação +30 % | O motor sabe aplicar, mas `DadosProjeto.CriarBase` nunca passa a inclinação | **Contra a segurança**: área menor que a exigida em telhado inclinado, sem aviso |
| 2 | Tabela 30 | C depende do material: aço preto em sistema seco ou ação prévia 100; aço preto molhado e galvanizado 120; cobre e plástico 150 | Um C só, digitado em Dados do projeto, sem relação com o tipo de tubo | **Contra a segurança** em sistema seco com C 120; a favor em cobre |
| 3 | 7.10.1, Tabelas 15 e 16, Figura 28 | Distância à obstrução × altura do defletor; pilar e tesoura a 3 vezes a maior dimensão, até 600 mm | Só aviso | Bico pode ficar obstruído sem o SPK reprovar |
| 4 | 7.8.1.1.3, 7.7.1, 7.8.1.3, 7.9.1 | Desnível acima de 900 mm conta como parede; espaçamento ao longo da inclinação; cumeeira a até 0,9 m; defletor paralelo ao telhado acima de 16,7 % | Rede plana, teto único por ambiente | Já na lista: rede seguindo o telhado |
| 5 | 8.5.3, 8.2 | Vários riscos: regra própria para hidrantes; a ocupação de maior demanda avança 4,5 m se não houver barreira | Uma classe por projeto | Tudo calculado com uma classe só |
| 6 | Figura 42 | Ver tabela abaixo | Pontas das curvas erradas | A favor da segurança: densidade maior que a pedida acima da área mínima |
| 7 | Tabela 14, 7.7.4.5, 7.8.4, 7.10.4 | ESFR: 9,3 m²; 3,7 m até 9,1 m de altura e 3,1 m acima; mínimo 2,4 m; defletor 150–350 mm (K 200 e 240) ou 150–450 mm (K 320 e 360) | Nenhuma conferência de ESFR | Rede ESFR só tem o cálculo conferido |
| 8 | 9.3.2, 9.3.4 | Folha de resumo com data, endereço, proprietário, responsável técnico, órgão; gráfico em Q^1,85 com abastecimento e demanda | Memorial sem esses campos e sem gráfico | Memorial incompleto para aprovação |
| 9 | 8.5.7 | Fileira única: todos os bicos dela, até 7 | Busca pela regra geral de área | A conferir em corredor |
| 10 | 7.3.1, 7.3.2 | Limite por coluna vale por pavimento; mezanino não conta | Soma tudo no setor | Pode reprovar sem precisar em vários pavimentos |
| 11 | 7.12.8.2, 7.12.8.3 | Subgeral termina em DN 32 ou maior, com conexão removível | Sem conferência; a ponta costuma ficar com o DN inicial e tampão | Hoje atende por acaso |

### Figura 42

Lido das linhas vetoriais da figura, calibradas pelos rótulos dos eixos.

| Curva | Área mínima (norma = SPK) | Área máxima | Norma | SPK |
|---|---|---|---|---|
| Leve | 140 m² → 4,1 | 280 m² | 2,8 | 2,85 |
| Ordinário 1 | 140 m² → 6,1 | 370 m² | **4,1** | 4,9 |
| Ordinário 2 | 140 m² → 8,1 | 370 m² | **6,1** | 6,9 |
| Extraordinário 1 | 230 m² → 12,2 | 465 m² | **8,1** | 10,2 |
| Extraordinário 2 | 230 m² → 16,3 | 465 m² | **12,2** | 14,3 |

Quem calcula na área mínima não é afetado.

## Não implementado, sem risco para o resultado

- Sala pequena (7.6.2, 7.7.2.1.3): risco leve, até 75 m²; bico a até 2,7 m da parede. Hoje o SPK usa mais bicos.
- Redução por resposta rápida (8.5.5.3) e temperatura alta (8.5.5.6): o motor aplica, mas a janela não oferece. A área fica maior.
- Método por recinto (8.5.6) e demanda por tabela (8.4): o SPK sempre calcula. Sistema novo pede cálculo hidráulico (8.1).
- Laterais (Tabela 12), cobertura estendida (Tabela 11) e CCAE (Tabela 13): fora do SPK; lateral é recusado com o motivo.

## Fora do que o SPK verifica

O projetista responde por isto, e o SPK não confere: conexão de teste (5.7), recalque (5.8), alarme (5.9), suportes (5.10, Tabela 6), válvula de alívio e manômetros (6.1), temperatura do bico (7.4), espaços encobertos (7.12.1), shaft, escada e poço de elevador (7.12), clareza até a estocagem (7.11), lista das plantas (9.1), placa (10.2) e estoque de sobressalentes (5.2.7: 6, 12 ou 24 bicos, e pelo menos 4 por tipo).

## Conferido e aderente

- Hazen-Williams com 6,05 × 10⁷ (9.4.2.1.1) e pressão total (9.4.4.9).
- Pressão mínima de 48 kPa (9.4.4.10) e máxima de 1 210 kPa em risco extra (9.4.4.11).
- Lado da área ≥ 1,2 √A, frações para cima (9.4.4.2).
- Densidade da área mínima abaixo de 140 ou 230 m² (8.5.4 a e b); ajustes somados sobre a área original (8.5.5.7).
- Tabela 8 (área por coluna), Tabela 10 (área e distância), Tabela 29 (hidrantes e duração), Tabelas 31, 33 e 34 (diâmetros).
- Tabelas 23 a 27 (teto alto), com a ressalva da cobertura estendida.
- Parede: 100 mm no mínimo, metade da distância no máximo, ¾ em parede irregular. Entre bicos: 1,8 m.
- Defletor: 25–300 mm; 25–150 mm abaixo da viga e até 560 mm do teto; um bico por vão (7.8.1.2).
- DN mínimo: 25 em ferroso, 20 em cobre e plástico (9.4.1.2).

O tratamento das conexões (9.4.4.7) foi conferido só pela leitura do código, não contra um exemplo calculado.

## O que a norma responde das perguntas ao engenheiro

- **Treliças:** 7.2.4, 7.10.1.2.5, 7.10.1.2.6 e Figura 28.
- **Obstrução por viga:** Tabela 15.
- **Vigotas e terças:** 7.8.1.2 e suas exceções, mais a Tabela 15.
- **Sala pequena:** 7.6.2 e 7.7.2.1.3 permitem.
- **Setor menor que a área de operação:** 8.5.4 a confirma a densidade de 140 m².
- **Gôndolas:** 4.4 manda qualquer armazenamento, mesmo transitório e de qualquer altura, para a NBR 13792, cancelada e substituída pela NBR 16981:2021. O Anexo A cita lojas como Ordinário 2. A decisão continua com o engenheiro: ver abaixo o que a 16981 acrescenta.
- **Classes do Anexo A úteis no MNEVVM:** lojas Ordinário 2; padaria e área de serviço de restaurante Ordinário 1; área de refeição e escritórios Leve.

## NBR 16981:2021 e as gôndolas

Conferência de 03/11/2026, sem mudança no código. Fonte: cópia da 1ª edição da NBR 16981 (28/07/2021), não licenciada para este projeto; a re-checar na norma oficial. Só itens e números. Lido por seções e Anexo A; do Anexo B, as regras de obstrução (B.3.1.5) e o detalhe de intraprateleiras (B.4) só por alto.

A 16981 cancela a NBR 13792:1997 e é usada junto com a NBR 10897.

| Tema | Item da 16981 | O que diz | Efeito no SPK, se a área for armazenamento |
|---|---|---|---|
| Gôndola | Toda a norma | As palavras "gôndola" e "supermercado" não aparecem | Enquadramento é decisão do engenheiro |
| Até 3,7 m, classes I a IV | 5.2.1.1, 6.2.1.1, Tabela 62 | Mesmas exigências do transitório. O 5.2.1.1 cita a Tabela 58 por engano; lido como 62 | Classe I a IV define a curva, sem depender de ser transitório |
| Hidrantes e duração | Tabela 62 | ORD1 e ORD2: 950 L/min por 90 min. EXT1: 1 900 L/min por 120 min | A Tabela 29 da 10897 dá 60 min em Ordinário: reserva maior |
| Curvas | Figura 84 | Leitura do gráfico: ORD2 de 139 m² → 8,1 a 279 m² → cerca de 6,9 | Na área mínima, a densidade de ORD2 é a mesma da 10897 |
| K mínimo | 4.2.2 | Densidade acima de 8,1 pede K 115; acima de 13,9, K 160 certificado | O motor não confere; só o teto alto tem K mínimo. Regra a criar se a área for armazenamento |
| Atacarejo | B.5.1, Anexo B (informativo) | Porta-palete até 6,0 m como prateleira aberta, com condições. O Anexo B não se mistura com as seções 4 a 8 (B.1.3) | Caminho próprio, por número de chuveiros e pressão, fora do que o SPK calcula |

A classe de mercadoria (I a IV, plásticos) é outra classificação, diferente do risco da 10897. Hoje o SPK tem uma classe só por projeto.

**Pergunta ao engenheiro:** a área de vendas é loja (Ordinário 2 pelo Anexo A da 10897) ou armazenamento (16981)? Exemplo de ordem de grandeza, com 8,1 mm/min e cerca de 140 m²: 125 m³ de reserva como Ordinário 2 e 187 m³ como classe III até 3,7 m.

## NBR 5580 e NBR 5590: diâmetros internos

As correções no código que saíram desta conferência, e o que ainda falta, estão em [aderencia-tubos-e-bombas.md](aderencia-tubos-e-bombas.md): os temas `TubosAco`, DN 90, interno vindo do Revit e Tabela 3 da tabela abaixo já foram tratados lá.

Conferência de 03/11/2026. Fontes: cópias da NBR 5590:2015 (4ª edição) e da NBR 5580:2002, não licenciadas para este projeto; a re-checar na norma oficial. A edição da 5580 precisa ser conferida: a 10897 a cita com outro título. Os internos são calculados: externo − 2 × espessura nominal.

| DN | 5580 L | 5580 M | 5590 Sch 10 | 5590 Sch 40 |
|---|---|---|---|---|
| 25 | 28,4 | 27,0 | 27,9 | 26,6 |
| 32 | 37,1 | 35,7 | 36,7 | 35,1 |
| 40 | 42,3 | 41,6 | 42,8 | 40,9 |
| 50 | 54,3 | 52,8 | 54,8 | 52,5 |
| 65 | 69,4 | 68,6 | 66,9 | 62,7 |
| 80 | 82,2 | 80,9 | 82,8 | 77,9 |
| 90 | 94,1 | 93,1 | 95,5 | 90,1 |
| 100 | 106,8 | 105,3 | 108,2 | 102,3 |
| 125 | Não há | 130,2 | 134,5 | 128,2 |
| 150 | Não há | 155,1 | 161,5 | 154,1 |
| 200 | Não há | Não há | 211,6 | 202,7 |

| Tema | O que há | Efeito no SPK |
|---|---|---|
| `TubosAco` | Sch 10 e Sch 40 de ASME B36.10 conferem com a Tabela A.4 da 5590, com diferença de até 0,05 mm, vinda do arredondamento do diâmetro externo. DN 90 não está lá, e a classe 5580 também não | O "CONFERIR" de `Conexoes.cs:70` fica respondido para a 5590. Falta decidir se a 5580 entra |
| DN 90 | Existe nas duas normas e em `TabelaDeDiametros` | `ComprimentosEquivalentes` não tem DN 90: a conexão fica sem comprimento equivalente, com aviso (`LeitorRede.cs:393`). Perda menor que a real |
| DN 150 Sch 40 e Sch 80 | A Tabela A.4 tem 15 espessuras e 14 rótulos: marca STD e 40 em 6,35 mm e XS e 80 em 9,52 mm; a A.5 usa 7,11 mm e 10,97 mm | Mantidos os valores da A.5, a confirmar na norma oficial |
| Interno vindo do Revit | `LeitorRede.cs:1101` lê do tipo de tubo, sem comparar com nenhuma tabela | Tipo errado muda a perda sem aviso: DN 50 com Sch 10 no lugar de Sch 40 dá cerca de 19 % a menos; DN 65 com 5580 M no lugar de Sch 40, cerca de 35 % a menos |
| Mesmo DN, externo diferente | DN 65: 76,1 mm na 5580 e 73,0 mm na 5590. DN 125 e DN 150 também diferem. Rosca ISO 7-1 na 5580 e NPT na 5590 | Projetista precisa saber qual norma o tipo de tubo segue |
| Classe leve da 5580 | Só até DN 100 | Acima disso, classe M ou 5590 |
| Tabela 3 da 10897 (edição 2014) | Parede mínima coincide com Sch 10 até DN 150; em DN 200 pede 4,78 mm, e o Sch 10 tem 3,76 mm | Sem conferência no SPK |

## NBR 16704: bombas

Conferência de 03/11/2026, sem mudança no código. Fonte: cópia da 1ª edição (27/02/2019), não licenciada para este projeto; a re-checar na norma oficial. Lido no detalhe: seções 1 a 8 e 12 e Anexo A. Seções 9 a 11 (painéis e diesel), só os títulos dos itens e os valores principais. Tabelas lidas pelo texto extraído, sem conferir na imagem.

| Tema | Item | O que diz | Efeito no SPK |
|---|---|---|---|
| Escolha da bomba | 4.8.1, Anexo A | Vazão de projeto até 150 % da nominal; boa prática de 90 a 120 % | Regra para "Sugerir a bomba pela curva" |
| Vazões nominais | Tabela 1 | Lista padronizada, de 95 a 18 925 L/min | Idem |
| Pressão nominal | 4.8.2 | Pelo menos 2,7 bar | Idem |
| Curva | 6.2 | Sem vazão, até 140 % da pressão nominal; a 150 % da vazão, pelo menos 65 % | `CurvaAbastecimento` só interpola os pontos informados e não confere isso |
| Diâmetros | Tabela 2 | Sucção, descarga, alívio, medidor e cabeçote por vazão nominal | Serve para "Tubo geral até a casa de bombas" e para o quadro de bombas |
| Sucção | 4.14.3.1, 6.1.2 | Sem pressão negativa; até −0,2 bar no flange com tanque na cota da bomba ou acima | Fora do cálculo hoje |
| Casa de bombas | 4.12 | TRRF de 2 h; fora do prédio, a 15 m; diesel com chuveiros | Fora do que o SPK verifica; o projetista responde |
| Ensaio | 12.1, 12.2 | 13,8 bar, ou 3,4 bar acima da máxima, por 2 h; em campo, 6 + 6 partidas e 1 h no total | Fora do que o SPK verifica |

Exemplo de ordem de grandeza: demanda de 2 076 L/min leva à bomba de 1 892 L/min, operando a 110 %.

A 16704 difere do Anexo B da 10897 de 2014 em sucção, ciclo de partida do diesel e fonte de energia. A comparação com a edição de 2020 ainda não foi feita. **Pergunta ao engenheiro:** para projeto novo, seguir a 16704 onde as duas divergem?

## Normas que faltam

- ABNT NBR 13206: diâmetros internos dos tubos de cobre.
- ABNT NBR 13714 (hidrantes): quando o cálculo for até a bomba.
- IT-24 vigente: a do repositório é de 2019.
- IT-23 no site oficial: a usada é uma cópia, e a Portaria CCB-009/800/2025 anuncia alteração.
