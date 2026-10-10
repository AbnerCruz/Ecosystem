// Captura a 1.8.4-beta REAL (apps/urbe, sem modificações) para comparar lado a lado com o cliente nativo.
// Tutorial de primeira abertura, Math.random/Date.now fixos (cidade reproduzível), relógio do mundo opcional.
//   node csharp/tools/legacy-screenshot.mjs <saida.png> [largura=1280] [altura=760] [--casa "Comece aqui"] [--touch]
// Requer playwright (PLAYWRIGHT_MODULE=/caminho/playwright/index.mjs, CHROMIUM_PATH=...). Só para desenvolvimento/testes.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const args = process.argv.slice(2), out = args[0];
if (!out) throw new Error('uso: legacy-screenshot.mjs <saida.png> [largura] [altura] [--casa nome] [--touch]');
const num = args.filter(a => /^\d+$/.test(a)).map(Number), width = num[0] || 1280, height = num[1] || 760;
const casa = args.includes('--casa') ? args[args.indexOf('--casa') + 1] : null, touch = args.includes('--touch');
const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.png': 'image/png', '.svg': 'image/svg+xml', '.webmanifest': 'application/manifest+json' };
const server = createServer(async (req, res) => {
  try { const p = normalize(decodeURIComponent(new URL(req.url, 'http://x').pathname)).replace(/^([/\\])+/, ''); const b = await readFile(join(root, p || 'index.html')); res.writeHead(200, { 'content-type': types[extname(p)] || 'application/octet-stream' }); res.end(b); } catch { res.writeHead(404); res.end(); }
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || 'playwright');
const browser = await chromium.launch(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {});
try {
  const ctx = await browser.newContext({ viewport: { width, height }, hasTouch: touch, isMobile: touch, serviceWorkers: 'block' });
  await ctx.route(/^https?:\/\/(?!127\.0\.0\.1)/, r => r.abort());
  await ctx.addInitScript(() => { let s = 0x12345678 >>> 0; Math.random = () => { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; }; const T = 1760000000000; Date.now = () => T; });
  const page = await ctx.newPage();
  await page.goto(`http://127.0.0.1:${server.address().port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => { const d = window.UrbeCore && UrbeCore.service('diagnostics.world'); const w = d && d.legacy(); return w && w.buildings.length >= 48 && w.roads.size > 0; }, null, { timeout: 60000 });
  await page.waitForTimeout(2500);
  // Abre na cidade (o tutorial abre a nota "Comece aqui" no editor): botão Voltar do editor.
  await page.evaluate(() => { const b = document.querySelector('#sidebarToggleMobile'); if (b) b.click(); });
  await page.waitForTimeout(6000);
  if (casa) {
    const pos = await page.evaluate(nome => {
      const w = UrbeCore.service('diagnostics.world').legacy(), r = document.getElementById('game').getBoundingClientRect(), T = 32;
      const bs = w.buildings; let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
      bs.forEach(b => { x0 = Math.min(x0, b.x); y0 = Math.min(y0, b.y - 1); x1 = Math.max(x1, b.x + b.w); y1 = Math.max(y1, b.y + b.h + 1); });
      const z = Math.min(Math.max(Math.min(r.width / ((x1 - x0 + 6) * T), Math.max(200, r.height - 170) / ((y1 - y0 + 6) * T)), .45), 1.3), cx = (x0 + x1) / 2 * T, cy = (y0 + y1) / 2 * T, h = bs.find(b => b.name === nome);
      return { x: r.left + ((h.x + 1.5) * T - cx) * z + r.width / 2, y: r.top + ((h.y + 1.5) * T - cy) * z + r.height / 2 };
    }, casa);
    await page.mouse.click(pos.x, pos.y); await page.waitForTimeout(1800);
  }
  await page.screenshot({ path: out });
  console.log('ok', out);
} finally { await browser.close(); server.close(); }
