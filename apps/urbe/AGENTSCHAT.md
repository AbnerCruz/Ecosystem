# AGENTSCHAT.md — Log de coordenação entre agentes e proprietário

> Append-only. Mais recente no topo. Leia antes de trabalhar; escreva ao terminar (formato em `AGENTS.md` §7).
> Não guarde segredos aqui. Decisões normativas devem também constar em REQUIREMENTS/ADR.

---

### 2026-10-02 — Claude — governança: integração rotineira automática (ADD-0012 do Ecosystem)
- **Estado:** a regra de merge do Urbe em `AGENTS.md` §3 mudou (Ecosystem PR #40, integrado pelo proprietário): trabalho **rotineiro** do Urbe entra na `main` sozinho pelo integrador do Ecosystem, com `npm run check` e o CI verdes no estado combinado; **crítico** (dados, fronteira de confiança do desktop/plugins/credenciais, assinatura/identidade/canal, licença, ADR consolidado) espera a autorização do proprietário.
- **Feito:** datas corrigidas (o encerramento do RM-F2-03 foi em 2026-10-01, não 10-02) neste log e em `docs/v2/ROADMAP.md`. Esta mudança é o canário rotineiro: não toca zona crítica.
- **Decisões (com fonte):** ADD-0012 e ADR-0015 do Ecosystem; zonas críticas em `docs/governance/integration-policy.json` do Ecosystem.
- **Pendências / bloqueios:** nenhuma.
- **Próximos passos:** U-R1 segue com RM-F2-01 e RM-F2-02, em tarefas próprias.

### 2026-10-01 — Claude — RM-F2-03 (JSZip em `vendor/jszip`), primeiro item de U-R1 no monorepo Ecosystem
- **Estado:** RM-F2-03 `[?]`: implementado e verificado automaticamente; falta validar import/export ZIP no navegador (`tests/e2e/zip.e2e.mjs`, não executado: sem Playwright instalado nesta sessão). O Urbe agora é desenvolvido em `apps/urbe/` do Ecosystem (ADR-0009 do Ecosystem; refatoração interna R).
- **Feito:** `src/legacy/bootstrap.js` movido (git mv, conteúdo idêntico) para `vendor/jszip/jszip.min.js`; `vendor/jszip/LICENSE`; `src/modules.json`, `index.html`, `sw.js`, `tools/gen-modules.mjs` e `THIRD-PARTY-NOTICES.md` atualizados; `src/legacy/` removido. Nenhum formato de dados tocado.
- **Verificação:** `node tools/check-modules.mjs` OK (73 módulos); `npm run check` verde (56/56 arquivos de teste).
- **Pendências:** texto de `vendor/jszip/LICENSE` escrito a partir do cabeçalho do arquivo (MIT, opção já adotada em THIRD-PARTY-NOTICES) — conferir com a licença upstream; validação humana do ZIP; origem `AbnerCruz/Urbe` precisa da sincronização (`sync-from-ecosystem.yml`) depois do merge.
- **Próximos passos:** RM-F2-01 (HOTSPOTS) e RM-F2-02 (harness do monólito); F1 ainda tem RM-F1-20…26 abertos.
- **Integração (2026-10-01, agente integrador):** branch `ccr-5adec8cc-oy464b`, PR #35 do Ecosystem (reconciliado com a `main` depois da Fase 3; sem conflito). `tests/e2e/zip.e2e.mjs` rodou em Chromium real e passou (1/1). RM-F2-03 segue `[?]` até o merge, que **só acontece com pedido explícito do proprietário**; depois, disparar `sync-from-ecosystem.yml` em `AbnerCruz/Urbe`.
- **Encerramento (2026-10-01):** o proprietário pediu o merge ("Pode fazer o merge e aprovar tudo"; ADD-0011 do Ecosystem). RM-F2-03 `[x]` no mesmo PR que o integra (estado combinado reconciliado com a `main` e verde). Próximos itens de U-R1: RM-F2-01 e RM-F2-02, em tarefas próprias.

### 2026-09-30 — Claude — RM-F1-10…19 (persistência, identidade, mundo, export, multi-cidade)
- **Estado:** F0 quase completa e F1 até RM-F1-19 `[x]`. PR #36 (F0 + RM-F1-01…13) mergeado na `main` (`3351aaa`) com CI verde, como autorizado ("se estiver validado e funcionando é pra entrar na main"). RM-F1-14…19 estão na branch `claude/new-session-eh5rwa`, com PR próprio.
- **Feito:**
  - RM-F1-10/11/12: adaptadores `idb`/`fsa`/`router` extraídos de `app.js`, com suíte de contrato em Node e em IDB/OPFS reais.
  - RM-F1-13: escritor único do `mapa.json` (`metadataProvider`) e validação de `mapa.v`.
  - RM-F1-14: IDs `reg_`/`ast_` e `parentId`.
  - RM-F1-15: `.urbe/identity.json` e reconciliação de rename externo, com o app fechado e aberto.
  - RM-F1-16: GC de órfãos com retenção e registro.
  - RM-F1-17: reorganização da cidade sempre com backup e desfazer.
  - RM-F1-18: `urbe-export.json` com hashes, estado local sem segredos e import de `.zip` pelo Explorer (bug da 1.8.2).
  - RM-F1-19: multi-cidade idempotente, com fusão do mapa pelo `WorkspacePersistence`.
  - Código morto apagado do `app.js`: ~450 linhas; teto de dívida rebaixado a cada item.
- **Decisões (com fonte):**
  - Casa só guarda a posição se a nota continua no mesmo bairro (DATA-CATALOG §4, RM-F1-15).
  - Lixeira só expira com `trashDays` explícito (RM-F1-16).
  - Export de formato futuro é recusado (DATA-CATALOG §9).
  - `nomeSeguro` virou `UrbeArtifacts.safeName`, fonte única de nome de arquivo.
- **Pendências / bloqueios:**
  - RM-F0-12: baseline L de performance não gerado.
  - RM-F0-15: SHAs das actions exigem fonte confiável e aprovação.
  - RM-F0-16: exclusão das 31 branches remotas foi negada pelo classificador e precisa de autorização explícita do proprietário.
  - O hardening de Electron/Android foi testado só com simulação.
- **Próximos passos:** RM-F1-20…26, depois F2 (monólito/legacy), F3, F4, F5, F6 e F7.

### 2026-09-30 — Claude (Sonnet 5.5) — aprovação do proprietário e integração na `main`
- **Estado:** SPEC e ROADMAP **aprovados**; OD-05, OD-08, OD-10, OD-11 **aprovadas**; licença **definida**. Implementação da 2.0 continua NÃO iniciada.
- **Decisões do proprietário (mensagem de 2026-09-30):** "Todos os direitos reservados, tudo meu" (OD-03 → ADR-0003); "Se estiver validado e funcionando é pra entrar na main sim" (autoriza a integração desta branch na `main` após validação: CI verde e sem alteração de runtime); "De resto aprova tudo" (SPEC, ROADMAP, ADR-0005/0006/0007, OD-05/08/10/11).
- **Feito:** ADRs 0003–0007 → Accepted; `RM-F4-12` desbloqueado (`[ ]`); documentos atualizados; TRACEABILITY regenerada.
- **Pendências:** budgets absolutos de performance (RM-F5-02, após o baseline); OD-06 e OD-09 seguem adiadas com gate.
- **Próximos passos:** iniciar F0 (RM-F0-03…18) em sessão própria; RM-F0-16 trata as branches `claude/*`.

### 2026-09-30 — Claude (Sonnet 5.5) — descoberta da issue #33 e planejamento integral da 2.0
- **Estado:** descoberta concluída; SPEC, ROADMAP, TRACEABILITY e ADRs 0001–0007 produzidos. **Implementação da 2.0 NÃO iniciada.** Runtime 1.8.2-beta intocado.
- **Feito:** merge fast-forward de `origin/chore/urbe-2-foundation` (PR #34 não mergeado; fundação preservada) na branch `claude/new-session-eh5rwa`; `docs/v2/discovery/*` (9 documentos), `REQUIREMENTS.md` (REQ-025..088 acrescentados, nenhum removido), `adr/0001..0007`, `SPEC.md`, `ROADMAP.md` (118 itens, F0–F6), `TRACEABILITY.md` (gerado), `AUDIT-COVERAGE.md`, `tools/check-traceability.mjs`, `tools/gen-traceability.mjs`, `tests/traceability.mjs`, `AGENTS.md`, este arquivo.
- **Decisões do proprietário (respostas em 2026-09-30):**
  1. Build: por enquanto **sem build**; launcher que centralize os aplicativos do proprietário é ideia futura, fora da 2.0-beta (ADR-0001; REQ-033/034 ADIADOS).
  2. Plugins: **full-trust aprovado com UX honesta** (ADR-0002; REQ-052 ADIADO).
  3. Licença: **fonte-disponível/proprietária** (ADR-0003); texto final da `LICENSE` pendente.
  4. Compatibilidade: **ler tudo da 1.x; escrever 2.x com proteção forward** (ADR-0004).
  5. Pedido: incluir `AGENTS.md` e `AGENTSCHAT.md` (REQ-088).
- **Propostas aguardando confirmação (adotadas na SPEC como padrão, sujeitas a veto):** OD-05 release por tag (ADR-0005); OD-10 identidade em sidecar (ADR-0006); OD-11 arquivos `*.v2.json` paralelos (ADR-0004); OD-08 mitigação de chaves de IA + CSP (ADR-0007).
- **Pendências / bloqueios:** texto da `LICENSE` (OD-03) bloqueia o gate G4 (RM-F4-12 `[!]`); budgets absolutos de performance dependem do baseline (RM-F5-02); atualização do checklist da issue #33 e criação de PR dependem do proprietário/ferramentas GitHub.
- **Próximos passos:** proprietário revisa e aprova SPEC/ROADMAP (responder OD-05/10/11/03); depois iniciar F0 (RM-F0-03…18). Nada da 2.0 é implementado antes disso.
- **Branches:** 31 branches `claude/*` remotas aguardam verificação (RM-F0-16); esta sessão não removeu nenhuma.
