# Memorial de cálculo elétrico — Apartamento tipo — 2 dormitórios

> Protótipo do motor AutoEletrica. Resultado **ATENDE** às verificações abaixo. Não substitui a análise e a responsabilidade técnica do projetista. Norma de referência: ABNT NBR 5410:2004 (versão corrigida 2008).

## 1. Dados da alimentação

| Item | Valor | Referência |
|---|---|---|
| Tensões | 127 / 220 V, 2 fase(s) | 4.2.2.1 |
| Esquema de aterramento | TN-C-S | 4.2.2.2 |
| Icc presumida no ponto de entrega | 6,0 kA | 4.2.3 |
| Alimentação | aérea, 40 dias de trovoada/ano (AQ2) | 4.2.6, 5.4.2.1 |
| Temperatura ambiente | 30 °C | 6.2.5.3 |
| Uso | habitação (seção 9.5) | 9.5 |

## 2. Quadros

### QD — Quadro de distribuição

| Grandeza | Valor | Referência |
|---|---|---|
| Circuitos | 8 + 3 de reserva | Tabela 59 |
| Potência instalada / de demanda | 14.940 / 12.446 VA | 4.2.1 |
| Demanda: iluminação e tomadas de uso geral | 6.540 VA × 0,62 = 4.046 VA (28 un.) | prática de projeto e normas de distribuidoras (padrão do motor; conferir com a distribuidora) |
| Demanda: chuveiros e aquecedores | 5.500 VA × 1,00 = 5.500 VA (1 un.) | prática de projeto e normas de distribuidoras (padrão do motor; conferir com a distribuidora) |
| Demanda: ar-condicionado | 1.400 VA × 1,00 = 1.400 VA (1 un.) | prática de projeto e normas de distribuidoras (padrão do motor; conferir com a distribuidora) |
| Demanda: uso específico (TUE, motores, VE, alimentadores) | 1.500 VA × 1,00 = 1.500 VA (1 un.) | prática de projeto e normas de distribuidoras (padrão do motor; conferir com a distribuidora) |
| Corrente de demanda | 49,0 A | 4.2.1 |
| Carga por fase | A: 7.740 VA (60,9 A), B: 7.200 VA (56,7 A); desequilíbrio 7 % | 4.2.5.6 |
| Icc no barramento | 6,30 kA | 5.3.5.1 |
| Disjuntor geral | C50 A, 2P, 7,5 kA | 5.3.4, 6.3.4 |
| DPS | classe I, 3 un., esquema 2+1 (cada fase e o neutro ao PE), Uc ≥ 140 V (comercial 175 V), Up ≤ 1,5 kV, In ≥ 10 kA, Iimp ≥ 12,5 kA, ligações 4 mm² — 5.4.2.1.1 a) — alimentação por linha aérea em região AQ2 (40 dias de trovoada por ano) | 5.4.2.1.1, 6.3.5.2, Tabela 49 |
| Circuitos com DR | 1, 3, 4, 5, 7, 8 | 5.1.3.2.2 |
| DR1 100 A / 30 mA tipo A 4P: 1, 3, 4, 5, 7, 8 | até 6 circuitos por DR; In ≥ soma dos In do grupo | 6.3.3.2, IEC 61008-1 |
| Seccionador geral, barramentos e identificação | disjuntor geral como seccionador; barramento para o In do geral; circuitos identificados e advertência fixada | 5.6.3, 6.5.4.9, 6.5.4.10 |

Quadro de cargas:

| Circ. | Descrição | Ligação | Fase | Potência (VA) | IB (A) | Fase / N / PE (mm²) | Disjuntor | DR | Queda (%) | Situação |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Iluminação | F+N+PE | A | 1.040 | 8,2 | 1,5 / 1,5 / 1,5 | C10 A, 1P, 7,5 kA | 30 mA A | 1,68 | atende |
| 2 | TUG sala, dormitórios, circulação e varanda | F+N+PE | B | 1.200 | 9,4 | 2,5 / 2,5 / 2,5 | C10 A, 1P, 7,5 kA | — | 1,40 | atende |
| 3 | TUG cozinha | F+N+PE | A | 1.900 | 15,0 | 4,0 / 4,0 / 4,0 | C16 A, 1P, 7,5 kA | 30 mA A | 0,98 | atende |
| 4 | TUG área de serviço | F+N+PE | B | 1.800 | 14,2 | 4,0 / 4,0 / 4,0 | C16 A, 1P, 7,5 kA | 30 mA A | 1,19 | atende |
| 5 | Chuveiro elétrico | 2F+PE | AB | 5.500 | 25,0 | 4,0 / 4,0 / 4,0 | B25 A, 2P, 7,5 kA | 30 mA A | 0,67 | atende |
| 6 | Ar-condicionado dormitório 1 | 2F+PE | AB | 1.400 | 6,4 | 2,5 / 2,5 / 2,5 | C8 A, 2P, 7,5 kA | — | 0,33 | atende |
| 7 | Micro-ondas (TUE cozinha) | 2F+PE | AB | 1.500 | 6,8 | 2,5 / 2,5 / 2,5 | C8 A, 2P, 7,5 kA | 30 mA A | 0,39 | atende |
| 8 | TUG banheiro | F+N+PE | A | 600 | 4,7 | 2,5 / 2,5 / 2,5 | C10 A, 1P, 7,5 kA | 30 mA A | 0,57 | atende |

