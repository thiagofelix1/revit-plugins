# O que ainda não faz

Esta versão é um protótipo em validação com os engenheiros. O que ainda não está pronto:

- **Gerar o projeto inteiro** a partir da arquitetura com um clique. Hoje o fluxo é assistido, etapa por etapa.
- **Vários pontos na mesma caixa** e alinhamento vertical de pontos.
- **Desvio de obstáculos** no traçado de eletrodutos e caixa automática no fim de cada subida; eletrocalhas; prumada modelada entre pavimentos.
- **Anti-sobreposição** de etiquetas e um conjunto de famílias de etiqueta de referência.
- **Legenda de símbolos**: a API do Revit não cria vista de legenda.
- **Carimbo e numeração do escritório** nas pranchas: hoje sai o primeiro carimbo do projeto.
- **Multifilar**, checklist de as built e detalhe de entrada da distribuidora.
- **Fatores de demanda da distribuidora**: hoje são os padrões do plugin.
- **Seletividade** entre disjuntores, **motores** (partida e relé) e neutro reduzido automático.
- No Verificar: **piscina e sauna**, neutro compartilhado e interruptor no neutro (não dá para ler do modelo), rota de fuga; os volumes do banheiro não descontam paredes.

## Para relatar um problema

Anote o passo, tire um print da mensagem e, se possível, envie o relatório do Verificar (botão Salvar relatório) e o memorial gerado. O Revit também registra cada mensagem no diário (journal), na pasta %LOCALAPPDATA%\\Autodesk\\Revit, que pode ser lido depois.
