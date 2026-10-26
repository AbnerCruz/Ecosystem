# Catálogo de dados — Assistente nativo do Urbe

Autoridade de implementação: `Urbe.Core/Assistant/AssistantStore.cs`. Este catálogo
descreve a evolução do cliente C#; o catálogo em `docs/v2/discovery/` permanece
congelado como evidência histórica do JavaScript e não recebe formatos nativos.

## Urbe C# — espaços por agente (25/10/2026, ADR-0041)

Configuração privada atual `AssistantStore/config.json`: campos aditivos
`agentWorkspaceVersion:1`, `agentPolicies:{id:policy}`, `agentImages:{id:{providerId,model}}`
e, após a correção de permissões, `agentToolScopes:{id:"read"|"write"|"all"}`.
O novo campo é opcional, aditivo e não exige migração: sem entrada vale o teto
legado `AiAgentDef.Tools`; a política individual continua deny-by-default.
Mesmo ownership Core, fora do vault. Preserva `AgentModels`, instruções, credenciais,
memórias e escolhas legadas. Backup dos bytes originais verificado em
`config-before-agent-workspaces.json`; gravação lida de volta; ilegível/versão
futura/falha de backup bloqueiam migração. Marcador torna repetição idempotente.
`conversa-*.json` não é migrado nem reatribuído: AgentId/Vault filtram o histórico.
Excluir um agente exige confirmação: seus `conversa-*.json` legíveis, com
`AgentId` correspondente, são removidos em todos os vaults, com verificação
de leitura/listagem após cada exclusão. Se o armazenamento falhar silenciosamente,
a operação é abortada com erro visível e os dados de configuração do agente
permanecem disponíveis para nova tentativa. Arquivos de outros agentes e
conteúdo dos vaults não são afetados. JSON de conversa ilegível não pode ter
sua propriedade inferida e não é apagado por suposição.

Fixtures/testes: `AgentWorkspaceTests` e `AgentWorkspaceUiTests`.

## UC-89 — escopo de ferramentas e delegação (25/10/2026)

`config.json` acrescenta dois campos opcionais, com as mesmas regras de escrita
verificada do `AssistantStore`:
- `agentToolScopes:{agentId:"read"|"write"|"all"}`: teto efetivo dos
  comandos; se ausente, vale o `AiAgentDef.Tools` original (sem grant novo).
- `agentDelegationAuto:{agentId:boolean}`: autorização para solicitar
  especialistas sem confirmação inicial; ausente equivale a `false`.
  Nem `true` permite que origem somente leitura dispare automaticamente
  especialista com escrita. O executor destinatário usa sua política própria.

Os arquivos `conversa-*.json` preservam o formato: cada consulta delegada
cria um novo arquivo com `AgentId` do especialista e dois campos opcionais de proveniência: `originConversationId` e `originAgentId`. O par é validado antes de continuar uma consulta usando `conversation_id`; sem correspondência exata não há acesso ao diálogo. As mensagens do solicitante não são transplantadas
da origem. Conversas ativas permanecem na memória até concluir; seu registro
é gravado no armazenamento privado existente. Não há acesso direto de
agentes a arquivos de históricos ou memórias dos demais.

## UC-90 — formulários e fila de mensagens (ADR-0042)

`conversa-*.json` acrescenta arrays opcionais `forms` e `queuedMessages`, vazios quando ausentes. Cada formulário usa versão 1 de `definition`, IDs estáveis de sessão/conversa/agente/chamada, submission_message_id, estado pending/submitted/cancelled, valores JSON tipados e source_hash opcional. Dados continuam privados no AssistantStore, fora do vault. Rascunhos de templates usam `form-template-HASH.json`, mesma definição/sessão, chave por vault/path/título e hash do conteúdo do template. Não existe migração de notas/configuração nem reatribuição de conversas. Gravação é verificada por releitura; falha não é anunciada como sucesso. Formatos futuros de rascunho são recusados sem sobrescrever. Fixtures/testes: FormsTests e AssistantCrashFormsUiTests.

Formulários reutilizáveis são notas Markdown de `Modelos/`, com comentário opcional `<!-- urbe-form JSON -->`. Sem comentário, os modelos antigos continuam com seu inventário de placeholders e campos de texto. O comentário é omitido da nota criada. Condições e cálculos são dados declarativos, não scripts. A fila contém mensagens no formato atual de AiChatMessage; só entram no transcript do modelo quando chega sua vez.

