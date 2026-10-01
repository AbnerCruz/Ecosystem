// Migração multi-cidade da 1.x (REQ-045, RM-F1-19): cópia idempotente, mapa fundido só pelo WorkspacePersistence depois
// do load, registro em vault.json, 3 boots = mesmo resultado, fusão interrompida refeita sem duplicar, arquivar sem mover dados.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import { createEnv } from './helpers/vault-env.mjs';

const app = fs.readFileSync(new URL('../src/app.js', import.meta.url), 'utf8');
const start = app.indexOf('async function urbeEnsureSingleVault()'), end = app.indexOf('\n}\n', start) + 3;
const oldMap = JSON.stringify({ v: 4, regioes: [{ caminho: 'Bairro', nome: 'Bairro', x: 2, y: 3, w: 10, h: 12 }], notas: { 'Bairro/capitulo.md': { id: 'doc_a1', x: 4, y: 5 } }, construcoes: [{ caminho: 'Bairro', name: 'Casa', x: 8, y: 9, files: [{ relPath: 'Bairro/imagem.png' }] }] });
const store = new Map([
  ['Cidade A', new Map([['Bairro/capitulo.md', 'escrito'], ['Bairro/imagem.png', 'PIXELS'], ['.urbe/mapa.json', oldMap]])],
  ['Cidade B', new Map([['outro.md', 'outro texto']])],
]);
const ctx = { window: {} }; vm.createContext(ctx);
for (const f of ['src/core/artifacts.js', 'src/persistence/multi-city.js']) vm.runInContext(fs.readFileSync(new URL('../' + f, import.meta.url), 'utf8'), ctx);
const FS = { async cidades() { return [...store.keys()]; }, async criarCidade(v) { store.set(v, new Map()); }, async listar(v) { return [...store.get(v).keys()]; }, async ler(v, f) { return store.get(v).get(f) ?? null; }, async escrever(v, f, x) { store.get(v).set(f, x); }, async lerBlob(v, f) { return store.get(v).get(f) ?? null; }, async escreverBlob(v, f, x) { store.get(v).set(f, x); }, async criarPasta(v, p) { store.get(v).set(p + '/.pasta', ''); } };
const m = { FS, dirDe: (p) => p.slice(0, Math.max(0, p.lastIndexOf('/'))), Date, JSON, window: ctx.window }; vm.createContext(m);
vm.runInContext(app.slice(start, end), m);
const urbe = () => store.get('Urbe');

async function boot() {
  await vm.runInContext('urbeEnsureSingleVault()', m);
  const env = createEnv(new Map([...urbe()].filter(([, v]) => typeof v === 'string')));
  await env.p.load('V');
  const r = await env.context.window.UrbeMultiCity.finishing;
  await env.p.flush();
  for (const [k, v] of env.disk()) urbe().set(k, v); // grava de volta no "disco" do vault Urbe
  for (const k of [...urbe().keys()]) if (!env.disk().has(k) && typeof urbe().get(k) === 'string' && !k.endsWith('.png')) urbe().delete(k);
  return { env, r: JSON.parse(JSON.stringify(r)) };
}

// boot 1: copia, não grava mapa antes do load, funde depois pelo WorkspacePersistence
await vm.runInContext('urbeEnsureSingleVault()', m);
assert.equal(urbe().has('.urbe/mapa.json'), false, 'nada de mapa antes do load (escritor único)');
assert.equal(urbe().get('Cidades/Cidade A/Bairro/capitulo.md'), 'escrito');
assert.equal(urbe().get('Cidades/Cidade B/outro.md'), 'outro texto');
assert.equal(urbe().get('.urbe/origens/Cidade A.json'), oldMap, 'cópia do mapa de origem');
const mk = JSON.parse(urbe().get('.urbe/merged-v1.json'));
assert.equal(mk['Cidade A'].pendente, true); assert.equal(mk['Cidade B'].pendente, false, 'sem mapa na origem: nada a fundir');
assert.equal(store.get('Cidade A').get('Bairro/capitulo.md'), 'escrito', 'origem intacta');

const b1 = await boot();
assert.deepEqual(b1.r.merged, ['Cidade A']);
let mapa = JSON.parse(urbe().get('.urbe/mapa.json'));
assert.ok(mapa.regioes.some((r) => r.caminho === 'Cidades/Cidade A/Bairro'));
assert.equal(mapa.notas['Cidades/Cidade A/Bairro/capitulo.md'].id, 'doc_a1', 'ID da origem preservado');
assert.equal(mapa.construcoes[0].files[0].relPath, 'Cidades/Cidade A/Bairro/imagem.png');
let vj = JSON.parse(urbe().get('.urbe/vault.json'));
const mc = vj.migrations.filter((x) => x.id === 'multi-city');
assert.equal(mc.length, 1); assert.deepEqual(mc[0].sources, ['Cidade A']);
assert.equal(JSON.parse(urbe().get('.urbe/merged-v1.json'))['Cidade A'].pendente, undefined, 'pendência baixada depois de gravar');
assert.equal(b1.env.docs.get('Cidades/Cidade A/Bairro/capitulo.md').id, 'doc_a1', 'o documento carregado já tem o ID da origem');

// boots 2 e 3: mesmo resultado, sem recopiar (edição do usuário preservada) e sem duplicar
urbe().set('Cidades/Cidade A/Bairro/capitulo.md', 'editado');
const snap = () => JSON.stringify([...urbe()].filter(([k]) => !k.startsWith('.urbe/backup/')).map(([k, v]) => [k, k === '.urbe/vault.json' ? JSON.parse(v).migrations.length : v]).sort());
const s2 = (await boot(), snap()); const s3 = (await boot(), snap());
assert.equal(s2, s3, '3 boots consecutivos, mesmo resultado');
assert.equal(urbe().get('Cidades/Cidade A/Bairro/capitulo.md'), 'editado', 'não recopia por cima da edição');
mapa = JSON.parse(urbe().get('.urbe/mapa.json'));
assert.equal(mapa.regioes.filter((r) => r.caminho === 'Cidades/Cidade A/Bairro').length, 1, 'região não duplicada');
assert.equal(JSON.parse(urbe().get('.urbe/vault.json')).migrations.filter((x) => x.id === 'multi-city').length, 1);

// marcador apagado: vault.json ainda sabe que a cidade foi migrada ⇒ não recopia
urbe().delete('.urbe/merged-v1.json'); urbe().delete('Cidades/Cidade B/outro.md');
await vm.runInContext('urbeEnsureSingleVault()', m);
assert.equal(urbe().has('Cidades/Cidade A/Bairro/capitulo.md'), true);

// fusão interrompida (mapa já gravado, pendência não baixada): refeita sem duplicar
{
  const M = ctx.window.UrbeMultiCity, t = JSON.parse(JSON.stringify(mapa));
  assert.equal(M.mergeMap(t, oldMap, 'Cidades/Cidade A'), false); assert.deepEqual(t, mapa);
}

// arquivar: só esconde da lista; nada é movido nem apagado
{
  const { env } = await boot();
  const before = new Map(store);
  const list = JSON.parse(JSON.stringify(await env.core.commands.execute('workspace.archiveOldCities')));
  assert.ok(list.includes('Cidade A'));
  assert.deepEqual([...env.context.window.UrbeMultiCity.archived(env.p)].sort(), list.sort());
  assert.deepEqual([...store.keys()], [...before.keys()], 'nenhuma cidade movida ou apagada');
  await env.core.commands.execute('workspace.unarchiveOldCities');
  assert.equal(env.context.window.UrbeMultiCity.archived(env.p).size, 0);
}
console.log('multi-city: ok');
