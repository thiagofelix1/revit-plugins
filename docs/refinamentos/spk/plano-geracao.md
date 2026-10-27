# Plano — do esboço às folhas

Como sair de um contorno no Revit (cobertura, forro, níveis) e chegar nas plantas do projeto, com a rede já ligada e o cálculo do motor SPK no meio. Cada etapa tem um critério de pronto para ir marcando.

As pranchas de referência são as do Mercantil Nova Era: setorização (009), sprinklers da cobertura (010), sprinklers do forro (011), detalhes e cortes da VGA (012), isométrico (013) e planta com isométrico de uma VGA (014).

Normas citadas por item, sem reproduzir texto. O motor e o que ele já acertou estão em [motor-prototipo.md](motor-prototipo.md).

## Situação em 26/10/2026

O acompanhamento rumo à distribuição está em [lista-desenvolvimento.md](lista-desenvolvimento.md) e os fluxos de uso em [fluxos.md](fluxos.md). Decisão de 26/10/2026: a geração automática passa a ser montada sobre ferramentas de modelagem assistida (ligar bicos ao ramal, traçar ramal, aplicar diâmetros, posicionar bicos por ambiente); as etapas 2 e 4 abaixo seguem essa ordem.

- **Provado no Revit** (à mão e pelo [teste automático](teste-automatico.md)): Rede de exemplo → Calcular → Folhas → PDF, com 12 bicos, ATENDE, crítico a 2,83 bar e 606 L/min, setorização, isométrico, planta e 3D do setor, corte, etiquetas do escritório e padrão do escritório importado (carimbo, etiqueta, templates, legendas).
- **Sem validação de engenharia**: o conteúdo das folhas e a leitura de um projeto real **bem conectado** (nunca testada; a Nova Era é o caso oposto).
- **A Nova Era** ensinou os limites da leitura por proximidade: ver [aprendizados-nova-era.md](aprendizados-nova-era.md). Decisão: Verificar modelo + leitura estrita, em vez de adaptar o plugin ao arquivo.
- **Próximos passos**, nesta ordem: Verificar modelo e leitura estrita; folhas completas (carimbo, tabelas, cotas); validação com projetos reais bem conectados como cenários de regressão; núcleo comum entre plugins, certificado de código e instalador; licenciamento e piloto pago; geração avançada (telhado inclinado, ambientes, geral até a casa de bombas) e dimensionamento.

## O que já está feito

- [x] Motor hidráulico (Hazen-Williams, árvore, grelha, área mais desfavorável, ESFR de 12 bicos).
- [x] Conferência com o memorial Sprinkler-CALC da Nova Era, fora do Revit: 12 bicos ESFR K 360, pressão nó a nó até 0,01 bar, vazão dos doze 0,4 % acima.
- [x] Add-in no Revit 2027, aba SPK: descobrir famílias, confirmar fator K e área, calcular a rede lida, abrir o memorial.
- [x] Amostragem no modelo da Nova Era: o bico crítico saiu a 2,83 bar e 606 L/min, coerente com K 360 na pressão mínima do memorial.

## O que essa amostragem não fecha

O arquivo da Nova Era tem os bicos desenhados sem o conector ligado ao tubo. A leitura só conseguiu juntar 8 bicos e 11 tubos. A área do ESFR pede 12 bicos em 3 ramais, então o comando calculou 4. A pressão na fonte (7,55 bar) não se compara aos 14,50 bar do memorial.

Gerar a rede pelo plugin evita esse buraco: cada tubo nasce de um conector ao seguinte, com cotovelo e tê. A planta deixa de ser geometria solta.

## Fluxo na faixa de opções

Os botões de descobrir famílias, calcular rede, exemplo e abrir memorial serviram para provar o motor dentro do Revit. Não são o fluxo de quem parte de um esboço.

O uso fica nesta ordem:

1. **Dados do esboço.** Classe, K em L/min/bar½, área em m², pressão mínima, C do tubo, espaçamento e altura. Uma janela.
2. **Gerar.** Bicos, VGAs e tubos já conectados.
3. **Calcular.** A rede que acabou de ser gerada. Se não atender, a lista diz o item.
4. **Resultados.** Memorial desse cálculo e, depois, as folhas.

| Projeto | Rede | Resultados |
|---|---|---|
| Dados do esboço | Rede de exemplo, Gerar | Memorial, Folhas |
| Famílias, menor | Calcular | |

**Rede de exemplo** cria os 12 bicos no ponto zero, apaga a malha marcada antes e abre a planta e o 3D já enquadrados. É a base para as folhas. **Gerar** continua para escolher o ponto e os dados da janela.

**Famílias** continua no painel Projeto, menor que os dados do esboço. Serve para um modelo que já veio desenhado, como a Nova Era. Não entra no caminho do esboço.

**Exemplo** sai da faixa principal. Fica num menu Avançado, para não parecer um passo do projetista. Calcula o galpão fictício e não lê o arquivo aberto.

A malha mínima (etapa 1) já nasce com Dados, Gerar e Calcular nessa ordem.

## Ordem

