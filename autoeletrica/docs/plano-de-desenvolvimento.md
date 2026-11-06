# Plano de desenvolvimento do AutoEletrica

Escrito em 05/11/2026. Objetivo: um plugin de elétrica predial para o Revit tão completo quanto o AltoQi QiElétrico / Builder e tão auditável quanto o SPK, tudo com a norma citada. Fontes: catálogo de recursos da AltoQi (PDF "Compare as versões", 2020–Assinatura), central de suporte do módulo Elétrico (212 artigos, usados como inventário), páginas de produto, mais a [concorrência](concorrencia.md), a [base de regras](base-de-regras.md) e a [API](revit-api-eletrica.md). A lista de normas a comprar está em [normas-necessarias.md](normas-necessarias.md).

## 1. O que o QiElétrico faz, para não esquecer nada

Inventário do concorrente de referência, agrupado como o produto deles organiza. É a régua de "completo".

| Grupo | Recursos do QiElétrico / Builder (declarados) |
|---|---|
| Configuração e cadastro | Parâmetros normativos da NBR 5410; parâmetros de corrente contínua; fatores de demanda usuais e por tipo de carga; cadastro de concessionárias com tabelas de fornecimento; cadastro de peças (tomadas, lâmpadas, interruptores, caixas, conexões, eletrocalhas, disjuntores, DPS, condutores); mais de 60 esquemas de ligação (F+N, 2F, 3F+N, retorno, paralelo, intermediário); tipo e subtipo de ponto; nomeação de circuitos e comandos; alturas de lançamento; cores da fiação por aplicação; disjuntor IEC ou NEMA no desenho |
| Lançamento de pontos | Tomadas (simples, dupla, quádrupla, chave de ar, bomba com chave-boia, portão); lâmpadas, arandelas, interruptores de várias teclas, paralelo, intermediário, escada, comando em outro pavimento, comando de três ou mais lugares; campainha; ventilador com luminária; pontos de espera; pontos em conduletes; vários pontos na mesma caixa; pontos no mesmo alinhamento vertical; embutido e aparente; **distribuir tomadas** (mínimo da NBR 5410 por aplicação, área e perímetro) e **distribuir lâmpadas**; carregador de veículo elétrico; subestação; aterramento (haste, caixa de inspeção, BEP); gerador e QTA; transformador e nobreak; quadro com transformador; quadro de medição; alimentador predial (entrada de serviço); caixa de passagem; copiar quadros e caixas para outros pavimentos; copiar e espelhar lançamento (apartamento tipo) |
| Circuitos e comandos | Criar e definir circuitos e comandos; numeração e prefixos; fase do circuito; mover entre quadros; circuitos reserva; tipos de carga; hierarquia de quadros; **centro de cargas** (posição do quadro); agrupamento automático dos circuitos aos quadros; sub-redes |
| Condutos | **Lançamento automático dos condutos** (Distribuir condutos: quadrantes do quadro, pesos A e B, limite de condutos por caixa por posição, comprimento máximo 15 m, pontos pesados direto ao quadro, interruptores ligados às lâmpadas do mesmo comando); desvio automático; manual em planta e no isométrico; inclinado; entre pavimentos (prumada); eletrocalhas, perfilados e conduletes com conexões; repetições (condutos em paralelo); dimensionamento por ocupação com FCA; legenda de condutos; verificar traçado; comprimento calculado com folga |
| Fiação | Automática em todos os trechos pelo caminho mais econômico, com fase, neutro, terra e retorno; semiautomática e manual; alterar caminho; terra independente em todos os circuitos; retorno com seção diferente; condutores em paralelo; cabos isolados fora de condutos; cabos multipolares; indicação por cores; legenda de fiação com numeração configurável; organização das indicações (anti-sobreposição, forma numérica quando há muitos fios) |
| Dimensionamento | Corrente de projeto e corrigida (FCT, FCA); capacidade de condução; seção mínima; critério da concessionária (tabela por faixa de carga); coordenação com disjuntor em sobrecarga e curto; capacidade de interrupção; queda total e parcial; seção do terra e do retorno; neutro reduzido em 3F+N; IDR por grupo de circuitos; DPS com disjuntor de desconexão; demanda (unidade individual por tipo de carga; uso coletivo pelo método Codi RTD 027/1990, por área); entrada de serviço; corrente contínua; fotovoltaico (NBR 16612 e 16690); barramento blindado; método dos lúmens; status de erro por circuito (12 tipos) e indicações de lançamento (10 tipos) |
| Quadros e diagramas | Quadro de cargas e de demanda em planta, atualizados; quadro executivo; **unifilar editável** (barramento, disjuntor por circuito, IDR, DPS, medição, quadros subordinados recursivos, gerador e QTA) e **multifilar** (barramento por fase, potência instalada e demandada por fase, dados de condutores, aterramento); esquema de cargas instaladas e demandadas (balanceamento); diagrama de FCA; detalhe padrão da concessionária; prumada elétrica com fiação; esquema vertical |
| Detalhamento e pranchas | Plantas adicionais de luz e força; detalhe isométrico com lançamento e edição; vista frontal (corte); 3D realista com eletrodutos em curva; cotas automáticas; legendas automáticas (símbolos, fiação, condutos, materiais); pranchas automáticas com regiões de impressão; selo e controle de revisões |
| Listas e relatórios | Lista de materiais por projeto, pavimento, quadro e circuito (DOCX, HTML, XLSX), com folga de metragem; relatório de dimensionamento por circuito e quadro; relatório de demanda; relatório luminotécnico; memorial descritivo com blocos padrão |
| BIM | IFC importa e exporta; propriedades exportadas; notas BCF; colisões entre disciplinas; integração com orçamento (Visus) |
| Outras disciplinas | Fotovoltaico (módulos, inversor, strings, arranjos, caixa de junção, baterias, CC/CA, retorno de investimento); SPDA (análise de risco, Faraday, Franklin, eletrogeométrico, descidas automáticas, captores naturais, SPDA estrutural, memorial); cabeamento estruturado (racks, mapa de cabos, esquema lógico, plano de face, prumada, NBR 14565/TIA 568); telefonia fixa, interfone, TV; CFTV; alarme de incêndio (pontos) |

