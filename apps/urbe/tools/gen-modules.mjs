#!/usr/bin/env node
// Cria src/modules.json a partir do index.html atual (bootstrap; depois o manifesto é a fonte).
//   node tools/gen-modules.mjs            → escreve src/modules.json (não sobrescreve sem --force)
import { writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT, rd, scan, MANIFEST, walk } from './lib/modules.mjs';

const force = process.argv.includes('--force');
if (existsSync(join(ROOT, MANIFEST)) && !force) { console.error(`${MANIFEST} já existe (use --force para recriar)`); process.exit(1); }

const html = rd('index.html');
const scripts = [...html.matchAll(/<script src="\.\/([^"]+)"><\/script>/g)].map((m) => m[1]);
const styles = [...html.matchAll(/<link rel="stylesheet" href="\.\/([^"]+)">/g)].map((m) => m[1]);
const scans = new Map(scripts.map((f) => [f, scan(f)]));
const providerOfGlobal = new Map(), providerOfService = new Map();
for (const [f, s] of scans) { for (const g of s.globals) providerOfGlobal.set(g, f); for (const v of s.services) providerOfService.set(v, f); }

const idx = (f) => scripts.indexOf(f);
const appIdx = idx('src/app.js'), coreIdx = idx('src/core/core.js');
const LAYER = (f) => {
  if (f.startsWith('vendor/') || f === 'src/legacy/bootstrap.js') return 'vendor';
  if (f === 'src/native/bridge.js') return 'native';
  if (f === 'src/app.js') return 'app';
  if (f.startsWith('src/persistence/')) return 'persistence';
  if (f.startsWith('src/core/') || f.startsWith('src/composition/store') || f.startsWith('src/composition/compiler')) return 'core';
  if (f === 'src/ui/icons.js' || f === 'src/ui/dialogs.js') return 'kit';
  if (f.startsWith('src/ui/')) return 'ui';
  return 'feature';
};
const modules = scripts.map((f) => {
  const s = scans.get(f), i = idx(f);
  const deps = new Set(), hard = new Set(), soft = new Set();
  const refs = [...s.refGlobals.filter((g) => !s.globals.includes(g)).map((g) => providerOfGlobal.get(g)),
                ...s.refServices.filter((v) => !s.services.includes(v)).map((v) => providerOfService.get(v))].filter(Boolean);
  for (const p of refs) { if (p === f) continue; deps.add(p); }
  for (const p of deps) (idx(p) < i ? hard : soft).add(p);
  const name = f.replace(/^src\//, '').replace(/^vendor\//, 'vendor/').replace(/\.js$/, '');
  return {
    name, file: f, layer: LAYER(f),
    phase: f === 'src/app.js' ? 'app' : i > appIdx ? 'late' : i < coreIdx ? 'boot' : LAYER(f) === 'core' || LAYER(f) === 'persistence' ? 'core' : 'feature',
    provides: { globals: s.globals, services: s.services },
    requires: [...hard].sort((a, b) => idx(a) - idx(b)).map((p) => p),
    uses: [...soft].sort((a, b) => idx(a) - idx(b)),
    platforms: ['web', 'electron', 'android'],
  };
});
const inShell = new Set([...scripts, ...styles]);
const assets = ['src/world/chunk-worker.js', 'manifest.webmanifest', 'icon-192.png', 'icon-512.png', 'icon-maskable-512.png', 'apple-touch-icon.png',
  ...walk('vendor/katex/fonts').filter((f) => f.endsWith('.woff2')).sort()].filter((f) => !inShell.has(f));
const manifest = { version: 1, note: 'Fonte única dos módulos (ADR-0001). index.html (blocos urbe:styles/urbe:scripts) e sw.js (urbe:shell) são derivados por tools/check-modules.mjs --write.', styles, assets, modules };
writeFileSync(join(ROOT, MANIFEST), JSON.stringify(manifest, null, 2) + '\n');
console.log(`${MANIFEST}: ${modules.length} módulos, ${styles.length} estilos, ${assets.length} assets`);
