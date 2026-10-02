// RM-F2-01 (REQ-022): o relatório de hotspots está em dia com as métricas e traz uma decisão por arquivo avaliado.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT } from '../tools/lib/modules.mjs';
import { DOC, BEGIN, END, FOCUS, table, metrics } from '../tools/hotspots.mjs';

const doc = readFileSync(join(ROOT, DOC), 'utf8');
const i = doc.indexOf(BEGIN), j = doc.indexOf(END);
assert.ok(i >= 0 && j > i, 'marcadores do bloco de métricas presentes');
assert.equal(doc.slice(i + BEGIN.length, j).trim(), table().trim(), 'HOTSPOTS.md em dia (rode: node tools/hotspots.mjs --write)');

// os sete hotspots da SPEC §3.1 (REQ-022) são exatamente os avaliados
assert.deepEqual([...FOCUS].sort(), ['src/ai/ui.js', 'src/customize/panel.js', 'src/pages/engine.js', 'src/pages/free.js', 'src/pages/studio.js', 'src/world/life.js', 'src/world/pixel-art.js']);
const decisions = doc.slice(doc.indexOf('## 2.'));
for (const f of FOCUS) {
  const row = decisions.split('\n').find((l) => l.startsWith(`| \`${f}\``));
  assert.ok(row, `${f}: sem linha de decisão na seção 2`);
  assert.match(row.split('|').slice(-2)[0], /\*\*(Manter|Adiar|Extrair)/, `${f}: decisão explícita (manter, adiar ou extrair)`);
}

// as métricas são reproduzíveis e coerentes
const a = metrics(), b = metrics();
assert.deepEqual(a, b, 'métricas determinísticas');
for (const r of a.rows) assert.ok(r.loc > 0 && r.fanOut >= 0 && r.fanIn >= 0, `${r.file}: métricas válidas`);
console.log('OK   hotspots: relatório em dia, uma decisão por arquivo');