O que o QiElétrico **não** faz, pelo que a documentação mostra, e onde o AutoEletrica passa na frente: curto mínimo no fim do circuito e I²t (ele dimensiona a capacidade de interrupção, não a energia passante), Zs·Ia do seccionamento automático, agrupamento trecho a trecho como consequência do traçado (ele pede ajuste do FCA por conduto), verificação geométrica dos volumes de banheiro, verificação de trechos de eletroduto (15 m, curvas), manual do usuário, e nada roda dentro do Revit.

## 2. Princípios (os mesmos do SPK)

1. **Motor separado:** `Eletrica.Motor` em .NET puro (netstandard2.0), sem Revit, testado com `dotnet test`; o add-in traduz modelo em dados e grava resultados.
2. **Norma como dado:** tabelas com item e edição; o relatório cita o item; o texto da norma nunca é reproduzido.
3. **Fazer pelo correto:** verificação explícita com mensagem para o projetista, nunca heurística que "adivinha" ligação.
4. **Assistido pesa tanto quanto automático:** toda função automática é uma sequência de ferramentas que o projetista usa sozinho; ele sempre ajusta na mão.
5. **IA fora dos números:** entra na classificação de ambientes e cargas, nas explicações e no memorial descritivo.
6. **Base comum com o SPK:** armazenamento no .rvt, padrão do escritório, folhas e carimbo, memorial .docx, teste automático, licença.
7. **Famílias do escritório:** o plugin mapeia famílias e parâmetros (como o botão Famílias do SPK) e grava em parâmetros compartilhados; traz um template e famílias próprias só como ponto de partida.

## 3. Inventário completo de funcionalidades do AutoEletrica

Legenda de nível: **B** básico (o que os engenheiros pediram), **A** assistido (ferramenta que acelera o trabalho manual), **G** geração automática, **C** cálculo, **D** documentação, **V** verificação. "Fase" aponta a seção 4. "QiE" diz se o QiElétrico tem equivalente.

### 3.1 Projeto, configuração e cadastro

| # | Funcionalidade | Nível | Norma / regra | QiE | Fase |
|---|---|---|---|---|---|
| 1.1 | **Dados do projeto**: tensão e esquema de condutores vivos; esquema de aterramento (TN-C-S, TT, IT); distribuidora e ponto de entrega; Icc presumida ou trafo; alimentação aérea ou subterrânea; dias de trovoada (AQ); temperatura ambiente; uso da edificação (habitação, comercial, BD3/BD4, indústria) | B | 4.2.2, 4.2.3, 4.2.6, 5.4.2.1 | sim | 1 |
| 1.2 | **Padrão do escritório**: símbolos, formato das tags, cores da legenda, alturas padrão (baixa, média, alta, teto), carimbo, prefixos de circuito e quadro | B | 6.1.5.3 (cores) | sim | 1 (base comum) |
| 1.3 | **Mapeamento de famílias**: descobrir tomadas, interruptores, luminárias, quadros, caixas, eletrodutos e seus parâmetros (potência, tensão, polos, IP, altura); conferir conectores e sistema de distribuição | B | — | cadastro de peças | 1 |
| 1.4 | **Catálogo de componentes** (dados do motor): condutores (NBR 16612/NM 247-3, 0,6/1 kV, LSZH) com diâmetro externo; disjuntores (curvas B/C/D, In, Icn, I²t por fabricante); DR (IΔn, tipo AC/A/B, S); DPS (classe, Up, Uc, In, Iimp); eletrodutos (NBR 15465, aço) com diâmetro interno; eletrocalhas; caixas; tomadas NBR 14136 10/20 A | C | 6.2.3, 6.3.4, 6.3.5, 6.2.11 | sim | 2 |
| 1.5 | **Esquemas de ligação**: F+N, 2F, 2F+N, 3F, 3F+N, com PE; retorno simples, paralelo, intermediário, comando de vários lugares; definem contagem de condutores por trecho | B | 4.2.2.1 | 60+ esquemas | 1 |
| 1.6 | **Tipos de carga e fatores de demanda**: iluminação, TUG, TUE, motor, ar-condicionado, chuveiro, VE; fatores por tipo e por faixa (padrão e por distribuidora) | C | 4.2.1, norma da distribuidora | sim | 2 |
| 1.7 | **Pacotes de distribuidora**: padrão de entrada, tabela de fornecimento (carga → disjuntor e cabo mínimo), demanda (individual e coletiva, método Codi), detalhe padrão | C/D | 1.7 da 5410; ND/GED/NTC | sim | 4 |
| 1.8 | **Influências externas por ambiente**: AD/AE (IP), BB/BC (situação 1/2), BD, BE, uso (molhado, banheiro, cozinha, externo, garagem) com sugestão por nome do ambiente confirmada pelo projetista (IA ajuda aqui) | B/V | 4.2.6, Tab. 32, Anexo C | parcial | 1 |

