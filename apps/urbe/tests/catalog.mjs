// O catálogo de dados cobre todo caminho persistido pelo código (REQ-023, RM-F1-03).
import assert from 'node:assert/strict';
import { check, found } from '../tools/check-catalog.mjs';

assert.deepEqual(check(), [], 'código × catálogo');
const lits = found();
for (const p of ['.urbe/mapa.json', '.urbe/journal.json', '.urbe/history.json', '.urbe/trash.json', '.urbe/compositions.json', '.urbe/tutorial.json']) assert.ok(lits.has(p), 'literal esperado no código: ' + p);
// novo formato sem linha no catálogo → falha
const novo = new Map([...lits, ['.urbe/novo-formato.json', 'src/x.js']]);
assert.ok(check(undefined, novo).some((e) => /novo-formato\.json/.test(e)), 'formato fora do catálogo deve falhar');
// formato catalogado, mas sem política → falha
const semPolitica = 'x .urbe/mapa.json .urbe/journal.json .urbe/history.json .urbe/trash.json .urbe/compositions.json .urbe/tutorial.json .urbe/merged-v1.json .urbe/origens Personalização/texturas Personalização/temas Personalização/estilos Personalização/plugins Personalização/tema.json Páginas/Modelos Páginas/Blocos Páginas';
assert.ok(check(semPolitica, lits).some((e) => /sem linha na tabela de política/.test(e)), 'sem política deve falhar');
console.log('catalog: ok');
