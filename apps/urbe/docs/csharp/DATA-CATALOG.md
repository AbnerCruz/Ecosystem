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

## CST-220 — modalidades no cache de modelos (05/11/2026)

`config.json` → `modelCache.{providerId}.list[]` acrescenta três campos opcionais,
escritos só quando o provedor os informa (caches antigos mantêm o JSON idêntico):
- `inputModalities` / `outputModalities`: arrays de texto (`text`, `image`, `file`,
  `audio`, `video`), como a OpenRouter os informa; ausente = não informado, nunca "não".
- `cachedInput`: preço da entrada em cache em US$/1M tokens.

É cache (fonte de verdade: o catálogo do provedor, renovado ao escolher modelo). As
capacidades tipadas (`AiModelCatalog`) são derivadas em memória dele, dos modelos do
aparelho e de uma tabela curta de fatos publicados; não há outro catálogo persistido.
Sem migração. Teste: `AiModelSelectorTests.Store_KeepsReportedModalitiesAndOldCachesByteForByte`.

## CST-216 — diário de execução (06/11/2026)

Arquivo privado novo, no mesmo `AssistantStorage` das conversas (nunca no vault):
`execucao-<runId>.jsonl`, uma linha JSON por evento, só acrescentada (append com
flush em disco). Autoridade: `Urbe.Core/Assistant/AssistantRunJournal.cs`. Cada linha
traz `v` (versão do schema, 1), `seg` (execução 1, e +1 a cada Retomar) e `t`:
`begin` (run, conversa, vault, agente, modelo, objetivo, `base` = mensagens da conversa
no início, `request`, `sources:[{path,sha}]`, `scope`/`expected`/`origin` opcionais),
`request`/`response` (etapa do modelo; a resposta com seus tool_use e `usage`),
`approval` (call, decisão), `intent` (call, tool, hash da entrada, `path`, `before`/
`after` = SHA-256 do texto da nota, `target` em movimentos, `after:"absent"` em
exclusões), `result` (recibo: conteúdo — leituras cortadas em 4000 caracteres —, erro,
status, `changes:[{path, sha antes/depois}]`, `image`, `seen:[{path,sha}]`),
`resume` (a nota de retomada), `finished` (como o laço terminou) e `end` (fechado:
`ended`, `reopened` ou `discarded`). É a autoridade do **estado do run e dos efeitos**;
a conversa continua a autoridade da transcrição (salva no início do run para servir de
base). Linha cortada pela morte do processo é ignorada; `v` maior que 1 não é lido nem
reescrito. Mantêm-se os abertos e os 30 fechados mais recentes; excluir um agente exclui
os diários dele com verificação. Sem migração: conversas antigas não têm diário.

## CST-217 — brief de delegação (06/11/2026)

`conversa-*.json` ganha o campo opcional `brief:{task, scope[], sources[], expected
("note"|"text"), artifact, maxSteps, role ("delegate"|"reviewer"), reviewSha?}` nas
conversas criadas por `delegate_to_agent`. Ausente nas demais (JSON idêntico ao de
antes). Escopo de escrita é aplicado pelo `AssistantScopedHost` antes de gravar. Testes:
`AssistantDelegationTests`, `AssistantRunJournalTests`, `RunRecoveryDelegationUiTests`.

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

## CST-229 — sessão de abas explícitas (beta.39, 01/11/2026)

Preferência local do dispositivo, chave existente `urbe.editor.workspace.v1`:
`{tabs:[path],activeId:path|null,vault,tabPolicy:"explicit-v1"}`.
Autoridade: `MainView.Commands.SaveSession` / `RestoreSession`. `tabPolicy` é
opcional na leitura; sessões antigas sem marcador restauram somente a nota
ativa disponível, ou a última disponível. Sessões novas preservam as abas
escolhidas pelo usuário e o foco, sem transformar navegação comum em histórico
de abas. Fechar a última grava `tabs:[]`; não ressuscita abas fechadas.

O escopo por nome de vault é o contrato nativo preexistente. Não há migração
de notas nem alteração de metadados ou conteúdo do vault. Versões anteriores
ignoram o campo aditivo; o catálogo histórico JavaScript continua congelado.
Fixtures: `EditorTabs_RestoresLegacyHistoryAsOneTabAndPersistsExplicitClosures`
e `Parity_CommandsSessionDownloadAndTips`.

## CST-147/150/151/165/227/228 — editor e Publicações (02/11/2026)

