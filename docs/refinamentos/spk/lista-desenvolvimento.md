# Lista de desenvolvimento — SPK até a distribuição

Uma linha por entrega, na ordem combinada. `[x]` só quando provado no Revit (à mão ou pela [suíte automática](teste-automatico.md)); o que só compila fica `[ ]`. A ordem de cada etapa só começa com o critério de pronto da anterior, salvo o que não depende do engenheiro parceiro.

Atualizada em 29/10/2026. Como o engenheiro usa o plugin de ponta a ponta: [fluxos.md](fluxos.md).

## 0. Base (feito)

- [x] Motor hidráulico: Hazen-Williams, pressão total, comprimento equivalente, árvore e anel (gradiente global), busca da área mais desfavorável, ESFR de 12 bicos. Conferido com o memorial Sprinkler-CALC da Nova Era (0,01 bar nó a nó). Mesmo modelo, mesmo resultado: a leitura segue os conectores pela ordem deles na família (o Revit muda a ordem do conjunto a cada abertura e a perda de cotovelo e válvula caía ora num lado, ora no outro, ora em lado nenhum; a Nova Era desagrupada variava 18,7 a 19,0 bar).
- [x] Add-in para Revit 2025, 2026 e 2027 (aba SPK): Dados do projeto, Famílias, Dados do esboço, Gerar, Rede de exemplo (5 cenários), Calcular, Memorial (.docx), Folhas (PDF), Guia.
- [x] Padrão do escritório importado de outro projeto: carimbo, etiqueta de tubo, templates, legendas.
- [x] Folhas: setorização, isométrico geral, planta + 3D do setor com área de cálculo, corte.
- [x] Teste automático no Revit com suíte de regressão e conferência automática (`Passou`).
- [x] Pacote de teste (`dist/`, zip com instalar.bat) para o engenheiro parceiro.

## 1. Verificar modelo e leitura estrita

Pronto quando um projeto real bem conectado passa sem erro e um projeto mal conectado mostra cada problema no lugar certo.

- [x] Verificar modelo: peças encostadas sem ligação, tubo terminando no meio de outro sem tê, bico sem tubo, família de bico sem conector, ponta aberta, tubo fora de sistema de incêndio, bico sem K e área confirmados, bico que não chega a VGA, redes separadas. Clique leva ao elemento.
- [x] Provado por sabotagem (bico desligado, tê apagado, sistema de água fria, tubo desligado) e na cópia da Nova Era (971 encostados, 169 derivações sem tê, 610 tubos em sistema hidrônico, 172 redes separadas, em menos de 1 s).
- [x] Leitura estrita: Calcular mostra os erros antes e só liga por proximidade se o engenheiro pedir.
- [ ] Lista de problemas comuns nos modelos que o escritório recebe (**depende do engenheiro**) e checagens que faltarem.
- [ ] Um projeto real **bem conectado** calculando sem proximidade (**depende do engenheiro**: ceder um projeto).
- [ ] Tubos duplicados ou sobrepostos; tubo em nível ou fase errada.
- [x] Correção assistida das peças encostadas (ver etapa 6, item 1).

## 2. Folhas completas

Pronto quando o engenheiro aceita entregar as folhas sem retocar à mão.

- [x] Carimbo: número na sequência do escritório, código da prancha por molde (sugerido pelas folhas do projeto), campos do projeto copiados das outras folhas do mesmo projeto. Número já usado por outra folha é recusado sem apagar nada. Provado na cópia da Nova Era (folhas 17 a 20, código NEPN-INC-018-LG-SPK-ISO-R00, CNPJ, lote e CREA preenchidos).
- [x] Quadro de chuveiros (tipo, K, área, quantidade) e quadro de demanda na entrada do setor (vazões, pressão em kPa e mca, duração, reserva), no isométrico.
- [x] Isométrico no padrão da NEPN-INC-013: nível Fino, vermelho, diâmetro só nos trechos principais.
- [x] Hachura da área de cálculo no formato real da área; corte e 3D só com o setor.
- [ ] Revisões no carimbo (criar ou atribuir revisão às folhas do SPK) — decidir com o engenheiro se o SPK deve mexer nisso.
- [ ] Cotas entre ramais, bicos e paredes.
- [ ] Etiqueta "Ø + comprimento" e número do bico como etiqueta (família do kit do escritório), no lugar de texto.
- [ ] Área livre do carimbo configurável por escritório (o mapa-chave pode cair sobre carimbos grandes).
- [x] Folhas com arquitetura e estrutura vinculadas: vínculos em meio-tom; sem móveis, vegetação e equipamentos quando a vista não tem template do escritório (com template, o template manda); legenda da setorização com fundo branco; recorte de anotações ligado; a planta do setor sobe de escala até caber e fica centrada na região. Falta: eixos duplicados (os mesmos eixos na arquitetura e na estrutura; a API não esconde categoria de um vínculo só) e eixos passando por cima da legenda.
- [x] Área descoberta (sem forro, laje ou telhado acima): Bicos por ambiente e Gerar rede não põem bicos e dizem o motivo.
- [x] Lote de correções do teste no Snowdon (28/10): escala das plantas descontando a folga das anotações (25,4 mm de cada lado) e quadro centrado também na setorização e no mapa-chave; corte começando 30 cm antes do tubo principal (descida ou subgeral) e com a caixa na orientação que o Revit usa (antes ele cortava atrás da parede do fim do corredor); quadros do isométrico na escala da folha (o carimbo dizia 1:1); janela de ambientes começa vazia e lembra a última escolha; tampão na ponta da subgeral; fileiras a menos de 0,9 m viram uma linha (antes 15 cm: o ramal passava entre duas linhas e deixava bicos a 14 cm, sem ligar).
- [ ] Quadro de bombas com a bomba escolhida (hoje: a demanda que ela precisa atender).
- [ ] Conferência das folhas pelo engenheiro (**depende do engenheiro**).

