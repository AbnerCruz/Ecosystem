# P6-4 — Qualificação técnica do primeiro Host real (auditoria sem integração)

> Levantamento de código para desbloqueio do R3. **Não aprova nem executa a integração** e não altera ADR-0017 D7 ou as fases locais dos Products. Autoridade de status: ROADMAP.md e Issue #144.

## Código observado

| Critério | Lunet2D | Urbe C# | Hub |
| --- | --- | --- | --- |
| Sessão real do Host | LunetCapabilityHost.OpenForProject e LunetHostSession no Core | WorkspaceSession de editor/conhecimento na UI, mas não equivale ao Host API do Agent Runtime | LocalCapabilityHost / LocalHostSession |
| Contexto | ForProject usa GameId e árvore ecosystem/product/project | Sessão de VaultSnapshot e documentos; Context/grants do agente não estão acoplados | LocalContext e grants capturados |
| Capabilities | text.inspect 1.0.0 está registrada; Discover, InvokeAsync, Connections | Domínio DocumentStore/KnowledgeIndex, sem prova encontrada de Host API genérica nas fontes analisadas | Capability Registry local, Discover e DispatchAsync |
| Segurança do Host | Grants deny-by-default, sessão, fechamento, cancelamento/revogação | Fluxos do editor precisam de adapter e autorização por UC-21 | Grants capturados, revogação/cancelamento, eventos |
| Marco do Product | **Fase 4 ativa; AI/Agentic Workspace previsto na Fase 8** | **UC-18 ativo; IA prevista em UC-21** | Host técnico já presente, mas ADR-0017 D3 define Hub como control plane, não como Product de IA |
| Elegível para P6-4 agora? | **Não** sem reavaliar D7/critério local | **Não** antes do marco ou nova decisão | Não satisfaz o gate de contexto Urbe/Lunet |

Fontes inspecionadas:
- apps/lunet2d/src/Lunet.Core/Capabilities/LunetCapabilityHost.cs (LunetHostContext.ForProject, LunetCapabilityHost, LunetHostSession)
- apps/lunet2d/ROADMAP.md (Fases 4 e 8)
- apps/urbe/csharp/src/Urbe.UI/WorkspaceSession.cs (VaultSnapshot, DocumentStore, KnowledgeIndex)
- apps/urbe/docs/csharp/ROADMAP.md (UC-18 e UC-21)
- apps/hub/src/Hub.Core/Capabilities/LocalCapabilityHost.cs (LocalCapabilityHost, LocalHostSession)
- apps/ecosystem-ai/src/AgentWorkspace/WorkspaceSession.cs e AgentWorkspace.csproj (a sessão encaminha ao AgentRunner; depende só de AgentRuntime.Core)
- docs/adr/0017-agent-runtime-execution-runtime-e-organizacoes.md D3/D7 e docs/architecture/agent-runtime.md §11.1.

## Lacuna precisa

O Agent Workspace R3 já executa dois contextos simulados; não existe prova de que a mesma sessão receba Context, capacidades e grants de um **Product real**. Lunet tem o Host mais próximo do contrato mínimo porque sua Host API já existe, mas o marco Fase 8 foi aprovado como ponto de integração de agentes.

O adaptador de prova deve ser **opcional, local e testável**, sem criar cliente de IA, UI Agent, gravação de dados do usuário ou dependência essencial de outro Product. Sua viabilidade requer:
1. Mapear Context/identidade/grants fornecidos por LunetHostSession para a sessão do Agent Workspace com fronteira de confiança explícita.
2. Usar uma capability real já registrada (text.inspect@1.0.0) para testar descoberta, invocação, cancelamento/revogação e falha de permissão contra uma execução do runner Scripted/Recorded, sem rede.
3. Garantir que nenhum assembly de Core ganhe referência direta ao Product concreto; só adapter da borda quando permitido, mantendo standalone sem Hub/IA.
4. Aplicar testes de conformance com dispositivo apenas se superfícies Android/toque/lifecycle forem alteradas; provas puramente headless não substituem eventual DEVICE.
5. Preservar a diferença entre **prova técnica de integração** e **gate de uso real pelo Product**.

## Decisão e próxima ação

**Não antecipar Fase 8 do Lunet por inferência**. A alternativa de realizar *um pilot técnico restrito* já durante Fase 4 altera o sequencing de D7/§11.1 e precisa de ADR/decisão explícita conforme NN-011, com opções:

- A — autorizar um piloto Host real headless e sem UI/IA no Lunet, limitado à prova de P6-4; manter a Fase 8 para o Agentic Workspace de produto;
- B — preservar integralmente a ordem atual e executar trabalho independente do núcleo até UC-21 do Urbe ou Fase 8 do Lunet.

Esta matriz **não escolhe nem registra** A/B como decisão do proprietário. Nenhum estado P6-4/P6-5 muda, nenhum schema/capability é publicado, e nenhum segundo consumidor é alegado antes do teste de integração.
