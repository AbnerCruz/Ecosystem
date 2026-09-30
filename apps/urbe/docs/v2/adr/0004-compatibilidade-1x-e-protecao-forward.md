# ADR-0004 — Compatibilidade 1.x → 2.x e proteção forward

- Status: Accepted (aprovado pelo proprietário em 2026-09-30)
- Data: 2026-09-30
- Requisitos: REQ-007, REQ-023, REQ-035, REQ-036, REQ-037, REQ-038, REQ-082
- Decisores: Abner P. S. Cruz (proprietário)

## Contexto

A 1.8.2 descarta e sobrescreve `history.json`, `compositions.json` e o `journal.json` de versão desconhecida (R-1) e reescreve `version` de páginas (R-14). Não existe versão de formato do vault nem fixtures históricos. O `mapa.json` tem `v` escrito e nunca lido.

## Forças

- dados do usuário são invariante (REQ-007);
- a 1.8.2 em campo não pode ser corrigida retroativamente;
- vaults circulam por sincronizadores entre aparelhos com versões diferentes.

## Alternativas consideradas

### A — Ler tudo da 1.x; escrever 2.x com proteção forward + backup

Migração segura; novos nomes de arquivo para o que a 1.x sobrescreve.

### B — Escrever formato compatível com 1.x durante toda a 2.0

Convivência total; trava o novo modelo de identidade.

### C — Adiar

Mantém risco de perda.

## Decisão

Adotar **A** (decisão do proprietário). Regras: (1) a 2.x lê mapa v1/v2/v4, notas sem `id`, journal/history/trash/compositions v1, páginas v1 e `tema.json` v1; (2) o vault declara `formatVersion` em `.urbe/vault.json`; (3) nenhum leitor sobrescreve, descarta ou apaga um arquivo `.urbe/*` de versão desconhecida/maior — preserva, avisa e opera em modo seguro naquele artefato (REQ-035); (4) toda migração faz backup restaurável em `.urbe/backup/` e é idempotente (REQ-038); (5) **[proposta, OD-11]** para os artefatos que a 1.x sobrescreve, a 2.x grava `history.v2.json`, `trash.v2.json`, `compositions.v2.json`, `journal.v2.json`, mantendo o v1 intacto durante a beta; `mapa.json` permanece em `v:4` legível pela 1.x, com extensões nos novos arquivos.

## Consequências positivas

- nenhuma perda silenciosa em vault misto 1.x/2.x
- migrações verificáveis por fixtures

## Consequências negativas / trade-offs

- arquivos paralelos aumentam o vault e a complexidade
- o v1 divergirá do v2 se a 1.x continuar editando

## Migração

Detecção na abertura: sem `vault.json` → vault 1.x → backup → migração → `vault.json`. Rollback por restauração do backup.

## Validação

fixtures de vault (REQ-037), testes forward (`futuro.json` intacto), teste de idempotência, gate G1.

## Relações

- documentos afetados: DATA-CATALOG §6–7, SPEC §5, tutorial, CHANGELOG
