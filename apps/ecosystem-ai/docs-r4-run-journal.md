# R4 — EventLog durável local do Ecosystem AI

`EcosystemAi.RunJournal.LocalRunEventLog` implementa a porta existente
`AgentRuntime.IEventLog`. Não cria executor, modelo, ferramentas nem estado
de run paralelo. A reconstrução oficial continua sendo
`AgentRuntime.RunState.Replay`.

O Host escolhe explicitamente um diretório privado; o Product salva um arquivo
JSON versionado por run, indexado por SHA-256 do ID (o ID não vira path).
Todos os eventos são serializados pelo `EventSerializer` já existente; o adapter
verifica `EventContract`, sequência, integridade do envelope, schema e o replay
do Core a cada leitura e escrita. A implementação grava um snapshot atômico
em arquivo temporário com flush antes de renomear. Operações multi-instância
exigem lock exclusivo; nenhum dado é silenciosamente reparado ou sobrescrito
após corrupção/versão desconhecida. Symlinks no arquivo canônico e no lock
são recusados. Há limites de 10.000 eventos e 8 MiB por run.

**O que é comprovado:** duas instâncias conseguem reabrir um run concluído,
preservar identidade/contexto/eventos e reconstruir o estado do Core; um run
sem arquivo retorna lista vazia; corrupção, ordem inválida, evento após terminal,
versão futura e lock impedem alterações. O CI executa essas provas sem rede.

**Limites:** o adapter registra os payloads fornecidos pela porta. A redação de
segredos conhecida permanece responsabilidade do AgentRunner/Host antes do
append; nenhum SecretStore nem criptografia é fornecido aqui. Os journals
são dados locais em claro. Checksum detecta corrupção acidental, não autentica
um invasor com acesso de escrita à pasta. O adapter não decide se é seguro
retentar uma tool com efeito colateral após interrupção; retomada automática
de tarefas e reconciliação de side effects são slices posteriores. Nenhum
background service, sync ou deploy foi implementado.

Teste: `dotnet test --project apps/ecosystem-ai/tests/AgentRuntime.Tests`.


## Integração opcional na CLI do Product

Uma execução normal continua usando log efêmero em memória. Para persistir os eventos
do mesmo `AgentRunner`/`WorkspaceSession`, indique uma pasta privada **fora do
workspace autorizado ao agente**:

```text
--project /pasta/projeto --goal "..." --endpoint https://... --model ... [demais flags] --journal /pasta/privada/runs
```

O run recebe um ID impresso na saída, e seus eventos são gravados e validados
pelo `LocalRunEventLog`. O conteúdo do journal pode incluir prompts, argumentos,
resultados e texto de ferramentas, portanto fica **em claro e não deve ser
armazenado dentro do projeto que o agente pode ler ou em pasta sincronizada
sem proteção**. Nenhuma escrita de eventos é ativada sem `--journal`.

Consulta local, sem chave, provedor ou rede:

```text
--show-run --journal /pasta/privada/runs --run-id cli-run-ID
```

A consulta verifica checksum, contrato e replay do Core antes de devolver somente
metadados (status, verificação, passos, ferramentas, custo registrado e tipo de
cada evento). **Não imprime payload, mensagens, argumentos ou resultados**;
um run ausente não cria novo arquivo. Isso é auditoria, não execução de retomada:
o comando `--show-run` nunca chama modelo ou reexecuta tools. A persistência
do log ainda não restaura automaticamente orçamento, side effects ou uma
execução interrompida. O catálogo de sessões e o journal continuam opt-ins
independentes, sem segunda autoridade de estado.
