// Coleta de órfãos (REQ-042, RM-F1-16): simulação por padrão, retenção configurável, registro em vault.json,
// e nenhum dado ativo perdido (documento vivo ou na lixeira nunca é órfão).
import assert from 'node:assert/strict';
import { createEnv, readFixture, readExpect } from './helpers/vault-env.mjs';

const E = readExpect('v1-orfaos'), NOW = Date.UTC(2026, 8, 30); // 30/09/2026
const J = (x) => JSON.parse(JSON.stringify(x));
const run = (env, ctx) => env.core.commands.execute('workspace.gc', { now: NOW, ...ctx });

// 1) simulação: relata e não muda nada
{
  const env = createEnv(readFixture('v1-orfaos')); await env.p.load('V');
  const before = [...env.disk()];
  const r = J(await run(env));
  assert.equal(r.dryRun, true);
  assert.deepEqual(r.plan.history.map((h) => h.id), E.orphans.historyOld, 'histórico órfão antigo (o recente fica pela retenção de 30 dias)');
  assert.deepEqual(r.plan.compositions[0].sources, E.orphans.compositionDangling, 'fonte de composição sem documento; a nota na lixeira não conta');
  assert.deepEqual(r.plan.trash, [], 'lixeira não expira sem trashDays');
  assert.deepEqual([...env.disk()], before, 'dry-run não grava nada');
  assert.equal(Object.keys(env.history.export().documents).length, E.history);
}

// 2) execução: coleta só o órfão, registra em vault.json, não toca dados ativos
{
  const env = createEnv(readFixture('v1-orfaos')); await env.p.load('V');
  const evs = []; env.core.events.on('workspace:gc', (e) => evs.push(e));
  const r = J(await run(env, { dryRun: false }));
  assert.deepEqual(r.counts, { history: 1, trash: 0, compositionRefs: 1 });
  const hist = JSON.parse(env.get('.urbe/history.v2.json')).documents;
  assert.deepEqual(Object.keys(hist).sort(), [E.ids['Alfa.md'], ...E.orphans.historyRecent, ...E.orphans.trashed].sort(), 'vivo, recente e na lixeira permanecem');
  const comp = JSON.parse(env.get('.urbe/compositions.v2.json')).items[0];
  assert.deepEqual(comp.sources, [E.ids['Alfa.md'], ...E.orphans.trashed], 'composição mantida, só a fonte órfã sai');
  assert.equal(JSON.parse(env.get('.urbe/trash.v2.json')).items.length, 1);
  const vj = JSON.parse(env.get('.urbe/vault.json'));
  assert.equal(vj.maintenance.at(-1).kind, 'gc'); assert.deepEqual(vj.maintenance.at(-1).removed, r.counts);
  assert.ok(vj.migrations.length === 1 && vj.migrations[0].backup, 'GC em vault 1.x passa pela migração com backup antes de gravar');
  assert.equal(evs.length, 1);
  assert.equal(env.get('Alfa.md'), readFixture('v1-orfaos').get('Alfa.md'), 'notas intactas');
  assert.equal(env.get('.urbe/history.json'), readFixture('v1-orfaos').get('.urbe/history.json'), 'v1 intacto');
  // idempotente: segunda execução não encontra nada
  assert.deepEqual(J(await run(env, { dryRun: false })).counts, { history: 0, trash: 0, compositionRefs: 0 });
}

// 3) retenção configurável: orphanDays=0 leva o recente; trashDays expira a lixeira (e aí o histórico dela vira órfão na próxima)
{
  const env = createEnv(readFixture('v1-orfaos')); await env.p.load('V');
  const r = J(await run(env, { dryRun: false, orphanDays: 0, trashDays: 7 }));
  assert.deepEqual(r.counts, { history: 2, trash: 1, compositionRefs: 1 });
  const r2 = J(await run(env, { dryRun: false, orphanDays: 0 }));
  assert.deepEqual(r2.plan.history.map((h) => h.id), E.orphans.trashed, 'histórico do item expirado vira órfão');
  assert.equal(r2.counts.compositionRefs, 1, 'e a referência na composição também');
}

// 4) vault somente leitura: recusa aplicar
{
  const env = createEnv(readFixture('vault-futuro')); await env.p.load('V');
  await assert.rejects(() => run(env, { dryRun: false }), /somente leitura/);
}
console.log('gc: ok');
