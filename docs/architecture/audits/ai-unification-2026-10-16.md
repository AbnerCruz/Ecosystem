# AI-603 — Auditoria de convergência da IA (2026-10-16)

**Snapshot de auditoria**, não autoridade de estado vivo. Base examinada:
`3651207e7cd6a4d66cd977c8a306d9e7bbf5e484` (main). Estado vivo: Issue
[#603](https://github.com/AbnerCruz/Ecosystem/issues/603); escopo: ROADMAP.
Direção: ADD-0027 / DEC-0050 / ADR-0033. Resultados deste incremento:
[PR #604](https://github.com/AbnerCruz/Ecosystem/pull/604).

## Método e fontes

Leitura direta de MANIFEST (incluindo NN-001..023), AGENTS, ecosystem.json,
ROADMAP, ARCHITECTURE, `agent-runtime.md`, local-first, comunicação, DoD e
política de integração; ADR-0011/0017/0020/0021/0031/0032; AGENTS e roadmaps dos
Products; SPEC §16 do Lunet e SPEC de Math Authoring. Inspeção de código abaixo,
fixtures/oráculos e testes, git log e PRs/Issues atuais. A matriz distingue
implementação observada de validação física; providers são demonstrados por
wire/fixtures, não por chamadas comerciais reais nesta auditoria.

**Caminhos relativos à raiz:**

- `apps/urbe/csharp/src/Urbe.Core/Assistant/`: AssistantAgent, AssistantProviders,
  AssistantStore, AssistantTools, AssistantCustomizeTools, AssistantPageTools,
  AssistantPluginTools, UserAgentsFile.
- `apps/urbe/csharp/src/Urbe.Client/AssistantPanel.cs` e `.Views.cs`:
  composição, contexto, configuração, cartões, aprovações editáveis e desfazer.
- `apps/urbe/csharp/tests/Urbe.Core.Tests/Assistant*Tests.cs`,
  `UserAgentsFileTests.cs`, `Urbe.Client.Tests/Assistant*UiTests.cs`;
  `csharp/tools/assistant-*-oracle.mjs` e fixtures `tests/fixtures/assistant/`.
- `apps/ecosystem-ai/src/AgentRuntime.Core/`: todos os componentes do Core;
  `AgentWorkspace/WorkspaceSession.cs`, `AgentRuntime.Providers.ChatCompletions`,
  `AgentRuntime.Tools.Files`, `EcosystemAi.Product`, configuração Android e testes.
- `tests/integration/p6-lunet-host/`: composição externa já autorizada ADR-0031.
- `apps/tabletop-rpg/src/TabletopRpg.ModelGateway/` e seus agentes integrados;
  PRs pendentes separados abaixo. Nenhuma implementação HTTP do gateway na main.
- `apps/math-authoring/SPEC.md`, `ROADMAP.md`: IA opcional futura, sem gateway
  consumidor pronto. `apps/lunet2d/docs/SPEC.md` §16: direção, não implementação.

O plano `agent-runtime.md` §2 é uma auditoria histórica: “nenhum runtime” e
“IA do Urbe apenas JS” não descrevem mais o código. Seus contratos propostos não
se tornam públicos só porque existem classes locais com nomes parecidos.
ADRs 0020/0021 ainda têm status Proposto, apesar de implementação integrada:
essa diferença documental não autoriza chamá-los de contrato estável.

## Matriz de equivalência verificada

| Recurso | Urbe nativo (código) | Ecosystem AI (código) | Convergência / limite |
|---|---|---|---|
| Autoridade do loop | `AssistantAgent.RunAsync`, independente de provider | `AgentRunner` único; Workspace/Product o compõem | Melhorar Runner; manter Urbe até equivalência |
| OpenAI / OpenRouter / compatível | presets e wire Chat Completions | ChatCompletionsProvider + endpoint configurável | Adaptador sem domínio é candidato real |
| Gemini | preset via API OpenAI-compatible | possível via endpoint compatível; sem adapter Gemini próprio demonstrado | Não alegar suporte nativo completo |
| Ollama | preset OpenAI-compatible, chave opcional e endereço configurável | transporte aceita HTTP loopback; configuração Android exige chave, sem suporte LAN equivalente | Lacuna real de configuração/política de endpoint |
| Anthropic | wire Messages, SSE, tools, modelos e erros próprios | Sem adapter Anthropic na main | Incorporar adapter fora do Core com equivalência |
| Streaming texto/tools | SSE/JSON, texto, tool deltas e cartões parciais | `IModelProvider.CompleteAsync`, resposta completa; flag Streaming não implementa stream | Paridade obrigatória antes de troca |
| Descoberta de modelos | modelos/contexto/preço/tools; cache 6h; fallback Anthropic | `ProviderModelDiscovery`, GET models com limites; IDs somente | Compartilhar transporte/parser após contrato; UI/cache por consumidor |
| Erros e retry | mensagens amigáveis; retry 429/5xx no adapter | tipos estáveis, timeout/transporte sem retry; alguns HTTP transient + retry do Runner | Harmonizar sem repetir chamadas possivelmente cobradas |
| Providers/modelos salvos | `AiConfig`, preferência e cache | SavedModelConfiguration, seleção rápida por provider/modelo | Sem duplicar formato nem assumir config global |
| Segredos | `ProviderToJson` inclui `apiKey`; FolderAssistantStorage grava JSON privado sem cifra | Android ModelPreferences: AES-GCM com chave AndroidKeyStore; CLI recebe por ambiente | Não promover armazenamento do Urbe; migração segura específica pendente |
| Falhas de persistência | FolderAssistantStorage engole IOException/UnauthorizedAccessException | ModelPreferences publica em memória após Commit e preserva erro de leitura | Risco concreto: não tratar “salvo” do Urbe como comprovado em erro de disco |
| Agentes especializados | quatro built-ins + custom, tools/read/write/delegate | AgentDefinition, roster, equipes e produtor/revisor | Perfis/dados são específicos; execução é generalizável |
| Instruções | global + papel + `Personalização/AGENTS.md` por modelo (UC-50) | SystemPrompt/profile do agente; contexto montado pelo Product | Preservar UC-50; não ler documentos arbitrários como política |
| Memória | fatos gerais e por nome de vault | histórico/catalog/contexto; não equivale à memória de fatos do Urbe | Escopo por identidade estável antes de generalizar |
| Contexto eficiente | Prepare repara pares tool-use/result, corta contexto, acompanha leitura parcial | conversa do run e contexto de histórico opt-in limitado no Product | Adaptar preservando pares e limites; não importar domínio de notas |
| Planejamento | update_plan como tool e checklist | TeamPlan, TaskSpec, dependências e planejamentos do Product | Plano conversacional ≠ task graph; não duplicar autoridade |
| Delegação | delegate_research, pesquisador read-only, limite/não recursivo | equipes com produção/revisão; não mesma API de subagentes dinâmicos | Paridade ainda não demonstrada |
| Leitura paralela | leituras independentes em paralelo, escritas em ordem | ferramentas sequenciais | Não substituir perdendo comportamento/performance |
| Descoberta de tools | schemas filtrados por agente/política | ToolDescriptor com versão/permissões/escopo; ToolResolver | Mapear tools locais a contratos, não renomear e fingir integração |
| Autorização | readonly/ask/auto; destrutivo/plugin sempre confirma; UI fornece approver | interseção Host/org/projeto/agente, agent.act explícito; destrutivo aprova | Granularidade diferente; nenhum autoapprove genérico herdado |
| Prévia antes/depois | AiPreview e diff; editar nota proposta antes de aprovar (UC-51) | ChangeSet/IsolatedWorkspace e revisão independente; ReviewedProjectFile por gesto do operador | Exportar resposta confirmada não é edição agêntica genérica |
| Desfazer | lote de mudanças de notas, preserva conteúdo editado depois | overlay/changes antes de integrar; sem undo de ferramentas genérico | Permanecer domínio/adapter até prova de reversibilidade |
| Cancelar/continuar | cancelar stream/approval; Continue inicia nova mensagem/run | token; ResumeAsync reconstrói eventos e não repete efeito não idempotente interrompido | Semânticas diferentes; não chamar ambos de retomada durável |
| Repetição improdutiva | falhas idênticas não executadas novamente; 3 repetições param | limite de passos, falhas/provider/verificação e sequência de resultado repetido | PR #604 incorpora supressão por falha, com replay e invalidação por mutação |
| Injeção de instruções | prompt instrui tratar notas/resultados como dados | grants/escopo e verificadores; instruções de contexto no Product | Prompt é defesa auxiliar, não sandbox/prova de imunidade |
| Auditoria/aceite | mensagens, status, mudanças e uso por run | event log, RunState replay, verifier separado, receipts | Preservar verificação independente; texto “feito” não vira sucesso |
| Raciocínio | UI/metadados guardam reasoning recebido | ReasoningBlock descartado; serializer recusa | Não promover persistência de raciocínio privado; status operacionais bastam |
| Tokens/custos | AiUsage com custo opcional/estimativa por preço na UI | Usage/Money e ledger por escopo; sem custo calculável falha fechada no provider | Distinguir real/estimado/desconhecido; sem teto obrigatório novo no RPG |
| Orçamento | limite de passos, sem ledger financeiro equivalente | reserva/settle com tetos explícitos; sem escopo configurado nega | Contrato de políticas por Product antes de integrar RPG |
| UX mobile | Avalonia C#; chat, modelo/agente/permissões, Markdown/LaTeX/cartões/diff | Android C# nativo, catálogo/sessões/tarefas/equipes/recibos; CLI/web opcionais | Compartilhar comportamentos; não impor tela ou WebView |
| Independência | Urbe.Core + cliente, vault próprio | Runtime no Product AI, sem Hub | Atualmente não existe pacote de runtime consumido por dois Products em produção |

**Melhorias recentes que são parte da referência:** UC-50 AGENTS por modelo;
UC-51 edição antes de aprovar e marca `{ia:"modelo"}`; plugins declarativos do
vault, validação/aprovação obrigatória antes de ativar; páginas/tema e desfazer;
botão do assistente no editor e contexto de nota/seleção. Essas mudanças estão
na main examinada; nenhuma será apagada para simplificar a migração.

## Consumidores e trabalhos em andamento

| Product | Situação observada | Decisão técnica |
|---|---|---|
| Ecosystem AI | consumidor de produção de Runner/Workspace; provider concreto, histórico, tarefas, equipes e Android | Referência de infraestrutura; não extrair armazenamento/UI junto |
| Urbe | consumidor real de seus próprios providers/loop, UX mais completa; novos recursos ativos | Melhor candidato adicional **por transporte/provider primeiro**, mantendo loop e UI; ainda não consumidor do Runner |
| Lunet2D | Host real/capability text.inspect; piloto test-only usa Runner/Workspace; experiência IA na Fase 8 | Reutilizar piloto como regressão, não antecipar Fase 8 nem contar dois projetos como dois Products |
| Tabletop RPG | gateway local, Player/GM/Director; intents e perspectivas privadas | Adapter de modelos futuramente; preservar mecanismo de jogo e orçamento próprio |
| Math Authoring | documento/transações/storage; IA futura fora do MVP | Não antecipar IA nem criar adapter vazio |

**PRs/Issues inspecionados em 16/10 (snapshot):**

- Main: #601 (caixas/editor e comentar prévia), #600 (beta.13); #573 (configuração
  AI com Keystore), #571 (plugins pelo Assistente), #578 (publicação revisada de
  respostas no AI). Commits recentes `096cdf25` (UC-50) e `8e9e2274` (UC-51).