## 3. Circuitos: os seis critérios de 6.2.6.1.2

### Circuito 1 — Iluminação (QD)

1.040 VA, F+N+PE (fase A), IB = **8,2 A**; L = 10,0 m (pelos eletrodutos); PVC 70 °C Cobre, método B1 (trecho crítico QD-circB), 5 circuito(s) agrupado(s). **Fase 1,5 mm², neutro 1,5 mm², PE 1,5 mm², disjuntor C10 A, 1P, 7,5 kA.** Critério que definiu a seção: seção mínima.
DR obrigatório: 5.1.3.2.2 a) — ponto em local com banheira ou chuveiro; IΔn 30 mA, tipo A, In ≥ 16 A.

| Critério | Resultado | Valor | Limite | Item | Como foi calculado |
|---|---|---|---|---|---|
| capacidade de corrente | atende | IB = 8,2 A | IZ = 10,5 A | 6.2.6.1.2 a), 6.2.5, Tabela 36 | Tabela 36, método B1, 2 condutores carregados: 17,5 A × FCT 1,00 × FCA 0,60 (5 circuito(s) no trecho QD-circB) |
| seção mínima | atende | 1,5 mm² | ≥ 1,5 mm² | 6.2.6.1.2 f), Tabela 47 | circuito de iluminação: 1,5 mm² em cobre |
| sobrecarga | atende | IB 8,2 ≤ In 10 ≤ IZ 10,5 A | I2 = 14,5 ≤ 1,45 IZ = 15,2 A | 5.3.4.1, 6.3.4.2, NBR NM 60898 8.6.2.3 | disjuntor C10 A, 1P, 7,5 kA; I2 = 1,45 In atende automaticamente quando In ≤ IZ |
| queda de tensão | atende | 1,68 % no circuito, 1,68 % acumulada | ≤ 4 % no circuito e ≤ 5 % do ponto de entrega | 6.2.6.1.2 e), 6.2.7.1 c), 6.2.7.2 | L = 10,0 m (pelos eletrodutos), R = 13,045 Ω/km a 54 °C, cos φ = 1,00 |
| capacidade de interrupção | atende | Icc presumida no quadro = 6,30 kA | Icn = 7,5 kA | 5.3.5.5 a), 6.3.4.3, NBR NM 60898 5.3.4 | corrente de curto trifásica máxima no barramento do quadro pelo método das impedâncias |
| solicitação térmica no curto | não avaliado | I²t ≤ 84000 A²s (classe 3) | k²S² = 29756 A²s (k = 115) | 5.3.5.5 b), Tabela 30, NBR NM 60898 Anexo ZA | não comprovado: o limite da classe 3 (84000 A²s) passa de k²S²; a curva I²t real do fabricante costuma ficar abaixo. Informe o I²t de catálogo na Icc presumida para comprovar, ou use disjuntor limitador |
| seccionamento automático | atende | Zs = 0,352 Ω, Ikmín = 343 A | Ia = 100 A (curva C, 10 × In); Zs · Ia = 35 ≤ Uo = 127 V | 5.1.2.2.4.2, Tabela 25 | esquema TNCS: o disjuntor deve atuar em até 0,8 s na falta fase-PE no fim do circuito; aqui pela atuação instantânea (< 0,1 s) |

### Circuito 2 — TUG sala, dormitórios, circulação e varanda (QD)

1.200 VA, F+N+PE (fase B), IB = **9,4 A**; L = 12,5 m (pelos eletrodutos); PVC 70 °C Cobre, método B1 (trecho crítico QD-circA), 3 circuito(s) agrupado(s). **Fase 2,5 mm², neutro 2,5 mm², PE 2,5 mm², disjuntor C10 A, 1P, 7,5 kA.** Critério que definiu a seção: seção mínima.

