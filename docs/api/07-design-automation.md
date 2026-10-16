# Revit na nuvem: APS Automation API (Design Automation v3)

Resumo da documentação oficial em https://aps.autodesk.com/en/docs/design-automation/v3/ (lida pelo HTML estático em 16/10/2026). Índice completo em [indices/design-automation-v3.md](indices/design-automation-v3.md).

## O que é

Um Revit **sem interface** rodando na nuvem da Autodesk, que executa o seu add-in sobre um arquivo `.rvt`, `.rte` ou `.rfa`. Dá acesso completo à API de banco de dados (`RevitAPI.dll`) sem Revit instalado na máquina do cliente. Casos de uso citados pela Autodesk: criar famílias, automatizar modelagem, analisar dados do modelo, gerar relatórios, impor padrões e gerar documentação.

Para este projeto, faz sentido em:

- Validação de entregáveis em lote (caderno BIM, IFC), recebendo o arquivo pela web.
- Extração de quantitativos para orçamento sem depender do Revit do orçamentista.
- Verificação de normas como serviço (o cliente sobe o `.rvt`, recebe o relatório).

Não serve para interação em tempo real: cada execução é um job em fila, que baixa o arquivo, processa e devolve.

## Conceitos

| Termo | O que é |
|---|---|
| **AppBundle** | Pacote com o seu add-in (DLL, `.addin`, dependências) |
| **Activity** | Definição de uma ação: qual engine, quais AppBundles, quais entradas e saídas. Funciona como a declaração de uma função |
| **WorkItem** | Uma execução de uma Activity com arquivos concretos. É a chamada da função; não pode ser alterado depois de criado |
| **Engine** | Versão do Revit que executa: `Autodesk.Revit.2025`, `Autodesk.Revit.2026`, `Autodesk.Revit.2027` etc. |
| **Nickname / Alias** | Nome curto da sua app e apelidos de versão (por exemplo, `prod`) para AppBundles e Activities |

Fluxo: subir o AppBundle (`POST appbundles`), publicar a Activity (`POST activities`), enviar o arquivo de entrada para um armazenamento (OSS da APS ou qualquer URL assinada), criar o WorkItem (`POST workitems`) com as URLs de entrada e saída, aguardar (de preferência por callback `onComplete`) e baixar o resultado.

## Converter um add-in para rodar lá

