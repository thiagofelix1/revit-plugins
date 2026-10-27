# Verificar o modelo

O botão **Verificar modelo** (painel Rede) confere se o SPK consegue seguir a água pelas conexões do Revit, do bico até a VGA. Ele só lê o modelo: não altera nada. Rode antes de calcular, e de novo depois de cada correção.

## Como usar

1. Clique em **Verificar modelo**. O topo da janela resume quantos bicos, tubos e conexões foram conferidos e quantos problemas apareceram.
2. Os problemas vêm em grupos, cada um com a explicação de como resolver. Abra o grupo para ver cada ocorrência, com o lugar no modelo (coordenadas e nível).
3. Escolha uma ocorrência e clique em **Mostrar** (ou dê dois cliques): a janela fecha, os elementos ficam selecionados e a vista vai até eles. **Selecionar todos do grupo** seleciona todas as ocorrências de uma vez, útil para trocar o sistema de vários tubos juntos.
4. Corrija e rode de novo. **Copiar relatório** copia a lista em texto, para mandar a alguém.

A tela **Seu projeto agora**, aqui no Guia, também mostra quantos erros e avisos o modelo tem.

## O que cada cor quer dizer

| Marca | Significado |
|---|---|
| **ERRO** | O cálculo não consegue seguir a rede por ali. Com erro, o Calcular avisa antes e só calcula ligando por proximidade se você pedir. |
| **AVISO** | O cálculo segue, mas vale conferir: pode faltar um pedaço de rede. |
| **INFO** | Só informação, por exemplo onde o SPK vai considerar a entrada da rede. |

## Os problemas e como corrigir

| Problema | O que é | Como corrigir |
|---|---|---|
| **Peças encostadas, mas não ligadas** (erro) | No desenho as peças se tocam (a menos de 5 cm), mas o Revit não as conectou. É o caso mais comum em modelos copiados ou desenhados à mão, e foi o que impediu a leitura da Nova Era. | Selecione e arraste a ponta do tubo até o conector da outra peça (o Revit liga quando o cursor mostra o conector), ou apague o trecho e redesenhe com a ferramenta de tubo. |
| **Tubo que termina no meio de outro, sem tê** (erro) | A ponta de um tubo (ou de um tê) encosta no corpo de outro tubo, sem conexão. A água não passa. | Apague o tubo que chega e redesenhe partindo do outro tubo: o Revit cria o tê sozinho. |
| **Bico sem tubo** (erro) | O bico não está ligado a nada, e nada encosta nele. | Selecione o bico e use a ferramenta **Conectar a** (Connect Into) para ligá-lo ao ramal, ou apague se sobrou. |
| **Família de bico sem conector** (erro) | A família do bico não tem conector de tubulação, então nunca vai ligar. Comum em símbolos 2D usados como bico. | Troque por uma família de sprinkler com conector. Se for só símbolo de desenho, marque o tipo como **Ignorar** em Famílias. |
| **Tubos fora de sistema de incêndio** (erro) | Tubos ligados aos bicos estão num sistema de água fria, hidrônico ou outro. Na Nova Era, 610 tubos estavam em "Fornecimento hidrônico". | Use **Selecionar todos do grupo** e troque o **Tipo de sistema** nas propriedades para um sistema de proteção contra incêndio (molhado, seco ou pré-ação). |
| **Bicos sem fator K e área confirmados** (erro) | O tipo de bico existe no modelo mas não foi confirmado em Famílias, então o SPK não sabe a vazão dele. | Em **Famílias**, marque o tipo como **Chuveiro** e informe o fator K (L/min/bar½) e a área de cobertura (m²). |
| **Bicos que não chegam a nenhuma VGA** (erro) | Seguindo as conexões a partir desses bicos, não se chega a nenhuma válvula marcada como Fonte. | Procure, no mesmo setor, peças encostadas sem ligação ou derivações sem tê entre os bicos e a VGA. |
| **Tubo ou conexão com saída livre** (aviso) | Uma ponta ficou sem continuação, tampão ou bico (por exemplo, um tê com uma boca vazia). | Confira se falta um pedaço de rede. Se a ponta for de propósito (dreno, teste), pode deixar. |
| **Nenhuma VGA marcada como Fonte** (aviso) | O mapeamento não tem válvula com o papel Fonte. | Em **Famílias**, marque a família da VGA como **Fonte**. Sem ela, o cálculo começa na ponta de tubo mais baixa de cada rede. |
| **Entrada da rede sem VGA** (info) | Sem VGA, o SPK usa a ponta de baixo da descida como entrada de cada rede. | Nada a fazer numa rede gerada pelo SPK. Num projeto real, modele e marque a VGA. |

## Redes separadas

O resumo diz em quantas **redes separadas** o modelo está: pedaços de rede que não se ligam entre si. Cada setor (cada VGA) deve ser uma rede. Muitas redes separadas num setor só indicam peças desligadas; na Nova Era eram 172.
