// UC-2: dados de aceite atravessam cliente real; corrupção, omissão e sequência ignorada não podem passar.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { ROOT } from '../tools/lib/v2-docs.mjs';
import { json, compareResults } from '../tools/lib/csharp-parity.mjs';
import { runVaultCase } from '../tools/lib/parity-vault.mjs';

const cases = json('docs/csharp/acceptance/cases.json').cases.filter((c) => c.operation.startsWith('vault.'));
assert.equal(cases.filter((c) => c.operation === 'vault.scenario').length, 12);
assert.equal(cases.filter((c) => c.operation === 'vault.restore').length, 3);
const run = spawnSync(process.execPath, ['tools/parity-vault-client.mjs'], {
  cwd: ROOT, input: JSON.stringify({ schemaVersion: 1, cases }), encoding: 'utf8', timeout: 30000 });
assert.equal(run.status, 0, run.stderr);
const results = JSON.parse(run.stdout).results;
compareResults(cases, results);
for (const [id, key] of [['vault-vault-futuro','wholeVaultUnchanged'], ['vault-v1-mapa-v4','backupIntegrity'],
  ['vault-v1-notas-sem-id','idsStableAfterReload'], ['vault-backup-corrupt','restoreRejected']]) {
  const r = structuredClone(results), found = r.find((x) => x.id === id);
  found.output[key] = !found.output[key]; assert.throws(() => compareResults(cases, r), id);
}
const future = cases.find((c) => c.id === 'vault-vault-futuro');
const missing = structuredClone(future.input); missing.steps.push('erase-vault');
await assert.rejects(() => runVaultCase(missing), /não suportada/);
const binary = cases.find((c) => c.id === 'vault-v1-mapa-v4');
assert.ok(binary.input.files.some((f) => f.encoding === 'base64' && f.path.endsWith('.png')), 'bytes binários no corpus');
const changed = structuredClone(binary.input);
changed.files.find((f) => f.path.endsWith('.png')).content = Buffer.from('diferente').toString('base64');
const out = await runVaultCase(changed);
assert.throws(() => compareResults([binary], [{ id: binary.id, output: out }]), 'asset alterado deve falhar');
const journal = cases.find((c) => c.id === 'vault-v1-journal-pendente');
assert.ok(journal.expected.diskHashes['Delta.md'], 'nota recuperada tem prova de conteúdo, não só de presença');
console.log('csharp-vault-parity: ok (12 ciclos + 3 restaurações, bytes e mutações negativas)');