## 3. Validação com projetos reais

Pronto quando houver pelo menos três projetos reais conferidos pelo engenheiro como cenários da suíte.

- [ ] Cada projeto real cedido vira cenário em `testes/regressao.json`, com o resultado conferido pelo engenheiro como esperado.
- [ ] Comparar memorial do SPK com o memorial que o escritório entregou para a mesma obra (pressão, vazão, área, reserva).
- [x] Tempo de cálculo em rede grande: na cópia da Nova Era, depois de Corrigir encostados e derivações, a maior rede (276 bicos ESFR, 1 126 trechos) calcula só pelas conexões em 15 s (antes ~8 min por proximidade). ATENDE, bico crítico 2,83 bar e 606 L/min (o memorial tem 603 a 606 L/min), 18,16 bar na entrada.
- [x] Entrada sem VGA nem marca: a ponta aberta do tubo mais grosso da rede (tampão nunca), a mais baixa no empate. Antes era a mais baixa: na Nova Era era um dreno de DN 25 oito metros abaixo da geral de DN 150, e a pressão passava de 100 MPa.
- [ ] Comparar a Nova Era com o memorial ponto a ponto (**depende do engenheiro**: qual rede é a VGA 4 e onde fica a VGA de cada uma; as VGAs não estão ligadas no modelo, e o depósito aparece em três redes, de 276, 186 e 24 bicos, com a geral de DN 150 em x ≈ 10 m, y = 74 m). Depois, Marcar setor na entrada certa e conferir 14,50 bar (o memorial tem um dispositivo de 2,50 bar fora do modelo).

## 4. Pronto para distribuir

- [ ] Certificado de assinatura de código (some o "Sempre carregar" e os alertas do Windows). Compra e instalação ficam com o dono do produto.
- [ ] Instalador (MSI ou equivalente) para 2025–2027, com desinstalação.
- [ ] Núcleo comum para os próximos plugins (licença, padrão do escritório, folhas, teste automático).
- [ ] Desligar no pacote o que é de desenvolvimento (menu Avançado, teste automático).

## 5. Licença e piloto pago

- [ ] Licença por assinatura (ativação, validade, uso offline).
- [ ] Primeiro escritório pagando, com canal de suporte.

## 6. Modelagem assistida e geração automática

Os dois fluxos de uso (projeto regular e projeto complexo como a Nova Era) estão em [fluxos.md](fluxos.md). As ferramentas vêm primeiro, uma por vez, cada uma útil sozinha em projeto real; a geração automática é montada em cima delas. Esta etapa pode começar antes da 4 e da 5: os itens 1 a 4 não dependem do engenheiro para existir, só para serem validados.

Pronto quando um setor da Nova Era (ou de um projeto do engenheiro) é desenhado com as ferramentas, passa no Verificar modelo sem erro e calcula sem proximidade.

