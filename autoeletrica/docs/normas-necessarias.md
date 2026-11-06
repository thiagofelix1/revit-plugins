# Normas que o AutoEletrica precisa, e quais faltam baixar

Levantamento de 05/11/2026. As edições vêm do catálogo da ABNT, de notícias do setor e das referências da própria NBR 5410; onde não foi possível confirmar, está marcado "confirmar edição". Os PDFs ficam em `nr/` (fora do Git), e cada norma lida vira um mapa em `nr/normas/`.

## 1. O que já temos

| Norma | Edição na máquina | Situação |
|---|---|---|
| ABNT NBR 5410 — Instalações elétricas de baixa tensão | 2004, corrigida 2008 (vigente) | Lida inteira; mapa em `nr/normas/nbr-5410-2004.md` |
| ABNT NBR 10897, 16981, 16704, 5590, 5580 | várias | Do SPK |

Recebidas em 06/11/2026 em `nr/eletrica/` (todas lidas e mapeadas em `nr/normas/`, ver o README de lá):

| Norma | Edição do arquivo | Serve? | Observação |
|---|---|---|---|
| NBR NM 60898 | 2004 | sim | vigente |
| NBR NM 247-3 | 2002 | sim | há emendas; conferir na compra |
| NBR 15465 | 2007 | sim | só diâmetro externo; interno vem do catálogo |
| NBR 14136 | 2012 | sim | vigente |
| NBR ISO/CIE 8995-1 | 2013 | sim | vigente |
| NBR IEC 61439-1 | 2016/2017 | sim, parcial | **o arquivo não tem a Parte 3** (quadros de distribuição DBO) |
| NBR 7286, 7287, 7288, 13248 | 2001, 1992, 1994, 2000 | sim, para temperaturas | edições antigas; 7286 e 7287 têm edição de 2015 |
| NBR 16612 | Em1:2020 | só para FV | **só a emenda**; e a 16612 é o cabo solar, não o condutor predial (erro corrigido na seção 2) |
| NBR 5419 | **2001** | **não** | edição cancelada; precisa da **5419:2026** partes 1 a 4 |
| IEC 61008-1 | AMD1:2012 (inglês) | pouco | só a emenda, sem as tabelas; falta a NBR IEC 61008-1 e a 61009-1 |
| NBR IEC 60947-2 | ? | ainda não | PDF digitalizado sem texto (precisa de OCR ou de outro arquivo) |

Ainda faltam, da prioridade 1: **NBR 5419:2026 (1 a 4)**, **NBR IEC 61008-1 e 61009-1** (DR), **NBR IEC 61643-11** (DPS), **NBR IEC 61439-3** (quadros DBO), um PDF legível da **NBR IEC 60947-2**, e a **norma da distribuidora**. Para DR e DPS o plugin segue com os mapas montados de fontes públicas (`nr/normas/dr-iec-61008-61009.md` e `dps-iec-61643.md`) até as normas chegarem.

## 2. Prioridade 1 — baixar agora (entram nas fases 1 e 2)

