# Mapa do código do SPK

Fotografia de 05/11/2026. Serve para achar onde cada coisa acontece e o que é conferido em cada etapa. Para o que falta fazer, veja [lista-desenvolvimento.md](lista-desenvolvimento.md); para o uso de ponta a ponta, [fluxos.md](fluxos.md); para a conferência com as normas, os arquivos `aderencia-*.md`.

## Visão geral

São duas partes, com cerca de 20 mil linhas de C#:

| Parte | Pasta | O que faz | Conhece o Revit? |
|---|---|---|---|
| Motor | `motor-spk/src/Spk.Motor` | Hidráulica, tabelas de norma, geometria dos bicos, memorial | Não |
| Add-in | `addin-spk/src/Spk.AddIn` | Botões, leitura e escrita do modelo, folhas, teste automático | Sim |

A regra da divisão: o add-in traduz o modelo para `Rede` e `Edificio`, e o motor calcula sem saber de onde vieram. A IA nunca entra no cálculo dos números.

```text
Botão (Commands/)
  → ferramenta (Modelagem/, Verificacao/, Folhas/) ou serviço (Servicos/)
      → LeitorRede ou LeituraDoEdificio: modelo → objetos do motor
      → Spk.Motor: tabela, posição dos bicos, cálculo
      → grava no modelo (parâmetros SPK, ArmazenamentoSpk) e em Documentos\SPK
```

## Pastas do add-in

| Pasta ou arquivo | Conteúdo |
|---|---|
| `SpkApplication.cs` | Monta a aba SPK e os painéis |
| `Commands/` | Um comando por botão: pergunta, chama a ferramenta e mostra o resultado |
| `Modelagem/` | Gerar rede, Bicos por ambiente, Traçar ramal, Ligar bicos, Ligar à geral, Ligar tubos, Editar bicos, Diâmetros, Cores, Conferir na planta |
| `Verificacao/` | Verificar modelo e as correções de peças encostadas e derivações sem tê |
| `Arquitetura/LeituraDoEdificio.cs` | Lê ambientes, tetos e vigas do arquivo e dos vínculos |
| `LeitorRede.cs` | Lê tubos, peças e bicos e monta a `Rede` do motor |
| `GeradorMalha.cs` | Peças básicas: criar tubo, escolher tipo e DN, ligar, inserir tê |
| `Servicos/` | Cálculo e folhas sem janelas; assinatura da rede |
| `Folhas/` | Setorização, isométrico, planta, corte, carimbo, quadros, lista de material |
| `Automacao/` | Teste automático no Revit (fila, cenas, sabotagens) |
| `Ajuda/` | Guia e textos de ajuda |
| `DadosProjeto.cs`, `DadosEsboco.cs`, `MapeamentoGravado.cs`, `ResultadoGravado.cs`, `ArmazenamentoSpk.cs` | O que fica gravado no `.rvt` |

## Pastas do motor

| Pasta | Conteúdo principal |
|---|---|
| `Modelo/` | `Rede`, `Trecho`, `No`, `Chuveiro`, `Edificio`, `Topologia` (setores, ramais, numeração) |
| `Calculo/` | `MotorHidraulico`, `BuscaAreaMaisDesfavoravel`, `CalculoSpk`, `DimensionamentoPorTabela`, `Abastecimento` |
| `Normas/` | `Nbr10897`, `AreaDeOperacao`, `BaseDeProjeto`, `Conexoes`, `EspacamentoDeBicos`, `ObstrucaoDeBico`, `TabelaDeDiametros`, `TetoAlto`, `TubosNormalizados` |
| `Geracao/BicosNoAmbiente.cs` | Grade de bicos, um bico por vão entre vigas, conferência de espaçamento |
| `Relatorios/` | Memorial em `.md` e `.docx` |

## Botões

