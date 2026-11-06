# SPK — add-in do Revit

Add-in fino sobre o [motor](../motor-spk): lê e desenha a rede de sprinklers no Revit 2025 a 2027 e chama o motor para dimensionar e calcular. O mapa do código, com os botões e o que cada etapa confere, está em [docs/refinamentos/spk/mapa-do-codigo.md](../docs/refinamentos/spk/mapa-do-codigo.md).

## Compilar

```bash
dotnet build "addin-spk/src/Spk.AddIn" -c Debug.R27
```

Troque `R27` por `R26` ou `R25` para as outras versões. O Revit precisa estar fechado, porque ele trava a DLL, e um build que falha apaga o plugin da pasta `publish`. Depois de compilar, a janela "Complemento não certificado" volta ao abrir o Revit: clique em "Sempre carregar".

## Ícones

Os ícones (`src/Spk.AddIn/Icones.cs`) são desenhados em código num quadro de 32 × 32, um por botão, sem repetir. O estilo é o mesmo do AutoEletrica:

| Cor | Uso |
|---|---|
| Grafite | Contorno e tubos (traço largo com miolo claro) |
| Vermelho | O que o botão faz |
| Cinza e cinza-claro | Apoio |
| Azul | Água |
| Cores da legenda | Só no Cores da rede |

Todos precisam ficar legíveis também em 16 px.

Em 06/11/2026:

- **Gerador corrigido.** O `Gerar` escalava o desenho por pixels/32 num bitmap a 192 dpi, então o desenho saía com o dobro do tamanho, e só o quarto de cima à esquerda aparecia no botão. A escala passou a ser tamanho/32, e a margem caiu de 2 para 1.
- **Ícones redesenhados.** São 30, e antes 11 desenhos eram divididos entre vários botões.

| Painel | Ícones |
|---|---|
| Projeto | Padrão do escritório, Dados do projeto, Famílias, Arquitetura |
| Rede | Gerar rede, Bicos por ambiente, Traçar ramais, Ligar bicos, Ligar à geral, Inserir bico, Apagar bico, Mover bico, Ligar tubos, Conferir na planta, Limpar conferência, Malha retangular, Dados do esboço, Rede de exemplo |
| Cálculo | Marcar setor, Verificar modelo, Diâmetros, Calcular |
| Resultados | Memorial, Folhas, Cores da rede, Tirar cores, Lista de material |
| Ajuda | Guia, Avançado, Exemplo |

O que diferencia os ícones parecidos:

- **Inserir, Apagar e Mover bico:** o mesmo bico, com "+", com "x" ou com a seta.
- **Calcular:** um manômetro.
- **Cores da rede:** uma paleta, e Tirar cores é a paleta apagada.
- **Guia:** a interrogação no círculo.

Para conferir sem abrir o Revit, um console `net10.0-windows` com `UseWPF` inclui o `Icones.cs` por `<Compile Include=... Link=...>`. Ele chama o `Gerar` por reflexão em cada método `public static void X(DrawingContext)` e salva uma folha PNG com cada ícone em 32 e em 16 px.