| Critério | Resultado | Valor | Limite | Item | Como foi calculado |
|---|---|---|---|---|---|
| capacidade de corrente | atende | IB = 9,4 A | IZ = 16,8 A | 6.2.6.1.2 a), 6.2.5, Tabela 36 | Tabela 36, método B1, 2 condutores carregados: 24,0 A × FCT 1,00 × FCA 0,70 (3 circuito(s) no trecho QD-circA) |
| seção mínima | atende | 2,5 mm² | ≥ 2,5 mm² | 6.2.6.1.2 f), Tabela 47 | circuito de força (tomadas inclusive): 2,5 mm² em cobre |
| sobrecarga | atende | IB 9,4 ≤ In 10 ≤ IZ 16,8 A | I2 = 14,5 ≤ 1,45 IZ = 24,4 A | 5.3.4.1, 6.3.4.2, NBR NM 60898 8.6.2.3 | disjuntor C10 A, 1P, 7,5 kA; I2 = 1,45 In atende automaticamente quando In ≤ IZ |
| queda de tensão | atende | 1,40 % no circuito, 1,40 % acumulada | ≤ 4 % no circuito e ≤ 5 % do ponto de entrega | 6.2.6.1.2 e), 6.2.7.1 c), 6.2.7.2 | L = 12,5 m (pelos eletrodutos), R = 7,510 Ω/km a 43 °C, cos φ = 1,00 |
| capacidade de interrupção | atende | Icc presumida no quadro = 6,30 kA | Icn = 7,5 kA | 5.3.5.5 a), 6.3.4.3, NBR NM 60898 5.3.4 | corrente de curto trifásica máxima no barramento do quadro pelo método das impedâncias |
| solicitação térmica no curto | não avaliado | I²t ≤ 84000 A²s (classe 3) | k²S² = 82656 A²s (k = 115) | 5.3.5.5 b), Tabela 30, NBR NM 60898 Anexo ZA | não comprovado: o limite da classe 3 (84000 A²s) passa de k²S²; a curva I²t real do fabricante costuma ficar abaixo. Informe o I²t de catálogo na Icc presumida para comprovar, ou use disjuntor limitador |
| seccionamento automático | atende | Zs = 0,266 Ω, Ikmín = 453 A | Ia = 100 A (curva C, 10 × In); Zs · Ia = 27 ≤ Uo = 127 V | 5.1.2.2.4.2, Tabela 25 | esquema TNCS: o disjuntor deve atuar em até 0,8 s na falta fase-PE no fim do circuito; aqui pela atuação instantânea (< 0,1 s) |

### Circuito 3 — TUG cozinha (QD)

1.900 VA, F+N+PE (fase A), IB = **15,0 A**; L = 8,5 m (pelos eletrodutos); PVC 70 °C Cobre, método B1 (trecho crítico QD-circB), 5 circuito(s) agrupado(s). **Fase 4,0 mm², neutro 4,0 mm², PE 4,0 mm², disjuntor C16 A, 1P, 7,5 kA.** Critério que definiu a seção: capacidade de corrente.
DR obrigatório: 5.1.3.2.2 d) — ponto em cozinha, copa, área de serviço, lavanderia, garagem ou local molhado de habitação; IΔn 30 mA, tipo A, In ≥ 16 A.

| Critério | Resultado | Valor | Limite | Item | Como foi calculado |
|---|---|---|---|---|---|
| capacidade de corrente | atende | IB = 15,0 A | IZ = 19,2 A | 6.2.6.1.2 a), 6.2.5, Tabela 36 | Tabela 36, método B1, 2 condutores carregados: 32,0 A × FCT 1,00 × FCA 0,60 (5 circuito(s) no trecho QD-circB) |
| seção mínima | atende | 4 mm² | ≥ 2,5 mm² | 6.2.6.1.2 f), Tabela 47 | circuito de força (tomadas inclusive): 2,5 mm² em cobre |
| sobrecarga | atende | IB 15,0 ≤ In 16 ≤ IZ 19,2 A | I2 = 23,2 ≤ 1,45 IZ = 27,8 A | 5.3.4.1, 6.3.4.2, NBR NM 60898 8.6.2.3 | disjuntor C16 A, 1P, 7,5 kA; I2 = 1,45 In atende automaticamente quando In ≤ IZ |
| queda de tensão | atende | 0,98 % no circuito, 0,98 % acumulada | ≤ 4 % no circuito e ≤ 5 % do ponto de entrega | 6.2.6.1.2 e), 6.2.7.1 c), 6.2.7.2 | L = 8,5 m (pelos eletrodutos), R = 4,891 Ω/km a 54 °C, cos φ = 1,00 |
| capacidade de interrupção | atende | Icc presumida no quadro = 6,30 kA | Icn = 7,5 kA | 5.3.5.5 a), 6.3.4.3, NBR NM 60898 5.3.4 | corrente de curto trifásica máxima no barramento do quadro pelo método das impedâncias |
| solicitação térmica no curto | atende | I²t ≤ 84000 A²s (classe 3) | k²S² = 211600 A²s (k = 115) | 5.3.5.5 b), Tabela 30, NBR NM 60898 Anexo ZA | limite da classe 3 de limitação de energia, curva C, In 16 A, Icn 10 kA (Tabela ZA): qualquer disjuntor classe 3 protege o condutor |
| seccionamento automático | atende | Zs = 0,119 Ω, Ikmín = 1018 A | Ia = 160 A (curva C, 10 × In); Zs · Ia = 19 ≤ Uo = 127 V | 5.1.2.2.4.2, Tabela 25 | esquema TNCS: o disjuntor deve atuar em até 0,8 s na falta fase-PE no fim do circuito; aqui pela atuação instantânea (< 0,1 s) |

### Circuito 4 — TUG área de serviço (QD)

1.800 VA, F+N+PE (fase B), IB = **14,2 A**; L = 11,0 m (pelos eletrodutos); PVC 70 °C Cobre, método B1 (trecho crítico QD-circB), 5 circuito(s) agrupado(s). **Fase 4,0 mm², neutro 4,0 mm², PE 4,0 mm², disjuntor C16 A, 1P, 7,5 kA.** Critério que definiu a seção: sobrecarga.
DR obrigatório: 5.1.3.2.2 d) — ponto em cozinha, copa, área de serviço, lavanderia, garagem ou local molhado de habitação; IΔn 30 mA, tipo A, In ≥ 16 A.

