// Identidade documental em sidecar (REQ-042, RM-F1-15, ADR-0006): `.urbe/identity.json` + reconciliação de rename/move externo.
import assert from 'node:assert/strict';
import { createEnv, readFixture } from './helpers/vault-env.mjs';

const ID = '.urbe/identity.json';
const move = (m, from, to) => { const c = m.get(from); m.delete(from); m.set(to, c); };
const snapshotNotes = (disk) => new Map([...disk].filter(([k]) => !k.startsWith('.urbe/')));
async function session(files, edit) {
  const env = createEnv(files); await env.p.load('V');
  if (edit) await edit(env);
  env.docs.upsert({ path: '_toque.md', content: 'x' }); env.docs.remove(env.docs.get('_toque.md').id); // força um flush com o estado atual
  await env.p.flush(); return env;
}

// fingerprint: normaliza fim de linha e espaços finais; conteúdo vazio não casa
{
  const { context } = createEnv(new Map()), I = context.window.UrbeIdentity;
  assert.equal(I.fingerprint('a\r\nb  \n\n'), I.fingerprint('a\nb'));
  assert.notEqual(I.fingerprint('a\nb'), I.fingerprint('a\nc'));
  assert.equal(I.fingerprint('  \n\n'), '');
  assert.equal(I.parse(null).state, 'absent'); assert.equal(I.parse('{').state, 'corrupt'); assert.equal(I.parse('{"version":9,"docs":{}}').state, 'future');
}

// 1) sessão 1 grava o sidecar; nenhuma nota é tocada
const s1 = await session(readFixture('v1-mapa-v4'));
const idAlfa = s1.docs.get('Alfa.md').id, idGama = s1.docs.get('Pasta/Gama.md').id;
const sidecar = JSON.parse(s1.get(ID));
assert.equal(sidecar.version, 1);
assert.equal(sidecar.docs[idAlfa].path, 'Alfa.md'); assert.ok(sidecar.docs[idAlfa].fingerprint && sidecar.docs[idAlfa].seen);
for (const [rel, c] of snapshotNotes(readFixture('v1-mapa-v4'))) if (rel.endsWith('.md')) assert.equal(s1.get(rel), c, rel + ' sem alteração (nada é escrito nas notas)');
// sem mudança, o sidecar não é regravado (seen estável)
const before = s1.get(ID); s1.docs.upsert({ ...s1.docs.get('Beta.md') }); await s1.p.flush(); assert.equal(s1.get(ID), before);

// 2) rename + move externo com o app fechado: mesmo ID, casa e vínculos
{
  const disk = s1.disk(); move(disk, 'Alfa.md', 'Arquivo/Alfa renomeada.md');
  const mapa0 = JSON.parse(disk.get('.urbe/mapa.json'));
  const env = createEnv(disk); const evs = []; env.core.events.on('workspace:reconciled', (e) => evs.push(e));
  await env.p.load('V');
  assert.equal(env.docs.get('Arquivo/Alfa renomeada.md').id, idAlfa, 'rename externo preserva o ID');
  assert.equal(JSON.stringify(evs[0].renames.map((r) => [r.from, r.to])), JSON.stringify([['Alfa.md', 'Arquivo/Alfa renomeada.md']]));
  const n = env.p.meta.notas['Arquivo/Alfa renomeada.md'];
  assert.equal(n?.id ?? idAlfa, idAlfa); assert.equal(n.x, undefined, 'movida para outra pasta: ganha lugar no bairro novo');
  assert.deepEqual([...n.tags], [...mapa0.notas['Alfa.md'].tags], 'metadados acompanham');
  assert.equal(env.p.meta.notas['Alfa.md'], undefined);
  await env.p.flush();
  assert.equal(JSON.parse(env.get(ID)).docs[idAlfa].path, 'Arquivo/Alfa renomeada.md');
}

// 2b) rename na mesma pasta: a casa fica no mesmo lugar
{
  const disk = s1.disk(); move(disk, 'Pasta/Gama.md', 'Pasta/Gama 2.md');
  const x0 = JSON.parse(disk.get('.urbe/mapa.json')).notas['Pasta/Gama.md'].x;
  const env = createEnv(disk); await env.p.load('V');
  assert.equal(env.docs.get('Pasta/Gama 2.md').id, idGama); assert.equal(env.p.meta.notas['Pasta/Gama 2.md'].x, x0);
}

