# Urbe 2.0 — ROADMAP

> Derivado integralmente de `SPEC.md`. Cada REQ em estado IMPLEMENTAR chega a itens concretos e verificáveis; a matriz completa REQ → SPEC → fase → item → teste/gate está em `TRACEABILITY.md` (gerada por `tools/gen-traceability.mjs` a partir deste arquivo e da SPEC).
> **Estados:** `[ ]` não iniciado · `[~]` em andamento · `[?]` implementado, aguardando validação · `[x]` concluído (DoD atendido, SPEC §10.1) · `[!]` bloqueado.
> Só `[x]` é concluído. Este roadmap **não autoriza** a execução até ser aprovado pelo proprietário; a execução segue: IMPLEMENTAR → TESTAR → VERIFICAR → CORRIGIR → DOCUMENTAR → AUDITAR → VALIDAR GATE → AVANÇAR.
> Formato de item (lido por ferramentas): título `### RM-Fn-nn — …`; linhas `- **Estado/REQ/SPEC/Fase/Depende/Implementação/Integração/Testes/Documentação/Aceite/Gate:**`.

## Fases e gates
| Fase | Escopo | Gate |
|---|---|---|
| F0 | Governança, estrutura e medição | G0: AGENTS/AGENTSCHAT; `check-modules`, `check-traceability`, `version.mjs`, `run-tests` no CI; harness e baseline; release separado; branches tratadas |
| F1 | Persistência, dados e migração | G1: fixtures históricos verdes; proteção forward; `vault.json`; backup; adapters e escritor único do mapa; identidade; artefatos |
| F2 | Arquitetura do monólito e legacy | G2: renderer canônico em produção; editor extraído; cadeias e adapters legacy removidos; sem teste dependente de `app.js` |
| F3 | Segurança | G3: CSP; plugins; Electron/Android; HTML/embeds; ZIP; IA; testes de segurança verdes |
| F4 | Plataformas, release e distribuição | G4: E2E web+Electron; smoke instalador/APK; `UrbeNative.contract`; release por tag; LICENSE final (bloqueado por OD-03) |
| F5 | Performance | G5: baseline, budgets aprovados, `perf:check` verde, flush incremental, busca indexada |
| F6 | UX, auditoria e release 2.0 | G6: S1–S10; acessibilidade; tutorial/migração; recuperação; auditoria final; release |

Ordem entre fases: F0 → F1 → F2; F3 pode iniciar após F0 nos itens sem dependência de F1/F2 (as dependências explícitas mandam); F4 exige F1–F3 para E2E/smoke; F5 exige F1/F2 para persistência/busca; F6 encerra.


## F0 — Governança, estrutura e medição (G0)

### RM-F0-01 — Publicar AGENTS.md e AGENTSCHAT.md
- **Estado:** [x]
- **REQ:** REQ-088
- **SPEC:** §10.4
- **Fase:** F0
- **Depende:** —
- **Implementação:** `AGENTS.md` (contrato de agentes) e `AGENTSCHAT.md` (log de handoff) na raiz.
- **Integração:** Referenciados em `CONTRIBUTING.md` e `docs/v2/README.md`.
- **Testes:** `tools/check-traceability.mjs` verifica existência dos dois arquivos.
- **Documentação:** Os próprios arquivos; entrada de sessão em AGENTSCHAT.
- **Aceite:** Arquivos existem, são referenciados e o verificador passa.
- **Gate:** G0

### RM-F0-02 — Verificador e gerador de rastreabilidade
- **Estado:** [x]
- **REQ:** REQ-002, REQ-024
- **SPEC:** §2, §9.5
- **Fase:** F0
- **Depende:** —
- **Implementação:** `tools/check-traceability.mjs` (REQ IMPLEMENTAR ∈ SPEC ∧ ROADMAP ∧ TRACEABILITY) e `tools/gen-traceability.mjs` (gera TRACEABILITY.md a partir de SPEC+ROADMAP).
- **Integração:** Ferramenta isolada, somente lê `docs/v2`; não toca runtime.
- **Testes:** Execução local: `node tools/check-traceability.mjs` retorna 0; teste negativo em `tests/traceability.mjs` (REQ removido → falha).
- **Documentação:** `docs/v2/AUDIT-COVERAGE.md` (auditoria manual cruzada).
- **Aceite:** Verificador passa com o ledger atual e falha com REQ sem cobertura.
- **Gate:** G0

### RM-F0-03 — Executar verificador de rastreabilidade no CI
- **Estado:** [ ]
- **REQ:** REQ-068
- **SPEC:** §9
- **Fase:** F0
- **Depende:** RM-F0-02
- **Implementação:** Passo em `structural-checks.yml`: `node tools/check-traceability.mjs && node tools/gen-traceability.mjs --check`.
- **Integração:** Job estrutural existente.
- **Testes:** Passo falha em PR que remove REQ/item; PR de teste comprova.
- **Documentação:** CONTRIBUTING §Rastreabilidade.
- **Aceite:** CI vermelho quando cobertura quebra; verde no estado atual.
- **Gate:** G0

### RM-F0-04 — Alinhar ARCHITECTURE.md à realidade e ao alvo
- **Estado:** [ ]
- **REQ:** REQ-001
- **SPEC:** §1.2, §3.1
- **Fase:** F0
- **Depende:** —
- **Implementação:** Reescrever `ARCHITECTURE.md`: camadas alvo (§3.1), estado real (aquário canônico não ligado, `app.js`), autoridades, dependências permitidas/proibidas.
- **Integração:** Link para SPEC §3 e ARCHITECTURE-MAP; sem duplicar texto normativo.
- **Testes:** Revisão manual + `tools/check-modules.mjs` valida referências a módulos citados.
- **Documentação:** `ARCHITECTURE.md`.
- **Aceite:** Diagrama e texto não contradizem os fatos de ARCHITECTURE-MAP §4.
- **Gate:** G0

### RM-F0-05 — Runner de testes multiplataforma
- **Estado:** [ ]
- **REQ:** REQ-063
- **SPEC:** §9
- **Fase:** F0
- **Depende:** —
- **Implementação:** `tools/run-tests.mjs`: executa todos `tests/*.mjs` em processos filhos, agrega falhas, resumo e código de saída.
- **Integração:** `package.json` `scripts.test` → `node tools/run-tests.mjs`; CI usa o mesmo comando.
- **Testes:** Teste do runner com script falho/passando; roda em Windows (CI matrix) e Linux.
- **Documentação:** CONTRIBUTING §Testes.
- **Aceite:** `npm test` roda todos, não para no primeiro erro, funciona em Windows e Linux.
- **Gate:** G0

### RM-F0-06 — Fonte única de versão (`tools/version.mjs`)
- **Estado:** [ ]
- **REQ:** REQ-019, REQ-065
- **SPEC:** §10.2
- **Fase:** F0
- **Depende:** —
- **Implementação:** `tools/version.mjs check|sync`: fonte `package.json`; valida/sincroniza `package-lock.json`, `core.js`, `index.html <title>`, `sw.js` cache, CHANGELOG, `tests/static.mjs`; `V21_VERSION` passa a derivar de `UrbeCore.version` (uma atribuição).
- **Integração:** `tests/static.mjs`/`consistency.mjs` param de hardcodar; `app.yml` usa `version.mjs check`.
- **Testes:** `tests/version.mjs` (drift injetado falha); `consistency.mjs` adaptado.
- **Documentação:** CONTRIBUTING §Release.
- **Aceite:** Bump de versão em um lugar propaga por `sync`; `check` falha em drift.
- **Gate:** G0

### RM-F0-07 — Gerar `src/modules.json` a partir do `index.html` atual
- **Estado:** [ ]
- **REQ:** REQ-025
- **SPEC:** §3.2
- **Fase:** F0
- **Depende:** —
- **Implementação:** Script `tools/gen-modules.mjs` cria manifesto (`name,file,provides,requires,phase,platforms`) preservando a ordem atual; `requires` derivado de `service()`/`window.Urbe*` usados.
- **Integração:** Sem alterar `index.html` ainda.
- **Testes:** `tests/modules-manifest.mjs`: manifesto reproduz a ordem e o conjunto de `index.html`/`sw.js`.
- **Documentação:** SPEC §3.2; ADR-0001.
- **Aceite:** Manifesto cobre 100% dos JS de `src/` e reproduz a ordem atual.
- **Gate:** G0

### RM-F0-08 — `check-modules` e derivação de `index.html`/`sw.js`/`build-www`
- **Estado:** [ ]
- **REQ:** REQ-025
- **SPEC:** §3.2
- **Fase:** F0
- **Depende:** RM-F0-07
- **Implementação:** `tools/check-modules.mjs` (dependência sem provedor anterior, ciclo, arquivo ausente/extra) e geração/validação de `index.html` (bloco de scripts), `sw.js` (APP_SHELL) e `tools/build-www.mjs` a partir do manifesto.
- **Integração:** CI executa `check-modules`; `consistency.mjs` lê o manifesto.
- **Testes:** Testes negativos: ordem inválida, ciclo, arquivo fora do manifesto.
- **Documentação:** ADR-0001 status confirmado; CONTRIBUTING §Módulos.
- **Aceite:** Reordenar módulo inválido falha no CI; `index.html` e `sw.js` nunca divergem do manifesto.
- **Gate:** G0

### RM-F0-09 — Validação automática de boundaries de camadas
- **Estado:** [ ]
- **REQ:** REQ-015
- **SPEC:** §3.1
- **Fase:** F0
- **Depende:** RM-F0-08
- **Implementação:** `tests/boundaries.mjs` + regras em `check-modules`: `ui→features→core→persistence→native`; `world`/`pages` não dependem de `ai`; acesso a `window.Urbe*` só se declarado em `requires`.
- **Integração:** Roda no CI; violações conhecidas listadas com prazo em `docs/v2/discovery/BOUNDARY-EXCEPTIONS.md`.
- **Testes:** Teste negativo com dependência proibida injetada.
- **Documentação:** ARCHITECTURE.md.
- **Aceite:** Nenhuma violação nova; exceções conhecidas explícitas e decrescentes.
- **Gate:** G0

