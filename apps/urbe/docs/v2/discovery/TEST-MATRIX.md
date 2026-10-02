# Matriz de testes — capacidades × cobertura (Urbe 1.8.2-beta)

> Descoberta da issue #33 (AUD-009; REQ-016, 061–064, 069). **[F]** fato, **[I]** inferência, **[P]** proposta.

## 1. Como os testes rodam hoje
- [F] Sem framework: cada `tests/*.mjs` é script Node com `vm.runInContext` sobre o código-fonte, sem navegador nem DOM real; módulos IIFE carregados em contextos fake (`tests/core.mjs:1-10`). Falha vira `process.exitCode=1` ou `throw`.
- [F] `package.json` `scripts.test`: `for t in tests/*.mjs; do node "$t" || exit 1; done` — POSIX-only, para no primeiro erro. [I] Não roda em `cmd.exe`.
- [F] CI (`structural-checks.yml`) roda todos sem parar (`status=1`), Node 22, em push a `main` e todo PR; passos: `node --check` em todo `.js` versionado, `tools/build-tutorial.mjs --check`, `tests/*.mjs`. Sem lint, audit, cobertura, typecheck, `npm ci`.
- [F] 31 arquivos (~148 KB); cinco de uma linha densa (`composition`, `production-order`, `recovery-history`, `runtime-adapters`, `world-system`).
- [F] Vários "testes" são checagens de string no código-fonte (`static.mjs` 45 checks, `ui-shell.mjs`, `city.mjs`, partes de `search-editor.mjs` e `editor-persistence.mjs`); 10 leem `src/app.js` como texto.

## 2. O que cada teste cobre
| Arquivo | Cobertura | Tipo |
|---|---|---|
| `core.mjs` | EventBus, CommandRegistry, StateStore, serviços, `core.start()` | unitário |
| `documents.mjs` | DocumentStore, wikilinks, backlinks, tags, busca, reindex | unitário |
| `editor.mjs` | abas, undo/redo, outline, contexto | unitário |
| `editor-workspace.mjs` | back/forward, find/replace, sessão (localStorage), split | unitário |
| `editor-persistence.mjs` | `pagehide`/`visibilitychange` gravam; Visual não ressuscita nota; SW não recarrega na 1ª instalação | contrato + string |
| `explorer.mjs` | identidade, árvore, multiseleção, favoritos, rename/move/duplicate/delete | unitário |
| `trash.mjs` | lixeira, restore com identidade, purge | unitário |
| `recovery-history.mjs` | journal, `history.json`, recuperação | contrato |
| `persistence-world.mjs` | load do vault, ID estável, `mapa.json`, espacial no rename | contrato |
| `integration-runtime.mjs` | scripts únicos, troca de vault, journal, migração para vault único, zip (fake), toque | integração (vm) |
| `app-loader.mjs` | trecho real de `abrirCidade` | integração (fatia de texto) |
| `composition*.mjs` | vínculo, ordem, overrides, roundtrip, `javascript:` neutralizado | unitário |
| `world-system/production-order/runtime-adapters.mjs` | projeção, ruas, ordem de scripts (core/world), agendador+renderer, toque | unitário |
| `world-terrain.mjs` | terreno determinístico, rios, placas, clima, pontes, coordenadas negativas | unitário |
| `world-life.mjs` | luz, clima, eventos, fauna, partículas | unitário |
| `city.mjs` | enquadramento, casa por nota, ruas, rótulos | string+vm |
| `consistency.mjs` | src⇄`index.html`⇄`sw.js`, versão, CHANGELOG, CI contém checks | estrutural |
| `static.mjs` | existência/ordem de módulos, cache do SW, versão hardcoded | estrutural |
| `ui-shell.mjs` | sem `prompt/confirm/alert`, ordem, `UrbeDialogs`, SW rede-primeiro | string |
| `search-editor.mjs` | ranking do quick-open; strings do Visual | unitário+string |
| `math.mjs` | KaTeX real no `vm`, delimitadores, autocomplete, catálogo | unitário |
| `tutorial.mjs` | conteúdo (≥40 notas), primeira abertura, restauração | integração (vm) |
| `customize.mjs` | tema, variáveis, plugins (desligado por padrão, hash, isolamento de erro, limpeza, bloqueio de escrita na pasta) | unitário |
| `pages.mjs`, `pages-free.mjs` | motor de páginas: normalize, render sem injeção, blocos, layout livre, embed só https, `write_page` da IA | unitário |
| `ai-agent.mjs` | loop agêntico (~25 casos), políticas, retries 429, conversões, compactação, escape de Markdown | unitário |
| `native-bridge.mjs` | `native/desktop/vault-fs.js` **real** em tmpdir: `..`, absoluto, symlink, atômico, concorrência, diário | integração |
| `native-android.mjs` | Capacitor **simulado** (mocks): vault, links, downloads, update, leitura em lote | mock |

