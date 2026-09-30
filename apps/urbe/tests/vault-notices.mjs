// Avisos de formato do vault chegam ao usuário (RM-F1-05/06/07).
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const el = { textContent: '', classList: { add() {}, remove() {} } };
const alerts = [];
const ctx = { window: { UrbeDialogs: { alert: (o) => alerts.push(o) } }, document: { getElementById: (id) => (id === 'toast' ? el : null) }, setTimeout, clearTimeout };
ctx.window.document = ctx.document; vm.createContext(ctx);
for (const f of ['src/core/artifacts.js','src/core/core.js', 'src/ui/vault-notices.js']) vm.runInContext(fs.readFileSync(new URL('../' + f, import.meta.url), 'utf8'), ctx);
const core = ctx.window.UrbeCore;

core.events.emit('workspace:foreign', { items: [{ path: '.urbe/history.v2.json', version: 3 }, { path: '.urbe/vault.json', version: 3 }] });
assert.match(el.textContent, /versão mais nova/); assert.match(el.textContent, /history\.v2\.json/); assert.ok(!/vault\.json/.test(el.textContent), 'vault.json não entra na lista (tem diálogo próprio)');
core.events.emit('workspace:readonly', { formatVersion: 3 });
assert.equal(alerts.length, 1); assert.match(alerts[0].message, /somente para leitura/);
core.events.emit('workspace:migrated', { backup: '.urbe/backup/20261001-000000-1-2' });
assert.match(el.textContent, /formato do Urbe 2\.0/); assert.match(el.textContent, /\.urbe\/backup\/20261001-000000-1-2/);
console.log('vault-notices: ok');
