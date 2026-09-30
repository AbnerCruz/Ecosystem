# Decisões da descoberta e gate da SPEC

> Descoberta da issue #33. Estados: **CONSOLIDADA** (decidida pelo proprietário nesta descoberta), **PROPOSTA** (recomendação técnica adotada na SPEC como padrão, pendente de confirmação/veto do proprietário), **ADIADA** (com gate explícito e justificativa).
> Decisões consolidadas em 2026-09-30 pelo proprietário (respostas registradas em `AGENTSCHAT.md`).

## 1. Decisões consolidadas

### OD-01 — Módulos e build — CONSOLIDADA → ADR-0001
- Contexto: 60 scripts síncronos com ordem implícita (AUD-003).
- Alternativas: (A) sem build + registro explícito de módulos + validação; (B) ES modules nativos sem bundler; (C) bundler; (D) adiar.
- **Decisão (proprietário):** por enquanto sem build; launcher que centralize os aplicativos do proprietário é ideia futura, não entra na 2.0-beta; manter simples. → alternativa A.
- Consequências: REQ-025 (registro/validação), REQ-033 e REQ-034 ADIADOS.

### OD-02 — Confiança de plugins — CONSOLIDADA → ADR-0002
- Alternativas: (A) full-trust aprovado com UX honesta; (B) isolamento por capabilities; (C) híbrido.
- **Decisão (proprietário):** A.
- Consequências: REQ-051 e REQ-032 implementar; REQ-052 ADIADO.

### OD-03 — Licença/distribuição — CONSOLIDADA (direção) → ADR-0003
- Alternativas: adiar com bloqueio; MIT; fonte-disponível/proprietária; GPL/AGPL.
- **Decisão (proprietário):** fonte-disponível/proprietária.
- **Pendência residual (bloqueia distribuição pública, não o desenvolvimento):** texto final de `LICENSE` (permissões de uso, redistribuição, contribuições/CLA). Pergunta: *"Quais permissões o `LICENSE` deve conceder (uso pessoal, modificação privada, redistribuição, contribuições) e em nome de quem?"* → REQ-080, gate G4.

### OD-04 — Compatibilidade de escrita 1.x — CONSOLIDADA → ADR-0004
- Alternativas: ler tudo + escrever 2.x com proteção forward; escrever compatível com 1.x durante toda a 2.0; adiar.
- **Decisão (proprietário):** ler tudo da 1.x; escrever 2.x com proteção forward.
- Detalhe [P]: OD-11.

## 2. Propostas recomendadas (adotadas na SPEC; sujeitas a veto)

### OD-05 — Mecanismo de release → ADR-0005
- Contexto: push em `main` com bump publica Release (AUD-006).
- Alternativas: (A) tag `v*` protegida + `workflow_dispatch`, com `needs` nos testes; (B) branch `release/*`; (C) manter acoplamento com aprovação manual de ambiente.
- Consequências de A: um passo a mais para publicar, ganho de separação, rollback claro; compatível com `electron-updater` e o updater Android.
- **Recomendação:** A. **Pergunta:** *"Confirma que publicações passam a sair só por tag `v<versão>` (ou execução manual), e nunca por merge em `main`?"* → REQ-066, REQ-081.

### OD-06 — Vault único vs múltiplos no app instalado; pasta no Android → ADIADA
- Contexto: app instalado guarda um vault (`bridge.js:117-123`); web suporta várias cidades migradas para `Cidades/`; Android sem seletor de pasta (`app.js:3280`).
- Alternativas: manter vault único; múltiplos vaults; SAF no Android.
- **Decisão [C]:** manter vault único e política de `Cidades/` (REQ-045). SAF/MANAGE_EXTERNAL_STORAGE → REQ-077 ADIADO (Play Store não é alvo). Gate de reavaliação: mudança de canal de distribuição.

### OD-07 — Estrutura física de pastas → SPEC §3
- Contexto: 71 arquivos em `src/`, subsistemas já separados.
- Alternativas: reorganizar em massa; convergir por subsistema e mover só com o REQ que exige.
- **Recomendação/adotada:** sem movimentos em massa; novos diretórios previstos: `src/persistence/adapters/`, `vendor/jszip/`, `tests/fixtures/vaults/`, `tools/`. **Pergunta ao proprietário: nenhuma pendente; veto possível.**

### OD-08 — Chaves de IA → ADR-0007
- Alternativas: manter texto puro + mitigações; criptografia nativa (safeStorage/Keystore) por plataforma; senha-mestra.
- **Recomendação/adotada:** mitigações agora (REQ-053), cripto nativa ADIADA (REQ-054). Gate de reavaliação: existência de E2E nativo (REQ-061/062).