| Critério | Resultado | Valor | Limite | Item | Como foi calculado |
|---|---|---|---|---|---|
| capacidade de corrente | atende | IB = 14,2 A | IZ = 19,2 A | 6.2.6.1.2 a), 6.2.5, Tabela 36 | Tabela 36, método B1, 2 condutores carregados: 32,0 A × FCT 1,00 × FCA 0,60 (5 circuito(s) no trecho QD-circB) |
| seção mínima | atende | 4 mm² | ≥ 2,5 mm² | 6.2.6.1.2 f), Tabela 47 | circuito de força (tomadas inclusive): 2,5 mm² em cobre |
| sobrecarga | atende | IB 14,2 ≤ In 16 ≤ IZ 19,2 A | I2 = 23,2 ≤ 1,45 IZ = 27,8 A | 5.3.4.1, 6.3.4.2, NBR NM 60898 8.6.2.3 | disjuntor C16 A, 1P, 7,5 kA; I2 = 1,45 In atende automaticamente quando In ≤ IZ |
| queda de tensão | atende | 1,19 % no circuito, 1,19 % acumulada | ≤ 4 % no circuito e ≤ 5 % do ponto de entrega | 6.2.6.1.2 e), 6.2.7.1 c), 6.2.7.2 | L = 11,0 m (pelos eletrodutos), R = 4,849 Ω/km a 52 °C, cos φ = 1,00 |
| capacidade de interrupção | atende | Icc presumida no quadro = 6,30 kA | Icn = 7,5 kA | 5.3.5.5 a), 6.3.4.3, NBR NM 60898 5.3.4 | corrente de curto trifásica máxima no barramento do quadro pelo método das impedâncias |
| solicitação térmica no curto | atende | I²t ≤ 84000 A²s (classe 3) | k²S² = 211600 A²s (k = 115) | 5.3.5.5 b), Tabela 30, NBR NM 60898 Anexo ZA | limite da classe 3 de limitação de energia, curva C, In 16 A, Icn 10 kA (Tabela ZA): qualquer disjuntor classe 3 protege o condutor |
| seccionamento automático | atende | Zs = 0,151 Ω, Ikmín = 802 A | Ia = 160 A (curva C, 10 × In); Zs · Ia = 24 ≤ Uo = 127 V | 5.1.2.2.4.2, Tabela 25 | esquema TNCS: o disjuntor deve atuar em até 0,8 s na falta fase-PE no fim do circuito; aqui pela atuação instantânea (< 0,1 s) |

### Circuito 5 — Chuveiro elétrico (QD)

5.500 VA, 2F+PE (fase AB), IB = **25,0 A**; L = 6,0 m (pelos eletrodutos); PVC 70 °C Cobre, método B1 (trecho crítico QD-bwc), 1 circuito(s) agrupado(s). **Fase 4,0 mm², neutro 4,0 mm², PE 4,0 mm², disjuntor B25 A, 2P, 7,5 kA.** Critério que definiu a seção: capacidade de corrente.
DR obrigatório: 5.1.3.2.2 a) — ponto em local com banheira ou chuveiro; IΔn 30 mA, tipo A, In ≥ 25 A.

| Critério | Resultado | Valor | Limite | Item | Como foi calculado |
|---|---|---|---|---|---|
| capacidade de corrente | atende | IB = 25,0 A | IZ = 32,0 A | 6.2.6.1.2 a), 6.2.5, Tabela 36 | Tabela 36, método B1, 2 condutores carregados: 32,0 A × FCT 1,00 × FCA 1,00 (1 circuito(s) no trecho QD-bwc) |
| seção mínima | atende | 4 mm² | ≥ 2,5 mm² | 6.2.6.1.2 f), Tabela 47 | circuito de força (tomadas inclusive): 2,5 mm² em cobre |
| sobrecarga | atende | IB 25,0 ≤ In 25 ≤ IZ 32,0 A | I2 = 36,3 ≤ 1,45 IZ = 46,4 A | 5.3.4.1, 6.3.4.2, NBR NM 60898 8.6.2.3 | disjuntor B25 A, 2P, 7,5 kA; I2 = 1,45 In atende automaticamente quando In ≤ IZ |
| queda de tensão | atende | 0,67 % no circuito, 0,67 % acumulada | ≤ 4 % no circuito e ≤ 5 % do ponto de entrega | 6.2.6.1.2 e), 6.2.7.1 c), 6.2.7.2 | L = 6,0 m (pelos eletrodutos), R = 4,893 Ω/km a 54 °C, cos φ = 1,00 |
| capacidade de interrupção | atende | Icc presumida no quadro = 6,30 kA | Icn = 7,5 kA | 5.3.5.5 a), 6.3.4.3, NBR NM 60898 5.3.4 | corrente de curto trifásica máxima no barramento do quadro pelo método das impedâncias |
| solicitação térmica no curto | atende | I²t ≤ 90000 A²s (classe 3) | k²S² = 211600 A²s (k = 115) | 5.3.5.5 b), Tabela 30, NBR NM 60898 Anexo ZA | limite da classe 3 de limitação de energia, curva B, In 25 A, Icn 10 kA (Tabela ZA): qualquer disjuntor classe 3 protege o condutor |
| seccionamento automático | atende | Zs = 0,087 Ω, Ikmín = 1389 A | Ia = 125 A (curva B, 5 × In); Zs · Ia = 11 ≤ Uo = 127 V | 5.1.2.2.4.2, Tabela 25 | esquema TNCS: o disjuntor deve atuar em até 0,8 s na falta fase-PE no fim do circuito; aqui pela atuação instantânea (< 0,1 s) |

