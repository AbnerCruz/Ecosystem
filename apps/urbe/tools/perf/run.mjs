#!/usr/bin/env node
// Harness de performance (REQ-070): mede os cenários de docs/v2/discovery/PERFORMANCE.md §4 em Chromium real.
//   node tools/perf/run.mjs [--vault S|M|L] [--runs N] [--cpu 1|4|6] [--scenarios open,edit,save,search,city,tutorial] [--out arquivo.json]
// Não altera o app: só o observa (eventos do core, rAF, IndexedDB via hook injetado pelo harness).
import { writeFileSync } from 'node:fs';
import { execSync } from 'node:child_process';
import { makeVault } from './make-vault.mjs';
import { loadPlaywright } from '../lib/playwright.mjs';
import { startServer } from '../lib/static-server.mjs';

const arg = (k, d) => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : d; };
const VAULT = arg('vault', 'S'), RUNS = +arg('runs', 3), CPU = +arg('cpu', 1), OUT = arg('out', null);
const SCEN = arg('scenarios', 'open,edit,save,search,city,tutorial').split(',');
const median = (a) => { const s = [...a].sort((x, y) => x - y); return s.length ? s[Math.floor(s.length / 2)] : null; };
const p95 = (a) => { const s = [...a].sort((x, y) => x - y); return s.length ? s[Math.min(s.length - 1, Math.ceil(s.length * 0.95) - 1)] : null; };
const stat = (a) => ({ runs: a.length, median: median(a), p95: p95(a), min: Math.min(...a), max: Math.max(...a) });
const round = (x) => Math.round(x * 100) / 100;

const { chromium } = await loadPlaywright();
const srv = await startServer();
const browser = await chromium.launch({ args: ['--enable-precise-memory-info'] });
const results = {};

async function newPage({ seed }) {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  if (CPU > 1) await cdp.send('Emulation.setCPUThrottlingRate', { rate: CPU });
  await page.addInitScript(() => { // contadores de gravação no IndexedDB (só do harness)
    window.__idb = { puts: 0, bytes: 0, reset() { this.puts = 0; this.bytes = 0; } };
    const put = IDBObjectStore.prototype.put;
    IDBObjectStore.prototype.put = function (v, k) { window.__idb.puts++; window.__idb.bytes += typeof v === 'string' ? v.length : (v && v.size) || 0; return put.call(this, v, k); };
  });
  await page.goto(srv.url + '/manifest.webmanifest');
  if (seed) {
    const files = [...seed.files].map(([p, c]) => [p, typeof c === 'string' ? c : Array.from(c)]);
    await page.evaluate(async (files) => {
      const db = await new Promise((ok, err) => { const r = indexedDB.open('knowledge-city', 3); r.onupgradeneeded = (e) => { const d = e.target.result; for (const s of ['kv', 'fs', 'blobs']) if (!d.objectStoreNames.contains(s)) d.createObjectStore(s); }; r.onsuccess = () => ok(r.result); r.onerror = () => err(r.error); });
      await new Promise((ok, err) => { const t = db.transaction(['fs', 'kv'], 'readwrite'); const fs = t.objectStore('fs'); for (const [p, c] of files) fs.put(typeof c === 'string' ? c : new Blob([new Uint8Array(c)]), 'Urbe/' + p); t.objectStore('kv').put('Urbe', 'ultimaCidade'); t.oncomplete = ok; t.onerror = () => err(t.error); });
      db.close();
    }, files);
  }
  return { ctx, page, cdp };
}
async function open(p, expectDocs) {
  const t0 = Date.now();
  await p.page.goto(srv.url + '/index.html');
  await p.page.waitForFunction((n) => window.UrbeCore && UrbeCore.state.select('ready') && UrbeCore.service('documents').list().length >= n, expectDocs, { timeout: 180000 });
  return Date.now() - t0;
}
const heapMB = (p) => p.page.evaluate(() => performance.memory ? performance.memory.usedJSHeapSize / 1048576 : null);

const seed = makeVault(VAULT);
const info = { vault: VAULT, notes: seed.notes, assets: seed.assets, kb: Math.round(seed.bytes / 1024), sha256: seed.hash.slice(0, 16) };

