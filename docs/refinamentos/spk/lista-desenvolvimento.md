# Lista de desenvolvimento — SPK até a distribuição

Uma linha por entrega, na ordem combinada. `[x]` só quando provado no Revit (à mão ou pela [suíte automática](teste-automatico.md)); o que só compila fica `[ ]`. A ordem de cada etapa só começa com o critério de pronto da anterior, salvo o que não depende do engenheiro parceiro.

Atualizada em 26/10/2026. Como o engenheiro usa o plugin de ponta a ponta: [fluxos.md](fluxos.md).

## 0. Base (feito)

- [x] Motor hidráulico: Hazen-Williams, pressão total, comprimento equivalente, árvore e anel (gradiente global), busca da área mais desfavorável, ESFR de 12 bicos. Conferido com o memorial Sprinkler-CALC da Nova Era (0,01 bar nó a nó).
- [x] Add-in para Revit 2025, 2026 e 2027 (aba SPK): Dados do projeto, Famílias, Dados do esboço, Gerar, Rede de exemplo (5 cenários), Calcular, Memorial (.docx), Folhas (PDF), Guia.
- [x] Padrão do escritório importado de outro projeto: carimbo, etiqueta de tubo, templates, legendas.
- [x] Folhas: setorização, isométrico geral, planta + 3D do setor com área de cálculo, corte.
- [x] Teste automático no Revit com suíte de regressão e conferência automática (`Passou`).
- [x] Pacote de teste (`dist/`, zip com instalar.bat) para o engenheiro parceiro.

## 1. Verificar modelo e leitura estrita

Pronto quando um projeto real bem conectado passa sem erro e um projeto mal conectado mostra cada problema no lugar certo.

- [x] Verificar modelo: peças encostadas sem ligação, tubo terminando no meio de outro sem tê, bico sem tubo, família de bico sem conector, ponta aberta, tubo fora de sistema de incêndio, bico sem K e área confirmados, bico que não chega a VGA, redes separadas. Clique leva ao elemento.
- [x] Provado por sabotagem (bico desligado, tê apagado, sistema de água fria, tubo desligado) e na cópia da Nova Era (971 encostados, 169 derivações sem tê, 610 tubos em sistema hidrônico, 172 redes separadas, em menos de 1 s).
- [x] Leitura estrita: Calcular mostra os erros antes e só liga por proximidade se o engenheiro pedir.
- [ ] Lista de problemas comuns nos modelos que o escritório recebe (**depende do engenheiro**) e checagens que faltarem.
- [ ] Um projeto real **bem conectado** calculando sem proximidade (**depende do engenheiro**: ceder um projeto).
- [ ] Tubos duplicados ou sobrepostos; tubo em nível ou fase errada.
- [x] Correção assistida das peças encostadas (ver etapa 6, item 1).

## 2. Folhas completas

Pronto quando o engenheiro aceita entregar as folhas sem retocar à mão.

- [x] Carimbo: número na sequência do escritório, código da prancha por molde (sugerido pelas folhas do projeto), campos do projeto copiados das outras folhas do mesmo projeto. Número já usado por outra folha é recusado sem apagar nada. Provado na cópia da Nova Era (folhas 17 a 20, código NEPN-INC-018-LG-SPK-ISO-R00, CNPJ, lote e CREA preenchidos).
- [x] Quadro de chuveiros (tipo, K, área, quantidade) e quadro de demanda na entrada do setor (vazões, pressão em kPa e mca, duração, reserva), no isométrico.
- [x] Isométrico no padrão da NEPN-INC-013: nível Fino, vermelho, diâmetro só nos trechos principais.
- [x] Hachura da área de cálculo no formato real da área; corte e 3D só com o setor.
- [ ] Revisões no carimbo (criar ou atribuir revisão às folhas do SPK) — decidir com o engenheiro se o SPK deve mexer nisso.
- [ ] Cotas entre ramais, bicos e paredes.
- [ ] Etiqueta "Ø + comprimento" e número do bico como etiqueta (família do kit do escritório), no lugar de texto.
- [ ] Área livre do carimbo configurável por escritório (o mapa-chave pode cair sobre carimbos grandes).
- [ ] Folhas com arquitetura e estrutura vinculadas (visto no Snowdon): os eixos dos dois vínculos aparecem duplicados; móveis, vegetação e textos da arquitetura poluem a setorização (vínculo em meio-tom, sem essas categorias); a legenda da setorização cai em cima da primeira planta; a planta do setor pode passar da borda da folha.
- [ ] Gerar rede em área externa (terraço, jardim sem cobertura): hoje gera; deveria pular ou perguntar.
- [ ] Quadro de bombas com a bomba escolhida (hoje: a demanda que ela precisa atender).
- [ ] Conferência das folhas pelo engenheiro (**depende do engenheiro**).

