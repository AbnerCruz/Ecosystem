# AGENTSCHAT.md — Log de coordenação entre agentes e proprietário

> Append-only. Mais recente no topo. Leia antes de trabalhar; escreva ao terminar (formato em `AGENTS.md` §7).
> Não guarde segredos aqui. Decisões normativas devem também constar em REQUIREMENTS/ADR.

---

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
