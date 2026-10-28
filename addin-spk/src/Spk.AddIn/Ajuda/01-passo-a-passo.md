# Passo a passo

O SPK calcula a rede de chuveiros automáticos (sprinklers) que está no modelo, grava o resultado nos elementos e monta as folhas do projeto. A ordem abaixo é a de um projeto do começo ao fim.

## 1. Padrão do escritório (uma vez por projeto, opcional)

Botão **Padrão do escritório**, no painel Projeto. Abra ao lado, na mesma sessão do Revit, um projeto que já tenha o visual do escritório (o projeto modelo ou uma obra anterior). O SPK sugere o carimbo e a etiqueta de tubo mais usados nele, os templates de vista e as legendas; você confere e importa. As folhas passam a sair com esse carimbo, essa etiqueta, esses templates e essas legendas.

Na mesma janela ficam o **número da primeira folha** do SPK e o **código da prancha** (ex.: `NEPN-INC-{numero:000}-LG-SPK-{tipo}-R00`, com o parâmetro da folha onde ele vai). O SPK sugere os dois pelas folhas que o projeto já tem. Em branco, as folhas ficam SPK-SETORES, SPK-ISO...

## 2. Dados do projeto (critérios de engenharia)

Botão **Dados do projeto**. Aqui o engenheiro escolhe a classe de risco, a base de cálculo (densidade e área pela Figura 42, densidade e área informadas, ou ESFR), a demanda de hidrantes, a duração, o fator C e, se tiver, o ensaio do abastecimento. O SPK nunca escolhe a classe de risco sozinho: o cálculo usa exatamente o que estiver aqui, e isso vai para o memorial.

## 3. Arquitetura e estrutura (vínculos)

Botão **Arquitetura**, no painel Projeto. O SPK lê a arquitetura e a estrutura do arquivo e dos vínculos Revit carregados (um IFC vinculado, como a estrutura feita no TQS ou no Eberick, também vale): cada ambiente com a área, a cota do piso e o que forma o teto (forro, laje ou telhado, o primeiro acima do ambiente), e as vigas com largura, fundo e topo. Nada muda no modelo: a janela mostra o que o SPK enxerga, para conferir antes de posicionar bicos. Vínculo não carregado aparece com o nome: recarregue em Gerenciar vínculos.

## 4. A rede

Há dois caminhos:

- **Rede já desenhada no modelo.** Botão **Famílias**: o SPK lista as famílias do modelo e você confirma quais são bicos (com fator K e área de cobertura) e qual válvula é a VGA de cada setor (papel **Fonte**). Símbolos 2D e bicos que não entram no cálculo ficam como **Ignorar**.
- **Rede desenhada com ajuda do SPK** (painel Rede). **Bicos por ambiente**: marque os ambientes da arquitetura e o SPK põe os bicos pela regra da norma (área por bico, distância entre bicos e até a parede, Tabela 2), com o defletor 10 cm abaixo do forro ou da laje, ou logo abaixo da viga quando há viga no ambiente; o botão **Conferir os bicos existentes** faz a mesma conferência nos bicos que já estão lá, sem mudar nada. Depois desenhe a subgeral. **Traçar ramais**: selecione os bicos (pode arrastar uma janela sobre vários) e a subgeral. O SPK separa as fileiras e traça um ramal por fileira, cruzando a subgeral, até o último bico. Na janela você escolhe a altura do ramal: na altura da subgeral (tê, ou cruzeta com bicos dos dois lados), acima ou abaixo dela com uma subida, ou a uma altura fixa acima do piso; e se liga os bicos agora ou só traça os ramais. Bico pendente fica abaixo do ramal; bico em pé, acima. **Ligar bicos**: com o ramal já desenhado, selecione os bicos e o ramal; o SPK parte o ramal, põe o tê e faz a descida até cada bico, ou o braço, o cotovelo e a descida quando o bico está de lado, e um cotovelo no último bico, na ponta do ramal. O que não for feito aparece com o motivo; Ctrl+Z desfaz tudo.
- **Rede gerada pelo SPK.** **Gerar rede**: marque os ambientes da arquitetura e escolha o arranjo (espinha de peixe, com a subgeral no meio, ou lateral, com a subgeral junto à parede). Em cada ambiente o SPK põe os bicos pela norma, a subgeral com a descida até o piso na parede de entrada, os ramais, a ligação dos bicos e os diâmetros da tabela, usando as mesmas ferramentas dos botões ao lado. A rede fica acima do defletor pelo drop pedido (30 cm), sem encostar na laje; o ambiente onde ela não cabe é recusado com o motivo. **Várias salas num setor**: gere a rede das salas e desenhe a geral com a ferramenta Tubo, da VGA pelo corredor, passando em frente às salas. Selecione a geral e clique **Ligar à geral**: cada rede solta perde a descida até o piso e ganha um tubo até a geral, com um tê nela; a ponta da geral ganha tampão, e a entrada do setor fica na ponta mais baixa (a descida da VGA). Depois, **Diâmetros**, **Verificar modelo** e **Calcular**. Sem arquitetura, **Malha retangular** cria uma malha no ponto que você clicar, com a geometria de **Dados do esboço**, e **Rede de exemplo** cria um dos cenários de teste prontos.

