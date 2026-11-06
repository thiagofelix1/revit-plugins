# Entregáveis

Tudo sai do último cálculo. Se o modelo mudou depois dele, o Guia e o Memorial avisam: rode **Calcular** de novo antes de gerar.

## Arquivos (pasta Documentos\\AutoEletrica\\calculo)

| Arquivo | O que traz |
|---|---|
| **Memorial de cálculo** (.docx e .md) | Dados do projeto e premissas, cada quadro com demanda, fases, geral, DRs e DPS, cada circuito com os seis critérios, cada eletroduto com ocupação e DN, as verificações e os itens da norma citados. Abre pelo botão **Memorial**. |
| **Manual do usuário** (.docx e .md) | Exigido pela NBR 5410 6.1.8.3, em linguagem para o morador: o que cada disjuntor desliga, o DR e o teste mensal, o DPS, a advertência do quadro, cuidados e manutenção. Abre pelo botão **Manual do usuário**. |
| **Lista de material** (.md e .csv) | Condutores por seção e função a partir dos trechos, com 10 % de folga; eletrodutos por tipo e DN; disjuntores; DRs; DPS; pontos e quadros. O .csv abre no Excel. |

## Vistas no Revit

| Botão | Vistas criadas |
|---|---|
| **Quadros** | AE — Quadro de cargas de cada quadro (circuitos com fase, seções, disjuntor, DR e queda; demanda por categoria; cargas por fase; proteções), AE — Unifilar de cada quadro (entrada, geral, DPS, DRs com os seus circuitos, ramos com disjuntor e condutores, quadros alimentados) e AE — Prumada elétrica (níveis, quadros, alimentadores). Também o quadro de cargas nativo do Revit, quando o projeto tem o modelo de quadro. |
| **Lista de material** | AE — Lista de material, mais tabelas nativas do Revit de eletrodutos por diâmetro, conexões, pontos, luminárias, interruptores e quadros. As tabelas nativas atualizam sozinhas. |
| **Legenda** | AE — Legenda de fiação: cada circuito com número, descrição, quadro, condutores e seções, disjuntor e DR, e as cores dos condutores do padrão (6.1.5.3). |
| **Desenhar fiação** | Fios do Revit na planta aberta, um por trecho de eletroduto com circuitos. |
| **Etiquetar** | Etiquetas de eletroduto, fio, tomada, luminária, interruptor e quadro na planta aberta. |

Rodar um botão de novo refaz as vistas dele com os dados atuais.

## Pranchas

Botão **Pranchas**. Gera, no primeiro carimbo do projeto:

- **AE-PL-nn**: uma planta por nível com os elementos elétricos, recortada na área deles, com os vínculos em meio-tom e a legenda de fiação ao lado. Se o projeto tem um template de vista chamado **AE — Planta**, a planta usa ele.
- **AE-QD-nn**: uma folha por quadro, com o quadro de cargas e o unifilar.
- **AE-DOC-01**: prumada, lista de material e tabelas.

As folhas do plugin ficam registradas no projeto. Rodar de novo apaga e refaz só as folhas AE-; as folhas do escritório não são tocadas.

## Antes de entregar

1. **Verificar modelo** sem erros.
2. **Calcular** com o resultado ATENDE, e o Guia dizendo que o cálculo está atualizado.
3. **Etiquetar** e **Desenhar fiação** nas plantas que vão para as pranchas.
4. **Quadros**, **Lista de material** e **Pranchas**, nesta ordem.
5. Revise o memorial e o manual no Word.
