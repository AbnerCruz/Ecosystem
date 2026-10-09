# P6-5 R4 — Primeira superfície executável do Ecosystem AI (CLI)

Este incremento entrega uma **interface local de linha de comando**, sem duplicar o loop de agentes.
É um *slice de Product*, não a UX final de chat/projetos/equipes mobile e não fecha P6-5.

## Uso real

Requer .NET SDK 10 e um endpoint compatível com Chat Completions (`/chat/completions`)
que aceite `tools` no formato `function`, informando custos via `usage.cost`
ou devolvendo contadores de tokens para estimativa com tarifas informadas pelo operador.

```bash
cd apps/ecosystem-ai
export ECOAI_API_KEY='CHAVE_DIGITADA_PELO_USUARIO'
dotnet run --project src/EcosystemAi.Cli -- \
  --project ./meu-projeto --goal "Resuma README.md usando files.read" \
  --endpoint https://SEU-ENDPOINT/api/v1/chat/completions \
  --model ID_DO_MODELO --budget-cents 30 --max-call-cents 5 \
  --input-usd-per-million 1 --output-usd-per-million 3
```

Os valores das tarifas são **exemplos**, não preços correntes; devem corresponder
ao modelo configurado. Valores se referem a dólares por 1 milhão de tokens.
O ledger contabiliza em centavos de USD, arredondando valores positivos para
cima. Isto é **aproximação**, não garantia contratual de cobrança pelo provedor.
`--budget-cents` limita somente a execução atual da CLI, não a conta externa;
uma chamada já enviada pode exceder sua reserva e o Runtime irá bloquear.
Orçamento e logs são efêmeros; **não** são histórico ou controle de conta persistente.

**Privacidade:** ao usar `files.read` com um endpoint externo, o conteúdo lido é enviado a esse provedor. Não escolha uma pasta contendo segredos ou dados sensíveis que não possam ser compartilhados. O isolamento por diretório protege outros caminhos, mas não oculta arquivos legítimos dentro do projeto autorizado.

Por padrão o agente só vê `files.read`. A flag `--allow-create` adiciona
`files.write`, que cria arquivos novos **sem sobrescrever**. Exclusão e execução
de comandos ficam indisponíveis. Com `--accept-exists arquivo.txt`, um arquivo
criado só é considerado entregue se o verificador o encontrar realmente no disco;
sem esse parâmetro, o aceite verifica apenas que houve resposta textual, nunca sua
correção factual. Os arquivos estão confinados à raiz do projeto com o sandbox
já usado por `AgentRuntime.Tools.Files`.

Chaves vêm de `ECOAI_API_KEY` somente em memória, não de flags/arquivo.
O token não integra o prompt; logs não copiam payloads HTTP de falha.
HTTP não-criptografado é aceito apenas em loopback para modelos locais.
Redirecionamentos estão desativados no HttpClient da CLI.

IDs internos de capabilities, como `files.read`, são traduzidos para nomes de função seguros na API (prefixo + hash estável) e revertidos na resposta; o Registry e o Runtime continuam vendo exclusivamente o ID canônico. Isso evita rejeição de nomes com ponto em endpoints Chat Completions.

O Product instancia `AgentWorkspace.WorkspaceSession` e
`AgentRuntime.AgentRunner`, com `ToolHost`, `InMemoryLedger`,
`ContextPath` e `IEventLog` existentes. Apenas a porta de rede vive em
`AgentRuntime.Providers.ChatCompletions`; o Core continua provider-agnostic
e sem rede. A CLI não tem servidor, persistência, modelo embutido ou tarefa 24/7.

## Testes

`dotnet test --project tests/AgentRuntime.Tests` compila a CLI e executa testes
do adapter com HTTP falso, sem acessar serviços nem exigir chave.
Os testes verificam tradução de ferramentas, ordem das mensagens, orçamento/custo,
ausência de credenciais no corpo, erros e bloqueio de endpoints HTTP remotos.

## Faltam para encerrar P6-5

Interface mobile de verdade, histórico persistente de sessões/projetos, equipes,
tarefas e memória, artefatos navegáveis, configuração de provedores, preços e
permissions de UX, secret store da plataforma, registro de validação de build e
fluxos completos avaliados com usuário. Nenhum destes itens é declarado concluído.

## Histórico local opcional (slice R4 de projetos/sessões)

A CLI continua efêmera por padrão. Para salvar perguntas, respostas redigidas
e recibos de runs em uma pasta controlada pelo usuário, acrescentar:

```text
--catalog /pasta/historico --project-name "Meu projeto" --session-title "Sessão 1"
```

A saída fornece `projectId` e `sessionId`. Para reutilizar uma sessão em outra
execução, passar `--catalog`, `--project-id` e `--session-id`.
O vínculo do projeto com o workspace original é verificado ao reabrir.
O catálogo nunca contém o valor de `ECOAI_API_KEY` por gravação automática,
mas mensagens selecionadas para persistência são texto claro; o redator
do Runtime é aplicado ao conteúdo com segredos conhecidos.

O histórico pode ser consultado sem endpoint/modelo configurado:

```text
--list --catalog /pasta/historico
--show --catalog /pasta/historico --project-id ID --session-id ID
```

A persistência **não é memória automática do modelo** e ainda não reconstitui
um run interrompido ou o ledger. Ela apenas permite consulta posterior da
conversa e comprovantes mínimos. A escolha da pasta é opt-in.
Detalhes: [docs-r4-local-store.md](docs-r4-local-store.md).


## Auditoria persistente de execuções (opt-in)

Por padrão, a execução e o log continuam efêmeros. Para guardar eventos
validados do próprio Runtime, adicione `--journal /pasta/privada/runs` à
execução normal; o diretório deve estar fora de `--project`.
A CLI imprime o `cli-run-...` necessário para consulta posterior.

```bash
dotnet run --project src/EcosystemAi.Cli -- \
  --show-run --journal /pasta/privada/runs --run-id cli-run-ID
```

`--show-run` dispensa `--project`, endpoint, modelo e chave. Reconstitui
o estado pelo mesmo `RunState.Replay`, exibindo apenas metadados de eventos
sem payload, argumentos de tools nem respostas; não executa modelo, não
retoma tarefas e não modifica journals ausentes. O arquivo do journal **não
é criptografado**; dados enviados/retornados durante execuções podem ser
sensíveis. Veja [docs-r4-run-journal.md](docs-r4-run-journal.md).