### Circuito 6 — Ar-condicionado dormitório 1 (QD)

1.400 VA, 2F+PE (fase AB), IB = **6,4 A**; L = 9,0 m (pelos eletrodutos); PVC 70 °C Cobre, método B1 (trecho crítico QD-circA), 3 circuito(s) agrupado(s). **Fase 2,5 mm², neutro 2,5 mm², PE 2,5 mm², disjuntor C8 A, 2P, 7,5 kA.** Critério que definiu a seção: seção mínima.

| Critério | Resultado | Valor | Limite | Item | Como foi calculado |
|---|---|---|---|---|---|
| capacidade de corrente | atende | IB = 6,4 A | IZ = 16,8 A | 6.2.6.1.2 a), 6.2.5, Tabela 36 | Tabela 36, método B1, 2 condutores carregados: 24,0 A × FCT 1,00 × FCA 0,70 (3 circuito(s) no trecho QD-circA) |
| seção mínima | atende | 2,5 mm² | ≥ 2,5 mm² | 6.2.6.1.2 f), Tabela 47 | circuito de força (tomadas inclusive): 2,5 mm² em cobre |
| sobrecarga | atende | IB 6,4 ≤ In 8 ≤ IZ 16,8 A | I2 = 11,6 ≤ 1,45 IZ = 24,4 A | 5.3.4.1, 6.3.4.2, NBR NM 60898 8.6.2.3 | disjuntor C8 A, 2P, 7,5 kA; I2 = 1,45 In atende automaticamente quando In ≤ IZ |
| queda de tensão | atende | 0,33 % no circuito, 0,33 % acumulada | ≤ 4 % no circuito e ≤ 5 % do ponto de entrega | 6.2.6.1.2 e), 6.2.7.1 c), 6.2.7.2 | L = 9,0 m (pelos eletrodutos), R = 7,323 Ω/km a 36 °C, cos φ = 0,85 |
| capacidade de interrupção | atende | Icc presumida no quadro = 6,30 kA | Icn = 7,5 kA | 5.3.5.5 a), 6.3.4.3, NBR NM 60898 5.3.4 | corrente de curto trifásica máxima no barramento do quadro pelo método das impedâncias |
| solicitação térmica no curto | não avaliado | I²t ≤ 84000 A²s (classe 3) | k²S² = 82656 A²s (k = 115) | 5.3.5.5 b), Tabela 30, NBR NM 60898 Anexo ZA | não comprovado: o limite da classe 3 (84000 A²s) passa de k²S²; a curva I²t real do fabricante costuma ficar abaixo. Informe o I²t de catálogo na Icc presumida para comprovar, ou use disjuntor limitador |
| seccionamento automático | atende | Zs = 0,194 Ω, Ikmín = 622 A | Ia = 80 A (curva C, 10 × In); Zs · Ia = 16 ≤ Uo = 127 V | 5.1.2.2.4.2, Tabela 25 | esquema TNCS: o disjuntor deve atuar em até 0,8 s na falta fase-PE no fim do circuito; aqui pela atuação instantânea (< 0,1 s) |

### Circuito 7 — Micro-ondas (TUE cozinha) (QD)

1.500 VA, 2F+PE (fase AB), IB = **6,8 A**; L = 8,5 m (pelos eletrodutos); PVC 70 °C Cobre, método B1 (trecho crítico QD-circB), 5 circuito(s) agrupado(s). **Fase 2,5 mm², neutro 2,5 mm², PE 2,5 mm², disjuntor C8 A, 2P, 7,5 kA.** Critério que definiu a seção: seção mínima.
DR obrigatório: 5.1.3.2.2 d) — ponto em cozinha, copa, área de serviço, lavanderia, garagem ou local molhado de habitação; IΔn 30 mA, tipo A, In ≥ 16 A.