## 3. Validação com projetos reais

Pronto quando houver pelo menos três projetos reais conferidos pelo engenheiro como cenários da suíte.

- [ ] Cada projeto real cedido vira cenário em `testes/regressao.json`, com o resultado conferido pelo engenheiro como esperado.
- [ ] Comparar memorial do SPK com o memorial que o escritório entregou para a mesma obra (pressão, vazão, área, reserva).
- [ ] Tempo de cálculo em rede grande (hoje ~8 min em 700 bicos lidos por proximidade): medir num modelo bem conectado.

## 4. Pronto para distribuir

- [ ] Certificado de assinatura de código (some o "Sempre carregar" e os alertas do Windows). Compra e instalação ficam com o dono do produto.
- [ ] Instalador (MSI ou equivalente) para 2025–2027, com desinstalação.
- [ ] Núcleo comum para os próximos plugins (licença, padrão do escritório, folhas, teste automático).
- [ ] Desligar no pacote o que é de desenvolvimento (menu Avançado, teste automático).

## 5. Licença e piloto pago

- [ ] Licença por assinatura (ativação, validade, uso offline).
- [ ] Primeiro escritório pagando, com canal de suporte.

## 6. Modelagem assistida e geração automática

Os dois fluxos de uso (projeto regular e projeto complexo como a Nova Era) estão em [fluxos.md](fluxos.md). As ferramentas vêm primeiro, uma por vez, cada uma útil sozinha em projeto real; a geração automática é montada em cima delas. Esta etapa pode começar antes da 4 e da 5: os itens 1 a 4 não dependem do engenheiro para existir, só para serem validados.

Pronto quando um setor da Nova Era (ou de um projeto do engenheiro) é desenhado com as ferramentas, passa no Verificar modelo sem erro e calcula sem proximidade.

