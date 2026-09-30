# Urbe 2.0 — Documentação de projeto

A Urbe 2.0 concluiu a **descoberta (issue #33)** e tem SPEC, ROADMAP, TRACEABILITY e ADRs redigidos, **aguardando aprovação do proprietário**. A implementação da 2.0 ainda **não foi iniciada**.

Este diretório existe para impedir que planejamento, decisões e implementação voltem a depender apenas do contexto de PRs e sessões de agentes.

## Documentos

- [AUDIT-1X.md](AUDIT-1X.md) — linha de base estrutural da Urbe 1.x.
- [REQUIREMENTS.md](REQUIREMENTS.md) — Requirement Ledger (REQ-001..088).
- [PROGRAM.md](PROGRAM.md) — processo de concepção, trilhas e gates.
- [discovery/](discovery/) — evidência da descoberta: [ARCHITECTURE-MAP](discovery/ARCHITECTURE-MAP.md), [LEGACY-MAP](discovery/LEGACY-MAP.md), [DATA-CATALOG](discovery/DATA-CATALOG.md), [TEST-MATRIX](discovery/TEST-MATRIX.md), [PERFORMANCE](discovery/PERFORMANCE.md), [THREAT-MODEL](discovery/THREAT-MODEL.md), [PRODUCT-UX](discovery/PRODUCT-UX.md), [PLATFORMS](discovery/PLATFORMS.md), [OPEN-DECISIONS](discovery/OPEN-DECISIONS.md).
- [SPEC.md](SPEC.md) — especificação normativa (rascunho para aprovação).
- [ROADMAP.md](ROADMAP.md) — itens executáveis `RM-Fn-nn` (estados `[ ] [~] [?] [x] [!]`).
- [TRACEABILITY.md](TRACEABILITY.md) — matriz REQ → SPEC → fase → item → teste/gate (**gerada**).
- [AUDIT-COVERAGE.md](AUDIT-COVERAGE.md) — auditoria de cobertura cruzada.
- [adr/](adr/) — decisões arquiteturais (0001–0007).
- [`AGENTS.md`](../../AGENTS.md) e [`AGENTSCHAT.md`](../../AGENTSCHAT.md) — contrato e log de coordenação de agentes (raiz).

## Verificação automatizada

```
node tools/check-traceability.mjs        # REQ IMPLEMENTAR ∈ SPEC ∧ ROADMAP ∧ TRACEABILITY
node tools/gen-traceability.mjs --check  # TRACEABILITY em dia
node tests/traceability.mjs              # teste do verificador
```

## Regra principal

PRs, commits e CHANGELOG registram trabalho realizado. Eles não substituem a especificação, os requisitos ou ADRs.

Uma decisão importante descoberta durante implementação deve voltar para os documentos normativos.
