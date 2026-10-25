# Roteiro de teste no Revit — ramo `noite/correcoes-e-folhas`

Tudo o que está aqui compila para o Revit 2025, 2026 e 2027, e o motor passa nos 41 testes. Nada do lado do Revit foi rodado dentro do Revit. Este roteiro diz o que clicar e o que deve aparecer em cada passo. Quando algo não bater, anote o passo, tire um print do diálogo (a lista "Avisos" é o que mais ajuda) e seguimos daí.

## 0. Preparar

1. Feche o Revit. Com ele aberto, a DLL fica travada e o build falha.
2. Confira que está no ramo certo e gere o add-in:

   ```bash
   git -C "C:/Users/thiago felix/projects/revit-plugins" switch noite/correcoes-e-folhas
   ```

   ```bash
   dotnet build "C:/Users/thiago felix/projects/revit-plugins/addin-spk/src/Spk.AddIn" -c Debug.R27
   ```

   O build já publica em `bin\Debug.R27\publish\Spk.AddIn`, que é para onde o manifesto de desenvolvimento aponta. O último build da noite já deixou essa pasta atualizada, então o build só é necessário se algo mudar.
3. Abra o Revit 2027. A aba **SPK** deve ter o painel Projeto com **Dados do projeto**, **Dados do esboço** e **Famílias**.

## 1. Malha de exemplo, do começo ao fim

Use um projeto novo (modelo de arquitetura ou de sistemas, tanto faz).

| Passo | O que fazer | O que deve acontecer |
|---|---|---|
| 1.1 | **Rede de exemplo** | 12 bicos (3 ramais × 4) ligados por tubos, planta e 3D enquadrados. |
| 1.2 | **Calcular** | Como o projeto ainda não tem critérios, abre a janela **Dados do projeto** antes de qualquer coisa. |
| 1.3 | Na janela, escolha Armazenamento e a base ESFR; pressão mínima `283` kPa; item `IT-24 Tabela 5.3.4.3.1`; deixe hidrantes em branco e confirme | Recusa: em armazenamento, hidrantes e duração são obrigatórios. |
| 1.4 | Preencha hidrantes (por exemplo `1900`) e duração (`60`) e confirme | A janela fecha e aparece a prévia "12 bicos entram no cálculo", com o resumo dos critérios e do bico (K 360, área 12 m²). |
| 1.5 | OK na prévia | Resultado com ATENDE ou NÃO ATENDE, pressão na fonte, bico crítico (um número, por exemplo `14`, não mais `E123456`) e o caminho do memorial. |
| 1.6 | Selecione um bico e olhe as Propriedades, grupo Dados | `SPK Nº`, `SPK Setor` = Malha, `SPK Área de cálculo` marcado, `SPK Pressão (kPa)` e `SPK Vazão (L/min)` preenchidos. Num tubo: `SPK Setor`, `SPK Vazão`, `SPK Velocidade (m/s)`. |
| 1.7 | **Memorial** | Os números dos nós no `.docx` são os mesmos de `SPK Nº`. |

### Área que não cabe reprova

Faça num **segundo projeto novo**: Rede de exemplo sempre volta para 3 × 4, e Gerar não apaga a malha anterior.

| Passo | O que fazer | O que deve acontecer |
|---|---|---|
| 1.8 | **Dados do projeto** com os mesmos valores de 1.3–1.4. **Gerar**: na janela, 2 ramais × 4 bicos; depois clique um ponto na planta do nível | Malha de 8 bicos criada. |
| 1.9 | **Calcular** | NÃO ATENDE, com a verificação "Área de operação com o número de chuveiros exigido": o ESFR pede 12 bicos e só há 8. Antes o cálculo seguia com 8 e aprovava. |

Volte ao primeiro projeto para o passo 2.

### K americano

| Passo | O que fazer | O que deve acontecer |
|---|---|---|
| 1.10 | **Dados do esboço**: K `25,2` | A janela recusa: K fica entre 40 e 500 L/min/bar½. |

## 2. Folhas da malha de exemplo

No primeiro projeto, com o cálculo do passo 1.5 feito:

| Passo | O que fazer | O que deve acontecer |
|---|---|---|
| 2.1 | **Folhas** | Um diálogo por conjunto: Setorização, Isométrico geral e Setor calculado, cada um com as folhas criadas e os avisos, e no fim o caminho do PDF. A folha SPK-SETORES abre. |
| 2.2 | Folha **SPK-SETORES** | Uma planta com a hachura diagonal azul do setor "Malha", o nome no meio, e a legenda "SETORIZAÇÃO DAS VGAs" à direita. |
| 2.3 | Folha **SPK-ISO** | 3D da malha em vermelho, só tubulação e bicos, com etiquetas de diâmetro nos tubos mais longos (se o projeto tiver etiqueta de tubo carregada). Quadro à direita com o setor, os critérios e o último cálculo. |
| 2.4 | Folha **SPK-MALHA-01** | Planta com a área de cálculo hachurada em vermelho e o rótulo "ÁREA DE CÁLCULO — 12 chuveiros", número junto de cada bico da área, o bico crítico pintado com "Nº — CRÍTICO / pressão · vazão", etiquetas de diâmetro. 3D à direita com os bicos da área em laranja e o crítico em vermelho. Quadro de resultado embaixo da planta. Sem mapa-chave (só há um setor). |
| 2.5 | Folha **SPK-MALHA-02** | Corte olhando ao longo dos ramais: ramais de topo, subgeral e descida de perfil. |
| 2.6 | Ainda com uma folha SPK aberta, **Folhas** de novo | Refaz tudo sem erro. O plugin sai da vista SPK antes de recriá-la. |
| 2.7 | Abra o PDF | Quatro folhas, na ordem SETORES, ISO, MALHA-01, MALHA-02. |

