// Separação integração × publicação nos workflows (REQ-006, REQ-066).
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { check } from '../tools/check-workflows.mjs';

const dir = new URL('../.github/workflows/', import.meta.url);
const files = Object.fromEntries(readdirSync(dir).filter((f) => f.endsWith('.yml')).map((f) => [f, readFileSync(new URL(f, dir), 'utf8')]));
assert.deepEqual(check(files), [], 'workflows atuais respeitam as regras');

// push em main não publica
const app = files['app.yml'];
assert.ok(!/branches:\s*\[main\]/.test(app.split('jobs:')[0]) && !/push:/.test(app.split('jobs:')[0]), 'app.yml não dispara em push');
assert.ok(!/softprops|gh release/.test(app), 'app.yml não publica');
// release só por tag, depende de testes e do build
const rel = files['release.yml'];
assert.match(rel, /tags:\s*\['v\*'\]/); assert.match(rel, /npm run check/); assert.match(rel, /needs:\s*\[verificar, construir\]/);
assert.match(rel, /github\.ref_type.*tag/s, 'exige execução sobre tag');
assert.match(rel, /correspond/, 'tag precisa bater com package.json');

// regressões
assert.ok(check({ ...files, 'app.yml': app + '\n      - uses: softprops/action-gh-release@v2\n' }).some((e) => /só release\.yml pode publicar/.test(e)), 'publicar fora do release.yml falha');
assert.ok(check({ ...files, 'release.yml': rel.replace("tags: ['v*']", 'branches: [main]') }).some((e) => /tag v\*/.test(e)), 'release por branch falha');
assert.ok(check({ ...files, 'release.yml': rel.replace('npm run check', 'true') }).some((e) => /npm run check/.test(e)), 'release sem testes falha');
assert.ok(check({ ...files, 'structural-checks.yml': files['structural-checks.yml'].replace(/^permissions:[\s\S]*?\n\n/m, '') }).some((e) => /permissions/.test(e)), 'sem permissions mínimas falha');
console.log('workflows: ok');