Do tutorial [Execute a Revit Add-in](https://aps.autodesk.com/en/docs/design-automation/v3/tutorials/revit/about_this_tutorial/), tarefa 1:

1. Remover a referência a `RevitAPIUI.dll` e a qualquer UI.
2. Adicionar o pacote NuGet `Autodesk.Forge.DesignAutomation.Revit` (o "Automation Service Bridge"; há versões de 2023 a 2027).
3. Trocar o ponto de entrada para `IExternalDBApplication` e o tipo no `.addin` para `DBApplication`.
4. No `OnStartup`, assinar `DesignAutomationBridge.DesignAutomationReadyEvent`. O handler é o ponto de entrada: recebe `DesignAutomationData` com `RevitApp`, `RevitDoc` e `FilePath`, e precisa marcar `e.Succeeded = true`.
5. Ler e gravar arquivos **só na pasta de trabalho atual** (e subpastas).

```csharp
public class VerificadorApp : IExternalDBApplication
{
    public ExternalDBApplicationResult OnStartup(ControlledApplication app)
    {
        DesignAutomationBridge.DesignAutomationReadyEvent += AoFicarPronto;
        return ExternalDBApplicationResult.Succeeded;
    }

    public ExternalDBApplicationResult OnShutdown(ControlledApplication app)
        => ExternalDBApplicationResult.Succeeded;

    private void AoFicarPronto(object sender, DesignAutomationReadyEventArgs e)
    {
        var doc = e.DesignAutomationData.RevitDoc;
        var relatorio = Verificador.Executar(doc);   // mesma biblioteca usada no add-in desktop
        File.WriteAllText("relatorio.json", relatorio.ToJson());
        e.Succeeded = true;
    }
}
```

Separar desde o início a lógica (biblioteca só com `RevitAPI.dll`) da UI (add-in desktop) permite que o mesmo código rode no desktop e na nuvem.

## Restrições para Revit

Da página [Restrictions](https://aps.autodesk.com/en/docs/design-automation/v3/developers_guide/restrictions/):

- Sem UI: o add-in tem que ser só de banco de dados (`DBApplication`).
- Escrita em disco só na pasta de trabalho do job.
- Não existe `ActiveView` nem `ActiveDocument`.
- Exportar para Navisworks não está disponível; Autodesk Desktop Connector não é suportado.
- O job roda com privilégios baixos: registro do Windows praticamente inacessível, subprocessos também limitados.
- Caminhos com mais de 260 caracteres quebram (o pacote é descompactado na máquina do serviço).
- **Sem acesso à rede**: o tutorial diz explicitamente que a aplicação não pode usar a rede. A exceção é o mecanismo `onDemand` da própria API, que busca entradas extras durante o WorkItem ([Use OnDemand Input](https://aps.autodesk.com/en/docs/design-automation/v3/tutorials/using-on-demand-inputs/)).

Consequência para a arquitetura com IA: **a LLM não pode ser chamada de dentro do job**. O backend chama a LLM antes (e passa o resultado como arquivo de entrada do WorkItem, por exemplo um JSON de mapeamento para SINAPI) ou depois (lendo o JSON extraído pelo job).

Callbacks atrás de firewall: liberar os IPs listados na página de restrições para Revit (3.229.167.149, 44.207.230.78, 54.175.193.194).

## Falhas e avisos

Sem UI, avisos e erros do Revit não podem ser respondidos por ninguém. O Bridge vem com um tratador padrão que suprime avisos, tenta resolver erros e, se a resolução padrão for apagar elementos, desfaz a transação em vez de apagar. Dá para substituir com `IFailuresProcessor` (`Application.RegisterFailuresProcessor()`) ou o evento `FailuresProcessing`. Nunca retorne `WaitForUserInput`. Ver [Handling Revit Failures](https://aps.autodesk.com/en/docs/design-automation/v3/developers_guide/revit/handling-failures/).

## Modelos na nuvem (ACC / Autodesk Docs)

O serviço abre e altera modelos na nuvem, compartilhados ou não, se o WorkItem receber um token OAuth de 3 pernas do usuário no argumento `adsk3LeggedToken` (escopo `code:all`). Sem ele, `OpenDocumentFile()` falha em modelos na nuvem. Se o usuário já tiver elementos em posse no modelo, o job para por padrão. A página [Revit Cloud Model Integration](https://aps.autodesk.com/en/docs/design-automation/v3/developers_guide/revit/revit-cloud-model-integration/) lista suporte para Revit 2022, 2023 e 2024; confirme o suporte para 2025+ antes de depender disso.

## Limites e cotas

Da página [Automation API Rate Limits](https://aps.autodesk.com/en/docs/design-automation/v3/developers_guide/rate-limits/da-rate-limits/):

| Limite (Revit) | Valor |
|---|---|
| Tempo padrão máximo de um WorkItem (inclui download e upload) | 10.800 s (3 h) |
| Tempo máximo configurável (`limitProcessingTimeSec`) | 43.200 s (12 h) |
| Tamanho descompactado do AppBundle | 5.000 MB |
| Upload de AppBundle | 100 MB |
| JSON de WorkItem, Activity ou AppBundle | 64 KB |
| Versões e aliases de AppBundles e de Activities | 100 de cada |
| Consulta de status (`GET workitems/:id`) | 150 por minuto por app (acima disso, HTTP 429) |
| Sem saída por mais de 1 minuto | O job é considerado travado: escreva no console periodicamente como "batimento" |

Memória das máquinas que rodam Revit: 32 GB desde setembro de 2021 (notas de versão do serviço).

## Ciclo de vida das engines

Suporte de 4 anos após o lançamento do desktop, mais 2 anos como obsoleta, depois remoção. Datas por versão em [02-versoes.md](02-versoes.md). O serviço adicionou o Revit 2027 em abril de 2026.

## Custo

O serviço é cobrado por consumo de processamento. Os preços estão em https://www.autodesk.com/products/autodesk-platform-services/overview#pricing (a página bloqueia acesso automatizado; consultar no navegador). A API permite fixar cotas menores que as padrão (`servicelimits`) para controlar gasto.

## Exemplos oficiais

- https://github.com/autodesk-platform-services/aps-count-delete-walls: o exemplo recomendado para começar (contar e apagar paredes).
- https://github.com/autodesk-platform-services/aps-checkmodels-createissues-revit: verificar modelos e criar issues no ACC. É o mais próximo do produto de validação.
- https://github.com/autodesk-platform-services/aps-revit-file-parameters-exchange: ler e escrever parâmetros.
- https://github.com/autodesk-platform-services/aps-design-automation-tools: ferramentas de apoio.
- A página de tarefa 1 do tutorial também cita uma ferramenta de depuração local no GitHub para testar o add-in antes de subir.