| Painel | Botão | Comando | Faz o trabalho em |
|---|---|---|---|
| Projeto | Padrão do escritório | `ImportarPadraoCommand` | `PadraoEscritorio` |
| Projeto | Dados do projeto | `DadosProjetoCommand` | `DadosProjeto` |
| Projeto | Famílias | `DescobrirFamiliasCommand` | `MapeamentoGravado`, `BicosDoModelo` |
| Projeto | Arquitetura | `ArquiteturaCommand` | `LeituraDoEdificio` |
| Rede | Gerar rede | `GerarRedeCommand` | `GerarRede` |
| Rede | Bicos por ambiente | `BicosPorAmbienteCommand` | `BicosPorAmbiente`, `BicosNoAmbiente` |
| Rede | Traçar ramais | `TracarRamalCommand` | `TracarRamal` |
| Rede | Ligar bicos | `LigarBicosCommand` | `LigarBicos` |
| Rede | Ligar à geral | `LigarAGeralCommand` | `LigarAGeral` |
| Rede | Inserir, Apagar e Mover bico | `EditarBicosCommands` | `EditarBicos` |
| Rede | Ligar tubos | `LigarTubosCommand` | `LigarTubos` |
| Rede | Conferir na planta e Limpar | `ConferenciaCommands` | `ConferenciaVisual` |
| Rede | Malha retangular, Dados do esboço, Rede de exemplo | `GerarMalhaCommand`, `DadosEsbocoCommand`, `RedeExemploCommand` | `GeradorMalha`, `CenariosExemplo` |
| Cálculo | Marcar setor | `MarcarSetorCommand` | `EntradasDeSetor` |
| Cálculo | Verificar modelo | `VerificarModeloCommand` | `VerificacaoModelo`, `Reconexao`, `Derivacoes` |
| Cálculo | Diâmetros | `DiametrosCommand` | `DimensionamentoPorTabela`, `AplicarDiametros` |
| Cálculo | Calcular | `CalcularRedeCommand` | `ServicoCalculo`, `CalculoSpk` |
| Resultados | Memorial | `AbrirMemorialCommand` | Abre `Documentos\SPK\calculo\memorial.docx` |
| Resultados | Folhas | `FolhasCommand` | `ServicoFolhas`, `Folhas/` |
| Resultados | Cores, Tirar cores, Lista de material | `ResultadosCommands` | `CoresDaRede`, `ListaDeMaterial` |
| Ajuda | Guia | `GuiaCommand` | `SituacaoDoProjeto`, `TextoDoGuia` |

## Fluxo do Gerar rede

Cada ambiente roda num grupo de transações. Se uma etapa recusa, o ambiente volta inteiro, e os outros seguem. A geração não roda o Verificar modelo nem o Calcular sozinha: o diálogo final manda o projetista rodar os dois.

### 1. Antes de gerar: `GerarRedeCommand.Execute`

| Conferência | Se falhar |
|---|---|
| Há projeto aberto, fora do editor de família | Pede para abrir um projeto |
| `DadosProjetoUi.Obter` devolve os dados | Para |
| A classe não é armazenamento nem a base é ESFR | Recusa: ainda não é automático |
| `LeituraDoEdificio.Ler` achou ambientes | Recusa e sugere a Malha retangular |
| Há família de sprinkler carregada | Recusa |
| Janela confirmada, com tipo de tubo escolhido | Cancela |
| Com geral e VGA por clique, o ponto foi clicado numa planta | Pede para abrir uma planta |

O tipo de tubo escolhido fica gravado em `DadosEsboco`.

### 2. Setor único ou várias redes: `GerarRede.ExecutarComGeral`

- **Sem corredor escolhido:** chama `Executar`, e cada sala fica com a sua entrada.
- **Com corredor:** acha o caminho (`CaminhoDoCorredor`), os ramos saindo da VGA (`RamosDaEntrada`) e o trecho de frente para cada sala (`TrechoDaSala`). Depois gera as salas, põe os bicos do corredor, encurta a geral até 50 cm depois da última entrada, desenha a geral, liga as salas (`LigarAGeral`), liga os bicos do corredor (`LigarBicos`) e aplica os diâmetros do setor.

| Situação com corredor | Resultado |
|---|---|
| Corredor com menos de 60 cm livres | Recusa a geral; as salas ficam separadas |
| Corredor em T ou em cruz | Aviso: a geral segue só o trecho principal |
| Nenhuma sala ganhou rede | Recusa a geral |
| Bicos do corredor sem 15 cm abaixo da geral | Os bicos saem, com aviso |
| Bico do corredor caiu no tê de uma sala | Anda 25 ou 40 cm e tenta de novo; o que não couber sai, com aviso |
| Tipo de tubo não tem tê no DN | A recusa do Revit volta como motivo |

### 3. Rede em cada ambiente: `GerarRede.Gerar`

