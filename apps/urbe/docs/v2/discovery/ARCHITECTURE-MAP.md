# Mapa de arquitetura — Urbe 1.8.2-beta

> Descoberta da issue #33. Snapshot: `main` em `91de0f4` (1.8.2-beta) + fundação 2.0 (só docs).
>
> Convenção de evidência: **[F]** fato observado (caminho:linha), **[I]** inferência, **[P]** decisão proposta, **[C]** decisão consolidada. Linhas de `src/app.js` são aproximadas (arquivo com linhas longas).

## 1. Carregamento e inicialização

### 1.1 `index.html` é o contrato de runtime
- [F] 60 `<script>` síncronos, sem `defer`/`async`, em `index.html:208-267`; um script inline em `index.html:26` aplica `urbe.aparencia.v1` antes do primeiro desenho.
- [F] Ordem por bloco: (208-214) ícones, diálogos, `native/bridge`, touch-debug, terrain, pixel-art, JSZip; (215-222) core, documents, trash, history, composition/store+compiler, knowledge-index, persistence; (223-228) world projection/system/roads, scheduler, renderer, touch; (229-233) explorer, composition/ui, diagnostics; (234-240) editor; (241-245) ai; (246-247) KaTeX, math; (248-251) pages; (252-256) customize, tutorial; (257) **`app.js`**; (258-267) life, studio, visual-tools, math/editor, settings, panel, tips, keymap, command-palette, quick-open.
- [F] `sw.js:1-98` lista o mesmo conjunto de JS (61 arquivos) — a diferença é `src/world/chunk-worker.js`, presente só no SW porque é carregado por `new Worker` (`app.js:4806`) e faz `importScripts('./terrain.js','./pixel-art.js')`. `tests/consistency.mjs:12-17` protege o conjunto, mas **não a ordem**.
- [F] `tools/build-www.mjs:1-11` copia `index.html`, manifest, PNGs, `src/` e `vendor/` para `native/www` (gitignored) e não define ordem; Electron serve o diretório do app via `app://urbe` (`native/desktop/main.js:62-69`) e não empacota `sw.js`.
- [F] Erros de ordem viram funcionalidade ausente, não exceção: guards silenciosos `if(!core||!docs)return;` em `persistence/workspace.js:3`, `world/projection.js:3`, `system.js:3`, `roads.js:3`; `customize.js:269` reage a `service:provided` para `world.custom` (só criado em `app.js:5192`); `life.js:13` retorna sem `world.life.host`; `pages/studio.js:588-591` embrulha `document.open` e depende de `app.js:4366`.
- [I] Confirma AUD-003: a ordem de scripts é dependência implícita sem verificação estática. → REQ-025.
- [F] Outras "ordens" próprias coexistem: `tools/build-tutorial.mjs:14` (vm com core/documents/engine), `tests/production-order.mjs` (só core/world), `tests/app-loader.mjs:3-8` (subconjunto + fatia `app.js` por marcador de texto).

### 1.2 Sequência de boot real
[F→I] `index.html` carrega módulos (cada um é IIFE que publica `window.UrbeX` e registra serviços em `UrbeCore` se disponível) → `app.js` executa suas camadas em ordem de arquivo, registra serviços `legacy.*`, `world.custom`, `city.layout`, `world.life.host`, `workspace.storage` → `urbeIniciar` (`app.js:5599`) escolhe pasta/vault, `urbeEnsureSingleVault` (`app.js:5612`), `abrirCidade` (cadeia de 4 camadas) → `persistence.load` → `documents.replaceAll` → projeção/mundo → `core.start()` emite `core:ready`.

## 2. Núcleo canônico

- [F] `UrbeCore` (`core/core.js:90`): `EventBus`, `CommandRegistry`, `StateStore`, `provide/service/hasService/start`; estado `{workspace, activeDocument, mode, ready}` (`core.js:77`); versão hardcoded `core.js:81`.
- [F] Eventos do core: `command:registered/unregistered/before/after/error`, `state:changed`, `service:provided`, `core:ready`.
- [F] Serviços e responsabilidades, por subsistema (bytes de `src/`):

