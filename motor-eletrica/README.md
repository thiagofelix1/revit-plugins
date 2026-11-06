# Motor AutoEletrica — protótipo

Verificação e dimensionamento de instalações elétricas de baixa tensão pela ABNT NBR 5410:2004 (com a NBR NM 60898 para os disjuntores, a NBR NM 247-3 e a NBR 15465 para a ocupação dos eletrodutos), sem dependência da Revit API. É o par do [motor SPK](../motor-spk/README.md): mesma arquitetura, mesmo padrão de teste. O plano do produto está em [`autoeletrica/docs/plano-de-desenvolvimento.md`](../autoeletrica/docs/plano-de-desenvolvimento.md).

## Rodar

Precisa do .NET SDK 10.

```bash
dotnet test                                                                   # testes das tabelas, dos critérios e do exemplo
dotnet run --project src/Eletrica.Cli -- exemplo exemplos/apartamento.json
dotnet run --project src/Eletrica.Cli -- calcular exemplos/apartamento.json exemplos/apartamento-saida
dotnet run --project src/Eletrica.Cli -- verificar exemplos/apartamento.json
```

O resultado do exemplo está em [`exemplos/apartamento-saida/memorial.md`](exemplos/apartamento-saida/memorial.md), com o [manual do usuário](exemplos/apartamento-saida/manual-do-usuario.md), a [lista de material](exemplos/apartamento-saida/lista-de-material.md) e o [unifilar em texto](exemplos/apartamento-saida/unifilar.txt) ao lado (os .docx não ficam no Git; a CLI os gera).

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/Eletrica.Motor` | Biblioteca `netstandard2.0` (carrega no Revit 2023–2027). |
| `src/Eletrica.Motor/Modelo` | `Instalacao`: alimentação, ambientes, quadros, circuitos, pontos, trechos de eletroduto (com retornos), comandos de iluminação. O construtor valida tudo de uma vez. `Lancamento.cs`: padrão do escritório (`PadraoDeLancamento`), posição do ponto, proposta de pontos e de circuitos, `Comando`. |
| `src/Eletrica.Motor/Lancamento` | `PropostaDePontos` (o que cada ambiente precisa pela 9.5.2: tomadas por perímetro e uso, 600 VA, luz no teto com a carga mínima, chuveiro e ar pelo padrão) e `DivisaoDeCircuitos` (luz separada, cozinha/copa e área de serviço exclusivas, banheiro à parte, acima de 10 A sozinho, o resto por ambiente até o limite do padrão; F+N ou F+F). |
| `src/Eletrica.Motor/Normas` | Tabelas e regras como dados, cada valor com o item: `Nbr5410` (fatores, mínimos, Tabela 59, 9.5…), `Nbr5410Capacidades` (Tabelas 36 a 39 completas), `Nbr60898` (In, Icn, curvas, Tabela 15, classes de I²t do Anexo ZA), `Condutores` (NM 247-3, temperaturas, resistividade), `Eletrodutos` (NBR 15465 + diâmetros internos de catálogo), `DispositivosDR`, `DispositivosDPS`. |
| `src/Eletrica.Motor/Calculo` | `Grafo` (comprimento real pelos eletrodutos), `Eletrotecnica` (IB, queda, impedâncias, curto pelo método das impedâncias), `Dimensionamento` (os seis critérios de 6.2.6.1.2, circuito a circuito, com o agrupamento lido trecho a trecho), `Ocupacao` (eletrodutos), `Verificacao` (Verificar modelo), `CalculoEletrica` (orquestra). |
| `src/Eletrica.Motor/Relatorios` | `Memorial` (Markdown) e `MemorialDocx` (.docx por OpenXML, sem Word), `QuadroDeCargas`, `ManualDoUsuario` (6.1.8.3, Markdown e .docx), `ListaDeMaterial` (condutores por seção e função pelos trechos, eletrodutos, proteções, pontos; Markdown e CSV), `Unifilar` (o diagrama como dados: entrada, geral, DPS, DRs, ramos, quadros filhos), `Docx` (peças OpenXML). |
| `src/Eletrica.Motor/Exemplos` | Apartamento de dois dormitórios com traçado de eletrodutos. |
| `src/Eletrica.Cli` | Linha de comando com entrada em JSON (o formato que o add-in exporta). |
| `testes/Eletrica.Motor.Testes` | xUnit: um valor por coluna das tabelas, casos-limite de cada critério, o exemplo inteiro e sabotagens do exemplo para o Verificar modelo. |

## O que o cálculo faz

1. **Verificar modelo** (`Verificacao`): pontos de tomada e de luz mínimos por cômodo (9.5.2), carga de iluminação (9.5.2.1.2), banheiro (9.1: ponto nos volumes 0–2, IP mínimo por volume, chuveiro fora do volume 1, equipotencialização, volumes não apurados; `B-01` a `B-06`, `A-08`, `A-09`), chuveiro sem tomada (9.5.2.3), DR obrigatório por uso do ambiente e tipo do DR pela carga (5.1.3.2.2, 6.3.3.2; `DR-01`, `DR-02`), divisão de circuitos (4.2.5.5, 9.5.3), comandos (`L-01` a `L-03`), trechos de eletroduto antes do cálculo (15 m − 3 m/curva, 3 curvas, DN mínimo, ponta solta; `T-01` a `T-04`), DPS obrigatório (5.4.2.1.1), reserva do quadro (Tabela 59), e a consistência do modelo (ponto sem circuito, sem caminho pelos eletrodutos, quadro sem alimentador). Cada problema tem código, gravidade, item da norma e os elementos envolvidos, para o add-in destacar na vista.
0. **Lançar** (`Lancamento`, usado pelo add-in antes de existir circuito): a proposta de pontos por ambiente e a divisão em circuitos, testadas contra o próprio Verificar (a proposta não gera erro `A-`; a divisão não gera `C-01`, `C-03`, `C-04`).
2. **Dimensionar** (`Dimensionamento`), do quadro de entrada para as pontas: IB pela ligação; capacidade de corrente pelas Tabelas 36 a 39 com fator de temperatura (Tabela 40) e de agrupamento (Tabela 42) **em cada trecho por onde o circuito passa**, valendo o pior (6.2.5.8); disjuntor NM 60898 com IB ≤ In ≤ IZ (e I2 = 1,45 In ≤ 1,45 IZ); queda de tensão com o comprimento real pelo grafo de eletrodutos, acumulando os alimentadores (≤ 4 % no terminal, ≤ 5 % do ponto de entrega); curto máximo no barramento (Icn), I²t ≤ k²S² (classe 3 do Anexo ZA ou o I²t de catálogo informado), curto mínimo fase-PE no fim do circuito e Zs·Ia ≤ Uo (TN); seção mínima (Tabela 47). A seção final é a menor que passa em tudo e o relatório diz qual critério mandou. Neutro (6.2.6.2) e PE (Tabela 58) saem da fase. DR por circuito (obrigatoriedade, IΔn, tipo).
3. **Eletrodutos** (`Ocupacao`): taxa de ocupação com o diâmetro externo real (NM 247-3) de todos os condutores do trecho, inclusive os retornos dos comandos, DN sugerido, 15 m menos 3 m por curva, até 3 curvas, e o compartilhamento permitido (6.2.10.2: circuitos do mesmo quadro de origem, seções dentro de três valores sucessivos; códigos `E-ORIG`, `E-SEC3`).
4. **Quadros**: potência instalada e de demanda com as parcelas por categoria (`Demanda`, fatores de `FatoresDeDemanda`: iluminação + TUG por faixa, chuveiros e ar-condicionado por número de aparelhos; padrão do motor até a norma da distribuidora entrar), balanceamento de fases com fases impostas (`Fases`), Icc no barramento, disjuntor geral, DRs por grupo (tipo e até seis circuitos, `GrupoDR`), DPS (classe, esquema, Uc, Up, In, Iimp, seções de ligação), reserva.
5. **Esquema TT**: todo circuito com DR (30 mA onde a proteção adicional é exigida, 300 mA nos demais) e RA · IΔn ≤ UL quando a resistência do eletrodo é informada.
6. **Casos especiais**: dois condutores em paralelo por fase quando nenhuma seção única atende (6.2.5.7); agrupamento 1/√n numa segunda passada nos eletrodutos com seções fora de três valores sucessivos (6.2.5.5.5); folga de comprimento e de ocupação e PE comum por eletroduto como opções do projeto; cada critério reprovado sai com a sugestão do que fazer (`Sugestoes`). `Catalogo` reúne disjuntores, DRs, condutores, eletrodutos e tomadas padronizados.

Premissas que o memorial declara: fator de tensão 1,05/0,95 e X/R = 3 da rede a montante no método das impedâncias; resistência a quente ×1,5 no curto mínimo; reatância 0,08 Ω/km; temperatura de operação do condutor pelo Anexo G da IEC 60364-5-52; diâmetros internos de eletroduto de catálogo (a NBR 15465 só fixa o externo).

## O que falta (fases 2 a 4 do plano)

Fatores de demanda da distribuidora do projeto (hoje são os padrões do motor), seletividade entre disjuntores em série (tabelas de fabricante, 7.12), motores (partida, relé, 7.14), neutro reduzido automático, cabos multipolares como opção explícita, memorial .docx, lista de material, e as normas que ainda não chegaram (NBR IEC 61008/61009 e 61643 oficiais, 5419:2026, 61439-3). Ver `autoeletrica/docs/normas-necessarias.md`.
