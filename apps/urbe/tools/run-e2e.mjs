#!/usr/bin/env node
// Executa tests/e2e/*.e2e.mjs (REQ-061): cada arquivo é um script Node independente que usa Chromium real.
//   node tools/run-e2e.mjs [filtro...]     (sequencial; sai com código != 0 se algum falhar)
import { readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const filters = process.argv.slice(2);
const files = readdirSync(join(ROOT, 'tests/e2e')).filter((f) => f.endsWith('.e2e.mjs')).sort().filter((f) => !filters.length || filters.some((x) => f.includes(x)));
let failed = 0;
for (const f of files) {
  const t0 = Date.now();
  console.log(`\n▶ ${f}`);
  const r = spawnSync(process.execPath, [join('tests/e2e', f)], { cwd: ROOT, stdio: 'inherit', timeout: 10 * 60 * 1000 });
  const ok = r.status === 0;
  if (!ok) failed++;
  console.log(`${ok ? 'ok  ' : 'FAIL'} ${f} (${((Date.now() - t0) / 1000).toFixed(1)}s)`);
}
console.log(`\n${files.length - failed}/${files.length} cenários E2E passaram`);
process.exit(failed ? 1 : 0);
