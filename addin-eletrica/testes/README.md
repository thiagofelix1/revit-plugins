# Teste automático do AutoEletrica no Revit

O mesmo esquema do [teste automático do SPK](../../docs/refinamentos/spk/teste-automatico.md): o plugin roda cenários sozinho, dentro do Revit de verdade, pelo mesmo código dos botões, e grava um relatório. Serve para pegar exceção e regressão sem ninguém clicar, antes de mandar para os engenheiros e a cada mudança.

## Rodar

```bash
py -3 -I addin-eletrica/testes/fila.py tudo
```

Depois abra o Revit 2027 (com o add-in de desenvolvimento instalado). Duas telas aparecem antes do plugin carregar e o teste não consegue responder: o aviso de licença do trial (fechar no X) e o aviso de add-in sem assinatura ("Sempre carregar"; volta a cada build). Em seguida o plugin roda a fila, responde os diálogos do Revit, salva as cópias e fecha o Revit.

No fim, em `Documentos\AutoEletrica\testes`:

- `concluido.txt`: uma linha por cenário, `ok` ou `FALHA`;
- `relatorio.json`: por cenário, cada etapa com tempo, texto e erro (com a pilha), as métricas, as divergências contra o esperado, os diálogos respondidos, e listas de conferência (ambientes, pontos lançados, circuitos propostos, recusas do traçado, avisos da passagem, problemas do Verificar, circuitos calculados, folhas);
- `saida\`: as cópias dos projetos depois do teste (abra para olhar as plantas, pranchas e quadros), as famílias e a arquitetura geradas pela cena, e os memoriais em `Documentos\AutoEletrica\calculo`.

Para mandar o resultado: o `relatorio.json` basta.

## Os cenários (`regressao.json`)

| Cenário | Projeto | O que prova |
|---|---|---|
| `apto-completo` | Novo, pelo template elétrico brasileiro, com a cena `apartamento` | O fluxo inteiro sem clique: ler ambientes do vínculo, lançar pontos por ambiente (tomadas, luz, interruptor com comando, chuveiro), Verificar antes dos circuitos (acusa ponto sem circuito), Criar circuitos, traçar eletrodutos em estrela, Passar circuitos (com retornos), Verificar (sem ponto sem circuito nem sem caminho, divisão de circuitos certa, toda luminária com comando), Caixas por regra e Verificar de novo (sem trecho acima de 15 m nem com mais de 3 curvas), Calcular, Desenhar fiação, Etiquetar, Legenda, Quadros, Lista de material e Pranchas. |
| `apto-tt` | Idem, esquema TT com RA de 80 Ω | O Calcular pelo caminho do TT dentro do Revit. |
| `snowdon-mecanica` | Cópia do Snowdon Towers Sample Electrical (exemplo da Autodesk que vem com o Revit), com os outros modelos do Snowdon copiados ao lado para os vínculos resolverem | A mecânica num projeto de terceiros grande e já modelado: ler quadros, circuitos, pontos e eletrodutos de um modelo que o plugin não criou, passar circuitos, verificar, calcular, desenhar fiação, etiquetar e gerar todos os entregáveis sem erro. O Snowdon é americano (120/208 V, tomadas NEMA, escritórios): os números da NBR saem cheios de avisos, e isso é esperado. Ele prova que o código roda, não que a engenharia está certa. |

## A cena `apartamento`

Tudo sai dos templates que vêm com o Revit em português, nada de família de cliente:

- **Arquitetura** (`DefaultBRAPTB.rte`) num arquivo próprio, vinculado deslocado 20 m e 10 m: apartamento de 10 × 8 m com sala, cozinha, área de serviço, circulação, banheiro e dois dormitórios, paredes e pé-direito de 2,80 m, seis portas.
- **Famílias de teste** criadas pelos templates de família (instalação elétrica, luminária, equipamento elétrico): tomada 10 A (127 V, 100 VA), luminária (127 V, 100 VA), interruptor, chuveiro (220 V, 2 polos, 5 500 VA), quadro (painel, 220 V trifásico) e caixa de passagem; não hospedadas, com conector elétrico. Carimbo A1 pelo template, se o projeto não tiver.
- **Sistema de distribuição** 127/220 V trifásico (criado com as tensões, se o template não tiver), **quadro QD-1** na circulação a 1,50 m, **padrão** com essas famílias, o tipo de eletroduto do template, DN 25 e passagem a 2,70 m, e os **usos** dos ambientes confirmados pelo nome.

O traçado em estrela (passo `tracarEstrela`) liga, em cada ambiente, o quadro à luminária e a luminária a cada outro ponto, pela cota de passagem: é o que o projetista desenharia num apartamento pequeno, sem o roteamento automático da 1.0.

## Escrever um cenário

```json
{
  "nome": "meu-projeto",
  "abrir": "C:\\caminho\\projeto.rvt",
  "dados": { "TensaoFaseNeutro": 127, "TensaoFaseFase": 220, "Fases": 2, "Esquema": "TNCS", "IccPresumidaKA": 6, "Habitacao": true },
  "usosSugeridos": true,
  "passos": ["verificar", "passarCircuitos", "calcular", "pranchas"],
  "esperado": { "verificar.M-03": "0", "pranchas.folhas": ">=3", "etapasComErro": "0" }
}
```

| Campo | O que faz |
|---|---|
| `abrir` | Projeto de origem (aceita `{Samples}` e `{Documentos}`); o teste trabalha numa cópia em `saida`. |
| `novo` | Nome do template de projeto (busca nas pastas de templates do Revit): cria um projeto novo. |
| `copiarJunto` | Arquivos copiados para `saida` antes de abrir (aceita `*` no nome), para vínculos de caminho relativo resolverem. |
| `dados` | Dados do projeto (os campos da janela, incluindo as opções de cálculo). |
| `cena` | `apartamento`. |
| `usosSugeridos`, `usos` | Confirma o uso sugerido pelo nome de cada ambiente; `usos` troca por trecho do nome (`{"Copa": "Cozinha"}`). |
| `lancamento`, `passagem` | Sobrescrevem o padrão do escritório (alturas, potências, limites; teto, piso ou nível dos pontos). |
| `passos` | Em ordem: `ambientes`, `parametros`, `pontosPorAmbiente`, `criarCircuitos`, `tracarEstrela`, `passarCircuitos`, `caixasPorRegra`, `verificar`, `calcular`, `desenharFiacao`, `etiquetar`, `legenda`, `quadros`, `lista`, `pranchas`. Passo repetido ganha `#2`, `#3` nas métricas. |
| `esperado` | Métrica → condição (`"0"`, `"3"`, `">=1"`, `"<=2"`, `"true"`). As métricas são `passo.nome` (ex.: `verificar#2.M-03` é a contagem do código M-03 no segundo Verificar; código que não apareceu vale zero), mais `etapasComErro` e `dialogos` (janelas do Revit que o engenheiro teria que fechar). |
| `salvar` | Padrão `true`: salva a cópia no fim. |

## O que ainda não está coberto

- Ferramentas que dependem de clique (Inserir ponto, Comando, Traçar eletroduto, Ligar pontos, Alimentar quadro, Fiação manual): o código por trás delas é o mesmo dos passos, mas a seleção na tela não é testada.
- As janelas (Padrão, Famílias, Ambientes, Criar circuitos, Verificar): o teste usa os dados direto.
- Olho humano nas plantas e pranchas: o relatório conta folhas, vistas e etiquetas; se o desenho ficou bonito é com você.