| Ordem | Etapa | Conferência ou regra | Resultado |
|---|---|---|---|
| 1 | Vigas fundas: `VigasDosVaos`, `EhGrelha` | Decide entre um bico por vão e grelha | Muda o caminho das etapas seguintes |
| 2 | Defletor | Sob laje ou telhado, ou nos vãos: pelo menos 25 cm do teto (10 + 15 cm) | Aviso se mudou o pedido |
| 3 | Bicos: `BicosPorAmbiente.Posicionar` | Família hospedada | Erro explicando |
| 3 | | Altura do defletor impossível (`Defletor`) | Recusa o ambiente |
| 3 | | Vigas fundas fora de duas direções em ângulo reto | Recusa |
| 3 | | Vãos estreitos, com bicos perto demais | Recusa: posicionar à mão |
| 3 | | Nenhum bico coube | Recusa |
| 3 | | Espaçamento, área e parede: `BicosNoAmbiente.Conferir` | Falhas na conferência |
| 3 | | Obstrução por viga, Tabela 15: `ObstrucaoDeBico.Conferir` | Aviso; reprova depois no Calcular |
| 4 | Altura da rede, grelha | Bico tem de ser em pé, com o conector para baixo | Recusa se não for |
| 4 | | Rede 15 cm abaixo do fundo da viga mais baixa, e pelo menos 15 cm abaixo do defletor | Recusa se as vigas forem rasas |
| 4 | Altura da rede, normal | Defletor + drop (padrão 30 cm); sob laje, até 10 cm abaixo do teto | Aviso se baixou |
| 4 | | Menos de 15 cm entre rede e defletor | Recusa |
| 4 | | Com bicos nos vãos, subgeral por baixo das vigas | Recusa se as vigas forem rasas |
| 4 | | Rede passando na altura de uma viga | Aviso, até 3 vigas |
| 5 | Subgeral: `Subgeral` | Espinha de peixe entre as colunas do meio; lateral junto à parede | Com uma coluna só, vira lateral e avisa |
| 5 | | Começa a 30 cm da parede e termina 20 cm depois da última fileira | Fileira colada à entrada: troca de parede ou recusa |
| 5 | | Testa 21 pontos dentro do ambiente | Aviso se a subgeral sair |
| 5 | | DN inicial: `GeradorMalha.DiametroComTe`, do esboço | Sempre um tamanho que o tipo tem |
| 6 | Ramais: `TracarRamal.Executar` | Subgeral reta e horizontal | Recusa |
| 6 | | Fileira quase paralela à subgeral | Recusa a fileira |
| 6 | | Fileira que não cruza a subgeral, ou cruza a menos de 15 cm da ponta | Recusa a fileira |
| 6 | | Bico em cima do cruzamento, ou subida com menos de 15 cm | Recusa a fileira |
| 6 | | Descida com menos de 25 cm: ramal nasce em DN 25 | O tê cabe; a tabela sobe depois |
| 6 | | Nenhum ramal traçado | Recusa o ambiente |
| 7 | Bicos: `LigarBicos.Executar` | Parte o ramal, tê e descida; ou braço, cotovelo e descida | Bico fora ou acima do ramal é recusado com motivo |
| 8 | Diâmetros: `AplicarDiametros.Executar`, com `ajustarPontas: true` | Só se a classe tem tabela | Malha fechada ou outro erro de leitura vira aviso |
| 9 | Tampão: `Tampar` | Pontas abertas na altura da rede; o pé da descida fica aberto | Sem tampão no tipo, aviso |

## Fluxo dos Diâmetros

```text
DadosProjeto → BicosDoModelo.Ler → LeitorRede.Montar
  → DimensionamentoPorTabela.Calcular   conta bicos a jusante; DN pela tabela
  → AplicarDiametros.Pedido             DN por tubo; o maior dos trechos dele
  → PontasSemAgua                       na rede gerada, ou se o projetista escolher
  → Tamanho                             menor tamanho do tipo não abaixo do pedido
  → MudarTodos                          tira as peças, muda os tubos e refaz as peças
      se falhar → Mudar                 um tubo por vez
  → Apertadas                           avisa peça mais estreita que a linha
```

Conferências de `DimensionamentoPorTabela`:

