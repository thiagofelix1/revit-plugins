# Oportunidades e fases do AutoEletrica

Escrito em 05/11/2026 depois de estudar a NBR 5410:2004 inteira, mapear a concorrência ([concorrencia.md](concorrencia.md)) e a Revit API ([revit-api-eletrica.md](revit-api-eletrica.md)). As fontes estão nesses dois arquivos.

## 1. A leitura honesta do mercado

No SPK o mercado estava vazio na norma brasileira: os concorrentes eram estrangeiros e em NFPA. Na elétrica é o contrário: há pelo menos seis plugins brasileiros para o Revit com NBR 5410, um deles grátis e open source, e o JC Tools sozinho lista 78 ferramentas por R$ 99 por mês. Fiação automática, tags, eletroduto, quadro de cargas, unifilar e memorial em Word já são oferta comum.

Então a pergunta "onde construir" tem três respostas, em ordem de força:

### 1.1 Confiança: o cálculo completo e auditável

A NBR 5410 manda determinar a seção por **seis critérios simultâneos** (6.2.6.1.2): capacidade de corrente com fatores, sobrecarga, curto-circuito e solicitação térmica, seccionamento automático contra choque (Zs·Ia ≤ Uo em TN), queda de tensão e seção mínima. O que os concorrentes descrevem para em capacidade + queda + coordenação com o disjuntor; o Circuito Livre admite que as tabelas foram digitadas sem conferência; o JC Tools cita "verificação de curto-circuito com sugestão de curva B", sem dizer se calcula a corrente de curto mínima no fim do circuito.

O que o AutoEletrica faz de diferente, e que é exatamente o que fez o SPK ser levado a sério:

- **Icc máxima no quadro e mínima no fim de cada circuito**, com a impedância de fonte informada pela distribuidora ou estimada; disjuntor conferido nos dois lados (Ia ≤ Ikmin e Ib ≥ Ik, 6.3.4.3.2) e I²t ≤ k²S² (5.3.5.5.2).
- **Agrupamento trecho a trecho lido do traçado real**: o plugin sabe quais circuitos dividem cada eletroduto e eletrocalha; a regra "vale o pior trecho" (6.2.5.8) sai de graça do modelo. À mão ninguém faz isso; os concorrentes pedem um fator global.
- **Comprimento real** pelo traçado dos eletrodutos (não o comprimento lógico do Revit, que erra ~17 %).
- **DR dimensionado**, não só "colocado": soma de fugas ≤ 50 % de IΔn (6.3.3.2.6), tipo A/B conforme a carga, seletividade 3× com tipo S.
- **DPS selecionado** por AQ, esquema de aterramento, Up, Uc, In/Iimp e disjuntor do DPS (6.3.5.2).
- **Memorial circuito a circuito** dizendo qual critério mandou e citando o item, no formato que a norma pede em 6.1.8.1 (plantas, unifilar, memorial, especificação, parâmetros). A norma aceita o cálculo de Zs no lugar da medição em campo quando comprimento e seção são conferíveis (7.3.5.1, nota 1): o memorial do plugin é esse documento.

Isso é cálculo determinístico, no motor .NET puro, com testes contra projetos aprovados, igual ao SPK. IA nenhuma nos números.

### 1.2 Verificação: o "Verificar modelo" da elétrica

O SPK ganhou um comando que aponta problemas em vez de adivinhar. Na elétrica a lista de verificações da norma é longa, geométrica e quase ninguém oferece ([base-de-regras.md](base-de-regras.md)):

