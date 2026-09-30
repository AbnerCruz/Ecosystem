#!/usr/bin/env node
// Runner multiplataforma dos testes (REQ-063): executa todo tests/*.mjs em processos filhos, com
// concorrência limitada, agrega falhas e retorna código != 0 se algum falhar. Não depende de shell POSIX.
//   node tools/run-tests.mjs [--jobs N] [filtro...]   (filtro = trecho do nome do arquivo)
import { readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { spawn } from 'node:child_process';
import { cpus } from 'node:os';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
let jobs = Math.max(1, Math.min(4, cpus().length));
const filters = [];
for (let i = 0; i < args.length; i++) { if (args[i] === '--jobs') jobs = Math.max(1, +args[++i] || 1); else filters.push(args[i]); }

// tests/*.mjs e tests/security/*.mjs (tests/e2e, helpers, lib e fixtures não são testes unitários)
const DIRS = ['', 'security'];
const files = DIRS.flatMap((d) => readdirSync(join(ROOT, 'tests', d), { withFileTypes: true }).filter((e) => e.isFile() && e.name.endsWith('.mjs')).map((e) => (d ? d + '/' : '') + e.name)).sort()
  .filter((f) => !filters.length || filters.some((x) => f.includes(x)));
if (!files.length) { console.error('nenhum teste encontrado'); process.exit(1); }

const run = (file) => new Promise((resolve) => {
  const t0 = Date.now();
  const child = spawn(process.execPath, [join('tests', ...file.split('/'))], { cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'] });
  let out = '';
  child.stdout.on('data', (d) => { out += d; });
  child.stderr.on('data', (d) => { out += d; });
  child.on('close', (code) => resolve({ file, code, out, ms: Date.now() - t0 }));
  child.on('error', (e) => resolve({ file, code: 1, out: String(e), ms: Date.now() - t0 }));
});

const queue = [...files], results = [];
await Promise.all(Array.from({ length: Math.min(jobs, files.length) }, async () => {
  while (queue.length) {
    const r = await run(queue.shift());
    results.push(r);
    console.log(`${r.code === 0 ? 'ok  ' : 'FAIL'} ${r.file} (${(r.ms / 1000).toFixed(1)}s)`);
  }
}));
const failed = results.filter((r) => r.code !== 0);
for (const r of failed) console.log(`\n::group::${r.file}\n${r.out}\n::endgroup::`);
console.log(`\n${results.length - failed.length}/${results.length} arquivos de teste passaram${failed.length ? ' — FALHAS: ' + failed.map((r) => r.file).join(', ') : ''}`);
process.exit(failed.length ? 1 : 0);