## 3. Capacidades sem teste (lacunas) → REQ
| Lacuna [F] | REQ |
|---|---|
| `native/desktop/main.js` (149 linhas): guard de IPC (`:73`), `protocol.handle('app')` (`:62-69`), `external()`/`setWindowOpenHandler` (`:45-56`), permission handler (`:146`), updater (`:116-137`), `printToPdf` (`:98-111`), watcher (`:31-41`); `preload.js` | REQ-062, REQ-055 |
| `UrbeAndroidPlugin.java` e `MainActivity.java` (só boilerplate `ExampleUnitTest`/`ExampleInstrumentedTest`); path traversal do Java só via mock JS | REQ-062, REQ-056 |
| Sem E2E, navegador real, Electron real, Android real, visual/CSS, performance; nenhum `performance.now`/budget/benchmark em `tests/`, `tools/`, `.github`; PRs citam Playwright/Electron/Pixel (AUD-009) sem artefato no repo | REQ-061, REQ-070 |
| `sw.js` verificado por regex, não por execução | REQ-062 |
| Adaptador IDB do modo web interno (`DBK`/`FS`), handles FSA | REQ-062, REQ-046 |
| Importar/exportar ZIP, preview PDF.js; zip-slip/zip-bomb (sem limite nem filtro `..`) | REQ-058 |
| Studio de páginas (UI), `composition/ui.js`, `ai/ui.js` além do escape, providers com rede real, chaves IA no IDB | REQ-061 |
| Bairros/layout (v1.8.1/1.8.2): nenhum teste referencia `urbeDentroDe` nem "buraco no bairro" — **regressão sem cobertura** | REQ-064 |
| CSP (não existe), permissões do Manifest, updater | REQ-050, REQ-055 |
| `migrarAntiga`, `urbeReorganizarCidade` (mundo), mapas v1/v2, `.urbe/*.json` de versão futura | REQ-037, REQ-035 |
| Adapters `legacy.runtime`/`legacy.documents` | REQ-030, REQ-064 |
| `npm test` não roda em Windows nativo | REQ-063 |

## 4. Workflows (CI/CD)
- [F] `structural-checks.yml`: gatilhos `push` main e `pull_request`; `node --check` em `git ls-files '*.js'` (inclui `vendor/`); `build-tutorial --check`; `tests/*.mjs`.
- [F] `app.yml` ("Urbe instalável"): `push` main, PR filtrado (`native/**`, `src/native/**`, `package*.json`, `capacitor.config.json`, `tools/build-www.mjs`, `app.yml`), `workflow_dispatch` (`publicar`). Job `versao` (`app.yml:38-57`): lê `package.json`, exige `V21_VERSION='<v>'` em `src/app.js`, `WANT=true` em push ou `publicar=true`, não publica se `v<versão>` existe. Jobs `windows` (NSIS x64 sem assinatura), `android` (`gradlew assembleRelease`, falha em release real sem secrets; assina com chave debug em PR/manual, `build.gradle:38-43`), `lancamento` (`softprops/action-gh-release@v2`, notas por `awk` do CHANGELOG).
- [F] Acoplamento: **todo merge em `main` com bump de versão publica Release** sem aprovação (AUD-006). O release **não depende de `structural-checks`** (`needs:` só intra-arquivo). [I] Release pode sair com teste vermelho (depende de branch protection, não visível).
- [F] Sem smoke do instalador/APK; `permissions: contents: write` no workflow inteiro; actions por tag (`@v4`, `@v2`), não SHA; sem `dependabot.yml`; dependências com caret. → REQ-066, REQ-069.
- [F] `tools/build-tutorial.mjs` (gera `content.js`; confere links, tags, títulos, alcançabilidade, validade de `.page.json`); `tools/build-www.mjs`.

## 5. Estratégia-alvo de testes [P]
| Nível | Runner | Exemplos | Gate |
|---|---|---|---|
| Estrutural | Node (`tools/check-*.mjs`) | registro de módulos, versão única, traceability, boundaries | G0 |
| Unitário/contrato | Node `vm`/ESM | core, documents, pages, ai, contratos de adapter | G1+ |
| Fixtures de dados | Node + `tests/fixtures/vaults/*` | abertura/migração de vaults históricos; proteção forward | G1 |
| Integração de plataforma | Node + Electron/Capacitor mocks + JUnit | `main.js`, `preload`, plugin Java, SW | G3/G4 |
| E2E | Playwright (web) + smoke Electron (`URBE_TEST_*`) | abrir vault fixture, criar nota, salvar, recarregar, plugin, IA mock; harness de produção do monólito `tests/e2e/app-runtime.mjs` (RM-F2-02) | G4 |
| Performance | script de harness versionado | abertura/edição/busca/cidade/memória | G5 |
| Manual | checklist em `docs/v2/manual/` | Android real (voltar, permissão, update), SmartScreen | G6 |
| Segurança | testes de propriedade | zip-slip, CSP, sandbox, permissions | G3 |

Todo item do ROADMAP referencia teste e gate; testes de string sobre `app.js` são substituídos por comportamento (REQ-064).
