#!/usr/bin/env node
// Gate de dívida (REQ-012): mede as camadas históricas de src/app.js e falha se qualquer métrica subir
// acima do teto registrado em tools/debt-ceiling.json. Tetos só descem (`--ratchet`).
import { readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = process.env.URBE_ROOT || join(dirname(fileURLToPath(import.meta.url)), '..');
const CEIL = join(ROOT, 'tools', 'debt-ceiling.json');

export function measure(src = readFileSync(join(ROOT, 'src/app.js'), 'utf8')) {
  const overrides = (src.match(/^[A-Za-z_$][\w$]*\s*=\s*function\b/gm) || []).length;
  const declared = [...src.matchAll(/^function\s+([A-Za-z_$][\w$]*)/gm)].map((m) => m[1]);
  const seen = new Map();
  for (const n of declared) seen.set(n, (seen.get(n) || 0) + 1);
  const duplicates = [...seen.values()].filter((c) => c > 1).length;
  const previous = (src.match(/^(?:var|let|const)\s+[A-Za-z_$][\w$]*(?:Antes|Base|Old|Legacy|Prev|Previous)[\w$]*/gm) || []).length;
  const versionAssignments = (src.match(/\bV21_VERSION\s*=[^=]/g) || []).length;
  const lines = src.split('\n').length;
  return { overrides, duplicates, previous, versionAssignments, lines };
}

export function compare(m, ceil) {
  return Object.keys(ceil).filter((k) => m[k] > ceil[k]).map((k) => `${k}: ${m[k]} > teto ${ceil[k]}`);
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const m = measure();
  let ceil; try { ceil = JSON.parse(readFileSync(CEIL, 'utf8')); } catch { ceil = null; }
  if (!ceil || process.argv.includes('--init')) { writeFileSync(CEIL, JSON.stringify(m, null, 2) + '\n'); console.log('tetos iniciais gravados', m); process.exit(0); }
  if (process.argv.includes('--ratchet')) {
    const next = Object.fromEntries(Object.keys(ceil).map((k) => [k, Math.min(ceil[k], m[k])]));
    writeFileSync(CEIL, JSON.stringify(next, null, 2) + '\n'); console.log('tetos reduzidos', next); process.exit(0);
  }
  const bad = compare(m, ceil);
  if (bad.length) { console.error('ERRO: dívida de src/app.js aumentou (REQ-012):\n' + bad.map((x) => '  ' + x).join('\n') + '\nNão crie camadas de versão/sobrescritas; extraia e apague (AGENTS.md §3).'); process.exit(1); }
  const shrunk = Object.keys(ceil).filter((k) => m[k] < ceil[k]);
  console.log('OK dívida:', JSON.stringify(m), shrunk.length ? `— reduzida em ${shrunk.join(', ')} (rode --ratchet para fixar o novo teto)` : '');
}
