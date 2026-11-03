# Aderência do SPK à ABNT NBR 10897:2020

Conferência feita em 03/11/2026, lendo a norma e comparando com o código. Aqui só itens e números, sem o texto da norma: a cópia usada não é licenciada para este projeto e não entra no repositório. Complementa a [aderência à IT-23/2025](aderencia-it23-2025.md); a IT-23 reproduz boa parte da NBR com outra numeração (as Tabelas 7 a 11 da IT são as 23 a 27 da NBR).

Lido no detalhe: seções 3 a 10 e Anexo A. Lido por cima, por estarem fora do que o SPK faz: Anexo B (reservatórios) e Anexo C (inspeção). As figuras só foram conferidas onde pesam no cálculo.

## Divergências, da mais grave para a menor

Corrigidos em novembro de 2026, e mantidos na tabela como registro: 1 (campo de inclinação em Dados do projeto e aviso quando o teto medido não bate), 2 (aviso quando o C não bate com a Tabela 30 pelo nome do tubo; o C continua digitado) e 6 (Figura 42).

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
- **Gôndolas:** 4.4 manda qualquer armazenamento, mesmo transitório e de qualquer altura, para a NBR 13792. O Anexo A cita lojas como Ordinário 2. A decisão continua com o engenheiro.
- **Classes do Anexo A úteis no MNEVVM:** lojas Ordinário 2; padaria e área de serviço de restaurante Ordinário 1; área de refeição e escritórios Leve.

## Normas que faltam

- ABNT NBR 13792 (armazenamento): pesa na decisão das gôndolas.
- ABNT NBR 5580, NBR 5590 e NBR 13206: diâmetros internos dos tubos.
- ABNT NBR 16704 (bombas) e NBR 13714 (hidrantes): quando o cálculo for até a bomba.
- IT-24 vigente: a do repositório é de 2019.
- IT-23 no site oficial: a usada é uma cópia, e a Portaria CCB-009/800/2025 anuncia alteração.
