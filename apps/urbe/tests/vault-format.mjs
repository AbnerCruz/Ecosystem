// Formato do vault (REQ-036, REQ-035, REQ-038): vault.json, migração 1→2 com backup restaurável e proteção forward.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createEnv, readFixture, readExpect } from './helpers/vault-env.mjs';

const sha = (s) => createHash('sha256').update(s).digest('hex');
const snapshot = (env) => new Map([...env.disk()].map(([k, v]) => [k, sha(v)]));

// 1) vault 1.x → 2.x: backup restaurável, v1 intactos, v2 gravados, vault.json com a migração
{
  const files = readFixture('v1-mapa-v4'), env = createEnv(files), { p, docs } = env;
  const before = snapshot(env);
  const events = []; env.core.events.on('workspace:migrated', (e) => events.push(e));
  await p.load('V');
  assert.equal(p.info().format, 'absent', 'sem vault.json = vault 1.x');
  assert.equal(env.has('.urbe/vault.json'), false, 'nada é gravado só por abrir');
  docs.upsert({ ...docs.get('Alfa.md'), content: docs.get('Alfa.md').content + 'edição\n' });
  await p.flush();
  const vj = JSON.parse(env.get('.urbe/vault.json'));
  assert.equal(vj.formatVersion, 2); assert.match(vj.createdBy, /^urbe@/);
  assert.equal(vj.migrations.length, 1); assert.deepEqual([vj.migrations[0].from, vj.migrations[0].to], [1, 2]);
  const dir = vj.migrations[0].backup; assert.match(dir, /^\.urbe\/backup\/\d{8}-\d{6}-1-2/);
  assert.equal(events.length, 1, 'evento workspace:migrated');
  // v1 intactos (bytes) e v2 gravados
  for (const rel of ['.urbe/history.json', '.urbe/trash.json', '.urbe/compositions.json']) assert.equal(sha(env.get(rel)), before.get(rel), `${rel} (v1) nunca é reescrito`);
  for (const rel of ['.urbe/history.v2.json', '.urbe/trash.v2.json', '.urbe/compositions.v2.json']) assert.ok(env.has(rel), rel + ' gravado');
  assert.equal(JSON.parse(env.get('.urbe/history.v2.json')).version, 2);
  assert.equal(env.trash.list().length, 1); // itens migrados do v1
  // backup íntegro: manifest + arquivos v1 com o mesmo conteúdo
  const man = JSON.parse(env.get(dir + '/manifest.json'));
  assert.deepEqual(man.files.map((f) => f.path).sort(), ['.urbe/compositions.json', '.urbe/history.json', '.urbe/mapa.json', '.urbe/trash.json']);
  for (const f of man.files) { assert.equal(env.get(dir + '/files/' + f.path.replace(/^\.urbe\//, 'urbe/')), files.get(f.path), 'cópia de ' + f.path); assert.equal(f.sha256, sha(files.get(f.path))); }
  // idempotência: segunda abertura + gravação não cria novo backup
  const env2 = createEnv(env.disk()); await env2.p.load('V');
  assert.equal(env2.p.info().format, 'current');
  env2.docs.upsert({ ...env2.docs.get('Beta.md'), content: 'x\n' }); await env2.p.flush();
  assert.equal([...env2.disk().keys()].filter((k) => /^\.urbe\/backup\/[^/]+\/manifest\.json$/.test(k)).length, 1, 'migração idempotente (um backup só)');
  assert.equal(JSON.parse(env2.get('.urbe/vault.json')).migrations.length, 1);
  // restauração pelo comando: volta os arquivos v1 ao estado do backup
  assert.equal(env2.core.commands.execute('workspace.backups').constructor.name, 'Promise');
  assert.equal((await env2.core.commands.execute('workspace.backups'))[0].dir, dir);
  await env2.adapter.write('V', '.urbe/history.json', 'CORROMPIDO');
  const restored = await env2.core.commands.execute('workspace.restoreBackup', { dir, reload: false });
  assert.ok(restored.restored.includes('.urbe/history.json')); assert.equal(env2.get('.urbe/history.json'), files.get('.urbe/history.json'));
}

// 2) vault novo (sem nada): vault.json sem migração e sem backup
{
  const env = createEnv(new Map()), { p, docs } = env;
  await p.load('V'); docs.upsert({ path: 'Nova.md', content: '# Nova\n' }); await p.flush();
  const vj = JSON.parse(env.get('.urbe/vault.json'));
  assert.deepEqual(Array.from(vj.migrations), []);
  assert.equal([...env.disk().keys()].some((k) => k.startsWith('.urbe/backup/')), false);
}

// 3) vault.json corrompido: backup do arquivo e recriação
{
  const files = readFixture('v1-mapa-v4'); files.set('.urbe/vault.json', '{ isso não é json');
  const env = createEnv(files); await env.p.load('V');
  assert.equal(env.p.info().format, 'corrupt');
  env.docs.upsert({ ...env.docs.get('Alfa.md'), content: 'y\n' }); await env.p.flush();
  const vj = JSON.parse(env.get('.urbe/vault.json'));
  const man = JSON.parse(env.get(vj.migrations[0].backup + '/manifest.json'));
  assert.ok(man.files.some((f) => f.path === '.urbe/vault.json'), 'vault.json corrompido preservado no backup');
}

// 4) proteção forward por artefato (futuro-v2): preserva o arquivo e desliga só a escrita daquele artefato
{
  const files = readFixture('futuro-v2'), expect = readExpect('futuro-v2'), env = createEnv(files);
  const evs = []; env.core.events.on('workspace:foreign', (e) => evs.push(e));
  await env.p.load('V');
  const info = env.p.info();
  assert.equal(info.format, 'current'); assert.equal(info.readOnly, false);
  assert.deepEqual(Object.keys(info.sideReadonly).sort(), ['compositions', 'history', 'journal', 'trash']);
  assert.ok(evs.length === 1 && evs[0].items.length >= 4, 'workspace:foreign informa os arquivos preservados');
  env.docs.upsert({ ...env.docs.get('Alfa.md'), content: 'editada\n' }); env.docs.upsert({ ...env.docs.get('Beta.md'), content: 'editada também\n' }); await env.p.flush(); // duas notas ⇒ operação multi-arquivo (tentaria usar o diário)
  for (const rel of expect.futureFiles) assert.equal(sha(env.get(rel)), expect.futureHashes[rel], rel + ' preservado');
  assert.equal(env.get('Alfa.md'), 'editada\n', 'as notas continuam sendo gravadas');
  assert.equal(JSON.parse(env.get('.urbe/mapa.json')).v, 4);
}

// 5) vault de formato futuro: somente leitura (nenhum byte muda)
{
  const files = readFixture('vault-futuro'), expect = readExpect('vault-futuro'), env = createEnv(files);
  const before = snapshot(env);
  const evs = []; env.core.events.on('workspace:readonly', (e) => evs.push(e));
  await env.p.load('V');
  assert.equal(env.p.info().readOnly, true); assert.equal(evs.length, 1);
  assert.equal(env.docs.get('Alfa.md').id, expect.ids['Alfa.md'], 'leitura funciona');
  env.docs.upsert({ ...env.docs.get('Alfa.md'), content: 'tentativa\n' });
  assert.equal(await env.p.flush(), false);
  assert.deepEqual([...snapshot(env)], [...before], 'nenhum arquivo alterado, criado ou apagado');
}

// 6) mapa v>4 é preservado (persistência não o reescreve)
{
  const files = readFixture('futuro-desconhecido'), env = createEnv(files);
  await env.p.load('V'); assert.equal(env.p.info().mapaReadonly, true);
  env.docs.upsert({ ...env.docs.get('Alfa.md'), content: 'z\n' }); await env.p.flush();
  assert.equal(JSON.parse(env.get('.urbe/mapa.json')).v, 99);
}

// 6b) mapa.v inválido (não é inteiro ≥ 1) é preservado; mapa sem `v` é legado e continua gravável (REQ-040)
{
  for (const [v, readonly] of [['quatro', true], [0, true], [2.5, true], [undefined, false], [2, false]]) {
    const files = readFixture('v1-mapa-v4'), m = JSON.parse(files.get('.urbe/mapa.json'));
    if (v === undefined) delete m.v; else m.v = v;
    files.set('.urbe/mapa.json', JSON.stringify(m));
    const env = createEnv(files); await env.p.load('V');
    assert.equal(env.p.info().mapaReadonly, readonly, 'mapa.v=' + String(v));
    env.docs.upsert({ ...env.docs.get('Alfa.md'), content: 'w\n' }); await env.p.flush({ v: 4, notas: {} });
    if (readonly) assert.equal(env.get('.urbe/mapa.json'), files.get('.urbe/mapa.json'), 'mapa preservado (v=' + String(v) + ')');
    else assert.equal(JSON.parse(env.get('.urbe/mapa.json')).v, 4);
  }
}

// 6c) escritor único: o mapa vem do metadataProvider a cada gravação; null (mundo carregando) mantém o último mapa
{
  const env = createEnv(readFixture('v1-mapa-v4')); await env.p.load('V');
  let mapa = null; env.p.metadataProvider = () => mapa;
  env.docs.upsert({ ...env.docs.get('Alfa.md'), content: 'p\n' }); await env.p.flush();
  assert.equal(JSON.parse(env.get('.urbe/mapa.json')).v, 4, 'sem provider pronto, grava o mapa conhecido');
  mapa = { v: 4, notas: { 'Alfa.md': { x: 77, y: 1 } }, regioes: [], construcoes: [] };
  env.docs.upsert({ ...env.docs.get('Alfa.md'), content: 'q\n' }); await env.p.flush();
  assert.equal(JSON.parse(env.get('.urbe/mapa.json')).notas['Alfa.md'].x, 77, 'mapa atual do provider');
  env.p.metadataProvider = () => { throw new Error('quebrado'); };
  const warn = console.warn; console.warn = () => {};
  env.docs.upsert({ ...env.docs.get('Alfa.md'), content: 'r\n' }); await env.p.flush(); console.warn = warn;
  assert.equal(JSON.parse(env.get('.urbe/mapa.json')).notas['Alfa.md'].x, 77, 'provider com erro não apaga o mapa');
}

// 7) journal v2 é recuperado e removido
{
  const files = new Map([['A.md', 'disco\n']]);
  files.set('.urbe/journal.v2.json', JSON.stringify({ version: 2, timestamp: 1, documents: [{ id: 'doc_x', path: 'A.md', content: 'recuperado\n' }] }));
  const env = createEnv(files); await env.p.load('V');
  assert.equal(env.docs.get('A.md').content, 'recuperado\n'); assert.equal(env.has('.urbe/journal.v2.json'), false);
}
console.log('vault-format: ok');
