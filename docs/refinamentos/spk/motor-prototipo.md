# Protótipo do motor de cálculo — o que validar com o Ciroca

Código em [`motor-spk/`](../../../motor-spk/README.md). Exemplo de saída: [`memorial.md`](../../../motor-spk/exemplos/saida/memorial.md).

## O que ele faz hoje

Dado um setor (um VGA) com nós, tubos e bicos:

1. Calcula a rede inteira (espinha de peixe, grelha ou anel) por Hazen-Williams, com comprimentos equivalentes e desnível.
2. **Acha sozinho a área de operação mais desfavorável**: testa todas as posições do retângulo da área (lado paralelo aos ramais ≥ 1,2√A) e pega a pior. Num VGA de 448 bicos são cerca de 1 200 posições, em menos de 5 s.
3. Diz a pressão e a vazão necessárias no VGA, o bico crítico, a vazão total com hidrantes e a reserva.
4. Compara com a curva da bomba ou com um ensaio de vazão (estática + residual).
5. Roda as verificações da NBR e gera o memorial com uma tabela por bico e por trecho.

Critérios disponíveis: densidade-área pela Figura 42 (com os ajustes de resposta rápida, teto inclinado, ação prévia e alta temperatura), densidade e área informadas à mão (para usar valores da IT-24) e ESFR (12 bicos, 4 em 3 ramais, pressão mínima da tabela da IT).

### Exemplo (rede fictícia)

Galpão de risco ordinário 2, 320 bicos K80 em espinha de peixe, rede a 9 m, VGA no térreo: 864 áreas avaliadas; a pior pede **499 kPa no VGA** com 1 487 L/min nos bicos (2 437 L/min com hidrantes, reserva de 146 m³). O bico mais distante, no último ramal, fica exatamente nos 85 L/min exigidos (8,1 L/min/m² × 10,5 m²).

Um resultado que vale mostrar a ele: na área escolhida, os bicos do ramal incompleto ficaram do lado da subgeral, que é o que a NBR indica na Nota 2 da Figura 46. Isso não foi programado como regra; o motor chegou lá testando todas as posições.

## Como foi testado

São 27 testes automáticos. Os principais comparam o motor com o cálculo feito à mão pelo método clássico (bico remoto no mínimo, sobe até o nó, balanceia o outro ramo) e conferem que a soma das perdas de cada malha da grelha fecha em zero.

### Caso real: Mercantil Nova Era (MNE Ponta Negra, 02/10/2023)

Memorial do **Sprinkler-CALC 7.2 Win** (Walsh Engineering, EUA) em `plugin-inicial-arquivos/MEMORIAL SPRINKLER NOVA ERA - 02-10.23.txt`. Rede reconstruída em `motor-spk/exemplos/nova-era.json`.

| | Sprinkler-CALC | Nosso motor |
|---|---|---|
| 12 bicos ESFR K360, 3 ramais × 4 | sim (nós 92–95, 100–103, 108–111) | os mesmos, área manual |
| Bico mais desfavorável | 109 a **2,83 bar** / 603 L/min | **B109 a 2,83 bar** |
| Vazão dos 12 bicos | 7 564 L/min | 7 598 L/min (+0,4 %) |
| Pressão no bico, nó a nó | — | diferença máxima **0,01 bar** |
| Pressão na base da coluna (nó 1) | 14,50 bar | 12,03 bar |

A diferença de 2,47 bar na fonte é o **“2,50 bars friction loss at ref. pt. 0”** do memorial (dispositivo não desenhado na tabela de tubos; o salto aparece entre os nós 23 e 24). Da grelha para frente (nó 24 até os bicos) os números batem. Sem a entrada original do .CALC, não dá para cravar se esse 2,50 bar é um filtro/válvula ou um acréscimo de relatório.

Outros dados que o memorial responde: tubos **BS 3600** e **BS 1387** galvanizado (diâmetros internos 208,3 / 158,3 / 66,87 / 65,8 mm, C = 120); hidrante **0** neste cálculo; velocidade máxima **7,22 m/s**; rede em **grelha** (vazões negativas nos ramais).

## Perguntas para o Ciroca

### Valores que tirei da norma, mas precisam de confirmação