### 3.2 Pontos (tomadas, iluminação, comandos, equipamentos)

| # | Funcionalidade | Nível | Norma | QiE | Fase |
|---|---|---|---|---|---|
| 2.1 | **Inserir ponto** com circuito, potência, altura e ambiente já preenchidos (tomada simples/dupla/quádrupla, TUE, luminária, arandela, interruptor de 1 a 6 teclas, paralelo, intermediário, campainha, ventilador, ponto de espera, condulete) | B | 4.2.1.2 | sim | 1 |
| 2.2 | **Vários pontos na mesma caixa** e **no mesmo alinhamento vertical** (tomada baixa + média + alta) | A | — | sim | 1 |
| 2.3 | **Comando de iluminação**: ligar interruptor às luminárias (um lugar, paralelo, intermediário, vários lugares, outro pavimento, área externa com fotocélula ou minuteria) | B | 5.6.6 | sim | 1 |
| 2.4 | **Pontos por ambiente** (automático): tomadas pelo perímetro e uso (1/3,5 m, 1/5 m, 2 sobre a bancada, 1 em banheiro e varanda), luz no teto com interruptor junto da porta do lado da abertura, TUEs onde há chuveiro, ar-condicionado, fogão, máquina (lendo louças e equipamentos dos vínculos) | G | 9.5.2.1, 9.5.2.2, 4.2.1.2.3 | Distribuir tomadas e lâmpadas | 3 |
| 2.5 | **Luminárias por lúmens**: iluminância por uso do ambiente (NBR ISO/CIE 8995-1), fluxo da família, fator de utilização e manutenção, malha regular | G/C | 4.2.1.2.2, 8995-1 | método dos lúmens | 4 |
| 2.6 | **Equipamentos**: quadro de distribuição, quadro de medição, alimentador predial, caixa de passagem, transformador, nobreak, gerador e QTA, carregador de VE, subestação (como ponto de entrega) | B | 6.5.4, 6.6, NBR 17019 | sim | 1 (quadros e caixas), 4 (resto) |
| 2.7 | **Aterramento**: BEP, caixa de inspeção, eletrodo (anel ou armadura), condutor de aterramento, equipotencialização de banheiro | B/D | 6.4.1, 6.4.2, 9.1.3.1.2 | haste e BEP | 2 |
| 2.8 | **Copiar e espelhar** pavimento tipo e apartamento (pontos, circuitos, eletrodutos, fiação), mantendo quadros e numeração | A | — | sim | 3 |
| 2.9 | **Alturas por posição** (piso, baixa, média, alta, teto) e embutido × aparente | B | — | sim | 1 |

### 3.3 Circuitos e quadros

| # | Funcionalidade | Nível | Norma | QiE | Fase |
|---|---|---|---|---|---|
| 3.1 | **Criar, editar, numerar circuitos** (ElectricalSystem) com prefixo, descrição, fase, quadro; mover entre quadros; excluir vazios; reserva | B | 4.2.5 | sim | 1 |
| 3.2 | **Circuitos automáticos**: TUE separada (> 10 A), cozinha e área de serviço exclusivas, iluminação separada (ou comum até 16 A em habitação), limite de pontos ou potência por circuito configurável, por ambiente ou por proximidade | G | 4.2.5.5, 9.5.3 | agrupamento automático | 3 |
| 3.3 | **Balanceamento de fases** automático e manual, com esquema de cargas por fase | C | 4.2.5.6 | esquema de cargas | 2 |
| 3.4 | **Hierarquia de quadros** (medição → geral → terminais), alimentadores, quadros subordinados, sub-redes (normal e emergência) | B | 4.2.5.7, 6.6.7 | sim | 1 |
| 3.5 | **Centro de cargas**: posição sugerida do quadro pela média ponderada das potências | G | — | sim | 3 |
| 3.6 | **Quadro**: disjuntor geral, DR por grupo, DPS com disjuntor de desconexão, barramentos, reserva da Tabela 59, identificação e advertência | C/D | 6.5.4, Tab. 59, 6.3.5.2.5 | sim | 2 |
| 3.7 | **Gerador e QTA**: sub-rede de emergência, dispositivos de transferência, cargas essenciais, fonte de segurança | B/C | 4.2.5.7, 6.6 | sim (2025) | 4 |

### 3.4 Eletrodutos, eletrocalhas e caixas (a "passagem")

