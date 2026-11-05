# Base de regras: o que o AutoEletrica verifica e aplica

Lista das regras da ABNT NBR 5410:2004 que viram código, agrupadas pela ferramenta que as usa. Cada regra cita o item da norma; os valores numéricos ficam na base de dados do motor com item e edição, e o texto da norma não é reproduzido. O mapa completo, com explicação e página, está em `nr/normas/nbr-5410-2004.md` (fora do Git).

Convenção de identificação: `E-` verificação de elétrica, numerada por grupo. O relatório do Verificar modelo e o memorial citam o código e o item.

## 1. Pontos por ambiente (Verificar modelo, Gerar pontos)

| Código | Regra | Item | Entrada no modelo |
|---|---|---|---|
| E-P01 | Pelo menos um ponto de luz fixo no teto com interruptor em cada cômodo de habitação | 9.5.2.1.1 | Room/Space + luminárias (`OST_LightingFixtures`) + interruptores (`OST_LightingDevices`) |
| E-P02 | Carga mínima de iluminação: 100 VA até 6 m², +60 VA por 4 m² inteiros | 9.5.2.1.2 | `Room.Area` |
| E-P03 | Tomadas mínimas por perímetro: 1/3,5 m em cozinha, copa, área de serviço, lavanderia; 2 sobre a pia; 1/5 m em sala, quarto e cômodos > 6 m²; 1 em banheiro, varanda e cômodos ≤ 6 m² | 9.5.2.2.1 | `Room.Perimeter`, nome ou uso do ambiente, tomadas (`OST_ElectricalFixtures`) |
| E-P04 | Potência mínima por ponto de tomada: 600 VA × 3 + 100 VA em ambientes molhados; 100 VA nos demais | 9.5.2.2.2 | Parâmetro de carga da família |
| E-P05 | TUE a até 1,5 m do equipamento; 1 TUG com 1 000 VA em casa de máquinas, barrilete, sala de bombas | 4.2.1.2.3 | Posição do equipamento (chuveiro, ar-condicionado) e da tomada |
| E-P06 | Aquecedor de água ligado direto, sem tomada | 9.5.2.3 | Família do chuveiro ligada a tomada |
| E-P07 | Toda tomada com contato de PE; tomadas de tensão diferente marcadas | 6.5.3 | Tipo da família; tensão do circuito |
| E-P08 | Interruptor e tomada fora dos volumes 0, 1 e 2 de banheiro; tomada no volume 3 só com DR 30 mA, SELV ou separação | 9.1.4.3 | Volumes gerados a partir de boxe e banheira (0,60 m; 2,25 m; 0,60 m; 3,00 m; 2,40 m) |
| E-P09 | IP mínimo por volume de banheiro (X7, X4, X3, X1) e de piscina (X8, X5, X2/X4/X5) | 9.1.4.1, 9.2.4.1 | Parâmetro IP da família de luminária |
| E-P10 | Nenhuma tomada em sauna; IP24; dispositivos fora | 9.4.4.3 | Uso do ambiente |
| E-P11 | IP por influência externa do ambiente (AD → IPX, AE → IPnX) | Tab. 32 | Códigos AD/AE do ambiente |

## 2. Circuitos (Verificar modelo, Gerar circuitos)

| Código | Regra | Item |
|---|---|---|
| E-C01 | Iluminação e tomadas em circuitos distintos; em habitação, comum só com IB ≤ 16 A e sem concentrar toda a iluminação ou todas as tomadas num só | 4.2.5.5, 9.5.3.3 |
| E-C02 | Equipamento com In > 10 A em circuito independente | 9.5.3.1 |
| E-C03 | Tomadas de cozinha, copa, área de serviço e lavanderia em circuitos exclusivos desses locais | 9.5.3.2 |
| E-C04 | Neutro nunca comum a mais de um circuito | 6.2.6.2.1 |
| E-C05 | Todo circuito com PE em toda a extensão | 5.1.2.2.3.6 |
| E-C06 | Cargas equilibradas entre as fases | 4.2.5.6 |
| E-C07 | Disjuntor que secciona todas as fases juntas em circuito com mais de uma fase (habitação) | 9.5.4 |
| E-C08 | Nenhum dispositivo unipolar de comando ou proteção no neutro | 5.6.6.1.2, 6.3.2.2 |
| E-C09 | Todo equipamento com dispositivo de comando funcional; plugue serve até 20 A | 5.6.6.1.3, .4 |
| E-C10 | Circuitos de supervisão predial e de serviços de segurança separados e independentes | 4.2.5.3, 6.6.7.1 |
| E-C11 | Fontes diferentes (rede e gerador) sem compartilhar quadros e linhas | 4.2.5.7 |