- [x] 1. Reconectar peças encostadas: botão Corrigir em Verificar modelo, um Ctrl+Z desfaz tudo. Não move tubo; cada ligação é conferida (nenhuma ligação vizinha desfeita, nenhuma peça presa movida). Nova Era: 910 de 971 ligadas em 55 s, ligações do modelo 4.600 → 5.512.
- [x] 1b. Derivação sem tê: Corrigir em Verificar modelo. Tubo chegando no corpo de outro: estica até o eixo, parte o tubo de passagem e põe o tê (cruzeta se vierem dois de lados opostos). Conexão posta por cima do tubo inteiro (tê ou luva de comprimento zero): parte o tubo nas bocas, tira o pedaço de dentro e liga. Até 3 passadas, um Ctrl+Z. Nova Era: 978 de 1 032 em 3 min, redes separadas de 1 044 para 326, nenhuma ligação desfeita. Tê mecânico sentado em cima do tubo (bocas da passagem ~3 cm fora do eixo): anda até o eixo com a bucha e o bico pendurados, a descida estica. Peça de grupo de modelo ou fixada: recusa com o nome do grupo (o Revit ignora o mover em silêncio; mexer no grupo mudaria todas as cópias). Nova Era: 978 de 1 032 como está (os tês mecânicos que sobram estão em grupos de modelo); desagrupada (1 030 grupos), 1 221 ligações e sobram 94, redes com bico de 381 para 176. Falta: bucha de redução encostada de lado (48) e recusas do Revit por cota presa (45). Ideia: botão "desagrupar e corrigir".
- [x] 2. Ligar bicos selecionados ao ramal: parte o ramal, tê, descida, ou braço + cotovelo + descida; bico fora do ramal ou acima dele é recusado com motivo; um Ctrl+Z. Provado na cena de teste (5 ligados, 1 recusado, cálculo com os 5 num ramal só). Braço e descida saem com o DN do ramal (o ajuste é o item 4); bico em pé ainda não.
- [x] Cálculo: bico em braço perpendicular conta no ramal de onde sai (antes virava um ramal à parte e a área de cálculo saía picotada).
- [x] 3. Traçar o ramal: reta pelos bicos (eixo principal, passando pela mediana lateral), na altura da subgeral, do tê nela até o último bico, e os bicos ligados pelo Ligar bicos (último por cotovelo na ponta). Recusa fileira dos dois lados da subgeral (cruzeta) e fileira quase paralela a ela. Provado na cena de teste (5 bicos, 1 de lado; cálculo num ramal só).
- [x] 3b. Traçar ramais de várias fileiras de uma vez (como o Auto Branch Line do AutoSPRINK): fileiras separadas pelo SPK, ramal na altura da subgeral (tê ou cruzeta), com subida (riser nipple) ou a uma altura fixa; bico em pé.
- [x] 4. Aplicar diâmetros por tabela (número de bicos atendidos) e conferir pelo cálculo. Botão Diâmetros, no painel Cálculo; tabelas da IT-23/2025 (12 a 14).
- [x] 5. Ler a arquitetura e a estrutura vinculadas: ambientes (contorno, piso, teto pelo primeiro forro, laje ou telhado acima), vigas (largura, fundo, topo). Botão Arquitetura, painel Projeto. Testado com vínculos gerados pelo teste; falta conferir nos vínculos reais da Nova Era (NEPN-ARQ R03 e NEPN-PRE R08), pedidos ao engenheiro.
- [x] 6. Posicionar bicos num ambiente ou contorno pela regra da NBR, junto com a conferência de espaçamento, área por bico e distância à parede. Botão Bicos por ambiente (painel Rede): Tabela 2 da IT-23/2025 (bico spray de cobertura-padrão), sala em L cortada em retângulos, defletor abaixo do forro/laje ou abaixo da viga (até 560 mm do teto); conferência dos bicos existentes. Falta: obstruções laterais (distância do bico à viga), cobertura estendida, laterais, CCAE e ESFR (o bico por vão entre vigas fundas foi feito, ver abaixo).
- [x] 7. Geração automática por contorno (contorno → bicos → ramais → subgeral → VGA), usando 2 a 6. Botão Gerar rede (painel Rede): por ambiente, bicos pela norma, subgeral com a descida na parede de entrada, ramais e ligação, diâmetros da tabela; espinha de peixe e lateral. Verificação limpa e cálculo direto. Falta: grelha; ambiente em L (a subgeral pode sair dele, vira aviso); vários ambientes num setor só: feito no modo assistido (botão Ligar à geral: o engenheiro desenha a geral e o SPK liga as redes das salas, com tê e tampão; e no automático: Gerar rede com "juntar num setor" põe os ramais paralelos ao corredor, a entrada de cada sala do lado dele, a geral pelo eixo do corredor e liga tudo. Corredor em L feito: a geral segue as duas pernas, com cotovelo no canto, e cada sala liga na perna que tem à frente. Falta: corredor em T ou em cruz (hoje a geral segue só o trecho principal, com aviso), VGA feita: na ponta do corredor ou no ponto clicado, com a geral saindo dela para os dois lados. Bicos do corredor feitos: pela norma como numa sala, ligados direto na geral com tê e descida; sem forro o defletor desce a 30 cm da laje; bico que cai no tê de uma sala anda até 40 cm; o que não couber sai, com aviso. Pergunta ao engenheiro: bico direto na geral é aceito, ou o corredor pede ramal próprio?); a malha retangular (Malha retangular) usar as mesmas peças, o que muda os valores dos cenários de exemplo; rede sob vigas altas.
- [x] **Um bico por vão entre vigas**: viga funda demais para o bico ficar abaixo dela (mais de 560 mm da laje) põe os bicos nos vãos (vigas paralelas) ou nos quadros (grelha), cada um com a sua grade. Gerar rede: com vigas paralelas, defletor 25 cm abaixo da laje, ramais nos vãos e subgeral por baixo das vigas com subida; na grelha, a rede inteira por baixo das vigas com bico em pé (com pendente, recusa explicando). Só conta viga do teto (topo até 50 cm dele). Vãos estreitos (vigotas) são recusados: um bico por vão ficaria a menos de 1,8 m do vizinho. Falta: a regra de obstrução (distância à viga × altura do defletor acima do fundo dela; hoje é aviso), a regra para vigotas e terças (pergunta ao engenheiro), viga inclinada, testar a grelha com bico em pé (sem família aqui). No Snowdon (estrutura metálica com vigotas): 184 bicos em 23 ambientes, 30 recusados por vigotas.
  - Antes desta etapa: no Snowdon (exemplo da Autodesk, arquitetura e estrutura vinculadas), 42 dos 67 ambientes foram recusados por viga (23 deles pela viga de 52 cm, que põe o defletor a 573 mm da laje; máximo 560 mm). Em prédio de concreto brasileiro, viga é a regra.