if (SCEN.includes('open')) {
  const t = [], heap = [];
  for (let i = 0; i < RUNS; i++) { const p = await newPage({ seed }); t.push(await open(p, seed.notes)); heap.push(await heapMB(p)); await p.ctx.close(); }
  results.open_ms = stat(t); results.heap_after_open_mb = stat(heap.map(round));
}
if (SCEN.some((s) => ['edit', 'save', 'search', 'city'].includes(s))) {
  const p = await newPage({ seed }); await open(p, seed.notes); await p.page.waitForTimeout(1500);
  if (SCEN.includes('save')) {
    const ms = [], bytes = [], puts = [];
    for (let i = -1; i < RUNS; i++) { // i = -1: aquecimento (a 1ª gravação também materializa mapa/histórico)
      const r = await p.page.evaluate(async (i) => {
        const docs = UrbeCore.service('documents'), d = docs.list().filter((x) => x.path.endsWith('.md'))[i + 4];
        window.__idb.reset();
        const done = new Promise((ok) => { const off = UrbeCore.events.on('workspace:saved', () => { off(); ok(performance.now()); }); });
        const t0 = performance.now();
        UrbeCore.commands.execute('document.update', { id: d.id, path: d.path, content: d.content + ' editado' + i });
        const t1 = await done; return { ms: t1 - t0, bytes: window.__idb.bytes, puts: window.__idb.puts };
      }, i);
      if (i >= 0) { ms.push(round(r.ms)); bytes.push(r.bytes); puts.push(r.puts); }
      await p.page.waitForTimeout(300);
    }
    results.save_after_edit_ms = stat(ms); results.save_bytes_written = stat(bytes); results.save_idb_puts = stat(puts);
  }
  if (SCEN.includes('search')) {
    const lat = await p.page.evaluate(() => {
      const docs = UrbeCore.service('documents'); UrbeCore.commands.execute('ui.quickOpen.open');
      const input = document.querySelector('.uqo-input'); const out = [];
      for (const q of ['cidade', 'projeto rio', 'Nota 00042', 'memória conexão', 'lua estrela', 'zzzz', 'ponte', 'clima tempo', 'busca índice', 'tema03', 'praça', 'cálculo', 'fórmula', 'sol', 'livro', 'meta', 'grafo', 'tag5', 'versão', 'dia noite']) {
        const t0 = performance.now(); input.value = q; input.dispatchEvent(new Event('input')); out.push(performance.now() - t0);
      }
      return out;
    });
    results.search_ms = stat(lat.map(round));
  }
  if (SCEN.includes('edit')) {
    await p.page.evaluate(() => { const d = UrbeCore.service('documents').list().find((x) => x.path.endsWith('.md')); UrbeCore.commands.execute('document.open', { id: d.id, path: d.path }); });
    await p.page.waitForTimeout(800);
    const big = await p.page.evaluate(() => { const ed = document.getElementById('renderedPreview'); return !!ed; });
    if (big) {
      await p.page.evaluate(() => { const d = UrbeCore.service('documents').list().find((x) => x.path.endsWith('.md')); UrbeCore.commands.execute('document.update', { id: d.id, path: d.path, content: '# Grande\n\n' + ('palavra outra palavra '.repeat(5000)) }); });
      await p.page.waitForTimeout(500);
      await p.page.click('#renderedPreview').catch(() => {});
      const lat = await p.page.evaluate(async () => {
        const el = document.getElementById('renderedPreview'); el.focus(); const out = [];
        for (let i = 0; i < 100; i++) { const t0 = performance.now(); document.execCommand('insertText', false, 'x'); await new Promise((r) => requestAnimationFrame(() => r())); out.push(performance.now() - t0); }
        return out;
      });
      results.typing_ms_per_char = stat(lat.map(round));
    }
  }
  if (SCEN.includes('city')) {
    await p.page.evaluate(() => { const b = document.querySelector('[data-view="world"],#navWorld,button[aria-label="Cidade"]'); b && b.click(); UrbeCore.commands.execute('workspace.navigate.world'); }).catch(() => {});
    await p.page.waitForTimeout(1500);
    const fps = (ms) => p.page.evaluate((ms) => new Promise((res) => { let n = 0; const t0 = performance.now(); const f = () => { n++; if (performance.now() - t0 < ms) requestAnimationFrame(f); else res(n / ((performance.now() - t0) / 1000)); }; requestAnimationFrame(f); }), ms);
    results.city_idle_fps = stat([round(await fps(4000))]);
    const box = await p.page.locator('#game').boundingBox().catch(() => null);
    if (box) {
      const cx = box.x + box.width / 2, cy = box.y + box.height / 2;
      const drag = (async () => { await p.page.mouse.move(cx, cy); await p.page.mouse.down(); for (let i = 0; i < 60; i++) { await p.page.mouse.move(cx + Math.sin(i / 6) * 200, cy + Math.cos(i / 6) * 120); await p.page.waitForTimeout(50); } await p.page.mouse.up(); })();
      const f = await fps(3000); await drag; results.city_drag_fps = stat([round(f)]);
    }
    results.heap_after_city_mb = stat([round(await heapMB(p))]);
  }
  await p.ctx.close();
}
if (SCEN.includes('tutorial')) {
  const t = [];
  for (let i = 0; i < RUNS; i++) { const p = await newPage({ seed: null }); const t0 = Date.now(); await p.page.goto(srv.url + '/index.html'); await p.page.waitForFunction(() => window.UrbeCore && UrbeCore.state.select('ready') && UrbeCore.service('documents').list().length >= 40, null, { timeout: 60000 }); t.push(Date.now() - t0); await p.ctx.close(); }
  results.first_open_with_tutorial_ms = stat(t);
}

const ver = browser.version();
await browser.close(); await srv.close();
let commit = 'unknown'; try { commit = execSync('git rev-parse --short HEAD', { encoding: 'utf8' }).trim(); } catch {}
const report = { app: JSON.parse((await import('node:fs')).readFileSync(new URL('../../package.json', import.meta.url), 'utf8')).version, commit, date: new Date().toISOString(), env: { node: process.version, chromium: ver, cpuThrottle: CPU, platform: process.platform }, vault: info, results };
const json = JSON.stringify(report, null, 2);
if (OUT) writeFileSync(OUT, json + '\n');
console.log(json);