## 3. DR (Verificar modelo, Calcular)

| Código | Regra | Item |
|---|---|---|
| E-D01 | DR ≤ 30 mA obrigatório: pontos em banheiro; tomadas externas; tomadas internas que alimentem o exterior; em habitação todos os pontos de cozinha, copa, lavanderia, área de serviço, garagem e áreas molhadas (luminária a ≥ 2,50 m pode sair); em não residencial as tomadas desses locais; tomadas até 32 A | 5.1.3.2.2 |
| E-D02 | Piscina: volume 2 com DR 30 mA, SELV ou separação; volumes 0 e 1 só SELV 12 V | 9.2.3.1 |
| E-D03 | Fuga natural dos circuitos protegidos ≤ 50 % de IΔn | 6.3.3.2.6 |
| E-D04 | Tipo AC só com correntes senoidais; A com componente contínua pulsante; B em CC (VE, fotovoltaica) | 6.3.3.2.1 a .3 |
| E-D05 | Seletividade entre DRs: montante tipo S e IΔn ≥ 3 × jusante | 6.3.6.3.2 |
| E-D06 | DR não protege circuito TN-C; PEN separado antes do DR | 5.1.2.2.4.2 f |
| E-D07 | Em TT, choque só por DR: RA · IΔn ≤ UL (50 V; 25 V em situação 2) | 5.1.2.2.4.3, Anexo C |
| E-D08 | Congelador: DR de alta imunidade em circuito próprio, ou separação elétrica | 5.1.3.2.2 nota 4 |

## 4. Condutores (Calcular)

| Código | Regra | Item |
|---|---|---|
| E-S01 | Seção mínima: 1,5 mm² iluminação; 2,5 mm² força e tomadas; 0,5 mm² sinal | Tab. 47 |
| E-S02 | Método de referência pela maneira de instalar (eletroduto embutido em alvenaria → B1; eletrocalha → B1/B2 ou E/F; forro e shaft → espaço de construção 21/22; enterrado → D) | Tab. 33 |
| E-S03 | Capacidade de corrente das Tabelas 36 a 39 pelo método, isolação, material e número de condutores carregados | 6.2.5.2 |
| E-S04 | Fator de temperatura (Tab. 40), de resistividade do solo (Tab. 41), de agrupamento (Tab. 42 a 45), de neutro carregado 0,86 (harmônicas > 15 %) | 6.2.5.3 a 6.2.5.6 |
| E-S05 | Circuitos até 30 % da capacidade não contam no agrupamento | 6.2.5.5.2 |
| E-S06 | Condutores dessemelhantes (mais de três seções sucessivas): F = 1/√n ou NBR 11301 | 6.2.5.5.5 |
| E-S07 | Vale o pior trecho do percurso | 6.2.5.8 |
| E-S08 | Neutro igual à fase em monofásico, bifásico e trifásico com harmônicas > 15 %; reduzido só acima de 25 mm² pela Tab. 48 com circuito equilibrado, harmônicas ≤ 15 % e neutro protegido; acima de 33 % de 3ª harmônica, Anexo F | 6.2.6.2 |
| E-S09 | Queda de tensão: ≤ 5 % do ponto de entrega (7 % de transformador ou gerador próprio); ≤ 4 % no circuito terminal; +0,005 %/m acima de 100 m até 0,5 %; com IB incluindo harmônicas | 6.2.7 |
| E-S10 | Condutores em paralelo só iguais, sem derivação, em trifólio ou conduto comum até 50 mm² | 6.2.5.7 |
| E-S11 | PE: S até 16; 16 de 16 a 35; S/2 acima; 2,5/4 mm² fora do conduto; comum a vários circuitos pela maior fase | 6.4.3.1 |
| E-S12 | PEN só fixo, ≥ 10 mm² Cu | 6.4.3.4.1 |
| E-S13 | Alumínio só em indústria ≥ 16 mm² ou comércio ≥ 50 mm² BD1, com BA5; nunca em BD4 | 6.2.3.8 |
| E-S14 | Cabos sem halogênio e baixa fumaça em áreas comuns e de público de locais BD2/3/4; não propagantes em BE2 | 5.2.2.2.3, 5.2.2.3.6 |
| E-S15 | Cores: neutro azul-claro; PE verde-amarelo ou verde; PEN azul com anilhas; fase outra cor | 6.1.5.3 |