- [#602](https://github.com/AbnerCruz/Ecosystem/pull/602), ativo: Urbe teclado,
  tutorial e UX, ADD-0026 / DEC-0049. Não tocar seus arquivos de cliente nem
  reutilizar IDs de governança reservados. ADD-0027 / DEC-0050 usados aqui.
- RPG: [#521](https://github.com/AbnerCruz/Ecosystem/pull/521) NPC;
  [#523](https://github.com/AbnerCruz/Ecosystem/pull/523) eventos/pay-as-you-go;
  [#526](https://github.com/AbnerCruz/Ecosystem/pull/526) QuickStart;
  [#528](https://github.com/AbnerCruz/Ecosystem/pull/528) Android, empilhados.
  Código de PR não é código integrado. Não modificar/sobrepor essa cadeia.
- Issues #504/510/516/518 ainda relatam review histórico de partes que já
  aparecem na main; isso exige reconciliação própria, não reimplementação.
- Issue #497 (evidências do quadro AI), #495/#492/#477 (Lunet Fase 4) e #299
  (Math storage) consultadas. Não assumir conclusão ou apropriar trabalho alheio.
- PRs Dependabot abertos #581–#590 são atualizações de dependência; esta missão
  não os integra nem altera versões por efeito colateral.

## Extraction Review preliminar — NN-022

**Resultado: promoção ainda não aprovada nem executada.**

1. **Complexidade removida:** transporte/providers normalizados, uso/erros,
   discovery e depois execução genérica teriam uma implementação por
   responsabilidade; ferramentas/estado de domínio não entram no pacote.
2. **Consumidores reais:** AI e Urbe já têm necessidade/código funcional, mas
   não usam a mesma implementação. Piloto Lunet só fornece evidência de boundary
   headless. Falta provar dois consumidores efetivos do recorte proposto.
3. **Contrato a estabilizar:** request/response, stream público de status/texto/
   tool-deltas sem raciocínio privado, modelo/capabilities, usage real/estimado/
   desconhecido, cancelamento, erros/retry seguro, descoberta e SecretRef. Sem
   nomes de Products/Hosts ou IO no Core. Não existe ainda esse contrato público
   completo; não congelar as classes atuais por conveniência.
4. **Custo novo:** compatibilidade/versão de contrato, builds/packaging standalone,
   CI de consumidores, lifecycle de stream/credenciais, migração e política de
   orçamento. Evitar promover todo Workspace/store/ledger de uma vez.
5. **Saldo:** favorável à convergência incremental; **insuficiente para extrair
   já o Runtime completo**. Próxima review deve anexar duas composições reais,
   equivalência e grafo/CI, definir owner e forma de distribuição e só então
   registrar ADR de promoção/contrato/componentes. Não criar pasta preparatória.

## Alternativas e riscos

Copiar Urbe: avanço aparente com deriva permanente. Trocar loop agora: perde
capacidades listadas na matriz. Extrair AI inteiro: acopla storage/UI e regras
financeiras. A alternativa escolhida pelo proprietário é convergência
incremental (ADR-0033); contratos concretos/promoções ainda têm seus gates.

Riscos prioritários: chave em texto claro no Urbe; erro de persistência oculto;
tratamento diferente de custo desconhecido/limites; retry remoto com cobrança
incerta; modelo de grants diferente; contexto/memória por nome; semântica de
continuação; leitura parcial; rollback e revalidação de conteúdo após edição.
Nenhum risco é resolvido só por mover arquivos. A migração deve testar falha de
save, cancelamento, segredo-sentinela, modelo sem tools, formato futuro,
permissão ausente/revogada, aprovação recusada/editada e reabertura no aparelho.

## Plano executável e gates

| Incremento | Implementação / saída | Gate antes do próximo |
|---|---|---|
| AI-603 / A | registrar ordem, matriz e revisão preliminar; #604 melhora proteção do Runtime | CI/revisão/integração separados; não encerra missão |
| B1 | especificar equivalência de transporte/providers com fixtures do Urbe e testes do adapter AI; streaming/Anthropic/usage sem alterar loop | testes dos dois lados e decisão de contrato concreto |
| B2 | política de segredos por plataforma e migração não destrutiva; distinguir política de gasto do consumidor e telemetria | revisão crítica e testes de falhas/privacidade; DEVICE quando necessário |
| C | Urbe consome o recorte neutro por adapter, mantendo AssistantAgent/Panel; AI usa a mesma implementação | ADR/contrato/Extraction Review/manifest/CI aprovados no mesmo conjunto; dois consumidores reais; standalone |
| D | RPG pelo gateway preservando intents/visões e pay-as-you-go; Lunet na Fase 8; Math quando elegível | roadmap e capacidades reais de cada Product |
| E/F | equivalência de configuração/permissões/histórico e progresso, builds necessários Android/desktop | teste real de providers/aparelho por build; regressões sem atalhos |

A recomendação “Urbe primeiro por providers” reduz risco de regressão do loop,
mas não autoriza uma extração disfarçada por links de código entre Products.
Também não proíbe melhorar o Runtime local enquanto a review amadurece.

## Evidência e limites do primeiro código

PR #604: supressão de ferramentas que falharam, identidade canônica, contagem e
invalidação por mutação reconstruídas do event log. Testes de replay, falhas,
argumentos, isolamento, recusa/aprovação e logs antigos. 268/268 testes Runtime,
11/11 ProjectStore, 135/135 testes do assistente/AGENTS do Urbe Core e piloto P6-4 passaram localmente. 22 checks de consistência
passaram, com ROADMAP × Issues não verificado localmente por falta do snapshot.
CI/integração são registrados pelo PR/handoff, não presumidos nesta auditoria.

Não houve chamada paga nem validação DEVICE. O Urbe não foi modificado. Não há
segundo consumidor do Runtime em produção, nem equivalência completa ou missão
concluída. APKs existentes não contêm automaticamente código em PR; builds
futuros devem ser vinculados ao commit e aos workflows correspondentes.
