# ADR-0007 — Credenciais de IA e Content-Security-Policy

- Status: Proposed (pendente de confirmação do proprietário — OD-08)
- Data: 2026-09-30
- Requisitos: REQ-021, REQ-050, REQ-053, REQ-054, REQ-060
- Decisores: Abner P. S. Cruz (proprietário)

## Contexto

Chaves de IA ficam em texto puro no IndexedDB `urbe-ai`, legíveis por qualquer código da origem (plugins, XSS). Não há CSP. Android tem `allowBackup=true`. A IA pode criar `.js`, `.css`, `.html` no vault.

## Forças

- paridade entre plataformas;
- plugins full-trust já leem o storage (ADR-0002);
- custo de criptografia nativa por plataforma.

## Alternativas consideradas

### A — Mitigações agora (allowBackup, exclusão de export, máscara, aviso) + CSP + política de artefatos ativos de IA

Baixo custo; risco residual aceito e documentado.

### B — Criptografia nativa (safeStorage/Keystore) por plataforma

Melhor proteção; custo alto e paridade difícil.

### C — Senha-mestra do usuário

Fricção de UX; não protege de plugin com o app desbloqueado.

## Decisão

Recomendação: **A**. `allowBackup=false`; chaves nunca no ZIP/manifesto; máscara e aviso de risco; CSP por plataforma documentada e testada (compatível com CDNs fixados/vendorizados, plugins e exports); artefatos ativos criados por IA exigem confirmação explícita, sem execução automática e sem `url()` externo em CSS não aprovado. B fica ADIADA (REQ-054), com gate de reavaliação: E2E nativo estável.

## Consequências positivas

- redução imediata de superfície
- decisão explícita de risco residual

## Consequências negativas / trade-offs

- chave continua legível por plugin aprovado

## Migração

Nenhuma migração de dados de chaves.

## Validação

testes de manifest/backup, CSP (política + E2E), política de artefatos IA (G3).

## Relações

- documentos afetados: THREAT-MODEL §6, tutorial (Arquivos e segurança)