```mermaid
flowchart LR
    A["Esboço<br/>contorno e níveis"] --> B["Bicos"]
    B --> C["VGAs"]
    C --> D["Tubos já conectados"]
    D --> E["Cálculo"]
    E --> F{"Atende?"}
    F -- não --> G["Ajustar diâmetros"] --> E
    F -- sim --> H["Folhas e PDF"]
```

Não montar folha antes de uma malha pequena passar no cálculo. Folha em cima de tubo solto repete o erro da amostragem.

## 1. Malha mínima conectada

Um retângulo só, sem o prédio da Nova Era. Serve para provar o caminho inteiro.

Entrada: um contorno retangular num nível, altura da rede, fator K, área de cobertura, pressão mínima e um ponto de fonte.

- [x] Faixa na ordem Dados do esboço, Rede de exemplo, Gerar, Calcular, Memorial. Famílias no painel Projeto. O cálculo do galpão fictício fica no menu Avançado.
- [x] Botão Rede de exemplo: malha no ponto zero, vistas "SPK — Planta exemplo" e "SPK — Rede exemplo", tubos e sprinklers visíveis, 3D enquadrado.
- [x] Colocar os bicos num grid, com o espaçamento da janela, usando uma família de sprinkler já carregada no projeto.
- [x] Criar ramais, uma subgeral e um trecho até a fonte com `Pipe.Create` entre conectores.
- [x] Inserir cotovelo e tê nos encontros, também por conector. Exige um tipo de tubo com tê nas preferências de roteamento; o modelo padrão do Revit não tem.
- [x] Conferir no próprio modelo que cada bico tem sistema de tubulação preenchido.
- [x] Rodar o motor nessa rede, sem aproximação por distância (15 conexões com comprimento equivalente).
- [x] A área de operação acha 12 bicos, 4 em cada um de 3 ramais, quando o grid for grande o bastante para isso.

Pronto quando o comando de calcular a rede não precisa encostar bico em tubo por proximidade, e o aviso de "bicos fora da rede" fica em zero.

## 2. Esboço de verdade

O contorno deixa de ser um retângulo digitado e passa a vir do modelo.

- [ ] Ler o contorno da cobertura e o do forro (piso, telhado ou ambiente fechado). Ignorar contorno aberto.
- [ ] Dois planos independentes: bico de telhado e bico de forro não caem no mesmo setor sem regra explícita.
- [ ] Respeitar afastamento de parede e espaçamento máximo. A primeira versão não desvia de viga nem de terça.
- [x] Parâmetros do esboço numa janela: K em L/min/bar½, área em m², DN. Classe, pressão mínima e C do tubo ficam em Dados do projeto, que é critério do engenheiro. K abaixo de 40 é recusado no cálculo como provável K americano.

Pronto quando um galpão retangular simples, desenhado no Revit, recebe os bicos sem editar coordenada à mão.

## 3. Setorização em VGAs

- [ ] Dividir a área pelo limite da Tabela 8 (coluna por pavimento).
- [ ] Cortar de preferência numa linha de estrutura, quando houver pilar ou viga no caminho.
- [ ] Um sistema de tubulação por VGA, com nome visível no Revit. Hoje o setor é o parâmetro compartilhado "SPK Setor" gravado ao calcular, com o nome da VGA (marca ou comentário).
- [x] A fonte de cada setor é a VGA, não a ponta de tubo mais baixa. A leitura não atravessa outras VGAs; com várias, Calcular pede para clicar na do setor.

Pronto quando dois retângulos grandes viram dois sistemas, e o cálculo de um não mistura bico do outro.

## 4. Traçado

Espinha de peixe primeiro. Grelha fica para depois.

- [ ] Ramal na fila de bicos, subgeral cruzando os ramais, geral até a VGA.
- [x] Diâmetro inicial único e conhecido (DN em Dados do esboço), para o cálculo ter diâmetro interno real.
- [x] Comprimento equivalente das conexões entra no trecho: cotovelo pelo ângulo, tê e cruzeta só no ramo que desvia, válvulas pelo nome da família.
- [x] C do tubo vem de Dados do projeto. O padrão de ensaio é 120.

Pronto quando a rede gerada, lida de volta pelos conectores, reproduz o grafo que o gerador acabou de criar: mesmo número de bicos, de tubos e de ramais.

## 5. Cálculo em cima do que foi gerado

O motor não muda. Muda só a origem da rede.

- [x] Busca da área mais desfavorável na rede gerada. Área que não cabe nos ramais reprova (decisão de 25/10/2026).
- [x] Se não atender, a lista de verificações diz o item. A primeira versão não muda diâmetro sozinha.
- [x] Resultados gravados no modelo: número de cálculo, setor, área de cálculo, pressão e vazão no bico; setor, vazão e velocidade no tubo (parâmetros compartilhados "SPK …").
- [x] Memorial `.docx` do setor, o mesmo gerador de hoje, com a numeração que aparece nas folhas.