O que olhar com atenção nas folhas, porque não deu para conferir sem o Revit:

- **Escala.** Cada vista escolhe a menor escala da lista (1:50 a 1:2000) que cabe na sua região da folha. Se ficar pequena ou grande demais, me diga o tamanho do carimbo.
- **Plano de corte da planta.** Fica 0,30 m acima do ponto mais alto da rede, a partir do nível dos bicos. Se a rede não aparecer na planta, o intervalo de vista é o primeiro suspeito.
- **Hachura.** O Revit exige as curvas da região no plano da vista. O plugin tenta a cota do nível e depois zero. Se aparecer o aviso "Hachura da área de cálculo: …", me mande o texto.
- **3D travado.** Para etiquetar no 3D, a vista é travada (`SaveOrientationAndLock`). Se o aviso "Travar o 3D para etiquetar" aparecer, o 3D fica sem etiquetas, mas o resto continua.
- **Corte.** Se o aviso "Corte do setor" aparecer, a folha -02 não é criada. Me mande o texto.

## 3. Modelo real com VGAs (cópia do modelo da Nova Era)

Trabalhe numa cópia do `.rvt`, não no original.

| Passo | O que fazer | O que deve acontecer |
|---|---|---|
| 3.1 | **Famílias** | Além dos bicos, aparecem candidatas a VGA (acessórios com "VGA", "governo" ou "alarme" no nome) sugeridas como **Fonte**. K em gpm/psi½ aparece já convertido, com a nota da conversão. Confirme. |
| 3.2 | **Dados do projeto** | Preencha os critérios reais do setor. |
| 3.3 | **Calcular** | Com mais de uma VGA, o Revit pede para clicar na VGA do setor. Só as VGAs ficam selecionáveis; Esc cancela sem mensagem. |
| 3.4 | Resultado | "Setor: <nome da VGA>" (marca, comentário ou "VGA <id>"). Os bicos de outras VGAs ficam de fora, com aviso. |
| 3.5 | **Folhas** | SPK-SETORES com uma planta por nível e uma cor por VGA (1 azul, 2 verde, 3 magenta, 4 amarelo, 5 laranja). SPK-ISO com o sistema inteiro ligado aos bicos e a tubulação de outros sistemas escondida. SPK-<VGA>-01 com o mapa-chave do nível e o setor calculado em cor cheia. |

Pode demorar. Os setores são lidos um por VGA, com o mesmo leitor do cálculo. Se uma VGA não estiver ligada aos bicos, ela aparece nos avisos da setorização e as outras seguem.

## 4. Se quiser o visual do escritório

Crie no projeto templates de vista com estes nomes. O plugin aplica cada um quando ele existe, e no lugar do estilo padrão:

- `SPK Planta`, para a planta do setor.
- `SPK 3D`, para os dois 3D.
- `SPK Corte`, para o corte.
- `SPK Setorização`, para a setorização e o mapa-chave.

Se o template controlar a escala, o plugin não consegue mudá-la e avisa.

## 5. Padrão do escritório (fase 1)

Traz da Nova Era o carimbo, a etiqueta de tubo, os templates de vista e as legendas, para as folhas saírem no visual do escritório.

| Passo | O que fazer | O que deve acontecer |
|---|---|---|
| 5.1 | Abra o projeto de teste **e** o da Nova Era na mesma sessão do Revit. Volte para o projeto de teste | — |
| 5.2 | **Padrão do escritório** (painel Projeto) | Janela com o carimbo e a etiqueta de tubo mais usados na Nova Era já sugeridos, os templates com "SPK", "sprinkler" ou "-INC" no nome sugeridos por papel, e a lista de legendas. Confira e ajuste. |
| 5.3 | **Importar** | Resumo do que foi copiado. Se uma legenda não vier inteira, o aviso diz o motivo; o mais provável é pedir uma legenda qualquer no projeto de teste para o plugin duplicar (Vista > Legendas > Legenda). |
| 5.4 | **Folhas** | Folhas com o carimbo da Nova Era, diâmetros com a etiqueta do escritório (planta e 3D), vistas com os templates escolhidos e as legendas marcadas à direita, embaixo do mapa-chave (folha do setor) e embaixo das notas (isométrico). |

Rodar **Padrão do escritório** de novo troca as escolhas; legendas já importadas têm o conteúdo atualizado.

## O que ainda não existe

- O número do bico é uma nota de texto, não uma etiqueta. A etiqueta de tubo é a padrão do projeto, que mostra só o diâmetro, sem o comprimento.
- O contorno do setor na setorização é a união das coberturas dos bicos e não é recortado pelas paredes.
- Ficaram de fora a geral até a casa de bombas, as tabelas de bomba, as cotas encadeadas, a tabela de sprinklers, a rede sob telhado inclinado (010), os cortes do prédio e a família da VGA (012) e o layout por ambiente (011).
- O setor é gravado como parâmetro. Ainda não existe um sistema de tubulação do Revit por VGA.
