// Leitor dos documentos normativos da Urbe 2.0 (docs/v2). Somente leitura.
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
export const V2 = join(ROOT, 'docs', 'v2');
export const STATES = ['IMPLEMENTAR', 'ADIADO', 'REJEITADO', 'SUBSTITUÍDO', 'FORA DE ESCOPO'];
export const ITEM_STATES = ['[ ]', '[~]', '[?]', '[x]', '[!]'];
export const GATES = ['G0', 'G1', 'G2', 'G3', 'G4', 'G5', 'G6', 'G7'];
export const FIELDS = ['Estado', 'REQ', 'SPEC', 'Fase', 'Depende', 'Implementação', 'Integração', 'Testes', 'Documentação', 'Aceite', 'Gate'];

export const read = (rel) => readFileSync(join(V2, rel), 'utf8');

/** REQ-nnn → {id, classe, estado, texto} (todas as tabelas de REQUIREMENTS.md). */
export function parseRequirements(md = read('REQUIREMENTS.md')) {
  const reqs = new Map();
  for (const line of md.split('\n')) {
    const m = line.match(/^\|\s*(REQ-\d{3})\s*\|\s*([A-Z]+)\s*\|\s*([^|]+?)\s*\|\s*(.*?)\s*\|(?:\s*(.*?)\s*\|)?\s*$/);
    if (m) reqs.set(m[1], { id: m[1], classe: m[2], estado: m[3], texto: m[4] });
  }
  return reqs;
}

/** REQ-nnn → seção da SPEC (cláusula `- **REQ-nnn** — …`). */
export function parseSpec(md = read('SPEC.md')) {
  const clauses = new Map();
  let h2 = '', h3 = '';
  for (const line of md.split('\n')) {
    let m;
    if ((m = line.match(/^##\s+(\d+)[.\s]/))) { h2 = m[1]; h3 = ''; }
    else if ((m = line.match(/^###\s+(\d+\.\d+)[.\s]/))) { h3 = m[1]; }
    if ((m = line.match(/^- \*\*(REQ-\d{3})\*\* —/)) && !clauses.has(m[1])) clauses.set(m[1], '§' + (h3 || h2));
  }
  return clauses;
}

/** Itens do ROADMAP. */
export function parseRoadmap(md = read('ROADMAP.md')) {
  const items = [];
  let cur = null;
  for (const line of md.split('\n')) {
    let m;
    if ((m = line.match(/^###\s+(RM-F\d-\d{2})\s+—\s+(.+)$/))) { cur = { id: m[1], titulo: m[2], campos: {} }; items.push(cur); continue; }
    if (cur && (m = line.match(/^- \*\*([^:*]+):\*\*\s*(.*)$/))) cur.campos[m[1]] = m[2].trim();
    else if (/^##\s/.test(line)) cur = null;
  }
  for (const it of items) it.reqs = (it.campos.REQ || '').match(/REQ-\d{3}/g) || [];
  return items;
}
