# AutoEletrica — add-in do Revit

Add-in fino sobre o [motor](../motor-eletrica/README.md): lança quadros, pontos, comandos e circuitos pela NBR 5410 e pelo padrão do escritório; lê o modelo (quadros, circuitos, pontos, ambientes, eletrodutos, comandos), passa os circuitos pelos eletrodutos, verifica, calcula e grava o resultado em parâmetros compartilhados "AE" e num memorial. Mesma estrutura e as mesmas peças de infraestrutura do [SPK](../addin-spk/README.md) (Nice3point SDK, Revit 2025 a 2027, armazenamento no .rvt, transação segura, ícones desenhados).

Estado em 06/11/2026: **compila para 2025, 2026 e 2027 com as entregas 0.2 (lançamento), 0.3 (eletrodutos, fiação, fio, etiquetas e legenda) e 0.4 (Verificar completo) incluídas; ainda não foi aberto no Revit** (ver "Testar").

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
| Eletrodutos | Traçar eletroduto | Clique nos elementos em sequência (quadro, caixa, pontos). Entre cada dois: sobe à cota de passagem do padrão (teto, piso ou altura dos pontos), L em planta, desce ao destino; tipo e DN do padrão; curvas pelas conexões do tipo (ou pontas ligadas direto); liga aos conectores de eletroduto das famílias quando há. Chegando num eletroduto existente, parte-o e põe um tê (perto de uma ponta livre, uma curva). |
| Eletrodutos | Ligar pontos | Selecione pontos e clique no eletroduto, caixa ou quadro de destino: descida e ramal de cada ponto, com tê. |
| Eletrodutos | Caixas por regra | Anda em cada trecho entre caixas somando comprimento e 3 m por curva; onde passa de 15 m ou da terceira curva (6.2.11.1.6 b, 6.2.11.1.7), parte o eletroduto e insere a caixa do papel Caixa de passagem (família livre). Sem família, só aponta. Caixas quebram o trecho no grafo mesmo sem conector. |
| Fiação | Passar circuitos | Monta o grafo dos eletrodutos (conectores), leva cada circuito do quadro aos pontos e ao primeiro interruptor de cada comando pelo menor caminho, conta os retornos entre interruptores (2 no paralelo/intermediário) e do último interruptor às luminárias (1), respeita `AE Forçar` / `AE Evitar`, e grava em cada eletroduto `AE Circuitos`, `AE Condutores`, `AE Retornos` e `AE Fiação` (`"5 cond. C1 +2R"`). |
| Fiação | Fiação manual | Selecione eletrodutos e, circuito a circuito, force ou evite a passagem (ou evite todos os outros). Grava `AE Forçar circuitos` / `AE Evitar circuitos` e roda Passar circuitos de novo. |
| Fiação | Desenhar fiação | Na planta aberta, um `Wire` por trecho com circuitos, seguindo o percurso em planta (arcos de até 3 vértices para embutido; polilinha para aparente), com `AE Circuitos`, `AE Fiação` e `AE Condutores` para a etiqueta de fio; tenta ajustar as marcas de condutor. Rodar de novo apaga os fios AE da vista e redesenha. |
| Fiação | Etiquetar | Cria etiquetas de eletroduto, fio, tomada, luminária, interruptor e quadro na planta aberta com as famílias de etiqueta carregadas (os rótulos devem apontar para os parâmetros AE; prefere tipos com "AE" no nome). |
| Fiação | Legenda | Vista de desenho "AE — Legenda de fiação": tabela dos circuitos (número, descrição, quadro, n#S(PE), disjuntor, DR) e as cores dos condutores do padrão (6.1.5.3). Rodar de novo refaz. |
| Cálculo | Verificar modelo | Roda o Verificar do motor (códigos A-, B-, C-, DR-, L-, M-, Q-, T-) mais as verificações do modelo do Revit (R-: sistema de distribuição do quadro × Dados, família sem conector ou sem potência, interruptor nos volumes 0–2, chuveiro elétrico a mais de 1,5 m do boxe, interruptor sem comando) e mostra a lista com código, gravidade e item da norma; filtro por gravidade, "Mostrar" seleciona os elementos, "Salvar relatório" grava `Documentos\AutoEletrica\verificacao\<projeto>-verificacao.md`. Os volumes do banheiro (9.1.2.1) saem da geometria do boxe, banheira ou chuveiro da arquitetura (`Verificacao/VolumesDeBanheiro.cs`): volume 1 sobre a peça até 2,25 m, 2 a 0,60 m até 3,00 m, 3 a mais 2,40 m até 2,25 m; sem boxe, 0,60 m ao redor do chuveiro; paredes não descontadas. O IP vem de parâmetro da família com "IP" ou "grau de proteção" no nome. |
| Cálculo | Calcular | Dimensiona tudo, grava `AE Seção`, `AE Disjuntor`, `AE IB`, `AE Queda`, `AE DR`, `AE Situação`, `AE Comprimento` nos circuitos, `AE Fiação`, `AE DN sugerido`, `AE Ocupação` nos eletrodutos, `AE Ambiente` nos pontos, e salva `Documentos\AutoEletrica\calculo\<projeto>-memorial.md`. |
| Resultados | Memorial | Abre o último memorial. |
| Ajuda | Guia | Passo a passo e a situação do projeto aberto. |

## Como o modelo é lido (`Leitura/LeitorInstalacao.cs`)

- **Quadros**: todo `ElectricalEquipment` que alimenta circuitos ou é alimentado por um; fases pelo sistema de distribuição; o pai é o quadro do circuito que o alimenta.
- **Circuitos**: `ElectricalSystem` de energia; ligação pelos polos, tensão e neutro; tipo de carga por `AE Tipo de carga` ou pelos pontos; comprimento do Revit como reserva quando não há eletrodutos.
- **Pontos**: os elementos de cada circuito (tomadas, luminárias, equipamentos); potência por `AE Potência (VA)`, pela carga aparente da família ou rateio da carga do circuito; ambiente pelo Room no ponto (arquivo ou vínculo); altura pelo nível; tipo pelo nome da família (chuveiro, ar-condicionado, TUE...). Interruptores não são pontos de utilização.
- **Trechos**: cadeias de eletrodutos ligados por curvas e luvas, entre caixas, tês, pontas e pontos; DN pelo diâmetro; tipo pelo nome do tipo de eletroduto; os circuitos vêm de `AE Circuitos` e os retornos de `AE Retornos` (entram na ocupação).
- **Comandos**: elementos com `AE Comando`; interruptores (Lighting Devices, com a ordem `S1#1`) e luminárias; o circuito é o das luminárias; o nó de cada interruptor no grafo de eletrodutos.
- **Caixas**: famílias do papel Caixa de passagem (ou com "caixa", "condulete", "box" no nome) no nó do grafo em que estão; quebram o trecho e vão para o motor como `Caixa`.

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

## O que as entregas 0.2 a 0.4 ainda não cobrem

No Verificar: piscina e sauna (9.2, 9.3), neutro compartilhado, PE ausente e interruptor no neutro (não há como ler do modelo), emendas, SELV junto, rota de fuga e corta-fogo (sem dado), caixa sem tampa; os volumes do banheiro não descontam paredes e divisórias. O critério de pronto da 0.4 ("rodado nos três projetos de referência, aponta problemas reais que os engenheiros confirmam") só fecha com os projetos.


Vários pontos na mesma caixa e alinhamento vertical (item 2.2), esquemas de ligação além de F+N/F+F e retorno simples/paralelo/intermediário (1.5), influências externas além do uso do ambiente (1.8), um .rvt de referência; no traçado, desvio de obstáculos e caixa automática no fim de cada subida (4.1 completo, 4.8); nas tags, anti-sobreposição (6.4) e famílias de etiqueta de referência no repositório (as do escritório precisam de rótulos nos parâmetros AE); legenda de símbolos (a API não cria vista de legenda). O resto do plano (0.4 a 0.6): Verificar completo (volumes do banheiro pela geometria), quadro de cargas na prancha, memorial .docx, lista de material, teste automático no Revit, e a base comum extraída do SPK. Ver `autoeletrica/docs/plano-de-desenvolvimento.md`.
