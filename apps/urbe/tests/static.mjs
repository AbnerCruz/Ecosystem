import fs from 'node:fs';

const read = p => fs.readFileSync(new URL('../'+p, import.meta.url), 'utf8');
const index = read('index.html');
const app = read('src/app.js');
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
const sw = read('sw.js');

const checks = [
  ['runtime externo', index.includes('./src/app.js')],
  ['workspace core externo', index.includes('./src/core/core.js')],
  ['core antes do legado', index.indexOf('./src/core/core.js') < index.indexOf('./src/app.js')],
  ['css base externo', index.includes('./src/styles/base.css')],
  ['css shell externo', index.includes('./src/styles/shell.css')],
  ['versão 0.30', index.includes('Urbe v0.30.0') && app.includes("V21_VERSION='0.30.0'")],
  ['diagnóstico URBE', app.includes('window.URBE')],
  ['ponte de comandos', app.includes("workspace.navigate.world") && app.includes("document.create") && app.includes("workspace.save")],
  ['diagnóstico Burgo', app.includes('window.Burgo')],
  ['core contratos', core.includes('class EventBus') && core.includes('class CommandRegistry') && core.includes('class StateStore')],
  ['documentos canônicos', index.includes('./src/core/documents.js') && documents.includes('class DocumentStore')],
  ['índice de conhecimento', index.includes('./src/core/knowledge-index.js') && knowledge.includes('class KnowledgeIndex')],
  ['editor session', index.includes('./src/editor/session.js') && editorSession.includes('class EditorSession')],
  ['editor context', index.includes('./src/editor/context.js') && editorContext.includes("editor.context")],
  ['editor chrome', index.includes('./src/editor/chrome.js') && editorChrome.includes('urbeEditorChrome')],
  ['editor workspace', index.includes('./src/editor/workspace.js') && editorWorkspace.includes('editor.workspace')],
  ['editor split', index.includes('./src/editor/split.js') && index.includes('./src/editor/split-ui.js') && editorSplit.includes('editor.split')],
  ['atalhos unificados', index.includes('./src/core/keymap.js') && keymap.includes("ui.commandPalette.open")],
  ['command palette', index.includes('./src/ui/command-palette.js') && palette.includes("ui.commandPalette.open")],
  ['quick open indexado', index.includes('./src/ui/quick-open.js') && keymap.includes("ui.quickOpen.open")],
  ['cache app', sw.includes("'./src/app.js'")],
  ['cache core', sw.includes("'./src/core/core.js'")],
  ['cache conhecimento', sw.includes("'./src/core/documents.js'") && sw.includes("'./src/core/knowledge-index.js'") && sw.includes("'./src/editor/session.js'") && sw.includes("'./src/editor/context.js'") && sw.includes("'./src/editor/chrome.js'") && sw.includes("'./src/editor/workspace.js'") && sw.includes("'./src/editor/split.js'") && sw.includes("'./src/editor/split-ui.js'")],
  ['cache comandos', sw.includes("'./src/core/keymap.js'") && sw.includes("'./src/ui/command-palette.js'") && sw.includes("'./src/ui/quick-open.js'")],
  ['cache css', sw.includes("'./src/styles/base.css'") && sw.includes("'./src/styles/shell.css'")],
  ['cache versionado', sw.includes('urbe-shell-v0.30.0')],
  ['sem runtime principal inline', !index.includes('V25_MAX=18')]
];

let failed = 0;
for (const [name, ok] of checks) {
  console.log((ok ? 'OK  ' : 'FAIL') + ' ' + name);
  if (!ok) failed++;
}
if (failed) process.exit(1);
