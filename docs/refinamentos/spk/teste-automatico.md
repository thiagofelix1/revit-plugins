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

## O relatório

Por cenário: `Ok`, `Erro` (com a pilha), `Etapas` (nome, segundos, texto do resultado ou erro), `Calculo` (atende, setor, bicos, trechos, pressão, vazão, crítico, falhas, avisos), `Folhas` (números, PDF, textos), `Dialogos` respondidos e `Vgas` com, para cada válvula Fonte, a posição, cada conector (ligado ou não, distância da ponta e do corpo do tubo mais próximo) e um rastreio da cadeia de tubos a partir dela até o primeiro vão.

## O que ainda pede clique

Duas telas aparecem antes do plugin carregar e o teste não consegue responder:

- **Licenciamento do trial da Autodesk** ("Estamos com problemas"): fechar no X.
- **Aviso de plugin sem assinatura** ("Sempre carregar"): volta a cada build, porque a DLL muda. Some com a DLL assinada por certificado de código (passo de distribuição).

## Cenários de regressão

| Cenário | Esperado |
|---|---|
| Malha de exemplo (cópia do Projeto2) | 12 bicos, ATENDE, crítico a 2,83 bar e 606 L/min, 4 folhas, etiquetas, 1 descida |

Os cenários de projeto real entram quando houver um projeto bem conectado e conferido pelo engenheiro (ver [aprendizados-nova-era.md](aprendizados-nova-era.md)).
