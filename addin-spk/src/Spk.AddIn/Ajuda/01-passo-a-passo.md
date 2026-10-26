# Passo a passo

O SPK calcula a rede de chuveiros automáticos (sprinklers) que está no modelo, grava o resultado nos elementos e monta as folhas do projeto. A ordem abaixo é a de um projeto do começo ao fim.

## 1. Padrão do escritório (uma vez por projeto, opcional)

Botão **Padrão do escritório**, no painel Projeto. Abra ao lado, na mesma sessão do Revit, um projeto que já tenha o visual do escritório (o projeto modelo ou uma obra anterior). O SPK sugere o carimbo e a etiqueta de tubo mais usados nele, os templates de vista e as legendas; você confere e importa. As folhas passam a sair com esse carimbo, essa etiqueta, esses templates e essas legendas.

## 2. Dados do projeto (critérios de engenharia)

Botão **Dados do projeto**. Aqui o engenheiro escolhe a classe de risco, a base de cálculo (densidade e área pela Figura 42, densidade e área informadas, ou ESFR), a demanda de hidrantes, a duração, o fator C e, se tiver, o ensaio do abastecimento. O SPK nunca escolhe a classe de risco sozinho: o cálculo usa exatamente o que estiver aqui, e isso vai para o memorial.

## 3. A rede

Há dois caminhos:

- **Rede já desenhada no modelo.** Botão **Famílias**: o SPK lista as famílias do modelo e você confirma quais são bicos (com fator K e área de cobertura) e qual válvula é a VGA de cada setor (papel **Fonte**). Símbolos 2D e bicos que não entram no cálculo ficam como **Ignorar**.
- **Rede gerada pelo SPK.** **Dados do esboço** define a malha (ramais, espaçamentos, altura, bico, DN) e **Gerar** cria bicos, tubos e conexões já ligados, no ponto que você clicar. **Rede de exemplo** cria um dos cenários de teste prontos.

## 4. Calcular

Botão **Calcular**. O SPK mostra quantos bicos entram, lê a rede pelas conexões, pergunta o setor quando há mais de um (clicando na VGA, ou num bico do setor) e procura a área de operação mais desfavorável. O resultado diz se **ATENDE**, a pressão e a vazão necessárias na entrada do setor, o bico crítico e cada verificação que não passou.

O resultado fica gravado no modelo: cada bico do setor ganha o número de cálculo, o setor, se está na área de cálculo, a pressão e a vazão; cada tubo ganha o setor, a vazão e a velocidade (parâmetros **SPK …**, que podem ser usados em tabelas e etiquetas).

## 5. Memorial

Botão **Memorial**: abre o memorial de cálculo em Word, com critérios, resumo, verificações, as áreas avaliadas e todos os trechos com vazão, diâmetro, perdas e pressões. Os números dos nós são os mesmos das folhas.

## 6. Folhas

Botão **Folhas**: monta a setorização, o isométrico geral e as folhas do último setor calculado, e exporta tudo num PDF na pasta **SPK** ao lado do arquivo do projeto. Rodar de novo refaz as folhas do SPK com os dados atuais.