### RM-F0-10 — Gerador de vaults sintéticos S/M/L
- **Estado:** [ ]
- **REQ:** REQ-070
- **SPEC:** §8
- **Fase:** F0
- **Depende:** —
- **Implementação:** `tools/perf/make-vault.mjs`: vaults determinísticos de 50/500/5000 notas (4 KB média, 10% links, 5% assets, semente fixa).
- **Integração:** Usado por harness (RM-F0-11) e E2E.
- **Testes:** `tests/perf-vault.mjs`: mesma semente → mesmos hashes.
- **Documentação:** PERFORMANCE §4.
- **Aceite:** Três vaults reproduzíveis byte a byte.
- **Gate:** G0

### RM-F0-11 — Harness dos cenários de performance
- **Estado:** [ ]
- **REQ:** REQ-070
- **SPEC:** §8
- **Fase:** F0
- **Depende:** RM-F0-10
- **Implementação:** `tools/perf/run.mjs` (`npm run perf`): cenários 2–7 de PERFORMANCE §4 em Chromium (Playwright) com throttling D2; marcas `performance.mark` no boot; saída JSON.
- **Integração:** Instrumentação mínima e inerte em produção (`?perf=1`).
- **Testes:** Execução repetida: variação ≤ tolerância declarada; teste do parser de resultados.
- **Documentação:** `docs/v2/perf/README.md`.
- **Aceite:** Harness roda localmente e no CI e produz JSON com ambiente e commit.
- **Gate:** G0

### RM-F0-12 — Publicar baseline 1.8.2
- **Estado:** [ ]
- **REQ:** REQ-070
- **SPEC:** §8
- **Fase:** F0
- **Depende:** RM-F0-11
- **Implementação:** Executar harness sobre 1.8.2-beta em D1/D2 e commitar `docs/v2/perf/baseline-<data>.json`.
- **Integração:** Referência para REQ-071.
- **Testes:** Três execuções por cenário; mediana e p95 registradas.
- **Documentação:** PERFORMANCE.md §1 atualizado (números reproduzíveis).
- **Aceite:** Baseline publicado e reproduzível.
- **Gate:** G0

### RM-F0-13 — Gate de dívida (`tools/check-debt.mjs`)
- **Estado:** [ ]
- **REQ:** REQ-012
- **SPEC:** §1.2
- **Fase:** F0
- **Depende:** —
- **Implementação:** Mede em `src/app.js`: nº de `nome=function` de nível superior, funções duplicadas, variáveis `*Antes|*Base|*Old`, atribuições de `V21_VERSION`, bytes; teto = valor atual e monotonicamente decrescente.
- **Integração:** CI falha se qualquer métrica sobe; PR deve declarar dívida criada/removida (template).
- **Testes:** `tests/debt.mjs` (injeção de sobrescrita falha).
- **Documentação:** PR template + CONTRIBUTING.
- **Aceite:** Nenhuma nova camada de versão possível sem quebrar o CI.
- **Gate:** G0

### RM-F0-14 — Separar integração e publicação (release por tag)
- **Estado:** [ ]
- **REQ:** REQ-006, REQ-066
- **SPEC:** §10.2
- **Fase:** F0
- **Depende:** RM-F0-06
- **Implementação:** Novo workflow `release.yml` (tag `v*` ou `workflow_dispatch`, `needs` de estrutura+testes), remover publicação do push em `main` em `app.yml`; PRs mantêm build de validação.
- **Integração:** `app.yml` passa a ser só build/validação; `permissions` mínimas por job.
- **Testes:** Dry-run em branch de teste: push em `main` não publica; tag publica em repositório de teste.
- **Documentação:** CONTRIBUTING §Release; ADR-0005 confirmado (OD-05).
- **Aceite:** Merge em `main` nunca cria Release; tag com testes verdes cria.
- **Gate:** G0

### RM-F0-15 — Endurecer CI: actions por SHA e Dependabot
- **Estado:** [ ]
- **REQ:** REQ-066
- **SPEC:** §10.2
- **Fase:** F0
- **Depende:** RM-F0-14
- **Implementação:** Fixar actions por SHA; `permissions` mínimas; `.github/dependabot.yml` (npm, actions, gradle).
- **Integração:** Workflows existentes.
- **Testes:** Verificador `tools/check-workflows.mjs` rejeita `uses:` sem SHA.
- **Documentação:** CONTRIBUTING.
- **Aceite:** Nenhuma action por tag móvel; Dependabot abre PRs.
- **Gate:** G0

### RM-F0-16 — Ciclo de vida de branches e backlog de issues
- **Estado:** [ ]
- **REQ:** REQ-018, REQ-067
- **SPEC:** §10.4
- **Fase:** F0
- **Depende:** —
- **Implementação:** Verificar cada branch `claude/*` (incorporada? descartada?), registrar em AGENTSCHAT e remover; criar issues-filhas de #33 para cada fase/item do ROADMAP (backlog rastreável).
- **Integração:** Issue #33 como item-mãe; issues referenciam RM-ids.
- **Testes:** Lista de branches remotas após limpeza só contém `main`, `chore/*` e trabalho ativo.
- **Documentação:** CONTRIBUTING §Branches.
- **Aceite:** Zero branches `claude/*` obsoletas; backlog com RM-ids.
- **Gate:** G0

### RM-F0-17 — Definition of Done no PR template e issue templates
- **Estado:** [ ]
- **REQ:** REQ-005, REQ-011
- **SPEC:** §1.2, §10.1
- **Fase:** F0
- **Depende:** —
- **Implementação:** Atualizar `.github/pull_request_template.md` (RM-id, REQ, dívida, gate, DoD checklist) e templates de issue (exigir REQ para feature).
- **Integração:** Rastreabilidade PR→RM-id.
- **Testes:** Revisão manual + `check-traceability` valida formato de RM-id citado (opcional).
- **Documentação:** CONTRIBUTING §DoD.
- **Aceite:** PR sem RM-id/REQ não passa a revisão; template contém DoD completo.
- **Gate:** G0

### RM-F0-18 — Comando único `npm run check` (sincronização docs×código)
- **Estado:** [ ]
- **REQ:** REQ-004
- **SPEC:** §1.2
- **Fase:** F0
- **Depende:** RM-F0-05, RM-F0-06, RM-F0-08, RM-F0-02
- **Implementação:** `npm run check` executa version, modules, traceability, tutorial `--check`, workflows e testes.
- **Integração:** CI chama `npm run check`.
- **Testes:** Falha em qualquer sub-check falha o comando.
- **Documentação:** CONTRIBUTING.
- **Aceite:** Um comando valida todas as sincronizações; verde no CI.
- **Gate:** G0


## F1 — Persistência, dados e migração (G1)

### RM-F1-01 — Criar fixtures de vaults históricos
- **Estado:** [ ]
- **REQ:** REQ-037
- **SPEC:** §5.1
- **Fase:** F1
- **Depende:** RM-F0-10
- **Implementação:** `tests/fixtures/vaults/*` (v1-mapa-v1, v1-mapa-v2, v1-mapa-v4, v1-notas-sem-id, v1-journal-pendente, v1-cidades-mescladas, v1-personalizacao, v1-paginas, v1-composicoes-historico, v1-mundo-antigo, futuro-desconhecido) e gerador documentado.
- **Integração:** Usadas por testes de migração, E2E e harness.
- **Testes:** Cada fixture abre no código 1.8.2 sem erro (teste de caracterização).
- **Documentação:** `tests/fixtures/vaults/README.md`.
- **Aceite:** Todas as fixtures existem, versionadas e abrem em 1.8.2.
- **Gate:** G1

### RM-F1-02 — Harness de abertura/migração de fixtures
- **Estado:** [ ]
- **REQ:** REQ-037
- **SPEC:** §5.1
- **Fase:** F1
- **Depende:** RM-F1-01
- **Implementação:** `tests/vault-migration.mjs`: para cada fixture abre via `WorkspacePersistence` e afirma bytes de notas, IDs, arquivos futuros e idempotência.
- **Integração:** Base para todos os itens de dados seguintes (deve passar em 1.8.2 antes de qualquer mudança).
- **Testes:** Rodar contra 1.8.2 (baseline) e depois a cada item.
- **Documentação:** DATA-CATALOG §5.
- **Aceite:** Harness verde na 1.8.2; vermelho quando nota é alterada.
- **Gate:** G1

### RM-F1-03 — Catálogo de formatos verificado por código
- **Estado:** [ ]
- **REQ:** REQ-023
- **SPEC:** §5.1
- **Fase:** F1
- **Depende:** RM-F1-02
- **Implementação:** `tools/check-catalog.mjs`: todo nome `.urbe/*`, `Personalização/*`, `Páginas/*` escrito por `src/` deve constar em DATA-CATALOG com versão e política; testes por formato.
- **Integração:** CI.
- **Testes:** Teste negativo: novo arquivo sem linha no catálogo falha.
- **Documentação:** DATA-CATALOG.md.
- **Aceite:** Nenhum formato persistido fora do catálogo.
- **Gate:** G1

### RM-F1-04 — `vault.json` e detecção de versão do vault
- **Estado:** [ ]
- **REQ:** REQ-036
- **SPEC:** §5.1
- **Fase:** F1
- **Depende:** RM-F1-02
- **Implementação:** `src/persistence/vault-meta.js`: ler/criar `.urbe/vault.json` (`formatVersion,createdBy,lastWriter,migrations[]`); ausência ⇒ vault 1.x; leitura antes de qualquer escrita.
- **Integração:** `WorkspacePersistence.load` chama antes de `flush`.
- **Testes:** Fixtures: 1.x sem `vault.json`, 2.x, futuro maior.
- **Documentação:** DATA-CATALOG; SPEC §5.1.
- **Aceite:** Vault 1.x é reconhecido e nenhuma escrita ocorre antes da leitura.
- **Gate:** G1

