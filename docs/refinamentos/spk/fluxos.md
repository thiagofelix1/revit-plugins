# Fluxos de ponta a ponta

Como o engenheiro usa o SPK do começo ao fim de um projeto, em dois casos: um projeto regular (galpão, depósito, pavimento tipo) e um projeto real complexo, com arquitetura e estrutura (o caso da Nova Era). Cada passo diz **quem faz** (engenheiro ou plugin) e **o que já existe**.

Decidido em 26/10/2026: o SPK terá **geração automática** da rede e também **modelagem assistida** (ferramentas que ajudam a desenhar a rede real, como fazem o AutoSPRINK RVT e o SprinkCAD). As duas usam as mesmas peças básicas; a ordem de construção está na [lista de desenvolvimento](lista-desenvolvimento.md).

## Fluxo A — projeto regular

1. **Preparação** (uma vez por projeto)
   - Padrão do escritório: carimbo, etiquetas, templates, numeração das folhas. *Existe.*
   - Dados do projeto: o engenheiro escolhe classe de risco, base de cálculo, hidrantes e abastecimento. *Existe.*
2. **Esboço da rede**
   - O engenheiro desenha ou escolhe o **contorno** da área (laje, ambiente) e o **ponto da VGA**.
   - O plugin **gera** bicos, ramais, subgeral e a ligação até a VGA, já conectados, no arranjo escolhido (espinha de peixe, lateral, grelha). *Existe em versão simples (retângulo digitado); falta ler o contorno do modelo.*
   - O engenheiro **ajusta** com as ferramentas assistidas: move uma fileira, liga um bico a mais, retraça um ramal. *A construir.*
3. **Conferência**
   - Verificar modelo: peças desligadas, bico sem tubo, sistema errado. *Existe.*
   - Conferência da norma: espaçamento, área por bico, distância à parede. *A construir.*
4. **Cálculo**
   - O plugin acha a área mais desfavorável, calcula e diz se atende. *Existe.*
   - Se não atende, o engenheiro sobe diâmetros com a ferramenta de diâmetros e recalcula. *A construir* (dimensionamento automático vem depois).
5. **Saídas**
   - Memorial (.docx) e folhas (setorização, isométrico com quadros, planta do setor, corte) no carimbo do escritório. *Existe.*
   - Lista de material. *A construir.*
6. **Revisão**
   - Mudou algo: ajusta, roda Calcular e Folhas de novo. O plugin recria as folhas dele sem tocar nas do escritório. *Existe.*

## Fluxo B — projeto real complexo (Nova Era)

A rede convive com o prédio: forro, vigas, telhado inclinado, pilares, shafts, câmaras frias. Muda principalmente o desenho da rede.

1. **Preparação**
   - Igual ao fluxo A.
   - A **arquitetura e a estrutura** costumam vir como **modelos vinculados** (outro .rvt dentro do de incêndio). O plugin precisa enxergar forros, ambientes, vigas e telhados dentro dos vínculos. *A construir.*
2. **Setorização**
   - O engenheiro decide os setores (um por VGA): depósito, loja, docas, mezanino.
   - O plugin mostra a área de cada setor contra o limite da Tabela 8 e avisa se passou; mais tarde, sugere cortes. *Folha de setorização existe; sugestão a construir.*
3. **Bicos, ambiente por ambiente**
   - Forro de loja: o plugin lê o ambiente ou o forro e distribui os bicos pela regra (espaçamento, distância da parede, área máxima); o engenheiro confere e move o que bater em luminária ou difusor.
   - Depósito sob telhado com vigas: o plugin distribui os ESFR seguindo a inclinação e evitando a sombra das vigas; os casos de obstrução são decisão do engenheiro.
   - Câmara fria: o engenheiro escolhe o bico seco e posiciona; o plugin confere.
   - *A construir. É o passo mais difícil do produto.*
4. **Tubulação**
   - O engenheiro traça a **subgeral** no caminho que quer (entre ou sob as vigas). É decisão de projeto; a ferramenta ajuda a desenhar.
   - O plugin **liga os bicos ao ramal** e **traça os ramais** até a subgeral pelos bicos selecionados.
   - O engenheiro traça o **tubo geral** da VGA até a casa de bombas.
   - O plugin avisa **colisão** com viga ou duto e **inclinação**.
   - *A construir.*
5. **Conferência**
   - Verificar modelo. Na Nova Era encontrou 971 peças encostadas sem ligação, 169 derivações sem tê e 610 tubos em sistema hidrônico. Num projeto feito com as ferramentas do plugin isso quase não acontece, porque tudo nasce ligado.
   - Em projeto antigo ou recebido de outro, **reconexão assistida** em lote. *Verificação existe; reconexão a construir.*
6. **Cálculo por setor**
   - Uma VGA por vez; o plugin acha a pior área de cada setor. *Existe.*
   - O engenheiro ajusta diâmetros até todos atenderem; o plugin mostra a demanda do setor mais exigente, que dimensiona a bomba. *Visão de todos os setores juntos a construir.*
7. **Saídas**
   - Memorial por setor; folhas: setorização de todos os níveis, isométrico geral com casa de bombas, planta com isométrico por setor (como a 014), cortes e detalhe da VGA.
   - *Conjunto principal existe.* O isométrico mostra casa de bombas e tubo geral se estiverem modelados. *Folhas por setor em lote e cotas a construir.*

## Diferença entre os dois

| | Fluxo A (regular) | Fluxo B (complexo) |
|---|---|---|
| Quem desenha a rede | principalmente o plugin; o engenheiro ajusta | principalmente o engenheiro, com as ferramentas do plugin |
| Ganho principal | velocidade: rede, cálculo e folhas em horas | menos trabalho repetitivo, rede sempre ligada, cálculo e folhas sem retrabalho |
| Maior dificuldade técnica | ler o contorno do modelo | ler os vínculos de arquitetura e estrutura e as obstruções |

## Peças básicas comuns

Gerar a rede inteira é chamar, em sequência, as mesmas ferramentas da modelagem assistida:

1. posicionar bicos num contorno ou ambiente pela regra da norma;
2. ligar bicos ao ramal (braços e descidas);
3. traçar o ramal até a subgeral;
4. traçar a subgeral até a VGA;
5. aplicar diâmetros.

Por isso as ferramentas vêm primeiro, uma por vez, cada uma útil sozinha em projeto real; a geração automática é montada em cima delas, e o gerador retangular de hoje passa a usar as mesmas peças.

## Respondido: arquitetura e estrutura vêm vinculadas

O engenheiro confirmou (27/10/2026): a arquitetura e a estrutura chegam como **vínculo**, e a estrutura muitas vezes vem de outros programas (IFC). Na Nova Era, o arquivo de incêndio vincula NEPN-ARQ (arquitetura), NEPN-PRE (pré-moldados), NEPN-INC-HID (hidrantes) e o arquivo do cliente; o próprio arquivo não tem paredes, forros, lajes nem vigas. O plugin lê os vínculos carregados (botão Arquitetura) e também o que estiver no próprio arquivo.