1. **Figura 42.** O PDF só traz o gráfico, sem tabela. Usei estes extremos de cada reta (densidade em L/min/m² na área mínima → na área máxima):
   - leve: 4,1 em 140 m² → 2,85 em 280 m²;
   - ordinário 1: 6,1 em 140 m² → 4,9 em 370 m²;
   - ordinário 2: 8,1 em 140 m² → 6,9 em 370 m²;
   - extra 1: 12,2 em 230 m² → 10,2 em 465 m²;
   - extra 2: 16,3 em 230 m² → 14,3 em 465 m².

   Ele usa esses valores? Qual ponto da curva costuma escolher?
2. **Ajustes da área de operação somados.** Com resposta rápida (−35 % a 4 m) e teto inclinado (+30 %), considerei −5 % sobre a área original (8.5.5.7). É assim que ele faz, ou aplica um ajuste em cima do outro?
3. **Pé-direito entre 6,0 e 6,1 m com resposta rápida.** A Figura 43 diz "0 % acima de 6 m", mas o texto (8.5.5.3.1) permite a redução até 6,1 m. Usei a fórmula até 6,1 m.
4. **Pressão de velocidade.** A NBR imprime 255·Q²/D⁴; pela física é 225. O motor usa só a pressão total (Pt), que a NBR permite (9.4.4.9), então isso hoje não afeta o resultado. Ele calcula com Pt ou com Pn?

### Dados que dependem da prática dele

5. **Comprimentos equivalentes.** Usei a tabela de literatura (aço Schedule 40, C = 120, corrigida pelo C). Ele usa tabela de fabricante? De qual?
6. **Tubos.** Qual tubo ele especifica: NBR 5580 classe média, NBR 5590 (Schedule 40), Schedule 10? Preciso dos diâmetros internos que ele usa.
7. **Conexões no cálculo.** Ele conta o tê da subgeral no ramal e os cotovelos de subida dos bicos? (A NBR 9.4.4.7 tem as regras; quero saber como o software dele aplica.)
8. **Bomba.** Ele calcula até o VGA ou até a bomba (com a sucção e o barrilete)? Hoje a "fonte" pode ser qualquer nó: basta modelar o trecho da bomba até o VGA.
9. **Hidrantes.** Ele soma a demanda de hidrantes no VGA, na bomba ou no ponto onde o hidrante se liga à rede? O motor aceita as duas formas.
10. **Velocidade.** A NBR não limita a velocidade. Ele, ou o Corpo de Bombeiros, adota algum limite (5 ou 6 m/s, por exemplo)?
11. **Depósitos (IT-24).** Quais tabelas ele mais usa (porta-paletes, ESFR, pilhas)? Hoje a densidade e a área da IT entram à mão; dá para embutir as tabelas mais usadas.
12. **Memorial.** O que o Corpo de Bombeiros pede e em qual formato? Ele pode mandar um memorial antigo como modelo?

### O teste que decide se o motor está certo

13. **Dois ou três projetos já calculados no software do XP**, com a entrada (traçado do caminho crítico, diâmetros, comprimentos, conexões, K, cotas) e a saída (pressão e vazão em cada nó). A gente roda os mesmos dados no motor e compara nó a nó. Proposta de tolerância: ±1 kPa por nó e ±1 % na vazão total.
14. **Qual é o nome do software?** Se ele exportar arquivo, dá para importar os projetos antigos direto.

## O que ainda não está no protótipo

- Método por tabela (8.4, só para sistemas de até 465 m²) e método por recinto (8.5.6).
- Tabelas da IT-24 embutidas (classes de mercadoria, alturas, porta-paletes, chuveiros internos).
- Sistemas secos e de ação prévia (tempo de descarga), Darcy-Weisbach, válvulas redutoras de pressão.
- Hidrante com vazão dependente da pressão (hoje é vazão fixa no nó).
- Bicos acima e abaixo do forro somados na mesma área (9.4.4.5.2).
- Dimensionamento automático de diâmetros e escolha de bomba (Fase 2 do refinamento).
- Leitura do modelo Revit (próximo passo, no Windows): o add-in vai gerar o mesmo JSON que a CLI lê hoje.