| Critério | Resultado | Valor | Limite | Item | Como foi calculado |
|---|---|---|---|---|---|
| capacidade de corrente | atende | IB = 6,8 A | IZ = 14,4 A | 6.2.6.1.2 a), 6.2.5, Tabela 36 | Tabela 36, método B1, 2 condutores carregados: 24,0 A × FCT 1,00 × FCA 0,60 (5 circuito(s) no trecho QD-circB) |
| seção mínima | atende | 2,5 mm² | ≥ 2,5 mm² | 6.2.6.1.2 f), Tabela 47 | circuito de força (tomadas inclusive): 2,5 mm² em cobre |
| sobrecarga | atende | IB 6,8 ≤ In 8 ≤ IZ 14,4 A | I2 = 11,6 ≤ 1,45 IZ = 20,9 A | 5.3.4.1, 6.3.4.2, NBR NM 60898 8.6.2.3 | disjuntor C8 A, 2P, 7,5 kA; I2 = 1,45 In atende automaticamente quando In ≤ IZ |
| queda de tensão | atende | 0,39 % no circuito, 0,39 % acumulada | ≤ 4 % no circuito e ≤ 5 % do ponto de entrega | 6.2.6.1.2 e), 6.2.7.1 c), 6.2.7.2 | L = 8,5 m (pelos eletrodutos), R = 7,410 Ω/km a 39 °C, cos φ = 1,00 |
| capacidade de interrupção | atende | Icc presumida no quadro = 6,30 kA | Icn = 7,5 kA | 5.3.5.5 a), 6.3.4.3, NBR NM 60898 5.3.4 | corrente de curto trifásica máxima no barramento do quadro pelo método das impedâncias |
| solicitação térmica no curto | não avaliado | I²t ≤ 84000 A²s (classe 3) | k²S² = 82656 A²s (k = 115) | 5.3.5.5 b), Tabela 30, NBR NM 60898 Anexo ZA | não comprovado: o limite da classe 3 (84000 A²s) passa de k²S²; a curva I²t real do fabricante costuma ficar abaixo. Informe o I²t de catálogo na Icc presumida para comprovar, ou use disjuntor limitador |
| seccionamento automático | atende | Zs = 0,184 Ω, Ikmín = 656 A | Ia = 80 A (curva C, 10 × In); Zs · Ia = 15 ≤ Uo = 127 V | 5.1.2.2.4.2, Tabela 25 | esquema TNCS: o disjuntor deve atuar em até 0,8 s na falta fase-PE no fim do circuito; aqui pela atuação instantânea (< 0,1 s) |

### Circuito 8 — TUG banheiro (QD)

600 VA, F+N+PE (fase A), IB = **4,7 A**; L = 10,5 m (pelos eletrodutos); PVC 70 °C Cobre, método B1 (trecho crítico QD-circB), 5 circuito(s) agrupado(s). **Fase 2,5 mm², neutro 2,5 mm², PE 2,5 mm², disjuntor C10 A, 1P, 7,5 kA.** Critério que definiu a seção: seção mínima.
DR obrigatório: 5.1.3.2.2 a) — ponto em local com banheira ou chuveiro; IΔn 30 mA, tipo A, In ≥ 16 A.

| Critério | Resultado | Valor | Limite | Item | Como foi calculado |
|---|---|---|---|---|---|
| capacidade de corrente | atende | IB = 4,7 A | IZ = 14,4 A | 6.2.6.1.2 a), 6.2.5, Tabela 36 | Tabela 36, método B1, 2 condutores carregados: 24,0 A × FCT 1,00 × FCA 0,60 (5 circuito(s) no trecho QD-circB) |
| seção mínima | atende | 2,5 mm² | ≥ 2,5 mm² | 6.2.6.1.2 f), Tabela 47 | circuito de força (tomadas inclusive): 2,5 mm² em cobre |
| sobrecarga | atende | IB 4,7 ≤ In 10 ≤ IZ 14,4 A | I2 = 14,5 ≤ 1,45 IZ = 20,9 A | 5.3.4.1, 6.3.4.2, NBR NM 60898 8.6.2.3 | disjuntor C10 A, 1P, 7,5 kA; I2 = 1,45 In atende automaticamente quando In ≤ IZ |
| queda de tensão | atende | 0,57 % no circuito, 0,57 % acumulada | ≤ 4 % no circuito e ≤ 5 % do ponto de entrega | 6.2.6.1.2 e), 6.2.7.1 c), 6.2.7.2 | L = 10,5 m (pelos eletrodutos), R = 7,284 Ω/km a 34 °C, cos φ = 1,00 |
| capacidade de interrupção | atende | Icc presumida no quadro = 6,30 kA | Icn = 7,5 kA | 5.3.5.5 a), 6.3.4.3, NBR NM 60898 5.3.4 | corrente de curto trifásica máxima no barramento do quadro pelo método das impedâncias |
| solicitação térmica no curto | não avaliado | I²t ≤ 84000 A²s (classe 3) | k²S² = 82656 A²s (k = 115) | 5.3.5.5 b), Tabela 30, NBR NM 60898 Anexo ZA | não comprovado: o limite da classe 3 (84000 A²s) passa de k²S²; a curva I²t real do fabricante costuma ficar abaixo. Informe o I²t de catálogo na Icc presumida para comprovar, ou use disjuntor limitador |
| seccionamento automático | atende | Zs = 0,225 Ω, Ikmín = 536 A | Ia = 100 A (curva C, 10 × In); Zs · Ia = 22 ≤ Uo = 127 V | 5.1.2.2.4.2, Tabela 25 | esquema TNCS: o disjuntor deve atuar em até 0,8 s na falta fase-PE no fim do circuito; aqui pela atuação instantânea (< 0,1 s) |

