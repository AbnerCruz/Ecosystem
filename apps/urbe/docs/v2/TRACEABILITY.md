# Rastreabilidade — Urbe 2.0

> **Gerado por `tools/gen-traceability.mjs`** a partir de `REQUIREMENTS.md`, `SPEC.md` e `ROADMAP.md`. Não editar à mão: altere a fonte e regenere.
> Verificação: `node tools/check-traceability.mjs` e `node tools/gen-traceability.mjs --check`.

## Matriz REQ → SPEC → fase → item do ROADMAP → teste/gate

| REQ | SPEC | Fase | Item | Estado do item | Teste (resumo) | Gate |
|---|---|---|---|---|---|---|
| REQ-001 | §1.2 | F0 | RM-F0-04 — Alinhar ARCHITECTURE.md à realidade e ao alvo | [ ] | Revisão manual + `tools/check-modules.mjs` valida referências a módulos citados. | G0 |
| REQ-002 | §1.2 | F0 | RM-F0-02 — Verificador e gerador de rastreabilidade | [x] | Execução local: `node tools/check-traceability.mjs` retorna 0; teste negativo em `tests/traceability.mjs` (REQ removido → falha). | G0 |
| REQ-002 | §1.2 | F6 | RM-F6-08 — Relatório final de rastreabilidade | [ ] | `gen-traceability --check`. | G6 |
| REQ-003 | §3.1 | F2 | RM-F2-02 — Harness de teste de produção do monólito | [ ] | Cobre boot, abrir vault fixture, criar nota, mundo. | G2 |
| REQ-003 | §3.1 | F2 | RM-F2-19 — `app.js` reduzido a composição/bootstrap | [ ] | Métrica: sem regra de domínio (revisão + lint de imports). | G2 |
| REQ-004 | §1.2 | F0 | RM-F0-18 — Comando único `npm run check` (sincronização docs×código) | [ ] | Falha em qualquer sub-check falha o comando. | G0 |
| REQ-004 | §1.2 | F6 | RM-F6-09 — Sincronização final docs×código×release | [ ] | CI. | G6 |
| REQ-005 | §1.2 | F0 | RM-F0-17 — Definition of Done no PR template e issue templates | [ ] | Revisão manual + `check-traceability` valida formato de RM-id citado (opcional). | G0 |
| REQ-005 | §1.2 | F6 | RM-F6-07 — Auditoria de Definition of Done | [ ] | Relatório de auditoria. | G6 |
| REQ-006 | §10.2 | F0 | RM-F0-14 — Separar integração e publicação (release por tag) | [ ] | Dry-run em branch de teste: push em `main` não publica; tag publica em repositório de teste. | G0 |
| REQ-006 | §10.2 | F6 | RM-F6-12 — Release candidato 2.0 | [ ] | CI + smoke instalador/APK. | G6 |
| REQ-007 | §1.2 | F1 | RM-F1-25 — Testes de invariante local-first/offline e integridade de notas | [ ] | Falha se `fetch` for chamado no fluxo essencial. | G1 |
| REQ-008 | §6.1 | F4 | RM-F4-02 — Suíte de conformidade `native-contract` por adapter | [ ] | Falha se capacidade declarada não funcionar. | G4 |
| REQ-008 | §6.1 | F4 | RM-F4-15 — Matriz de capacidades por plataforma verificada | [ ] | `check` compara tabela e contrato. | G4 |
| REQ-009 | §8 | F5 | RM-F5-01 — Gate de regressão de performance no CI | [ ] | Teste com regressão injetada. | G5 |
| REQ-009 | §8 | F5 | RM-F5-08 — Validar orçamento pós-migração | [ ] | `perf:check` verde. | G5 |
| REQ-010 | §7.1 | F3 | RM-F3-01 — Índice de testes de segurança e rastreio do threat model | [ ] | Teste negativo: ameaça sem teste falha. | G3 |
| REQ-010 | §7.1 | F3 | RM-F3-18 — Teste de esquemas de links externos | [ ] | `tests/security/links.mjs`. | G3 |
| REQ-011 | §1.2 | F0 | RM-F0-17 — Definition of Done no PR template e issue templates | [ ] | Revisão manual + `check-traceability` valida formato de RM-id citado (opcional). | G0 |
| REQ-011 | §1.2 | F6 | RM-F6-10 — Auditoria de features novas versus ledger | [ ] | Relatório. | G6 |
| REQ-012 | §1.2 | F0 | RM-F0-13 — Gate de dívida (`tools/check-debt.mjs`) | [ ] | `tests/debt.mjs` (injeção de sobrescrita falha). | G0 |
| REQ-012 | §1.2 | F6 | RM-F6-11 — Auditoria de dívida e legacy final | [ ] | `check-debt`. | G6 |
| REQ-013 | §5.2 | F1 | RM-F1-14 — IDs estáveis de regiões, construções e vínculos | [ ] | Fixtures: rename externo mantém região/asset; mapa v4 legível pela 1.8.2 (teste de compat). | G1 |
| REQ-013 | §5.2 | F1 | RM-F1-24 — Regra: relações internas novas usam ID | [ ] | Teste negativo com relação por path. | G1 |
| REQ-014 | §5.2 | F1 | RM-F1-09 — Modelo de artefatos aplicado (indexação e roteamento) | [ ] | Fixtures: plugin/tema/página não aparecem como nota; abrir cada tipo roteia corretamente. | G1 |
| REQ-015 | §3.1 | F0 | RM-F0-09 — Validação automática de boundaries de camadas | [ ] | Teste negativo com dependência proibida injetada. | G0 |
| REQ-016 | §9 | F4 | RM-F4-05 — Cenários E2E críticos (web) | [ ] | Cenários passam em Chromium (D1) e emulação D2. | G4 |
| REQ-016 | §9 | F4 | RM-F4-08 — `GATES.md`: teste → gate | [ ] | `check-traceability` valida que testes citados existem. | G4 |
| REQ-017 | §10.3 | F4 | RM-F4-12 — `LICENSE` de todos os direitos reservados | [ ] | `check-license` verde. | G4 |
| REQ-017 | §10.3 | F4 | RM-F4-13 — Registro da decisão de distribuição | [ ] | Revisão. | G4 |
| REQ-018 | §1.2 | F0 | RM-F0-16 — Ciclo de vida de branches e backlog de issues | [ ] | Lista de branches remotas após limpeza só contém `main`, `chore/*` e trabalho ativo. | G0 |
| REQ-019 | §10.2 | F0 | RM-F0-06 — Fonte única de versão (`tools/version.mjs`) | [ ] | `tests/version.mjs` (drift injetado falha); `consistency.mjs` adaptado. | G0 |
| REQ-020 | §7.2 | F3 | RM-F3-06 — Coerência documental do modelo de plugins | [ ] | `build-tutorial --check` com tópicos obrigatórios. | G3 |
| REQ-021 | §7.1 | F3 | RM-F3-08 — Política de credenciais por plataforma documentada | [ ] | Revisão. | G3 |
| REQ-022 | §3.1 | F2 | RM-F2-01 — Avaliação de hotspots por responsabilidade e acoplamento | [ ] | Script `tools/hotspots.mjs` reproduz métricas. | G2 |
| REQ-023 | §5.1 | F1 | RM-F1-03 — Catálogo de formatos verificado por código | [ ] | Teste negativo: novo arquivo sem linha no catálogo falha. | G1 |
| REQ-023 | §5.1 | F1 | RM-F1-26 — Política de migração por formato (contrato final) | [ ] | `check-catalog` valida presença de política por formato. | G1 |
| REQ-024 | §2 | F0 | RM-F0-02 — Verificador e gerador de rastreabilidade | [x] | Execução local: `node tools/check-traceability.mjs` retorna 0; teste negativo em `tests/traceability.mjs` (REQ removido → falha). | G0 |
| REQ-024 | §2 | F6 | RM-F6-06 — Auditoria final de cobertura | [ ] | Verificador verde; revisão manual. | G6 |
| REQ-025 | §3.2 | F0 | RM-F0-07 — Gerar `src/modules.json` a partir do `index.html` atual | [ ] | `tests/modules-manifest.mjs`: manifesto reproduz a ordem e o conjunto de `index.html`/`sw.js`. | G0 |
| REQ-025 | §3.2 | F0 | RM-F0-08 — `check-modules` e derivação de `index.html`/`sw.js`/`build-www` | [ ] | Testes negativos: ordem inválida, ciclo, arquivo fora do manifesto. | G0 |
| REQ-026 | §3.3 | F2 | RM-F2-05 — Ligar renderer canônico (paridade visual) | [ ] | Paridade de frame em fixture + `production-order` atualizado; perf não regride (baseline). | G2 |
| REQ-026 | §3.3 | F2 | RM-F2-06 — Ligar controlador de toque canônico | [ ] | `runtime-adapters.mjs` + E2E de gesto (RM-F4-06). | G2 |
| REQ-026 | §3.3 | F2 | RM-F2-07 — Ligar grafo de ruas canônico | [ ] | `world-system.mjs` + testes de rota com fixtures. | G2 |
| REQ-026 | §3.3 | F2 | RM-F2-08 — Remover desenho e laço duplicados de `app.js` | [ ] | Paridade visual; `check-debt` cai; testes verdes. | G2 |
| REQ-027 | §3.3 | F2 | RM-F2-09 — Extrair renderMarkdown para o editor | [ ] | Testes de renderização (tabelas, callouts, math, links, sanitização) + regressão `composition`. | G2 |
| REQ-027 | §3.3 | F2 | RM-F2-10 — Extrair editor Visual e conversão | [ ] | Round-trip Markdown↔Visual em fixtures; sem ressuscitar nota apagada. | G2 |
| REQ-027 | §3.3 | F2 | RM-F2-11 — Extrair autocompletar de wikilinks e barra de formatação | [ ] | Testes de sugestão/inserção; E2E. | G2 |
| REQ-028 | §3.3 | F1 | RM-F1-10 — Contrato de adapters de persistência e testes | [ ] | Suíte roda contra mock e contra `vault-fs.js`. | G1 |
| REQ-028 | §3.3 | F1 | RM-F1-11 — Adapters IDB e FSA extraídos de `FS/Disco/DBK` | [ ] | Suíte de contrato + fixtures + `integration-runtime`. | G1 |
| REQ-028 | §3.3 | F1 | RM-F1-12 — Adapters Electron/Android e autoridade única de escrita | [ ] | `native-bridge.mjs`, `native-android.mjs` + suíte de contrato. | G1 |
| REQ-029 | §3.3 | F2 | RM-F2-08 — Remover desenho e laço duplicados de `app.js` | [ ] | Paridade visual; `check-debt` cai; testes verdes. | G2 |
| REQ-029 | §3.3 | F2 | RM-F2-12 — Mundo derivado só da projeção (L3, L4) | [ ] | Fixtures abrem com mesmo resultado; sem marcador de texto. | G2 |
| REQ-029 | §3.3 | F2 | RM-F2-13 — Eliminar cadeia `estadoDesejado` e `abrirCidade` | [ ] | Fixtures; `check-debt` cai. | G2 |
| REQ-029 | §3.3 | F2 | RM-F2-14 — Eliminar demais cadeias e duplicadas; `V21_VERSION` | [ ] | `check-debt` = 0 sobrescritas/duplicadas; testes verdes. | G2 |
| REQ-029 | §3.3 | F2 | RM-F2-19 — `app.js` reduzido a composição/bootstrap | [ ] | Métrica: sem regra de domínio (revisão + lint de imports). | G2 |
| REQ-030 | §3.3 | F2 | RM-F2-04 — Remover resíduos de UI e gancho `window.URBE` | [ ] | Teste de UI sem referência; grep zero. | G2 |
| REQ-030 | §3.3 | F2 | RM-F2-12 — Mundo derivado só da projeção (L3, L4) | [ ] | Fixtures abrem com mesmo resultado; sem marcador de texto. | G2 |
| REQ-030 | §3.3 | F2 | RM-F2-15 — Remover adapters `legacy.runtime` e `legacy.documents` | [ ] | Testes de produção por consumidor (não existiam). | G2 |
| REQ-030 | §3.3 | F2 | RM-F2-16 — Extrair hosts `world.custom`, `city.layout`, `world.life.host`, `workspace.storage`; remover wrapper `document.open` | [ ] | Testes de contrato de `world.custom`; `world-life.mjs`, `customize.mjs`. | G2 |
| REQ-031 | §3.3 | F2 | RM-F2-03 — Mover JSZip para `vendor/jszip` | [ ] | `consistency.mjs` e `modules-manifest` verdes; import/export ZIP funciona. | G2 |
| REQ-032 | §3.4 | F2 | RM-F2-16 — Extrair hosts `world.custom`, `city.layout`, `world.life.host`, `workspace.storage`; remover wrapper `document.open` | [ ] | Testes de contrato de `world.custom`; `world-life.mjs`, `customize.mjs`. | G2 |
| REQ-032 | §3.4 | F2 | RM-F2-17 — Contratos públicos versionados | [ ] | `tests/contracts.mjs`. | G2 |
| REQ-035 | §5.1 | F1 | RM-F1-05 — Proteção forward: journal, history, trash, compositions | [ ] | Fixture `futuro-desconhecido`: bytes inalterados após abrir/salvar; teste de coexistência v1/v2. | G1 |
| REQ-035 | §5.1 | F1 | RM-F1-06 — Proteção forward: mapa, tema, páginas, blocos, modelos | [ ] | Fixture futuro: nada reescrito; UI mostra aviso. | G1 |
| REQ-036 | §5.1 | F1 | RM-F1-04 — `vault.json` e detecção de versão do vault | [ ] | Fixtures: 1.x sem `vault.json`, 2.x, futuro maior. | G1 |
| REQ-037 | §5.1 | F1 | RM-F1-01 — Criar fixtures de vaults históricos | [ ] | Cada fixture abre no código 1.8.2 sem erro (teste de caracterização). | G1 |
| REQ-037 | §5.1 | F1 | RM-F1-02 — Harness de abertura/migração de fixtures | [ ] | Rodar contra 1.8.2 (baseline) e depois a cada item. | G1 |
| REQ-038 | §5.1 | F1 | RM-F1-07 — Motor de backup pré-migração e restauração | [ ] | Testes: backup íntegro, restauração byte a byte, migração repetida não duplica. | G1 |
| REQ-039 | §5.2 | F1 | RM-F1-08 — Fonte única de tipos de artefato | [ ] | `tests/artifacts.mjs` (tabela de casos); `grep` de listas antigas retorna 0. | G1 |
| REQ-040 | §5.2 | F1 | RM-F1-13 — Escritor único do mapa e leitura de `mapa.v` | [ ] | Fixtures mapa v1/v2/v4; teste de dupla escrita (não há); assinatura de binários preservada. | G1 |
| REQ-041 | §5.2 | F1 | RM-F1-14 — IDs estáveis de regiões, construções e vínculos | [ ] | Fixtures: rename externo mantém região/asset; mapa v4 legível pela 1.8.2 (teste de compat). | G1 |
| REQ-042 | §5.2 | F1 | RM-F1-15 — Sidecar de identidade e reconciliação | [ ] | Fixtures: rename externo, cópia de vault sem `.urbe/`, cópias idênticas (ambiguidade tratada). | G1 |
| REQ-042 | §5.2 | F1 | RM-F1-16 — GC de órfãos em history/trash/compositions | [ ] | Fixture com órfãos; dry-run e execução. | G1 |
| REQ-043 | §5.2 | F1 | RM-F1-17 — Versão de mundo e reorganização com backup/desfazer | [ ] | Fixture `v1-mundo-antigo`, `v1-cidades-mescladas`: backup criado, posições restauráveis. | G1 |
| REQ-044 | §5.2 | F1 | RM-F1-18 — Export ZIP com manifesto e estado local | [ ] | Testes: hash íntegro, adulteração detectada, ausência de chaves. | G1 |
| REQ-045 | §5.2 | F1 | RM-F1-19 — Migração multi-cidade idempotente | [ ] | Fixture `v1-cidades-mescladas`: 3 boots consecutivos, mesmo resultado. | G1 |
| REQ-046 | §5.2 | F1 | RM-F1-20 — Escrita recuperável na web e integridade do modo IDB | [ ] | Simulação de falha no meio da escrita; export do IDB reproduz vault. | G1 |
| REQ-047 | §5.2 | F1 | RM-F1-21 — Estado local por vault e tolerância a órfãos | [ ] | Trocar de vault não reabre abas alheias; orphan não lança. | G1 |
| REQ-048 | §5.2 | F1 | RM-F1-22 — Auditoria e destino de `aiLocal` e campos sem consumidor | [ ] | Fixtures com `aiLocal` preservados ou migrados sem perda. | G1 |
| REQ-049 | §5.2 | F1 | RM-F1-23 — Páginas preservam chaves desconhecidas e versão | [ ] | `pages.mjs` ampliado: página v2 fictícia sobrevive a open/save. | G1 |
| REQ-050 | §7.3 | F3 | RM-F3-02 — Externalizar script inline e definir política CSP | [ ] | Aparência aplicada antes do primeiro desenho (medição TTI não regride). | G3 |
| REQ-050 | §7.3 | F3 | RM-F3-03 — Aplicar e testar CSP em web, Electron e Capacitor | [ ] | Testes: violação bloqueada; app funciona; E2E sem violações. | G3 |
| REQ-051 | §7.2 | F3 | RM-F3-04 — UI de aprovação mostra código, hash e alcance | [ ] | Teste de UI e de diff em mudança de código. | G3 |
| REQ-051 | §7.2 | F3 | RM-F3-05 — SHA-256 obrigatório e reaprovação; ferramentas de IA com escrita | [ ] | `customize.mjs` ampliado. | G3 |
| REQ-053 | §7.4 | F3 | RM-F3-07 — Mitigações de credenciais de IA | [ ] | Testes de manifest, de export sem chaves e de UI. | G3 |
| REQ-055 | §7.3 | F3 | RM-F3-09 — Endurecer permissões, protocolo e IPC do Electron | [ ] | Testes de `main.js` (RM-F3-19): permissão negada a iframe, host inválido negado. | G3 |
| REQ-055 | §7.3 | F3 | RM-F3-10 — `printToPDF` isolado e hooks de teste inertes | [ ] | Teste: hooks ignorados em build normal. | G3 |
| REQ-056 | §7.3 | F3 | RM-F3-11 — Android: `printHtml`, FileProvider e backup | [ ] | JUnit/instrumentado + teste do manifest. | G3 |
| REQ-056 | §7.3 | F3 | RM-F3-12 — Android: validação canônica em todas as operações e symlinks | [ ] | JUnit com `..`, symlink e absoluto. | G3 |
| REQ-057 | §7.3 | F3 | RM-F3-13 — Sandbox de embeds sem `allow-same-origin` com scripts | [ ] | `pages-free.mjs`: embed da própria origem não obtém acesso. | G3 |
| REQ-057 | §7.3 | F3 | RM-F3-14 — CDN com versão fixa e SRI ou vendorização | [ ] | Teste de `integrity=`; export abre offline. | G3 |
| REQ-058 | §7.3 | F3 | RM-F3-15 — Limites de importação de ZIP | [ ] | Testes zip-slip e zip-bomb. | G3 |
| REQ-058 | §7.3 | F3 | RM-F3-16 — Testes de zip-slip/bomb nas três plataformas | [ ] | Suíte por plataforma. | G3 |
| REQ-060 | §7.4 | F3 | RM-F3-17 — Política de artefatos ativos criados por IA | [ ] | `ai-agent.mjs` ampliado. | G3 |
| REQ-061 | §9 | F4 | RM-F4-04 — Scaffold Playwright e fixtures E2E | [ ] | Smoke: abre app com fixture. | G4 |
| REQ-061 | §9 | F4 | RM-F4-05 — Cenários E2E críticos (web) | [ ] | Cenários passam em Chromium (D1) e emulação D2. | G4 |
| REQ-061 | §9 | F4 | RM-F4-06 — E2E de gestos e cidade (paridade do toque) | [ ] | Cenários E2E de toque emulado. | G4 |
| REQ-061 | §9 | F4 | RM-F4-07 — Smoke E2E do Electron | [ ] | Passa no Windows CI. | G4 |
| REQ-062 | §9 | F3 | RM-F3-19 — Testes de `main.js` e `preload.js` | [ ] | `tests/desktop-main.mjs`, `tests/desktop-preload.mjs`. | G3 |
| REQ-062 | §9 | F3 | RM-F3-20 — Testes JUnit do plugin Android e SW real | [ ] | `app/src/test/...`, `tests/e2e/sw.spec`. | G3 |
| REQ-063 | §9 | F0 | RM-F0-05 — Runner de testes multiplataforma | [ ] | Teste do runner com script falho/passando; roda em Windows (CI matrix) e Linux. | G0 |
| REQ-064 | §9 | F2 | RM-F2-02 — Harness de teste de produção do monólito | [ ] | Cobre boot, abrir vault fixture, criar nota, mundo. | G2 |
| REQ-064 | §9 | F2 | RM-F2-18 — Eliminar testes dependentes de texto de `app.js` | [ ] | grep de `readFileSync('src/app.js')` em `tests/` = 0. | G2 |
| REQ-065 | §10.2 | F0 | RM-F0-06 — Fonte única de versão (`tools/version.mjs`) | [ ] | `tests/version.mjs` (drift injetado falha); `consistency.mjs` adaptado. | G0 |
| REQ-066 | §10.2 | F0 | RM-F0-14 — Separar integração e publicação (release por tag) | [ ] | Dry-run em branch de teste: push em `main` não publica; tag publica em repositório de teste. | G0 |
| REQ-066 | §10.2 | F0 | RM-F0-15 — Endurecer CI: actions por SHA e Dependabot | [ ] | Verificador `tools/check-workflows.mjs` rejeita `uses:` sem SHA. | G0 |
| REQ-067 | §10.4 | F0 | RM-F0-16 — Ciclo de vida de branches e backlog de issues | [ ] | Lista de branches remotas após limpeza só contém `main`, `chore/*` e trabalho ativo. | G0 |
| REQ-068 | §9 | F0 | RM-F0-03 — Executar verificador de rastreabilidade no CI | [ ] | Passo falha em PR que remove REQ/item; PR de teste comprova. | G0 |
| REQ-069 | §9 | F4 | RM-F4-09 — Smoke do instalador Windows e do APK no CI | [ ] | Job verde em PR e antes do release. | G4 |
| REQ-070 | §8 | F0 | RM-F0-10 — Gerador de vaults sintéticos S/M/L | [ ] | `tests/perf-vault.mjs`: mesma semente → mesmos hashes. | G0 |
| REQ-070 | §8 | F0 | RM-F0-11 — Harness dos cenários de performance | [ ] | Execução repetida: variação ≤ tolerância declarada; teste do parser de resultados. | G0 |
| REQ-070 | §8 | F0 | RM-F0-12 — Publicar baseline 1.8.2 | [ ] | Três execuções por cenário; mediana e p95 registradas. | G0 |
| REQ-071 | §8 | F5 | RM-F5-02 — Derivar e aprovar budgets | [ ] | Validação do schema; aprovação registrada em AGENTSCHAT. | G5 |
| REQ-072 | §8 | F5 | RM-F5-03 — Persistência incremental | [ ] | Medir bytes/tempo por flush em S/M/L; O(1) em bytes. | G5 |
| REQ-073 | §8 | F5 | RM-F5-04 — Busca indexada e com debounce | [ ] | `search-editor.mjs` + medição p95 em L. | G5 |
| REQ-074 | §8 | F5 | RM-F5-05 — Carregamento sob demanda de módulos pesados | [ ] | TTI melhora vs baseline; E2E funcional. | G5 |
| REQ-074 | §8 | F5 | RM-F5-06 — Load do vault com concorrência limitada | [ ] | Medir abertura em M/L (web IDB, Electron). | G5 |
| REQ-075 | §8 | F5 | RM-F5-07 — Teto e compactação de histórico | [ ] | Teste de teto; migração de history v1. | G5 |
| REQ-076 | §6.2 | F4 | RM-F4-01 — Definir e implementar `UrbeNative.contract` | [ ] | Teste de shape por adapter. | G4 |
| REQ-076 | §6.2 | F4 | RM-F4-02 — Suíte de conformidade `native-contract` por adapter | [ ] | Falha se capacidade declarada não funcionar. | G4 |
| REQ-079 | §6.2 | F4 | RM-F4-03 — Paridade de exportação no Electron (`saveFile`) | [ ] | Teste de `saveFile` (caminho, cancelamento). | G4 |
| REQ-080 | §10.3 | F4 | RM-F4-11 — `THIRD-PARTY-NOTICES` | [ ] | `tools/check-license.mjs` valida cobertura das dependências. | G4 |
| REQ-080 | §10.3 | F4 | RM-F4-12 — `LICENSE` de todos os direitos reservados | [ ] | `check-license` verde. | G4 |
| REQ-081 | §10.2 | F4 | RM-F4-10 — Política de release, canais e rollback | [ ] | Dry-run de release e de rollback em repositório de teste. | G4 |
| REQ-081 | §10.2 | F6 | RM-F6-12 — Release candidato 2.0 | [ ] | CI + smoke instalador/APK. | G6 |
| REQ-082 | §10.5 | F4 | RM-F4-14 — Guia de migração 1.x→2.x e aviso no app | [ ] | E2E: abrir fixture 1.x mostra aviso e cria backup. | G4 |
| REQ-083 | §11.1 | F6 | RM-F6-01 — Publicar e verificar critérios de sucesso S1–S10 | [ ] | Cada S# aponta teste/gate executado. | G6 |
| REQ-084 | §11.1 | F6 | RM-F6-02 — Tutorial e documentação de usuário sincronizados | [ ] | `tutorial.mjs` ampliado. | G6 |
| REQ-085 | §11.1 | F6 | RM-F6-03 — Tela única de Recuperação | [ ] | E2E: restaurar backup e item da lixeira. | G6 |
| REQ-086 | §11.1 | F6 | RM-F6-04 — Linha de base de acessibilidade | [ ] | Relatório axe versionado. | G6 |
| REQ-086 | §11.1 | F6 | RM-F6-05 — Corrigir falhas de acessibilidade dos fluxos essenciais | [ ] | axe sem violações críticas nos fluxos essenciais. | G6 |
| REQ-088 | §10.4 | F0 | RM-F0-01 — Publicar AGENTS.md e AGENTSCHAT.md | [x] | `tools/check-traceability.mjs` verifica existência dos dois arquivos. | G0 |

## Requisitos que não são IMPLEMENTAR

| REQ | Estado | Onde está a justificativa |
|---|---|---|
| REQ-033 | ADIADO | REQUIREMENTS.md; SPEC §13 |
| REQ-034 | ADIADO | REQUIREMENTS.md; SPEC §13 |
| REQ-052 | ADIADO | REQUIREMENTS.md; SPEC §13 |
| REQ-054 | ADIADO | REQUIREMENTS.md; SPEC §13 |
| REQ-059 | ADIADO | REQUIREMENTS.md; SPEC §13 |
| REQ-077 | ADIADO | REQUIREMENTS.md; SPEC §13 |
| REQ-078 | ADIADO | REQUIREMENTS.md; SPEC §13 |
| REQ-087 | FORA DE ESCOPO | REQUIREMENTS.md; SPEC §13 |

## Cobertura

- REQ IMPLEMENTAR: 80; com item no ROADMAP: 80.
- Itens no ROADMAP: 118; concluídos `[x]`: 2.