| Situação | Resultado |
|---|---|
| Classe sem tabela (extra, armazenamento) | Erro: só cálculo hidráulico |
| Malha fechada | Erro: a tabela vale só para árvore |
| Sistema com 465 m² ou mais | Aviso: ponto de partida |
| K diferente de 80 | Aviso: ponto de partida |
| Bicos ou ramais a mais de 3,7 m | Aviso e Tabela 14 |
| Material sem tabela | Aviso; o trecho fica como está |
| Risco leve com bicos demais para o DN 90 | Aviso e tabela de ordinário |
| Mais de 8 bicos num lado do ramal | Aviso |
| Trecho sem bico a jusante | Não recebe DN |

`MudarTodos` só refaz tê, cruzeta e redução que sejam das preferências de roteamento do tipo: peça escolhida pelo projetista não muda. Cotovelo e luva só voltam entre tubos do mesmo DN, ligados direto.

Pontas sem água: o Gerar rede sempre as ajusta, inclusive a ponta da geral no passo de setor (`ajustarPontas: true`). No botão Diâmetros, se alguma ponta ficaria em DN diferente da linha, o projetista escolhe entre ajustar ou manter. O Calcular avisa quando uma ponta ficou mais larga que a linha (`PontasLargas`).

## Fluxo do Calcular

```text
CalcularRedeCommand
  → SetorUi.ModeloPronto         mostra os erros do Verificar antes
  → BicosDoModelo.Ler            K e área da malha gerada ou de Famílias
  → ServicoCalculo.ValidarFatorK K abaixo de 40 parece americano: recusa
  → prévia para o projetista
  → SetorUi.NoSetor              qual setor calcular
  → ServicoCalculo.Executar
      → LeitorRede.Montar
      → AlturasDoTeto, ObstrucoesDoSetor
      → CalculoSpk.Executar      busca a pior área, calcula, verifica
      → avisos de K, inclinação, fator C e tubos
      → memorial.md e memorial.docx
      → GravarNoModelo           parâmetros SPK e assinatura da rede
```

### O que `LeitorRede.Montar` faz

1. `Alcançaveis`: tudo que os bicos alcançam pelos conectores, parando nas VGAs, em ordem de id.
2. Um nó por bico e por peça.
3. Um trecho por pedaço de tubo, com interno, nominal e fator C.
4. `LigarPecas`: trecho curto entre peças ligadas direto, com o interno real do mesmo DN.
5. `LigarBicos`: trecho curto do bico à peça, com 25 mm de interno.
6. Sem nenhum bico ligado: `AnexarPorProximidade`, até 50 cm, com aviso.
7. `MaterialDaRede`: o material mais comum, pelo nome do tipo; nome que não diz conta como aço.
8. Acha a fonte, nesta ordem: a VGA escolhida, o ponto de Marcar setor, ou a ponta aberta do tubo mais grosso (nunca um tampão; a mais baixa no empate).
9. `AplicarConexoes`: comprimento equivalente de cada peça, depois de achar a fonte.
10. Numera nós e trechos a partir da fonte.

### Onde entra a perda de cada peça

| Peça | Regra |
|---|---|
| Cotovelo | No trecho de um dos lados; no outro, se deste não há trecho |
| Válvula | Pelo nome da família (borboleta, gaveta, retenção); desconhecida fica fora, com aviso |
| Tê e cruzeta | Só nos conectores fora da passagem (`Ramos`) |

A passagem em `Ramos` é escolhida pelos critérios abaixo, nesta ordem:

1. Par alinhado de maior diâmetro.
2. Maior diâmetro do que está ligado depois da peça.
3. Par por onde a água entra.
4. Eixo do par, que não depende da ordem dos conectores.

### Verificações que decidem ATENDE ou NÃO ATENDE: `CalculoSpk.Verificar`

| Verificação | Item citado no código |
|---|---|
| Convergência do cálculo | 9.4.3.1 |
| Área de operação com o número de bicos exigido | 9.4.4.2 ou a referência da base |
| Pressão mínima em todos os bicos da área | 9.4.4.10 |
| Vazão de cada bico ≥ densidade × área | 9.4.4.6 |
| Bicos cobrem a área de operação | 9.4.4.2; IT-23/2025 10.4.3 a |
| Resposta rápida com pelo menos 5 bicos | 8.5.5.3.2 |
| Área mínima, em base de número fixo | Referência da base |
| 1 210 kPa em risco extra | 9.4.4.11 |
| 2 070 kPa em tubo de aço | 5.3.2 |
| Diâmetro mínimo | 9.4.1.2 |
| Área do setor por coluna | 7.3, Tabela 8 |
| Abastecimento atende, se houver curva | 9.3.4, Anexo B |
| Teto alto: K, densidade, área e duração | IT-23/2025 8.16.7 (`TetoAlto.Verificar`) |
| Obstrução por viga | Tabela 15 (`ObstrucaoDeBico`) |

