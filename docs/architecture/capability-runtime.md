# Capability Runtime — sequência e evidência

Autoridade de escopo/estado: [ROADMAP P5-1..P5-7](../../ROADMAP.md). Contratos canônicos: [ADR-0012](../adr/0012-contratos-da-fase-2.md). Detalhes do primeiro experimento: [ADR-0023](../adr/0023-capability-runtime-local-no-hub.md). Este documento define critérios, não aprova gate nem escolhas de IPC.

## Ordem de execução

| Item | Entrega | Evidência necessária |
|---|---|---|
| P5-1 | Plano e boundaries | Referências normativas e dependências explícitas |
| P5-2 | Primeiro runtime/Tool local ao Hub | Testes positivos/negativos, build Android, uso no Hub |
| P5-3 | Contrato público/Host API | Consumidores auditados; ADR/decisão; fixtures conformes à Fase 2 |
| P5-4 | Transporte IPC local | Comparação aprovada; caller autenticado, mensagens inválidas, disconnect, timeout/cancelamento |
| P5-5 | Adapter do segundo Host | Mesma implementação da Tool; teste de consumidor real; Extraction Review antes de mover código |
| P5-6 | Connections | UI derivada do Registry por Context, grants e erros claros |
| P5-7 | Gate | Dois Hosts reais; DEVICE; aprovação registrada |

P4-9 continua independente deste experimento local: bridge integrado não equivale a release assinada/corte validado. O gate da Fase 4 não é autoaprovado pelo avanço de código na Fase 5.

## Boundaries

Host captura identidade, Context e concessões. Tool recebe input validado e Context fixo; não escolhe autoridade. Registry deriva disponibilidade de definições/contratos, escopo e permissões. Product Shell fornece integração de UX, lifecycle e projeto. Hub é primeiro Host e não requisito de execução/distribuição dos Products (NN-003/023).

O slice fica em `apps/hub` até um segundo consumidor real exigir a mesma capacidade. Não referenciar `Hub.Core` a partir de outro Product para fingir compartilhamento. A Extraction Review compara contrato, duplicação removida, dependências novas e custo de manutenção; somente depois autoriza componente compartilhado (ADR-0011/NN-022).

## Critério de segundo Host e IPC

Auditar Urbe C# e Lunet2D: Shell executável, modelo de Context real, APIs próprias para grants/lifecycle, uma necessidade concreta e suíte de consumidor. A implementação JS congelada do Urbe não recebe integração nova durante a migração. Ecosystem AI tem prova headless de sessão, mas isso não é automaticamente Product Shell pronto nem gate do Workspace hospedado.

Comparar chamada em processo, serviço Android autenticado e socket local autenticado segundo plataformas efetivas, caller identity, ownership, desconexão e custo. Não escolher transporte antes do consumidor nem expor localhost sem autenticação. Novo contrato/IPC transversal exige ADR/decisão pelo processo existente; GitHub nunca é transporte de execução.

## Auditoria do segundo Host (base 093ef4f, somente leitura)

| Candidato | Fato observado | Lacuna para consumir a mesma Tool |
|---|---|---|
| Lunet2D | Activity Android e editor reais; `ProjectManifest.GameId` persistido identifica projeto; `LunetProject` lê texto de projeto | Ainda não expõe Host API/capability/grants contextual. Leitura de arquivo não será concedida à Tool por conveniência; adapter pode fornecer texto já selecionado |
| Urbe | Aplicativo JS/Android existente; reescrita C# com paridade tem processo próprio | JS está congelado por DEC-0025-C; não inserir integração nova para contornar o marco da migração |
| Ecosystem AI | `WorkspaceSession` captura Context/grants e encaminha ao `AgentRunner`; bibliotecas e testes executáveis | Sessão experimental não é Shell/UX de Product nem segundo Host visual real |

Fontes verificadas: `apps/lunet2d/src/Lunet.Android/MainActivity.cs`, `MainActivity.Ide.cs`, `apps/lunet2d/src/Lunet.Core/ProjectManifest.cs`, `LunetProject.cs`, `apps/ecosystem-ai/src/AgentWorkspace/WorkspaceSession.cs` e inventário de `apps/urbe` nesta base. Esta auditoria não declara escolha de Host/transporte nem necessidade comprovada de extração.

**Inferência:** Lunet oferece o menor caminho para uma prova Android porque já tem editor/projeto. **Proposta para P5-3:** provar um consumidor que fornece texto selecionado, com Context baseado no GameId, antes de revisar extração/contrato. Nada é integrado no Lunet por esta proposta. Host API/IPC e componentes compartilhados exigem a decisão correspondente pelo processo canônico antes da implementação transversal.

## Matriz do slice local

