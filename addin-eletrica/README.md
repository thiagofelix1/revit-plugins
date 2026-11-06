# AutoEletrica — add-in do Revit

Add-in fino sobre o [motor](../motor-eletrica/README.md): lê o modelo (quadros, circuitos, pontos, ambientes, eletrodutos), passa os circuitos pelos eletrodutos, verifica pela NBR 5410, calcula e grava o resultado em parâmetros compartilhados "AE" e num memorial. Mesma estrutura e as mesmas peças de infraestrutura do [SPK](../addin-spk/README.md) (Nice3point SDK, Revit 2025 a 2027, armazenamento no .rvt, transação segura, ícones desenhados).

Estado em 06/11/2026: **compila para 2025, 2026 e 2027; ainda não foi aberto no Revit** (ver "Testar").

## A aba AutoEletrica

| Painel | Botão | O que faz |
|---|---|---|
| Projeto | Dados do projeto | Tensões, fases, esquema de aterramento, Icc presumida, linha aérea, dias de trovoada, SPDA, temperatura, habitação. Gravado no .rvt. |
| Projeto | Ambientes | Lista os Rooms do arquivo e dos vínculos com o uso sugerido pelo nome; o engenheiro confirma. O uso define pontos mínimos, potências e DR. |
| Projeto | Parâmetros | Cria os parâmetros compartilhados AE (ver abaixo). |
| Fiação | Passar circuitos | Monta o grafo dos eletrodutos (conectores), leva cada circuito do quadro aos pontos pelo menor caminho e grava em cada eletroduto `AE Circuitos`, `AE Condutores` e `AE Fiação`. |
| Fiação | Etiquetar | Cria etiquetas de eletroduto, tomada e luminária na planta aberta com as famílias de etiqueta carregadas (os rótulos devem apontar para os parâmetros AE). |
| Cálculo | Verificar modelo | Roda o Verificar do motor e mostra a lista com código, gravidade e item da norma; "Mostrar" seleciona os elementos no modelo. |
| Cálculo | Calcular | Dimensiona tudo, grava `AE Seção`, `AE Disjuntor`, `AE IB`, `AE Queda`, `AE DR`, `AE Situação`, `AE Comprimento` nos circuitos, `AE Fiação`, `AE DN sugerido`, `AE Ocupação` nos eletrodutos, `AE Ambiente` nos pontos, e salva `Documentos\AutoEletrica\calculo\<projeto>-memorial.md`. |
| Resultados | Memorial | Abre o último memorial. |
| Ajuda | Guia | Passo a passo e a situação do projeto aberto. |

## Como o modelo é lido (`Leitura/LeitorInstalacao.cs`)

- **Quadros**: todo `ElectricalEquipment` que alimenta circuitos ou é alimentado por um; fases pelo sistema de distribuição; o pai é o quadro do circuito que o alimenta.
- **Circuitos**: `ElectricalSystem` de energia; ligação pelos polos, tensão e neutro; tipo de carga por `AE Tipo de carga` ou pelos pontos; comprimento do Revit como reserva quando não há eletrodutos.
- **Pontos**: os elementos de cada circuito (tomadas, luminárias, equipamentos); potência por `AE Potência (VA)`, pela carga aparente da família ou rateio da carga do circuito; ambiente pelo Room no ponto (arquivo ou vínculo); altura pelo nível; tipo pelo nome da família (chuveiro, ar-condicionado, TUE...). Interruptores não são pontos de utilização.
- **Trechos**: cadeias de eletrodutos ligados por curvas e luvas, entre caixas, tês, pontas e pontos; DN pelo diâmetro; tipo pelo nome do tipo de eletroduto; os circuitos vêm de `AE Circuitos`.

## Testar

1. `dotnet build -c Debug.R27` em `src/Eletrica.AddIn` (publica em `bin\Debug.R27\publish\Eletrica.AddIn`).
2. O manifesto de desenvolvimento em `%AppData%\Autodesk\Revit\Addins\2027\Eletrica.AddIn.addin` aponta para essa pasta (já criado nesta máquina).
3. Abrir o Revit 2027 num projeto elétrico com quadro, circuitos e eletrodutos; seguir o Guia. Modelos de teste: os do SPK (Snowdon, MNEVVM) só para a arquitetura; falta um .rvt elétrico de referência dos engenheiros.
4. Distribuição: `distribuicao\instalar.bat` copia a pasta `Revit 20xx` para a pasta de add-ins do usuário, como no SPK.

## O que falta (plano 0.2 a 0.6)

Inserir pontos e criar circuitos de dentro do plugin, traçar eletrodutos, caixas por regra, fio desenhado (`Wire`), legenda, quadro de cargas na prancha, memorial .docx, lista de material, teste automático no Revit, e a base comum extraída do SPK. Ver `autoeletrica/docs/plano-de-desenvolvimento.md`.