Só avisam, sem reprovar:

- Bicos soltos.
- K do esboço, não confirmado.
- Inclinação do teto.
- Fator C acima da Tabela 30.
- Tubo fora da norma pelo nome e pelo interno.
- Peça que parece treliça.
- Risco leve sem confirmação de resposta rápida.

Não há verificação de velocidade: ela só sai no memorial e nos parâmetros.

## Verificar modelo: `VerificacaoModelo`

| Código | Gravidade | O que acusa |
|---|---|---|
| `encostado-sem-ligar` | Erro | Peças encostadas, sem ligação |
| `derivacao-sem-conexao` | Erro | Tubo que termina no meio de outro, sem tê |
| `bico-sem-tubo` | Erro | Bico sem tubo |
| `bico-sem-conector` | Erro | Família de bico sem conector |
| `bico-sem-vga` | Erro | Bicos que não chegam a nenhuma VGA |
| `sistema-nao-incendio` | Erro | Tubos ligados aos bicos fora de sistema de incêndio |
| `bico-nao-mapeado` | Erro | Bicos sem K e área confirmados |
| `ponta-aberta` | Aviso | Tubo ou conexão com saída livre |
| `sem-vga` | Aviso | Nenhuma VGA marcada como Fonte |
| `bico-k-do-esboco` | Aviso | Bicos com K e área de Dados do esboço |
| `entrada-sem-vga` | Informação | Entrada da rede sem VGA |

Só a rede ligada a bicos entra no escopo. O botão Corrigir usa `Reconexao` (encostados) e `Derivacoes` (tê que falta, tê mecânico fora do eixo).

## O que fica gravado

| Onde | O quê |
|---|---|
| `.rvt`, por `ArmazenamentoSpk` | Dados do projeto, Dados do esboço, mapeamento de famílias, entradas de setor, último resultado e assinatura |
| Parâmetros SPK nos elementos (`ParametrosSpk`) | Número, setor, área de cálculo, pressão, vazão, velocidade |
| `Documentos\SPK\calculo` | `memorial.md` e `memorial.docx`: um arquivo só, sobrescrito a cada cálculo |
| `Documentos\SPK\testes` | Fila, relatório e saídas do teste automático |

A assinatura (`AssinaturaDaRede`) detecta cálculo velho: as Folhas recusam, o Memorial pergunta e o Guia avisa.

## Testes

| Camada | Onde | Situação em 05/11/2026 |
|---|---|---|
| Motor | `motor-spk/testes/Spk.Motor.Testes` | 179 de 179, na rodada de 04/11; o motor não mudou depois |
| Regressão no Revit 2027 | `docs/refinamentos/spk/testes/regressao.json`, 62 cenários, cerca de 40 min | 62 de 62 |
| Roteiro à mão | [roteiro-teste.md](roteiro-teste.md) | MNEVVM: faltam gerar a rede de novo e seguir do passo 6 |

Como o teste automático funciona: `Automacao/TesteAutomatico.cs` lê `Documentos\SPK\testes\fila.json` quando o Revit abre, roda nas cópias dos modelos e grava `relatorio.json` e `concluido.txt`. Detalhes em [teste-automatico.md](teste-automatico.md).

## Pontos sensíveis ao mexer

- **Ordem dos conectores:** o Revit muda a ordem a cada sessão. `Conectores` ordena por id, mas refazer uma peça troca qual tubo fica em cada id. Nada pode depender dessa ordem no cálculo.
- **Trecho `n == 0`:** não recebe DN nem aparece no memorial, mas o tubo existe e influencia a peça ao lado.
- **Subgeral partida:** `TracarRamal` parte a subgeral, e os pedaços não entram em `criados`. Para achar a ponta, use `lida.Tubos`.
- **Trecho entre peças:** usa o interno do mesmo DN no segmento; o DN do conector não é o interno.
- **`RedeLida.Tubos`:** liga o id do trecho ao tubo, e vários trechos podem estar no mesmo tubo.
- **Transações:** cada ambiente fica em grupo próprio. Use `TransacaoSegura` para receber o motivo da recusa em vez de exceção.
