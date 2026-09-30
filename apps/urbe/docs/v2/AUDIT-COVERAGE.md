# Auditoria de cobertura — descoberta, SPEC e ROADMAP da Urbe 2.0

> Data: 2026-09-30 · Escopo: issue #33 · Executada sobre `REQUIREMENTS.md` (88 REQ), `SPEC.md`, `ROADMAP.md` (118 itens), `TRACEABILITY.md`, `discovery/*`, `adr/*`.
> Verificação mecânica: `node tools/check-traceability.mjs` (REQ IMPLEMENTAR ∈ SPEC ∧ ROADMAP ∧ TRACEABILITY; dependências, gates e campos dos itens) e `node tools/gen-traceability.mjs --check`. Verificação semântica: as tabelas abaixo (revisão manual).

## 1. Decisões × requisitos
| Decisão | Estado | REQ que a materializam | ADR | Item(ns) |
|---|---|---|---|---|
| OD-01 sem build; launcher futuro | CONSOLIDADA | REQ-025 (impl.), REQ-033/034 (ADIADOS) | 0001 | RM-F0-07/08/09, RM-F5-05 |
| OD-02 plugins full-trust | CONSOLIDADA | REQ-020, REQ-051, REQ-032; REQ-052 ADIADO | 0002 | RM-F3-04/05/06, RM-F2-17 |
| OD-03 licença: todos os direitos reservados | CONSOLIDADA | REQ-017, REQ-080 | 0003 | RM-F4-11/12/13 |
| OD-04 compat 1.x, escrita 2.x + forward | CONSOLIDADA | REQ-007, 023, 035–038, 082 | 0004 | RM-F1-01…07, RM-F4-14 |
| OD-05 release por tag | CONSOLIDADA | REQ-006, 019, 065, 066, 081 | 0005 | RM-F0-06/14/15, RM-F4-10 |
| OD-06 vault único / Android | ADIADA | REQ-045; REQ-077 ADIADO | — | RM-F1-19 |
| OD-07 estrutura de pastas | CONSOLIDADA (aprovada) | SPEC §4 (sem REQ próprio; regida por REQ-003/012) | — | RM-F0-13 |
| OD-08 chaves de IA | CONSOLIDADA | REQ-021, 053, 060; REQ-054 ADIADO | 0007 | RM-F3-07/08/17 |
| OD-09 budgets | ADIADA com gate | REQ-009, 070, 071 | — | RM-F0-10/11/12, RM-F5-02 |
| OD-10 identidade em sidecar | CONSOLIDADA | REQ-013, 014, 039, 041, 042 | 0006 | RM-F1-08/09/14/15/16 |
| OD-11 arquivos `*.v2.json` | CONSOLIDADA | REQ-035 | 0004 | RM-F1-05 |
Resultado: nenhuma decisão sem REQ; nenhuma decisão consolidada contradita por REQ.

## 2. Achados do AUDIT-1X × requisitos
| Achado | REQ | Achado | REQ |
|---|---|---|---|
| AUD-001 centro histórico | 003, 026, 027, 029 | AUD-009 E2E não formalizado | 016, 061, 062 |
| AUD-002 camadas por versão | 012, 029 | AUD-010 tipos de artefato | 014, 039 |
| AUD-003 ordem de scripts | 015, 025 | AUD-011 metadados por path | 013, 041, 042 |
| AUD-004 adapters legacy | 030, 032 | AUD-012 plugins | 020, 051, 052 |
| AUD-005 versão duplicada | 019, 065 | AUD-013 threat model | 010, 050–060 |
| AUD-006 release acoplado | 006, 066, 081 | AUD-014 outros hotspots | 022 |
| AUD-007 planejamento em PRs | 002, 018, 067, 088 | AUD-015 compat do vault | 023, 035–038, 082 |
| AUD-008 branches | 018, 067 | AUD-016 licença | 017, 080 |

## 3. Riscos de dados 1.x (DATA-CATALOG R-1…R-15) × migração
| Risco | REQ | Item(ns) do ROADMAP | Teste/fixture |
|---|---|---|---|
| R-1 versão desconhecida destrói dados | 035, 036 | RM-F1-04/05/06 | `futuro-desconhecido` |
| R-2 sem ID no arquivo | 042 | RM-F1-15/16 | rename externo, cópia sem `.urbe/` |
| R-3 espaciais por path | 041 (013) | RM-F1-14 | mapa v4 legível pela 1.8.2 |
| R-4 `mundo` reposiciona a cidade | 043 | RM-F1-17 | `v1-mundo-antigo` |
| R-5 escrita dupla do mapa | 040, 028 | RM-F1-12/13 | mapas v1/v2/v4 |
| R-6 referências por nome/path | 041 | RM-F1-14 | fixtures de personalização |
| R-7 plugins/API PT | 032, 051 | RM-F2-17, RM-F3-04/05 | `customize.mjs` |
| R-8 chaves IA em texto puro | 044, 053 | RM-F1-18, RM-F3-07 | testes de export |
| R-9 estado local fora do backup | 044, 047 | RM-F1-18/21 | round-trip por hash |
| R-10 IDB é dado só do navegador | 046 | RM-F1-20 | export do IDB |
| R-11 extensões divergentes | 039 | RM-F1-08 | `artifacts.mjs` |
| R-12 `aiLocal` sem consumidor | 048 | RM-F1-22 | fixtures |
| R-13 `Cidades/` a cada boot | 045 | RM-F1-19 | 3 boots idempotentes |
| R-14 páginas descartam chaves | 049 | RM-F1-23 | `pages.mjs` ampliado |
| R-15 escrita não atômica (web) | 046 | RM-F1-20 | falha simulada |

