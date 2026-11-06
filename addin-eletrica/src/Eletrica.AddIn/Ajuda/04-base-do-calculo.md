# Base do cálculo

O cálculo segue a ABNT NBR 5410:2004, com a NBR NM 60898 para os disjuntores, a NBR NM 247-3 para os condutores e a NBR 15465 para os eletrodutos. As tabelas da norma estão no plugin como dados, cada valor com o item de onde saiu, e o memorial cita esses itens. Nenhum número do cálculo vem de inteligência artificial.

## De onde vêm os dados

| Dado | Origem no modelo |
|---|---|
| Tensões, fases, aterramento, curto presumido | Dados do projeto. |
| Quadros e hierarquia | Quadros do Revit e os circuitos alimentadores. |
| Circuitos e ligação | Circuitos do Revit: polos, tensão, neutro; tipo de carga por AE Tipo de carga ou pelos pontos. |
| Potência dos pontos | AE Potência (VA), a carga aparente da família, ou o rateio da carga do circuito. |
| Ambiente e uso | O Room no lugar do ponto, no arquivo ou no vínculo, com o uso confirmado em Ambientes. |
| Comprimento | O caminho real pelos eletrodutos, do quadro ao ponto mais distante. Sem eletrodutos, o comprimento do circuito do Revit. |
| Agrupamento | Quantos circuitos passam em cada trecho de eletroduto, gravado pelo Passar circuitos. |

## Os seis critérios (6.2.6.1.2)

Cada circuito é dimensionado do quadro de entrada para as pontas:

1. **Capacidade de corrente**: Tabelas 36 a 39 pelo método de instalação, com o fator de temperatura (Tabela 40) e o fator de agrupamento (Tabela 42) **em cada trecho por onde o circuito passa**. Vale o pior trecho (6.2.5.8).
2. **Sobrecarga**: disjuntor com IB ≤ In ≤ IZ e I2 ≤ 1,45 IZ (5.3.4).
3. **Queda de tensão**: com o comprimento real, acumulando os alimentadores. Limites de 4 % no circuito terminal a partir do quadro e de 5 % a partir do ponto de entrega (6.2.7).
4. **Curto-circuito**: corrente de curto máxima no barramento contra a capacidade de interrupção do disjuntor, e solicitação térmica I²t ≤ k²S² no curto máximo e no mínimo (5.3.5).
5. **Seccionamento automático**: curto mínimo fase-PE no fim do circuito e Zs·Ia ≤ Uo no esquema TN (5.1.2.2.4).
6. **Seção mínima**: Tabela 47, e o PE pela Tabela 58.

O curto é calculado pelo método das impedâncias, a partir da corrente presumida no ponto de entrega.

## Eletrodutos

- **Taxa de ocupação** com o diâmetro externo real de todos os condutores do trecho, inclusive os retornos dos comandos: 53 % com um condutor, 31 % com dois, 40 % com três ou mais. O plugin sugere o DN e, quando nem o maior serve, quantos eletrodutos repetir.
- **Trecho entre caixas**: até 15 m, menos 3 m por curva de 90°, e no máximo três curvas (6.2.11.1.6 e 6.2.11.1.7).
- **Compartilhamento** (6.2.10.2): circuitos do mesmo quadro de origem e com seções dentro de três valores sucessivos. Fora disso, o fator de agrupamento passa a ser 1/√n numa segunda passada (6.2.5.5.5).

## Quadros

- **Demanda** por categoria: iluminação e tomadas de uso geral por faixa de potência, chuveiros e ar-condicionado pelo número de aparelhos. Os fatores são o padrão do plugin até a norma da distribuidora do projeto entrar; podem ser desligados nos Dados do projeto.
- **Fases** balanceadas pela carga, respeitando a fase imposta no circuito (AE Fase).
- **Geral, DRs e DPS**: disjuntor geral pela demanda; DRs agrupados por tipo, até seis circuitos por DR, carregador de veículo sozinho; DPS com classe, tensão de operação, nível de proteção e seção de ligação.
- **Reserva** pela Tabela 59.

## DR e esquema TT

O DR de 30 mA é obrigatório (5.1.3.2.2) em todo ponto de banheiro com banheira ou chuveiro, nas tomadas e pontos de áreas externas e, em habitação, em todos os pontos de cozinha, copa, área de serviço, lavanderia, garagem e outros locais molhados, menos luminárias a 2,50 m ou mais. Fora de habitação, nos locais molhados, só as tomadas. O tipo sai da carga: A como padrão, F para ar-condicionado com inversor, B para carregador de veículo; o tipo AC não serve onde há eletrônica. No esquema TT, todo circuito leva DR (30 mA onde a proteção adicional é exigida, 300 mA nos demais) e, com a resistência do eletrodo informada, o plugin confere RA·IΔn ≤ UL.

## Casos especiais

- **Condutores em paralelo**: até dois por fase, quando nenhuma seção única atende (6.2.5.7).
- **Folgas**: de comprimento e de ocupação, como opções do projeto.
- **Sugestões**: cada critério reprovado sai com o que fazer, por exemplo subir a seção, dividir o circuito ou trocar o disjuntor.

## Premissas que o memorial declara

- Fator de tensão 1,05 no curto máximo e 0,95 no mínimo; relação X/R = 3 da rede a montante.
- Resistência do condutor a quente ×1,5 no curto mínimo; reatância de 0,08 Ω/km.
- Diâmetros internos de eletroduto de catálogo: a NBR 15465 só fixa o externo.

## Decisões ainda a confirmar com os engenheiros

- Quando o I²t do disjuntor pela classe 3 do Anexo ZA passa de k²S², o item sai como "não comprovado", sem reprovar.
- Corrente nominal mínima de 10 A em iluminação e tomadas de uso geral.
- Na divisão automática: banheiro em circuito próprio e limite de 10 A e 12 pontos por circuito, que são editáveis no Padrão.
