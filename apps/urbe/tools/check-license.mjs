#!/usr/bin/env node
// Coerência de licença e avisos de terceiros (REQ-017, REQ-080, ADR-0003).
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = process.env.URBE_ROOT || join(dirname(fileURLToPath(import.meta.url)), '..');
const rd = (f) => readFileSync(join(ROOT, f), 'utf8');

export function check() {
  const errors = [];
  if (!existsSync(join(ROOT, 'LICENSE'))) errors.push('LICENSE ausente');
  else if (!/Todos os direitos reservados/i.test(rd('LICENSE')) || !/Abner P\. S\. Cruz/.test(rd('LICENSE'))) errors.push('LICENSE deve declarar "Todos os direitos reservados" em nome de Abner P. S. Cruz (ADR-0003)');
  const pkg = JSON.parse(rd('package.json'));
  if (pkg.license !== 'SEE LICENSE IN LICENSE') errors.push(`package.json license deve ser "SEE LICENSE IN LICENSE" (atual: ${pkg.license})`);
  if (!existsSync(join(ROOT, 'THIRD-PARTY-NOTICES.md'))) { errors.push('THIRD-PARTY-NOTICES.md ausente'); return errors; }
  const notices = rd('THIRD-PARTY-NOTICES.md').toLowerCase();
  const need = { 'katex': 'KaTeX', 'jszip': 'JSZip', 'pako': 'pako', 'pdf.js': 'PDF.js', 'electron': 'Electron', 'chokidar': 'chokidar', 'electron-updater': 'electron-updater', 'capacitor': 'Capacitor' };
  for (const [k, name] of Object.entries(need)) if (!notices.includes(k)) errors.push(`THIRD-PARTY-NOTICES.md: falta ${name}`);
  for (const dep of Object.keys({ ...pkg.dependencies })) if (!notices.includes(dep.toLowerCase())) errors.push(`THIRD-PARTY-NOTICES.md: dependência de produção ${dep} não listada`);
  for (const dir of readdirSync(join(ROOT, 'vendor'), { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name)) {
    if (!notices.includes(dir.toLowerCase())) errors.push(`THIRD-PARTY-NOTICES.md: vendor/${dir} não listado`);
    if (!readdirSync(join(ROOT, 'vendor', dir)).some((f) => /^licen[cs]e/i.test(f))) errors.push(`vendor/${dir}: falta o arquivo de licença`);
  }
  return errors;
}
if (import.meta.url === `file://${process.argv[1]}`) {
  const e = check();
  if (e.length) { console.error(e.map((x) => 'ERRO: ' + x).join('\n')); process.exit(1); }
  console.log('OK: LICENSE (todos os direitos reservados), package.json e THIRD-PARTY-NOTICES coerentes');
}
