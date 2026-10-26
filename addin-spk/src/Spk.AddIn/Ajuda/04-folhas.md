# O que as folhas trazem

O botão **Folhas** gera três conjuntos, nesta ordem no PDF:

| Folha | Conteúdo |
|---|---|
| **SPK-SETORES** | Setorização das VGAs: uma planta por nível com a hachura de cada setor numa cor (VGA 1 azul, 2 verde, 3 magenta, 4 amarelo, 5 laranja) e a legenda com o número de chuveiros de cada setor. |
| **SPK-ISO** | Isométrico geral: tudo o que está ligado aos bicos, só tubulação, com etiqueta de diâmetro nos tubos e um quadro com setores, critérios e o último cálculo. |
| **SPK-‹setor›-01** | Planta do último setor calculado com a área de cálculo hachurada, os bicos da área numerados como no memorial, o bico crítico com pressão e vazão, etiquetas de diâmetro e DESCE/SOBE nas descidas; 3D do setor com os bicos da área em destaque; mapa-chave do nível (quando há mais de um setor) e o quadro de resultado. |
| **SPK-‹setor›-02** | Corte olhando ao longo dos ramais: descida, subgeral e ramais de perfil. |

## De onde vem o visual

- **Carimbo, etiqueta de tubo, templates e legendas**: do Padrão do escritório, se foi importado. Sem ele, o primeiro carimbo do projeto, a etiqueta de tubo padrão e o estilo do SPK (tubulação em vermelho).
- **Escala**: a menor escala que faz a vista caber no espaço dela na folha.
- **Plano de corte da planta**: logo acima da rede, a partir do nível dos bicos.

## PDF

O PDF vai para a pasta **SPK** ao lado do arquivo do projeto. Se o projeto ainda não foi salvo, vai para **Documentos\\SPK\\calculo**, junto do memorial.

## Rodar de novo

As vistas e folhas do SPK (nomes começando por **SPK**) são apagadas e recriadas a cada vez. Não edite essas folhas à mão: o que você mudar nelas some na próxima rodada. Para personalizar, use o Padrão do escritório e os templates.
