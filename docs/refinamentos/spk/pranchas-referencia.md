# Pranchas de referência — o que o plugin precisa gerar

Análise, prancha por prancha, do jogo de folhas de sprinkler da Mercantil Nova Era (NEPN-INC-009 a 014). Para cada folha: o que ela contém, como o Revit produz aquilo, o que o plugin já faz e o que falta. Complementa a etapa 6 de [plano-geracao.md](plano-geracao.md).

As folhas foram feitas **no Revit**, no mesmo `NEPN-INC-EX-GER-SPK-R01.rvt`: título de vista com número no círculo, carimbo como família de folha, legenda de cores e filtros. A meta é reproduzir com a API o que o projetista fez à mão nesse arquivo, não imitar um desenho de CAD.

## Resumo

| Prancha | Conteúdo principal | Hoje | Peça que falta primeiro |
|---|---|---|---|
| 009 Setorização das VGAs | 3 plantas 1:300 com hachura colorida por VGA e legenda | Nada | Setores (um sistema por VGA) e região preenchida por setor |
| 010 Sprinklers telhado | 2 plantas 1:200 da malha ESFR, mapa-chave 1:1000, tabela de sprinklers, 2 cortes 1:200 | Planta enquadrada, retângulo, bico crítico | Etiquetas de diâmetro, DESCE/DRENO, cotas, rede inclinada sob o telhado |
| 011 Sprinklers em forro | 5 plantas recortadas por ambiente 1:200, detalhe de escada 1:50 + 2 cortes, 2 mapas-chave | Nada | Layout por ambiente (Room), outros tipos de bico |
| 012 Detalhe VGAs e cortes | 5 cortes do prédio 1:200, identificador de cortes 1:1000, detalhe da VGA 1:10 com balões, 3D do barrilete | Um corte da descida, escala fixa | Família do conjunto da VGA, cortes posicionados, cotas de altura e folga |
| 013 Isométrico | 3D do sistema inteiro, da casa de bombas às pontas; tabelas de bomba; legenda; notas | Isométrico da malha gerada | Geral até a casa de bombas, etiquetas Ø em 3D, legenda e notas |
| 014 Planta e 3D da VGA 4 | Planta do setor 1:300 com a área de cálculo hachurada e numerada, 3D do setor com "Ø + comprimento", detalhe do registro de recalque | Planta do setor, retângulo, nota do bico crítico | Numeração de cálculo nos bicos, hachura da área, etiquetas "Ø100 71,11 m" |

## 009 — Setorização das VGAs

**Conteúdo.** Três plantas em 1:300 (nível telhado, docas, térreo). Cada área protegida tem uma hachura diagonal com a cor da VGA que a atende (VGA 1 azul, 2 verde, 3 magenta, 4 amarelo, 5 laranja), uma legenda das cores e o barrilete das VGAs desenhado no canto.

**Como o Revit faz.** Região preenchida (`FilledRegion.Create`) por setor, cada uma com um `FilledRegionType` na cor da VGA. A legenda pode ser uma vista de desenho (`ViewDrafting.Create`) com uma amostra de cada tipo e o texto. Uma alternativa mais "nativa" é usar Áreas com esquema de cor e `ColorFillLegend.Create`, o que também daria a área por VGA para conferir a Tabela 8. Isso fica a confirmar, porque criar esquema de área pela API é restrito.

**Hoje.** Nada. O plugin só conhece uma malha marcada `SPK-GERADO`, sem setor.

**Falta.**
- Setor como dado do modelo: um `PipingSystem` por VGA, com nome "VGA 1"… e o parâmetro de setor gravado em cada bico e tubo.
- O contorno de cada setor: envoltória dos bicos com folga de meia distância entre bicos, recortada pelas paredes do nível.
- Vista de planta por nível em 1:300, com template de vista do escritório.
- A mesma setorização em 1:1000 vira o **mapa-chave** que aparece de novo nas folhas 010, 011 e 014. É a mesma peça usada quatro vezes.

## 010 — Sprinklers telhado