// 3) pasta renomeada por fora: notas, região (reg_) e asset (ast_) continuam os mesmos
{
  const disk = s1.disk(), mapa = JSON.parse(disk.get('.urbe/mapa.json'));
  mapa.regioes[0].id = 'reg_pasta'; mapa.construcoes[0].id = 'ast_logo'; disk.set('.urbe/mapa.json', JSON.stringify(mapa));
  move(disk, 'Pasta/Gama.md', 'Projetos/Gama.md'); move(disk, 'Pasta/logo.png', 'Projetos/logo.png'); disk.set('Pasta/.pasta', ''); // marcador de pasta vazia não conta como pasta existente
  const env = createEnv(disk); await env.p.load('V');
  assert.equal(env.docs.get('Projetos/Gama.md').id, idGama);
  const reg = env.p.meta.regioes.find((r) => r.id === 'reg_pasta'), ast = env.p.meta.construcoes.find((c) => c.id === 'ast_logo');
  assert.equal(reg.caminho, 'Projetos', 'região renomeada mantém o ID'); assert.equal(reg.nome, 'Projetos', 'e o nome acompanha'); assert.equal(reg.x, mapa.regioes[0].x, 'e a geometria');
  assert.equal(env.p.meta.notas['Projetos/Gama.md'].x, JSON.parse(s1.get('.urbe/mapa.json')).notas['Pasta/Gama.md'].x, 'casa no mesmo lugar (a pasta mudou junto)');
  assert.equal(ast.caminho, 'Projetos'); assert.equal(ast.files[0].relPath, 'Projetos/logo.png', 'asset aponta para o caminho novo');
}

// 4) cópias idênticas: ambíguo ⇒ nenhum dos dois herda o ID; o original ainda presente nunca é "movido"
{
  const disk = s1.disk(); const c = disk.get('Beta.md'); disk.delete('Beta.md'); disk.set('B1.md', c); disk.set('B2.md', c);
  const env = createEnv(disk); const evs = []; env.core.events.on('workspace:reconciled', (e) => evs.push(e)); await env.p.load('V');
  const idBeta = s1.docs.get('Beta.md').id;
  assert.ok(env.docs.get('B1.md').id !== idBeta && env.docs.get('B2.md').id !== idBeta, 'ambiguidade não escolhe um lado');
  assert.equal(evs[0].ambiguous.length, 1);
  const copy = s1.disk(); copy.set('Beta cópia.md', copy.get('Beta.md'));
  const env2 = createEnv(copy); await env2.p.load('V');
  assert.equal(env2.docs.get('Beta.md').id, idBeta); assert.notEqual(env2.docs.get('Beta cópia.md').id, idBeta, 'cópia ganha ID novo');
}

// 5) cópia do vault sem `.urbe/`: abre, gera IDs novos e o sidecar; notas intactas
{
  const disk = new Map([...s1.disk()].filter(([k]) => !k.startsWith('.urbe/')));
  const env = await session(disk);
  assert.equal(env.docs.list().length, 3); assert.ok(env.has(ID));
  for (const [rel, c] of disk) if (rel.endsWith('.md')) assert.equal(env.get(rel), c);
}

// 6) sidecar de versão maior é preservado e não reescrito; ilegível é recriado
{
  const disk = s1.disk(); disk.set(ID, JSON.stringify({ version: 7, docs: {} }));
  const env = await session(disk); assert.equal(env.get(ID), JSON.stringify({ version: 7, docs: {} })); assert.equal(env.p.info().sideReadonly.identity, true);
  const bad = s1.disk(); bad.set(ID, '{quebrado'); const env2 = await session(bad); assert.equal(JSON.parse(env2.get(ID)).version, 1);
}

// 7) com o app aberto: syncFromDisk trata "sumiu + apareceu igual" como rename (todos os caminhos e lista de caminhos)
for (const mode of ['all', 'paths']) {
  const env = createEnv(s1.disk()); await env.p.load('V');
  const c = env.get('Beta.md'), id = env.docs.get('Beta.md').id;
  await env.adapter.remove('V', 'Beta.md'); await env.adapter.write('V', 'Sub/Beta movida.md', c);
  const n = await env.p.syncFromDisk(mode === 'all' ? undefined : ['Beta.md', 'Sub/Beta movida.md']);
  assert.ok(n >= 1, mode);
  assert.equal(env.docs.get('Sub/Beta movida.md')?.id, id, 'mesmo ID após mover por fora (' + mode + ')');
  assert.equal(env.docs.get('Sub/Beta movida.md').title, 'Beta movida');
  assert.equal(env.docs.list().filter((d) => d.path === 'Beta.md').length, 0);
  await env.p.flush(); assert.equal(env.get('Sub/Beta movida.md'), c, 'conteúdo intacto'); assert.equal(env.has('Beta.md'), false);
}
console.log('identity: ok');
