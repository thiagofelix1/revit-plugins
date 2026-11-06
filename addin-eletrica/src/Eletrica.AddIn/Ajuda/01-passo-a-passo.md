# Passo a passo

O AutoEletrica lança, verifica e dimensiona a instalação elétrica de baixa tensão pela NBR 5410, grava o resultado nos elementos do Revit e monta os entregáveis: memorial, manual do usuário, quadros de cargas, unifilar, prumada, lista de material e pranchas. A ordem abaixo é a de um projeto do começo ao fim. Cada etapa pode ser refeita: o cálculo sempre lê o modelo como ele está.

## 1. Dados do projeto (critérios de engenharia)

Botão **Dados do projeto**, no painel Projeto. Aqui o engenheiro escolhe as tensões (127/220 V, 220/380 V...), o número de fases, o esquema de aterramento (TN-S, TN-C-S, TT), a corrente de curto-circuito presumida no ponto de entrega, se a alimentação é aérea ou subterrânea, os dias de trovoada por ano, se há SPDA, a temperatura ambiente e se a edificação é habitação. As opções de cálculo também ficam aqui: aplicar fatores de demanda, resistência do eletrodo de aterramento (para o esquema TT), folga de comprimento dos circuitos, folga na ocupação dos eletrodutos e PE comum por eletroduto.

O AutoEletrica nunca escolhe esses valores sozinho: o cálculo usa exatamente o que estiver aqui, e isso vai para o memorial.

## 2. Ambientes

Botão **Ambientes**. O plugin lê os ambientes (Rooms) do arquivo e dos vínculos Revit carregados e sugere o uso de cada um pelo nome: sala, dormitório, cozinha, banheiro, área de serviço, varanda, circulação, garagem e outros. Confira e corrija. O uso define os pontos mínimos da NBR 5410 9.5.2, a potência das tomadas, onde o DR é obrigatório e quais circuitos precisam ser exclusivos. O uso confirmado fica gravado no projeto, ambiente por ambiente.

Vínculo não carregado não aparece: recarregue em Gerenciar vínculos.

## 3. Padrão, famílias e parâmetros

- **Padrão**: o padrão do escritório. Alturas (baixa, média, alta, interruptor, pé-direito), potências padrão (tomada de uso geral, tomada em área molhada, tomada de 20 A, chuveiro, ar-condicionado), corrente e número máximo de pontos por circuito, a partir de quanto o circuito vai para fase-fase, prefixos de circuito e de quadro, afastamento da parede, tipo e DN do eletroduto, por onde ele passa (teto, piso ou altura dos pontos), fio em arco ou em polilinha e as cores dos condutores. Os mínimos da norma valem por cima do padrão.
- **Famílias**: lista os tipos de família das categorias elétricas e você confirma o **papel** de cada um: tomada 10 A, 20 A ou dupla, luminária, arandela, interruptor de 1, 2 ou 3 teclas, paralelo, intermediário, chuveiro, ar-condicionado, ponto de força, quadro e caixa de passagem. O plugin lança e lê pelo papel, não pelo nome. A janela mostra a hospedagem e se a família tem conector elétrico.
- **Parâmetros**: cria no projeto os parâmetros compartilhados **AE** que o plugin preenche. As etiquetas e tabelas do escritório podem usá-los (ver o capítulo Parâmetros AE).

## 4. Quadros

- **Quadro**: coloca o quadro de distribuição com a família do papel Quadro, pela colocação normal do Revit. Cada quadro novo recebe o nome (QD-1, QD-2...) e o sistema de distribuição que bate com as tensões e fases dos Dados do projeto. Se o projeto não tem esse sistema, o plugin avisa.
- **Alimentar quadro**: clique no quadro terminal e depois no quadro que o alimenta. O plugin cria o circuito alimentador no quadro geral, com o terminal como carga. É a hierarquia medição, geral e terminais que o cálculo dimensiona.

## 5. Pontos e comandos

- **Pontos por ambiente**: para cada ambiente marcado, o plugin mostra o que a NBR 5410 9.5.2 pede (tomadas pelo perímetro e pelo uso, 600 VA nas áreas molhadas, luz no teto com a carga mínima) mais o que o padrão acrescenta (chuveiro, ar). Revise as quantidades na grade e lance. As tomadas vão espaçadas pelas paredes, longe das portas; a luz vai no centro; o interruptor fica junto da porta, já comandando a luz; o chuveiro e o ar vão na parede oposta à porta. Uma transação por ambiente; um **Ctrl+Z** desfaz tudo.
- **Inserir ponto**: escolha o papel, a família, a posição e a altura, a potência, o tipo de carga e o circuito. O Revit faz a colocação normal (clique onde quiser, **Esc** termina) e o plugin completa os dados de cada ponto colocado.
- **Comando**: clique nos interruptores, na ordem, e depois nas luminárias que eles acendem. O plugin grava o comando (S1, S2...), o tipo (simples, paralelo ou intermediário) e põe o interruptor no circuito das luminárias. O Passar circuitos conta os retornos por trecho, e o Verificar acusa luminária sem interruptor.