## 4. Eletrodutos (6.2.11.1)

| Trecho | De → Para | L (m) | Curvas | Condutores | Área (mm²) | DN | Taxa | DN mínimo | Situação |
|---|---|---|---|---|---|---|---|---|---|
| QD-circA | QD → cxA-circ | 2,0 | 1 | 9 | 106 | 25 | 36 % (≤ 40 %) | 25 | atende |
| QD-circB | QD → cxB-circ | 2,0 | 1 | 15 | 215 | 40 | 26 % (≤ 40 %) | 40 | atende |
| QD-bwc | QD → CH | 6,0 | 2 | 3 | 54 | 20 | 31 % (≤ 40 %) | 20 | atende |
| circ-sala | cxA-circ → cx-sala | 3,0 | 1 | 6 | 67 | 20 | 38 % (≤ 40 %) | 20 | atende |
| circ-d1 | cxA-circ → cx-d1 | 4,0 | 1 | 9 | 106 | 25 | 36 % (≤ 40 %) | 25 | atende |
| d1-d2 | cx-d1 → cx-d2 | 3,5 | 1 | 6 | 67 | 20 | 38 % (≤ 40 %) | 20 | atende |
| sala-var | cx-sala → cx-var | 4,5 | 1 | 6 | 67 | 20 | 38 % (≤ 40 %) | 20 | atende |
| circ-coz | cxB-circ → cx-coz | 3,5 | 1 | 15 | 215 | 40 | 26 % (≤ 40 %) | 40 | atende |
| coz-as | cx-coz → cx-as | 2,5 | 1 | 6 | 82 | 25 | 27 % (≤ 40 %) | 25 | atende |
| coz-bwc | cx-coz → cx-bwc | 2,0 | 0 | 6 | 67 | 20 | 38 % (≤ 40 %) | 20 | atende |
| circA-circ | cxA-circ → cx-circ | 0,5 | 0 | 6 | 67 | 20 | 38 % (≤ 40 %) | 20 | atende |
| desc-L-sala | cx-sala → L-sala | 0,5 | 0 | 3 | 27 | 20 | 15 % (≤ 40 %) | 16 | atende |
| desc-L-coz | cx-coz → L-coz | 0,5 | 0 | 3 | 27 | 20 | 15 % (≤ 40 %) | 16 | atende |
| desc-L-as | cx-as → L-as | 0,5 | 0 | 3 | 27 | 20 | 15 % (≤ 40 %) | 16 | atende |
| desc-L-bwc | cx-bwc → L-bwc | 0,5 | 0 | 3 | 27 | 20 | 15 % (≤ 40 %) | 16 | atende |
| desc-L-d1 | cx-d1 → L-d1 | 0,5 | 0 | 3 | 27 | 20 | 15 % (≤ 40 %) | 16 | atende |
| desc-L-d2 | cx-d2 → L-d2 | 0,5 | 0 | 3 | 27 | 20 | 15 % (≤ 40 %) | 16 | atende |
| desc-L-circ | cx-circ → L-circ | 0,5 | 0 | 3 | 27 | 20 | 15 % (≤ 40 %) | 16 | atende |
| desc-L-var | cx-var → L-var | 0,5 | 0 | 3 | 27 | 20 | 15 % (≤ 40 %) | 16 | atende |
| desc-T-sala-1 | cx-sala → T-sala-1 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-sala-2 | cx-sala → T-sala-2 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-sala-3 | cx-sala → T-sala-3 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-sala-4 | cx-sala → T-sala-4 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-d1-1 | cx-d1 → T-d1-1 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-d1-2 | cx-d1 → T-d1-2 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-d1-3 | cx-d1 → T-d1-3 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-d2-1 | cx-d2 → T-d2-1 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-d2-2 | cx-d2 → T-d2-2 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-d2-3 | cx-d2 → T-d2-3 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-circ | cx-circ → T-circ | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-var | cx-var → T-var | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-T-coz-1 | cx-coz → T-coz-1 | 3,0 | 1 | 3 | 54 | 20 | 31 % (≤ 40 %) | 20 | atende |
| desc-T-coz-2 | cx-coz → T-coz-2 | 3,0 | 1 | 3 | 54 | 20 | 31 % (≤ 40 %) | 20 | atende |
| desc-T-coz-3 | cx-coz → T-coz-3 | 3,0 | 1 | 3 | 54 | 20 | 31 % (≤ 40 %) | 20 | atende |
| desc-T-coz-4 | cx-coz → T-coz-4 | 3,0 | 1 | 3 | 54 | 20 | 31 % (≤ 40 %) | 20 | atende |
| desc-T-as-1 | cx-as → T-as-1 | 3,0 | 1 | 3 | 54 | 20 | 31 % (≤ 40 %) | 20 | atende |
| desc-T-as-2 | cx-as → T-as-2 | 3,0 | 1 | 3 | 54 | 20 | 31 % (≤ 40 %) | 20 | atende |
| desc-T-as-3 | cx-as → T-as-3 | 3,0 | 1 | 3 | 54 | 20 | 31 % (≤ 40 %) | 20 | atende |
| desc-T-bwc | cx-bwc → T-bwc | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-AC-d1 | cx-d1 → AC-d1 | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |
| desc-MO | cx-coz → MO | 3,0 | 1 | 3 | 40 | 20 | 22 % (≤ 40 %) | 16 | atende |