### OD-09 — Budgets numéricos de performance → ADIADA com gate
- Alternativas: fixar agora; medir baseline e fixar depois.
- **Decisão [C]:** medir primeiro (REQ-070); budgets relativos provisórios; absolutos fixados pelo proprietário após o baseline (REQ-071). Orçamentos iniciais sugeridos em PERFORMANCE §5.

### OD-10 — Identidade: sidecar vs frontmatter → ADR-0006
- Alternativas: (A) ID no frontmatter de cada nota (portável; **altera arquivos do usuário**, contradiz `app.js:1902-1907`); (B) sidecar `.urbe/identity.json` (path↔ID + fingerprint de conteúdo) com reconciliação em `syncFromDisk`; (C) manter status quo.
- **Recomendação/adotada:** B; frontmatter opt-in fica **ADIADO**. **Pergunta:** *"Aceita que a Urbe nunca escreva IDs dentro das notas por padrão?"*

### OD-11 — Formato de proteção forward por arquivo → ADR-0004
- Contexto: 1.8.2 sobrescreve `history/trash/compositions/journal` com versão desconhecida (R-1), e não pode ser corrigida.
- Alternativas: (A) mesmos nomes, versão maior (a 1.8.2 destrói); (B) novos nomes `*.v2.json` durante a transição, com v1 preservado; (C) exigir atualização de todos os aparelhos.
- **Recomendação/adotada:** B para artefatos que a 1.x sobrescreve (`history`, `trash`, `compositions`, `journal`); `mapa.json` mantém o nome e `v` 4 legível pela 1.x, com extensões em `.urbe/vault.json` e `identity.json`. **Pergunta:** *"Aceita que vaults usados em 2.x mantenham arquivos `.v2` paralelos aos v1 durante a beta?"*

## 3. Conflitos de requisitos
Nenhum REQ conflita. Tensões resolvidas:
- REQ-007 (compatibilidade) × REQ-013/041 (IDs): leitura por path preservada, IDs novos por adição (REQ-041).
- REQ-020 (decisão de plugins) × REQ-052: REQ-020 satisfeito por full-trust + REQ-051; REQ-052 adiado.
- REQ-003 (não reescrever) × REQ-029 (eliminar cadeias): substituição incremental, uma autoridade por item.
- REQ-025 (sem build) × REQ-034 (ES modules): REQ-034 ADIADO.

## 4. Riscos conhecidos que atravessam a SPEC
| Risco | Mitigação |
|---|---|
| Religar renderer canônico altera comportamento visual da cidade | teste de paridade + `[?]` até validação manual |
| Proteção forward exige arquivos paralelos `.v2` (OD-11) | ADR-0004, fixtures, migração idempotente |
| 1.8.2 já em campo continua vulnerável a R-1 | comunicação (REQ-082); release 1.8.x de aviso opcional [P, fora desta missão] |
| Escrita dupla do mapa sem guarda de concorrência | REQ-040 antes de REQ-041/042 |
| Falta de E2E antes de refatorar | G0/G1 bloqueiam extrações até haver testes de produção |
| Licença sem texto final | gate G4 bloqueado até OD-03 |
| Volume do ROADMAP (várias fases) | dependências explícitas e gates por fase |

## 5. Gate da descoberta (checklist para liberar a SPEC)
| Item do gate | Estado | Evidência |
|---|---|---|
| Arquitetura real mapeada | ✅ | `ARCHITECTURE-MAP.md` |
| Legacy inventariado (consumidores/condições) | ✅ | `LEGACY-MAP.md` |
| Dados e identidade catalogados | ✅ | `DATA-CATALOG.md` |
| Matriz de testes | ✅ | `TEST-MATRIX.md` |
| Performance (baseline definida, budgets adiados com gate) | ✅ | `PERFORMANCE.md` |
| Threat model | ✅ | `THREAT-MODEL.md` |
| Plataformas, distribuição, licença | ✅ (texto de licença pendente → gate G4) | `PLATFORMS.md` |
| Produto/UX e critérios de sucesso | ✅ (propostos; aprovados na aprovação da SPEC) | `PRODUCT-UX.md` |
| Decisões abertas resolvidas ou adiadas | ✅ OD-01..04 consolidadas; OD-05,10,11 propostas; OD-06,09 adiadas com gate | este documento |
| Requirement Ledger atualizado | ✅ REQ-025..088, nenhum removido | `REQUIREMENTS.md` |
| Conflitos resolvidos/adiados | ✅ §3 | este documento |
| Riscos conhecidos | ✅ §4 | este documento |

**Resultado:** gate liberado para produzir `SPEC.md`, com três propostas (OD-05, OD-10, OD-11) e um texto (OD-03) sujeitos à confirmação do proprietário na aprovação da SPEC. Nenhuma delas impede escrever a SPEC; três tornam-se gates de itens específicos do ROADMAP.
