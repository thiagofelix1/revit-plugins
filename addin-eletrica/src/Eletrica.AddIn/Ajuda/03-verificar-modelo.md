# Verificar modelo

O botão **Verificar modelo** (painel Cálculo) confere o modelo pela NBR 5410 e diz, item por item, o que falta ou está fora da norma. Ele só lê o modelo: nada muda. Rode antes de calcular e de novo depois de cada correção.

## Como usar

1. Clique em **Verificar modelo**. O topo da janela resume quantos erros, avisos e notas apareceram.
2. Filtre por gravidade, se quiser ver só os erros.
3. Escolha um item e clique em **Mostrar**: a janela fecha e os elementos ficam selecionados no modelo.
4. Corrija e rode de novo. **Salvar relatório** grava a lista em Documentos\\AutoEletrica\\verificacao, para mandar a alguém.

## Gravidade

| Gravidade | O que significa |
|---|---|
| **Erro** | Fora da norma, ou o cálculo não consegue seguir. Corrija antes de entregar. |
| **Aviso** | Provavelmente errado, ou admitido só em condições que o plugin não confere. Avalie. |
| **Nota** | Informação para o projeto: DR obrigatório, DPS, reserva, equipotencialização. Não é problema. |

## Modelo (M-)

| Código | O que aponta | Como resolver |
|---|---|---|
| M-01 | Ponto sem circuito. | Criar circuitos, ou ponha o ponto num circuito pelo Revit. |
| M-02 | Circuito sem pontos nem potência. | Fica como reserva. Apague ou ponha pontos. |
| M-03 | Ponto sem caminho pelos eletrodutos até o quadro. | Ligue o ponto ao eletroduto (Ligar pontos) e confira eletrodutos desconectados. |
| M-04 | Quadro sem circuitos. | Crie os circuitos ou apague o quadro. |
| M-05 | Quadro terminal sem circuito alimentador. | Alimentar quadro. |

## Ambientes (A-), só em habitação

| Código | O que aponta | Item |
|---|---|---|
| A-01 | Menos pontos de tomada que o mínimo para o uso, o perímetro e a área. | 9.5.2.2.1 |
| A-02 | Cômodo sem ponto de luz fixo comandado por interruptor. | 9.5.2.1.1 |
| A-03 | Ponto de luz fora do teto, onde a parede não é admitida. | 9.5.2.1.1 |
| A-04 | Carga de iluminação abaixo do mínimo para a área. | 9.5.2.1.2 |
| A-05 | Menos de 2 tomadas acima da bancada da pia. | 9.5.2.2.1 |
| A-06 | Tomada com potência abaixo do mínimo do cômodo (600 VA nas áreas molhadas). | 9.5.2.2.2 |
| A-07 | Casa de máquinas sem tomada de uso geral. | 4.2.1.2.3 |
| A-08 | Nota: prever a equipotencialização suplementar do banheiro. | 9.1.3.1.2 |
| A-09 | Volumes do banheiro não apurados: sem boxe, banheira ou chuveiro na arquitetura. | 9.1.2.1 |

## Banheiro (B-)

Os volumes saem da geometria do boxe, da banheira ou do chuveiro da arquitetura: volume 1 sobre a peça até 2,25 m; volume 2 a 0,60 m dela, até 3,00 m; volume 3 a mais 2,40 m, até 2,25 m. Sem boxe, vale 0,60 m ao redor do chuveiro. As paredes não são descontadas.

| Código | O que aponta | Item |
|---|---|---|
| B-01 | Tomada ou equipamento dentro de volume onde não é permitido. | 9.1.4.3.1, 9.1.4.4 |
| B-02 | Chuveiro ou aquecedor ligado por tomada: a ligação deve ser direta. | 9.5.2.3 |
| B-04 | Grau de proteção (IPX) abaixo do mínimo para o volume. | 9.1.4.1 |
| B-05 | Nota: ponto nos volumes 0 a 2 sem o IP informado na família. | 9.1.4.1 |
| B-06 | Ponto do chuveiro elétrico fora do volume 1. | 9.1.4.4 |

