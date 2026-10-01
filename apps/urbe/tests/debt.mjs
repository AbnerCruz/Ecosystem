// Gate de dívida de src/app.js (REQ-012): o estado atual respeita os tetos e nova sobrescrita/camada de versão falha.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { measure, compare } from '../tools/check-debt.mjs';

const ceil = JSON.parse(readFileSync(new URL('../tools/debt-ceiling.json', import.meta.url), 'utf8'));
const src = readFileSync(new URL('../src/app.js', import.meta.url), 'utf8');
const m = measure(src);
assert.deepEqual(compare(m, ceil), [], 'estado atual dentro dos tetos');
assert.equal(m.versionAssignments, 1, 'V21_VERSION atribuída uma única vez');

// nova sobrescrita de função → falha
assert.ok(compare(measure(src + '\nabrirCidade=function(){};\n'), ceil).some((x) => x.startsWith('overrides')), 'nova sobrescrita detectada');
// nova declaração duplicada → falha (ou empata se já houver)
assert.ok(compare(measure(src + '\nfunction buildTree(){}\nfunction zzNovaDup(){}\nfunction zzNovaDup(){}\n'), ceil).some((x) => x.startsWith('duplicates')), 'nova função duplicada detectada');
// variável "Antes/Base" nova → falha
assert.ok(compare(measure(src + '\nvar urbeAlgoAntes=function(){};\n'), ceil).some((x) => x.startsWith('previous')), 'nova variável de camada anterior detectada');
// nova camada de versão → falha
assert.ok(compare(measure(src + "\nV21_VERSION='9.9';\n"), ceil).some((x) => x.startsWith('versionAssignments')), 'nova atribuição de V21_VERSION detectada');
console.log('debt: ok');