**Conteúdo.**
- Planta "Nível cobertura – alta" e "– baixa", 1:200: malha ESFR completa (símbolo vermelho hachurado), ramais em vermelho, **etiqueta de diâmetro em cada trecho** (Ø65 nos ramais, Ø100 na subgeral), "DESCE" com seta nas descidas, "VEM DA VGA 2/3" nas chegadas, **cotas encadeadas** da distância entre ramais na borda (0,46 / 3,13 / 3,00…), marcas de corte A1 e A2.
- Mapa-chave da setorização em 1:1000 e legenda de cores.
- **Tabela de sprinklers**: local, norma, classificação, tipo/legenda com o símbolo, modelo, características (K, temperatura, Ø), área máxima por coluna e área máxima de cobertura por bico. Acima dela, a nota "ESFR K 25,2 projetado conforme Decreto 63.911/2018 – IT 24/2019 Tabela 5.3.4.3.1".
- **Cortes A1-A1 e A2-A2**, 1:200: o ramal **acompanha a inclinação do telhado**. Cotas do espaçamento entre bicos ao longo da água do telhado e da distância de cada duto de ar até o bico, com a legenda "dutos de ar / realocação de dutos de ar". É a verificação de obstrução (NBR 10897, Tabela 15, citada na nota).

**Como o Revit faz.** Etiquetas de tubo (`IndependentTag.Create` com uma família de etiqueta de tubo que mostre o diâmetro), símbolo genérico de anotação para DESCE e DRENO (`NewFamilyInstance` numa vista), cotas (`NewDimension` com referências dos bicos — `GetReferences(CenterLeftRight / CenterFrontBack)` — e das linhas de centro dos tubos), tabela (`ViewSchedule` dos tipos de sprinkler + `ScheduleSheetInstance.Create`).

**Hoje.** `GeradorFolhas` cria uma planta, enquadra a malha, desenha um retângulo, pinta o bico crítico e escreve uma nota. Escala 1:50 e plano de corte de 4/6 m fixos.

**Falta.**
- **Rede sob telhado inclinado**: o gerador faz uma malha plana a uma altura fixa. ESFR pede o defletor paralelo ao telhado, a 152–457 mm dele (nota da folha 013). A altura de cada bico sai do telhado no ponto (`ReferenceIntersector` para cima).
- Família de etiqueta de diâmetro e uma etiqueta por trecho, sem empilhar: uma só por trecho contínuo de mesmo diâmetro entre bicos.
- Símbolos DESCE/SOBE em todo tubo vertical; DRENO nas pontas baixas.
- Cotas encadeadas entre ramais e entre bicos, na borda do setor.
- A tabela de sprinklers vem dos **parâmetros de tipo** preenchidos pela janela de classificação (decisão 2): local, norma, classe, modelo, características e limites de área. O símbolo da coluna "tipo/legenda" vem de um componente de legenda. Ver limites da API mais abaixo.
- Cortes pelo sentido dos ramais com cotas de espaçamento ao longo da água e de distância a obstrução. A verificação de obstrução (dutos, vigas, terças) está **fora** do plano atual; entra depois, mas a folha já mostra que o projetista faz isso.

## 011 — Sprinklers em forro

**Conteúdo.** Cinco plantas 1:200 **recortadas por ambiente** (lojas e padaria, açougue/câmara resfriada/refeitório, sala de reunião, mezanino, docas). Bicos Standard K 5,6 distribuídos **dentro de cada ambiente**, cotados da parede ao bico e entre bicos nos dois eixos. Etiquetas Ø50 nos ramais, Ø100 na geral, Ø25 nos drenos. Câmara de congelados com o bico seco A1 (ponto azul). Detalhe da escada em 1:50 com cortes A e B: o tubo desce acompanhando o lance. Mapas-chave do térreo e das docas em 1:1000.

**Como o Revit faz.** O mesmo jogo de etiquetas e cotas da 010, mais vistas de planta recortadas (`CropBox`) por grupo de ambientes, ou vistas dependentes da mesma planta.

**Hoje.** Nada.

**Falta.**
- **Layout por ambiente**: ler os `Room` do nível (ou o forro), distribuir bicos respeitando o espaçamento máximo, a área máxima por bico (12,1 m² no Ordinário 2) e a distância máxima à parede. É outra regra de layout, diferente do grid da cobertura.
- **Tipo de bico por ambiente**: com forro, entre forro e telhado, câmara fria (seco). Isso sai da classificação do ambiente, que é decisão do engenheiro.
- Cotas da parede ao bico (referência da face da parede via `HostObjectUtils.GetSideFaces`).
- Vistas recortadas por ambiente e posicionadas em grade na folha.
- O detalhe da escada fica manual na primeira versão.

## 012 — Detalhe das VGAs e cortes