## Comandos de iluminação (L-)

| Código | O que aponta | Item |
|---|---|---|
| L-01 | Luminária sem interruptor que a comande. Use Comando. | 9.5.2.1.1 |
| L-02 | Um comando com luminárias em circuitos diferentes. | 5.6.6.1 |
| L-03 | Interruptor num circuito e as luminárias em outro. | 5.6.6.1 |

## Circuitos (C-) e DR

| Código | O que aponta | Item |
|---|---|---|
| C-01 | Circuito que mistura iluminação e tomadas, fora do que a norma admite. | 9.5.3.3, 4.2.5.5 |
| C-02 | Mistura admitida em habitação até 16 A, com condições. Avalie. | 9.5.3.3 |
| C-03 | Equipamento acima de 10 A dividindo o circuito com outros pontos. | 9.5.3.1 |
| C-04 | Tomadas de cozinha ou área de serviço junto com as de outros cômodos. | 9.5.3.2 |
| C-05 | Tomadas de uso geral em fase-fase: marcar a tensão no espelho. | 6.5.3.2 |
| DR-01 | Nota: o circuito exige DR de 30 mA, com o motivo. | 5.1.3.2.2 |
| DR-02 | Nota: pela carga, o DR deve ser do tipo F ou B; o tipo AC não serve. | 5.1.3.2.2 |

## Quadros (Q-)

| Código | O que aponta | Item |
|---|---|---|
| Q-01 | Nota: espaços de reserva a prever no quadro e no alimentador. | 6.5.4.7, Tabela 59 |
| Q-02 | Nota: DPS obrigatório no quadro de entrada, com o motivo. | 5.4.2.1.1 |
| Q-03 | Esquema TN-C na instalação: separar N e PE na entrada. | 5.4.3.6 |
| Q-04 | Nota: advertência padrão nos quadros residenciais. | 6.5.4.10 |

## Eletrodutos (T-), antes do cálculo

| Código | O que aponta | Como resolver |
|---|---|---|
| T-01 | Trecho entre caixas acima de 15 m menos 3 m por curva. | Caixas por regra. |
| T-02 | Mais de três curvas de 90° entre caixas. | Caixas por regra. |
| T-03 | Eletroduto com a ponta solta. | Ligue a ponta ao ponto, caixa ou quadro; confira a tolerância. |
| T-04 | Eletroduto abaixo do DN mínimo. | Troque o diâmetro. |

## Modelo do Revit (R-)

| Código | O que aponta |
|---|---|
| R-01 | Quadro sem sistema de distribuição, ou com sistema diferente dos Dados do projeto. |
| R-02 | Família sem conector elétrico: não entra em circuito nem no cálculo. |
| R-03 | Ponto sem potência: entra no cálculo com 0 VA. |
| R-04 | Ponto do chuveiro a mais de 1,5 m do boxe ou chuveiro da arquitetura. |
| R-05 | Interruptor dentro dos volumes 0, 1 ou 2 do banheiro. |
| R-06 | Nota: interruptor sem comando gravado. |
| R-07 | Nota: de quais peças da arquitetura saíram os volumes de cada banheiro. |

## Depois do cálculo (D- e E-)

Quando o Calcular já rodou, os critérios que não passaram entram no mesmo relatório, cada um com a sugestão do que fazer:

| Código | Critério |
|---|---|
| D-IZ | Capacidade de corrente do condutor. |
| D-IN | Sobrecarga: IB ≤ In ≤ IZ. |
| D-QT | Queda de tensão. |
| D-ICN | Capacidade de interrupção do disjuntor. |
| D-I2T, D-I2TMIN | Solicitação térmica no curto máximo e mínimo. |
| D-ZS | Seccionamento automático. |
| D-SMIN | Seção mínima. |
| E-OCUP | Taxa de ocupação do eletroduto. |
| E-ORIG | Circuitos de quadros diferentes no mesmo eletroduto. |
| E-SEC3 | Seções fora de três valores sucessivos no mesmo eletroduto. |