| Norma | Edição | Por que o plugin precisa | Onde entra |
|---|---|---|---|
| **ABNT NBR 5419 — Proteção contra descargas atmosféricas, partes 1 a 4** | **2026** (publicada em 10/03/2026, substitui 2015; exigível seis meses depois; versão corrigida com regra de transição) | Análise de risco define se há SPDA e o nível; o eletrodo de aterramento é comum (6.4.1.1.3 da 5410); o DPS classe I depende do nível de proteção; a parte 4 (MPS) trata da proteção interna. Sem ela, o quadro de entrada e o aterramento ficam pela metade. Também é o módulo SPDA futuro | 7.11, 2.7, módulo SPDA |
| **ABNT NBR NM 60898 — Disjuntores para proteção de sobrecorrentes para instalações domésticas e similares** | 2004 (confirmar se há edição nova) | Curvas B, C, D (faixas de disparo magnético), I2 = 1,45 In, Icn: é o que fecha IB ≤ In ≤ IZ, Zs·Ia e Ia ≤ Ikmin | 7.7, 7.8, 7.9 |
| **ABNT NBR IEC 60947-2 — Disjuntores de baixa tensão (industriais)** | confirmar edição | Disjuntores caixa moldada dos quadros gerais; Icu/Ics; curvas ajustáveis | 7.9, 7.12 |
| **ABNT NBR NM 247-3 — Condutores isolados (sem cobertura) PVC até 450/750 V** | 2002 (há emendas) | Diâmetros externos reais por seção (ocupação do eletroduto), temperaturas, rígido × flexível. É a norma do "fio" e do "cabo flexível 750 V" | 1.4, 4.9 |
| ABNT NBR 16612 — Cabos para sistemas fotovoltaicos | 2017 + Em1:2020 | Só para o módulo FV (corrigido: antes constava aqui como norma de condutores prediais, o que estava errado) | módulo FV |
| **ABNT NBR 7286, 7287, 7288 (ou as atuais que as substituíram) — cabos EPR, XLPE, PVC 0,6/1 kV** | confirmar edições | Alimentadores e prumadas em 90 °C; diâmetros externos | 1.4 |
| **ABNT NBR 13248 — Cabos não halogenados, baixa emissão de fumaça** | confirmar edição | Locais BD2/3/4 (5.2.2.2.3) | 1.4, 8.5 |
| **ABNT NBR 15465 — Sistemas de eletrodutos plásticos para instalações elétricas de baixa tensão** | 2008 (confirmar) | Tamanhos nominais e **diâmetros internos** dos eletrodutos de PVC (DN 16 a 85) para a taxa de ocupação; a NBR 6150 citada na 5410 foi substituída | 4.9, 1.4 |
| **ABNT NBR 14136 — Plugues e tomadas até 20 A/250 V** | 2012 (confirmar) | Tomadas 10 A e 20 A, corrente da tomada → potência do ponto e circuito | 2.1, 7.1 |
| **ABNT NBR IEC 61008-1 e 61009-1 — DR (interruptor e disjuntor diferencial)** | confirmar edições | IΔn, tipos AC/A/B/F, tipo S, faixas de atuação (50–100 %) | 7.10 |
| **ABNT NBR IEC 61643-1 (ou 61643-11) — DPS** | confirmar | Classes I/II/III, Up, Uc, In, Iimp, ensaios | 7.11 |
| **ABNT NBR IEC 61439-1 e -3 — Conjuntos de manobra e controle de baixa tensão; quadros de distribuição** | 2016 (parte 1; confirmar) | Substitui a 60439 citada na 5410; requisitos do quadro, aquecimento, distâncias | 3.6, 9.1 |
| **ABNT NBR ISO/CIE 8995-1 — Iluminação de ambientes de trabalho, parte 1: interior** | 2013 | Iluminância por tipo de ambiente para o método dos lúmens; substitui a NBR 5413 citada na 5410 | 2.5 |
| **Norma da distribuidora dos engenheiros** (ND-5.1/5.2 Cemig, GED Enel, NTC 901100 Copel, Neoenergia, Equatorial…) | a atual do site da distribuidora | Padrão de entrada, tabela carga → disjuntor e cabo, demanda, medição individual e coletiva, detalhe padrão | 1.7, 7.13, 9.12 |

## 3. Prioridade 2 — fases 3 e 4

