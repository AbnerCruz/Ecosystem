# RPG-015 — Orquestração de agentes por eventos, eficiência e telemetria

## Decisão do proprietário (11/10/2026)

- Tabletop RPG é **totalmente agnóstico de modelo e provedor**, seguindo o
  padrão de separação de `IModelProvider`/`ModelProfile`/`ProviderRegistry`
  do runtime de IA da plataforma.
- Consumo de IA é **pague pelo uso, sem teto obrigatório diário, mensal ou de
  gastos totais configurado pelo Tabletop RPG**. O usuário escolhe o provedor,
  modelo e credencial; preço informado pelo provedor pode variar.
- Priorizar economia inteligente: **zero chamadas na ociosidade**, não executar
  o mesmo evento duas vezes, não rodar em paralelo o mesmo assento, recusar
  loops, registrar recibos sem conteúdo secreto e nunca inventar valores
  monetários quando o provedor não reportar uso.
- Não confundir ausência de **limite de gasto** com autorização para loops
  infinitos: a confirmação de progresso, deduplicação e retry conservador
  continuam necessários para evitar cobrança acidental.

## Contratos existentes e fronteira de componentes

- runtime de IA da plataforma: `AgentRuntime.Core/Model.cs, na implementação de referência da plataforma` define
  `IModelProvider`, `ProviderRegistry` e `ModelProfile`. Provedores são
  adapters separados. Essa é a **referência semântica**, não uma dependência
  de código do Tabletop RPG.
- O RPG já possui `TabletopRpg.ModelGateway.IModelGateway`, também
  agnóstico, com snapshots por turno. **RPG-015 usa essa porta existente**,
  sem escolher OpenRouter ou acoplar formato OpenAI/Anthropic/Gemini.
- ADR-0017/ADR-0022/ADR-0011/NN-022 **proíbem a dependência direta de
  Product para Product** e a extração de runtime compartilhado sem
  contrato, review e aprovação. A ligação real com o provider registry do
  runtime de IA da plataforma deve ser feita **por capability/adapter aprovado**, não
  por `ProjectReference` entre Products.
- O ledger interno do runtime de IA da plataforma atualmente exige pelo menos um escopo
  com limite (`deny-by-default` em `InMemoryLedger`). Não inventamos uma
  implementação paralela desse ledger nem suprimimos sua segurança. Quando
  existir uma integração aprovada, o Host deverá conciliar a preferência
  **sem teto diário/mensal do usuário** com a exigência técnica de autorização
  por operação no runtime. Isso **não está integrado nesta etapa**.
- Não há rede, API key, SDK, preço fixo ou escolha de modelo no código
  novo do Orchestrator.

## O que o orquestrador executa

`RpgEventOrchestrator` registra agentes existentes por `ParticipantId`
e `SeatRole`: jogadores continuam usando `IAgentDecisionSource` +
`AgentTurnRunner`; Mestres continuam usando `GameMasterAgent.ProposeAsync`.

`DispatchAsync(eventKey, participantId, wake)` é a **única ativação
explícita** desta camada. Nunca cria timer, polling, worker ou loop de LLM.

- Um mesmo `eventKey` para o mesmo participante é considerado processado
  depois de uma primeira tentativa (inclusive quando houve erro remoto
  incerto). Repetição retorna `Duplicate`, não cobra novamente.
- Um participante já ocupado retorna `Busy`, sem consumir uma segunda
  chamada ao modelo. Outros participantes podem operar independentemente.
- Se o mesmo gatilho (kind+detail, armazenado apenas como hash) aparece
  novamente para o mesmo participante **sem mudança na revisão canônica**
  desde a última resposta sem progresso, retorna `NoProgress` sem nova
  chamada. O Host deve aprovar a proposta ou provocar mudança legítima
  de estado antes de repeti-la; não existe restrição financeira configurada.
- O fluxo preserva os gates dos agentes: intents de jogadores passam por
  `SessionEngine` (única autoridade de regras). Propostas do Mestre
  retornam **sem autoaprovação**, preservando a etapa `ApprovedGameMasterTools`.
- Um erro cuja consequência de cobrança/execução é desconhecida
  **nunca recebe retry automático**. Uma única segunda tentativa é
  permitida **somente** se o adapter levantar
  `NoChargeTransientModelException`, atestando que a primeira não teve
  custo nem efeito remoto.
- Um evento tem recibo de status, número de tentativas e causa classificada
  (sem prompt, conteúdo de fala, segredo, mundo ou reasoning). Nenhum
  status de erro é considerado sucesso por falta de exceção.
- O livro de eventos de uma instância é transitório e exige checkpoint ao
  atingir o limite operacional de 16.384 IDs. Esse limite é de replay/memória,
  **não é quota financeira, de token, de turno ou de modelo**.

## Medição de uso por modelo e provedor

O `ObservableModelGateway` é um decorator opcional **ao redor da porta
existente**: mantém identificadores fornecidos pelo Host e captura um
`ModelUsageReceipt` por tentativa. `ModelTurnResponse` agora admite
`ModelTurnUsage` opcional, validada (tokens e custo USD não negativos).

- Quando informado, registra tokens de entrada/saída e USD reportado
  pelo provider, preservando se o valor é estimativa.
- Quando ausente ou ocorre erro remoto, custo e tokens = **desconhecidos**
  (`null`), **nunca zero inventado**.
- Não registra payload de mensagens, resposta, senha, prompt nem raciocínio.
- **Não** acrescenta orçamento financeiro ao RPG nem consulta/fatura
  serviços: o valor é apenas telemetria, não cobrança nem garantia de
  correspondência com a fatura do provedor.
- Para realmente contar consumo, o Host injeta no Player/GM um gateway
  decorado e o adapter concreto precisa devolver `ModelTurnUsage`. Uma
  integração HTTP real ainda não existe no RPG-015.

## Limites de entrega

- Sem dependência direta de `runtime de IA da plataforma`, providers concretos, nova
  capability pública, migração no schema v2, UI/Android ou multiplayer.
- Handoff local-first e tests: `dotnet test --project
  apps/tabletop-rpg/tests/TabletopRpg.Core.Tests`.
- Orquestração e recibos são **transitórios**. O componente de RPG ainda
  não possui checkpoint persistido de progress markers ou sincronização
  de eventos entre múltiplos processos (R2/execução futura).
- RPG-015 não conclui o Gate G-R1. A criação rápida de campanha é RPG-016,
  e a experiência mobile está na R2.