- [x] Primeiro teste à mão num projeto de terceiros (MNEVVM: arquitetura R08 e estrutura R09, Revit 2025, 79 ambientes; base congelada em `modelos-teste\SPK-MNEVVM-base.rvt`, cenários `mnevvm-*`). Corrigido: **tipo de tubo** escolhido pelo projetista na janela do Gerar rede, com o material à vista e a última escolha lembrada no projeto (antes, o primeiro tipo com tê: cobre, sem aviso); as ferramentas assistidas seguem o tipo do tubo clicado. **Diâmetro** sempre um tamanho que o tipo tem (65 mm em cobre deixava o tubo sem segmento e os diâmetros da tabela não entravam). **Grade de bicos**: os eixos ficavam paralelos quando a parede reta mais longa não estava no lado maior da sala, e os bicos saíam espalhados; recorte menor que 60 cm (pilar, degrau) não divide mais a grade. **Laje e telhado**: o Gerar rede põe o defletor a 25 cm do teto, com aviso, em vez de recusar a sala. **Nível por cota de terreno**: alturas pela origem interna (`ProjectElevation`). **Bico hospedado**: fora do padrão, e recusado com o motivo se escolhido (ficava no piso). Falta: a ponta da subgeral depois da última fileira fica com o DN inicial; treliça tratada como viga maciça; ambiente com mais de um teto (Área de vendas).
- [x] Teto alto (IT-23/2025 8.16.7, Tabelas 7, 8, 10 e 11): no Calcular, bico sob teto acima de 6,1 m confere K mínimo, densidade e área e duração da reserva; a falta reprova. O teto é o do ambiente do bico, a arquitetura só é lida se há bico a 5 m ou mais do nível, e o critério continua sendo escolha do engenheiro. Falta: cobertura estendida (Tabela 9); um critério por setor (o teto mais alto do setor vale para ele inteiro).
- [x] K não confirmado: bico que usa o K de Dados do esboço gera aviso no Calcular (primeira linha, e no memorial), no Gerar rede e no Verificar modelo; confirmar a família em Famílias tira a marca da malha gerada e o bico passa a usar o K confirmado (antes, continuava com o K do esboço).
- [x] Descida curta sob laje e telhado: o Gerar rede traça o ramal em DN 25 quando a descida tem menos de 25 cm, porque o tê de DN 65 não cabia em 15 cm e o Revit desfazia o tubo invertido: só o último bico de cada ramal ligava (Área de vendas da MNEVVM: 35 de 179; agora 179). Bicos fora do cálculo viram ATENÇÃO na primeira linha do Calcular e no memorial, sem reprovar: o resultado vale só para os ligados.
- [x] Ligar bicos: a descida no meio do ramal sai em DN 25, não no DN do ramal (também em Traçar ramais e Inserir bico), e a recusa por descida curta diz o que fazer em vez da mensagem do Revit. Com ramal de 65 mm e o bico K5.6 da Tyco a folga é de 9 cm e o tê não cabe: nas ferramentas assistidas, a recusa pede para afastar o ramal ou reduzir o DN.
- [x] Cálculo velho: cada Calcular grava uma assinatura da rede (elementos ligados, posição, DN, Dados do projeto, K e área). As Folhas recusam, o Memorial pergunta e o Guia avisa quando ela não bate. Falta: o memorial é um arquivo só para todos os projetos (`memorial.docx`): abrir depois de calcular outro projeto mostra o do outro.
- [ ] Ambiente com mais de um teto (parte sob mezanino, parte em pé-direito duplo): hoje vale o teto no ponto do ambiente, e uma viga de outro nível aparece como "viga de 4 m".
- [ ] Ambiente que não é retângulo: o posicionamento pode sair fora da Tabela 2 (o Snowdon mostrou S de 6 m no 200 e no R100; a conferência avisa); a subgeral pode sair do ambiente; fileira colada à parede da entrada recusa o ambiente.
- [x] Editar bico com a rede ligada: Inserir bico (no ramal clicado, igual ao bico mais perto da rede), Apagar bico (com o caminho só dele até o primeiro encontro, que é refeito: tubo, cotovelo, tê ou cruzeta) e Mover bico (apaga e insere no ponto novo, religado ao ramal mais perto da mesma rede). Mover vários e deslizar na linha do ramal: feitos (ver abaixo).
- [x] Conferir na planta (modo assistido): a conferência da norma desenhada na planta aberta, só como anotação da vista. Círculo cinza com o alcance de cada bico; bico fora da regra com círculo vermelho e a nota do que falta (espaçamento, área por bico, distância à parede, altura do defletor); trecho de parede ou ponto do piso sem cobertura com círculo vermelho. Rodar de novo ou Limpar conferência apaga só as marcas do SPK.
- [x] Marcar setor (modo assistido): rede sem VGA desenhada ganha nome e entrada escolhidos pelo projetista (clique no tubo da descida da VGA; a ponta solta mais perto vira a fonte). Gravado no .rvt; vale no Calcular, Diâmetros, memorial e folhas, e o "Qual setor?" lista os marcados pelo nome. Marca que perdeu a rede (tubo apagado) vira aviso no cálculo.
- [x] Ligar tubos (modo assistido): o tubo que chega e o de destino; no meio do destino, tê (com subida se a altura é outra), na ponta dele, cotovelo no canto; tubos em linha são recusados com o motivo. Mesma ligação do Ligar à geral. Falta: tubos em linha (luva), destino vertical (coluna), ligar ponta de tubo direto em bico.
- [x] Cores da rede: pinta a vista por diâmetro, setor, velocidade ou pressão (substituição gráfica por elemento, funciona com template), legenda na planta, Tirar cores desfaz só o que foi pintado.
- [x] Lista de material: resumo (metros por DN, conexões por tipo, bicos, válvulas) e tabelas do Revit "SPK — ..." que se atualizam sozinhas. Falta: lista por setor.
- [x] Mover vários bicos (mesmo deslocamento, os da frente primeiro) e deslizar o bico na linha do ramal (bico, descida e tê andam; o ramal estica). Na cruzeta não desliza (arrastaria o outro ramal): apaga e insere.
- [ ] Aviso de colisão com viga e duto, e de inclinação.
- [ ] Rede seguindo telhado inclinado. Decidido pelo engenheiro (novembro de 2026): o ramal acompanha o telhado, em vez de ficar plano com descidas de alturas diferentes.
- [ ] Setorização: a área de cada setor já é conferida no cálculo (Tabela 1 da IT-23/2025, 4 800 m² por VGA e pavimento em leve e ordinário). Falta: sugestão de cortes quando passar.
- [ ] Tubo geral até a casa de bombas.
- [x] Complementos baratos: pintar a rede por pressão ou velocidade; lista de material (feitos: Cores da rede e Lista de material).

## 7. Dimensionamento

- [ ] Subir diâmetros até atender, com a ordem de DN que o projetista usar.
- [ ] Visão de todos os setores juntos e do setor que dimensiona a bomba.
- [ ] Sugerir a bomba pela curva de abastecimento.

## Conhecido e aceito por enquanto

- O campo ESCALA do carimbo do isométrico mostra 1:1: o Revit monta esse campo sem a vista 3D. A prancha 013 da Nova Era sai igual.
- O Projeto2 tem um bico solto (333229), que a verificação aponta com razão.