## 5. Proteção contra sobrecorrentes e choque (Calcular)

| Código | Regra | Item |
|---|---|---|
| E-Q01 | IB ≤ In ≤ IZ e I2 ≤ 1,45 IZ | 5.3.4.1 |
| E-Q02 | Proteção em toda redução de capacidade, ou até 3 m depois em trecho sem derivação | 5.3.4.2, 5.3.5.2 |
| E-Q03 | Icc presumida calculada no quadro (máxima) e no fim de cada circuito (mínima) | 5.3.5.1, 6.3.4.3 nota 2 |
| E-Q04 | Capacidade de interrupção ≥ Icc no ponto, ou retaguarda coordenada | 5.3.5.5.1 |
| E-Q05 | I²t ≤ k²S², k = 115 (Cu PVC), 143 (Cu EPR/XLPE), 76/94 (Al) | 5.3.5.5.2, Tab. 30 |
| E-Q06 | Disjuntor: Ia ≤ Ikmin e Ib ≥ Ik; fusível: Ia ≤ Ikmin | 6.3.4.3 |
| E-Q07 | TN: Zs · Ia ≤ Uo com tempos da Tab. 25 (0,4 s em 220 V situação 1; 0,8 s em 127 V); até 5 s em distribuição e equipamento fixo | 5.1.2.2.4.2, Tab. 25 |
| E-Q08 | Ia do disjuntor pela curva (B, C, D) da NBR NM 60898 e do fabricante | 5.3.3.1 |
| E-Q09 | Seletividade entre sobrecorrentes quando a continuidade importa; coordenação de energia passante | 6.3.6.1, 5.3.6 |
| E-Q10 | Sem proteção de sobrecarga (só curto) em bomba de incêndio e outros serviços de segurança, com sinalização | 5.3.4.4, 6.6.7.3 |
| E-Q11 | Motor: IB ≥ In de placa (× fator de serviço); queda ≤ 10 % na partida; partida direta > 3,7 kW consulta a distribuidora | 6.5.1 |
| E-Q12 | Suportabilidade a impulso dos componentes por categoria (Tab. 31) | 5.4.2.3 |

## 6. DPS e aterramento (Verificar modelo, Quadro de entrada)

