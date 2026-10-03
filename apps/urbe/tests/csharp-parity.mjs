// UC-1/UC-2: corpus não pode encolher ou virar verde com resultados ausentes/fabricados pelo runner.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { ROOT } from '../tools/lib/v2-docs.mjs';
import { json, read, verifyOracle, validateCorpus, renderParity, compareResults } from '../tools/lib/csharp-parity.mjs';

const corpus = json('docs/csharp/acceptance/cases.json'), oracle = json('docs/csharp/acceptance/oracle.json');
verifyOracle(oracle); validateCorpus(corpus);
assert.equal(read('docs/csharp/PARITY.md'), renderParity(corpus));
assert.ok(oracle.files.some((f) => f.path.includes('/.urbe/')), 'sidecars ocultos congelados');
assert.ok(oracle.files.some((f) => f.path.includes('/.urbe/identity.json')) || oracle.files.some((f) => f.path.includes('/.urbe/mapa.json')));
for (const mutate of [
  (x) => x.files.pop(), (x) => x.files.push(x.files[0]),
  (x) => x.files[0].sha256 = '0'.repeat(64), (x) => x.files[0].bytes++,
]) { const o = structuredClone(oracle); mutate(o); assert.throws(() => verifyOracle(o)); }
for (const mutate of [(x) => x.cases.pop(), (x) => x.cases[0].expected.html += 'errado',
  (x) => x.cases[1].id = x.cases[0].id, (x) => x.cases[0].source = 'inexistente']) {
  const c = structuredClone(corpus); mutate(c); assert.throws(() => validateCorpus(c));
}
const cases = corpus.cases.filter((c) => c.operation === 'markdown.render');
const run = spawnSync(process.execPath, ['tools/parity-js-client.mjs'], {
  cwd: ROOT, input: JSON.stringify({ schemaVersion: 1, cases }), encoding: 'utf8' });
assert.equal(run.status, 0, run.stderr);
const results = JSON.parse(run.stdout).results;
compareResults(cases, results);
for (const mutate of [(x) => x.pop(), (x) => x[0].output.html += 'errado',
  (x) => x[1].id = x[0].id, (x) => x[0].id = 'unknown', (x) => x[0].error = 'not implemented']) {
  const r = structuredClone(results); mutate(r); assert.throws(() => compareResults(cases, r));
}
// Runner deve falhar por timeout/processo inválido/JSON inválido/cliente que não implementa a operação.
for (const args of [
  ['markdown.render', 'executavel-inexistente-urbe'],
  ['markdown.render', process.execPath, '-e', 'process.stdout.write("invalid")'],
  ['markdown.render', process.execPath, '-e', 'process.stdout.write(JSON.stringify({schemaVersion:1,results:[]}))'],
  ['all', process.execPath, 'tools/parity-js-client.mjs'],
]) {
  const r = spawnSync(process.execPath, ['tools/csharp-parity.mjs', 'run', ...args], { cwd: ROOT, encoding: 'utf8' });
  assert.notEqual(r.status, 0, 'resultado incompleto/não implementado nunca fica verde');
}
console.log(`csharp-parity: ok (${cases.length} resultados calculados pelo JS; mutações recusadas)`);