- por ambiente: ponto de luz no teto, tomadas mínimas por perímetro (9.5.2.2.1), potências mínimas, TUE a 1,5 m do equipamento, chuveiro sem tomada, DR 30 mA obrigatório pelo uso do ambiente (5.1.3.2.2), **volumes de banheiro e piscina** desenhados a partir do boxe e da banheira (9.1, 9.2) com tomada e interruptor reprovados dentro dos volumes 0–2 e IP mínimo das luminárias;
- por circuito: iluminação misturada com tomada, TUE sem circuito próprio, cozinha e área de serviço fora de circuito exclusivo, neutro compartilhado, PE ausente, interruptor no neutro, disjuntor unipolar em circuito bifásico (9.5.4), seções com mais de três valores sucessivos no mesmo eletroduto (6.2.10.2);
- por eletroduto: ocupação acima de 40 %, trecho acima de 15 m descontando 3 m por curva, mais de três curvas, derivação sem caixa, emenda fora de caixa, travessia corta-fogo sem obturação, eletroduto em rota de fuga em BD2/3/4;
- por quadro: espaço de reserva (Tabela 59), seccionador geral, DPS na entrada quando AQ2 com rede aérea, PEN separado na entrada (5.4.3.6), identificação, advertência residencial;
- entregáveis: unifilar, memorial, parâmetros de projeto, **manual do usuário** (6.1.8.3), que a norma exige em toda residência e ninguém gera.

Um verificador assim serve a **qualquer modelo**, feito à mão, com o JC Tools ou com o Circuito Livre. É o melhor produto de entrada (gratuito ou barato) para conquistar o escritório e depois vender o gerador.

### 1.3 Geração: o projeto inteiro a partir da arquitetura

É o motivo de escritório pequeno ficar no QiBuilder: lá o elétrico de um sobrado leva 2 ou 3 dias; no Revit, 5 a 7. Os plugins brasileiros automatizam passos (tomadas por ambiente, conduíte entre dois pontos clicados, fiação). Falta o fluxo completo, igual ao "Gerar rede" do SPK:

1. ler ambientes, paredes, portas, forros e lajes dos vínculos (a arquitetura vem vinculada, como no SPK);
2. **pontos por ambiente** pela 9.5.2 e pelo uso do ambiente (TUEs onde há chuveiro, ar-condicionado, fogão, máquina), interruptor junto da porta, luminárias por lúmens (NBR ISO/CIE 8995-1) ou por padrão do escritório;
3. **circuitos** pela 9.5.3 e 4.2.5, com balanceamento de fases e reserva;
4. **quadros** posicionados e com reserva da Tabela 59;
5. **roteamento de eletrodutos** por regra: grafo sobre paredes, lajes e forros, caixas a cada 15 m e em toda derivação, no máximo 3 curvas, ocupação 40 %, diâmetro por trecho pela soma dos cabos que passam;
6. **fiação e tags** geradas do resultado, legenda, unifilar, quadro de cargas, memorial, lista, manual do usuário;
7. **Verificar** e **Calcular** rodando em cima, com o motor da seção 1.1.

E, como no SPK, cada passo também existe como ferramenta assistida (gerar pontos num ambiente, traçar um eletroduto entre caixas, ligar pontos a um circuito, aplicar diâmetros), porque o projetista sempre vai ajustar na mão. O gerador é a sequência dessas ferramentas.

### 1.4 O que torna isso defensável

- **Base comum com o SPK** (licença, padrão do escritório, carimbo e folhas, memorial .docx, armazenamento no .rvt, teste automático no Revit): o custo de fazer o segundo produto cai e o escritório de instalações compra os dois.
- **Revit 2026 e 2027 tiraram o dimensionamento nativo de fios** e criaram condutores e cabos como dados (CableType, ConductorSize, HotConductorSize…). Quem gravar o resultado nesses dados nativos (e não só em parâmetros compartilhados) fica integrado a tabelas, tags e exportação IFC do próprio Revit. Nenhum concorrente brasileiro fala disso.
- **Nova NBR 5410** prevista para o fim de 2026 ou 2027 (segunda consulta nacional em 2026): base de regras versionada por edição, como o SPK fez com a 10897 de 2014 e 2020, e estar pronto no dia da publicação (esquemas em CC, recarga de veículos, fotovoltaica, DPS e DR) é uma janela.
- **Padrão de entrada por distribuidora** (Cemig, Enel, Copel, Neoenergia, Equatorial) e demanda: fora da 5410 (1.7), cada uma com norma própria. Tratar como pacotes de regras plugáveis, como as ITs dos bombeiros no SPK. O JC Tools já tem "fábrica de medição por concessionária"; é preciso ter também.