| Norma | Edição | Por que | Onde entra |
|---|---|---|---|
| **ABNT NBR 17019 — Requisitos para instalações em locais especiais: alimentação de veículos elétricos** | 2022 | Pontos de recarga: circuito dedicado, DR tipo A/B ou com detecção CC, demanda, modos de recarga | 2.6, 7.15, módulo VE |
| **ABNT NBR 16690 — Instalações elétricas de arranjos fotovoltaicos — requisitos de projeto** | 2019 | Fiação CC, proteção, seccionamento, aterramento, caixa de junção | módulo FV |
| **ABNT NBR 16149, 16150, 16274** — interface de conexão de FV à rede, ensaios, comissionamento | confirmar | Lado CA do inversor, requisitos da distribuidora | módulo FV |
| **ABNT NBR 14565 — Cabeamento estruturado para edifícios comerciais** | **2025** (substituiu a 2019 em 11/2025) | Pontos, racks, distâncias, comprimento máximo, prumada | módulo cabeamento |
| **ABNT NBR 13300 e 14306 — Redes telefônicas internas; proteção elétrica e CEM em redes de telecomunicações** | confirmar | Telefonia e PTR; DPS de sinal (5.4.2.2 da 5410) | módulo cabeamento, 7.11 |
| **ABNT NBR 10898 — Sistema de iluminação de emergência** | **2023** (substituiu 2013) | Luminárias de emergência, autonomia, circuitos; conversa com o SPK e com as ITs | 8.9, módulo emergência |
| **ABNT NBR 17240 — Detecção e alarme de incêndio** | 2010 (revisão em andamento) | Pontos de detecção e alarme, circuitos de segurança | módulo emergência |
| **ABNT NBR 13570 — Instalações elétricas em locais de afluência de público** | 1996 (confirmar) | Requisitos extras para BD3/BD4: cabos, iluminação, divisão de circuitos | 1.8, 8.5 |
| **ABNT NBR 13534 — Instalações elétricas em estabelecimentos assistenciais de saúde** | 2008 (confirmar) | Grupos 0, 1 e 2, IT médico, DSI, equipotencialização | projetos hospitalares |
| **ABNT NBR 14039 — Instalações elétricas de média tensão de 1,0 kV a 36,2 kV** | 2005 vigente; **revisão prevista para 2026** | Entrada em MT, cabine, transformador; só para prédios maiores | módulo MT |
| **ABNT NBR 16384 — Segurança em eletricidade — recomendações e orientações para trabalho seguro** | 2020 (confirmar) | Memorial e manual; referência da NR-10 | 9.8, 9.10 |
| **ABNT NBR 15920 — Condutores elétricos — cálculo da corrente nominal; otimização econômica das seções** | 2011 (confirmar) | Seção econômica (Joule) como critério extra opcional | 7.4 |
| **ABNT NBR 11301 — Cálculo da capacidade de condução de corrente de cabos isolados** | 1990 (confirmar) | Casos fora das tabelas da 5410 (agrupamentos atípicos, solo real) | 7.3 |
| **ABNT NBR 16752 — Desenho técnico — requisitos para apresentação em folhas** | 2020 | Formatos e carimbo das pranchas (base comum) | 9.7 |
| **ABNT NBR 15575 — Edificações habitacionais — desempenho (parte elétrica)** | 2021 | Exigências de desempenho que condomínios pedem | 8.1 |
| **NR-10 (Ministério do Trabalho)** | atual, gratuita | Prontuário e segurança; citada no memorial | 9.8 |
| **ITs do Corpo de Bombeiros do estado (iluminação de emergência, alarme, sinalização)** | atuais, gratuitas | Como no SPK, regras do bombeiro local | módulo emergência |

## 4. Quando sair

| Norma | Previsão | O que fazer |
|---|---|---|
| **ABNT NBR 5410 nova edição** | fim de 2026 ou 2027 (2ª consulta nacional em 2026) | Comprar no dia; refazer a seção 19 do mapa com as diferenças; versionar a base de regras |
| **ABNT NBR 14039 nova edição** | 2026 | Só se o módulo de MT entrar |
| **ABNT NBR 17240 nova edição** | em revisão | Idem para o módulo de emergência |

## 5. Normas citadas na 5410 que não precisam ser compradas agora

NBR 5361 (disjuntores, antiga), NBR 5597/5598/5624 (eletrodutos de aço: só se o escritório usar aço), NBR 6147/6527 (tomadas e interruptores: requisitos de produto), NBR 6524 (fios nus), NBR 8661 (cabos planos), NBR 9313/9326/9513 (conectores), NBR 9518 e IEC 60079 (atmosferas explosivas: só em projeto industrial), NBR 5418, IEC 60364-5-5x (a 5410 já as incorpora), série IEC 61000 (CEM), IEC 60529 (código IP: tabela pública), IEC 61140 (classes I, II, III).

## 6. Ordem sugerida de leitura

1. NBR 5419:2026 partes 1 e 2 (risco e nível), depois 3 e 4.
2. NBR NM 60898, NBR IEC 61008/61009, NBR IEC 61643 (os dispositivos do quadro).
3. NBR 16612 (ou NM 247-3) e NBR 15465 (diâmetros para a ocupação).
4. NBR 14136, NBR IEC 61439, NBR ISO/CIE 8995-1.
5. A norma da distribuidora.
6. NBR 17019, NBR 16690, NBR 14565:2025, NBR 10898:2023, NBR 17240, NBR 13570, NBR 13534.

Ao receber cada PDF, ele vira um `nr/normas/nbr-XXXXX-AAAA.md` e uma linha no README daquela pasta, como já é feito.