### RM-F1-05 — Proteção forward: journal, history, trash, compositions
- **Estado:** [ ]
- **REQ:** REQ-035
- **SPEC:** §5.1
- **Fase:** F1
- **Depende:** RM-F1-04
- **Implementação:** Leitores 2.x preservam arquivos de versão desconhecida; gravam `journal.v2.json`, `history.v2.json`, `trash.v2.json`, `compositions.v2.json` sem tocar nos v1; avisam e operam em somente leitura para o artefato futuro.
- **Integração:** `WorkspacePersistence`, `history.js`, `trash.js`, `composition/store.js`.
- **Testes:** Fixture `futuro-desconhecido`: bytes inalterados após abrir/salvar; teste de coexistência v1/v2.
- **Documentação:** ADR-0004 (OD-11 confirmado).
- **Aceite:** Nenhum arquivo `.urbe/*` de versão desconhecida é alterado ou apagado.
- **Gate:** G1

### RM-F1-06 — Proteção forward: mapa, tema, páginas, blocos, modelos
- **Estado:** [ ]
- **REQ:** REQ-035
- **SPEC:** §5.1
- **Fase:** F1
- **Depende:** RM-F1-04
- **Implementação:** Preservar e avisar para `mapa.v>4`, `tema.json` `versao>1`, `.page.json` `version>1`, blocos/modelos futuros; modo somente leitura por artefato.
- **Integração:** `app.js` (abertura), `customize.js`, `pages/engine.js`.
- **Testes:** Fixture futuro: nada reescrito; UI mostra aviso.
- **Documentação:** tutorial (Solução de problemas).
- **Aceite:** Abrir vault futuro nunca perde dados.
- **Gate:** G1

### RM-F1-07 — Motor de backup pré-migração e restauração
- **Estado:** [ ]
- **REQ:** REQ-038
- **SPEC:** §5.1
- **Fase:** F1
- **Depende:** RM-F1-04
- **Implementação:** `src/persistence/backup.js`: `backup(files,de,para)` em `.urbe/backup/<data>-<de>-<para>/`, restauração e registro em `vault.json.migrations`; idempotência.
- **Integração:** Migrações chamam antes de escrever; comando `workspace.restoreBackup`.
- **Testes:** Testes: backup íntegro, restauração byte a byte, migração repetida não duplica.
- **Documentação:** MIGRATION.md.
- **Aceite:** Toda migração é precedida de backup restaurável.
- **Gate:** G1

### RM-F1-08 — Fonte única de tipos de artefato
- **Estado:** [ ]
- **REQ:** REQ-039
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-02
- **Implementação:** `src/core/artifacts.js` (`classify(path)`); substituir listas em `workspace.js:9,45`, `app.js:3105,4472`, `ai/tools.js:39`, `native/bridge.js:44`; decisão explícita para `.canvas`.
- **Integração:** Registrado no manifesto; consumidores usam `UrbeArtifacts`.
- **Testes:** `tests/artifacts.mjs` (tabela de casos); `grep` de listas antigas retorna 0.
- **Documentação:** DATA-CATALOG §1.
- **Aceite:** Uma só lista; comportamento das fixtures inalterado.
- **Gate:** G1

### RM-F1-09 — Modelo de artefatos aplicado (indexação e roteamento)
- **Estado:** [ ]
- **REQ:** REQ-014
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-08
- **Implementação:** `WorkspacePersistence.load` classifica; DocumentStore só indexa artefatos-nota; `document.open` roteia por tipo (remove wrapper L8 de `studio.js` no item RM-F2-17).
- **Integração:** Explorer/quick-open mostram tipos.
- **Testes:** Fixtures: plugin/tema/página não aparecem como nota; abrir cada tipo roteia corretamente.
- **Documentação:** tutorial; DATA-CATALOG §4.
- **Aceite:** Arquivos não-nota não viram casas; propriedade de arquivos comuns mantida.
- **Gate:** G1

### RM-F1-10 — Contrato de adapters de persistência e testes
- **Estado:** [ ]
- **REQ:** REQ-028
- **SPEC:** §3.3
- **Fase:** F1
- **Depende:** RM-F1-02
- **Implementação:** Definir contrato `{list,read,readBlob,write,writeBlob,remove,createFolder,removeFolder,stat}` e suíte `tests/adapter-contract.mjs` reutilizável.
- **Integração:** Adapters futuros herdam a suíte.
- **Testes:** Suíte roda contra mock e contra `vault-fs.js`.
- **Documentação:** SPEC §3.3; contracts/persistence-adapter.md.
- **Aceite:** Contrato e suíte publicados; `vault-fs` passa.
- **Gate:** G1

### RM-F1-11 — Adapters IDB e FSA extraídos de `FS/Disco/DBK`
- **Estado:** [ ]
- **REQ:** REQ-028
- **SPEC:** §3.3
- **Fase:** F1
- **Depende:** RM-F1-10
- **Implementação:** `src/persistence/adapters/idb.js` e `fsa.js`; `app.js` deixa de conter `DBK`/`FS` para esses modos (remove L5 parcial).
- **Integração:** `WorkspacePersistence` recebe adapter via serviço.
- **Testes:** Suíte de contrato + fixtures + `integration-runtime`.
- **Documentação:** DATA-CATALOG §3.
- **Aceite:** Modos interno e pasta operam via adapters; código antigo removido.
- **Gate:** G1

### RM-F1-12 — Adapters Electron/Android e autoridade única de escrita
- **Estado:** [ ]
- **REQ:** REQ-028
- **SPEC:** §3.3
- **Fase:** F1
- **Depende:** RM-F1-11
- **Implementação:** `adapters/electron.js`, `adapters/android.js` sobre `UrbeNative`; `WorkspacePersistence` como única escrita; remover `FS`/`Disco` residual (L5 completo).
- **Integração:** Bridge preservado.
- **Testes:** `native-bridge.mjs`, `native-android.mjs` + suíte de contrato.
- **Documentação:** LEGACY-MAP L5.
- **Aceite:** `grep 'FS\.' app.js` sem escrita direta; L5 removido.
- **Gate:** G1

### RM-F1-13 — Escritor único do mapa e leitura de `mapa.v`
- **Estado:** [ ]
- **REQ:** REQ-040
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-12
- **Implementação:** Eliminar `rodarSinc`; binários passam por `WorkspacePersistence`; validar `mapa.v`; unificar camadas de `estadoDesejado` no serializador do persistence.
- **Integração:** Persistência ↔ projeção via `world.projection.metadata()`.
- **Testes:** Fixtures mapa v1/v2/v4; teste de dupla escrita (não há); assinatura de binários preservada.
- **Documentação:** DATA-CATALOG §0.
- **Aceite:** Só `WorkspacePersistence` escreve `mapa.json`.
- **Gate:** G1

### RM-F1-14 — IDs estáveis de regiões, construções e vínculos
- **Estado:** [ ]
- **REQ:** REQ-041, REQ-013
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-13
- **Implementação:** `reg_`/`ast_` IDs e `parentId` no mapa; leitura de mapas sem ID gera IDs determinísticos e persiste na migração; `parentNoteName` mantido para leitura 1.x.
- **Integração:** Projeção/mundo usam IDs; sem dependência de path para relações novas.
- **Testes:** Fixtures: rename externo mantém região/asset; mapa v4 legível pela 1.8.2 (teste de compat).
- **Documentação:** DATA-CATALOG §4.
- **Aceite:** Relações novas por ID; leitura 1.x preservada.
- **Gate:** G1

### RM-F1-15 — Sidecar de identidade e reconciliação
- **Estado:** [ ]
- **REQ:** REQ-042
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-13
- **Implementação:** `src/persistence/identity.js`: `.urbe/identity.json`; `syncFromDisk` reconcilia por path e por fingerprint; nenhuma escrita em notas.
- **Integração:** `workspace.js:42-60`.
- **Testes:** Fixtures: rename externo, cópia de vault sem `.urbe/`, cópias idênticas (ambiguidade tratada).
- **Documentação:** ADR-0006 confirmado (OD-10).
- **Aceite:** Rename/move externo preserva ID e relações.
- **Gate:** G1

### RM-F1-16 — GC de órfãos em history/trash/compositions
- **Estado:** [ ]
- **REQ:** REQ-042
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-15
- **Implementação:** Coleta de referências órfãs com retenção configurável e log; comando `workspace.gc`.
- **Integração:** Persistência.
- **Testes:** Fixture com órfãos; dry-run e execução.
- **Documentação:** tutorial (Recuperação).
- **Aceite:** Órfãos identificados e coletados sem perda de dados ativos.
- **Gate:** G1

### RM-F1-17 — Versão de mundo e reorganização com backup/desfazer
- **Estado:** [ ]
- **REQ:** REQ-043
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-07, RM-F1-13
- **Implementação:** `mundo` explícito com versões; `urbeReorganizarCidade` chama backup e oferece desfazer; regra para vaults mesclados sem `mundo`.
- **Integração:** `app.js:3230,5382` (até extração).
- **Testes:** Fixture `v1-mundo-antigo`, `v1-cidades-mescladas`: backup criado, posições restauráveis.
- **Documentação:** DATA-CATALOG R-4.
- **Aceite:** Nenhuma reorganização sem backup e desfazer.
- **Gate:** G1

