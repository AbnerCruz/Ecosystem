import fs from 'node:fs';

const read = p => fs.readFileSync(new URL('../'+p, import.meta.url), 'utf8');
const index = read('index.html');
const app = read('src/app.js');
const shell = read('src/styles/shell.css');
const core = read('src/core/core.js');
const keymap = read('src/core/keymap.js');
const palette = read('src/ui/command-palette.js');
const documents = read('src/core/documents.js');
const knowledge = read('src/core/knowledge-index.js');
const editorSession = read('src/editor/session.js');
const editorContext = read('src/editor/context.js');
const editorChrome = read('src/editor/chrome.js');
const editorWorkspace = read('src/editor/workspace.js');
const editorSplit = read('src/editor/split.js');
const explorerModel = read('src/explorer/model.js');
const explorerMobile = read('src/explorer/mobile-ui.js');
const persistence = read('src/persistence/workspace.js');
const compositionStore = read('src/composition/store.js');
const compositionCompiler = read('src/composition/compiler.js');
const compositionUI = read('src/composition/ui.js');
const history = read('src/core/history.js');
const diagnostics = read('src/core/diagnostics.js');
const worldProjection = read('src/world/projection.js');
const worldSystem = read('src/world/system.js');
const roads = read('src/world/roads.js');
const renderer = read('src/world/renderer.js');
const touch = read('src/world/touch.js');
const scheduler = read('src/core/scheduler.js');
const sw = read('sw.js');