**Conteúdo.**
- Cinco **cortes do prédio** em 1:200 (cortes 1 a 5), com a estrutura, as porta-paletes em laranja e a rede. Cotas: espaçamento entre bicos (3,00), distância do bico à viga (0,55–0,90), altura do prédio (10,80), altura da porta-palete (6,99), **folga do topo da mercadoria até a rede (3,81)** e o ramal do dreno.
- **Identificador de cortes** em 1:1000 (planta-chave com as marcas).
- **Detalhe da VGA** em 1:10 (vista de elevação do conjunto) com **balões numerados** 1–8 e 14 e a legenda: alarme hidráulico, válvula de esfera, manômetro, retenção, borboleta, niple, união, bucha de redução.
- **3D do barrilete** das cinco VGAs com a identificação VGA1…VGA5.
- Nota: VGAs mantidas abertas com corrente e cadeado.

**Como o Revit faz.** `ViewSection.CreateSection` por corte; cotas verticais com referências de nível, do topo da porta-palete e do bico. O detalhe da VGA é um corte ou elevação recortado em volta da família do conjunto da válvula, com etiquetas que mostram o número de cada componente aninhado. O 3D é uma `View3D` com caixa de corte em volta do barrilete.

**Hoje.** Um corte só ("corte da descida"), sempre no eixo X, em 1:50.

**Falta.**
- **Família do conjunto da VGA** (a do escritório já existe no modelo da Nova Era) inserida por setor no barrilete, com os componentes como famílias compartilhadas aninhadas, para as etiquetas numeradas funcionarem.
- Posição dos cortes: atravessando cada setor no sentido dos ramais e passando pelas porta-paletes.
- **Verificação ESFR de folga**: topo da mercadoria ≥ 914 mm abaixo do defletor, defletor a 152–457 mm do teto. É verificação de norma, então pertence ao motor e entra no memorial, não só na cota. Precisa de ler as porta-paletes, que provavelmente vêm de um vínculo de arquitetura e precisam do mesmo mapeamento de famílias usado nos bicos.
- Marcas de corte reunidas numa planta-chave 1:1000.

## 013 — Isométrico

