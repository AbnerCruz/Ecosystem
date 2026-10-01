#!/usr/bin/env node
// Valida src/modules.json contra o código e contra index.html/sw.js/build-www (REQ-025, REQ-015).
//   node tools/check-modules.mjs          → valida
//   node tools/check-modules.mjs --write  → regenera os blocos derivados de index.html e sw.js
import { writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT, rd, scan, loadManifest, walk, derive, between, replaceBetween, BEGIN, END, PHASES, LAYERS, boundaryViolations, boundaryExceptions } from './lib/modules.mjs';

export function check({ manifest = loadManifest(), html = rd('index.html'), sw = rd('sw.js'), exceptions = boundaryExceptions() } = {}) {
  const errors = [];
  const err = (m) => errors.push(m);
  const mods = manifest.modules || [];
  const files = mods.map((m) => m.file);
  const idx = new Map(files.map((f, i) => [f, i]));
  if (manifest.version !== 1) err('modules.json: version deve ser 1');
  if (idx.size !== files.length) err('modules.json: arquivos duplicados');
  const names = new Set();
  for (const m of mods) {
    if (names.has(m.name)) err(`${m.name}: nome duplicado`);
    names.add(m.name);
    if (!PHASES.includes(m.phase)) err(`${m.file}: fase inválida "${m.phase}"`);
    if (!(m.layer in LAYERS)) err(`${m.file}: camada inválida "${m.layer}"`);
  }

  // 1. arquivos: existem, e todo .js/.css de src/ está no manifesto (worker em assets)
  const known = new Set([...files, ...manifest.styles, ...manifest.assets]);
  for (const f of known) if (!existsSync(join(ROOT, f))) err(`${f}: arquivo do manifesto não existe`);
  for (const f of walk('src').filter((x) => /\.(js|css)$/.test(x))) if (!known.has(f) && f !== 'src/modules.json') err(`${f}: arquivo de src/ fora do manifesto`);

  // 2. provides declarados == provides observados; referências declaradas
  const providerG = new Map(), providerS = new Map();
  const scans = new Map();
  for (const m of mods) {
    let s; try { s = scan(m.file); } catch { continue; }
    scans.set(m.file, s);
    for (const g of s.globals) if (!m.provides.globals.includes(g)) err(`${m.file}: publica ${g} sem declarar em provides.globals`);
    for (const v of s.services) if (!m.provides.services.includes(v)) err(`${m.file}: provê serviço "${v}" sem declarar`);
    for (const g of m.provides.globals) if (!s.globals.includes(g)) err(`${m.file}: declara provides.globals ${g}, mas não o publica`);
    for (const v of m.provides.services) if (!s.services.includes(v)) err(`${m.file}: declara serviço "${v}", mas não o provê`);
    for (const g of m.provides.globals) providerG.set(g, m.file);
    for (const v of m.provides.services) providerS.set(v, m.file);
  }
  for (const m of mods) {
    const s = scans.get(m.file); if (!s) continue;
    const declared = new Set([...(m.requires || []), ...(m.uses || [])]);
    const refs = [...s.refGlobals.filter((g) => !s.globals.includes(g)).map((g) => [g, providerG.get(g)]),
                  ...s.refServices.filter((v) => !s.services.includes(v)).map((v) => [`serviço "${v}"`, providerS.get(v)])];
    for (const [what, p] of refs) if (p && p !== m.file && !declared.has(p)) err(`${m.file}: usa ${what} de ${p} sem declarar em requires/uses`);
    for (const r of m.requires || []) {
      if (!idx.has(r)) err(`${m.file}: requires "${r}" fora do manifesto`);
      else if (idx.get(r) >= idx.get(m.file)) err(`${m.file}: requires ${r}, que só carrega depois (ordem inválida ou ciclo)`);
    }
    for (const u of m.uses || []) if (!idx.has(u)) err(`${m.file}: uses "${u}" fora do manifesto`);
  }

  // 3. boundaries de camada (REQ-015): só as exceções registradas, e nenhuma exceção obsoleta
  const viol = boundaryViolations(manifest);
  const key = (x) => x.from + ' -> ' + x.to;
  const allowed = new Set(exceptions.map(key));
  for (const v of viol) if (!allowed.has(key(v))) err(`boundary: ${v.from} → ${v.to} (${v.why}) não está em BOUNDARY-EXCEPTIONS.md`);
  const seen = new Set(viol.map(key));
  for (const e of exceptions) if (!seen.has(key(e))) err(`BOUNDARY-EXCEPTIONS.md: ${key(e)} não é mais violação — remova da lista`);

  // 4. derivados: index.html e sw.js iguais ao que o manifesto produz
  const d = derive(manifest);
  const cmp = (text, b, e, expected, label) => {
    const x = between(text, b, e);
    if (!x) return err(`${label}: marcadores ${b} … ${e} ausentes`);
    if (x.body.trim() !== expected.trim()) err(`${label}: diverge do manifesto (rode: node tools/check-modules.mjs --write)`);
  };
  cmp(html, BEGIN.html_styles, END.html_styles, d.styles, 'index.html (estilos)');
  cmp(html, BEGIN.html_scripts, END.html_scripts, d.scripts, 'index.html (scripts)');
  cmp(sw, BEGIN.sw, END.sw, d.swLines, 'sw.js (APP_SHELL)');
  return errors;
}

if (import.meta.url === `file://${process.argv[1]}`) {
  if (process.argv.includes('--write')) {
    const m = loadManifest(), d = derive(m);
    let h = replaceBetween(rd('index.html'), BEGIN.html_styles, END.html_styles, d.styles);
    h = replaceBetween(h, BEGIN.html_scripts, END.html_scripts, d.scripts);
    writeFileSync(join(ROOT, 'index.html'), h);
    writeFileSync(join(ROOT, 'sw.js'), replaceBetween(rd('sw.js'), BEGIN.sw, END.sw, d.swLines));
    console.log('index.html e sw.js regenerados a partir de src/modules.json');
  }
  const errors = check();
  if (errors.length) { console.error(errors.map((e) => 'ERRO: ' + e).join('\n')); process.exit(1); }
  const m = loadManifest();
  console.log(`OK: ${m.modules.length} módulos, ${m.styles.length} estilos, ${m.assets.length} assets; index.html e sw.js derivados do manifesto`);
}