## 4. Arquitetura atual × alvo (LEGACY-MAP)
| Legacy | Substituta / ação | REQ | Item(ns) |
|---|---|---|---|
| L0 JSZip em `legacy/` | `vendor/jszip` | 031 | RM-F2-03 |
| L1, L2 `legacy.runtime/documents` | serviços canônicos, `editor.session.record` | 030 | RM-F2-15 |
| L3, L4 espelho building↔doc, wrappers | `world.projection` | 030, 029 | RM-F2-12 |
| L5 `FS/Disco/DBK` | adapters de persistência | 028 | RM-F1-10/11/12 |
| L6 render/interação em `app.js` | `aquarium.renderer/touch/roads` | 026, 029 | RM-F2-05…08 |
| L7, L8 hosts, wrapper `document.open` | interfaces de módulos; roteamento por artefato | 030, 032 | RM-F1-09, RM-F2-16 |
| L9 migrações 1.x | permanecem, cobertas por fixtures/backup | 035–038, 043, 045 | RM-F1-02/07/17/19 |
| L10, L11 resíduos e `window.URBE` | remoção/diagnóstico | 030 | RM-F2-04 |
| Cadeias de sobrescrita | 1 camada | 029 | RM-F2-13/14 |
| Editor em `app.js` | `src/editor/*` | 027 | RM-F2-09/10/11 |
| Testes por texto | testes de comportamento | 064 | RM-F2-02/18 |
| Ordem de scripts | `modules.json` validado | 025, 015 | RM-F0-07/08/09 |
| Aquário canônico não ligado | religar | 026 | RM-F2-05…08 |
Estado alvo final: `app.js` só composição (RM-F2-19).

## 5. Ameaças × requisitos × itens (THREAT-MODEL §8)
| Ameaça | REQ | Itens |
|---|---|---|
| T-1 sem CSP | 050 | RM-F3-02/03 |
| T-Plugin | 051, 032, 020 | RM-F3-04/05/06 |
| T-IA | 053, 060, 021 | RM-F3-07/08/17 |
| T-Electron | 055, 062 | RM-F3-09/10/19 |
| T-Android | 056, 062 | RM-F3-11/12/20 |
| T-HTML | 057 | RM-F3-13/14 |
| T-Import | 058 | RM-F3-15/16 |
| T-Vault | 035 | RM-F1-05/06 |
| T-Links | 010 | RM-F3-18 |
| T-Updater | 059 ADIADO (documentado em REQ-082) | RM-F4-14 |

## 6. Lacunas de teste (TEST-MATRIX §3) × requisitos
| Lacuna | REQ | Itens |
|---|---|---|
| `main.js`/`preload.js` sem teste | 062, 055 | RM-F3-19 |
| Java/Android sem teste | 062, 056 | RM-F3-20 |
| Sem E2E/browser/perf | 061, 070 | RM-F4-04…07, RM-F0-11 |
| SW só por regex | 062 | RM-F3-20 |
| IDB/FSA sem teste | 062, 046 | RM-F1-10/11/20 |
| ZIP zip-slip/bomb | 058 | RM-F3-15/16 |
| Bairros 1.8.1/1.8.2 sem cobertura | 064 | RM-F2-02 (harness) + RM-F4-06 |
| CSP inexistente | 050 | RM-F3-03 |
| `migrarAntiga`, mundo, mapas v1/v2 | 037, 035 | RM-F1-01/02 |
| `legacy.*` sem teste | 030, 064 | RM-F2-15 |
| `npm test` só POSIX | 063 | RM-F0-05 |
| Release não depende de testes | 066 | RM-F0-14 |
| Sem smoke de instalador/APK | 069 | RM-F4-09 |

## 7. ROADMAP × gates
Cada item tem exatamente um gate G0–G6 (validado por `check-traceability`). Ordem de dependências validada (nenhuma dependência inexistente). Nenhum item bloqueado (a licença foi decidida). Itens concluídos nesta missão: `RM-F0-01`, `RM-F0-02` (`[x]`, com verificador executado). Todos os demais: `[ ]`.

## 8. Lacunas encontradas na auditoria e correções
1. A verificação mecânica inicial acusou ausência de `AGENTS.md`/`AGENTSCHAT.md` (REQ-088) → criados e referenciados em CONTRIBUTING e `docs/v2/README.md`.
2. SPEC §12 listava adapters de persistência em G2 enquanto o ROADMAP os põe em F1/G1 (dependência de `REQ-040`) → G1 e G2 da SPEC alinhados ao ROADMAP.
3. `REQ-007` aparecia em duas cláusulas → mantida uma cláusula primária (§1.2) e a outra reduzida a referência.
4. Itens de extração de hotspots (REQ-022) não foram criados: o REQ exige apenas **avaliação**; extrações resultantes entram como novos REQ (o ledger só cresce).
5. Aprovações do proprietário (2026-09-30): licença, OD-05/08/10/11, SPEC e ROADMAP. Resta apenas a fixação dos budgets absolutos após o baseline (RM-F5-02).

## 9. Conclusão
Cobertura mecânica: todo REQ IMPLEMENTAR possui cláusula na SPEC, ao menos um item no ROADMAP e linha na TRACEABILITY. Cobertura semântica: nenhuma decisão, achado de auditoria, risco de dados, adapter legacy ou lacuna de teste conhecida ficou sem REQ e item. O planejamento está **aprovado pelo proprietário (2026-09-30)**; a implementação da 2.0 **não foi iniciada**.
