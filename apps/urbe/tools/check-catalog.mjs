#!/usr/bin/env node
// Todo caminho persistido no vault que o código de src/ escreve/lê está no catálogo de dados (REQ-023, RM-F1-03).
// Procura literais `.urbe/...`, `Personalização/...` e `Páginas/...` em src/ e exige que apareçam em
// docs/v2/discovery/DATA-CATALOG.md (com versão e política) — nenhum formato persistido fora do catálogo.
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = process.env.URBE_ROOT || join(dirname(fileURLToPath(import.meta.url)), '..');
const CATALOG = 'docs/v2/discovery/DATA-CATALOG.md';
const LITERAL = /['"`]((?:\.urbe\/[\w.\-]+(?:\/[\w.\-]*)?)|(?:(?:Personalização|Páginas)\/[\p{L}\p{N}_.\/\-]*))['"`]/gu;
// Literais que são exemplos/prompt/texto (não formatos persistidos)
const IGNORE = [/^Personalização\/$/, /^Páginas\/$/, /Portfólio\.page\.json$/, /arquivo\.css/];

function walk(d) { return readdirSync(join(ROOT, d)).flatMap((e) => (statSync(join(ROOT, d, e)).isDirectory() ? walk(d + '/' + e) : [d + '/' + e])); }

export function found(files = walk('src').filter((f) => f.endsWith('.js'))) {
  const out = new Map();
  for (const f of files) for (const m of readFileSync(join(ROOT, f), 'utf8').matchAll(LITERAL)) { const lit = m[1]; if (IGNORE.some((r) => r.test(lit))) continue; if (!out.has(lit)) out.set(lit, f); }
  return out;
}
/** Normaliza literais ao "prefixo" catalogável: `.urbe/x.json`, `.urbe/origens/`, `Personalização/temas/`... */
const key = (lit) => lit.replace(/\/$/, '');

export function check(catalog = readFileSync(join(ROOT, CATALOG), 'utf8'), lits = found()) {
  const errors = [];
  for (const [lit, file] of lits) {
    const k = key(lit);
    if (!catalog.includes(k)) errors.push(`${file}: "${lit}" não está em ${CATALOG} (todo formato persistido precisa de linha no catálogo)`);
  }
  // política por formato (seção 7): cada .urbe/*.json catalogado aparece na tabela de política
  const policy = catalog.split('## 9.')[1] || '';
  for (const lit of lits.keys()) if (/^\.urbe\/[\w.\-]+\.json$/.test(lit) && !policy.includes(lit)) errors.push(`${lit}: sem linha na tabela de política de migração (DATA-CATALOG §9)`);
  return errors;
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const e = check();
  if (e.length) { console.error(e.map((x) => 'ERRO: ' + x).join('\n')); process.exit(1); }
  console.log(`OK: ${found().size} caminhos persistidos no código, todos catalogados com política`);
}