### RM-F1-18 — Export ZIP com manifesto e estado local
- **Estado:** [ ]
- **REQ:** REQ-044
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-04
- **Implementação:** `urbe-export.json` (format, appVersion, files+sha256, state); import valida; estado local sem chaves de IA.
- **Integração:** `exportZip`/`importFiles`.
- **Testes:** Testes: hash íntegro, adulteração detectada, ausência de chaves.
- **Documentação:** tutorial (Exportar).
- **Aceite:** Export/import round-trip verificado por hash.
- **Gate:** G1

### RM-F1-19 — Migração multi-cidade idempotente
- **Estado:** [ ]
- **REQ:** REQ-045
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-07
- **Implementação:** Marcador em `vault.json`; sem recopiar `Cidades/` a cada boot; opção de arquivar.
- **Integração:** `urbeEnsureSingleVault`.
- **Testes:** Fixture `v1-cidades-mescladas`: 3 boots consecutivos, mesmo resultado.
- **Documentação:** MIGRATION.md.
- **Aceite:** Migração roda uma vez; sem duplicação.
- **Gate:** G1

### RM-F1-20 — Escrita recuperável na web e integridade do modo IDB
- **Estado:** [ ]
- **REQ:** REQ-046
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-11
- **Implementação:** FSA: temp+substituir quando suportado; caso contrário journal por arquivo; IDB: verificação de integridade, exportação garantida e aviso persistente.
- **Integração:** Adapters idb/fsa.
- **Testes:** Simulação de falha no meio da escrita; export do IDB reproduz vault.
- **Documentação:** tutorial (Onde ficam seus arquivos).
- **Aceite:** Interrupção não corrompe arquivo; IDB exportável.
- **Gate:** G1

### RM-F1-21 — Estado local por vault e tolerância a órfãos
- **Estado:** [ ]
- **REQ:** REQ-047
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-04
- **Implementação:** Chaves `urbe.editor.workspace.v1::<vault>`, explorer, aprovações; IDs órfãos ignorados sem erro; migração das chaves antigas.
- **Integração:** `editor/workspace.js`, `explorer/model.js`, `plugins.js`.
- **Testes:** Trocar de vault não reabre abas alheias; orphan não lança.
- **Documentação:** DATA-CATALOG §2.
- **Aceite:** Estado local isolado por vault.
- **Gate:** G1

### RM-F1-22 — Auditoria e destino de `aiLocal` e campos sem consumidor
- **Estado:** [ ]
- **REQ:** REQ-048
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-02
- **Implementação:** Confirmar consumo; consumir, migrar ou remover com leitura compatível; registrar no catálogo.
- **Integração:** Mapa/projeção.
- **Testes:** Fixtures com `aiLocal` preservados ou migrados sem perda.
- **Documentação:** DATA-CATALOG R-12.
- **Aceite:** Nenhum campo persistido sem dono/consumidor documentado.
- **Gate:** G1

### RM-F1-23 — Páginas preservam chaves desconhecidas e versão
- **Estado:** [ ]
- **REQ:** REQ-049
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-06
- **Implementação:** `normalize` mantém `unknown` e `version` original; avisos não destrutivos.
- **Integração:** `pages/engine.js`, Studio.
- **Testes:** `pages.mjs` ampliado: página v2 fictícia sobrevive a open/save.
- **Documentação:** tutorial (Páginas).
- **Aceite:** Round-trip de página sem perda.
- **Gate:** G1

### RM-F1-24 — Regra: relações internas novas usam ID
- **Estado:** [ ]
- **REQ:** REQ-013
- **SPEC:** §5.2
- **Fase:** F1
- **Depende:** RM-F1-14
- **Implementação:** Lint `tools/check-relations.mjs`: novos campos de relação em formatos persistidos devem usar `*Id`; exceções listadas.
- **Integração:** CI.
- **Testes:** Teste negativo com relação por path.
- **Documentação:** DATA-CATALOG §4.
- **Aceite:** Nenhuma relação nova por path.
- **Gate:** G1

### RM-F1-25 — Testes de invariante local-first/offline e integridade de notas
- **Estado:** [ ]
- **REQ:** REQ-007
- **SPEC:** §1.2
- **Fase:** F1
- **Depende:** RM-F1-02
- **Implementação:** `tests/invariants.mjs`: fluxos essenciais sem rede; bytes de notas preservados em open/save; nenhuma chamada de rede no core.
- **Integração:** Também no E2E.
- **Testes:** Falha se `fetch` for chamado no fluxo essencial.
- **Documentação:** PRODUCT-UX §6 (S1).
- **Aceite:** Invariantes verificadas em CI.
- **Gate:** G1

### RM-F1-26 — Política de migração por formato (contrato final)
- **Estado:** [ ]
- **REQ:** REQ-023
- **SPEC:** §5.1
- **Fase:** F1
- **Depende:** RM-F1-05, RM-F1-06
- **Implementação:** Completar DATA-CATALOG §7 com política forward/backward por arquivo, versão alvo e teste; publicar `docs/v2/contracts/vault-format.md`.
- **Integração:** SPEC §5.
- **Testes:** `check-catalog` valida presença de política por formato.
- **Documentação:** contracts/vault-format.md.
- **Aceite:** Todo formato tem schema, versão, política, fixture e teste.
- **Gate:** G1


## F2 — Arquitetura do monólito e legacy (G2)

### RM-F2-01 — Avaliação de hotspots por responsabilidade e acoplamento
- **Estado:** [ ]
- **REQ:** REQ-022
- **SPEC:** §3.1
- **Fase:** F2
- **Depende:** RM-F0-09
- **Implementação:** Relatório `docs/v2/discovery/HOTSPOTS.md` com métricas (fan-in/out, responsabilidades, coesão) para `pages/engine`, `studio`, `free`, `world/life`, `ai/ui`, `customize/panel`, `pixel-art`; decisão explícita por arquivo (extrair, manter, adiar).
- **Integração:** Insumo para novos REQ (não extrai).
- **Testes:** Script `tools/hotspots.mjs` reproduz métricas.
- **Documentação:** HOTSPOTS.md.
- **Aceite:** Nenhuma extração de hotspot sem decisão registrada.
- **Gate:** G2

### RM-F2-02 — Harness de teste de produção do monólito
- **Estado:** [ ]
- **REQ:** REQ-064, REQ-003
- **SPEC:** §3.1, §9
- **Fase:** F2
- **Depende:** RM-F1-02
- **Implementação:** `tests/app-runtime.mjs`: carrega `index.html` real em jsdom/Playwright e expõe abertura/boot para testes de comportamento; substitui fatiamento por marcadores.
- **Integração:** Usado por todos os itens de extração.
- **Testes:** Cobre boot, abrir vault fixture, criar nota, mundo.
- **Documentação:** TEST-MATRIX §5.
- **Aceite:** Testes de extração não dependem de texto de `app.js`.
- **Gate:** G2

### RM-F2-03 — Mover JSZip para `vendor/jszip`
- **Estado:** [ ]
- **REQ:** REQ-031
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F0-08
- **Implementação:** `vendor/jszip/jszip.min.js` + `LICENSE`; atualizar `index.html`, `sw.js`, `modules.json`, `build-www`.
- **Integração:** Removido `src/legacy/bootstrap.js`.
- **Testes:** `consistency.mjs` e `modules-manifest` verdes; import/export ZIP funciona.
- **Documentação:** THIRD-PARTY.
- **Aceite:** `src/legacy/` sem vendor.
- **Gate:** G2

### RM-F2-04 — Remover resíduos de UI e gancho `window.URBE`
- **Estado:** [ ]
- **REQ:** REQ-030
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-02
- **Implementação:** Auditar/remover L10 (minimapa legado, botão Vault/ZIP oculto, input compat, diálogo antigo) e L11 (`window.URBE`) ou movê-lo para `diagnostics`.
- **Integração:** `app.js`, `ui/icons.js`.
- **Testes:** Teste de UI sem referência; grep zero.
- **Documentação:** LEGACY-MAP L10/L11.
- **Aceite:** L10 e L11 removidos.
- **Gate:** G2

### RM-F2-05 — Ligar renderer canônico (paridade visual)
- **Estado:** [ ]
- **REQ:** REQ-026
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-02, RM-F0-12
- **Implementação:** `aquarium.renderer.configure` chamado pelo boot real; `app.js` fornece camadas de desenho como callbacks até serem migradas; teste de paridade (screenshots/hash de canvas).
- **Integração:** Scheduler central controla o quadro.
- **Testes:** Paridade de frame em fixture + `production-order` atualizado; perf não regride (baseline).
- **Documentação:** ARCHITECTURE-MAP §4.
- **Aceite:** Renderer canônico dirige o desenho em produção.
- **Gate:** G2

### RM-F2-06 — Ligar controlador de toque canônico
- **Estado:** [ ]
- **REQ:** REQ-026
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-05
- **Implementação:** `aquarium.touch.attach` no canvas real; remover handlers `canvasDown/Move/Up*` duplicados.
- **Integração:** Cobertura de gestos (arrastar, zoom, toque longo).
- **Testes:** `runtime-adapters.mjs` + E2E de gesto (RM-F4-06).
- **Documentação:** LEGACY-MAP L6.
- **Aceite:** Um só controlador de toque.
- **Gate:** G2

### RM-F2-07 — Ligar grafo de ruas canônico
- **Estado:** [ ]
- **REQ:** REQ-026
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-05
- **Implementação:** `roads.setPath/sync` chamados pela produção; remover A*/`rebuildRoadNetwork` duplicados.
- **Integração:** Rotas de moradores usam `aquarium.roads`.
- **Testes:** `world-system.mjs` + testes de rota com fixtures.
- **Documentação:** LEGACY-MAP L6.
- **Aceite:** Um só grafo de ruas.
- **Gate:** G2

