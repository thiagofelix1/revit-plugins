# O que o modelo da Nova Era ensinou (25 e 26/10/2026)

Rodamos o plugin na cópia do `NEPN-INC-EX-GER-SPK-R01.rvt`, primeiro à mão e depois pelo [teste automático](teste-automatico.md). O motor já tinha sido conferido contra o memorial dessa obra; aqui o assunto é **ler o modelo**.

## Como o modelo está

| Achado | Consequência |
|---|---|
| Nenhum conector ligado: bicos, tubos, conexões e válvulas estão encostados, mas o Revit não sabe que se ligam. | A leitura pelos conectores não acha rede nenhuma. Só a leitura por proximidade (plano B) encontra alguma coisa. |
| Tubos de sprinkler classificados como **Fornecimento hidrônico**, em 3.822 sistemas de um tubo só. | O filtro "só tubos de incêndio" descartava quase tudo. |
| Ramais ligados à subgeral **no meio do tubo** (subgeral de 71 m inteira). | Junção ponta-com-ponta não liga os ramais. |
| Parte da rede dentro de **grupos**. | Gravar resultado no bico exigia o parâmetro variar entre grupos. |
| VGAs não existem como família de VGA: são válvulas de gaveta de 4" (`405-NRS-RW-4`), cada uma entre flanges e conexões, com **tubos de comprimento zero** encostados. | A ponta do tubo caía no flange, não na válvula, e o tubo vazio interrompia a busca. |
| O barrilete das VGAs fica perto da casa de bombas (x ≈ 127 m) e a rede dos bicos não chega a ele; a geral desce e termina com um pedaço solto. | Nenhuma VGA alcança os bicos pelo modelo. |
| Duas válvulas de 4" (no prumo de x ≈ 8 m) alimentam a linha das docas, de bicos de forro. | "Nenhum bico ligado" ali é o resultado certo quando os bicos de forro estão marcados como Ignorar. |

## O que o plugin passou a fazer (útil para qualquer modelo)

- Usar tubos de qualquer sistema quando nenhum de incêndio chega aos bicos, avisando para corrigir a classificação.
- Ligar ramal que encosta no meio de outro tubo, atravessar conexões e válvulas, ligar peças encostadas e seguir através de tubo de comprimento zero.
- Guardar o tubo de cada trecho, para etiquetar, marcar descida e gravar vazão e velocidade.
- Gravar resultado em bico e tubo dentro de grupo.
- Separar setores por pedaço de rede quando não há VGA alcançável, e o Calcular perguntar qual setor.
- Dizer a que distância a VGA está da rede, e parar a busca na primeira área com demanda irreal, dizendo quais bicos estavam nela.

## O limite

Com tudo isso, um setor isolado (400 bicos) foi lido e calculado de forma coerente: crítico a 2,83 bar e 606 L/min. O prédio inteiro, não: a leitura por proximidade juntou os setores numa rede só de 715 bicos e algumas áreas pediram mais de 100 MPa, sinal de ligação errada entre tubos que só passam perto.

**Decisão (26/10/2026):** não adaptar o plugin para "passar" neste arquivo. O caminho é:

1. **Verificar modelo**: um comando que aponta, no Revit, tubo solto, vão, peça desligada, válvula fora do caminho, bico sem tubo e sistema errado, para o projetista corrigir.
2. **Leitura estrita pelos conectores** como padrão; a leitura por proximidade vira modo assistido, sempre com aviso.
3. Validar o fluxo inteiro em projetos **bem conectados**, conferidos pelo engenheiro, e transformá-los em cenários de regressão do teste automático.