Nenhuma migração, arquivo de configuração ou banco de progresso novo. `progress` e `progresso` são blocos de código cercado Markdown. Campos: `label`, `value`, `total`, `source: tasks`, `note` (ID real opcional), `section` (título único opcional). Valores manuais persistem no próprio bloco; `$nome` consulta frontmatter numérico. Tarefas e percentuais são projeções do conteúdo, não são gravados em metadados auxiliares. Versões antigas exibem o código legível, sem apagá-lo. Texto continua pelo mesmo contrato de escrita/autoria/undo. Numeração de linhas é somente visual.

`.publication.json` mantém `schemaVersion=1`, sem novo campo persistido: `design.layout` reconhece `article`, `book`, `slides`; `design.theme` reconhece `paper`, `ocean`, `ink`. `sources[].fragment` já reservado agora resolve títulos únicos e marcadores `^id` existentes; seleção ausente/ambígua é fonte indisponível e números de linha são recusados. Nenhum offset vira ID permanente. Cada `sections[].id` identifica a peça, inclusive quando várias usam a mesma nota. Histórico de composição é transitório por sessão e não substitui o histórico das notas.

EPUB é snapshot de distribuição gerado sob comando, com OPF, navegação, spine, XHTML e recursos locais. Não é nova fonte de verdade nem formato de importação automática. Editar o EPUB externo não edita o vault. O seletor nativo usa `application/epub+zip`; cancelar não anuncia salvamento. Fontes não suportadas ou alteradas durante a exportação geram erro, não sucesso parcial silencioso.

## CST-230/241 — continuidade de leitores (ADR-0047 proposto; implementação local)

Arquivo privado opcional adjacente ao estado local: `<LocalStatePath>.readers.v1.json`.
Autoridade do schema: `Urbe.Core/ReaderSessions.cs`; IO único compartilhado:
`ReadingStateFile`. Envelope `{version:1,entries:[...]}`. Entrada: `vaultId`
(SHA-256 da identidade nativa da pasta/URI, não o nome exibido), `path`,
`revision` (SHA-256 dos bytes), `format`, `location:{part,offset,scroll}`,
`preferences:{fontSize,lineHeight,zoom,theme}`, `updated` (Unix ms), `deleted`.
Partes são positivas; capítulos/slides não são páginas físicas do EPUB refluível.
Offset refere-se ao texto da revisão exata; scroll é proporção auxiliar.

Não há cópia de notas, OCR, seleção autoral, páginas privadas, credenciais ou
telemetria nessa projeção. Notas/anotações/citações pertencem ao Markdown real
e passam pelo editor e writer verificados. Ler nunca grava o original. Ausência
do arquivo inicia vazio; não migra notas nem metadados de vault. Revisão diferente
não restaura uma posição como exata. Renomear/mover remapeia entradas e grava um
tombstone no caminho antigo para não ressuscitá-lo ao mesclar escritores.

IO serial, debounce, lock por arquivo, escrita temporária, rename atômico e
releitura exata. Mesclagem preserva modificações mais recentes; em empate, o
snapshot que está sendo gravado prevalece sobre disco, e alterações em memória
que ocorreram durante IO não são substituídas por esse disco. JSON inválido ou
versão futura é preservado e bloqueia escrita. Falha não anuncia salvamento da
posição. `.lock` e `.tmp-*` são artefatos técnicos privados, sem texto autoral.
Revisão/schema dependem do gate crítico do ADR antes da integração/release.

Citações continuam Markdown interoperável: `page=`, `chapter=`, `slide=`,
`image=` e `time=` em wikilinks, com `sha256=`; seleção acrescenta `text=` e
`length=`, região acrescenta `region=x,y,largura,altura` normalizada. OCR tem
rótulo estimado/revisado, idioma e rotação legíveis na própria nota; não vira
transcrição certificada. Alteração da fonte bloqueia o retorno exato. Editar ou
remover uma anotação usa a própria nota/undo/lixeira, sem banco oculto de texto.

Perfis WebView2 privados: `Urbe/reader-local-webview` e
`Urbe/reader-browser-webview`. Perfil local só recebe conteúdo sem acesso a
arquivos genéricos; browser remoto não ganha bridge, host objects ou permissões
do vault. Android usa origem local/CSP para documento e WebView remoto; não se
afirma multi-profile universal. Cache/cookies remotos pertencem ao WebView do
sistema; não são exportados nem copiados para notas automaticamente.
