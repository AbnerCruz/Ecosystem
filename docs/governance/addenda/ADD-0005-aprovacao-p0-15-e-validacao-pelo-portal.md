# ADD-0005 — P0-15 aprovado e aprovação de validações pelo portal

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-01, em conversa com o agente `claude-code`, e persistido aqui conforme NN-009.
> **Registros:** validação P0-15 em [`HO-20261001-p0-15-aprovado`](../handoffs/HO-20261001-p0-15-aprovado.json); capacidade nova em [ADR-0008](../../adr/0008-aprovacao-de-validacoes-pelo-portal.md) e DEC-0015 ([`decisions.json`](../decisions.json)).
>
> **Contexto da mensagem:** resposta ao resumo do agente que listava a validação do portal no celular (P0-15) como a única validação pendente no portal.

## Mensagem do proprietário (transcrição integral)

> P0-15 aprovado. Falta inclusive poder aprovar pelo proprio portal

*(O texto foi mantido como enviado, inclusive "proprio".)*

## Interpretação registrada pelo agente (para conferência do proprietário)

| Trecho | Interpretação adotada | Natureza |
|--------|----------------------|----------|
| "P0-15 aprovado" | A validação humana do portal no celular (layout, toque, legibilidade; objeto `https://abnercruz.github.io/Ecosystem/`) **passou**. Registrada no handoff `HO-20261001-p0-15-aprovado` com esta mensagem como evidência. | Decisão do proprietário (validação humana, NN-017). |
| "Falta poder aprovar pelo próprio portal" | Assim como as decisões (DEC-0010), cada **validação humana pendente** no portal deve ter controles **Aprovar** e **Reprovar** que registram o resultado no repositório, sem o agente intermediar. A **capacidade** é pedido do proprietário. | Decisão do proprietário (capacidade). |
| (mecanismo) | O agente propôs [ADR-0008](../../adr/0008-aprovacao-de-validacoes-pelo-portal.md): estender o mecanismo do ADR-0007 (Issue pré-preenchida confirmada pelo dono + workflow), sem token no navegador. Pede ratificação em DEC-0015. | Proposta do agente. |
