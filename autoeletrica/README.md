# AutoEletrica — plugin de projeto elétrico predial para o Revit

Pasta de trabalho do segundo produto do repositório: um add-in do Revit para projeto de instalações elétricas de baixa tensão segundo a ABNT NBR 5410, construído sobre a mesma base do [SPK](../docs/refinamentos/spk/README.md) (motor .NET puro + add-in fino, memorial auditável, folhas no padrão do escritório, licença por assinatura).

Estado em 06/11/2026: **motor e add-in existem e compilam, com as entregas 0.2 (lançamento), 0.3 (eletrodutos, fiação, fio, etiquetas, legenda), 0.4 (Verificar completo), 0.5 (motor da Fase 2) e 0.6 (entregáveis: memorial .docx, manual do usuário, quadros de cargas, unifilar, prumada, lista de material, pranchas) escritas; nada foi aberto no Revit ainda.** A NBR 5410 e mais dez normas satélites foram estudadas (mapas em `nr/normas/`, fora do Git), o mercado foi mapeado e o roadmap está proposto. O [motor](../motor-eletrica/README.md) (`Eletrica.Motor`, 183 testes) propõe os pontos por ambiente (9.5.2) e a divisão em circuitos (9.5.3), verifica (cômodo, banheiro com volumes e IP, DR, circuitos, comandos, trechos, quadro) e dimensiona pelos seis critérios de 6.2.6.1.2 com o agrupamento lido trecho a trecho, o compartilhamento de eletrodutos (6.2.10.2), demanda por categoria, fases balanceadas, DRs por grupo, TT com RA, condutores em paralelo e sugestão para cada critério reprovado; o [add-in](../addin-eletrica/README.md) lança quadro, pontos, comandos e circuitos, traça eletrodutos e caixas por regra, passa os circuitos pelos eletrodutos (retornos, fiação manual), desenha o fio, etiqueta, gera a legenda, verifica (inclusive os volumes do banheiro pela geometria da arquitetura e a consistência do modelo), calcula, grava nos parâmetros AE e gera os entregáveis (memorial e manual .docx, quadros de cargas, unifilar, prumada, lista de material e pranchas). Próximos passos: abrir no Revit 2027 com um projeto elétrico, validar com os dois engenheiros ([perguntas](docs/perguntas-engenheiros.md)), fechar os critérios de pronto com os projetos de referência e seguir o [plano](docs/plano-de-desenvolvimento.md) (base comum, teste automático, 1.0 Gerar projeto).

## Documentos

| Arquivo | Conteúdo |
|---|---|
| [docs/oportunidades.md](docs/oportunidades.md) | **Comece aqui.** Leitura honesta do mercado, onde está a lacuna de verdade, tese do produto e fases de desenvolvimento |
| [docs/plano-de-desenvolvimento.md](docs/plano-de-desenvolvimento.md) | **O plano completo.** Inventário do QiElétrico como régua, inventário de funcionalidades do AutoEletrica (básico, assistido, automático, cálculo, documentação, verificação) com norma e fase, roadmap por entrega, aba do Revit, modelo de dados e testes |
| [docs/normas-necessarias.md](docs/normas-necessarias.md) | Normas que faltam baixar, com edição vigente, prioridade e onde cada uma entra |
| [docs/concorrencia.md](docs/concorrencia.md) | Os plugins de elétrica para Revit no Brasil e fora, com preço, recursos e o que falta em cada um |
| [docs/base-de-regras.md](docs/base-de-regras.md) | As regras da NBR 5410 que o plugin verifica e aplica, com o item citado, agrupadas por ferramenta |
| [docs/revit-api-eletrica.md](docs/revit-api-eletrica.md) | O que a Revit API dá para elétrica (circuitos, fiação, eletrodutos, quadros, condutores no 2026+) e as armadilhas |
| [docs/perguntas-engenheiros.md](docs/perguntas-engenheiros.md) | O que perguntar aos dois engenheiros antes de escrever código, e quais projetos pedir como caso de referência |
| [../motor-eletrica](../motor-eletrica/README.md) | O motor de cálculo (.NET puro) com as tabelas das normas, o dimensionamento, o Verificar modelo e o memorial |
| [../addin-eletrica](../addin-eletrica/README.md) | O add-in do Revit: aba AutoEletrica, leitura do modelo, passar circuitos, verificar, calcular, etiquetar |

## Resumo em dez linhas

1. O mercado brasileiro de plugin elétrico para Revit **já é cheio**: JC Tools (78 ferramentas, R$ 99/mês), RACTools (R$ 897 único), Nômade BIM (R$ 479/ano), PRO-Elétrica, SCE Tools, OF Elétrico e um open source GPL (Circuito Livre). Todos fazem fiação, tags, eletroduto e algum dimensionamento. Diferente do SPK, "o básico" não é lacuna.
2. O que nenhum deles entrega: **cálculo completo e auditável** (os seis critérios de 6.2.6.1.2, com curto-circuito no fim do circuito, I²t, seletividade e agrupamento trecho a trecho lido do traçado real), **verificação de conformidade explicável** por ambiente (DR, volumes de banheiro, pontos mínimos, eletroduto, quadro) e **geração do projeto inteiro a partir da arquitetura vinculada**, que é o que faz escritório escolher o QiBuilder em vez do Revit.
3. O padrão de produto é o mesmo do SPK: motor em .NET puro com as tabelas da norma como dados versionados e cada número rastreado ao item; IA só em classificação assistida, explicações e texto; add-in fino.
4. A base comum (licença, padrão do escritório, folhas, memorial .docx, teste automático no Revit) sai do SPK e serve aos dois.
5. Fase 1 entrega o pedido dos engenheiros feito do jeito certo: pontos, circuitos, passagem de fiação com contagem por trecho, tags de fiação e de pontos, legenda, e o **Verificar modelo** elétrico.
6. Fase 2 é o motor de dimensionamento e os entregáveis: quadro de cargas, unifilar, memorial, lista de material, manual do usuário (6.1.8.3, que ninguém gera).
7. Fase 3 é a geração automática: pontos por ambiente (9.5.2), circuitos (9.5.3), roteamento de eletrodutos por regra (15 m, 3 curvas, ocupação), quadros e balanceamento, com as mesmas peças usadas em modo assistido.
8. Fase 4 é o que fica além da 5410: padrão de entrada por distribuidora, luminotécnico (NBR ISO/CIE 8995-1), SPDA (NBR 5419), fotovoltaico, e a nova edição da 5410 quando sair (prevista para o fim de 2026 ou 2027).
9. O Revit 2026 **removeu o dimensionamento nativo de fios** e criou o modelo de condutores e cabos (CableType, ConductorSize); add-ins passam a ser o único caminho para isso, e o nosso resultado pode viver nos dados nativos, não só em parâmetros compartilhados.
10. Riscos: mercado concorrido e barato, responsabilidade do cálculo, fio do Revit ser 2D e por vista, comprimento lógico do circuito diferente do real. Mitigações no [oportunidades.md](docs/oportunidades.md).