Com a arquitetura em vínculo, use famílias **de face** ou **livres**. Família hospedada em parede do próprio arquivo não encontra parede no vínculo; o lançamento recusa e explica.

## 6. Criar circuitos

Botão **Criar circuitos**. O plugin pega os pontos sem circuito e divide pela NBR 5410 e pelo padrão: iluminação separada das tomadas, cozinha e área de serviço com circuitos exclusivos, banheiro à parte, cada equipamento acima de 10 A sozinho e o resto por ambiente, até o limite de corrente e de pontos do padrão. A proposta mostra número, descrição (editável), tipo, ligação (fase-neutro ou fase-fase), pontos, potência, corrente e o motivo de cada divisão. Ao confirmar, os circuitos são criados no quadro escolhido. Se o conector da família tiver polos diferentes da ligação proposta, o plugin avisa.

## 7. Eletrodutos

- **Traçar eletroduto**: clique nos elementos em sequência (quadro, caixa, pontos). Entre cada dois, o eletroduto sobe à cota de passagem do padrão, anda em L na planta e desce ao destino, com o tipo e o DN do padrão e as curvas do tipo de eletroduto. Chegando num eletroduto existente, o plugin parte o eletroduto e põe um tê. **Ctrl+Z** desfaz.
- **Ligar pontos**: selecione vários pontos e clique no eletroduto, caixa ou quadro de destino. Cada ponto ganha a sua descida e o seu ramal até lá, com tê.
- **Caixas por regra**: onde um trecho entre caixas passa de 15 m (menos 3 m por curva de 90°) ou de três curvas (NBR 5410 6.2.11.1.6 b e 6.2.11.1.7), o plugin parte o eletroduto e insere a caixa de passagem do escritório. Sem família de caixa confirmada, só aponta os lugares.

## 8. Passar circuitos

Botão **Passar circuitos**, no painel Fiação. O plugin leva cada circuito do quadro até os seus pontos, e até os interruptores dos comandos, pelo menor caminho nos eletrodutos. Em cada eletroduto ele grava quais circuitos passam, os retornos e quantos condutores o trecho leva, por exemplo **5 cond. C1 +2R**. É essa contagem trecho a trecho que o cálculo usa para o agrupamento e para a ocupação.

Quando o menor caminho não é o que você quer, use **Fiação manual**: selecione eletrodutos e diga, circuito a circuito, o que deve passar por eles (forçar) e o que não pode passar (evitar). Fica gravado nos eletrodutos, e o Passar circuitos roda de novo respeitando isso.

Rode Passar circuitos de novo sempre que mudar eletrodutos, circuitos ou comandos.

## 9. Verificar modelo

Botão **Verificar modelo**, no painel Cálculo. Confere o modelo pela NBR 5410: pontos mínimos por cômodo, carga de iluminação, banheiro e volumes, chuveiro, DR obrigatório, divisão de circuitos, comandos, trechos de eletroduto, DPS, reserva do quadro, ponto sem circuito e ponto sem caminho até o quadro. Cada item traz o código, a gravidade e o item da norma; **Mostrar** seleciona os elementos no modelo. O capítulo **Verificar modelo** explica cada código.

## 10. Calcular

Botão **Calcular**. O plugin dimensiona todos os circuitos pelos seis critérios da NBR 5410, os eletrodutos e os quadros (demanda, fases, geral, DRs por grupo, DPS). O resultado fica gravado nos parâmetros AE dos circuitos, dos eletrodutos e dos pontos, e o caminho de cada circuito do Revit é alinhado ao traçado dos eletrodutos. O Calcular também grava o memorial (.docx e .md), o manual do usuário e a lista de material na pasta **Documentos\\AutoEletrica\\calculo**.

O plugin guarda uma assinatura da instalação calculada. Se o modelo mudar depois, o Guia e o Memorial avisam que o cálculo está desatualizado.

## 11. Fiação na planta

Depois do cálculo, as etiquetas já trazem seções e disjuntores:

- **Desenhar fiação**: desenha na planta aberta um fio do Revit por trecho de eletroduto com circuitos, seguindo o percurso (arcos para embutido, polilinha para aparente). É saída gráfica: o dado mestre continua nos eletrodutos. Rodar de novo apaga os fios do plugin na vista e redesenha.
- **Etiquetar**: põe na vista aberta a etiqueta de fiação de cada eletroduto e fio, e as etiquetas dos pontos, interruptores e quadros, com as famílias de etiqueta carregadas no projeto.
- **Legenda**: cria a vista de desenho com a legenda de fiação (cada circuito com condutores, seções, disjuntor e DR) e as cores dos condutores do padrão.

## 12. Resultados

- **Memorial**: abre o último memorial de cálculo em Word.
- **Manual do usuário**: abre o manual da instalação para o morador (NBR 5410 6.1.8.3).
- **Quadros**: refaz as vistas de quadro de cargas e de unifilar de cada quadro e a prumada elétrica.
- **Lista de material**: lista pelo cálculo, em vista de desenho, Markdown e CSV, mais as tabelas nativas do Revit.
- **Pranchas**: gera as pranchas AE- no carimbo do projeto. Rodar de novo refaz só as folhas AE-.

O capítulo **Entregáveis** detalha o que cada um traz.
