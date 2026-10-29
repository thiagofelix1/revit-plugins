# Aderência do SPK à IT-23/2025 (CBPMESP)

Conferência feita em 29/10/2026 contra a Instrução Técnica 23/2025 do Corpo de Bombeiros de São Paulo (Sistemas de chuveiros automáticos), em vigor desde 20/03/2025 (Portaria CCB-003/800/2025; errata na CCB-005/800/2025). Nos assuntos omissos ela remete à ABNT NBR 10897, que não está nesta máquina (licenciada). Aqui só itens e números, sem o texto da norma.

## O que o SPK já segue

| Regra | Item da IT-23/2025 | No SPK |
|---|---|---|
| Área máxima por bico e distância máxima entre bicos (spray de cobertura-padrão): risco leve 20,9 m² e 4,6 m; ordinário 12,1 m² e 4,6 m; extra 9,3 ou 12,1 m² e 3,7 ou 4,6 m, pela densidade | Tabela 2 | `EspacamentoDeBicos` |
| Área por bico = S × L, com o dobro da distância à parede | 8.11.1 | conferência de Bicos por ambiente |
| Distância mínima entre bicos 1,8 m | 8.12.4.1 | posicionamento, conferência, Inserir/Mover bico |
| Distância mínima à parede 100 mm | 8.12.3.1 | posicionamento |
| Teto sem obstrução: defletor de 25 a 300 mm do teto | 8.13.1.1.1 | 100 mm por padrão; 300 mm no corredor só com laje (para a geral caber acima) |
| Teto com viga: defletor 25 a 150 mm abaixo da viga e até 560 mm do teto | 8.13.1.2 | defletor abaixo da viga até 560 mm |
| Viga funda: defletor de 25 a 300 mm do teto com um bico em cada vão | 8.13.1.2 b) | bicos nos vãos, defletor 250 mm |
| Área máxima por coluna/VGA por pavimento: 4 800 m² (leve e ordinário), 3 700 m² (extraordinário e armazenamento) | 8.8, Tabela 1 | verificação "Área do setor (VGA) dentro do limite por coluna" |
| Área de operação retangular, lado paralelo aos ramais ≥ 1,2 √A | 11.1.3.2.1 | busca da área mais desfavorável |
| Área menor que 140 m² (leve e ordinário) usa a densidade de 140 m² | 10.4.3 a) | setor menor que a área de operação: todos os bicos dele operam (corrigido em 29/10) |
| Pressão mínima 48 kPa | 11.1.3.9 | verificação de pressão |
| Tubo ferroso no mínimo DN 25 | 11.1.1.1 | verificação de diâmetro e tabelas |
| Número de bicos por ramal e de ramais por subgeral limitado só pela água | 11.1.1.2 | não há limite no cálculo; os diâmetros por tabela são do método antigo |
| Teto com inclinação acima de 16,7 %: área de operação +30 % | 10.4.4.4 | ajuste da área |

## O que é aviso, porque o modelo não diz

- **Resposta rápida em risco leve** (8.10.1): sistema novo em risco leve usa chuveiro de resposta rápida. A família do Revit não traz isso, e o cálculo passou a avisar.
- **Obstruções** (7.10, 8.13.3): a IT remete à NBR 10897 (distância do bico à viga × altura do defletor). Hoje é aviso.

## Pontos para o engenheiro confirmar

1. **Bico direto na geral (corredor)**: a IT não proíbe (11.1.1.2 só limita pela água). Confirmar se o escritório aceita, ou se o corredor pede ramal próprio.
2. **Setor menor que a área de operação**: lido como "todos os bicos do setor operam, com a densidade de 140 m²" (10.4.3 a). Confirmar.
3. **Sala pequena** (7.7): em risco leve, sala de até 75 m² com teto desobstruído e paredes e teto incombustíveis pode ter bico a até 2,70 m da parede (em vez da metade da distância máxima). O SPK ainda não usa: usaria menos bicos. Implementar se o escritório usa.
4. **Corredor de uma fileira** (10.4.4.8.7 e 10.4.4.8.9): pelo método por recinto, a área de cálculo de um corredor com uma fileira de bicos inclui todos até 5 (risco leve, aberturas protegidas) ou 7 (fileira única). O SPK calcula por densidade e área.
5. **VGA**: a IT pede válvulas acessíveis, não obstruídas por paredes ou dutos (7.2); em prédio de vários pavimentos, uma VGA no pavimento mais baixo por limite de área e conexão setorial (CS) nos demais (8.8.1); com uma VGA só, ela pode virar válvula de retenção e chave de fluxo (8.8.2). A posição exata é escolha do projeto: o SPK põe a descida na ponta do corredor ou no ponto clicado.