## 5. Verificações

| Código | Gravidade | Mensagem | Item | Elementos |
|---|---|---|---|---|
| A-08 | Nota | Banheiro: prever a equipotencialização suplementar reunindo as massas e os elementos condutivos dos volumes 0 a 3 (barra no banheiro ou ligação ao PE do quadro). | 9.1.3.1.2 | bwc |
| B-05 | Nota | Banheiro: Chuveiro (CH) no volume 1 sem grau IP informado na família; o mínimo é IPX4. | 9.1.4.1 | CH |
| DR-01 | Nota | Circuito 1 exige DR de 30 mA: 5.1.3.2.2 a) — ponto em local com banheira ou chuveiro (1 ponto(s)). | 5.1.3.2.2 | C1, L-bwc |
| DR-01 | Nota | Circuito 3 exige DR de 30 mA: 5.1.3.2.2 d) — ponto em cozinha, copa, área de serviço, lavanderia, garagem ou local molhado de habitação (4 ponto(s)). | 5.1.3.2.2 | C3, T-coz-1, T-coz-2, T-coz-3, T-coz-4 |
| DR-01 | Nota | Circuito 4 exige DR de 30 mA: 5.1.3.2.2 d) — ponto em cozinha, copa, área de serviço, lavanderia, garagem ou local molhado de habitação (3 ponto(s)). | 5.1.3.2.2 | C4, T-as-1, T-as-2, T-as-3 |
| DR-01 | Nota | Circuito 5 exige DR de 30 mA: 5.1.3.2.2 a) — ponto em local com banheira ou chuveiro (1 ponto(s)). | 5.1.3.2.2 | C5, CH |
| DR-01 | Nota | Circuito 7 exige DR de 30 mA: 5.1.3.2.2 d) — ponto em cozinha, copa, área de serviço, lavanderia, garagem ou local molhado de habitação (1 ponto(s)). | 5.1.3.2.2 | C7, MO |
| DR-01 | Nota | Circuito 8 exige DR de 30 mA: 5.1.3.2.2 a) — ponto em local com banheira ou chuveiro (1 ponto(s)). | 5.1.3.2.2 | C8, T-bwc |
| DR-02 | Nota | Circuito 6: pela carga (ArCondicionado), o DR deve ser do tipo F (correntes com componente contínua ou de alta frequência); tipo AC não serve. | 6.3.3.2, IEC 62423 | C6 |
| Q-01 | Nota | Quadro QD — Quadro de distribuição: 8 circuito(s); prever 3 espaço(s) de reserva e considerar a reserva no alimentador. | 6.5.4.7, Tabela 59 | QD |
| Q-02 | Nota | DPS obrigatório no quadro de entrada: 5.4.2.1.1 a) — alimentação por linha aérea em região AQ2 (40 dias de trovoada por ano). | 5.4.2.1.1, 6.3.5.2 | QD |
| Q-04 | Nota | Quadros residenciais saem com a advertência padrão (não trocar disjuntor por maior; não desativar o DR). | 6.5.4.10 | QD |

## 6. Premissas do cálculo

- Capacidade de corrente pelas Tabelas 36 a 39, fatores de temperatura (Tabela 40) e de agrupamento em feixe (Tabela 42, linha 1) aplicados no pior trecho do traçado (6.2.5.8).
- Disjuntores pela ABNT NBR NM 60898: In preferenciais, I2 = 1,45 In, atuação instantânea garantida no limite superior da faixa da curva (B 5 In, C 10 In, D 20 In).
- Queda de tensão com a resistência do condutor na temperatura de operação estimada (IEC 60364-5-52, Anexo G) e reatância de 0,08 Ω/km.
- Curto-circuito pelo método das impedâncias: rede a montante pela Icc presumida com X/R = 3, fator de tensão 1,05 (máximo) e 0,95 (mínimo), resistências a quente ×1,5 no curto mínimo.
- Energia passante do disjuntor: classe 3 de limitação (NBR NM 60898, Anexo ZA) quando a combinação existe; quando o limite da classe passa de k²S², o critério fica "não comprovado" até o I²t de catálogo ser informado.
- Demanda dos quadros: fatores por tipo de carga (prática de projeto e normas de distribuidoras (padrão do motor; conferir com a distribuidora)); fases balanceadas por potência decrescente, respeitando as fases impostas.
- Em eletroduto com seções fora de três valores sucessivos, o agrupamento usa 1/√n (6.2.5.5.5) numa segunda passada; sem seção única que atenda, dois condutores em paralelo por fase (6.2.5.7).
- Diâmetros externos dos condutores isolados pela NBR NM 247-3 (Tabelas 1 e 3); diâmetros internos dos eletrodutos de catálogo (a NBR 15465 só fixa o externo).
- Gerado em 06/11/2026 10:19 em 87 ms.
