// tools/version.mjs: a fonte única (package.json) valida e sincroniza as demais superfícies (REQ-019, REQ-065).
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, copyFileSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const tmp = mkdtempSync(join(tmpdir(), 'urbe-version-'));
for (const f of ['package.json', 'package-lock.json', 'CHANGELOG.md', 'README.md', 'index.html', 'sw.js', 'src/core/core.js', 'src/app.js']) {
  mkdirSync(dirname(join(tmp, f)), { recursive: true });
  copyFileSync(join(root, f), join(tmp, f));
}
const run = (cmd) => spawnSync(process.execPath, [join(root, 'tools/version.mjs'), cmd], { env: { ...process.env, URBE_ROOT: tmp }, encoding: 'utf8' });

assert.equal(run('check').status, 0, 'estado atual coerente');

const sw = join(tmp, 'sw.js');
writeFileSync(sw, readFileSync(sw, 'utf8').replace(/urbe-shell-v[^']+'/, "urbe-shell-v0.0.1'"));
let r = run('check');
assert.equal(r.status, 1); assert.match(r.stderr, /sw\.js/, 'drift no cache do SW deve falhar');

const app = join(tmp, 'src/app.js');
writeFileSync(app, readFileSync(app, 'utf8') + "\nV21_VERSION='9.9.9';\n");
r = run('check');
assert.match(r.stderr, /V21_VERSION/, 'segunda atribuição de V21_VERSION deve falhar');
writeFileSync(app, readFileSync(app, 'utf8').replace("\nV21_VERSION='9.9.9';\n", ''));

const pkg = join(tmp, 'package.json');
writeFileSync(pkg, readFileSync(pkg, 'utf8').replace(/"version": "[^"]+"/, '"version": "9.9.9-beta"'));
assert.equal(run('check').status, 1, 'package.json novo sem sync deve falhar');
r = run('sync');
assert.match(r.stderr, /CHANGELOG/, 'sync corrige superfícies, mas exige entrada de CHANGELOG (humana)');
assert.match(readFileSync(sw, 'utf8'), /urbe-shell-v9\.9\.9-beta'/);
assert.match(readFileSync(join(tmp, 'index.html'), 'utf8'), /<title>Urbe v9\.9\.9-beta<\/title>/);
assert.match(readFileSync(join(tmp, 'README.md'), 'utf8'), /\*\*Versão 9\.9\.9 beta\.\*\*/);
console.log('version: ok');
