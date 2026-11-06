# Preparar o modelo

O AutoEletrica trabalha num projeto elétrico com a arquitetura vinculada. Antes de lançar, confira os itens abaixo. Quase todo problema de "o plugin não fez nada" vem de um deles.

## Arquitetura vinculada com ambientes

- Vincule a arquitetura (Inserir, Vincular Revit) e deixe o vínculo **carregado**.
- A arquitetura precisa ter **ambientes (Rooms)** com nome, nos níveis certos. É deles que vêm a área, o perímetro, o uso e o contorno onde as tomadas são distribuídas.
- Para as regras do banheiro, o plugin procura na arquitetura o **boxe, a banheira ou o chuveiro** pelo nome da família. Sem essas peças, os volumes não são apurados e o Verificar avisa (código A-09).
- Crie no projeto elétrico os mesmos níveis da arquitetura. Pontos e eletrodutos ficam nos níveis do projeto elétrico.

## Sistema de distribuição

O Revit só liga circuitos a um quadro com **sistema de distribuição** (Gerenciar, Configurações MEP, Configurações elétricas). Crie o sistema que bate com os Dados do projeto, por exemplo 127/220 V trifásico em estrela com neutro. O botão Quadro escolhe o sistema pelas tensões e fases dos Dados; se não encontrar, avisa. O Verificar acusa quadro com sistema diferente dos Dados (código R-01).

## Famílias elétricas

| O que a família precisa | Por quê |
|---|---|
| **Conector elétrico** com tensão e número de polos | Sem conector o Revit não aceita o elemento num circuito (código R-02). Chuveiro e ar em 127/220 V precisam de conector de 2 polos em 220 V. |
| Hospedagem **de face** ou **livre** | Família hospedada em parede não encontra a parede do vínculo. |
| **Carga aparente** como parâmetro de instância | O plugin grava a potência nela. Se for de tipo, a potência fica só em AE Potência (VA), que é o que o cálculo lê. |
| Parâmetro de **grau de proteção** (nome com "IP") | O Verificar compara o IP com o volume do banheiro (códigos B-04 e B-05). |
| Famílias de **etiqueta** com rótulos nos parâmetros AE | Para o Etiquetar mostrar circuitos, condutores e seções. |

Depois de carregar as famílias, abra **Famílias** e confirme o papel de cada tipo. O papel fica gravado pelo nome "Família : Tipo", então recarregar a família não perde a escolha.

## Parâmetros AE

Rode **Parâmetros** uma vez por projeto, antes de lançar. Os comandos que gravam dados criam o que faltar, mas as etiquetas e tabelas do escritório só enxergam os parâmetros depois que eles existem no projeto.

## Rede já desenhada

Se os pontos e circuitos já foram desenhados à mão, o plugin lê o que está no modelo: quadros, circuitos do Revit, pontos, eletrodutos e comandos. Confirme os Dados, os Ambientes e as Famílias, trace os eletrodutos que faltam, rode **Passar circuitos** e depois **Verificar modelo**. Os pontos sem circuito aparecem com o código M-01.

## Um projeto limpo para testar

Para ver o fluxo inteiro sem um projeto real: crie um projeto pelo template elétrico do Revit, vincule uma arquitetura com ambientes, carregue uma tomada, uma luminária, um interruptor e um quadro com conector, e siga o Passo a passo. O plugin também tem um teste automático que monta um apartamento do zero; ele é usado no desenvolvimento.
