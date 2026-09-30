#!/usr/bin/env node
// Gera docs/v2/TRACEABILITY.md a partir de REQUIREMENTS + SPEC + ROADMAP. `--check` compara sem escrever.
import { writeFileSync, existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { V2, parseRequirements, parseSpec, parseRoadmap } from './lib/v2-docs.mjs';

export function render() {
  const reqs = parseRequirements(), spec = parseSpec(), items = parseRoadmap();
  const cell = (s) => String(s).replace(/\|/g, '\\|');
  const out = [
    '# Rastreabilidade — Urbe 2.0',
    '',
    '> **Gerado por `tools/gen-traceability.mjs`** a partir de `REQUIREMENTS.md`, `SPEC.md` e `ROADMAP.md`. Não editar à mão: altere a fonte e regenere.',
    '> Verificação: `node tools/check-traceability.mjs` e `node tools/gen-traceability.mjs --check`.',
    '',
    '## Matriz REQ → SPEC → fase → item do ROADMAP → teste/gate',
    '',
    '| REQ | SPEC | Fase | Item | Estado do item | Teste (resumo) | Gate |',
    '|---|---|---|---|---|---|---|',
  ];
  for (const r of reqs.values()) {
    if (r.estado !== 'IMPLEMENTAR') continue;
    const its = items.filter((i) => i.reqs.includes(r.id));
    if (!its.length) out.push(`| ${r.id} | ${spec.get(r.id) || '—'} | — | **SEM ITEM** | — | — | — |`);
    for (const i of its) out.push(`| ${r.id} | ${spec.get(r.id) || '—'} | ${i.campos.Fase} | ${i.id} — ${cell(i.titulo)} | ${i.campos.Estado} | ${cell(i.campos.Testes)} | ${i.campos.Gate} |`);
  }
  out.push('', '## Requisitos que não são IMPLEMENTAR', '', '| REQ | Estado | Onde está a justificativa |', '|---|---|---|');
  for (const r of reqs.values()) if (r.estado !== 'IMPLEMENTAR') out.push(`| ${r.id} | ${r.estado} | REQUIREMENTS.md; SPEC §13 |`);
  const total = [...reqs.values()].filter((r) => r.estado === 'IMPLEMENTAR').length;
  const covered = [...reqs.values()].filter((r) => r.estado === 'IMPLEMENTAR' && items.some((i) => i.reqs.includes(r.id))).length;
  const done = items.filter((i) => i.campos.Estado === '[x]').length;
  out.push('', '## Cobertura', '', `- REQ IMPLEMENTAR: ${total}; com item no ROADMAP: ${covered}.`, `- Itens no ROADMAP: ${items.length}; concluídos \`[x]\`: ${done}.`, '');
  return out.join('\n');
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const text = render();
  const file = join(V2, 'TRACEABILITY.md');
  if (process.argv.includes('--check')) {
    const cur = existsSync(file) ? readFileSync(file, 'utf8') : '';
    if (cur !== text) { console.error('ERRO: TRACEABILITY.md desatualizado; rode `node tools/gen-traceability.mjs`'); process.exit(1); }
    console.log('OK: TRACEABILITY.md em dia');
  } else { writeFileSync(file, text); console.log('TRACEABILITY.md gerado'); }
}
