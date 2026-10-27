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
| `mapeamento` | Ajusta o mapeamento de Famílias: acha linhas pela família (e tipo) por trecho do nome; cria a linha se a família existe no modelo e ainda não tem. |
| `redeExemplo` | Cria a malha de exemplo. |
| `calcular` | `{}`: o padrão. `vga`: id da VGA (os ids saem no relatório, em `Vgas`). `setor`: sem VGA, o pedaço de rede pela ordem (0 = maior). `elemento`: id de um bico ou tubo do setor. |
| `folhas` | Gera as folhas; o PDF vai para `testes\saida\<nome>\`. |
| `padrao` | Grava no Padrão do escritório `numeroInicial`, `codigoFolha` e `parametroCodigo` antes de gerar as folhas. |
| `sabotar` | Estraga a malha gerada de propósito, sempre no mesmo elemento: `desligarBico`, `apagarTe` (tê entre três tubos), `sistemaAgua` (tubo em sistema de água fria), `desligarTubo` (uma ponta do tubo de maior diâmetro). |
| `cena` | Monta uma cena do zero para testar uma ferramenta de modelagem (apaga antes a malha gerada da cópia). `ramalComBicosSoltos`: ramal de 15 m com descida, 5 bicos soltos embaixo (2 de lado) e 1 depois do fim do ramal. |
| `tracarRamal` | Roda Traçar ramal com os bicos da cena e o primeiro tubo dela como subgeral (cena `subgeralComBicosSoltos`: subgeral de 9 m com descida e fileira de 5 bicos, 1 de lado). |
| `ramais` | Traçar ramais com opções: `altura` (`NaSubgeral`, `RelativaASubgeral`, `RelativaAoPiso`), `alturaM`, `toleranciaFileiraM`, `ligarBicos`. Cena `subgeralComFileiras`: 3 fileiras de 4 bicos dos dois lados da subgeral, 1 de lado. No esperado, `ramaisTracados`. |
| `ligarBicos` | Roda Ligar bicos com os bicos e ramais da cena e põe no relatório os ligados e os motivos dos recusados (`bicosLigados`, `bicosRecusados` no esperado). |
| `arquitetura` | Lê a arquitetura e a estrutura (arquivo e vínculos) e põe uma linha por ambiente e por viga em `Arquitetura`. No esperado, `textoDaArquitetura`. Cena `arquiteturaVinculada`: gera dois modelos pelos templates brasileiros do Revit (2 salas, forro a 2,80 m numa, laje a 4 m; 1 viga) e os vincula deslocados 20 m e 10 m. |
| `gerarRede` | Gerar rede em todos os ambientes (opções: `arranjo` `EspinhaDePeixe` ou `Lateral`, `dropM`, `aplicarDiametros`, `bicos`). Uma linha por ambiente em `Rede`. No esperado, `textoDaRede`. A cena `arquiteturaVinculada` apaga antes a rede que a cópia do projeto tiver. |
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

A suíte fica em [testes/regressao.json](testes/regressao.json), com o resultado esperado de cada cenário. Para rodar: copiar para `Documentos\SPK\testes\fila.json`, abrir o Revit e, no fim, olhar `Passou` de cada cenário no relatório. São 26:

| Grupo | Cenários | O que prova |
|---|---|---|
| Cálculo e folhas | os 5 da Rede de exemplo | valores de cálculo, 4 folhas, 2 quadros no isométrico, verificação sem problema na malha gerada |
| Sabotagem | bico desligado, tê apagado, sistema de água fria, tubo desligado | Verificar modelo acha cada problema, no elemento certo |
| Modelagem | cena com ramal e bicos soltos; cena com subgeral e fileira de bicos; a mesma com Diâmetros; 3 fileiras dos dois lados, com subida e com cruzeta | Ligar bicos liga 5 e recusa 1; Traçar ramal traça e liga 5; Diâmetros põe 8 tubos em DN 25, 1 em 32 e 4 em 40, com os cotovelos refeitos no DN novo e nenhuma redução entre tubos; Traçar ramais traça 3 ramais e liga os 12 bicos nos dois modos; verificação limpa |
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