Cada registro de execução pode incluir `efficiency:{reused_reads,no_op_writes,context_chars_saved}`. Ausente equivale a zero; não migra conteúdo nem altera tokens/custos informados pelo provedor. Cache de leituras é transitório por execução/versão do DocumentStore, sem novo arquivo ou autoridade persistida. Status `reused`, `noop` e parada `no_progress` tornam a otimização observável no histórico.

## UC-91 — exportações e manifesto de compatibilidade (26/10/2026; ADR-0043 proposto)

**Exportação:** snapshot gerado sob demanda pelo usuário e salvo fora dos dados internos do Urbe pelo seletor nativo. Não cria um segundo registro canônico. JSON completo usa envelope de schema `urbe.assistant.export/1`, com kind `conversation` ou `agent-memory`, mode, exportedAt e appVersion. Em `conversation`, o payload é a projeção atual de `AssistantStore.ConversationToJson`, contendo IDs, tool_use/tool_result, metadados, filas, formulários, webSources, usage e runs/efficiency. Em `agent-memory`, só registros de `config.json` com Scope=agent e AgentId da seleção, em todos os estados; outros escopos/agentes e Providers/ApiKey ficam de fora. Lite usa Markdown legível com mensagens e ferramenta resumidas. Os dados privados contidos no próprio histórico podem aparecer no arquivo exportado: o destino e compartilhamento pertencem ao usuário. Não existe escrita de export no vault nem importação automática.

**Compatibilidade declarada no vault:** arquivo opcional `.urbe/compatibility.json`, schemaVersion=1, array `files` com entradas `{path,minWriterVersion,features}`. A autoridade do arquivo original permanece seu próprio conteúdo; este manifesto é política auxiliar de escrita. Leitura não modifica o manifesto nem os arquivos. Ao abrir/salvar, `VaultReader` compara com a versão **instalada** do binário e a lista conhecida de recursos; desconhecido ou requisito futuro entra em FutureFiles. O planner de escrita evita alterações in-place; metadados estruturalmente ilegíveis levam o vault todo a somente leitura até revisão. Registros de arquivos já removidos são ignorados para não imobilizar o vault. Ausência do manifesto conserva o comportamento legado, sem declarar compatibilidade universal. Em UC-91 não há geração automática do manifesto por cada editor, nem migração forçada.

**Versões e patchnotes:** `Urbe.Android.csproj` permanece a autoridade da versão de distribuição. `PATCHNOTES.md` e `PATCHNOTES-HISTORY.md` são snapshots textuais legíveis/embutidos e precisam ser atualizados junto com novas versões, sem substituir o versionamento canônico ou atribuir código de PR a uma release anterior. O catálogo GitHub é leitura temporária sob demanda; nenhuma cache persistente e nenhuma instalação automática.

## UC-87.2 — Uso detalhado do provedor sem novos arquivos (26/10/2026)

Formato armazenado: mesmo `conversa-*.json` em `AssistantStore`, com `usage` em cada metadado de resposta e no acumulado. **Somente campos opcionais, aditivos**: `cacheReadInput` (tokens de entrada lidos de cache), `cacheCreationInput` (entrada usada para criação de cache), `reasoningOutput` (subset dos tokens de saída) e `costSource` (`reported`, `estimated`, `partial`, `unknown`). Ausência de qualquer campo preserva o formato e o comportamento de leitura das conversas antigas. Sem reescrita ou migração ao abrir, sem novo arquivo de memória/cache e sem mudança de autoridade. A serialização das conversas antigas continua inalterada quando não há dados dessas categorias. Os campos são emitidos somente se houver informação, e a leitura foi testada contra registros legados.

A Anthropic discrimina `input_tokens` não cacheados e categorias de leitura/criação de cache; o total é a soma dos buckets disjuntos, mantendo os subtotais como subgrupos. OpenAI usa `prompt_tokens` como total e `prompt_tokens_details.cached_tokens` como subconjunto; nunca somar `cached_tokens` ao total. `reasoning_tokens` são subconjunto de saída. Custos sem tarifas ou dados de provedor permanecem desconhecidos, nunca tratados como gratuitos. O acumulado indica `partial` quando alguma chamada não informa custo. Dados de uso não contêm credenciais nem texto para benchmarks.

Cache lexical transitório UC-87.2: `ConditionalWeakTable` em memória de processo, por objeto `AiMemoryRecord` com uma única versão recalculada ao detectar mudança de texto/tipo/caminhos; escopo, estado, datas e confiança são filtrados em cada consulta. Não existe persistência desse cache nem indexação secundária de autoridade. Fonte de verdade: `config.json` e `memoryRecords` atuais, sem migração.