- [x] 1. Reconectar peças encostadas: botão Corrigir em Verificar modelo, um Ctrl+Z desfaz tudo. Não move tubo; cada ligação é conferida (nenhuma ligação vizinha desfeita, nenhuma peça presa movida). Nova Era: 910 de 971 ligadas em 55 s, ligações do modelo 4.600 → 5.512.
- [ ] 1b. Derivação sem tê: criar o tê partindo o tubo (983 na Nova Era depois da reconexão).
- [x] 2. Ligar bicos selecionados ao ramal: parte o ramal, tê, descida, ou braço + cotovelo + descida; bico fora do ramal ou acima dele é recusado com motivo; um Ctrl+Z. Provado na cena de teste (5 ligados, 1 recusado, cálculo com os 5 num ramal só). Braço e descida saem com o DN do ramal (o ajuste é o item 4); bico em pé ainda não.
- [x] Cálculo: bico em braço perpendicular conta no ramal de onde sai (antes virava um ramal à parte e a área de cálculo saía picotada).
- [x] 3. Traçar o ramal: reta pelos bicos (eixo principal, passando pela mediana lateral), na altura da subgeral, do tê nela até o último bico, e os bicos ligados pelo Ligar bicos (último por cotovelo na ponta). Recusa fileira dos dois lados da subgeral (cruzeta) e fileira quase paralela a ela. Provado na cena de teste (5 bicos, 1 de lado; cálculo num ramal só).
- [x] 3b. Traçar ramais de várias fileiras de uma vez (como o Auto Branch Line do AutoSPRINK): fileiras separadas pelo SPK, ramal na altura da subgeral (tê ou cruzeta), com subida (riser nipple) ou a uma altura fixa; bico em pé.
- [x] 4. Aplicar diâmetros por tabela (número de bicos atendidos) e conferir pelo cálculo. Botão Diâmetros, no painel Cálculo; tabelas da IT-23/2025 (12 a 14).
- [x] 5. Ler a arquitetura e a estrutura vinculadas: ambientes (contorno, piso, teto pelo primeiro forro, laje ou telhado acima), vigas (largura, fundo, topo). Botão Arquitetura, painel Projeto. Testado com vínculos gerados pelo teste; falta conferir nos vínculos reais da Nova Era (NEPN-ARQ R03 e NEPN-PRE R08), pedidos ao engenheiro.
- [x] 6. Posicionar bicos num ambiente ou contorno pela regra da NBR, junto com a conferência de espaçamento, área por bico e distância à parede. Botão Bicos por ambiente (painel Rede): Tabela 2 da IT-23/2025 (bico spray de cobertura-padrão), sala em L cortada em retângulos, defletor abaixo do forro/laje ou abaixo da viga (até 560 mm do teto); conferência dos bicos existentes. Falta: um bico por vão entre vigas altas, obstruções laterais (distância do bico à viga), cobertura estendida, laterais, CCAE e ESFR.
- [x] 7. Geração automática por contorno (contorno → bicos → ramais → subgeral → VGA), usando 2 a 6. Botão Gerar rede (painel Rede): por ambiente, bicos pela norma, subgeral com a descida na parede de entrada, ramais e ligação, diâmetros da tabela; espinha de peixe e lateral. Verificação limpa e cálculo direto. Falta: grelha; ambiente em L (a subgeral pode sair dele, vira aviso); vários ambientes num setor só (hoje cada ambiente tem a sua entrada); a malha retangular (Malha retangular) usar as mesmas peças, o que muda os valores dos cenários de exemplo; rede sob vigas altas.
- [ ] **Um bico por vão entre vigas** (o que mais falta): no Snowdon (exemplo da Autodesk, arquitetura e estrutura vinculadas), 42 dos 67 ambientes foram recusados por viga (23 deles pela viga de 52 cm, que põe o defletor a 573 mm da laje; máximo 560 mm). Em prédio de concreto brasileiro, viga é a regra.
- [ ] Ambiente com mais de um teto (parte sob mezanino, parte em pé-direito duplo): hoje vale o teto no ponto do ambiente, e uma viga de outro nível aparece como "viga de 4 m".
- [ ] Ambiente que não é retângulo: o posicionamento pode sair fora da Tabela 2 (o Snowdon mostrou S de 6 m no 200 e no R100; a conferência avisa); a subgeral pode sair do ambiente; fileira colada à parede da entrada recusa o ambiente.
- [ ] Aviso de colisão com viga e duto, e de inclinação.
- [ ] Rede seguindo telhado inclinado.
- [ ] Setorização: área de cada setor contra a Tabela 8; depois, sugestão de cortes.
- [ ] Tubo geral até a casa de bombas.
- [ ] Complementos baratos, a qualquer momento: pintar a rede por pressão ou velocidade; lista de material (metros de tubo por diâmetro, conexões).

## 7. Dimensionamento

- [ ] Subir diâmetros até atender, com a ordem de DN que o projetista usar.
- [ ] Visão de todos os setores juntos e do setor que dimensiona a bomba.
- [ ] Sugerir a bomba pela curva de abastecimento.

## Conhecido e aceito por enquanto

- O campo ESCALA do carimbo do isométrico mostra 1:1: o Revit monta esse campo sem a vista 3D. A prancha 013 da Nova Era sai igual.
- O Projeto2 tem um bico solto (333229), que a verificação aponta com razão.