| Subsistema | Arquivos (B) | Globals | Serviços `provide` | Eventos emitidos |
|---|---|---|---|---|
| core (~22,5 KB) | core, documents, trash, history, knowledge-index, scheduler, diagnostics, keymap | UrbeCore, UrbeDocumentModel, UrbeTrash, UrbeRevisionHistory, UrbeScheduler, UrbeDiagnostics | documents, trash, history, knowledge, scheduler, diagnostics, keymap | `document:created/updated/removed`, `documents:reset`, `trash:changed`, `history:changed`, `knowledge:indexed`, `scheduler:error` |
| persistence (9,4 KB) | workspace.js (71 linhas) | UrbePersistence | persistence | `workspace:loaded/dirty/saving/saved/saveError/recovered/external` |
| editor (~29 KB) | session, context, workspace, split, split-ui, find-ui, chrome, visual-tools | UrbeEditorModel, UrbeVisualTools | editor.session/context/workspace/split/findBar | `editor:session/changed/history/workspace/navigation/split` |
| explorer (27,6 KB) | model, operations, mobile-ui | UrbeExplorerModel | explorer, explorer.operations, explorer.ui | `explorer:changed/folderCreated/folderRemoved/operation/visibility` |
| composition (18,3 KB) | store, compiler, ui | UrbeComposition, UrbeCompositionCompiler | compositions, composition.compiler/ui | `composition:created/updated/removed`, `compositions:replaced` |
| world (~118 KB) | terrain, pixel-art, chunk-worker, projection, system, roads, renderer, touch, life | UrbeTerrain, UrbeArt, UrbeWorldProjection, UrbeAquariumWorld, UrbeRoadGraph, UrbeAquariumRenderer, UrbeTouchController, UrbeVida | world.projection, aquarium.world/roads/renderer/touch | `world:*`, `aquarium:*`, `roads:*` |
| ai (~114 KB) | providers, tools, store, agent, ui | UrbeAIProviders, UrbeAITools, UrbeAIStore, UrbeAgent, UrbeAgentUI | — | — |
| pages (~255 KB) | engine, free, studio, templates, ai-tools | UrbePages, UrbePageTemplates, UrbePageStudio | pages.studio | `pages:studio` |
| customize (~95 KB) | customize, plugins, panel, ai-tools | UrbeCustomize, UrbePlugins, UrbeCustomizePanel | customize, plugins | `customize:applied/safe-mode`, `plugins:changed/error` |
| math (~34 KB) | core, editor | UrbeMath, UrbeMathEditor | — | — |
| ui (~38 KB) | icons, dialogs, settings, tips, touch-debug, command-palette, quick-open | UrbeIcons, UrbeDialogs, UrbeSettings, UrbeTouchDebug | commandPalette, quickOpen | — |
| tutorial (~119 KB) | content.js (gerado), tutorial.js | UrbeTutorialContent | tutorial | — |
| native (22,7 KB) | bridge.js | UrbeNative, UrbeNativeFS, UrbeNativeToast | — | consome `workspace:loaded` |
| app.js (438 KB) | app.js | `window.URBE`, sobrescreve `showDirectoryPicker`/`showOpenFilePicker` | legacy.runtime, legacy.documents, workspace.storage, world.custom, city.layout, world.life.host | consome `workspace:*`, `document:*`, `explorer:*`, `editor:navigation`, `command:after` |

- [F] Comandos registrados por área: core (`document.update/remove`, `trash.*`, `history.restore`, `workspace.search/diagnostics`), persistence (`workspace.flush`), editor (`editor.*`, `ui.find.open`), explorer (`explorer.*`, `ui.explorer.open`), composition, world (`world.enable/disable/moveDocument`, `aquarium.*`, `world.event.<id>`), pages (`pages.*`, wrapper de `document.open`), customize (`customize.*`, `plugins.reload`, `ui.customize/plugins`), math, ui, tutorial, `app.js` (`city.fit`, `city.reorganize`, `workspace.navigate.*`, `document.create/open`, `folder.create`, `workspace.save`).
- [F] Dependências de serviço dominantes: quase tudo depende de `documents`; `ai/tools` usa `documents, explorer, knowledge, persistence, quickOpen, trash`; `customize/plugins` usa `customize, editor.session, legacy.runtime, persistence, world.custom`; `app.js` consome 13 serviços do core.
- [F] `ai/*`, `math/*` e `ui/*` quase não usam o barramento de eventos/serviços — dependem de globals `window.Urbe*`.

## 3. `app.js` — autoridade histórica