### RM-F2-08 — Remover desenho e laço duplicados de `app.js`
- **Estado:** [ ]
- **REQ:** REQ-026, REQ-029
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-05, RM-F2-06, RM-F2-07
- **Implementação:** Apagar `frame`, `draw*`, camadas `drawTrees`/`drawRegions`, laço `requestAnimationFrame` do `app.js` (L6).
- **Integração:** Scheduler/renderer canônicos.
- **Testes:** Paridade visual; `check-debt` cai; testes verdes.
- **Documentação:** LEGACY-MAP L6 removido.
- **Aceite:** L6 zerado; `app.js` menor e sem desenho.
- **Gate:** G2

### RM-F2-09 — Extrair renderMarkdown para o editor
- **Estado:** [ ]
- **REQ:** REQ-027
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-02
- **Implementação:** `src/editor/markdown.js` (render, sanitização `v22Url`); `app.js` consome.
- **Integração:** Serviço `editor.surface`.
- **Testes:** Testes de renderização (tabelas, callouts, math, links, sanitização) + regressão `composition`.
- **Documentação:** contracts.
- **Aceite:** `renderMarkdown` fora de `app.js`.
- **Gate:** G2

### RM-F2-10 — Extrair editor Visual e conversão
- **Estado:** [ ]
- **REQ:** REQ-027
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-09
- **Implementação:** `src/editor/visual.js` (`markdownFromVisual`, contenteditable, `setEditorViewMode`).
- **Integração:** `editor-persistence` ajustado.
- **Testes:** Round-trip Markdown↔Visual em fixtures; sem ressuscitar nota apagada.
- **Documentação:** —
- **Aceite:** Visual fora de `app.js`.
- **Gate:** G2

### RM-F2-11 — Extrair autocompletar de wikilinks e barra de formatação
- **Estado:** [ ]
- **REQ:** REQ-027
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-10
- **Implementação:** `src/editor/wikilinks.js` e integração com `visual-tools`.
- **Integração:** KnowledgeIndex.
- **Testes:** Testes de sugestão/inserção; E2E.
- **Documentação:** —
- **Aceite:** Editor sem regra de edição em `app.js`.
- **Gate:** G2

### RM-F2-12 — Mundo derivado só da projeção (L3, L4)
- **Estado:** [ ]
- **REQ:** REQ-030, REQ-029
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F1-13, RM-F2-08
- **Implementação:** Remover `urbeReconcileWorld`, `urbeCasaParaDocumento`, wrappers `urbeLegacyDesired/urbeOpenLegacy`; mundo derivado de `world.projection`.
- **Integração:** Reescrever `tests/app-loader.mjs` sobre APIs públicas.
- **Testes:** Fixtures abrem com mesmo resultado; sem marcador de texto.
- **Documentação:** LEGACY-MAP L3/L4.
- **Aceite:** L3 e L4 removidos.
- **Gate:** G2

### RM-F2-13 — Eliminar cadeia `estadoDesejado` e `abrirCidade`
- **Estado:** [ ]
- **REQ:** REQ-029
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-12
- **Implementação:** Uma única implementação de cada; remover variáveis `*Antes/*Base/*Old` correspondentes.
- **Integração:** Persistência/projeção.
- **Testes:** Fixtures; `check-debt` cai.
- **Documentação:** —
- **Aceite:** Cadeias resolvidas para 1 camada.
- **Gate:** G2

### RM-F2-14 — Eliminar demais cadeias e duplicadas; `V21_VERSION`
- **Estado:** [ ]
- **REQ:** REQ-029
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-13
- **Implementação:** Resolver `rebuildRoadNetwork`, `buildTree`, `openFilePreview`, `openRegionDialog`, `renderFilePreview`; `V21_VERSION` atribuída 0 vezes (versão do core).
- **Integração:** Explorer usa `explorer.ui`.
- **Testes:** `check-debt` = 0 sobrescritas/duplicadas; testes verdes.
- **Documentação:** LEGACY-MAP.
- **Aceite:** Sem camadas de versão em `app.js`.
- **Gate:** G2

### RM-F2-15 — Remover adapters `legacy.runtime` e `legacy.documents`
- **Estado:** [ ]
- **REQ:** REQ-030
- **SPEC:** §3.3
- **Fase:** F2
- **Depende:** RM-F2-09, RM-F2-13
- **Implementação:** Extrair import/export ZIP, versão e `ensureFolders` para serviços; remover métodos sem uso; `legacy.documents` substituído por `editor.session.record`.
- **Integração:** Consumidores em `explorer/mobile-ui.js`, `settings.js`, `plugins.js`, `tutorial.js`.
- **Testes:** Testes de produção por consumidor (não existiam).
- **Documentação:** LEGACY-MAP L1/L2.
- **Aceite:** L1 e L2 removidos.
- **Gate:** G2

### RM-F2-16 — Extrair hosts `world.custom`, `city.layout`, `world.life.host`, `workspace.storage`; remover wrapper `document.open`
- **Estado:** [ ]
- **REQ:** REQ-030, REQ-032
- **SPEC:** §3.3, §3.4
- **Fase:** F2
- **Depende:** RM-F2-08, RM-F1-09
- **Implementação:** Cada host vira interface de módulo (render, layout, storage); `document.open` único com roteamento por artefato; contrato de `world.custom` versionado.
- **Integração:** Plugins e customize continuam funcionando.
- **Testes:** Testes de contrato de `world.custom`; `world-life.mjs`, `customize.mjs`.
- **Documentação:** contracts/world-custom.md.
- **Aceite:** L7 e L8 removidos.
- **Gate:** G2