## 5. Verificar o modelo

Botão **Verificar modelo**, no painel Cálculo. Confere se o SPK consegue seguir a água pelas conexões: peças encostadas mas não ligadas, tubo que termina no meio de outro sem tê, bico solto, ponta aberta, tubo fora de sistema de incêndio, bico sem fator K confirmado e bico que não chega a nenhuma VGA. Cada item diz como resolver; **Mostrar** seleciona os elementos e leva a vista até eles, e **Corrigir** liga de uma vez as peças encostadas. Numa rede gerada pelo SPK, a lista sai vazia. O capítulo **Verificar o modelo** explica cada problema e como corrigir.

## 6. Diâmetros (risco leve e ordinário)

Botão **Diâmetros**, no painel Cálculo. Com a rede ligada, o SPK conta quantos bicos cada trecho alimenta e põe em cada tubo o DN da tabela da norma (IT-23/2025, Tabelas 12 a 14, que reproduzem o método por tabela da NBR 10897): no ramal, DN 25 até 2 bicos, 32 até 3, 40 até 5, e assim por diante até a descida. Antes de mudar, mostra quantos tubos vão para cada DN e os avisos; no fim, oferece **Calcular agora** para conferir.

A tabela é o ponto de partida. Em sistema novo ela só vale como método abaixo de 465 m², com K 80 e rede em árvore; fora disso o SPK avisa, e quem decide é o cálculo. Rede em grelha (malha fechada) e risco extra não têm tabela. Se o tubo do modelo não tem o DN da tabela (DN 90, por exemplo), fica o tamanho seguinte que ele tiver.

## 7. Calcular

Botão **Calcular**. Se o modelo tiver erro de modelagem, o Calcular mostra os problemas antes e pergunta se quer vê-los ou calcular mesmo assim (ligando as peças por proximidade, com aviso no resultado). O SPK mostra quantos bicos entram, lê a rede pelas conexões, pergunta o setor quando há mais de um (clicando na VGA, ou num bico do setor) e procura a área de operação mais desfavorável. O resultado diz se **ATENDE**, a pressão e a vazão necessárias na entrada do setor, o bico crítico e cada verificação que não passou.

O resultado fica gravado no modelo: cada bico do setor ganha o número de cálculo, o setor, se está na área de cálculo, a pressão e a vazão; cada tubo ganha o setor, a vazão e a velocidade (parâmetros **SPK …**, que podem ser usados em tabelas e etiquetas).

## 8. Memorial

Botão **Memorial**: abre o memorial de cálculo em Word, com critérios, resumo, verificações, as áreas avaliadas e todos os trechos com vazão, diâmetro, perdas e pressões. Os números dos nós são os mesmos das folhas.

## 9. Folhas

Botão **Folhas**: monta a setorização, o isométrico geral e as folhas do último setor calculado, e exporta tudo num PDF na pasta **SPK** ao lado do arquivo do projeto. Rodar de novo refaz as folhas do SPK com os dados atuais.