Pronto quando uma malha gerada de 12 bicos ESFR K 360, com pressão mínima 2,83 bar, entrega o bico crítico nessa pressão e a vazão de um bico perto de 603 a 606 L/min. A vazão dos doze fica na ordem de 7 500 L/min. A pressão na fonte depende dos diâmetros e dos comprimentos equivalentes. Ela não precisa bater 14,50 bar enquanto o traçado não for o da Nova Era.

## 6. Folhas

Só depois do critério da etapa 5. O carimbo é o da família que o escritório já usa. O plugin não desenha o carimbo.

A análise detalhada de cada prancha da Nova Era (conteúdo, mecanismo do Revit, o que existe e o que falta) está em [pranchas-referencia.md](pranchas-referencia.md).

| Folha | O que o plugin cria | Depende de |
|---|---|---|
| Cobertura e forro | Vista de planta por nível, filtros por pressão, etiquetas de diâmetro, vazão e pressão, retângulo da área crítica | Etapa 5 gravada no modelo |
| Setorização | Planta colorida por sistema de tubulação, uma cor por VGA | Etapa 3 |
| Isométrico | Vista 3D só com os tubos e bicos do setor, diâmetro e comprimento do cálculo | Etapa 4 |
| Corte da VGA | Corte no ponto da válvula | Etapa 3 |
| Detalhe típico | Instância do detalhe do escritório, colocado na folha | Família já carregada |
| PDF | Exportar as folhas criadas | As vistas acima |

O botão Folhas, no painel Resultados, gera o que o modelo permite, cada conjunto numa subtransação (se um falha, os outros ficam e o motivo aparece no fim):

- **SPK-SETORES** (009): uma planta por nível com a hachura colorida de cada VGA, todas na mesma escala, e a legenda numa vista de desenho. Os setores são lidos pelo mesmo leitor do cálculo, uma VGA por vez; o contorno é a união das coberturas dos bicos (`Contorno`, no motor).
- **SPK-ISO** (013): tudo o que está ligado aos bicos por conector, sem a tubulação de outros sistemas, travado e com etiqueta de diâmetro nos tubos mais longos.
- **SPK-<setor>-01** (014): planta do último setor calculado com a área de cálculo hachurada, bicos da área numerados como no memorial, bico crítico com pressão e vazão, etiquetas de diâmetro, DESCE/SOBE; 3D do setor etiquetado; mapa-chave do nível quando há mais de um setor; quadro de resultado.
- **SPK-<setor>-02**: corte no sentido dos ramais.
- PDF `SPK-folhas.pdf` na pasta `SPK` ao lado do arquivo. Se o projeto não foi salvo, o PDF vai para `Documentos\SPK\calculo`, a mesma pasta do memorial.

Escala, recorte e intervalo de vista saem da rede. Se o projeto tiver templates de vista chamados "SPK Planta", "SPK 3D", "SPK Corte" e "SPK Setorização", eles são aplicados; sem eles, o plugin usa um estilo padrão (tubulação em vermelho). O carimbo é o primeiro carregado no projeto. O detalhe típico da válvula continua sendo a família do escritório: o plugin ainda não posiciona esse bloco.

Tudo isto compila para o Revit 2025–2027, mas ainda **não foi rodado dentro do Revit**. O roteiro de teste está em [roteiro-teste.md](roteiro-teste.md).

- [x] Uma folha de planta do setor, com a área de cálculo hachurada.
- [x] Uma folha de isométrico do mesmo setor e um isométrico geral.
- [x] Uma folha de setorização, com uma cor por VGA (testada com um setor; várias VGAs dependem de um modelo com as VGAs ligadas à rede).
- [x] PDF dessas folhas numa pasta do projeto.
- [ ] Etiqueta "Ø + comprimento" e "Nº de cálculo" como famílias de etiqueta do kit do escritório; hoje a etiqueta de tubo é a padrão do projeto e o número do bico é texto.
- [ ] Cotas encadeadas entre ramais e bicos, tabela de sprinklers, rede sob telhado inclinado (010).
- [ ] Cortes do prédio, família da VGA e folga ESFR (012); layout por ambiente (011).

## 7. Dimensionar

Fora da primeira entrega. Entra quando o traçado conectado e as folhas de um setor já existem.

- [ ] Subir diâmetros até todas as verificações atenderem, com a ordem de DN que o projetista usar.
- [ ] Sugerir o ponto da bomba a partir da curva de abastecimento informada.

## Fora deste plano

- Ler a Nova Era antiga até os 715 bicos virarem uma rede só. O arquivo não tem o conector ligado. A validação do motor dessa obra continua sendo o `nova-era.json`.
- Grelha como traçado gerado.
- Desvio de viga, terça e obstáculo.
- Bico de forro e bico de telhado somados na mesma área.
- SINAPI.
- Várias ITs estaduais. A primeira setorização usa a Tabela 8 já codificada e a regra ESFR já usada no memorial (12 bicos, 4 por ramal, pressão mínima informada).

## Como marcar

A etapa em curso é a **1. Malha mínima conectada**. As outras não começam antes do critério de pronto da anterior. Ao fechar uma etapa, trocar `[ ]` por `[x]` neste arquivo.
