#!/usr/bin/env node
// Métricas de hotspots por módulo (RM-F2-01, REQ-022): tamanho, acoplamento e superfície, lidos de src/modules.json e dos fontes.
//   node tools/hotspots.mjs            imprime a tabela (Markdown)
//   node tools/hotspots.mjs --write    regenera o bloco entre os marcadores de docs/v2/discovery/HOTSPOTS.md
//   node tools/hotspots.mjs --check    falha se o bloco do documento divergir das métricas atuais
// Só mede; não decide. As decisões por arquivo são escritas à mão no HOTSPOTS.md (nenhuma extração sem decisão registrada).
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT, loadManifest, rd, scan, boundaryViolations, boundaryExceptions } from './lib/modules.mjs';

export const DOC = 'docs/v2/discovery/HOTSPOTS.md';
export const BEGIN = '<!-- hotspots:begin -->';
export const END = '<!-- hotspots:end -->';
/** Arquivos avaliados pelo item (RM-F2-01). */
export const FOCUS = ['src/pages/engine.js', 'src/pages/studio.js', 'src/pages/free.js', 'src/world/life.js', 'src/ai/ui.js', 'src/customize/panel.js', 'src/world/pixel-art.js'];
const REFERENCE = []; // app.js fica de fora: muda a cada item da F2 e tem metas próprias (check-debt)

const count = (re, s) => (s.match(re) || []).length;

export function metrics() {
  const m = loadManifest();
  const byFile = new Map(m.modules.map((x) => [x.file, x]));
  const exceptions = boundaryExceptions();
  const violations = boundaryViolations(m);
  const fanIn = new Map(m.modules.map((x) => [x.file, new Set()]));
  for (const x of m.modules) for (const d of [...(x.requires || []), ...(x.uses || [])]) fanIn.get(d)?.add(x.file);
  // Quem usa os globais/serviços que o módulo provê (por referência textual nos outros scripts).
  const scans = new Map(m.modules.map((x) => [x.file, scan(x.file)]));
  const rows = [];
  for (const file of [...FOCUS, ...REFERENCE]) {
    const x = byFile.get(file); if (!x) throw new Error(`módulo fora do manifesto: ${file}`);
    const src = rd(file), own = scans.get(file);
    const refBy = new Set();
    for (const [f, s] of scans) if (f !== file && (own.globals.some((g) => s.refGlobals.includes(g)) || own.services.some((v) => s.refServices.includes(v)))) refBy.add(f);
    const lines = src.split('\n');
    rows.push({
      file, layer: x.layer,
      loc: lines.length, bytes: Buffer.byteLength(src),
      fanOut: new Set([...(x.requires || []), ...(x.uses || [])]).size,
      fanIn: new Set([...fanIn.get(file), ...refBy]).size,
      functions: count(/\bfunction\s+[\w$]+\s*\(/g, src) + count(/\b(?:const|let|var)\s+[\w$]+\s*=\s*(?:async\s*)?(?:\([^)]*\)|[\w$]+)\s*=>/g, src),
      provides: own.globals.length + own.services.length,
      domAccess: count(/\bdocument\.|\.querySelector|\.innerHTML|\.addEventListener/g, src),
      storageAccess: count(/\blocalStorage\b|\bindexedDB\b|\bFS\.|\bDisco\./g, src),
      boundaryExceptions: exceptions.filter((e) => e.from === file).length,
      violations: violations.filter((v) => v.from === file).length,
    });
  }
  return { rows, modules: m.modules.length };
}

export function table() {
  const { rows, modules } = metrics();
  const head = '| Arquivo | Camada | Linhas | Funções | Fan-out | Fan-in | Provê | DOM | Armaz. | Exceções de boundary |\n|---|---|---:|---:|---:|---:|---:|---:|---:|---:|';
  const body = rows.map((r) => `| \`${r.file}\` | ${r.layer} | ${r.loc} | ${r.functions} | ${r.fanOut} | ${r.fanIn} | ${r.provides} | ${r.domAccess} | ${r.storageAccess} | ${r.boundaryExceptions} |`).join('\n');
  return `${head}\n${body}\n\n_Base: ${modules} módulos em \`src/modules.json\`; reproduza com \`node tools/hotspots.mjs\`._`;
}

function block(text) { return `${BEGIN}\n${text}\n${END}`; }

function run(argv) {
  const t = table();
  if (!argv.includes('--write') && !argv.includes('--check')) { console.log(t); return 0; }
  const path = join(ROOT, DOC);
  let doc; try { doc = readFileSync(path, 'utf8'); } catch { console.error(`ERRO: ${DOC} ausente`); return 1; }
  const i = doc.indexOf(BEGIN), j = doc.indexOf(END);
  if (i < 0 || j < i) { console.error(`ERRO: marcadores ${BEGIN} … ${END} ausentes em ${DOC}`); return 1; }
  const next = doc.slice(0, i) + block(t) + doc.slice(j + END.length);
  if (argv.includes('--check')) {
    if (next !== doc) { console.error(`ERRO: ${DOC} diverge das métricas atuais (rode: node tools/hotspots.mjs --write)`); return 1; }
    console.log(`OK: ${DOC} em dia com as métricas`); return 0;
  }
  writeFileSync(path, next); console.log(`${DOC} atualizado`); return 0;
}

if (import.meta.url === `file://${process.argv[1]}`) process.exit(run(process.argv.slice(2)));
