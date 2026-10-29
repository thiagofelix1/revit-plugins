# Preparar o modelo

O cálculo só é tão bom quanto a rede que ele lê. O SPK segue a água pelas **conexões do Revit**: do bico ao tubo, do tubo à conexão, da conexão ao próximo tubo, até a VGA. Para isso, o modelo precisa de:

- **Peças ligadas.** Bicos, tubos, conexões e válvulas conectados entre si, não apenas encostados. Ao desenhar com as ferramentas de tubulação do Revit isso acontece sozinho; peças copiadas, movidas ou desenhadas soltas costumam ficar desligadas.
- **Sistema de incêndio.** Tubos de sprinkler num tipo de sistema de proteção contra incêndio (molhado, seco ou pré-ação), e não em água fria, hidrônico ou outro.
- **Uma VGA por setor no caminho da água.** A válvula de cada setor modelada entre o tubo que vem da bomba e a rede de bicos, e marcada como **Fonte** em Famílias. Sem a VGA desenhada, use **Marcar setor** (painel Cálculo): clique no tubo por onde a água chega (a descida da VGA) e dê o nome do setor. O cálculo, o memorial e as folhas passam a usar essa entrada e esse nome; sem marca, cada rede separada vira "Setor 1", "Setor 2"..., com a entrada na ponta mais baixa.
- **Bicos com fator K e área.** O fator K em L/min/bar½ (K 5,6 americano equivale a 80; K 25,2 equivale a 360) e a área de cobertura de cada bico em m².
- **Diâmetros reais.** O diâmetro interno vem do tipo de tubo; sem ele, o SPK usa o nominal e avisa.

## Conferir antes de calcular

O botão **Verificar modelo** lista, item a item, o que impede a leitura pelas conexões, com o lugar no modelo. É o jeito de saber se o modelo está pronto antes de calcular. No modelo da Nova Era, por exemplo, ele aponta peças encostadas sem ligação, derivações sem tê e tubos num sistema hidrônico em vez de um sistema de incêndio.

## Quando o modelo não está ligado

Se o engenheiro mandar calcular mesmo assim, o SPK tenta ligar por proximidade: cada bico ao tubo mais próximo (até 50 cm), tubos que se encostam, ramais que encostam no meio de outro tubo, peças encostadas. Esse modo **sempre avisa** no resultado. Funciona bem num setor isolado; num prédio inteiro pode ligar o que não devia, por exemplo tubos de setores diferentes que só passam perto. Nesse caso, o certo é corrigir o modelo e calcular de novo.

## Grupos

Bicos e tubos dentro de grupos do Revit recebem o resultado normalmente: os parâmetros do SPK podem ter valor diferente em cada cópia do grupo.