`LocalCapabilityHostTests` cobre dois contextos, grants negados/indeclarados/imutáveis, scope entre projetos, versões, impersonação, operação/protocolo/kind inválidos, schema/limite do input, repetição, journal/identidades limitados, busy, fechamento idempotente, cancelamento, resultado/progresso tardios e exceção sanitizada. `TextInspectionTool` usa Unicode scalar values (emoji simples conta um caractere), palavras como tokens de whitespace e linhas LF; não é análise linguística nem contador de graphemes.

Handlers são código confiável local: cancellation é cooperativa. Isolamento, timeout obrigatório de código não confiável, persistência e autenticação interprocessos são critérios de P5-4, não promessas deste slice.

## Slice P5-3 — contrato draft e conformance local

A Issue #175 inicia P5-3 sem declarar a Host API pronta. `docs/contracts/capabilities/text.inspect.json` é o primeiro contrato real em estado `draft`: versão 1.0.0, input `text`, outputs `characters/words/lines`, erros de execução da capability, `ui.display` como grant exigido pelo adapter local atual e lifecycle `stateless`. O Hub ainda **não** declara `provides: text.inspect` em `ecosystem.json`; portanto o Registry público/estático não anuncia provider e nenhum Product ganha dependência nova.

A definição local passa a carregar lifecycle e validador de output. Um handler que devolva JSON fora do formato recebe `INVALID_OUTPUT` e não produz frame `response`; exceção vira `EXECUTION_FAILED` sem mensagem arbitrária. Testes de conformance leem o contrato versionado e o comparam com a definição executável do Hub.

Isso é evidência para P5-3, não sua conclusão. Envelope público, Host API de Product Shell, política final de erros/grants/revogação, segundo consumidor real e a decisão arquitetural continuam pendentes. O protocolo `ecosystem-local/0` permanece experimental.

## Validação Android do primeiro Host

1. Abrir Hub e tocar **Analisar texto**; digitar `Olá mundo`, uma nova linha e `🙂`. Esperado: 11 caracteres, 3 palavras, 2 linhas.
2. Texto vazio: todos zero. Unicode, colagem e limite de entrada não devem travar o toque.
3. Fechar durante análise, reabrir e repetir; nenhuma resposta antiga aparece na nova sessão.
4. Enviar app ao fundo/retornar, girar tela, destruir/reabrir Activity: nenhum crash nem session reaproveitada após fechamento. O texto não persiste ao fechar/reabrir o dialog.
5. Conferir layout/teclado/toque em tela pequena e comportamento offline desta ferramenta; o refresh separado do catálogo pode tentar rede.

CI verde não substitui esta validação. O gate de dois Hosts precisa de outra evidência além deste roteiro.

## Dependências das fases seguintes

P6-4 já possui prova local do Workspace, mas precisa do Host real/Fase 5; somente então P6-5 entrega a experiência do Product de IA sem duplicar loop/ledger/provider. Fase 7 exige auditoria e consumidor real antes de extrair Tool. Remote Workers/organizações e deployment têm decisões próprias de confiança, privacidade, orçamento e segredos; não se implementam por inferência a partir do pedido de continuar.

## P5-3 — Host API v1 aceita e implementada

DEC-0034-A aceitou a alternativa semântica e neutra de transporte. A fonte normativa é `docs/contracts/host-api.v1.json`, validada por `host-api.schema.json`; a matriz independente de Host está em `examples/host-api/conformance.v1.json`.

A API pública v1 possui seis operações semânticas: `open-session`, `discover`, `invoke`, `cancel`, `revoke` e `close-session`. Context continua vindo de `context.schema.json`; capability/version/input/output/erros de domínio/lifecycle continuam vindo dos contratos de capability; permissões continuam deny-by-default e Registry continua sendo a autoridade de descoberta/compatibilidade. Identidade e grants são capturados pelo Host e nunca aceitos como elevação no payload.

A taxonomia de `hostErrors` é separada dos erros da capability. Em especial, output inválido do provider é `PROVIDER_CONTRACT_VIOLATION`; `text.inspect` mantém somente `INVALID_INPUT` e `EXECUTION_FAILED` como erros de domínio. Como recebe o texto integralmente em memória, `text.inspect` 1.0.0 não requer `ui.display` nem outro grant externo e passa a `stable`.

O primeiro binding continua local ao Hub e experimental em transporte (`ecosystem-local/0`). Ele agora suporta revogação real de grants, cancela invocação ativa que perdeu permissão e impede resultado tardio. `HostApiConformanceTests` executa todas as fixtures públicas contra esse binding. Isso prova a semântica do primeiro Host sem tornar `Hub.Core` SDK público.

P5-3 termina aqui. P5-4 escolhe transporte/autenticação/timeout/disconnect preservando esta semântica. P5-5 continua responsável pelo segundo Product Shell real; nenhum Product deve referenciar `Hub.Core`, e qualquer extração de runtime/Tool exige segundo consumidor e Extraction Review positiva (NN-022).

