# Teste automático no Revit

O plugin roda cenários de teste sozinho, dentro do Revit de verdade, e grava um relatório. Serve para conferir resultados (cálculo, folhas) e para pegar regressão a cada mudança, sem ninguém clicar.

## Como funciona

1. Escreve-se a fila de cenários em `Documentos\SPK\testes\fila.json`.
2. Abre-se o Revit (pelo terminal ou à mão). Ao iniciar, o plugin vê a fila e, no primeiro momento ocioso, roda cada cenário:
   - abre uma **cópia** do projeto em `testes\saida\<nome>.rvt` (o original nunca é tocado) ou cria um projeto novo;
   - grava os critérios e os ajustes de mapeamento pedidos;
   - roda Rede de exemplo, Calcular e Folhas pelo mesmo código dos botões (`ServicoCalculo`, `ServicoFolhas`, `RedeExemploCommand.Criar`), sem janelas;
   - responde sozinho os diálogos do Revit ("referência não encontrada" é ignorada; o resto é fechado) e anota cada um;
   - salva a cópia e fecha.
3. Grava `testes\relatorio.json` depois de cada cenário, `testes\concluido.txt` no fim, renomeia a fila para `fila-executada.json` e fecha o Revit.

Sem `fila.json` na pasta, nada disso é ligado: o Revit abre normalmente.

## A fila

```json
{
  "fecharRevit": true,
  "cenarios": [
    {
      "nome": "exemplo",
      "abrir": "C:\\Users\\...\\Documents\\Projeto2.rvt",
      "dados": { "Classe": "Armazenamento", "Base": "Esfr", "PressaoMinimaKPa": 283,
                 "ItemNorma": "IT-24 Tabela 5.3.4.3.1", "VazaoHidrantesLmin": 1900, "DuracaoMin": 60, "FatorC": 120 },
      "redeExemplo": true,
      "calcular": {},
      "folhas": true
    },
    {
      "nome": "obra-setor-vga",
      "abrir": "C:\\...\\obra.rvt",
      "mapeamento": [
        { "familia": "Sprinkler-Tyco-Pendent-TY-L", "papel": "Chuveiro", "fatorK": 360, "area": 9 },
        { "familia": "NPT004", "papel": "Ignorar" },
        { "familia": "", "tipo": "405-NRS-RW-4", "papel": "Fonte" }
      ],
      "calcular": { "vga": 10272354 },
      "folhas": true
    }
  ]
}
```

