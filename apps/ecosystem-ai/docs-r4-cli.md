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


## Continuação explícita de uma sessão (slice R4)

A CLI salva conversas no catálogo apenas com `--catalog`. Por padrão,
**cada execução continua independente**, mesmo ao reutilizar uma sessão.
Para fornecer ao modelo o contexto recente já salvo, use `--use-history`:

```bash
dotnet run --project src/EcosystemAi.Cli -- \
  --project ./meu-projeto --goal "Continue a análise anterior" \
  --catalog /pasta/privada/historico --project-id ID_PROJETO --session-id ID_SESSAO \
  --use-history --endpoint https://SEU-ENDPOINT/chat/completions \
  --model ID_MODELO --budget-cents 30 --max-call-cents 5 \
  --input-usd-per-million 1 --output-usd-per-million 3
```

`--use-history` exige projeto e sessão existentes, vinculados ao mesmo
workspace. Antes de gravar a pergunta atual, a CLI lê o catálogo validado
e envia ao provedor no prompt **no máximo dez mensagens anteriores, até
12.000 caracteres serializados**. Mensagens são incluídas por ordem,
marcadas com papéis `user`/`assistant` conforme o catálogo, serializadas
como JSON e tratadas como dados, não como instruções do sistema.
Se o registro mais recente ultrapassa o orçamento de contexto, a execução
é recusada em vez de escolher conversas antigas fora de ordem.

**Privacidade e custos:** o usuário precisa ativar a flag conscientemente.
Todo texto histórico selecionado será enviado ao endpoint configurado.
O redator de chaves conhecidas continua aplicado, mas não detecta todo
dado sensível eventualmente escrito nas mensagens. Histórico consome tokens
e pode elevar o custo real de cada chamada. O limite de orçamento da CLI
continua por execução, não por sessão; nunca é divulgado como limite da
conta do provedor.

O catálogo não ganha outro schema, índice, memória automática, vetor ou
resumo gerado. O Workspace e o AgentRunner são os mesmos; o transcript
é apenas conteúdo explícito do próximo `TaskSpec.Goal`.
`--list`, `--show`, `--show-run` e a exportação visual são consultas,
não levam históricos ao modelo.
