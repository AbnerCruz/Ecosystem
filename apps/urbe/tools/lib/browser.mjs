// Utilitários de navegador real (Playwright/Chromium) para E2E e performance (REQ-061, REQ-070).
import { loadPlaywright } from './playwright.mjs';
import { startServer } from './static-server.mjs';

/** Sobe o servidor estático e o Chromium; devolve helpers para criar páginas isoladas. */
export async function launchApp({ cpu = 1, viewport = { width: 1280, height: 800 }, args = [], headers } = {}) {
  const { chromium } = await loadPlaywright();
  const srv = await startServer({ headers });
  const browser = await chromium.launch({ args: ['--enable-precise-memory-info', ...args] });
  const pages = [];
  async function newPage({ seed = null, initScript = null, contextOptions = {}, mobile = false } = {}) {
    const ctx = await browser.newContext({ viewport: mobile ? { width: 390, height: 844 } : viewport, ...(mobile ? { hasTouch: true, isMobile: true } : {}), acceptDownloads: true, ...contextOptions });
    const page = await ctx.newPage();
    const errors = [];
    page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
    page.on('console', (m) => { if (m.type() === 'error') errors.push('console.error: ' + m.text()); });
    if (cpu > 1) await (await ctx.newCDPSession(page)).send('Emulation.setCPUThrottlingRate', { rate: cpu });
    await page.addInitScript(() => { // contadores de gravação no IndexedDB (só do harness)
      window.__idb = { puts: 0, bytes: 0, reset() { this.puts = 0; this.bytes = 0; } };
      const put = IDBObjectStore.prototype.put;
      IDBObjectStore.prototype.put = function (v, k) { window.__idb.puts++; window.__idb.bytes += typeof v === 'string' ? v.length : (v && v.size) || 0; return put.call(this, v, k); };
    });
    if (initScript) await page.addInitScript(initScript);
    if (seed) { await page.goto(srv.url + '/manifest.webmanifest'); await seedVault(page, seed); }
    const handle = { ctx, page, errors, url: srv.url };
    pages.push(handle);
    return handle;
  }
  return { url: srv.url, browser, newPage, async close() { for (const p of pages) await p.ctx.close().catch(() => {}); await browser.close(); await srv.close(); } };
}

/** Semeia o vault "Urbe" no IndexedDB do modo interno (a página precisa estar na origem do app). seed: {files: Map|Array de [path, string|Uint8Array]} */
export async function seedVault(page, seed, cidade = 'Urbe') {
  const files = [...(seed.files || seed)].map(([p, c]) => [p, typeof c === 'string' ? c : Array.from(c)]);
  await page.evaluate(async ({ files, cidade }) => {
    const db = await new Promise((ok, err) => { const r = indexedDB.open('knowledge-city', 3); r.onupgradeneeded = (e) => { const d = e.target.result; for (const s of ['kv', 'fs', 'blobs']) if (!d.objectStoreNames.contains(s)) d.createObjectStore(s); }; r.onsuccess = () => ok(r.result); r.onerror = () => err(r.error); });
    await new Promise((ok, err) => { const t = db.transaction(['fs', 'kv'], 'readwrite'); const fs = t.objectStore('fs'); for (const [p, c] of files) fs.put(typeof c === 'string' ? c : new Blob([new Uint8Array(c)]), cidade + '/' + p); t.objectStore('kv').put(cidade, 'ultimaCidade'); t.oncomplete = ok; t.onerror = () => err(t.error); });
    db.close();
  }, { files, cidade });
}

/** Abre o app e espera o core pronto com pelo menos `docs` documentos. Retorna ms. */
export async function openApp(h, { docs = 1, timeout = 60000, path = '/index.html', waitCity = true } = {}) {
  const t0 = Date.now();
  await h.page.goto(h.url + path);
  await h.page.waitForFunction((n) => window.UrbeCore && UrbeCore.state.select('ready') && UrbeCore.service('documents').list().length >= n, docs, { timeout });
  const ms = Date.now() - t0;
  if (waitCity) await waitCityLoaded(h.page, timeout);
  return ms;
}

/** Lê o vault "Urbe" do IndexedDB como Map path → string (Blob vira {blob:true,size}). */
export async function readVault(page, cidade = 'Urbe') {
  return await page.evaluate(async (cidade) => {
    const db = await new Promise((ok, err) => { const r = indexedDB.open('knowledge-city', 3); r.onsuccess = () => ok(r.result); r.onerror = () => err(r.error); });
    const out = await new Promise((ok, err) => { const t = db.transaction('fs', 'readonly'), s = t.objectStore('fs'), res = {}; const req = s.openCursor(); req.onsuccess = () => { const c = req.result; if (!c) return ok(res); if (String(c.key).startsWith(cidade + '/')) res[String(c.key).slice(cidade.length + 1)] = typeof c.value === 'string' ? c.value : { blob: true, size: c.value.size }; c.continue(); }; req.onerror = () => err(req.error); });
    db.close(); return out;
  }, cidade);
}

/** Espera o core terminar de gravar (debounce 900 ms + flush). */
export async function waitSaved(page, timeout = 15000) {
  await page.waitForFunction(() => { const p = UrbeCore.service('persistence'); return p && !p.timer && !p.pending && !p.busy; }, null, { timeout });
}

/** Espera a abertura da cidade terminar (overlay de carregamento fechado e camada legada de sincronização liberada). */
export async function waitCityLoaded(page, timeout = 60000) {
  await page.waitForFunction(() => { const el = document.getElementById('v21Loading'); return !el || !el.classList.contains('open'); }, null, { timeout });
  await page.waitForTimeout(400);
}

/** Semeia chaves do store kv do IndexedDB (ex.: kv["cidade"] legado). */
export async function seedKv(page, entries) {
  await page.evaluate(async (entries) => {
    const db = await new Promise((ok, err) => { const r = indexedDB.open('knowledge-city', 3); r.onupgradeneeded = (e) => { const d = e.target.result; for (const s of ['kv', 'fs', 'blobs']) if (!d.objectStoreNames.contains(s)) d.createObjectStore(s); }; r.onsuccess = () => ok(r.result); r.onerror = () => err(r.error); });
    await new Promise((ok, err) => { const t = db.transaction('kv', 'readwrite'); for (const [k, v] of entries) t.objectStore('kv').put(v, k); t.oncomplete = ok; t.onerror = () => err(t.error); });
    db.close();
  }, entries);
}
