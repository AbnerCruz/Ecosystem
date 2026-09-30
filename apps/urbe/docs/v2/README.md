# Urbe 2.0 — Documentação de projeto

A Urbe 2.0 está em **descoberta e auditoria**.

Este diretório existe para impedir que planejamento, decisões e implementação voltem a depender apenas do contexto de PRs e sessões de agentes.

## Documentos atuais

- [AUDIT-1X.md](AUDIT-1X.md) — linha de base estrutural da Urbe 1.x.
- [REQUIREMENTS.md](REQUIREMENTS.md) — Requirement Ledger com IDs estáveis.
- [PROGRAM.md](PROGRAM.md) — processo de concepção, trilhas de descoberta e gates.
- [adr/](adr/) — decisões arquiteturais significativas.

## Documentos deliberadamente ainda não criados

### SPEC.md

Será a especificação canônica da Urbe 2.0.

Não deve ser escrita como palpite inicial. Primeiro precisamos concluir descoberta suficiente sobre produto, arquitetura, dados, segurança, performance, plataformas e distribuição.

### ROADMAP.md

Será a decomposição executável da SPEC aprovada.

Não será um resumo por fases. Todo requisito `IMPLEMENTAR` deverá aparecer em pelo menos um item verificável.

### TRACEABILITY.md

Será a matriz:

```
REQ → SPEC → ROADMAP → TESTE/GATE
```

Ela será usada para auditar cobertura antes de aprovar o roadmap e antes de declarar a 2.0 concluída.

## Regra principal

PRs, commits e CHANGELOG registram trabalho realizado. Eles não substituem a especificação, os requisitos ou ADRs.

Uma decisão importante descoberta durante implementação deve voltar para os documentos normativos.
