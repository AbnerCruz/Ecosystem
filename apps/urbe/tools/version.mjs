#!/usr/bin/env node
// Fonte única da versão do produto: package.json (REQ-019, REQ-065).
//   node tools/version.mjs check   → falha se qualquer superfície divergir
//   node tools/version.mjs sync    → reescreve as superfícies derivadas a partir do package.json
import { readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = process.env.URBE_ROOT || join(dirname(fileURLToPath(import.meta.url)), '..');
const rd = (f) => readFileSync(join(ROOT, f), 'utf8');
const wr = (f, s) => writeFileSync(join(ROOT, f), s);
const version = JSON.parse(rd('package.json')).version;
const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

// [arquivo, regex com 1 grupo de captura da versão, substituição a partir da versão]
const surfaces = [
  ['src/core/core.js', /version:\s*'([^']+)'/, (v) => `version: '${v}'`],
  ['index.html', /<title>Urbe v([^<]+)<\/title>/, (v) => `<title>Urbe v${v}</title>`],
  ['sw.js', /urbe-shell-v([^']+)'/, (v) => `urbe-shell-v${v}'`],
];

function problems() {
  const out = [];
  for (const [f, re] of surfaces) {
    const m = rd(f).match(re);
    if (!m) out.push(`${f}: padrão de versão não encontrado`);
    else if (m[1] !== version) out.push(`${f}: ${m[1]} ≠ ${version}`);
  }
  const lock = JSON.parse(rd('package-lock.json'));
  if (lock.version !== version) out.push(`package-lock.json: ${lock.version} ≠ ${version}`);
  if (lock.packages?.['']?.version !== version) out.push(`package-lock.json packages[""]: ${lock.packages?.['']?.version} ≠ ${version}`);
  if (!rd('CHANGELOG.md').includes(`## v${version}`)) out.push(`CHANGELOG.md: sem cabeçalho "## v${version}"`);
  const readable = version.replace('-', ' ');
  if (!rd('README.md').includes(`**Versão ${readable}.**`)) out.push(`README.md: sem "**Versão ${readable}.**"`);
  const app = rd('src/app.js');
  const assigns = app.match(/\bV21_VERSION\s*=[^=]/g) || [];
  if (assigns.length !== 1 || !/var V21_VERSION=window\.UrbeCore\.version;/.test(app)) out.push(`src/app.js: V21_VERSION deve ser atribuída uma única vez a partir de UrbeCore.version (${assigns.length} atribuições)`);
  return out;
}

const cmd = process.argv[2];
if (cmd === 'sync') {
  for (const [f, re, rep] of surfaces) wr(f, rd(f).replace(re, rep(version)));
  const lock = JSON.parse(rd('package-lock.json'));
  lock.version = version;
  if (lock.packages?.['']) lock.packages[''].version = version;
  wr('package-lock.json', JSON.stringify(lock, null, 2) + '\n');
  const readme = rd('README.md').replace(/\*\*Versão [^*]+?\.\*\*/, `**Versão ${version.replace('-', ' ')}.**`);
  wr('README.md', readme);
  console.log(`versão ${version} sincronizada`);
}
const p = problems();
if (p.length) { console.error(p.map((x) => 'ERRO: ' + x).join('\n')); process.exit(1); }
if (cmd !== 'sync') console.log(`OK: versão ${version} coerente em package-lock, core, index, sw, CHANGELOG, README e app.js`);