- [F] 438.123 B, 5.632 linhas, uma única IIFE (`app.js:2`–`5632`) em ~20 camadas por versão. 90 atribuições `nome=function` de nível superior; 5 declarações duplicadas (`buildTree` 1354/2592, `estadoDesejado` 2041/2517, `openFilePreview` 552/2565, `openRegionDialog` 1495/2473, `renderFilePreview` 530/2550); ~30 variáveis guardam a versão anterior antes de sobrescrever.
- [F] `V21_VERSION` recebe `'0.21.0'` (3104), `'0.22.0'` (3323), `'0.23.0'` (3690), `'1.8.2-beta'` (4053); `document.title` é reatribuído nas mesmas camadas.
- [F] Cadeias: `estadoDesejado` 2041→2517→3226→3395→4469; `abrirCidade` 2105→2628→4549→5526; `drawTrees` 420→4293→4873→4958→5556; `rebuildRoadNetwork` 325→3477→4336; `drawRegions` 4710→5483.

| Linhas | Camada | Responsabilidade |
|---|---|---|
| 2-121 | base | sprites/atlas, laço de render sob demanda, câmera, terreno antigo |
| 122-365 | base | lote, wikilinks→ruas, A*, `rebuildRoadNetwork` |
| 366-504 | base | `draw*`, laço `frame` (479) |
| 505-564 | base | diálogos, gestos, preview |
| 565-1514 | base | **editor** (resumo, editor, autocompletar, `renderMarkdown` 761, Visual, arrasto do explorer) |
| 1515-1899 | v0.12 | IndexedDB `DBK` 1520, serialização, import/export, minimapa |
| 1900-2312 | v0.14 | `Disco`/`FS` (1911/1943), sync, `abrirCidade` 2105, `migrarAntiga` 2291 |
| 2313-2632 | v0.18 | regiões, explorer em árvore, binários |
| 2633-2962 | v0.19 | identidade de arquivo, importação, PDF.js, PWA |
| 2963-3317 | v0.20-0.21 | território, casca unificada (mais densa) |
| 3318-4044 | v0.22-0.23 | patches de ruas/A*, sanitização, casca mobile, versões, toolbar |
| 4045-4337 | v0.25 | moradores, gancho `window.URBE` (4312) |
| 4338-4637 | v0.27 | **ponte para o Core**: `legacy.runtime`, `legacy.documents`, `urbeReconcileWorld`, `workspace.storage`, `urbeEnsureSingleVault` |
| 4638-5119 | v0.40-0.41 | desenho, mundo vivo, worker de chunks, fauna |
| 5120-5557 | v0.42-1.2 | liga Assistente, `world.custom`, bairros/`city.layout`, `world.life.host` |
| 5558-5632 | boot | `urbeIniciar` 5599, escolha de pasta |

- [F] 10 testes leem `src/app.js` como texto (`ai-agent`, `app-loader`, `city`, `consistency`, `editor-persistence`, `integration-runtime`, `search-editor`, `static`, `ui-shell`, `world-terrain`); `tests/app-loader.mjs:8` fatia entre `'var urbeOpenLegacy=abrirCidade;'` e `'/* An existing installation'`.
- [I] Renomear/mover trechos quebra testes sem mudança semântica (REQ-064).

## 4. Fatos que contradizem ARCHITECTURE.md

1. [F] **Aquário canônico não está ligado à produção.** `AquariumRenderer.configure` e `TouchController.attach` não são chamados por `src/`; só `tests/production-order.mjs` chama `renderer.configure`. `app.js` tem seu próprio `requestAnimationFrame(frame)` (`app.js:479-503`) e desenha com `drawGround/Regions/Roads/Trees/Buildings`; `roads.setPath/sync` nunca são chamados por `app.js` (só `aquarium.roads.pending()`, `app.js:361`); `aquarium.world` só é consumido por `roads.js` e `diagnostics.js`. → REQ-026.
2. [F] O `DocumentStore` aceita qualquer extensão textual `md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv` fora de caminhos ocultos (`persistence/workspace.js:9,45`) — AUD-010. → REQ-039.
3. [F] A superfície de edição (textarea + Visual contenteditable) mora em `app.js`, não em `src/editor` (que cobre sessão, abas, split, busca, contexto). → REQ-027.
4. [F] O Explorer tem duas UIs: `explorer.ui` (`mobile-ui.js`) e a árvore de `app.js` (`buildTree`, `v21BuildTree` 3247).
5. [F] O painel de IA é totalmente `src/ai/ui.js`; `app.js:5120-5142` só o monta; `app.js:3073` mantém o cabeçalho "Fase 5: adaptador OpenRouter" sem código.
6. [I] A cidade real é renderizada e simulada por `app.js`; o caminho canônico é paralelo, coberto por teste, sem tráfego de produção.

