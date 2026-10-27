# O que as folhas trazem

O botão **Folhas** gera três conjuntos, nesta ordem no PDF:

| Folha | Conteúdo |
|---|---|
| **SPK-SETORES** | Setorização das VGAs: uma planta por nível com a hachura de cada setor numa cor (VGA 1 azul, 2 verde, 3 magenta, 4 amarelo, 5 laranja) e a legenda com o número de chuveiros de cada setor. |
| **SPK-ISO** | Isométrico geral: tudo o que está ligado aos bicos, em nível de detalhe Fino (tubos, conexões e válvulas com a forma real) e em vermelho, com o diâmetro dos trechos principais (geral, colunas, subgerais e descidas; os ramais têm o diâmetro na planta do setor), notas com setores e critérios, o **quadro de chuveiros** (tipo, K, área, quantidade) e o **quadro de demanda** na entrada do setor (vazões, pressão em kPa e mca, duração e reserva). |
| **SPK-‹setor›-01** | Planta do último setor calculado com a área de cálculo hachurada, os bicos da área numerados como no memorial, o bico crítico com pressão e vazão, etiquetas de diâmetro e DESCE/SOBE nas descidas; 3D do setor com os bicos da área em destaque; mapa-chave do nível (quando há mais de um setor) e o quadro de resultado. |
| **SPK-‹setor›-02** | Corte olhando ao longo dos ramais: descida, subgeral e ramais de perfil. |

## De onde vem o visual

- **Carimbo, etiqueta de tubo, templates e legendas**: do Padrão do escritório, se foi importado. Sem ele, o primeiro carimbo do projeto, a etiqueta de tubo padrão e o estilo do SPK (tubulação em vermelho).
- **Escala**: a menor escala que faz a vista caber no espaço dela na folha.
- **Plano de corte da planta**: logo acima da rede, a partir do nível dos bicos.

## Carimbo

O carimbo é a família do escritório; o SPK preenche os campos da folha:

- **Número**: na sequência do escritório a partir do número inicial do Padrão do escritório (sem ele, SPK-SETORES, SPK-ISO...). Se um número já for de outra folha do projeto, o SPK não renumera e avisa: nunca apaga nem mexe numa folha que não é dele.
- **Código da prancha**: pelo molde do Padrão do escritório, no parâmetro indicado ({tipo} vira ISO no isométrico e PLA nas outras).
- **Campos do projeto** (CNPJ, lote, uso, registro do responsável, desenhado e verificado por): copiados das outras folhas **deste** projeto quando elas têm o mesmo valor. Nunca vêm de outro projeto.

## PDF

O PDF vai para a pasta **SPK** ao lado do arquivo do projeto. Se o projeto ainda não foi salvo, vai para **Documentos\\SPK\\calculo**, junto do memorial.

## Rodar de novo

As vistas e folhas do SPK são apagadas e recriadas a cada vez (o SPK guarda no modelo quais folhas são dele, mesmo renumeradas). Não edite essas folhas à mão: o que você mudar nelas some na próxima rodada. Para personalizar, use o Padrão do escritório e os templates.