| # | Funcionalidade | Nível | Norma | QiE | Fase |
|---|---|---|---|---|---|
| 4.1 | **Traçar eletroduto** entre pontos, caixas e quadros com caminho ortogonal pela parede, laje ou forro, subida e descida automáticas, caixa no fim, tipo e DN do padrão do escritório | A | 6.2.11.1 | manual | 1 |
| 4.2 | **Ligar pontos** (selecionados) a um eletroduto ou caixa existente | A | — | manual | 1 |
| 4.3 | **Caixas de passagem** por regra: a cada 15 m descontando 3 m por curva, em toda derivação, no máximo 3 curvas, acessíveis | A/V | 6.2.11.1.6 b, .7, .9 | parcial | 1 |
| 4.4 | **Prumada**: eletroduto ou shaft entre pavimentos, caixas por pavimento, obturação corta-fogo | A | 6.2.9.6.8 | condutos entre pavimentos, prumada | 2 |
| 4.5 | **Eletrocalhas, perfilados e leitos** com conexões e ocupação; condutores isolados só em calha fechada | A/V | 6.2.11.3, 6.2.11.4 | sim | 2 |
| 4.6 | **Repetições** (eletrodutos em paralelo entre as mesmas caixas) | A | 6.2.11.1.6 nota | sim | 2 |
| 4.7 | **Roteamento automático** (Gerar rede): grafo de caminhos (teto, parede, piso), saídas do quadro por quadrante, custo por comprimento e por compartilhamento, limite de condutos por caixa, pontos pesados direto ao quadro, interruptores ligados às luminárias do mesmo comando, desvio de vigas e pilares dos vínculos, caixas por regra, sem cruzar quadros | G | 6.2.11.1 | Distribuir condutos | 3 |
| 4.8 | **Desvio automático** de obstáculos e **verificar traçado** (trechos sem caminho ao quadro, cruzamentos em T, trechos longos) | A/V | 6.2.11.1 | sim | 3 |
| 4.9 | **Diâmetro por trecho**: soma dos cabos que passam (diâmetro externo real) sobre a área útil, 53/31/40 %, folga de ampliação; sugestão de repetição quando estoura | C | 6.2.11.1.6 a, 4.2.5.4 | sim | 2 |
| 4.10 | **Comprimento real** por trecho e por circuito (soma dos eletrodutos + subidas e descidas), com folga configurável; SetCircuitPath para alinhar o Revit | C | 6.2.7 | sim | 2 |
| 4.11 | **Interferências** com estrutura e outras disciplinas (vigas, pilares, dutos, tubos) | V | 6.2.9.4 | colisões | 3 |

### 3.5 Fiação

| # | Funcionalidade | Nível | Norma | QiE | Fase |
|---|---|---|---|---|---|
| 5.1 | **Passar circuitos** pelos eletrodutos: caminho do quadro até cada ponto pelo grafo de eletrodutos, lista de circuitos e contagem F/N/R/PE por trecho gravadas em parâmetros | B | 6.2.10 | fiação automática | 1 |
| 5.2 | **Esquemas de retorno**: interruptor simples, paralelo (2 retornos), intermediário (4), vários lugares; fase passa pelo interruptor; neutro vai à luminária | B | 5.6.6.1.2 | sim | 1 |
| 5.3 | **Fio desenhado** (Wire) nas plantas a partir do traçado, com símbolos de fase, neutro, retorno e terra, no estilo do escritório | B/D | — | indicação da fiação | 1 |
| 5.4 | **Alterar caminho** de um circuito (forçar por um trecho, evitar outro) e **fiação manual** | A | — | sim | 1 |
| 5.5 | **Terra independente** por circuito ou PE comum no mesmo conduto | C | 6.4.3.1.5 | sim | 2 |
| 5.6 | **Condutores em paralelo**, cabos multipolares, cabos fora de condutos (bandeja) | C | 6.2.5.7, 6.2.10.1, 6.2.11.3 | sim | 2 |
| 5.7 | **Compartilhamento permitido**: mesma origem, seções dentro de 3 valores sucessivos, mesma temperatura, SELV separado | V | 6.2.10.2, 6.2.9.5 | não | 1 |

### 3.6 Tags, legendas e identificação