### 1.5 Onde não gastar energia primeiro

- Luminotécnico completo (PRO-Elétrica e RACTools já fazem; o Revit tem análise própria). Fazer o mínimo da 9.5.2.1.2 e deixar lúmens para depois.
- Fotovoltaico e SPDA: só depois do núcleo; o SPDA pode virar um terceiro produto sobre a mesma base.
- Diagrama multifilar detalhado: unifilar primeiro.

## 2. Tese do produto em uma frase

**O AutoEletrica gera, verifica e calcula o projeto elétrico predial no Revit segundo a NBR 5410, com cada número rastreado ao item da norma, usando a mesma base do SPK.** A IA entra na entrevista inicial (uso de cada ambiente, influências externas, distribuidora), nas explicações e no memorial descritivo; nunca nos números.

## 3. Fases

Mesma lógica do SPK: primeiro o que dá valor com menor risco, cada fase útil sozinha, e as peças básicas servem ao modo assistido e ao automático.

### Fase 0 — Base comum (extrair do SPK)

Separar de `addin-spk` o que não é sprinkler: armazenamento no `.rvt` (Extensible Storage e parâmetros compartilhados), padrão do escritório (carimbo, etiquetas, templates), folhas e carimbo, memorial `.docx`, janelas WPF comuns, teste automático (fila, cenas), e o futuro licenciamento. Resultado: `base/` (nome a decidir) referenciado pelos dois add-ins. Critério de pronto: o SPK continua passando o teste automático usando a base extraída.

### Fase 1 — O básico pedido pelos engenheiros, feito do jeito certo

1. **Dados do projeto**: tensão e esquema (127/220 ou 220/380; TN-C-S, TT), distribuidora, AQ e rede aérea, temperatura ambiente, método de instalação padrão, famílias mapeadas (como o botão Famílias do SPK).
2. **Pontos e circuitos**: criar e editar circuitos (`ElectricalSystem`), nomear, atribuir quadro, balancear; ler o que já existe.
3. **Passagem de fiação**: o modelo de verdade é o **eletroduto com os circuitos que passam nele** (o fio do Revit é 2D e por vista). Ferramenta "Passar circuitos" escolhe o caminho pelos eletrodutos modelados (grafo com `Conduit`, `ConduitRun`, caixas), grava por trecho a lista de circuitos e a contagem F/N/R/PE, e desenha a fiação (`Wire`) nas plantas quando o escritório quer o símbolo.
4. **Tags**: tag de fiação por trecho (fases, neutro, retorno, terra, número do circuito e seção, no formato do escritório), tag de ponto (circuito, potência, altura), tag de eletroduto (diâmetro), legenda de fiação por prancha. Tags leem parâmetros que o plugin grava, então funcionam com as famílias de tag do próprio escritório.
5. **Verificar modelo elétrico**: a lista da seção 1.2, com destaque na vista e relatório explicando cada item.

Critério de pronto: um dos engenheiros faz um projeto real de ponta a ponta usando só essas ferramentas e as dele.

### Fase 2 — Dimensionar e entregar

1. `Eletrica.Motor` (.NET puro, netstandard2.0): tabelas da 5410 como dados com item e edição; cálculo dos seis critérios; Icc; DR; DPS; quadro de cargas com demanda; validação contra projetos aprovados (os três que vamos pedir).
2. Comandos: **Calcular** (grava seção, disjuntor, queda, Icc, parecer por circuito nos parâmetros e nos dados de condutor do Revit 2026+), **Diâmetros de eletroduto** por trecho, **Quadro de cargas**, **Unifilar** (vista de desenho gerada), **Memorial** (.docx), **Lista de material**, **Manual do usuário**.
3. Cálculo desatualizado quando o modelo muda (`IUpdater`), como planejado no SPK.

### Fase 3 — Gerar o projeto

