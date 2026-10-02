# AGENTS.md — Contrato para agentes de IA no repositório Urbe

Este arquivo vale para qualquer agente (Claude Code, Codex, Copilot etc.) que trabalhe neste repositório. Ele complementa `CONTRIBUTING.md` e não substitui os documentos normativos em `docs/v2/`.

## 1. Fontes de verdade (em ordem)
1. `docs/v2/REQUIREMENTS.md` — Requirement Ledger (IDs REQ estáveis).
2. `docs/v2/SPEC.md` — especificação normativa da 2.0.
3. `docs/v2/ROADMAP.md` — itens executáveis (`RM-Fn-nn`), com dependências, testes e gates.
4. `docs/v2/TRACEABILITY.md` — gerado; **não editar à mão** (`node tools/gen-traceability.mjs`).
5. `docs/v2/adr/` — decisões arquiteturais; `docs/v2/discovery/` — evidência do estado 1.x.
6. `AGENTSCHAT.md` — log de coordenação e handoff (leia antes de começar, escreva ao terminar).

PRs, commits e CHANGELOG registram trabalho; **não** substituem requisitos, SPEC ou ADR.

## 2. Processo
`IDEIA → DESCOBERTA → DEFINIÇÃO → DECISÕES → REQUISITOS → ARQUITETURA → RISCOS → ESTRATÉGIA → SPEC → ROADMAP → AUDITORIA → EXECUÇÃO`.
Na execução, cada item segue: `IMPLEMENTAR → TESTAR → VERIFICAR → CORRIGIR → DOCUMENTAR → AUDITAR → VALIDAR GATE → AVANÇAR`.

## 3. Regras invioláveis
- Não implemente nada sem um item do ROADMAP (ou bug crítico da 1.x, registrado). Cite o `RM-id` e o REQ no commit/PR.
- **Nunca remova ou enfraqueça um REQ.** Mudar de estado exige justificativa e fica no ledger; requisitos novos são acrescentados.
- Decisão estrutural (boundary, formato de dados, segurança, release, plataforma) exige ADR. Decisão que depende do proprietário: apresente contexto, alternativas, consequências, recomendação e pergunta objetiva — **não decida por conveniência**.
- Separe sempre **FATO OBSERVADO** (com `caminho:linha`), **INFERÊNCIA**, **DECISÃO PROPOSTA** e **DECISÃO CONSOLIDADA**. Não afirme estado do repositório sem tê-lo verificado.
- **Uma autoridade por vez:** extrair código de `app.js` = provar a substituta com teste de produção e **apagar** a implementação antiga no mesmo item. Nada de nova camada de versão, `V21_VERSION` extra, wrapper histórico ou "TODO marcado como feito" (`tools/check-debt.mjs`, quando existir, bloqueia).
- **Dados do usuário são invariante:** local-first/offline-first; nenhum leitor sobrescreve/descarta arquivo de versão desconhecida; migração só com backup restaurável e idempotente (REQ-007/035/038).
- Não altere formato persistido sem linha em `discovery/DATA-CATALOG.md`, política de migração, fixture e teste.
- Não publique versão por efeito colateral: release só por tag/dispatch aprovado (REQ-066).
- **Integração (ADD-0012 e ADR-0015 do Ecosystem):** não integre à mão; quem integra é o integrador automático do Ecosystem. Mudança **rotineira** do Urbe (bug, feature de item autorizado do ROADMAP, refatoração interna, testes, UI, documentação), com `npm run check` e o CI verdes no estado combinado, entra **sozinha**. Mudança **crítica** espera a autorização do proprietário. São críticas as que tocam as zonas críticas do Urbe em `docs/governance/integration-policy.json` do Ecosystem e tudo o que a §5 abaixo lista: formato persistido, migração e backup (`src/persistence/**`, `DATA-CATALOG.md`), fronteira de confiança do desktop, plugins e credenciais de IA, assinatura, identidade e canal de publicação, licença, e mudança de ADR consolidado. Se a sua mudança é crítica fora dessas zonas (ex.: formato persistido dentro do `app.js`), declare-a crítica no handoff do Ecosystem (`criticality`). Nenhum agente põe a label `integrar`.
- Segredos: nunca commitar chaves, tokens ou dados pessoais; chaves de IA nunca vão ao vault, export ou logs.
- Item só vira `[x]` com a Definition of Done (SPEC §10.1) verificada. Se faltar validação humana: `[?]`. Se bloqueado: `[!]` com o bloqueio exato. Nunca comprimir vários requisitos em um item genérico.
- Descobertas novas **aumentam** o ledger/roadmap; nunca o encolhem.

## 4. Fluxo de trabalho
1. Leia `AGENTSCHAT.md` (últimas entradas) e o item do ROADMAP; confirme dependências `[x]`.
2. Crie branch temporária (`feat/REQ-xxx-…`, `fix/…`, `refactor/…`, `docs/…`, `chore/…`) ou use a branch designada pela sessão.
3. Implemente o mínimo do item; rode `npm test` (e os checks aplicáveis: `node tools/check-traceability.mjs`, `node tools/gen-traceability.mjs --check`, `node tools/build-tutorial.mjs --check`).
4. Atualize documentação afetada (SPEC/contratos/tutorial/CHANGELOG) e regenere `TRACEABILITY.md` se ROADMAP/SPEC/REQ mudarem.
5. Commits pequenos e claros; PR com template (problema, REQ/RM-id, o que muda e não muda, impacto em dados/plataformas/performance/segurança, testes, dívida criada/removida).
6. Acrescente entrada em `AGENTSCHAT.md` (formato abaixo) e pare no limite do item.

## 5. O que sempre confirmar com o proprietário
Budgets absolutos de performance (após o baseline); qualquer quebra de compatibilidade de dados; remoção de funcionalidade da 1.x; mudança de qualquer decisão consolidada (ADRs aceitos); publicação fora do mecanismo aprovado (REQ-066). Isto é o que é **crítico** no Urbe; o resto do trabalho dentro do ROADMAP é rotina e não precisa do proprietário (ADD-0012 do Ecosystem).

## 6. Limites
- Fora do escopo da 2.0: reescrita geral, bundler/ES modules (ADR-0001), launcher multi-app, isolamento de plugins, i18n, macOS/Linux, sync/nuvem próprios (`docs/v2/SPEC.md` §13).
- Sem framework/bundler por preferência; sem mover diretórios sem REQ que exija.

## 7. Formato de entrada no `AGENTSCHAT.md`
```
### AAAA-MM-DD — <agente> — <RM-id/REQ ou "descoberta">
- Estado: ...
- Feito: ...
- Decisões (com fonte): ...
- Pendências / bloqueios: ...
- Próximos passos: ...
```