| Código | Regra | Item |
|---|---|---|
| E-A01 | DPS obrigatório em AQ2 (> 25 dias de trovoada) com linha aérea e em AQ3 | 5.4.2.1.1 |
| E-A02 | DPS na entrada ou no quadro principal junto da entrada; cada linha externa tem o seu | 6.3.5.2.1, .2 |
| E-A03 | Esquema de conexão pelo aterramento (1, 2 ou 3 da Figura 13) | 6.3.5.2.3 |
| E-A04 | Up ≤ categoria II (1,5 kV em 127/220; 2,5 kV em 220/380); Uc ≥ 1,1 Uo; In ≥ 5 kA (20/10 kA no N–PE do esquema 3); Iimp ≥ 12,5 kA (50/25 kA); Icc suportável; disjuntor do DPS ≤ o indicado pelo fabricante | 6.3.5.2.4, .5, Tab. 49 |
| E-A05 | Ligação do DPS ≤ 0,5 m; DPS–PE ≥ 4 mm² (16 mm² para descarga direta) | 6.3.5.2.9 |
| E-A06 | DPS a jusante do DR exige DR com imunidade ≥ 3 kA (tipo S); a montante em TT só no esquema 3 | 6.3.5.2.6 |
| E-A07 | DPS em toda linha de sinal externa no ponto de entrada | 5.4.2.2.1 |
| E-A08 | Eletrodo: armaduras das fundações ou anel no perímetro; tubulação de água não é eletrodo; comum ao SPDA | 6.4.1.1 |
| E-A09 | BEP junto da entrada, reunindo armaduras, tubulações metálicas, blindagens, neutro (TN) e PE principal; barra PE do quadro principal pode ser o BEP | 6.4.2.1 |
| E-A10 | TN-C vira TN-S na entrada ou no quadro principal | 5.4.3.6 |
| E-A11 | Condutor de equipotencialização principal ≥ metade do maior PE, ≥ 6 mm² Cu, até 25 mm² | 6.4.4.1.1 |
| E-A12 | Equipotencialização suplementar em banheiros e piscinas | 9.1.3.1.2, 9.2.3.1.4 |
| E-A13 | Condutor de aterramento enterrado ≥ 50 mm² Cu sem proteção; 16 mm² protegido contra corrosão; 2,5 mm² protegido contra ambos | Tab. 52 |

## 7. Eletrodutos e condutos (Verificar modelo, Traçar, Gerar rede)

| Código | Regra | Item |
|---|---|---|
| E-T01 | Ocupação ≤ 53 % (1 condutor), 31 % (2), 40 % (3 ou mais), pela área externa dos cabos sobre a área útil interna | 6.2.11.1.6 a |
| E-T02 | Trecho contínuo ≤ 15 m interno, 30 m externo, −3 m por curva de 90°; sem caixa possível, um tamanho a mais a cada 6 m | 6.2.11.1.6 b |
| E-T03 | No máximo 3 curvas de 90° (270°) entre caixas; nenhuma acima de 90° | 6.2.11.1.7 |
| E-T04 | Caixa em toda entrada/saída de condutor, emenda, derivação e segmentação; emendas só em caixa | 6.2.11.1.9, .11 |
| E-T05 | Caixas acessíveis com tampa ou espelho | 6.2.11.1.10 |
| E-T06 | Vários circuitos no mesmo conduto só com mesma origem, seções dentro de três valores sucessivos, mesma temperatura máxima e isolados para a maior tensão | 6.2.10.2 |
| E-T07 | Condutores de um circuito, inclusive PE, juntos no mesmo conduto | 6.2.10.3, 6.4.3.5 |
| E-T08 | Faixa I (SELV) e faixa II só juntas com isolamento para a maior tensão ou compartimento separado | 6.2.9.5 |
| E-T09 | Reserva de ocupação para ampliações | 4.2.5.4 |
| E-T10 | Nada em duto de exaustão ou ventilação; não abaixo de tubulação que condensa; afastamento de linhas não elétricas | 6.2.9.4 |
| E-T11 | Travessia corta-fogo obturada por fora e por dentro; dispensa interna até 710 mm² com IP33; shaft obturado em cada piso salvo LSZH ou conduto metálico | 6.2.9.6 |
| E-T12 | Linha em rota de fuga de BD2/3/4 só com garantia de 2 h e fora do alcance | 5.2.2.2.2 |
| E-T13 | Bandeja, leito e perfilado: só cabos uni ou multipolares; camada única ou volume combustível ≤ 3,5/7 dm³/m | 6.2.11.3 |
| E-T14 | Canaleta com condutor isolado só fechada com tampa a ferramenta, ou BA4/BA5, ou a 2,50 m | 6.2.11.4.1 |
| E-T15 | Enterrado: 0,70 m (1,00 m sob via + 0,50 m de cada lado); 0,20 m de outras linhas; fita a 0,10 m acima; só cabos | 6.2.11.6 |
| E-T16 | Aéreo: 5,50 / 4,50 / 3,50 m do solo; 1,20 m de janelas e sacadas | 6.2.11.8 |
| E-T17 | Eletroduto flexível não é PE | 6.4.3.2.3 d |
| E-T18 | Juntas de dilatação: eletroduto seccionado com luva flexível ou cordoalha | 6.2.11.1.15 |

