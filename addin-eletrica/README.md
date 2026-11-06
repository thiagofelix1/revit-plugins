# AutoEletrica — add-in do Revit

Add-in fino sobre o [motor](../motor-eletrica/README.md): lança quadros, pontos, comandos e circuitos pela NBR 5410 e pelo padrão do escritório; lê o modelo (quadros, circuitos, pontos, ambientes, eletrodutos, comandos), passa os circuitos pelos eletrodutos, verifica, calcula e grava o resultado em parâmetros compartilhados "AE" e num memorial. Mesma estrutura e as mesmas peças de infraestrutura do [SPK](../addin-spk/README.md) (Nice3point SDK, Revit 2025 a 2027, armazenamento no .rvt, transação segura, ícones desenhados).

Estado em 06/11/2026: **compila para 2025, 2026 e 2027 com a entrega 0.2 (lançamento) incluída; ainda não foi aberto no Revit** (ver "Testar").

## A aba AutoEletrica

| Painel | Botão | O que faz |
|---|---|---|
| Projeto | Dados do projeto | Tensões, fases, esquema de aterramento, Icc presumida, linha aérea, dias de trovoada, SPDA, temperatura, habitação. Gravado no .rvt. |
| Projeto | Ambientes | Lista os Rooms do arquivo e dos vínculos com o uso sugerido pelo nome; o engenheiro confirma. O uso define pontos mínimos, potências e DR. |
| Projeto | Padrão | Padrão do escritório (`Padrao/PadraoEletrica.cs`): alturas baixa/média/alta/interruptor/pé-direito, potências padrão (TUG, TUG molhada, TUE 20 A, chuveiro, ar), corrente e pontos máximos por circuito, limite para fase-fase, prefixos de circuito e quadro, afastamento da parede. Os mínimos da norma valem por cima. |
| Projeto | Famílias | Lista os tipos de família das categorias elétricas e confirma o **papel** de cada um (tomada 10 A, 20 A, dupla, luminária, arandela, interruptor 1/2/3 teclas, paralelo, intermediário, chuveiro, ar, ponto de força, quadro, caixa). Mostra hospedagem e se há conector elétrico. O plugin lança e lê pelo papel. |
| Projeto | Parâmetros | Cria os parâmetros compartilhados AE (ver abaixo). |
| Lançamento | Quadro | Coloca o quadro (família do papel Quadro) pela colocação normal do Revit; os novos recebem nome `QD-n` e o sistema de distribuição do projeto que bate com as tensões e fases dos Dados (aviso se não houver). |
| Lançamento | Alimentar quadro | Clique no terminal e no geral: cria o circuito alimentador no geral com o terminal como carga (ou troca o pai se já havia). |
| Lançamento | Inserir ponto | Papel, família, posição e altura, potência, tipo de carga e circuito; o Revit faz a colocação normal (Esc termina); cada ponto novo é levado à altura, recebe os parâmetros AE e o ambiente e entra no circuito, se escolhido. |
| Lançamento | Pontos por ambiente | A proposta do motor para cada ambiente (9.5.2 + padrão) numa grade editável; lança tomadas espaçadas pelo contorno útil do ambiente (longe das portas, 600 VA e as da média primeiro, na parede mais longa), luz no centro (face do forro/laje se a família é de face), interruptor ao lado da porta já com o comando gravado, chuveiro e ar na parede oposta à porta. Uma transação por ambiente; um Ctrl+Z desfaz tudo. |
| Lançamento | Comando | Clique nos interruptores (na ordem) e nas luminárias: grava `AE Comando` (S1, S2…), a ordem e o tipo (simples, paralelo, intermediário), o Switch ID do Revit, e põe o interruptor no circuito das luminárias. |
| Lançamento | Criar circuitos | Pega os pontos sem circuito, divide pela NBR 5410 e pelo padrão (`Motor.Lancamento.DivisaoDeCircuitos`), mostra a proposta (número, descrição editável, tipo, ligação, pontos, VA, IB, motivo) e cria um `ElectricalSystem` por circuito no quadro escolhido, com descrição e `AE Tipo de carga`; os interruptores dos comandos entram junto. Avisa quando o conector da família dá polos diferentes da ligação proposta. |
| Fiação | Passar circuitos | Monta o grafo dos eletrodutos (conectores), leva cada circuito do quadro aos pontos e ao primeiro interruptor de cada comando pelo menor caminho, conta os retornos entre interruptores (2 no paralelo/intermediário) e do último interruptor às luminárias (1), e grava em cada eletroduto `AE Circuitos`, `AE Condutores`, `AE Retornos` e `AE Fiação` (`"5 cond. C1 +2R"`). |
| Fiação | Etiquetar | Cria etiquetas de eletroduto, tomada e luminária na planta aberta com as famílias de etiqueta carregadas (os rótulos devem apontar para os parâmetros AE). |
| Cálculo | Verificar modelo | Roda o Verificar do motor e mostra a lista com código, gravidade e item da norma; "Mostrar" seleciona os elementos no modelo. |
| Cálculo | Calcular | Dimensiona tudo, grava `AE Seção`, `AE Disjuntor`, `AE IB`, `AE Queda`, `AE DR`, `AE Situação`, `AE Comprimento` nos circuitos, `AE Fiação`, `AE DN sugerido`, `AE Ocupação` nos eletrodutos, `AE Ambiente` nos pontos, e salva `Documentos\AutoEletrica\calculo\<projeto>-memorial.md`. |
| Resultados | Memorial | Abre o último memorial. |
| Ajuda | Guia | Passo a passo e a situação do projeto aberto. |