1. Leitura da arquitetura vinculada (reaproveitar `LeituraDoEdificio` do SPK): ambientes, portas, paredes, forros, lajes, louças (boxe, banheira, pia) e equipamentos.
2. **Pontos por ambiente** (9.5.2), com uso do ambiente classificado por nome e confirmado pelo projetista (aqui a IA ajuda).
3. **Circuitos automáticos** (9.5.3, 4.2.5) e **quadros** com reserva.
4. **Roteamento de eletrodutos**: grafo de caminhos possíveis (paredes, laje, forro), caixas por regra, otimização de comprimento com restrições (15 m, 3 curvas, ocupação), diâmetro por trecho; primeiro embutido em alvenaria, depois forro e eletrocalha.
5. **Gerar projeto** = sequência das peças; o resultado sai verificado e calculado.

### Fase 4 — Além

Padrão de entrada por distribuidora e demanda; luminotécnico por lúmens; serviços de segurança ligados ao SPK (bomba de incêndio como carga, circuito resistente ao fogo); SPDA (NBR 5419); fotovoltaico; nova edição da 5410; cabeamento estruturado.

## 4. Arquitetura

Igual ao SPK ([mapa](../../docs/refinamentos/spk/mapa-do-codigo.md)):

```text
Botão (addin-eletrica/Commands)
  → ferramenta (Modelagem/, Verificacao/, Folhas/) ou serviço
      → LeitorInstalacao: modelo → Instalacao (quadros, circuitos, trechos de conduto, pontos, ambientes)
      → Eletrica.Motor: regras NBR 5410, dimensionamento, Icc, verificação
      → grava no modelo (parâmetros, dados de condutor, ArmazenamentoEletrica) e em Documentos\AutoEletrica
base comum: padrão do escritório, folhas, memorial docx, armazenamento, teste automático, licença
```

Decisões herdadas: motor sem Revit, testado com `dotnet test`; tabelas como dados com item citado, nunca texto da norma; leitura por proximidade só como modo assistido com aviso; Revit 2025–2027 (.NET 8 e 10), 2023/2024 só se os engenheiros pedirem.

## 5. Riscos

| Risco | Mitigação |
|---|---|
| Mercado cheio e barato (R$ 50–100/mês) | Vender confiança e geração completa, não ferramentas soltas; pacote com o SPK; verificador como porta de entrada |
| Erro de cálculo com responsabilidade | Testes contra projetos aprovados; memorial com cada item; termo de uso; mesma política do SPK |
| Fio do Revit é 2D e por vista; conduíte não conhece circuitos | O dado mestre é o trecho de conduto com a lista de circuitos (parâmetros nossos); o fio é só desenho gerado |
| Comprimento lógico ≠ real | Comprimento pelo traçado de eletrodutos; `SetCircuitPath` para alinhar o caminho do circuito ao traçado |
| Revit 2026 mudou o modelo de condutores (API antiga obsoleta) | Escrever para `CableType`/`ConductorSize` no 2026+ e para parâmetros no 2025; isolar em uma camada |
| Nova NBR 5410 muda tabelas e exigências | Base de regras versionada por edição; comprar a norma quando sair |
| GPL do Circuito Livre | Não copiar código; estudar recursos e limitações declaradas |
| Famílias dos escritórios variam (parâmetros, conectores, tags) | Mapeamento de famílias como no SPK; tags lendo parâmetros nossos |

## 6. Próximos passos

1. Mandar as [perguntas](perguntas-engenheiros.md) aos dois engenheiros e pedir três projetos aprovados (modelo + memorial + quadro de cargas).
2. Decidir o nome da base comum e extrair do SPK (Fase 0) sem quebrar o teste automático dele.
3. Começar `Eletrica.Motor` pelas tabelas (36 a 48, 58, 59) com conferência dupla e pelos testes de dimensionamento de um circuito; validar com o exemplo do AltoQi (circuito F+N 127 V, 17,5 A → 25 A corrigida → 4 mm², queda 0,65 %) e com os projetos dos engenheiros.
4. Em paralelo, a leitura do modelo: circuitos, eletrodutos e caixas de um projeto real deles, para saber o que as famílias trazem.
