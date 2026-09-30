// Licença e avisos de terceiros (REQ-017, REQ-080, ADR-0003).
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, cpSync, writeFileSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

const root = new URL('..', import.meta.url).pathname;
const run = (dir) => spawnSync(process.execPath, [join(root, 'tools/check-license.mjs')], { env: { ...process.env, URBE_ROOT: dir }, encoding: 'utf8' });
assert.equal(run(root).status, 0, 'estado atual coerente');

const tmp = mkdtempSync(join(tmpdir(), 'urbe-lic-'));
for (const f of ['LICENSE', 'THIRD-PARTY-NOTICES.md', 'package.json']) cpSync(join(root, f), join(tmp, f));
mkdirSync(join(tmp, 'vendor'), { recursive: true }); cpSync(join(root, 'vendor/katex/LICENSE'), join(tmp, 'vendor/katex/LICENSE'));
assert.equal(run(tmp).status, 0, 'cópia mínima passa');
writeFileSync(join(tmp, 'LICENSE'), 'MIT License'); let r = run(tmp);
assert.equal(r.status, 1); assert.match(r.stderr, /direitos reservados/);
cpSync(join(root, 'LICENSE'), join(tmp, 'LICENSE'));
const pkg = JSON.parse(readFileSync(join(tmp, 'package.json'), 'utf8')); pkg.license = 'MIT'; writeFileSync(join(tmp, 'package.json'), JSON.stringify(pkg));
r = run(tmp); assert.match(r.stderr, /SEE LICENSE IN LICENSE/);
pkg.license = 'SEE LICENSE IN LICENSE'; writeFileSync(join(tmp, 'package.json'), JSON.stringify(pkg));
writeFileSync(join(tmp, 'THIRD-PARTY-NOTICES.md'), 'katex jszip pako pdf.js electron electron-updater capacitor');
r = run(tmp); assert.match(r.stderr, /chokidar/);
console.log('license: ok');