## Como o modelo é lido (`Leitura/LeitorInstalacao.cs`)

- **Quadros**: todo `ElectricalEquipment` que alimenta circuitos ou é alimentado por um; fases pelo sistema de distribuição; o pai é o quadro do circuito que o alimenta.
- **Circuitos**: `ElectricalSystem` de energia; ligação pelos polos, tensão e neutro; tipo de carga por `AE Tipo de carga` ou pelos pontos; comprimento do Revit como reserva quando não há eletrodutos.
- **Pontos**: os elementos de cada circuito (tomadas, luminárias, equipamentos); potência por `AE Potência (VA)`, pela carga aparente da família ou rateio da carga do circuito; ambiente pelo Room no ponto (arquivo ou vínculo); altura pelo nível; tipo pelo nome da família (chuveiro, ar-condicionado, TUE...). Interruptores não são pontos de utilização.
- **Trechos**: cadeias de eletrodutos ligados por curvas e luvas, entre caixas, tês, pontas e pontos; DN pelo diâmetro; tipo pelo nome do tipo de eletroduto; os circuitos vêm de `AE Circuitos` e os retornos de `AE Retornos` (entram na ocupação).
- **Comandos**: elementos com `AE Comando`; interruptores (Lighting Devices, com a ordem `S1#1`) e luminárias; o circuito é o das luminárias; o nó de cada interruptor no grafo de eletrodutos.

## O que as famílias do escritório precisam ter

- **Conector elétrico** (tensão e número de polos) nas tomadas, luminárias, interruptores e equipamentos: sem ele o Revit não aceita o elemento num circuito, e Criar circuitos avisa. Para a ligação fase-fase (chuveiro, ar em 127/220 V) o conector precisa ser de 2 polos na tensão fase-fase; o plugin propõe a ligação e avisa quando o conector dá outra.
- **Hospedagem**: com a arquitetura em vínculo, use famílias **de face** (work plane based) ou **livres** (nível). Família hospedada em parede do próprio arquivo não encontra parede no vínculo; o lançamento recusa e explica.
- **Carga aparente** como parâmetro de instância ajuda (o plugin grava a potência nela); se for de tipo, a potência fica só em `AE Potência (VA)`, que é o que o motor lê.
- Famílias de **etiqueta** com rótulos nos parâmetros AE (`AE Fiação`, `AE Ambiente`, Circuit Number) para o Etiquetar.
- Ainda não há um conjunto de famílias de referência no repositório: é o próximo passo depois do primeiro teste no Revit, feito no editor de famílias com os engenheiros.

## Testar

1. `dotnet build -c Debug.R27` em `src/Eletrica.AddIn` (publica em `bin\Debug.R27\publish\Eletrica.AddIn`).
2. O manifesto de desenvolvimento em `%AppData%\Autodesk\Revit\Addins\2027\Eletrica.AddIn.addin` aponta para essa pasta (já criado nesta máquina).
3. Abrir o Revit 2027 num projeto elétrico com a arquitetura vinculada e famílias elétricas carregadas; seguir o Guia (Dados → Ambientes → Padrão → Famílias → Quadro → Pontos por ambiente → Criar circuitos → eletrodutos → Passar circuitos → Verificar → Calcular). Modelos de teste: os do SPK (Snowdon, MNEVVM) só para a arquitetura; falta um .rvt elétrico de referência dos engenheiros.
4. Distribuição: `distribuicao\instalar.bat` copia a pasta `Revit 20xx` para a pasta de add-ins do usuário, como no SPK.

## O que a 0.2 ainda não cobre

Vários pontos na mesma caixa e alinhamento vertical (item 2.2), esquemas de ligação além de F+N/F+F e retorno simples/paralelo/intermediário (1.5), influências externas além do uso do ambiente (1.8), caixas de passagem por regra, e um .rvt de referência. O resto do plano (0.3 a 0.6): traçar eletrodutos, fio desenhado (`Wire`), legenda, quadro de cargas na prancha, memorial .docx, lista de material, teste automático no Revit, e a base comum extraída do SPK. Ver `autoeletrica/docs/plano-de-desenvolvimento.md`.
