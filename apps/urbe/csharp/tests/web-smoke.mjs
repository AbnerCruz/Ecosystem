// UC-8: composition/bootstrap smoke, not UC-23/29 platform acceptance.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { createHash } from 'node:crypto';
import { readFile, stat } from 'node:fs/promises';
import { resolve, sep, extname } from 'node:path';
import { chromium } from 'playwright';
const root = resolve(process.argv[2] || 'artifacts/web/wwwroot');
await stat(resolve(root, 'index.html'));
const types = { '.wasm': 'application/wasm', '.js': 'text/javascript', '.json': 'application/json', '.html': 'text/html', '.css': 'text/css', '.png': 'image/png', '.webmanifest': 'application/manifest+json' };
const server = createServer(async (request, response) => {
    try {
        const url = new URL(request.url, 'http://localhost');
        const preview = url.pathname.startsWith('/preview/');
        const relative = decodeURIComponent(url.pathname.slice(preview ? '/preview/'.length : 1)) || 'index.html';
        const file = resolve(root, relative);
        if (!file.startsWith(root + sep)) { response.writeHead(403).end(); return; }
        let body = await readFile(file);
        if (preview && relative === 'index.html') body = Buffer.from(body.toString().replace('<base href="/"', '<base href="/preview/"'));
        if (preview && relative === 'service-worker-assets.js') {
            const text = body.toString();
            const manifest = JSON.parse(text.slice(text.indexOf('{'), text.lastIndexOf('}') + 1));
            const index = (await readFile(resolve(root, 'index.html'), 'utf8')).replace('<base href="/"', '<base href="/preview/"');
            manifest.assets.find(asset => asset.url === 'index.html').hash = 'sha256-' + createHash('sha256').update(index).digest('base64');
            body = Buffer.from('self.assetsManifest = ' + JSON.stringify(manifest) + ';');
        }
        response.writeHead(200, { 'Content-Type': types[extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-cache' }).end(body);
    } catch { response.writeHead(404).end(); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
let browser;
try {
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 800, height: 360 } });
    const errors = [];
    const origin = `http://127.0.0.1:${server.address().port}`;
    // Install the narrower scope first: a root SPA worker would otherwise intercept its first navigation.
    for (const base of ['/preview/', '/']) {
        const page = await context.newPage();
        page.on('pageerror', error => { errors.push(error.message); console.error(error.message); });
        page.on('console', message => { if (message.type() === 'error') console.error(message.text()); });
        page.on('response', response => { if (response.status() >= 400) console.error(response.status(), response.url()); });
        console.log('Bootstrap online:', base);
        await page.goto(origin + base);
        await page.getByRole('heading', { name: 'Urbe', exact: true }).waitFor({ timeout: 15000 }).catch(async error => { console.error(await page.content()); throw error; });
        assert.equal(await page.locator('[data-product]').getAttribute('data-product'), 'urbe');
        assert.equal(await page.evaluate(() => getComputedStyle(document.body).backgroundColor), 'rgb(18, 26, 25)', 'Estilos da RCL carregados');
        await page.evaluate(async () => {
            await Promise.race([navigator.serviceWorker.ready, new Promise((_, reject) => setTimeout(() => reject(new Error('Service worker não ficou pronto')), 15000))]);
        });
        await page.reload();
        await page.waitForFunction(() => !!navigator.serviceWorker.controller);
        await page.getByRole('heading', { name: 'Urbe', exact: true }).waitFor();
        await page.close();
    }
    await context.setOffline(true);
    for (const base of ['/', '/preview/']) {
        const page = await context.newPage();
        page.on('pageerror', error => { errors.push(error.message); console.error(error.message); });
        page.on('console', message => { if (message.type() === 'error') console.error(message.text()); });
        page.on('response', response => { if (response.status() >= 400) console.error(response.status(), response.url()); });
        await page.goto(origin + base);
        await page.getByRole('heading', { name: 'Urbe', exact: true }).waitFor();
        assert.match(await page.getByRole('status').innerText(), /Continue usando o Urbe atual/);
        const metrics = await page.evaluate(() => ({ width: innerWidth, content: document.documentElement.scrollWidth }));
        assert.ok(metrics.content <= metrics.width, 'Mobile horizontal sem overflow');
        await page.close();
    }
    assert.deepEqual(errors, []);
    console.log('UC-8 Web: composição C# + assets RCL + bootstrap publicado online/offline em / e /preview/ OK.');
} finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
}
