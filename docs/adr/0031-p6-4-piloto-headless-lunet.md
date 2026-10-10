# ADR-0031 — Piloto headless do Host Lunet para P6-4

## Status

Aceito para escopo restrito conforme DEC-0041 e ADD-0018 (autorização expressa do proprietário em conversa em 2026-10-08). **Integração na main continua sujeita ao portão crítico do integrador e à label `integrar` válida do proprietário.** Não revoga ADR-0017.

## Contexto

A Fase 5 do Ecosystem concluiu Host API, IPC e segundo Host real. O Lunet já tem `LunetCapabilityHost.OpenForProject`, Context proveniente de GameId, discover/invoke, grants, revoke/close e `text.inspect@1.0.0`. P6-4 #144 permanece waiting porque o roadmap do Lunet reserva a experiência completa de IA para a Fase 8. O proprietário autorizou avançar até desbloquear P6-4, em resposta explícita à proposta de piloto Lunet antecipado.

## Problema

O critério de sequencing do plano agent-runtime §11.1 mistura a maturidade da interface de IA do Product com a prova de que um Host real pode oferecer Context e capabilities ao Workspace. Esperar Fase 8 para testar apenas a fronteira impede progresso do Runtime compartilhado sem benefício técnico.

## Opções

- A — Autorizar **apenas** prova de composição local/headless com Host real Lunet e modelos `Scripted`, sem ligar UI/IA ao Product; Fase 8 permanece exclusiva para a experiência de agentes.
- B — Esperar Fase 8 do Lunet ou UC-21 do Urbe para qualquer prova de Host real.

## Decisão

DEC-0041 escolhe A. A fronteira **única** do piloto fica em `tests/integration/p6-lunet-host/`: projeto de testes que referencia os assemblies já existentes do Lunet e Ecosystem AI. Usa `ProjectStore.Create` + `OpenForProject`, Context Host-owned, `text.inspect` canônico, Tool Host-owned com grants de organização/projeto/agente e `agent.act`, verificador independente, custo sintético, cancelamento e revogação reais. O teste não cria nem registra capability, SDK, provider comercial, adapter público, segredo, permissão, UI, IA dentro do Lunet, servidor, IPC ou serviço compartilhado.

Não antecipar a Fase 8 do Lunet. P6-4 só pode ser fechado após CI combinado verde e integração da prova técnica em Product real. A P6-5 continua bloqueada enquanto P6-4 estiver aberta.

## Consequências

O Host Lunet é utilizado de fato, sem dependência de produção Lunet → Ecosystem AI e sem tornar Hub essencial. O assembly de composição fica no conjunto de testes, executado pelo CI do Ecosystem; os Products continuam empacotáveis independentemente. Uma integração de usuário em UI será objeto futuro, não inferida de testes headless. Não há validação Android necessária para este piloto que não altera UI/IPC, mas a Fase 8 exige seus próprios DEVICE.

O integrador classificará alterações em governança e CI como críticas. Nenhuma autorização automática é alegada em nome do proprietário; a label de integração segue o fluxo de ADD-0012.

## Alternativas rejeitadas

B adia a comprovação da fronteira sem segurança adicional proporcional; integrar o Product real com painel/modelo agora violaria o roadmap próprio; duplicar Runtime no Lunet violaria NN-020/022.

## Referências

MANIFEST §20, §23, §39; NN-001/002/003/008/009/010/011/016/017/018/020/022/023; ADR-0017 D3/D7, ADR-0021, ADR-0028, ADR-0015; DEC-0041; ADD-0018; ROADMAP P6-4; Issue #144; docs/architecture/p6-4-host-eligibility.md.