## 8. Quadros (Gerar quadros, Verificar modelo)

| Código | Regra | Item |
|---|---|---|
| E-K01 | Espaço de reserva: 2 (até 6 circuitos), 3 (7–12), 4 (13–30), 15 % acima de 30; alimentador dimensionado com a reserva | Tab. 59 |
| E-K02 | Quadro acessível e identificado por fora; componentes identificados com a notação do projeto | 6.5.4.8, .9 |
| E-K03 | Advertência padrão em quadro residencial | 6.5.4.10 |
| E-K04 | Seccionamento de todos os vivos de todos os circuitos; seccionador geral; PE e PEN nunca seccionados | 5.6.3.1, 5.6.2.2 |
| E-K05 | Conjuntos conforme NBR IEC 60439-1 (hoje 61439); distâncias 10/20 mm em montagem local | 6.5.4.1, .2 |
| E-K06 | Passagens em sala elétrica: 700 mm entre obstáculos, 2 000 mm de altura; 1 000 mm com partes vivas de um lado | Tab. 27, 28 |
| E-K07 | Dispositivos de manobra em BD3/BD4 só para pessoas autorizadas, em caixa incombustível em circulação | 5.2.2.2.4 |
| E-K08 | Seccionamento de emergência vermelho, acessível, travável, uma única ação | 5.6.5, 6.3.7.4 |

## 9. Entregáveis (Folhas, Memorial, Manual)

| Código | Regra | Item |
|---|---|---|
| E-E01 | Projeto com plantas, unifilar, detalhes, memorial descritivo, especificação dos componentes e parâmetros de projeto (Icc, queda, fatores de demanda, temperatura) | 6.1.8.1 |
| E-E02 | As built após a obra | 6.1.8.2 |
| E-E03 | Manual do usuário em residência e pequeno comércio: esquema do quadro com circuitos e pontos, potência máxima por circuito, reservas, aviso para não trocar disjuntores | 6.1.8.3 |
| E-E04 | Checklist de inspeção visual e sequência de ensaios da verificação final; cálculo de Zs dispensa a medição | 7.2.3, 7.3.1.1, 7.3.5.1 |
| E-E05 | Plaqueta "Conexão de segurança – Não remova" nas ligações do BEP | 6.4.2.1.5 |

## 10. Dados de projeto que o plugin pergunta (uma vez)

- Tensão e esquema de condutores vivos (127/220 V mono, bi ou trifásico; 220/380 V); esquema de aterramento (TN-C-S padrão, TT, IT).
- Distribuidora, ponto de entrega, Icc presumida ou potência e impedância do transformador, alimentação aérea ou subterrânea, dias de trovoada (AQ).
- Temperatura ambiente de referência e, por ambiente, os códigos AD/AE/BB/BC/BD/BE quando fogem do normal (padrão AA4/AB4, XX1, BC2).
- Tipo de uso da edificação (habitação, comercial, afluência de público BD3/BD4, indústria) e de cada ambiente (molhado, banheiro, cozinha, área externa, garagem).
- Maneira de instalar padrão (eletroduto embutido em alvenaria B1 é o default residencial), tipo de condutor (PVC 70 °C, EPR/XLPE 90 °C, LSZH), fabricante de disjuntor (curvas e I²t).
- Padrão do escritório: símbolos, formato das tags, cores da legenda, carimbo.
