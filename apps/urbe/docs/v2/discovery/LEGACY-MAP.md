# Mapa de legacy — Urbe 1.8.2-beta

> Descoberta da issue #33 (AUD-004). Cada adapter: consumidores, autoridade substituta, teste de produção necessário, condição e versão de remoção.
> **[F]** fato (caminho:linha), **[I]** inferência, **[P]** proposta.

## L0 — `src/legacy/bootstrap.js` não é adapter
- [F] É JSZip v3.10.1 minificado (`bootstrap.js:1-14`); expõe `window.JSZip`; consumidores `app.js:1718, 1729-1732, 1741, 2561, 2917`; carregado em `index.html:214` e `sw.js:27`.
- [I] O nome `legacy/` é enganoso e infla a métrica de "código de aplicação" (97 KB no AUDIT-1X).
- [P] Mover para `vendor/jszip/` com licença; atualizar `index.html`, `sw.js`, `tests/consistency.mjs`. **REQ-031.** Remoção do rótulo: 2.0-alpha.1.

## Adapters reais

| ID | Adapter | Onde | Consumidores | Autoridade substituta | Teste de produção exigido | Condição de remoção | REQ |
|---|---|---|---|---|---|---|---|
| L1 | Serviço `legacy.runtime` (15 métodos) | `app.js:4343-4358` | [F] `importFiles`/`exportZip` (`explorer/mobile-ui.js:149-150`, `ui/settings.js`); `aiSettings`/`version` (`settings.js`); `version` (`touch-debug.js:25`, `plugins.js:96`); `ensureFolders` (`tutorial.js:20`). Sem consumidor em `src/`: `openCity, save, navigate, createNote, createFolder, rebuildRoads, openVaults, vaultName, regions` | comandos `workspace.*`, `document.create`, `folder.create`; faltam serviços de import/export ZIP, versão e criação de pastas | teste que exercite cada consumidor via serviço canônico; nenhum teste referencia `legacy.runtime` hoje | extrair import/export, versão e `ensureFolders` para serviços canônicos; remover métodos sem uso | REQ-030 |
| L2 | Serviço `legacy.documents` (`syncBuilding`, `rebuild`) | `app.js:4402-4425` | [F] só `app.js` (1394; wrappers 4429, 4437) | `DocumentStore` + `editor.session` | gravação de nota via `editor.session.record` reflete na casa sem `syncBuilding` | editor grava direto pelo core; `world.buildings[].content` deixa de ser cópia | REQ-030, REQ-027 |
| L3 | Espelho building↔documento (`urbeReconcileWorld`, `urbeCasaParaDocumento`, `urbeGarantirPasta`, listeners) | `app.js:4373-4390, 4443-4570, 4486-4506` | [F] `estadoDesejado` (4469), `abrirCidade` (4549) | `world.projection` + `aquarium.world` | vault de fixture abre com mundo derivado só da projeção | depende de L6 (mundo derivado da projeção) | REQ-026, REQ-030 |
| L4 | Wrappers `urbeLegacyDesired`/`urbeOpenLegacy` | `app.js:4468, 4548` | [F] `tests/app-loader.mjs:8` | `persistence.load` + `world.projection.metadata()` | reescrever `app-loader.mjs` sobre APIs públicas | remover junto de L3; unifica escritor do mapa | REQ-029, REQ-040 |
| L5 ✅ removido (RM-F1-10/11/12) | `FS`/`Disco`/`DBK` → adapters `src/persistence/adapters/{idb,fsa,router}.js` | `app.js:1520, 1911, 1943`; `:2016` comenta "FS legado é apenas adapter do Persistence Core" | [F] `app.js:2018` (`_persistence`) | `persistence` + `UrbeNativeFS`/FSA como adapters | testes de contrato de adapter (web-IDB, FSA, Electron, Android) | extrair para `src/persistence/adapters/*` | REQ-028, REQ-046 |
| L6 | Render/interação da cidade em `app.js` (`frame`, `draw*`, `canvasDown/Move/Up`, A*) | `app.js:366-503, 2779-2804, 3026-3038, 3186, 4638-4777, 4778-5119` | [F] tudo em `app.js` | `aquarium.renderer`, `aquarium.touch`, `aquarium.roads` (existem, sem produção) | teste que prove tráfego de produção pelo renderer canônico + paridade visual | ligar `renderer.configure`/`touch.attach`/`roads.setPath` e apagar as camadas equivalentes | REQ-026, REQ-029 |
| L7 | Serviços "host": `world.custom` (5192), `city.layout` (5429), `world.life.host` (5535), `workspace.storage` (4624) | `app.js` | [F] `world.custom`: `plugins.js:68-72`, `customize.js:154,269`; `world.life.host`: `life.js:13`, `tests/world-life.mjs:23`; `workspace.storage`: `explorer/mobile-ui.js:143`, `ui/settings.js:8`; `city.layout`: só comando `city.reorganize` e `tests/static.mjs:43` | interfaces de módulos extraídos (render, layout, storage) | testes de contrato; `world.custom` é API pública para plugins (versionar antes) | cada host vira interface de módulo extraído (REQ-026/028) | REQ-030, REQ-032 |
| L8 ✅ removido (RM-F1-09) | Wrapper de `document.open` | `pages/studio.js:588-591` sobre `app.js:4366` | [F] | `document.open` único com roteamento por tipo de artefato | abrir nota, página, plugin por tipo | após modelo de artefatos (REQ-039) | REQ-030, REQ-039 |
| L9 | Migrações de dados 1.x: `migrarAntiga` (`:2291`), `urbeEnsureSingleVault` (`:4577`), `URBE_MUNDO='placas-1'` (`:5226`), IA legada (`ai/store.js:36-38`), `EXT_TEXTO` (`:1584`), compat `world.water.has` (`:49`) | `app.js`, `ai/store.js` | [F] boot (`app.js:5611`), `store.js:26` | política de migração (ADR-0004) | fixtures de vault histórico (REQ-037) | **só remover após fixtures e política**; não são dívida a apagar | REQ-035..038, 043, 045 |
| L10 | Resíduos de UI: minimapa legado oculto (`:1863`), botão Vault/ZIP oculto (`:3067`), input de diretório de compat (`:2878`), diálogo antigo (`:1785`), mapeamento de glifos (`ui/icons.js:65`) | vários | [F] | — | teste de UI de que nada os referencia | auditoria de uso; remoção pequena | REQ-030 |
| L11 | Gancho `window.URBE` (`povo, rotas, mundo, fauna, passo, olhar, quadro, diagnostico`) | `app.js:4312-4327` | [F] comentário diz "harness de verificação"; sem consumidor em `src/` ou `tests/` | modo de diagnóstico do core (`diagnostics`) | — | mover para diagnóstico ou remover | REQ-030 |

## Dívida de sobrescrita (não é adapter, é camada)
- [F] 90 sobrescritas de nível superior; 5 funções duplicadas; ~30 variáveis "Antes/Base/Old" (`app.js:2627, 3201-3271, 3332-3476, 4008-4039, 4292, 4335, 4468, 4548, 4629, 4958, 4975, 5101, 5339, 5369, 5525, 5555`).
- [F] Comentários-lápide "definição antiga removida na 1.0": `app.js:528, 1244, 1497, 1607, 1608, 1866, 2090, 3152`.
- [P] Regra: cada extração substitui uma autoridade e **apaga** a implementação anterior no mesmo item do ROADMAP (REQ-029).

## Ordem sugerida de remoção [P]
1. L0 (mecânico) → 2. L11, L10 (auditoria) → 3. L5 e escritor único do mapa (REQ-028/040) → 4. L6 + L3 + L4 (REQ-026/029) → 5. L1, L2 → 6. L7, L8 → 7. L9 permanece até política de migração e fixtures.

## Testes de produção ausentes
- [F] Nenhum teste referencia `legacy.runtime`/`legacy.documents` (grep em `tests/`). AUD-004 exige teste por adapter; criar antes de mexer (REQ-064).
