#!/usr/bin/env node
// Verifica a cobertura REQ → SPEC → ROADMAP → TRACEABILITY da Urbe 2.0 (REQ-002, REQ-024, REQ-068).
// Somente leitura de docs/v2; não toca o runtime.
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT, V2, STATES, ITEM_STATES, GATES, FIELDS, read, parseRequirements, parseSpec, parseRoadmap } from './lib/v2-docs.mjs';

export function check(src = {}) {
  const errors = [];
  const reqs = parseRequirements(src.requirements);
  const spec = parseSpec(src.spec);
  const items = parseRoadmap(src.roadmap);
  const trace = src.trace ?? (existsSync(join(V2, 'TRACEABILITY.md')) ? read('TRACEABILITY.md') : '');
  const ids = new Set(items.map((i) => i.id));
  if (ids.size !== items.length) errors.push('ROADMAP: IDs de item duplicados');

  for (const r of reqs.values()) {
    if (!STATES.includes(r.estado)) errors.push(`${r.id}: estado inválido "${r.estado}"`);
  }
  for (let n = 1; n <= Math.max(...[...reqs.keys()].map((k) => +k.slice(4))); n++) {
    const id = 'REQ-' + String(n).padStart(3, '0');
    if (!reqs.has(id)) errors.push(`${id}: ausente do ledger (requisitos não podem ser removidos)`);
  }
  for (const r of reqs.values()) {
    if (r.estado === 'IMPLEMENTAR') {
      if (!spec.has(r.id)) errors.push(`${r.id}: sem cláusula na SPEC`);
      const its = items.filter((i) => i.reqs.includes(r.id));
      if (!its.length) errors.push(`${r.id}: sem item no ROADMAP`);
      if (!trace.includes(r.id)) errors.push(`${r.id}: ausente de TRACEABILITY.md`);
    } else if (!/(ADIADO|REJEITADO|SUBSTITUÍDO|FORA DE ESCOPO)/.test(r.estado) || !r.texto.match(/Justificativa/)) {
      errors.push(`${r.id}: estado ${r.estado} exige justificativa`);
    }
  }
  for (const it of items) {
    for (const f of FIELDS) if (!it.campos[f]) errors.push(`${it.id}: campo "${f}" vazio`);
    if (!ITEM_STATES.includes(it.campos.Estado)) errors.push(`${it.id}: estado inválido "${it.campos.Estado}"`);
    if (!GATES.includes(it.campos.Gate)) errors.push(`${it.id}: gate inválido`);
    for (const q of it.reqs) if (!reqs.has(q)) errors.push(`${it.id}: ${q} não existe no ledger`);
    for (const d of (it.campos.Depende.match(/RM-F\d-\d{2}/g) || [])) if (!ids.has(d)) errors.push(`${it.id}: dependência ${d} inexistente`);
  }
  if (!src.skipFiles) for (const f of ['AGENTS.md', 'AGENTSCHAT.md']) if (!existsSync(join(ROOT, f))) errors.push(`${f} ausente na raiz (REQ-088)`);
  return { errors, reqs, spec, items };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const { errors, reqs, items } = check();
  const impl = [...reqs.values()].filter((r) => r.estado === 'IMPLEMENTAR').length;
  if (errors.length) { console.error(errors.map((e) => 'ERRO: ' + e).join('\n')); process.exit(1); }
  console.log(`OK: ${reqs.size} REQ (${impl} IMPLEMENTAR) · ${items.length} itens de ROADMAP · cobertura completa`);
}
