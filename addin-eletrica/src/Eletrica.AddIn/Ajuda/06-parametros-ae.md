# Parâmetros AE

O botão **Parâmetros** cria estes parâmetros compartilhados no projeto. O plugin os preenche; as etiquetas, tabelas e filtros do escritório podem usá-los. Não edite à mão os que o plugin grava: o próximo Passar circuitos ou Calcular escreve por cima. Os que você pode editar estão marcados.

## Eletrodutos

| Parâmetro | Quem grava | Conteúdo |
|---|---|---|
| AE Circuitos | Passar circuitos | Os circuitos que passam no trecho, por exemplo C1, C3, C7. |
| AE Condutores | Passar circuitos | Número de condutores no trecho, com retornos e PE. |
| AE Retornos | Passar circuitos | Os retornos dos comandos que passam no trecho. |
| AE Fiação | Passar circuitos, Calcular | O texto da etiqueta, por exemplo 5 cond. C1 +2R; depois do cálculo, com as seções. |
| AE DN sugerido | Calcular | O DN que a ocupação pede. Negativo quer dizer repetir esse número de eletrodutos. |
| AE Ocupação (%) | Calcular | Taxa de ocupação do trecho. |
| AE Forçar circuitos | Fiação manual (editável) | Circuitos que devem passar por este eletroduto. |
| AE Evitar circuitos | Fiação manual (editável) | Circuitos que não podem passar por este eletroduto. |

## Circuitos

| Parâmetro | Quem grava | Conteúdo |
|---|---|---|
| AE Seção (F/N/PE mm²) | Calcular | Seções de fase, neutro e PE. |
| AE Disjuntor | Calcular | Curva, corrente nominal, polos e capacidade de interrupção, por exemplo C16 A, 1P, 6 kA. |
| AE IB (A) | Calcular | Corrente de projeto. |
| AE Queda (%) | Calcular | Queda de tensão acumulada no fim do circuito. |
| AE DR | Calcular | Sensibilidade e tipo do DR, por exemplo 30 mA tipo A; um traço quando o circuito não exige DR. |
| AE Situação | Calcular | atende, ou NÃO ATENDE com os critérios que não passaram. |
| AE Comprimento (m) | Calcular | Comprimento real pelos eletrodutos. |
| AE Tipo de carga | Criar circuitos (editável) | Iluminação, tomadas de uso geral, uso específico, chuveiro, ar-condicionado... |
| AE Fase | Calcular | A fase ou as fases do circuito, pelo balanceamento. |

## Pontos

| Parâmetro | Quem grava | Conteúdo |
|---|---|---|
| AE Ambiente | Lançamento, Calcular | O ambiente onde o ponto está. |
| AE Potência (VA) | Lançamento (editável) | A potência que o cálculo usa. |
| AE Tipo do ponto | Lançamento (editável) | O tipo de carga do ponto. |
| AE Altura (m) | Lançamento (editável) | Altura de instalação. |
| AE Posição | Lançamento (editável) | Baixa, média, alta, interruptor, teto ou piso. |
| AE Comando | Comando | O comando do interruptor ou da luminária, por exemplo S1; nos interruptores, com a ordem, S1#1. |
| AE Tipo de comando | Comando | Simples, paralelo ou intermediário. |

## Para as etiquetas do escritório

- Etiqueta de eletroduto e de fio: rótulo em **AE Fiação**.
- Etiqueta de ponto: **AE Ambiente**, **AE Potência (VA)** e o Circuit Number do Revit.
- Etiqueta de circuito ou tabela de circuitos: **AE Seção**, **AE Disjuntor**, **AE DR**, **AE Situação**.

O Etiquetar prefere os tipos de etiqueta com "AE" no nome.
