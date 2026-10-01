// Teste do verificador de rastreabilidade da 2.0 (REQ-002, REQ-024, REQ-068): o estado atual passa e regressões falham.
import assert from 'node:assert/strict';
import { check } from '../tools/check-traceability.mjs';
import { render } from '../tools/gen-traceability.mjs';
import { read } from '../tools/lib/v2-docs.mjs';

const base = { requirements: read('REQUIREMENTS.md'), spec: read('SPEC.md'), roadmap: read('ROADMAP.md'), trace: render(), skipFiles: true };
let r = check(base);
assert.deepEqual(r.errors, [], 'estado atual deve estar íntegro:\n' + r.errors.join('\n'));

// REQ removido do ledger é detectado
r = check({ ...base, requirements: base.requirements.replace(/^\| REQ-030 \|.*$/m, '') });
assert.ok(r.errors.some((e) => e.includes('REQ-030')), 'remoção silenciosa de REQ deve falhar');

// REQ IMPLEMENTAR sem cláusula na SPEC
r = check({ ...base, spec: base.spec.replace(/^- \*\*REQ-058\*\* —/m, '- REQ-058 —') });
assert.ok(r.errors.some((e) => e.includes('REQ-058') && e.includes('SPEC')), 'REQ sem cláusula deve falhar');

// REQ IMPLEMENTAR sem item no ROADMAP
r = check({ ...base, roadmap: base.roadmap.replace(/- \*\*REQ:\*\* REQ-086/g, '- **REQ:** REQ-001') });
assert.ok(r.errors.some((e) => e.includes('REQ-086') && e.includes('ROADMAP')), 'REQ sem item deve falhar');

// TRACEABILITY desatualizada
r = check({ ...base, trace: base.trace.replaceAll('REQ-070', 'REQ-0X0') });
assert.ok(r.errors.some((e) => e.includes('REQ-070') && e.includes('TRACEABILITY')), 'matriz sem REQ deve falhar');

// Dependência inexistente
r = check({ ...base, roadmap: base.roadmap.replace('- **Depende:** RM-F0-02', '- **Depende:** RM-F9-99') });
assert.ok(r.errors.some((e) => e.includes('RM-F9-99')), 'dependência inexistente deve falhar');

console.log('traceability: ok');
