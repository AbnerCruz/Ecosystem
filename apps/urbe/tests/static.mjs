import fs from 'node:fs';

const read = p => fs.readFileSync(new URL('../'+p, import.meta.url), 'utf8');
const index = read('index.html');
const app = read('src/app.js');
const core = read('src/core/core.js');
const sw = read('sw.js');

const checks = [
  ['runtime externo', index.includes('./src/app.js')],
  ['workspace core externo', index.includes('./src/core/core.js')],
  ['core antes do legado', index.indexOf('./src/core/core.js') < index.indexOf('./src/app.js')],
  ['css base externo', index.includes('./src/styles/base.css')],
  ['css shell externo', index.includes('./src/styles/shell.css')],
  ['versão 0.27', index.includes('Urbe v0.27.0') && app.includes("V21_VERSION='0.27.0'")],
  ['diagnóstico URBE', app.includes('window.URBE')],
  ['ponte de comandos', app.includes("workspace.navigate.world") && app.includes("document.create") && app.includes("workspace.save")],
  ['diagnóstico Burgo', app.includes('window.Burgo')],
  ['core contratos', core.includes('class EventBus') && core.includes('class CommandRegistry') && core.includes('class StateStore')],
  ['cache app', sw.includes("'./src/app.js'")],
  ['cache core', sw.includes("'./src/core/core.js'")],
  ['cache css', sw.includes("'./src/styles/base.css'") && sw.includes("'./src/styles/shell.css'")],
  ['cache versionado', sw.includes('urbe-shell-v0.27.0')],
  ['sem runtime principal inline', !index.includes('V25_MAX=18')]
];

let failed = 0;
for (const [name, ok] of checks) {
  console.log((ok ? 'OK  ' : 'FAIL') + ' ' + name);
  if (!ok) failed++;
}
if (failed) process.exit(1);