### RM-F2-17 — Contratos públicos versionados
- **Estado:** [ ]
- **REQ:** REQ-032
- **SPEC:** §3.4
- **Fase:** F2
- **Depende:** RM-F2-16
- **Implementação:** `docs/v2/contracts/{plugin-api,world-custom,commands-events,native}.md`; `urbe.apiVersion`; testes de conformidade; política de deprecação.
- **Integração:** Runtime expõe `apiVersion`.
- **Testes:** `tests/contracts.mjs`.
- **Documentação:** contracts/*.
- **Aceite:** Mudanças incompatíveis exigem nova versão e teste.
- **Gate:** G2

### RM-F2-18 — Eliminar testes dependentes de texto de `app.js`
- **Estado:** [ ]
- **REQ:** REQ-064
- **SPEC:** §9
- **Fase:** F2
- **Depende:** RM-F2-14
- **Implementação:** Substituir os 10 testes que leem `src/app.js` por testes de comportamento (via `app-runtime`).
- **Integração:** —
- **Testes:** grep de `readFileSync('src/app.js')` em `tests/` = 0.
- **Documentação:** TEST-MATRIX §2.
- **Aceite:** Nenhum teste depende de marcadores de `app.js`.
- **Gate:** G2

### RM-F2-19 — `app.js` reduzido a composição/bootstrap
- **Estado:** [ ]
- **REQ:** REQ-003, REQ-029
- **SPEC:** §3.1, §3.3
- **Fase:** F2
- **Depende:** RM-F2-15, RM-F2-16, RM-F2-18
- **Implementação:** Verificar que `app.js` só compõe módulos e contém adapters legacy remanescentes com prazo.
- **Integração:** `check-debt` teto fixado no valor final.
- **Testes:** Métrica: sem regra de domínio (revisão + lint de imports).
- **Documentação:** ARCHITECTURE.md atualizado.
- **Aceite:** Critério S7 satisfeito.
- **Gate:** G2


## F3 — Segurança (G3)

### RM-F3-01 — Índice de testes de segurança e rastreio do threat model
- **Estado:** [ ]
- **REQ:** REQ-010
- **SPEC:** §7.1
- **Fase:** F3
- **Depende:** RM-F0-05
- **Implementação:** `tests/security/index.mjs`: cada ameaça T-* do THREAT-MODEL referencia ao menos um teste; `tools/check-threats.mjs` valida.
- **Integração:** CI.
- **Testes:** Teste negativo: ameaça sem teste falha.
- **Documentação:** THREAT-MODEL §8.
- **Aceite:** Todo boundary tem propriedade testável.
- **Gate:** G3

### RM-F3-02 — Externalizar script inline e definir política CSP
- **Estado:** [ ]
- **REQ:** REQ-050
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F0-08
- **Implementação:** Mover script de aparência (`index.html:26`) para arquivo; escrever política por plataforma (`docs/v2/contracts/csp.md`) incl. `unsafe-eval` justificado por plugins.
- **Integração:** Manifesto/`sw.js`.
- **Testes:** Aparência aplicada antes do primeiro desenho (medição TTI não regride).
- **Documentação:** contracts/csp.md.
- **Aceite:** Sem inline script; política escrita e aprovada.
- **Gate:** G3

### RM-F3-03 — Aplicar e testar CSP em web, Electron e Capacitor
- **Estado:** [ ]
- **REQ:** REQ-050
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F3-02
- **Implementação:** Meta/header nas três plataformas; relatório de violações em dev.
- **Integração:** Electron: cabeçalho no `protocol.handle`.
- **Testes:** Testes: violação bloqueada; app funciona; E2E sem violações.
- **Documentação:** THREAT-MODEL T-1.
- **Aceite:** CSP ativa e sem violações no E2E.
- **Gate:** G3

### RM-F3-04 — UI de aprovação mostra código, hash e alcance
- **Estado:** [ ]
- **REQ:** REQ-051
- **SPEC:** §7.2
- **Fase:** F3
- **Depende:** RM-F2-17
- **Implementação:** Diálogo de plugin com código/diff, SHA-256 e lista de alcance (rede, chaves, filesystem, bridge).
- **Integração:** `customize/panel.js`, `plugins.js`.
- **Testes:** Teste de UI e de diff em mudança de código.
- **Documentação:** tutorial (Plugins).
- **Aceite:** Usuário vê o que aprova.
- **Gate:** G3

### RM-F3-05 — SHA-256 obrigatório e reaprovação; ferramentas de IA com escrita
- **Estado:** [ ]
- **REQ:** REQ-051
- **SPEC:** §7.2
- **Fase:** F3
- **Depende:** RM-F3-04
- **Implementação:** Remover fallback FNV; sem `crypto.subtle` plugin não roda; plugins aprovados por FNV pedem reaprovação; `api.ia.ferramenta` com `write` exige aprovação.
- **Integração:** `plugins.js`, `agent.js`.
- **Testes:** `customize.mjs` ampliado.
- **Documentação:** THREAT-MODEL §5.
- **Aceite:** Sem fallback fraco.
- **Gate:** G3

### RM-F3-06 — Coerência documental do modelo de plugins
- **Estado:** [ ]
- **REQ:** REQ-020
- **SPEC:** §7.2
- **Fase:** F3
- **Depende:** RM-F3-04
- **Implementação:** README, tutorial, ARCHITECTURE e comentários descrevem full-trust aprovado; remover linguagem sugerindo isolamento.
- **Integração:** Documentação.
- **Testes:** `build-tutorial --check` com tópicos obrigatórios.
- **Documentação:** ADR-0002.
- **Aceite:** Nenhum texto contradiz o modelo.
- **Gate:** G3

### RM-F3-07 — Mitigações de credenciais de IA
- **Estado:** [ ]
- **REQ:** REQ-053
- **SPEC:** §7.4
- **Fase:** F3
- **Depende:** RM-F1-18
- **Implementação:** `allowBackup=false`; exclusão de chaves de export/backup; máscara na UI; aviso de risco.
- **Integração:** Manifesto Android, `ai/ui.js`, export.
- **Testes:** Testes de manifest, de export sem chaves e de UI.
- **Documentação:** tutorial (Assistente).
- **Aceite:** Chaves fora de backup/export.
- **Gate:** G3

### RM-F3-08 — Política de credenciais por plataforma documentada
- **Estado:** [ ]
- **REQ:** REQ-021
- **SPEC:** §7.1
- **Fase:** F3
- **Depende:** RM-F3-07
- **Implementação:** `docs/v2/contracts/credentials.md`: onde ficam, quem lê, riscos aceitos, gate de reavaliação (REQ-054).
- **Integração:** Threat model.
- **Testes:** Revisão.
- **Documentação:** THREAT-MODEL §6.
- **Aceite:** Política aprovada e publicada.
- **Gate:** G3

### RM-F3-09 — Endurecer permissões, protocolo e IPC do Electron
- **Estado:** [ ]
- **REQ:** REQ-055
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F3-19
- **Implementação:** Permission handler por origem; `protocol.handle('app')` com allowlist e host; IPC por igualdade exata de origem.
- **Integração:** `native/desktop/main.js`.
- **Testes:** Testes de `main.js` (RM-F3-19): permissão negada a iframe, host inválido negado.
- **Documentação:** THREAT-MODEL T-Electron.
- **Aceite:** Sem permissões/arquivos além do necessário.
- **Gate:** G3

### RM-F3-10 — `printToPDF` isolado e hooks de teste inertes
- **Estado:** [ ]
- **REQ:** REQ-055
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F3-09
- **Implementação:** Impressão sem `file://` privilegiado; `URBE_TEST_*` só sob flag de teste.
- **Integração:** `main.js`.
- **Testes:** Teste: hooks ignorados em build normal.
- **Documentação:** —
- **Aceite:** Sem superfície residual de teste em produção.
- **Gate:** G3

### RM-F3-11 — Android: `printHtml`, FileProvider e backup
- **Estado:** [ ]
- **REQ:** REQ-056
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F3-20
- **Implementação:** `allowBackup=false`; `printHtml` sem JS/base distinta; FileProvider mínimo.
- **Integração:** `AndroidManifest.xml`, `UrbeAndroidPlugin.java`, `file_paths.xml`.
- **Testes:** JUnit/instrumentado + teste do manifest.
- **Documentação:** THREAT-MODEL T-Android.
- **Aceite:** Sem exposição da origem do app na impressão.
- **Gate:** G3

### RM-F3-12 — Android: validação canônica em todas as operações e symlinks
- **Estado:** [ ]
- **REQ:** REQ-056
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F3-20
- **Implementação:** Validar caminho canônico no Java para leitura/escrita/remoção; `listTree` sem seguir symlinks.
- **Integração:** Plugin Java + `bridge.js`.
- **Testes:** JUnit com `..`, symlink e absoluto.
- **Documentação:** —
- **Aceite:** Nenhuma operação sem validação no Java.
- **Gate:** G3

### RM-F3-13 — Sandbox de embeds sem `allow-same-origin` com scripts
- **Estado:** [ ]
- **REQ:** REQ-057
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F2-02
- **Implementação:** `pages/free.js:179`: nunca `allow-scripts`+`allow-same-origin`; opções seguras por tipo.
- **Integração:** Motor de páginas.
- **Testes:** `pages-free.mjs`: embed da própria origem não obtém acesso.
- **Documentação:** THREAT-MODEL T-HTML.
- **Aceite:** Embed isolado.
- **Gate:** G3

### RM-F3-14 — CDN com versão fixa e SRI ou vendorização
- **Estado:** [ ]
- **REQ:** REQ-057
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F0-08
- **Implementação:** PDF.js/KaTeX/fontes: SRI ou `vendor/`; exports autocontidos por padrão.
- **Integração:** `app.js` (PDF.js), `pages/engine.js`.
- **Testes:** Teste de `integrity=`; export abre offline.
- **Documentação:** THIRD-PARTY.
- **Aceite:** Nenhum recurso remoto sem SRI.
- **Gate:** G3

### RM-F3-15 — Limites de importação de ZIP
- **Estado:** [ ]
- **REQ:** REQ-058
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F1-18
- **Implementação:** Limites de tamanho/contagem/razão, rejeição de absolutas/`..`/controle.
- **Integração:** Import ZIP e arquivos.
- **Testes:** Testes zip-slip e zip-bomb.
- **Documentação:** tutorial.
- **Aceite:** Importação segura.
- **Gate:** G3

### RM-F3-16 — Testes de zip-slip/bomb nas três plataformas
- **Estado:** [ ]
- **REQ:** REQ-058
- **SPEC:** §7.3
- **Fase:** F3
- **Depende:** RM-F3-15, RM-F3-19, RM-F3-20
- **Implementação:** Casos em web (vm), Electron (`vault-fs`) e Android (JUnit).
- **Integração:** —
- **Testes:** Suíte por plataforma.
- **Documentação:** THREAT-MODEL T-Import.
- **Aceite:** Comportamento verificado em web/Electron/Android.
- **Gate:** G3

### RM-F3-17 — Política de artefatos ativos criados por IA
- **Estado:** [ ]
- **REQ:** REQ-060
- **SPEC:** §7.4
- **Fase:** F3
- **Depende:** RM-F1-08
- **Implementação:** Confirmação com prévia para `.js/.css/.html`; sem aplicar automaticamente; CSS sem `url()` externo; aviso de prompt injection.
- **Integração:** `ai/tools.js`, `customize/ai-tools.js`, `ai/ui.js`.
- **Testes:** `ai-agent.mjs` ampliado.
- **Documentação:** tutorial (Assistente).
- **Aceite:** IA não ativa código/estilo sem confirmação.
- **Gate:** G3

### RM-F3-18 — Teste de esquemas de links externos
- **Estado:** [ ]
- **REQ:** REQ-010
- **SPEC:** §7.1
- **Fase:** F3
- **Depende:** RM-F0-05
- **Implementação:** Testes para `openExternal`/`openUrl`/`target=_blank` (allowlist de esquemas).
- **Integração:** Bridge.
- **Testes:** `tests/security/links.mjs`.
- **Documentação:** THREAT-MODEL T-Links.
- **Aceite:** Allowlist verificada nas três plataformas.
- **Gate:** G3

### RM-F3-19 — Testes de `main.js` e `preload.js`
- **Estado:** [ ]
- **REQ:** REQ-062
- **SPEC:** §9
- **Fase:** F3
- **Depende:** RM-F0-05
- **Implementação:** Extrair lógica testável de `main.js` (guards, permissões, protocolo) e testar com mocks do Electron; smoke real fica em RM-F4-07.
- **Integração:** `native/desktop/*`.
- **Testes:** `tests/desktop-main.mjs`, `tests/desktop-preload.mjs`.
- **Documentação:** TEST-MATRIX §3.
- **Aceite:** Guard de IPC, permissões, protocolo, `external()` cobertos.
- **Gate:** G3

### RM-F3-20 — Testes JUnit do plugin Android e SW real
- **Estado:** [ ]
- **REQ:** REQ-062
- **SPEC:** §9
- **Fase:** F3
- **Depende:** RM-F0-05
- **Implementação:** JUnit para `UrbeAndroidPlugin` (readTexts, openUrl, saveFile) e teste do `sw.js` em Playwright (install, cache, rede-primeiro).
- **Integração:** Gradle test no CI Android.
- **Testes:** `app/src/test/...`, `tests/e2e/sw.spec`.
- **Documentação:** TEST-MATRIX §3.
- **Aceite:** Plugin e SW cobertos por execução real.
- **Gate:** G3


## F4 — Plataformas, release e distribuição (G4)

### RM-F4-01 — Definir e implementar `UrbeNative.contract`
- **Estado:** [ ]
- **REQ:** REQ-076
- **SPEC:** §6.2
- **Fase:** F4
- **Depende:** RM-F2-17
- **Implementação:** `contract:{version:1,capabilities}` em `preload.js`, `bridge.js`, web; capacidades ausentes declaradas.
- **Integração:** Consumidores verificam capacidades, não plataforma.
- **Testes:** Teste de shape por adapter.
- **Documentação:** contracts/native.md.
- **Aceite:** Contrato exposto nas três plataformas.
- **Gate:** G4

### RM-F4-02 — Suíte de conformidade `native-contract` por adapter
- **Estado:** [ ]
- **REQ:** REQ-076, REQ-008
- **SPEC:** §6.1, §6.2
- **Fase:** F4
- **Depende:** RM-F4-01
- **Implementação:** `tests/native-contract.mjs` executa o mesmo conjunto contra Electron (vault-fs real), Android (mock+JUnit) e web.
- **Integração:** CI.
- **Testes:** Falha se capacidade declarada não funcionar.
- **Documentação:** PLATFORMS §2.
- **Aceite:** Paridade comprovada; diferenças declaradas.
- **Gate:** G4

### RM-F4-03 — Paridade de exportação no Electron (`saveFile`)
- **Estado:** [ ]
- **REQ:** REQ-079
- **SPEC:** §6.2
- **Fase:** F4
- **Depende:** RM-F4-01
- **Implementação:** `fs:saveFile` com diálogo nativo; `bridge` usa em exports HTML/PDF/ZIP.
- **Integração:** `main.js`, `preload.js`.
- **Testes:** Teste de `saveFile` (caminho, cancelamento).
- **Documentação:** tutorial.
- **Aceite:** Exportar salva no Windows como no Android/web.
- **Gate:** G4

### RM-F4-04 — Scaffold Playwright e fixtures E2E
- **Estado:** [ ]
- **REQ:** REQ-061
- **SPEC:** §9
- **Fase:** F4
- **Depende:** RM-F1-01
- **Implementação:** `tests/e2e/` com Playwright/Chromium, servidor estático, vault fixture e utilitários; `npm run test:e2e`.
- **Integração:** CI job dedicado.
- **Testes:** Smoke: abre app com fixture.
- **Documentação:** CONTRIBUTING §E2E.
- **Aceite:** E2E executa localmente e no CI.
- **Gate:** G4

### RM-F4-05 — Cenários E2E críticos (web)
- **Estado:** [ ]
- **REQ:** REQ-061, REQ-016
- **SPEC:** §9
- **Fase:** F4
- **Depende:** RM-F4-04
- **Implementação:** Abrir → criar nota → salvar → recarregar → ver na cidade → aprovar plugin → importar ZIP → exportar → recuperar (lixeira/versões).
- **Integração:** Cada cenário mapeado a gate em `GATES.md`.
- **Testes:** Cenários passam em Chromium (D1) e emulação D2.
- **Documentação:** GATES.md.
- **Aceite:** Fluxo crítico verificado (S4).
- **Gate:** G4

### RM-F4-06 — E2E de gestos e cidade (paridade do toque)
- **Estado:** [ ]
- **REQ:** REQ-061
- **SPEC:** §9
- **Fase:** F4
- **Depende:** RM-F4-05, RM-F2-06
- **Implementação:** Arrastar, zoom, toque longo, seleção; renderer canônico.
- **Integração:** —
- **Testes:** Cenários E2E de toque emulado.
- **Documentação:** —
- **Aceite:** Interação da cidade coberta.
- **Gate:** G4

### RM-F4-07 — Smoke E2E do Electron
- **Estado:** [ ]
- **REQ:** REQ-061
- **SPEC:** §9
- **Fase:** F4
- **Depende:** RM-F4-04, RM-F3-10
- **Implementação:** Playwright `_electron`: abre app com vault fixture, cria nota, reabre.
- **Integração:** `URBE_TEST_*` só em flag de teste.
- **Testes:** Passa no Windows CI.
- **Documentação:** —
- **Aceite:** App desktop verificado por execução real.
- **Gate:** G4

### RM-F4-08 — `GATES.md`: teste → gate
- **Estado:** [ ]
- **REQ:** REQ-016
- **SPEC:** §9, §12
- **Fase:** F4
- **Depende:** RM-F4-05
- **Implementação:** Mapa de cada teste/E2E/perf/manual para o gate G0–G6 e nome do job de CI.
- **Integração:** TRACEABILITY consome.
- **Testes:** `check-traceability` valida que testes citados existem.
- **Documentação:** GATES.md.
- **Aceite:** Todo gate tem testes reproduzíveis associados.
- **Gate:** G4

### RM-F4-09 — Smoke do instalador Windows e do APK no CI
- **Estado:** [ ]
- **REQ:** REQ-069
- **SPEC:** §9
- **Fase:** F4
- **Depende:** RM-F4-07
- **Implementação:** Job que instala/extrai o NSIS e inicia com fixture; APK em emulador com vault fixture.
- **Integração:** `app.yml`/`release.yml`.
- **Testes:** Job verde em PR e antes do release.
- **Documentação:** PLATFORMS §3.
- **Aceite:** Artefatos empacotados iniciam e abrem vault.
- **Gate:** G4

### RM-F4-10 — Política de release, canais e rollback
- **Estado:** [ ]
- **REQ:** REQ-081
- **SPEC:** §10.2
- **Fase:** F4
- **Depende:** RM-F0-14
- **Implementação:** `docs/v2/RELEASE.md` (SemVer, canais, checklist, rollback) e dry-run comprovado.
- **Integração:** `release.yml`.
- **Testes:** Dry-run de release e de rollback em repositório de teste.
- **Documentação:** RELEASE.md.
- **Aceite:** Procedimento executado com sucesso em dry-run.
- **Gate:** G4

### RM-F4-11 — `THIRD-PARTY-NOTICES`
- **Estado:** [ ]
- **REQ:** REQ-080
- **SPEC:** §10.3
- **Fase:** F4
- **Depende:** RM-F2-03
- **Implementação:** Avisos de KaTeX, JSZip (MIT), pako, PDF.js, Electron/Chromium, Capacitor, chokidar, electron-updater.
- **Integração:** Empacotado no instalador/APK/`vendor`.
- **Testes:** `tools/check-license.mjs` valida cobertura das dependências.
- **Documentação:** THIRD-PARTY-NOTICES.
- **Aceite:** Toda dependência embutida atribuída.
- **Gate:** G4

### RM-F4-12 — `LICENSE` fonte-disponível/proprietária
- **Estado:** [!]
- **REQ:** REQ-080, REQ-017
- **SPEC:** §10.3
- **Fase:** F4
- **Depende:** RM-F4-11
- **Implementação:** Texto final de `LICENSE`, `package.json` `license` coerente, README.
- **Integração:** `check-license`.
- **Testes:** `check-license` verde.
- **Documentação:** ADR-0003.
- **Aceite:** BLOQUEADO: texto/permissões dependem do proprietário (OD-03). Gate G4 não fecha sem ele.
- **Gate:** G4

### RM-F4-13 — Registro da decisão de distribuição
- **Estado:** [ ]
- **REQ:** REQ-017
- **SPEC:** §10.3
- **Fase:** F4
- **Depende:** RM-F4-12
- **Implementação:** Publicar política de distribuição (canais, quem pode redistribuir) e vincular ao release.
- **Integração:** RELEASE.md.
- **Testes:** Revisão.
- **Documentação:** README/RELEASE.
- **Aceite:** Distribuição pública só após licença.
- **Gate:** G4

### RM-F4-14 — Guia de migração 1.x→2.x e aviso no app
- **Estado:** [ ]
- **REQ:** REQ-082
- **SPEC:** §10.5
- **Fase:** F4
- **Depende:** RM-F1-05, RM-F1-07
- **Implementação:** `docs/v2/MIGRATION.md`, tutorial e diálogo de primeira abertura pós-migração (backup, `*.v2.json`, reaprovação de plugins, SmartScreen/APK).
- **Integração:** Fluxo de migração.
- **Testes:** E2E: abrir fixture 1.x mostra aviso e cria backup.
- **Documentação:** MIGRATION.md; tutorial.
- **Aceite:** Usuário informado e com rollback.
- **Gate:** G4

### RM-F4-15 — Matriz de capacidades por plataforma verificada
- **Estado:** [ ]
- **REQ:** REQ-008
- **SPEC:** §6.1
- **Fase:** F4
- **Depende:** RM-F4-02
- **Implementação:** Gerar tabela de paridade a partir de `contract.capabilities` e publicar em PLATFORMS.md.
- **Integração:** Docs.
- **Testes:** `check` compara tabela e contrato.
- **Documentação:** PLATFORMS.md.
- **Aceite:** Diferenças de plataforma só via contrato.
- **Gate:** G4


## F5 — Performance (G5)

### RM-F5-01 — Gate de regressão de performance no CI
- **Estado:** [ ]
- **REQ:** REQ-009
- **SPEC:** §8
- **Fase:** F5
- **Depende:** RM-F0-12
- **Implementação:** `npm run perf:check` compara medição atual com budgets e falha acima da tolerância.
- **Integração:** Job CI (noturno + release).
- **Testes:** Teste com regressão injetada.
- **Documentação:** perf/README.md.
- **Aceite:** Regressões relevantes bloqueiam release.
- **Gate:** G5

### RM-F5-02 — Derivar e aprovar budgets
- **Estado:** [ ]
- **REQ:** REQ-071
- **SPEC:** §8
- **Fase:** F5
- **Depende:** RM-F0-12
- **Implementação:** `docs/v2/perf/budgets.json` (relativos provisórios → absolutos aprovados) por classe D1/D2/D3.
- **Integração:** Consumido por `perf:check`.
- **Testes:** Validação do schema; aprovação registrada em AGENTSCHAT.
- **Documentação:** PERFORMANCE §5.
- **Aceite:** Budgets aprovados pelo proprietário.
- **Gate:** G5

### RM-F5-03 — Persistência incremental
- **Estado:** [ ]
- **REQ:** REQ-072
- **SPEC:** §8
- **Fase:** F5
- **Depende:** RM-F1-13, RM-F1-12
- **Implementação:** Diff por documento/side-file sujo; `desired()` deixa de recomputar o vault.
- **Integração:** `WorkspacePersistence`.
- **Testes:** Medir bytes/tempo por flush em S/M/L; O(1) em bytes.
- **Documentação:** PERFORMANCE §6.
- **Aceite:** Flush independente do tamanho do vault.
- **Gate:** G5

### RM-F5-04 — Busca indexada e com debounce
- **Estado:** [ ]
- **REQ:** REQ-073
- **SPEC:** §8
- **Fase:** F5
- **Depende:** RM-F2-02
- **Implementação:** Índice pré-normalizado em `KnowledgeIndex`; quick-open usa índice; debounce e limite.
- **Integração:** `ui/quick-open.js`.
- **Testes:** `search-editor.mjs` + medição p95 em L.
- **Documentação:** —
- **Aceite:** Custo por consulta independente do conteúdo.
- **Gate:** G5

### RM-F5-05 — Carregamento sob demanda de módulos pesados
- **Estado:** [ ]
- **REQ:** REQ-074
- **SPEC:** §8
- **Fase:** F5
- **Depende:** RM-F0-08, RM-F3-03
- **Implementação:** Manifesto marca módulos `lazy` (Studio, IA, KaTeX, tutorial content) e loader injeta script quando necessário; compatível com CSP e SW.
- **Integração:** `index.html` gerado do manifesto.
- **Testes:** TTI melhora vs baseline; E2E funcional.
- **Documentação:** ADR-0001.
- **Aceite:** Menos JS no boot sem build.
- **Gate:** G5

### RM-F5-06 — Load do vault com concorrência limitada
- **Estado:** [ ]
- **REQ:** REQ-074
- **SPEC:** §8
- **Fase:** F5
- **Depende:** RM-F1-12
- **Implementação:** `workspace.js:9-18` lê com pool (N configurável) em vez de sequencial.
- **Integração:** Adapters.
- **Testes:** Medir abertura em M/L (web IDB, Electron).
- **Documentação:** PERFORMANCE §2.
- **Aceite:** Abertura mais rápida sem perder ordem/consistência.
- **Gate:** G5

### RM-F5-07 — Teto e compactação de histórico
- **Estado:** [ ]
- **REQ:** REQ-075
- **SPEC:** §8
- **Fase:** F5
- **Depende:** RM-F1-05
- **Implementação:** Limites por documento e total; compactação; gravação incremental de `history.v2.json`.
- **Integração:** History service.
- **Testes:** Teste de teto; migração de history v1.
- **Documentação:** tutorial (Versões).
- **Aceite:** Tamanho de histórico limitado e previsível.
- **Gate:** G5

### RM-F5-08 — Validar orçamento pós-migração
- **Estado:** [ ]
- **REQ:** REQ-009
- **SPEC:** §8
- **Fase:** F5
- **Depende:** RM-F5-03, RM-F5-04, RM-F5-05, RM-F5-06, RM-F5-07
- **Implementação:** Reexecutar harness; publicar `baseline-2.0.json` e comparar com 1.8.2.
- **Integração:** PERFORMANCE.md.
- **Testes:** `perf:check` verde.
- **Documentação:** PERFORMANCE.md.
- **Aceite:** 2.0 dentro dos budgets aprovados.
- **Gate:** G5


## F6 — UX, auditoria e release 2.0 (G6)

### RM-F6-01 — Publicar e verificar critérios de sucesso S1–S10
- **Estado:** [ ]
- **REQ:** REQ-083
- **SPEC:** §11
- **Fase:** F6
- **Depende:** RM-F4-05, RM-F5-08
- **Implementação:** `docs/v2/SUCCESS.md` com verificação e gate por critério; execução consolidada.
- **Integração:** Release checklist.
- **Testes:** Cada S# aponta teste/gate executado.
- **Documentação:** SUCCESS.md.
- **Aceite:** S1–S10 verificados.
- **Gate:** G6

### RM-F6-02 — Tutorial e documentação de usuário sincronizados
- **Estado:** [ ]
- **REQ:** REQ-084
- **SPEC:** §11
- **Fase:** F6
- **Depende:** RM-F4-14, RM-F3-06
- **Implementação:** Atualizar `tutorial/` (migração, plugins, backup, recuperação, exportação) e `build-tutorial --check` com tópicos obrigatórios.
- **Integração:** `content.js` regenerado.
- **Testes:** `tutorial.mjs` ampliado.
- **Documentação:** tutorial/.
- **Aceite:** Tutorial cobre todos os fluxos novos.
- **Gate:** G6

### RM-F6-03 — Tela única de Recuperação
- **Estado:** [ ]
- **REQ:** REQ-085
- **SPEC:** §11
- **Fase:** F6
- **Depende:** RM-F1-07, RM-F1-16
- **Implementação:** Configurações → Recuperação: journal, lixeira, versões, backups de migração, diagnóstico e modo seguro.
- **Integração:** UI settings.
- **Testes:** E2E: restaurar backup e item da lixeira.
- **Documentação:** tutorial (Solução de problemas).
- **Aceite:** Recuperação acessível em um lugar.
- **Gate:** G6

### RM-F6-04 — Linha de base de acessibilidade
- **Estado:** [ ]
- **REQ:** REQ-086
- **SPEC:** §11
- **Fase:** F6
- **Depende:** RM-F4-05
- **Implementação:** `docs/v2/A11Y.md` + axe no E2E + checklist manual (teclado, foco, contraste AA, ARIA).
- **Integração:** E2E.
- **Testes:** Relatório axe versionado.
- **Documentação:** A11Y.md.
- **Aceite:** Baseline medida e publicada.
- **Gate:** G6

### RM-F6-05 — Corrigir falhas de acessibilidade dos fluxos essenciais
- **Estado:** [ ]
- **REQ:** REQ-086
- **SPEC:** §11
- **Fase:** F6
- **Depende:** RM-F6-04
- **Implementação:** Ajustes de foco, rótulos, contraste nos fluxos essenciais.
- **Integração:** UI.
- **Testes:** axe sem violações críticas nos fluxos essenciais.
- **Documentação:** A11Y.md.
- **Aceite:** Fluxos essenciais acessíveis por teclado.
- **Gate:** G6

### RM-F6-06 — Auditoria final de cobertura
- **Estado:** [ ]
- **REQ:** REQ-024
- **SPEC:** §2
- **Fase:** F6
- **Depende:** RM-F6-01
- **Implementação:** Reexecutar `check-traceability`, atualizar `AUDIT-COVERAGE.md` e revisar REQs novos surgidos na execução.
- **Integração:** Gate de release.
- **Testes:** Verificador verde; revisão manual.
- **Documentação:** AUDIT-COVERAGE.md.
- **Aceite:** Cobertura 100% dos REQ IMPLEMENTAR.
- **Gate:** G6

### RM-F6-07 — Auditoria de Definition of Done
- **Estado:** [ ]
- **REQ:** REQ-005
- **SPEC:** §10.1
- **Fase:** F6
- **Depende:** RM-F6-06
- **Implementação:** Verificar todos os itens `[x]` contra DoD; reabrir os que falham.
- **Integração:** ROADMAP.
- **Testes:** Relatório de auditoria.
- **Documentação:** ROADMAP.
- **Aceite:** Nenhum item concluído sem evidência.
- **Gate:** G6

### RM-F6-08 — Relatório final de rastreabilidade
- **Estado:** [ ]
- **REQ:** REQ-002
- **SPEC:** §1.2
- **Fase:** F6
- **Depende:** RM-F6-07
- **Implementação:** Regerar TRACEABILITY.md com evidência (commit/CI) por item.
- **Integração:** Release notes.
- **Testes:** `gen-traceability --check`.
- **Documentação:** TRACEABILITY.md.
- **Aceite:** Cadeia REQ→teste/gate comprovada.
- **Gate:** G6

### RM-F6-09 — Sincronização final docs×código×release
- **Estado:** [ ]
- **REQ:** REQ-004
- **SPEC:** §1.2
- **Fase:** F6
- **Depende:** RM-F0-18
- **Implementação:** `npm run check` verde no candidato a release; CHANGELOG e versão coerentes.
- **Integração:** Release.
- **Testes:** CI.
- **Documentação:** CHANGELOG.
- **Aceite:** Nada divergente no release.
- **Gate:** G6

### RM-F6-10 — Auditoria de features novas versus ledger
- **Estado:** [ ]
- **REQ:** REQ-011
- **SPEC:** §1.2, §11
- **Fase:** F6
- **Depende:** RM-F6-06
- **Implementação:** Revisar mudanças de produto desde a 1.8.2: todas vinculadas a REQ.
- **Integração:** Ledger.
- **Testes:** Relatório.
- **Documentação:** REQUIREMENTS.md.
- **Aceite:** Nenhuma feature sem REQ.
- **Gate:** G6

### RM-F6-11 — Auditoria de dívida e legacy final
- **Estado:** [ ]
- **REQ:** REQ-012
- **SPEC:** §1.2
- **Fase:** F6
- **Depende:** RM-F2-19
- **Implementação:** Confirmar `check-debt` no teto final e LEGACY-MAP zerado ou justificado.
- **Integração:** Release.
- **Testes:** `check-debt`.
- **Documentação:** LEGACY-MAP.
- **Aceite:** Sem dívida nova; legacy resolvido.
- **Gate:** G6

### RM-F6-12 — Release candidato 2.0
- **Estado:** [ ]
- **REQ:** REQ-006, REQ-081
- **SPEC:** §10.2
- **Fase:** F6
- **Depende:** RM-F6-09, RM-F4-12, RM-F4-13
- **Implementação:** Tag `v2.0.0-beta.N`/`v2.0.0` seguindo RELEASE.md; publicação após G0–G6.
- **Integração:** `release.yml`.
- **Testes:** CI + smoke instalador/APK.
- **Documentação:** CHANGELOG/RELEASE.
- **Aceite:** Release publicado só com todos os gates.
- **Gate:** G6