**Conteúdo.** Um 3D do sistema inteiro, em linha simples vermelha: casa de bombas, registro de recalque, geral Ø200/Ø150 até o barrilete das VGAs e todos os setores, com DRENO nas pontas. Tabelas da bomba principal (diesel, 7 500 L/min a 150 m.c.a.) e da jockey (50 L/min a 160 m.c.a.). Legenda de símbolos (desce, sobe, tipos de bico, VGA). Notas de material (aço carbono DIN 2440 acima de 65 mm, galvanizado até 2", conexões classe 165 lb) e de uso do ESFR.

**Como o Revit faz.** `View3D` isométrica com template que só mostra tubo, conexão, acessório e sprinkler, em linha vermelha. **Etiqueta em vista 3D exige a vista travada** (`SaveOrientationAndLock`).

**Hoje.** Isométrico só da malha gerada, sem etiquetas.

**Falta.**
- **Geral da casa de bombas até as VGAs**: o gerador hoje só trabalha dentro de um setor. O usuário indica dois pontos (casa de bombas e barrilete) e o plugin traça a geral.
- Etiquetas de diâmetro na geral, com a vista travada.
- Tabelas de bomba a partir dos dados de abastecimento (os mesmos que o motor usa na curva).
- Legenda e notas: vêm do template do escritório (ver limites da API).

## 014 — Planta e 3D da VGA 4

**Conteúdo.**
- Planta "Sprinklers – VGA 4" em 1:300: o setor da VGA 4, a geral saindo do barrilete pela borda, a **área de cálculo hachurada** com a nota "ÁREA DE CÁLCULO — 12 SPRINKLERS ESFR – K25", e a **numeração de cálculo** (90 a 113) escrita junto de cada bico, a mesma numeração do memorial.
- **3D do setor** com etiquetas "**Ø100 71,11 m**": diâmetro e comprimento de cada trecho reto. Os 71,11 m indicam que os ramais estão ligados à subgeral por derivação ao longo do tubo, sem quebrar o tubo.
- Geral da casa de bombas e do registro de recalque até o barrilete, com "Ø65 7,76 m", "Ø200 6,44 m".
- Mapa-chave 1:1000, legenda, notas, tabelas da bomba.
- **Detalhe do registro de recalque** (planta e vistas A/B com balões).

**Como o Revit faz.** Região preenchida para a área de cálculo; etiqueta de sprinkler que mostra um parâmetro "Nº de cálculo"; etiqueta de tubo com diâmetro e comprimento.

**Hoje.** A planta do setor com o retângulo e a nota do bico crítico. Ressalva da revisão anterior: o retângulo hoje contorna a rede inteira, não a área de cálculo.

**Falta.**
- **Gravar no modelo a numeração dos nós de cálculo** (parâmetro compartilhado no bico) e usar a mesma numeração no memorial. Hoje o memorial usa `E<ElementId>`, que não aparece em folha nenhuma.
- Hachura da área de cálculo pelo contorno dos bicos da área mais desfavorável, não pela malha toda.
- Uma folha destas **por VGA**: o jogo de folhas sai do número de setores, não fixo em SPK-010/012/013.
- O registro de recalque é detalhe típico do template.

## Fundações que servem a todas as folhas

Estas peças vêm antes das folhas, porque cada folha depende delas:

1. **A rede como dado do modelo.** Setor (sistema por VGA), número de cálculo em cada bico, e resultados do cálculo (pressão e vazão no bico; diâmetro, vazão e velocidade no tubo) em parâmetros compartilhados. Hoje o resultado fica num `resultado.json` em `%TEMP%`. As etiquetas, a tabela e a hachura da área de cálculo leem desses parâmetros.
2. **Kit do escritório num projeto modelo.** Carimbo, famílias de etiqueta (Ø; Ø + comprimento; Nº de cálculo; nome da VGA), símbolos DESCE/SOBE/DRENO, família do conjunto da VGA, detalhes típicos (VGA, registro de recalque), legenda de símbolos, notas e **templates de vista** por tipo de folha. O plugin copia o kit para o projeto (`ElementTransformUtils.CopyElements` entre documentos) e usa os templates em vez de valores no código.
3. **Templates de vista resolvem os valores fixos atuais.** Escala, disciplina, visibilidade e cores saem do template. O plano de corte da planta deixa de ser 4/6 m e passa a ser calculado pela cota real da rede do nível.
4. **Montador de folha.** Cria a folha com o carimbo, preenche os parâmetros (cliente, obra, CNPJ, lote, código no padrão `NEPN-INC-0XX-LG-SPK-PLA-R0X`, prancha "09/16"), registra a revisão (`Revision.Create`) e posiciona vistas, tabelas e legendas em regiões fixas por tipo de folha. Exporta o PDF com `Document.Export(…, PDFExportOptions)`.
5. **Classificação pelo engenheiro** (decisão de 25/10/2026). A janela de classe, hidrantes e duração alimenta o motor e os parâmetros de tipo da tabela de sprinklers.

## Limites da API e pontos a confirmar no Revit

- **Vista de legenda não pode ser criada pela API**, só duplicada de uma existente. Por isso a legenda de símbolos e as notas precisam vir do projeto modelo.
- **Etiqueta em 3D exige vista travada.** Depois de travada, a orientação do isométrico não muda sem destravar.
- **Cotas** precisam de referência geométrica válida na vista. Bico e parede são simples; a linha de centro do tubo exige ler a geometria com `ComputeReferences`. É a parte mais trabalhosa das anotações.
- **Copiar vistas de desenho entre documentos** (detalhes típicos): confirmar se `CopyElements` aceita a vista inteira ou só o conteúdo.
- **Esquema de área para a setorização**: confirmar o que a API permite criar. `FilledRegion` não tem essa restrição.

## Ordem sugerida

1. Correções do cálculo da revisão de 25/10 (área incompleta reprova; classe pelo engenheiro; unidade do K; conexões; valores fixos da Nova Era).
2. Fundação 1 e 5: setor, numeração e resultados gravados no modelo; janela de classificação.
3. Fundação 2 a 4: projeto modelo do escritório, templates de vista, montador de folha, PDF.
4. **014 por VGA** — é a folha mais próxima do que já existe e prova a fundação inteira: numeração, hachura da área, etiquetas e 3D do setor.
5. **009 e os mapas-chave** — dependem só dos setores.
6. **013** — geral até a casa de bombas e etiquetas em 3D.
7. **010** — rede inclinada sob telhado, cotas encadeadas, tabela de sprinklers.
8. **012** — família da VGA, cortes do prédio, verificação de folga ESFR.
9. **011** — layout por ambiente; detalhe de escada manual.