| Campo | O que faz |
|---|---|
| `abrir` | Projeto de origem; o teste trabalha numa cópia. |
| `novo` | `true`: projeto novo pelo modelo padrão do Revit (que não tem tubo com tê; a Rede de exemplo precisa de um projeto que tenha). |
| `dados` | Grava Dados do projeto (mesmos campos de `DadosProjeto`). |
| `vincular` | Vincula modelos que já existem, origem interna com origem interna, logo depois de abrir: `arquivos` (caminhos), `apagarRede` (padrão sim: apaga bicos, tubos, conexões e acessórios da cópia) e `copiarNiveis` (padrão sim: cria os níveis dos vínculos que o projeto não tem, cada um com uma planta). Serve para testar com modelos de terceiros, como os exemplos que vêm com o Revit (Snowdon Towers em `C:\Program Files\Autodesk\Revit 2027\Samples`, copiados para `Documents\SPK\modelos-teste`). |
| `mapeamento` | Ajusta o mapeamento de Famílias: acha linhas pela família (e tipo) por trecho do nome; cria a linha se a família existe no modelo e ainda não tem. |
| `redeExemplo` | Cria a malha de exemplo. |
| `calcular` | `{}`: o padrão. `vga`: id da VGA (os ids saem no relatório, em `Vgas`). `setor`: sem VGA, o pedaço de rede pela ordem (0 = maior). `elemento`: id de um bico ou tubo do setor. O memorial do cenário fica em `testes\saida\<nome>-memorial.md`, para comparar trecho a trecho. |
| `folhas` | Gera as folhas; o PDF vai para `testes\saida\<nome>\`. |
| `padrao` | Grava no Padrão do escritório `numeroInicial`, `codigoFolha` e `parametroCodigo` antes de gerar as folhas. |
| `sabotar` | Estraga a malha gerada de propósito, sempre no mesmo elemento: `desligarBico`, `apagarTe` (tê entre três tubos), `sistemaAgua` (tubo em sistema de água fria), `desligarTubo` (uma ponta do tubo de maior diâmetro). |
| `cena` | Monta uma cena do zero para testar uma ferramenta de modelagem (apaga antes a malha gerada da cópia). `ramalComBicosSoltos`: ramal de 15 m com descida, 5 bicos soltos embaixo (2 de lado) e 1 depois do fim do ramal. |
| `tracarRamal` | Roda Traçar ramal com os bicos da cena e o primeiro tubo dela como subgeral (cena `subgeralComBicosSoltos`: subgeral de 9 m com descida e fileira de 5 bicos, 1 de lado). |
| `ramais` | Traçar ramais com opções: `altura` (`NaSubgeral`, `RelativaASubgeral`, `RelativaAoPiso`), `alturaM`, `toleranciaFileiraM`, `ligarBicos`. Cena `subgeralComFileiras`: 3 fileiras de 4 bicos dos dois lados da subgeral, 1 de lado. No esperado, `ramaisTracados`. |
| `ligarBicos` | Roda Ligar bicos com os bicos e ramais da cena e põe no relatório os ligados e os motivos dos recusados (`bicosLigados`, `bicosRecusados` no esperado). |
| `arquitetura` | Lê a arquitetura e a estrutura (arquivo e vínculos) e põe uma linha por ambiente e por viga em `Arquitetura`. No esperado, `textoDaArquitetura`. Cena `arquiteturaVinculada`: gera dois modelos pelos templates brasileiros do Revit (2 salas, forro a 2,80 m numa, laje a 4 m; 1 viga) e os vincula deslocados 20 m e 10 m. |
| `ambientes` | Só os ambientes cujo nome contém um destes textos, em `bicosPorAmbiente` e `gerarRede` (vazio: todos). |
| `ligarAGeral` | Depois de gerar a rede: desenha a geral (`pontos` [[x, y], ...] em m nas coordenadas do projeto, `alturaM` acima do nível, `dn`, `descidaNoInicio` = subida da VGA até o piso no primeiro ponto) e roda Ligar à geral nas redes soltas. Texto em `Geral`. A cena `andarComSalas` vincula um corredor de 24 × 2,5 m com 4 salas de 6 × 8 m (deslocado 20 m e 10 m). |
| `editarBicos` | Depois de gerar e ligar a rede, em ordem: `{"op": "apagar", "perto": [x, y]}` (o bico mais perto), `{"op": "inserir", "em": [x, y]}` (no ramal mais perto), `{"op": "mover", "perto": [x, y], "para": [x, y]}`. Texto em `Edicoes`. |
| `gerarRede` | Gerar rede em todos os ambientes (opções: `arranjo` `EspinhaDePeixe` ou `Lateral`, `dropM`, `aplicarDiametros`, `bicos`, `geralPor` = parte do nome do corredor para juntar as salas num setor). Uma linha por ambiente em `Rede`. No esperado, `textoDaRede`. A cena `arquiteturaVinculada` apaga antes a rede que a cópia do projeto tiver. |
| `bicosPorAmbiente` | Bicos por ambiente em todos os ambientes, com opções (`defletorM`, `abaixoDaVigaM`, `teto`, `direcao`, `tipo`; `{}` usa o padrão), e depois a conferência dos bicos que ficaram. Uma linha por ambiente em `Bicos` ("posicionar | ..." e "conferir | ..."). No esperado, `textoDosBicos`. |
| `inspecionarVinculos` | Diagnóstico: lista os vínculos (estado, origem, rotação) e quantos ambientes, forros, lajes, vigas, pilares, paredes e telhados há em cada um. |
| `diametros` | Diâmetros por tabela no setor (mesmos campos de `calcular`: `{}`, `vga`, `elemento`, `setor`); no relatório, tubos por DN, recusas, divergências e avisos. No esperado, `diametros` com os tubos por DN (`{"25": 8}`), que exige também zero recusa e divergência. |
| `listarConexoes` | Diagnóstico: com `diametros`, lista cada conector de tubo, peça e bico (posição, DN e a quem liga) antes e depois, em `ConexoesAntes` e `Conexoes`. |
| `reconectar` | Roda o Corrigir de Verificar modelo nas peças encostadas (depois de sabotar, antes de verificar) e põe no relatório quantas eram, quantas ligou, os motivos das recusadas e as ligações do modelo antes e depois. |
| `verificar` | Roda Verificar modelo e põe no relatório cada grupo de problema com a quantidade e exemplos. |
| `inspecionarCarimbo` | Lista os parâmetros da folha e do carimbo (até 8 folhas do projeto e, depois das folhas, as do SPK). Mostra onde o escritório guarda código, revisão e número. |
| `esperado` | O que o cenário deve dar; o relatório traz `Passou` e `Divergencias`. Campos: `atende`, `bicos`, `pressaoKPa` (±0,5), `critico`, `folhas` (quantidade), `verificacao` (código do grupo, ou `erros`, `avisos`, `setores`, com `"0"`, `"3"`, `">=1"`, `"<=2"`) `textoDasFolhas` (trechos que precisam aparecer no texto das folhas) e `reconexaoSemPerda` (ligações depois ≥ antes + feitas: a correção não desfez nenhuma ligação). |

## O relatório

Por cenário: `Ok`, `Erro` (com a pilha), `Etapas` (nome, segundos, texto do resultado ou erro), `Calculo` (atende, setor, bicos, trechos, pressão, vazão, crítico, falhas, avisos), `Folhas` (números, PDF, textos e `Vistas`: para cada vista 3D e planta do SPK, template, escala, estilo, se esconde anotações, quantas etiquetas existem e quantas aparecem), `Dialogos` respondidos e `Vgas` com, para cada válvula Fonte, a posição, cada conector (ligado ou não, distância da ponta e do corpo do tubo mais próximo) e um rastreio da cadeia de tubos a partir dela até o primeiro vão.

## O que ainda pede clique

Duas telas aparecem antes do plugin carregar e o teste não consegue responder:

- **Licenciamento do trial da Autodesk** ("Estamos com problemas"): fechar no X.
- **Aviso de plugin sem assinatura** ("Sempre carregar"): volta a cada build, porque a DLL muda. Some com a DLL assinada por certificado de código (passo de distribuição).

## Cenários de regressão

A suíte fica em [testes/regressao.json](testes/regressao.json), com o resultado esperado de cada cenário. Para rodar: copiar para `Documentos\SPK\testes\fila.json`, abrir o Revit e, no fim, olhar `Passou` de cada cenário no relatório.

Inteira ela leva uns 30 minutos. Para rodar em pedaços, [testes/fila.py](testes/fila.py) monta a fila de um grupo: `python -I fila.py calculo` (exemplos, sabotagens e numeração de folhas, ~8 min), `assistida` (ferramentas de modelagem, ~6 min), `geracao` (arquitetura, bicos, Gerar rede, setor, corredor, VGA, vigas, ~7 min) ou `nova-era` (reconectar, derivações e o cálculo da maior rede, ~12 min); `tudo` monta a suíte inteira. Cenário novo sem grupo cai em `assistida`, com aviso. São 43:

| Grupo | Cenários | O que prova |
|---|---|---|
| Cálculo e folhas | os 5 da Rede de exemplo | valores de cálculo, 4 folhas, 2 quadros no isométrico, verificação sem problema na malha gerada |
| Sabotagem | bico desligado, tê apagado, sistema de água fria, tubo desligado | Verificar modelo acha cada problema, no elemento certo |
| Modelagem | cena com ramal e bicos soltos; cena com subgeral e fileira de bicos; a mesma com Diâmetros; 3 fileiras dos dois lados, com subida e com cruzeta | Ligar bicos liga 5 e recusa 1; Traçar ramal traça e liga 5; Diâmetros põe 8 tubos em DN 25, 1 em 32 e 4 em 40, com os cotovelos refeitos no DN novo e nenhuma redução entre tubos; Traçar ramais traça 3 ramais e liga os 12 bicos nos dois modos; verificação limpa |
| Vigas fundas | `salaComVigas` (três vigas paralelas com o fundo 60 cm abaixo da laje) e `salaComGrelha` (vigas nas duas direções) | vigas paralelas: 8 bicos nos vãos, defletor 25 cm abaixo da laje, ramais nos vãos a 3,75 m e subgeral por baixo das vigas a 3,10 m, verificação limpa, 4 folhas; grelha: 8 bicos nos quadros com espaçamento ok, e o Gerar rede recusa com bico pendente (pede bico em pé) |
| Editar bicos | o setor automático, depois apagar um bico (cruzeta vira tê), inserir outro num ramal e mover um terceiro 70 cm de lado (religado com braço) | 16 bicos, 1 setor, sem erro nem aviso, ATENDE |
| Conferir na planta | o setor automático, depois inserir um bico a 45 cm de outro e apagar um bico da sala 1 | na planta do nível: 16 bicos conferidos, 16 círculos de alcance, 3 bicos fora da regra (os dois juntos e o vizinho do apagado) e 3 trechos sem cobertura (duas paredes da sala 1 e o corredor), com a imagem da planta em `saida` |
| Marcar setor | o setor automático com a descida da VGA marcada "VGA 01"; as 4 salas em redes separadas, com "VGA 03" e "VGA 01" marcadas | setor único: o cálculo, o memorial e as folhas saem como VGA 01 (SPK-VGA01-01), entrada na descida, ATENDE; salas separadas: setorização com VGA 01, VGA 03, Setor 2 e Setor 4, e o cálculo da VGA 01: ATENDE, a sala de 48 m² opera inteira com a densidade de 140 m² (IT-23/2025 10.4.3 a) |
| Cores e material | o setor automático marcado VGA 01 e calculado; cores por diâmetro, setor, velocidade e pressão na planta (uma imagem de cada em `saida`), depois Tirar cores; lista de material | as quatro pinturas com legenda, tudo volta ao normal; resumo com metros por DN e as tabelas SPK criadas |
| Mover bicos | o setor automático; o último bico de um ramal 50 cm para fora, dois bicos 30 cm na linha dos ramais (mover vários) e um bico 70 cm de lado | os três primeiros deslizam (ramal estica ou encolhe), o de lado é religado; 16 bicos, verificação sem erro, ATENDE |
| Derivações sem tê | `cena-derivacoes`: uma geral com cinco tubos chegando no corpo dela; `nova-era-derivacoes`: a cópia da Nova Era depois de reconectar | cena: tê, cruzeta (dois de lados opostos) e duas recusas com motivo (3 cm fora do eixo, perto da ponta); Nova Era: das 1 032 derivações sobram no máximo 310, todas recusadas com motivo (as de grupo de modelo com o nome do grupo), sem perder ligação; `nova-era-desagrupado`: a mesma cópia com os grupos de modelo desfeitos (etapa `desagrupar`), sobram no máximo 100 (tê mecânico levado ao eixo), e a maior rede (307 bicos ESFR) calcula: ATENDE com 1 838,3 kPa, o mesmo número em qualquer sessão do Revit |
| Tê mecânico | `cena-te-mecanico`: a cena cria a própria família de tê mecânico (do modelo "Conexão hidráulica métrica" do Revit, como conexão de tubo) e põe 4 tês 34 mm abaixo do eixo de um tubo DN65, cada um com bico: por descida, direto, fixado e em grupo | os dois primeiros encaixados (o tê e o que está pendurado nele sobem até o eixo), o fixado e o de grupo recusados com o motivo |
| Ligar tubos | tubos soltos desenhados pelo teste, longe da arquitetura | tê no meio do destino (rede de 4 peças), tê com subida de 40 cm (9 peças), cotovelo no canto com a ponta do destino (3 peças) e recusa de tubos em linha |
| Corredor em L | `andarEmL` (corredor em L de 16,5 m + 18,5 m, 5 salas, uma no canto), Gerar rede com `geralPor` = Corredor, Marcar setor, cores por diâmetro | geral pelas duas pernas com cotovelo no canto, no centro delas, as 5 salas ligadas (cada uma na perna que tem à frente) e os 8 bicos do corredor (com forro) ligados direto na geral, um deslocado 25 cm do tê de uma sala; 28 bicos, verificação sem erro, ATENDE como VGA 01, 4 folhas |
| VGA no meio | `andarComSalas` com `entradaDaGeral` em x = 32 m (meio do corredor) | descida no meio, geral para os dois lados com tê, 4 salas e 6 bicos do corredor ligados, 22 bicos, verificação sem erro, ATENDE como VGA 01 |
| Setor automático | `andarComSalas`, Gerar rede nas salas com `geralPor` = Corredor | as 4 salas com a entrada voltada para o corredor, geral pelo eixo dele, e os 6 bicos do corredor (só laje, defletor 30 cm abaixo dela) ligados direto na geral: 1 setor, 22 bicos, verificação sem erro nem aviso, ATENDE; 4 folhas |
| Setor com várias salas | `andarComSalas`, rede nas 4 salas, geral pelo corredor a 3,00 m e a 3,40 m (com subida) | as 4 redes ligadas: 1 setor, 16 bicos, verificação sem erro e sem ponta aberta, ATENDE; 4 folhas |
| Gerar rede | arquitetura vinculada, risco leve, espinha de peixe e lateral | na sala com forro: 4 bicos, rede a 3,00 m, 2 ramais, diâmetros da tabela, verificação sem erro, ATENDE e 4 folhas; o depósito com viga e laje baixa é recusado com o motivo (sem espaço entre o defletor e o teto) |
| Bicos por ambiente | arquitetura vinculada, risco leve | 4 bicos em cada sala, defletor 100 mm abaixo do forro (2,70) e 50 mm abaixo da viga (3,65, a 203 mm do teto); a conferência dos bicos criados no Revit confirma as alturas e o espaçamento |
| Arquitetura | vínculos gerados pelo teste; Nova Era com vínculos descarregados | teto pelo forro (2,80) e pela laje (3,85), viga com fundo a 3,70, coordenadas com o deslocamento do vínculo; os vínculos descarregados aparecem com o nome |
| Janelas | todos | nenhum cenário pode abrir janela do Revit que o engenheiro teria de fechar (antes, a Nova Era abria 55 no Corrigir) |
| Correção | bico e tubo desligados e corrigidos; Nova Era corrigida | o Corrigir religa sem perder ligação e o cálculo volta ao valor original (1.757,6 kPa); na Nova Era, 910 de 971 ligadas |
| Nova Era | verificação da cópia do modelo real | acha as peças encostadas sem ligação (971), as derivações sem tê, os 610 tubos em sistema hidrônico e os bicos não confirmados, em menos de 1 s |
| Carimbo | Nova Era numerada a partir de 17; Nova Era a partir de 13; Projeto2 a partir de 01 | número, código e campos do projeto preenchidos; número já usado por folha do escritório é recusado sem apagar nada; parâmetro de código ausente é avisado |

O Projeto2 tem um bico solto (333229), longe da rede, que a verificação aponta com razão: por isso os cenários da malha esperam `bico-sem-tubo` 1.

Valores de cálculo dos cinco cenários da Rede de exemplo, em 26/10/2026:

| Cenário | Esperado |
|---|---|
| Simples (12 ESFR) | 1 setor, 12 bicos, ATENDE, 1.757,6 kPa, crítico a 2,83 bar e 606 L/min, 4 folhas, 15 etiquetas, 1 descida |
| Depósito (espinha de peixe, 2 setores) | 2 setores de 80 bicos, ATENDE, 777,2 kPa, 28 áreas avaliadas, setorização com 2 cores e mapa-chave |
| Galpão (grelha, ordinário 2) | 1 setor de 56 bicos em anel, ATENDE, 284,5 kPa, crítico no limite da densidade (97,2 L/min) |
| Edifício de 2 pavimentos (ordinário 1) | 2 setores de 30 bicos, um por nível, ATENDE, 286,7 kPa, setorização com 2 plantas |
| Residência / escritório (risco leve) | 1 setor de 30 bicos, ATENDE, 346,5 kPa |

O resultado esperado é o desta rodada: serve para pegar mudança involuntária, não é conferência de engenharia.

Os cenários de projeto real entram quando houver um projeto bem conectado e conferido pelo engenheiro (ver [aprendizados-nova-era.md](aprendizados-nova-era.md)).

## Cuidado: relógio do Windows

Se o relógio do Windows voltar no tempo, os arquivos compilados ficam "mais novos" que o código e o `dotnet build` conclui que não há nada a refazer: a DLL publicada fica velha sem aviso. Em 26/10/2026 isso aconteceu e uma rodada da suíte testou a versão anterior. Na dúvida, compilar com `--no-incremental` e conferir a hora da DLL em `bin\Debug.R27\publish\Spk.AddIn`.
