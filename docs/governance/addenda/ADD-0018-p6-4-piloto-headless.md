# ADD-0018 — Autorização de piloto antecipado para desbloqueio P6-4

Data: 2026-10-08. Registro da instrução explícita do proprietário nesta conversa, após comparação entre os caminhos para P6-4: **“Tá, então pode fazer até desbloquear a p6-4”**.

**Objeto autorizado:** alternativa A descrita em docs/architecture/p6-4-host-eligibility.md — piloto técnico limitado e headless, usando o Host real Lunet2D na Fase 4 para exercitar o Agent Workspace do Ecosystem com Context, grants, text.inspect, cancelamento e revogação. A autorização trata da preparação da prova técnica, não de entrega de Agentic Workspace ao usuário na Fase 4.

**Limites:** não antecipar a experiência de IA da Fase 8, não alterar autorização de gastos, secrets, permissões públicas, formatos persistentes, IPC de produção nem qualquer outro Product; não alterar o critério da fase atual do Lunet. A evolução de P6-4 continua dependente de evidência objetiva e dos gates de CI/integração.

**Autorização do trabalho ≠ label de integração crítica.** ADD-0012/ADR-0015 exigem evento no GitHub com a label `integrar` aplicada pelo próprio proprietário depois da avaliação do PR para permitir merge de alteração crítica. O agente não aplicará essa label.

Decisão canônica: DEC-0041. ADR de escopo: ADR-0031. Execução: Issue #144, handoff HO-20261008-p6-4-lunet-real-host.
