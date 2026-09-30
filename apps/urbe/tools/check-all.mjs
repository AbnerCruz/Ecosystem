#!/usr/bin/env node
// `npm run check` (REQ-004): todas as sincronizações docs×código e os testes, em sequência; não para no primeiro erro.
import { spawnSync } from 'node:child_process';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const steps = [
  ['versão única', 'tools/version.mjs', 'check'],
  ['módulos, ordem e boundaries', 'tools/check-modules.mjs'],
  ['dívida de app.js', 'tools/check-debt.mjs'],
  ['rastreabilidade (REQ → SPEC → ROADMAP)', 'tools/check-traceability.mjs'],
  ['TRACEABILITY.md em dia', 'tools/gen-traceability.mjs', '--check'],
  ['workflows', 'tools/check-workflows.mjs'],
  ['licença e avisos de terceiros', 'tools/check-license.mjs'],
  ['catálogo de dados', 'tools/check-catalog.mjs'],
  ['fixtures de vaults em dia', 'tools/make-fixtures.mjs', '--check'],
  ['tutorial em dia', 'tools/build-tutorial.mjs', '--check'],
  ['testes (tests/*.mjs)', 'tools/run-tests.mjs'],
];
const only = process.argv.slice(2).filter((a) => !a.startsWith('-'));
let failed = [];
for (const [name, file, ...args] of steps) {
  if (only.length && !only.some((o) => name.includes(o) || file.includes(o))) continue;
  console.log(`\n▶ ${name}`);
  const r = spawnSync(process.execPath, [join(ROOT, file), ...args], { cwd: ROOT, stdio: 'inherit' });
  if (r.status !== 0) failed.push(name);
}
console.log(failed.length ? `\n✖ falharam: ${failed.join('; ')}` : '\n✔ todas as checagens passaram');
process.exit(failed.length ? 1 : 0);