## 5. Hotspots além de `app.js`

| Arquivo | Bytes / linhas | Responsabilidades misturadas | Direção [P] |
|---|---|---|---|
| `pages/engine.js` | 106.745 / 677 | markdown GFM, temas/tipografia, 34 blocos, livro, validação, CSS, render, schema para IA | dividir em markdown, tema/CSS, blocos, livro, render quando REQ-022 for executado |
| `pages/studio.js` | 83.982 / 593 | UI do estúdio, prévia em iframes, formulários gerados, edição livre `fx*`, exportar/PDF, galeria, sync vault, wrapper `document.open` | separar UI/exportação/wrapper |
| `world/life.js` | 44.431 / 441 | luz, clima, eventos, fauna, passo de simulação, desenho | separar simulação de desenho |
| `ai/ui.js` | 44.496 / 406 | configuração, conversa, aprovação com diff, markdown próprio (196-228), histórico | reutilizar renderer de markdown único |
| `pages/free.js` | 40.372 / 272 | layout livre, estilos validados, CSS, HTML, árvore | manter; depende de engine |
| `customize/panel.js` | 39.816 / 306 | aparência, texturas, editor de pixels, estilos, plugins, arquivo | dividir por seção |
| `world/pixel-art.js` | 36.617 / 383 | paleta, texturas, chão, árvores, construções, fauna, moradores | manter (dados/desenho) |
| `customize/customize.js` | 30.909 / 285 | temas, validação, CSS vars, texturas, IO no vault | manter |
| `ai/tools.js` | 29.212 / 282 | ferramentas do agente, `validPath`, política | manter |
| CSS | `base.css` 61 KB, `theme.css` 51 KB, `pages.css` 25 KB | — | fora do escopo de extração |

[I] Métrica de acoplamento fino não foi levantada (leitura estática por marcadores). REQ-022 exige avaliação por responsabilidade antes de extrair.

## 6. Versão do produto — fontes hardcoded

| Fonte | Local | Papel |
|---|---|---|
| `package.json:4` | `version` | base de Electron e Android (`build.gradle:5-19` deriva `versionName/versionCode`) |
| `package-lock.json:3,9` | espelho | — |
| `src/core/core.js:81` | `version` | exposta pelo core |
| `src/app.js:4053` | `V21_VERSION` | título, menus, `legacy.runtime.version`, `plugins.js:96`, checagem de reload (`app.js:2955`); 3 atribuições anteriores coexistem |
| `index.html:13` | `<title>` | comparado por `app.js:2955` |
| `sw.js:1` | `urbe-shell-v1.8.2-beta` | nome do cache |
| `CHANGELOG.md:3` | `## v1.8.2-beta` | notas do release via `awk` |
| `tests/static.mjs:39,40,78` | literais | quebram a cada bump |
| `tests/consistency.mjs:24` | última ocorrência de `V21_VERSION=` | não impede atribuições antigas |
| `.github/workflows/app.yml:51` | `grep V21_VERSION` | gate de versão |
→ REQ-019, REQ-065.

## 7. Plataformas nativas (resumo; detalhe em PLATFORMS.md)
- [F] Electron: `contextIsolation`, `sandbox`, `!nodeIntegration` (`main.js:46-49`), guard de IPC por origem (`main.js:73`), `vault-fs.js` valida `..`/absoluto/symlink.
- [F] Android: `MainActivity` (24 linhas) + `UrbeAndroidPlugin` (254 linhas); `bridge.js:207-` monta o mesmo contrato `UrbeNative` sobre Filesystem + UrbeAndroid.
- [F] Web: `bridge.js:12` é inerte sem `UrbeNative`/Capacitor.

## 8. Arquitetura alvo [P → C onde marcado]
- [C] Sem build obrigatório (ADR-0001): registro de módulos declarativo (`docs/v2` define o manifesto; formato exato na SPEC §3) alimenta `index.html`, `sw.js`, `build-www` e testes de boundary.
- [P] Camadas permitidas: `ui → editor/explorer/world/pages/ai/customize → core → persistence adapters → native`; `world` e `pages` não dependem de `ai`; `native` só é acessado via `persistence adapters` e `UrbeNative`; nenhum módulo lê `window.Urbe*` de outro que não declare em `requires`.
- [P] `app.js` converge para composição/bootstrap + adapters legacy temporários (REQ-029/030); cada extração remove a implementação substituída.
- [C] Nenhuma nova feature como camada de versão em `app.js` (REQ-012).
