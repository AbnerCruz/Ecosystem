// IDs estáveis no mapa (REQ-041, RM-F1-14): geração determinística para mapas 1.x, preservação dos existentes,
// campos lidos pela 1.8.2 intactos.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const ctx = { window: {} }; vm.createContext(ctx);
vm.runInContext(fs.readFileSync(new URL('../src/world/stable-ids.js', import.meta.url), 'utf8'), ctx);
const S = ctx.window.UrbeStableIds;
const fixture = () => JSON.parse(fs.readFileSync(new URL('./fixtures/vaults/v1-mapa-v4/.urbe/mapa.json', import.meta.url), 'utf8'));

// determinístico: o mesmo mapa 1.x gera os mesmos IDs em toda abertura
const a = fixture(), b = fixture();
S.assign(a); S.assign(b);
assert.match(a.regioes[0].id, /^reg_[0-9a-z]+$/); assert.match(a.construcoes[0].id, /^ast_[0-9a-z]+$/);
assert.equal(a.regioes[0].id, b.regioes[0].id); assert.equal(a.construcoes[0].id, b.construcoes[0].id);

// preserva IDs existentes e não remove nada que a 1.8.2 lê
const original = fixture(), m = fixture();
m.regioes[0].id = 'reg_meu'; m.construcoes[0].id = 'ast_meu';
const out = S.assign(m);
assert.equal(m.regioes[0].id, 'reg_meu'); assert.equal(m.construcoes[0].id, 'ast_meu');
assert.equal(out.regions.get('Pasta'), 'reg_meu');
for (const k of Object.keys(original.regioes[0])) assert.deepEqual(m.regioes[0][k], original.regioes[0][k], 'regioes[].' + k + ' preservado');
for (const k of Object.keys(original.construcoes[0])) assert.deepEqual(m.construcoes[0][k], original.construcoes[0][k], 'construcoes[].' + k + ' preservado (inclui parentNoteName)');

// idempotente e sem colisão: duas pastas de mesmo caminho (mapa corrompido) e ID duplicado recebem IDs distintos
const d = { regioes: [{ caminho: 'X' }, { caminho: 'X' }, { caminho: 'Y', id: 'reg_dup' }, { caminho: 'Z', id: 'reg_dup' }], construcoes: [{ name: 'f', caminho: '' }, { name: 'f', caminho: '' }] };
S.assign(d);
const ids = [...d.regioes, ...d.construcoes].map((x) => x.id);
assert.equal(new Set(ids).size, ids.length, 'IDs únicos: ' + ids.join(','));
assert.equal(d.regioes[2].id, 'reg_dup', 'o primeiro dono de um ID repetido o mantém');
const again = JSON.parse(JSON.stringify(d)); S.assign(again);
assert.deepEqual(again.regioes.map((r) => r.id), d.regioes.map((r) => r.id), 'segunda passada não muda nada');

// entradas estranhas não quebram; IDs com prefixo errado são trocados
assert.equal(S.assign(null).regions.size, 0);
const w = { regioes: [null, 3, { caminho: 'A', id: 'ast_errado' }], construcoes: 'x' }; S.assign(w);
assert.match(w.regioes[2].id, /^reg_/);

// ensure: ID novo aleatório só quando falta
const o = {}; const id1 = S.ensure(o, 'reg'); assert.match(id1, /^reg_/); assert.equal(S.ensure(o, 'reg'), id1);
assert.notEqual(S.ensure({}, 'ast'), S.ensure({}, 'ast'));
assert.equal(S.regionFor('Pasta'), a.regioes[0].id, 'pasta sem geometria no mapa recebe o mesmo ID determinístico');
console.log('stable-ids: ok');
