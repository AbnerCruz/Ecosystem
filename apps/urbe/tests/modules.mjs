// Manifesto de módulos (REQ-025): reproduz index.html/sw.js e rejeita ordem inválida, ciclo, arquivo fora do manifesto e uso não declarado.
import assert from 'node:assert/strict';
import { check } from '../tools/check-modules.mjs';
import { loadManifest, rd, derive } from '../tools/lib/modules.mjs';

const clone = (o) => JSON.parse(JSON.stringify(o));
const base = loadManifest();
assert.deepEqual(check(), [], 'estado atual íntegro');

// reproduz o conjunto e a ordem do index.html e do sw.js
const html = rd('index.html'), sw = rd('sw.js');
const inHtml = [...html.matchAll(/<script src="\.\/([^"]+)"><\/script>/g)].map((m) => m[1]);
assert.deepEqual(inHtml, base.modules.map((m) => m.file), 'ordem do index.html = manifesto');
const swFiles = [...sw.matchAll(/'\.\/([^']+)'/g)].map((m) => m[1]).filter((f) => /\.(js|css)$/.test(f));
for (const f of [...base.modules.map((m) => m.file), ...base.styles]) assert.ok(swFiles.includes(f), 'sw.js precacheia ' + f);
assert.ok(swFiles.includes('src/world/chunk-worker.js'), 'worker precacheado');
const d = derive(base); assert.ok(d.scripts.includes('./src/app.js'));

// ordem inválida: app.js movido para o começo
let m = clone(base); const app = m.modules.splice(m.modules.findIndex((x) => x.file === 'src/app.js'), 1)[0]; m.modules.unshift(app);
assert.ok(check({ manifest: m }).some((e) => /só carrega depois|diverge/.test(e)), 'ordem inválida detectada');

// ciclo: core.js passa a exigir app.js
m = clone(base); m.modules.find((x) => x.file === 'src/core/core.js').requires.push('src/app.js');
assert.ok(check({ manifest: m }).some((e) => /core\.js.*só carrega depois/.test(e)), 'ciclo/dependência futura detectada');

// arquivo de src/ fora do manifesto
m = clone(base); m.modules = m.modules.filter((x) => x.file !== 'src/ui/tips.js');
assert.ok(check({ manifest: m }).some((e) => /src\/ui\/tips\.js.*fora do manifesto/.test(e)), 'arquivo fora do manifesto detectado');

// uso de global sem declarar
m = clone(base); const q = m.modules.find((x) => x.file === 'src/ui/quick-open.js'); q.requires = q.requires.filter((r) => r !== 'src/explorer/model.js');
assert.ok(check({ manifest: m }).some((e) => /quick-open\.js.*sem declarar/.test(e)), 'dependência não declarada detectada');

// provides escondido
m = clone(base); m.modules.find((x) => x.file === 'src/core/scheduler.js').provides.globals = [];
assert.ok(check({ manifest: m }).some((e) => /scheduler\.js.*sem declarar em provides/.test(e)), 'global publicado sem declarar');

// derivados divergentes
assert.ok(check({ html: html.replace('./src/ui/tips.js', './src/ui/tipz.js') }).some((e) => /index\.html \(scripts\)/.test(e)), 'index.html divergente detectado');
assert.ok(check({ sw: sw.replace("'./src/app.js',", '') }).some((e) => /sw\.js/.test(e)), 'sw.js divergente detectado');
console.log('modules: ok');
