# Perguntas para os dois engenheiros e o que pedir de material

Antes de escrever código. As respostas decidem versão do Revit, famílias, formato das tags, o que é "básico" para eles e contra o que vamos testar o cálculo.

## Sobre o fluxo deles hoje

1. Em que software fazem o elétrico hoje (Revit puro, Revit com plugin, QiBuilder, AutoCAD com PRO-Elétrica ou Lumine)? Quanto tempo leva um sobrado, um prédio de 8 andares, um galpão, por etapa (pontos, circuitos, eletrodutos, fiação, dimensionamento, quadros, unifilar, pranchas, memorial)?
2. Já testaram JC Tools, RACTools, Nômade BIM, PRO-Elétrica Revit ou Circuito Livre? O que faltou ou irritou? Por que não usam (preço, confiança no cálculo, famílias, bugs)?
3. Qual versão do Revit usam e quando atualizam? (Hoje o add-in do SPK atende 2025–2027.)
4. A arquitetura e a estrutura chegam vinculadas, como no SPK? Vêm em RVT ou IFC?
5. Que tipos de projeto mais fazem: residencial unifamiliar, multifamiliar, comercial, industrial, galpão? Tensão típica (127/220 ou 220/380)? Esquema de aterramento que adotam (TN-C-S com PEN separado na entrada, TT)?
6. Qual distribuidora atende a maioria dos projetos? Que norma de padrão de entrada usam (ND-5.1 Cemig, GED Enel, NTC Copel…)? Fazem o cálculo de demanda da distribuidora à mão ou em planilha?

## Sobre o "básico" que pediram

7. "Passagem de fiação": o que exatamente querem ver? Fio desenhado em planta com as setinhas (F, N, R, T), ou o eletroduto com a lista de circuitos que passam nele, ou os dois? Como indicam hoje quantos fios passam em cada trecho?
8. Formato da tag de fiação que usam (exemplo real de uma planta deles): "3#2,5(2,5) C1"? Com retorno? Por trecho ou por circuito? Formato da tag de ponto (circuito, potência, altura)? Legenda de fiação por prancha?
9. Quais famílias usam para tomada, interruptor (simples, paralelo, intermediário), luminária, quadro, caixa de passagem e eletroduto? Têm parâmetros de potência, tensão, altura, circuito? Têm conectores elétricos configurados? (Pedir o template deles e algumas famílias.)
10. Símbolos: seguem a antiga NBR 5444 ou um padrão próprio do escritório?

## Sobre o cálculo

11. Como dimensionam hoje (planilha própria, AltoQi, PRO-Elétrica)? Quais critérios aplicam de fato: capacidade com FCT e FCA, queda, seção mínima, coordenação com o disjuntor, curto-circuito no quadro, curto mínimo no fim do circuito, I²t, seletividade? Calculam Zs para o seccionamento automático?
12. De onde tiram a corrente de curto presumida (distribuidora, estimativa pela potência do trafo, valor fixo)?
13. Fator de agrupamento: usam um global ou por trecho?
14. Que fabricante de disjuntor e DR especificam (curvas, I²t disponíveis)? Especificam DPS? Com que critério?
15. Tratam harmônicas (neutro carregado, 0,86)? Em que projetos?
16. O que o memorial de cálculo precisa conter para a prefeitura, o condomínio ou o cliente aceitarem? Têm um modelo?
17. Fazem o manual do usuário (6.1.8.3) e o checklist de verificação final? Alguém pede?

## Sobre a geração automática

18. Se o plugin lesse a arquitetura vinculada e propusesse pontos (9.5.2), circuitos (9.5.3), quadros e o traçado dos eletrodutos, o que eles aceitariam automático e o que sempre vão querer decidir na mão (posição do quadro, altura de tomadas, caminho pelo forro ou pela parede)?
19. Regras de escritório para posicionar: altura de tomada baixa/média/alta, distância do interruptor à porta, lado da abertura, tomada sobre bancada, eletroduto no teto ou na parede, caixas a cada quantos metros.
20. Embutido em alvenaria, em laje ou pelo forro? Eletrocalha em comercial? Drywall?

## Material a pedir

- **Três projetos aprovados e executados**, de portes diferentes (residência, prédio, comercial ou galpão), cada um com: modelo Revit (ou DWG) e vínculos, quadro de cargas, memorial de cálculo, unifilar, lista de material, pranchas em PDF. São os casos de referência: o motor só está pronto quando reproduz o dimensionamento deles (ou explica a diferença pela norma), como o SPK fez com a Nova Era.
- Template do Revit, famílias e tags que usam.
- Um exemplo da tag de fiação e da legenda que consideram "bonita".
- A norma da distribuidora que mais usam.
- Se possível, 30 minutos gravados de um deles fazendo o lançamento elétrico de um apartamento no Revit do jeito que fazem hoje (como o áudio do Ciroca no SPK): é onde aparecem as dores de verdade.
