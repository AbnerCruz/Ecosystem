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