| # | Funcionalidade | Nível | Norma | QiE | Fase |
|---|---|---|---|---|---|
| 6.1 | **Tag de fiação por trecho**: contagem e seção por função, número dos circuitos, no formato do escritório (ex.: 3#2,5(2,5) C1 C2); forma numérica quando há muitos fios | B | — | sim | 1 |
| 6.2 | **Tag de ponto**: circuito, potência, altura, tensão; tag de quadro; tag de eletroduto (DN) | B | 6.1.5 | sim | 1 |
| 6.3 | **Legenda de fiação** por prancha ou projeto com numeração configurável; legenda de símbolos; legenda de condutos | D | — | sim | 1 |
| 6.4 | **Anti-sobreposição** de tags e fios (otimizar indicações) | A | — | sim | 3 |
| 6.5 | **Cores dos condutores** na legenda e na lista (neutro azul-claro, PE verde-amarelo, fases por padrão) | D | 6.1.5.3 | sim | 1 |

### 3.7 Dimensionamento (Eletrica.Motor)

| # | Funcionalidade | Nível | Norma | QiE | Fase |
|---|---|---|---|---|---|
| 7.1 | **Corrente de projeto** por circuito e por quadro (potência, tensão, fator de potência, rendimento, demanda) | C | 4.2.1 | sim | 2 |
| 7.2 | **Método de referência** pelo traçado (B1 embutido, B2 multipolar, C sobre parede, E/F bandeja, D enterrado, espaços de construção) e **pior trecho** | C | Tab. 33, 6.2.5.8 | manual | 2 |
| 7.3 | **Capacidade de corrente** com FCT, agrupamento **por trecho** (Tab. 42–45), neutro carregado 0,86, condutores dessemelhantes | C | 6.2.5 | FCA por conduto | 2 |
| 7.4 | **Seção pelos seis critérios** com o critério que mandou | C | 6.2.6.1.2 | parcial | 2 |
| 7.5 | **Neutro** (igual, reduzido, Anexo F) e **PE** (Tab. 58 ou k) | C | 6.2.6.2, 6.4.3.1 | sim | 2 |
| 7.6 | **Queda de tensão** por trecho, parcial e total, com reatância em seções grandes | C | 6.2.7 | sim | 2 |
| 7.7 | **Curto-circuito**: Icc máxima trifásica no quadro e mínima fase-neutro/PE no fim de cada circuito; capacidade de interrupção; I²t ≤ k²S²; Ia ≤ Ikmin | C | 5.3.5, 6.3.4.3, Tab. 30 | Icc de interrupção | 2 |
| 7.8 | **Seccionamento automático**: Zs·Ia ≤ Uo com tempos da Tab. 25; RA·IΔn em TT | C | 5.1.2.2.4 | não | 2 |
| 7.9 | **Disjuntor**: In pela regra IB ≤ In ≤ IZ, curva por carga (B, C, D), polos, Icn | C | 5.3.4.1, 5.3.3 | sim | 2 |
| 7.10 | **DR**: obrigatoriedade por local, IΔn, tipo AC/A/B, por grupo, fuga ≤ 50 %, seletividade 3× tipo S | C | 5.1.3.2.2, 6.3.3.2 | sim | 2 |
| 7.11 | **DPS**: obrigatoriedade (AQ, rede aérea), classe, esquema de conexão, Up, Uc, In/Iimp, disjuntor de desconexão, seção das ligações | C | 5.4.2.1, 6.3.5.2 | sim | 2 |
| 7.12 | **Seletividade** entre disjuntores em série (tabelas de fabricante) e entre DRs | C | 6.3.6 | não | 4 |
| 7.13 | **Demanda e entrada**: fatores por tipo de carga, unidade individual e uso coletivo, alimentador predial, padrão de entrada | C | 4.2.1.1, distribuidora | sim | 4 |
| 7.14 | **Motores**: IB de placa, partida, queda na partida, relé térmico + disjuntor-motor | C | 6.5.1 | parcial | 4 |
| 7.15 | **Corrente contínua** (fotovoltaico, VE) | C | NBR 16690, 17019 | sim | 4 |
| 7.16 | **Recalcular ao mudar** (IUpdater) e status de "cálculo desatualizado" | C | — | tempo real | 2 |

### 3.8 Verificar modelo (relatório explicável, com destaque na vista)

| # | Verificação | Norma | Fase |
|---|---|---|---|
| 8.1 | Pontos mínimos e potências por ambiente; luz no teto; TUE a 1,5 m; chuveiro sem tomada | 9.5.2, 4.2.1.2.3 | 1 |
| 8.2 | Volumes de banheiro e piscina gerados a partir do boxe, banheira e piscina; dispositivos dentro dos volumes; IP; DR; equipotencialização | 9.1, 9.2 | 1 |
| 8.3 | DR obrigatório por uso do ambiente; tipo adequado | 5.1.3.2.2, 6.3.3.2 | 1 |
| 8.4 | Circuitos: luz × tomada, TUE própria, cozinha exclusiva, IB ≤ 16 A no comum, neutro compartilhado, PE ausente, interruptor no neutro, disjuntor unipolar em bifásico | 4.2.5.5, 9.5.3, 9.5.4, 6.3.2.2 | 1 |
| 8.5 | Eletrodutos: ocupação, 15 m e curvas, caixas, emendas, seções dentro de 3 valores, SELV junto, rota de fuga, corta-fogo, flexível como PE | 6.2.11.1, 6.2.10.2, 6.2.9.5, 5.2.2.2.2, 6.2.9.6 | 1 |
| 8.6 | Quadros: reserva, seccionador, DPS, PEN separado, identificação, advertência, acesso | Tab. 59, 5.6.3, 6.3.5, 5.4.3.6, 6.5.4 | 2 |
| 8.7 | Modelo: ponto sem circuito, circuito sem quadro, ponto sem caminho ao quadro, sistema de distribuição incompatível, famílias sem parâmetro, eletroduto desconectado, caixa sem tampa | — | 1 |
| 8.8 | Cálculo: cada critério reprovado por circuito, com sugestão (subir seção, trocar curva, dividir circuito) | 6.2.6.1.2 | 2 |
| 8.9 | Serviços de segurança: circuito próprio, resistente ao fogo, fora de BE2/BE3 | 6.6.7 | 4 |

### 3.9 Documentação e entregáveis

| # | Funcionalidade | Nível | Norma | QiE | Fase |
|---|---|---|---|---|---|
| 9.1 | **Quadro de cargas** por quadro (circuito, descrição, pontos, potência por fase, IB, seção, disjuntor, DR, eletroduto) em tabela nativa ou vista de desenho | D | 6.1.8.1 | sim | 2 |
| 9.2 | **Quadro de demanda** e **esquema de cargas por fase** | D | — | sim | 2 |
| 9.3 | **Unifilar** gerado e atualizável (medição, geral, DR, DPS, subordinados, gerador e QTA) | D | 6.1.8.1 b | editável | 2 |
| 9.4 | **Multifilar** por quadro | D | — | sim | 4 |
| 9.5 | **Prumada elétrica** com fiação e esquema vertical | D | — | sim | 2 |
| 9.6 | **Plantas** de luz e força separadas ou juntas, com fios, tags e legendas; **cortes** e **detalhes** (caixa, quadro, entrada); **3D** | D | — | sim | 2 (folhas da base comum) |
| 9.7 | **Pranchas** no carimbo do escritório com regiões, revisões e numeração | D | NBR 16752 | sim | 2 |
| 9.8 | **Memorial descritivo e de cálculo** (.docx) com parâmetros de projeto, critério por circuito, item da norma, relatório de demanda | D | 6.1.8.1 d, e, f | sim | 2 |
| 9.9 | **Lista de material** por projeto, pavimento, quadro e circuito (xlsx/csv), com folga, cores, separação por posição | D | — | sim | 2 |
| 9.10 | **Manual do usuário** da instalação | D | 6.1.8.3 | não | 2 |
| 9.11 | **Checklist de verificação final** e **as built** (comparar modelo executado) | D | 7.2, 7.3, 6.1.8.2 | não | 4 |
| 9.12 | **Detalhe padrão da distribuidora** e documentos do padrão de entrada | D | distribuidora | sim | 4 |

### 3.10 Geração automática (Gerar projeto)

Sequência das peças acima, por pavimento ou por unidade, com pré-visualização e desfazer por ambiente:

1. ler arquitetura e estrutura vinculadas (ambientes, portas, paredes, lajes, forros, louças, equipamentos, vigas e pilares);
2. classificar ambientes (uso, influências externas) com confirmação;
3. pontos por ambiente (2.4) e luminárias (2.5 quando houver);
4. quadros no centro de cargas (3.5) com hierarquia;
5. circuitos (3.2) e balanceamento (3.3);
6. eletrodutos (4.7) com caixas, prumadas e diâmetros;
7. passagem de circuitos (5.1) e fios (5.3);
8. cálculo (3.7) e verificação (3.8);
9. tags, legendas, quadros, unifilar, pranchas, memorial, lista e manual (3.6, 3.9).

Cada passo também é um botão. Rodar tudo é o "Gerar projeto"; o normal é rodar por partes e ajustar entre elas.

### 3.11 Outras disciplinas (produtos ou módulos futuros)

| Módulo | Escopo | Norma | Depende de |
|---|---|---|---|
| Fotovoltaico | Módulos, strings, arranjos, caixa de junção, inversor, CC/CA, proteção CC, aterramento, diagramas, memorial, retorno | NBR 16690, 16149, 16150, 16274, 5410 | Catálogo e motor CC |
| Veículos elétricos | Pontos de recarga, circuito dedicado, DR tipo A/B, demanda | NBR 17019 | Motor |
| SPDA | Análise de risco, nível, captores (Faraday, Franklin, eletrogeométrico), descidas, aterramento, MPS, memorial | NBR 5419:2026 (4 partes) | Base comum; compartilha eletrodo e DPS com a elétrica |
| Cabeamento estruturado, telefonia, TV, interfone | Pontos, racks, cabos, mapa de cabos, prumada, comprimento máximo | NBR 14565:2025, NBR 13300/14306 | Mesmo roteamento de condutos |
| Alarme e detecção, iluminação de emergência | Pontos, circuitos de segurança, autonomia | NBR 17240, NBR 10898:2023, ITs | Sub-rede de emergência |
| Média tensão e subestação | Entrada em MT, cabine, transformador | NBR 14039 | Fase posterior |

## 4. Roadmap por entregas

Cada entrega é usável sozinha e tem um critério de pronto verificável, como no SPK.

### 0.1 — Base comum (Fase 0)

Extrair de `addin-spk` para um projeto compartilhado: armazenamento no .rvt, padrão do escritório e carimbo, folhas e vistas, memorial .docx, janelas e ícones, transação segura e engolir avisos, leitura do edifício e dos vínculos, teste automático (fila, cenas, relatório), mapeamento de famílias. Esqueleto `addin-eletrica` com aba AutoEletrica e `motor-eletrica/Eletrica.Motor` com testes. **Pronto:** SPK passa o teste automático usando a base; a aba AutoEletrica abre no Revit 2025–2027.

### 0.2 — Dados, pontos e circuitos (Fase 1)

Itens 1.1, 1.2, 1.3, 1.5, 1.8, 2.1, 2.2, 2.3, 2.6 (quadros e caixas), 2.9, 3.1, 3.4. **Pronto:** um engenheiro lança um apartamento com as ferramentas e os circuitos ficam corretos no Revit.

*Escrito em 06/11/2026 (compila, não testado no Revit):* 1.1 Dados do projeto; 1.2 Padrão (alturas, potências, limites, prefixos); 1.3 Famílias (papel por tipo de família, hospedagem, conector); 1.5 em parte (F+N ou F+F pela corrente; retorno simples, paralelo e intermediário pelos comandos); 1.8 só o uso do ambiente; 2.1 Inserir ponto (colocação nativa + dados); 2.3 Comando; 2.6 Quadro e Alimentar quadro (caixas ficam para 4.3); 2.9 posições baixa/média/alta/interruptor/teto; 3.1 Criar circuitos com a divisão por norma (adianta o 3.2); 3.4 hierarquia por alimentador. Também adiantou do 2.4 (fase 3) o **Pontos por ambiente**: lança o mínimo da 9.5.2 pelas paredes, luz no centro, interruptor junto da porta. Falta 2.2 (mesma caixa, alinhamento vertical) e o teste real: o critério de pronto só fecha no Revit.

### 0.3 — Passagem de fiação e tags (Fase 1, o pedido dos engenheiros)

Itens 4.1, 4.2, 4.3, 5.1, 5.2, 5.3, 5.4, 5.7, 6.1, 6.2, 6.3, 6.5. **Pronto:** planta de um apartamento com eletrodutos, fios, tags e legenda no padrão do escritório, sem edição manual de tag.

*Escrito em 06/11/2026 (compila, não testado no Revit):* 4.1 Traçar eletroduto (ortogonal pela cota de passagem, tipo e DN do padrão, tê ao chegar num eletroduto; sem desvio de obstáculos nem caixa automática na subida); 4.2 Ligar pontos; 4.3 Caixas por regra (15 m − 3 m/curva, 3 curvas; caixas quebram o trecho no grafo); 5.1 Passar circuitos; 5.2 retornos pelos comandos; 5.3 Desenhar fiação (Wire em arcos ou polilinha, com AE Fiação); 5.4 Fiação manual (forçar/evitar por eletroduto); 5.7 mesma origem e seções em três valores sucessivos no motor (E-ORIG, E-SEC3); 6.1/6.2 Etiquetar (eletroduto, fio, ponto, interruptor, quadro, pelos parâmetros AE); 6.3/6.5 Legenda de fiação com cores (vista de desenho). Falta: famílias de etiqueta de referência, legenda de símbolos, 6.4 anti-sobreposição, e o teste real.

### 0.4 — Verificar modelo (Fase 1)

Itens 8.1 a 8.5 e 8.7 com relatório e destaque na vista. **Pronto:** rodado nos três projetos de referência, aponta problemas reais que os engenheiros confirmam.

*Escrito em 06/11/2026 (compila, não testado no Revit):* 8.1 completo no motor mais chuveiro a 1,5 m do boxe (R-04); 8.2 volumes do banheiro gerados do boxe, banheira ou chuveiro da arquitetura vinculada (sem descontar paredes), ponto e interruptor nos volumes, IP mínimo por volume lido da família, equipotencialização como nota; piscina e sauna ficam de fora; 8.3 DR por uso e tipo pela carga (DR-02); 8.4 o que dá para ler do modelo (luz × tomada, TUE própria, cozinha exclusiva, IB ≤ 16 A); 8.5 15 m, curvas, DN mínimo e ponta solta antes do cálculo (T-01 a T-04), ocupação, mesma origem e seções no cálculo; 8.7 ponto sem circuito, sem caminho, quadro sem alimentador, sistema de distribuição divergente, família sem conector ou sem potência, interruptor sem comando. Relatório em Markdown e filtro por gravidade na janela. Falta o teste nos projetos de referência.

### 0.5 — Motor e Calcular (Fase 2)

Itens 1.4, 1.6, 7.1 a 7.11, 7.16, 4.9, 4.10, 5.5, 5.6, 3.3, 3.6, 8.6, 8.8. **Pronto:** reproduz o dimensionamento dos três projetos de referência (ou explica a diferença pelo item da norma) e o exemplo do AltoQi; testes no motor para cada tabela e critério.

*Escrito em 06/11/2026 (179 testes; não comparado com projetos reais):* 1.4 `Catalogo` (disjuntores, DRs, condutores, eletrodutos, tomadas; sem dados de fabricante); 1.6 `FatoresDeDemanda` (iluminação + TUG por faixa, chuveiros e ar por número de aparelhos; padrões do motor, a trocar pela distribuidora); 7.1 a 7.11 completos, com 7.3 ganhando o 1/√n para seções dessemelhantes (segunda passada) e 7.8 o TT (DR em todo circuito, RA · IΔn ≤ UL); 7.16 pela assinatura da instalação (Guia e Memorial avisam "desatualizado"; sem IUpdater); 4.9 folga de ampliação e sugestão de repetição; 4.10 folga de comprimento e `SetCircuitPath`; 5.5 PE comum por eletroduto como opção; 5.6 dois condutores em paralelo por fase (cabos multipolares e bandeja só pelo método de referência do trecho); 3.3 `Fases` com fase imposta e `AE Fase`; 3.6/8.6 geral, DRs por grupo (tipo, até seis), DPS, seccionador e identificação como notas; 8.8 `Sugestoes` em cada problema D-/E-. Falta: comparar com os projetos de referência e o exemplo do AltoQi, seletividade (7.12), motores (7.14).

### 0.6 — Entregáveis (Fase 2)

Itens 9.1, 9.2, 9.3, 9.5, 9.6, 9.7, 9.8, 9.9, 9.10, 2.7, 4.4, 4.5, 4.6. **Pronto:** jogo de pranchas, memorial, lista e manual de um projeto real aceitos pelo engenheiro sem retrabalho fora do plugin. Esta é a versão vendável.

*Escrito em 06/11/2026 (183 testes; nada aberto no Revit):* 9.1/9.2 quadro de cargas com demanda e cargas por fase em vista de desenho (mais o PanelScheduleView nativo); 9.3 unifilar como dados no motor e desenho no add-in (geral, DPS, DRs, ramos, quadros filhos); 9.5 prumada (quadros por nível e alimentadores); 9.6/9.7 pranchas AE-PL por nível, AE-QD por quadro e AE-DOC, no primeiro carimbo do projeto, com registro para refazer; 9.8 memorial .docx (OpenXML) com tudo do Markdown; 9.9 lista de material pelos trechos em Markdown, CSV e vista, mais tabelas nativas; 9.10 manual do usuário (6.1.8.3) em Markdown e .docx. Não entraram: 2.7 aterramento, 4.4 prumada modelada, 4.5 eletrocalhas, 4.6 repetições, escolha de carimbo e numeração do escritório (vêm com a base comum do SPK), símbolos de anotação do escritório. O critério de pronto depende de um projeto real.

### 1.0 — Gerar projeto (Fase 3)

Itens 2.4, 2.8, 3.2, 3.5, 4.7, 4.8, 4.11, 6.4 e a sequência 3.10. **Pronto:** apartamento tipo gerado a partir da arquitetura vinculada em minutos, verificado e calculado, com ajustes manuais possíveis em qualquer etapa.

### 1.x — Além (Fase 4)

Itens 1.7, 2.5, 3.7, 7.12 a 7.15, 9.4, 9.11, 9.12, 8.9 e os módulos da seção 3.11, começando por distribuidora e VE (dependem só do motor), depois fotovoltaico e SPDA (nova 5419:2026), depois cabeamento.

## 5. A aba no Revit (proposta)

| Painel | Botões |
|---|---|
| Projeto | Padrão do escritório · Dados do projeto · Famílias · Ambientes (uso e influências) · Arquitetura (vínculos) |
| Pontos | Tomada · Luminária · Interruptor · Equipamento · Quadro · Caixa · Pontos por ambiente · Copiar pavimento |
| Circuitos | Criar · Editar · Numerar · Balancear · Centro de cargas · Circuitos automáticos |
| Condutos | Traçar eletroduto · Ligar pontos · Caixas · Prumada · Eletrocalha · Gerar rede · Verificar traçado |
| Fiação | Passar circuitos · Alterar caminho · Fios em planta · Tags · Legenda |
| Cálculo | Verificar modelo · Calcular · Diâmetros · Quadros (DR, DPS, reserva) |
| Resultados | Quadro de cargas · Unifilar · Prumada · Folhas · Memorial · Lista · Manual · Cores |
| Ajuda | Guia · Situação do projeto |

## 6. Modelo de dados do motor (esboço)

```text
Instalacao
  ├─ Alimentacao (tensão, esquema, Icc, distribuidora, AQ)
  ├─ Ambientes[] (uso, área, perímetro, influências, volumes especiais)
  ├─ Quadros[] (hierarquia, barramentos, dispositivos, reserva)
  ├─ Circuitos[] (tipo de carga, pontos, esquema de ligação, quadro, fase)
  ├─ Pontos[] (tipo, potência, tensão, altura, ambiente, circuito, comando)
  ├─ Trechos[] (conduto: tipo, DN, método de referência, comprimento, curvas, circuitos que passam)
  ├─ Caixas[]
  └─ Resultados (por circuito: IB, IZ, seção, disjuntor, DR, queda, Icc, Zs, parecer por critério)
```

O add-in monta `Instalacao` lendo o modelo (LeitorInstalacao) e grava `Resultados` de volta (parâmetros e dados de condutor); o motor nunca vê o Revit.

## 7. Testes

- **Motor:** teste por tabela (valores digitados com conferência dupla), por critério (casos limite) e por projeto de referência (os três dos engenheiros, mais o exemplo do AltoQi).
- **Add-in:** teste automático no Revit como o do SPK (fila, cenas, sabotagens): gerar um apartamento sintético, passar circuitos, verificar, calcular, gerar folhas; comparar com o esperado.
- **Modelos de terceiros:** reaproveitar os modelos de teste do SPK (Snowdon, MNEVVM) para arquitetura vinculada.

## 8. O que decidir com os engenheiros antes da 0.2

As [perguntas](perguntas-engenheiros.md) de sempre, mais três específicas deste plano: (a) o formato exato das tags e da legenda; (b) se preferem o fio desenhado ou só a tag no eletroduto; (c) se o roteamento automático deve priorizar teto (laje) ou parede, e as alturas padrão.