const checks = [
  ['runtime externo', index.includes('./src/app.js')],
  ['workspace core externo', index.includes('./src/core/core.js')],
  ['core antes do legado', index.indexOf('./src/core/core.js') < index.indexOf('./src/app.js')],
  ['css base externo', index.includes('./src/styles/base.css')],
  ['css shell externo', index.includes('./src/styles/shell.css')],
  ['versão 1.7.2-beta', index.includes('Urbe v1.7.2-beta') && app.includes("V21_VERSION='1.7.2-beta'")],
  ['app instalado com a mesma versão', JSON.parse(read('package.json')).version === '1.7.2-beta'],
  ['ponte nativa antes do app', index.indexOf('./src/native/bridge.js') > 0 && index.indexOf('./src/native/bridge.js') < index.indexOf('./src/app.js')],
  ['diagnóstico URBE', app.includes('window.URBE')],
  ['bairros organizados', app.includes("core.provide('city.layout'") && app.includes("register('city.reorganize'") && read('src/ui/settings.js').includes('city.reorganize')],
  ['ponte de comandos', app.includes("workspace.navigate.world") && app.includes("document.create") && app.includes("workspace.save")],
  ['core contratos', core.includes('class EventBus') && core.includes('class CommandRegistry') && core.includes('class StateStore')],
  ['documentos canônicos', index.includes('./src/core/documents.js') && documents.includes('class DocumentStore')],
  ['índice de conhecimento', index.includes('./src/core/knowledge-index.js') && knowledge.includes('class KnowledgeIndex')],
  ['editor session', index.includes('./src/editor/session.js') && editorSession.includes('class EditorSession')],
  ['editor context', index.includes('./src/editor/context.js') && editorContext.includes("editor.context")],
  ['editor chrome', index.includes('./src/editor/chrome.js') && editorChrome.includes('urbeEditorChrome')],
  ['editor workspace', index.includes('./src/editor/workspace.js') && editorWorkspace.includes('editor.workspace')],
  ['editor split', index.includes('./src/editor/split.js') && index.includes('./src/editor/split-ui.js') && editorSplit.includes('editor.split')],
  ['explorer canônico', index.includes('./src/explorer/model.js') && explorerModel.includes('class ExplorerModel')],
  ['explorer mobile first', index.includes('./src/explorer/mobile-ui.js') && explorerMobile.includes('EXPLORER')===false && explorerMobile.includes('HOLD=430')],
  ['persistence core', index.includes('./src/persistence/workspace.js') && persistence.includes('class WorkspacePersistence')],
  ['composition core', index.includes('./src/composition/store.js') && index.includes('./src/composition/compiler.js') && compositionStore.includes("provide('compositions'") && compositionCompiler.includes("composition.compiler")],
  ['composition visual mobile', index.includes('./src/composition/ui.js') && index.includes('./src/styles/composition.css') && compositionUI.includes('data-inspector-title') && explorerMobile.includes('data-compose')],
  ['composition persistence', persistence.includes(".urbe/compositions.json") && persistence.includes('journal.compositions')],
  ['no Burgo runtime', !index.includes('/burgo/') && !app.includes('v24Burgo') && !app.includes('window.Burgo') && !shell.includes('v24Burgo') && shell.includes('repeat(4')],
  ['workspace diagnostics', index.includes('./src/core/diagnostics.js') && diagnostics.includes('class Diagnostics')],
  ['revision history', index.includes('./src/core/history.js') && history.includes('class RevisionHistory')],
  ['world projection', index.includes('./src/world/projection.js') && worldProjection.includes('class WorldProjection')],
  ['aquarium world system', index.includes('./src/world/system.js') && worldSystem.includes('class AquariumWorld')],
  ['incremental road graph', index.includes('./src/world/roads.js') && roads.includes('class RoadGraph')],
  ['scheduler aquarium renderer', index.includes('./src/world/renderer.js') && renderer.includes('class AquariumRenderer')],
  ['scheduler loads before renderer', index.indexOf('./src/core/scheduler.js') < index.indexOf('./src/world/renderer.js')],
  ['mobile touch controller', index.includes('./src/world/touch.js') && touch.includes('class TouchController')],
  ['shared scheduler', index.includes('./src/core/scheduler.js') && scheduler.includes('class Scheduler')],
  ['atalhos unificados', index.includes('./src/core/keymap.js') && keymap.includes("ui.commandPalette.open")],
  ['command palette', index.includes('./src/ui/command-palette.js') && palette.includes("ui.commandPalette.open")],
  ['quick open indexado', index.includes('./src/ui/quick-open.js') && keymap.includes("ui.quickOpen.open")],
  ['cache app', sw.includes("'./src/app.js'")],
  ['cache core', sw.includes("'./src/core/core.js'")],
  ['cache conhecimento', sw.includes("'./src/core/documents.js'") && sw.includes("'./src/core/history.js'") && sw.includes("'./src/core/diagnostics.js'") && sw.includes("'./src/core/knowledge-index.js'") && sw.includes("'./src/editor/session.js'") && sw.includes("'./src/editor/context.js'") && sw.includes("'./src/editor/chrome.js'") && sw.includes("'./src/persistence/workspace.js'") && sw.includes("'./src/world/projection.js'") && sw.includes("'./src/world/system.js'") && sw.includes("'./src/world/roads.js'") && sw.includes("'./src/world/renderer.js'") && sw.includes("'./src/world/touch.js'") && sw.includes("'./src/core/scheduler.js'") && sw.includes("'./src/explorer/model.js'") && sw.includes("'./src/explorer/mobile-ui.js'") && sw.includes("'./src/editor/workspace.js'") && sw.includes("'./src/editor/split.js'") && sw.includes("'./src/editor/split-ui.js'")],
  ['cache composition', sw.includes("'./src/composition/store.js'") && sw.includes("'./src/composition/compiler.js'") && sw.includes("'./src/composition/ui.js'") && sw.includes("'./src/styles/composition.css'")],
  ['cache comandos', sw.includes("'./src/core/keymap.js'") && sw.includes("'./src/ui/command-palette.js'") && sw.includes("'./src/ui/quick-open.js'")],
  ['cache css', sw.includes("'./src/styles/base.css'") && sw.includes("'./src/styles/shell.css'")],
  ['cache versionado', sw.includes('urbe-shell-v1.7.2-beta')],
  ['sem runtime principal inline', !index.includes('V25_MAX=18')]
];

let failed = 0;
for (const [name, ok] of checks) {
  console.log((ok ? 'OK  ' : 'FAIL') + ' ' + name);
  if (!ok) failed++;
}
if (failed) process.exit(1);
