// Boundaries de camada (REQ-015): sem violação nova; exceções conhecidas só diminuem.
import assert from 'node:assert/strict';
import { loadManifest, boundaryViolations, boundaryExceptions, LAYERS } from '../tools/lib/modules.mjs';
import { check } from '../tools/check-modules.mjs';

const clone = (o) => JSON.parse(JSON.stringify(o));
const base = loadManifest();
const key = (x) => x.from + ' -> ' + x.to;
const viol = boundaryViolations(base), exc = boundaryExceptions();
assert.deepEqual(viol.map(key).sort(), exc.map(key).sort(), 'violações == exceções registradas');
assert.ok(viol.length <= 11, 'o número de exceções nunca cresce (teto 11 no início da 2.0)');
assert.ok(LAYERS.core <= LAYERS.persistence && LAYERS.persistence < LAYERS.feature && LAYERS.feature < LAYERS.ui && LAYERS.ui < LAYERS.app);

// injeta dependência proibida: core depende de feature
let m = clone(base);
m.modules.find((x) => x.file === 'src/core/knowledge-index.js').uses.push('src/editor/session.js');
assert.ok(check({ manifest: m }).some((e) => /boundary: src\/core\/knowledge-index\.js → src\/editor\/session\.js/.test(e)), 'core → feature deve falhar');

// world depende de ai
m = clone(base);
m.modules.find((x) => x.file === 'src/world/renderer.js').uses.push('src/ai/agent.js');
assert.ok(check({ manifest: m }).some((e) => /world depende de ai/.test(e)), 'world → ai deve falhar');

// exceção obsoleta
assert.ok(check({ exceptions: [...exc, { from: 'src/core/core.js', to: 'src/core/documents.js' }] }).some((e) => /não é mais violação/.test(e)), 'exceção obsoleta deve ser removida');
console.log('boundaries: ok');
