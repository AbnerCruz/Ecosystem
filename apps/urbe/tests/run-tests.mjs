// tools/run-tests.mjs agrega falhas e não para no primeiro erro (REQ-063).
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, copyFileSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const tmp = mkdtempSync(join(tmpdir(), 'urbe-rt-'));
mkdirSync(join(tmp, 'tests'), { recursive: true }); mkdirSync(join(tmp, 'tools'), { recursive: true });
copyFileSync(join(root, 'tools/run-tests.mjs'), join(tmp, 'tools/run-tests.mjs'));
writeFileSync(join(tmp, 'tests/a-ok.mjs'), "console.log('a')");
writeFileSync(join(tmp, 'tests/b-falha.mjs'), "console.error('boom'); process.exit(1)");
writeFileSync(join(tmp, 'tests/c-ok.mjs'), "console.log('c')");
const r = spawnSync(process.execPath, [join(tmp, 'tools/run-tests.mjs'), '--jobs', '1'], { encoding: 'utf8' });
assert.equal(r.status, 1, 'falha em um arquivo → código 1');
assert.match(r.stdout, /ok\s+a-ok\.mjs/); assert.match(r.stdout, /FAIL b-falha\.mjs/);
assert.match(r.stdout, /ok\s+c-ok\.mjs/, 'não pára no primeiro erro');
assert.match(r.stdout, /2\/3 arquivos/);
assert.ok(readdirSync(join(root, 'tests')).length > 3);
console.log('run-tests: ok');
