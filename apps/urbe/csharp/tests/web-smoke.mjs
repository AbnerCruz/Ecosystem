// UC-17/18: shared shell + Explorer/Editor session smoke.
// This is still not UC-29 human/device acceptance.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { createHash } from 'node:crypto';
import { readFile, stat } from 'node:fs/promises';
import { resolve, sep, extname } from 'node:path';
import { chromium } from 'playwright';

const root = resolve(process.argv[2] || 'artifacts/web/wwwroot');
await stat(resolve(root, 'index.html'));

const types = {
    '.wasm': 'application/wasm',
    '.js': 'text/javascript',
    '.json': 'application/json',
    '.html': 'text/html',
    '.css': 'text/css',
    '.png': 'image/png',
    '.webmanifest': 'application/manifest+json'
};

const server = createServer(async (request, response) => {
    try {
        const url = new URL(request.url, 'http://localhost');
        const preview = url.pathname.startsWith('/preview/');
        const relative = decodeURIComponent(
            url.pathname.slice(preview ? '/preview/'.length : 1)) || 'index.html';
        const file = resolve(root, relative);
        if (!file.startsWith(root + sep)) {
            response.writeHead(403).end();
            return;
        }

        let body = await readFile(file);
        if (preview && relative === 'index.html') {
            body = Buffer.from(
                body.toString().replace('<base href="/"', '<base href="/preview/"'));
        }
        if (preview && relative === 'service-worker-assets.js') {
            const text = body.toString();
            const manifest = JSON.parse(
                text.slice(text.indexOf('{'), text.lastIndexOf('}') + 1));
            const index = (await readFile(resolve(root, 'index.html'), 'utf8'))
                .replace('<base href="/"', '<base href="/preview/"');
            manifest.assets.find(asset => asset.url === 'index.html').hash =
                'sha256-' + createHash('sha256').update(index).digest('base64');
            body = Buffer.from('self.assetsManifest = ' + JSON.stringify(manifest) + ';');
        }

        response.writeHead(200, {
            'Content-Type': types[extname(file)] || 'application/octet-stream',
            'Cache-Control': 'no-cache'
        }).end(body);
    } catch {
        response.writeHead(404).end();
    }
});

await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));

async function assertShell(page, journey = false) {
    await page.getByRole('heading', { name: 'Urbe', exact: true })
        .waitFor({ timeout: 15000 })
        .catch(async error => {
            console.error(await page.content());
            throw error;
        });

    assert.equal(
        await page.locator('[data-product]').getAttribute('data-product'),
        'urbe');

    const nav = page.getByRole('navigation', { name: 'Navegação principal' });
    const labels = await nav.getByRole('link').allTextContents();
    assert.deepEqual(
        labels.map(value => value.trim()),
        ['Início', 'Explorer', 'Editor', 'Cidade', 'Mais']);

    assert.match(await nav.locator('a.active').innerText(), /Início/);
    assert.equal(
        await page.evaluate(() => getComputedStyle(document.body).backgroundColor),
        'rgb(18, 26, 25)',
        'Estilos da RCL carregados');

    if (journey) {
    await nav.getByRole('link', { name: 'Explorer', exact: true }).click();
    await page.getByRole('heading', { name: 'Explorer', exact: true }).waitFor();
    assert.match(await nav.locator('a.active').innerText(), /Explorer/);

    // UC-18: prove that the shared in-memory workspace really connects
    // Explorer → folder actions/move → Editor → Markdown domain → Visual.
    await page.getByLabel('Nova pasta').fill('Destino');
    await page.getByRole('button', { name: 'Criar pasta', exact: true }).click();
    await page.getByLabel('Ações de Destino').waitFor();

    await page.getByLabel('Nova nota').fill('Smoke');
    await page.getByRole('button', { name: 'Criar', exact: true }).click();
    await page.getByRole('heading', { name: 'Smoke', exact: true }).waitFor();
    assert.match(await nav.locator('a.active').innerText(), /Editor/);

    // UC-18 / REQ-094: native C# clock, stopwatch and timer share the
    // editor footer; run/pause/reset do not modify the Markdown document.
    const timeTools = page.getByRole('group', { name: 'Ferramentas de tempo' });
    await timeTools.waitFor();
    assert.match(await timeTools.getByLabel('Hora local').innerText(), /^\d{2}:\d{2}:\d{2}$/);
    await timeTools.getByRole('button', { name: 'Cronômetro', exact: true }).click();
    assert.equal(await timeTools.getByLabel('Tempo do cronômetro').innerText(), '00:00');
    await timeTools.getByRole('button', { name: 'Iniciar cronômetro' }).click();
    await page.waitForFunction(
        () => document.querySelector('[aria-label="Tempo do cronômetro"]')?.textContent !== '00:00',
        null, { timeout: 5000 });
    await timeTools.getByRole('button', { name: 'Pausar cronômetro' }).click();
    await timeTools.getByRole('button', { name: 'Zerar cronômetro' }).click();
    assert.equal(await timeTools.getByLabel('Tempo do cronômetro').innerText(), '00:00');
    await timeTools.getByRole('button', { name: 'Timer', exact: true }).click();
    await timeTools.getByLabel('Tempo (minutos)').fill('1');
    await timeTools.getByLabel('Tempo (minutos)').press('Tab');
    assert.equal(await timeTools.getByLabel('Tempo restante').innerText(), '01:00');
    await timeTools.getByRole('button', { name: 'Iniciar timer' }).click();
    await timeTools.getByRole('button', { name: 'Pausar timer' }).waitFor();
    await timeTools.getByRole('button', { name: 'Reiniciar timer' }).click();
    assert.equal(await timeTools.getByLabel('Tempo restante').innerText(), '01:00');

    await page.getByRole('button', { name: 'Fonte', exact: true }).click();
    const source = page.getByLabel('Markdown da nota');
    await source.fill('# Primeira versão\\n\\nTexto inicial.');
    await source.fill('# Título smoke\\n\\nTexto **forte**. [[Nova ligada]]');
    assert.equal(await source.inputValue(), '# Título smoke\\n\\nTexto **forte**. [[Nova ligada]]');

    await page.getByRole('button', { name: 'Desfazer', exact: true }).click();
    await page.waitForFunction(
        () => document.querySelector('textarea')?.value.includes('Primeira versão'));
    assert.match(await source.inputValue(), /Primeira versão/);

    await page.getByRole('button', { name: 'Refazer', exact: true }).click();
    await page.waitForFunction(
        () => document.querySelector('textarea')?.value.includes('Título smoke'));
    assert.equal(await source.inputValue(), '# Título smoke\\n\\nTexto **forte**. [[Nova ligada]]');

    await page.getByRole('button', { name: 'Visual', exact: true }).click();
    await page.locator('.editor-visual h1', { hasText: 'Título smoke' }).waitFor();
    assert.match(await page.locator('.editor-visual').innerText(), /Texto forte\./);

    // Split mode must reflect Source → canonical model and Visual → Source
    // without creating a parallel document or breaking history on mobile.
    await page.getByRole('button', { name: 'Dividido', exact: true }).click();
    const splitSource = page.getByLabel('Markdown da nota');
    await splitSource.waitFor({ state: 'visible' });
    assert.equal(await page.locator('.editor-stage-layout.split').count(), 1);
    const previousViewport = page.viewportSize();
    await page.setViewportSize({ width: 390, height: 844 });
    const mobileOverflow = await page.evaluate(
        () => document.documentElement.scrollWidth - innerWidth);
    assert.ok(mobileOverflow <= 0, 'Modo dividido não pode causar overflow no celular');
    await page.setViewportSize(previousViewport);

    const canonical = '# Título smoke\n\nTexto **forte**. [[Nova ligada]]\n';
    await splitSource.fill(canonical);
    await page.locator('.editor-visual h1', { hasText: 'Título smoke' }).waitFor();
    const addBlock = page.getByRole('button', { name: 'Adicionar', exact: true });
    assert.equal(await addBlock.isEnabled(), true);
    await addBlock.click();
    await page.waitForFunction(
        () => document.querySelector('.editor-source-field textarea')?.value.endsWith('Texto\n'));
    assert.match(await splitSource.inputValue(), /\n\nTexto\n$/);
    await page.getByRole('button', { name: 'Desfazer', exact: true }).click();
    await page.waitForFunction(
        target => document.querySelector('.editor-source-field textarea')?.value === target,
        canonical);
    await page.getByRole('button', { name: 'Visual', exact: true }).click();
    await page.locator('.editor-visual h1', { hasText: 'Título smoke' }).waitFor();


    const createMissingLink = page.getByRole('button', { name: 'Criar Nova ligada', exact: true });
    assert.equal(
        await createMissingLink.isEnabled(),
        true,
        'Criar wikilink não deve depender da possibilidade de edição visual estrutural');
    await createMissingLink.click();
    await page.getByRole('heading', { name: 'Nova ligada', exact: true }).waitFor();
    await page.getByRole('button', { name: '← Voltar', exact: true }).click();
    await page.getByRole('heading', { name: 'Smoke', exact: true }).waitFor();

    await page.getByRole('button', { name: '← Voltar', exact: true }).click();
    await page.getByRole('heading', { name: 'Explorer', exact: true }).waitFor();
    const smokeRow = page.getByRole('button', { name: /Smoke\.md/ });
    await smokeRow.waitFor();
    assert.match(await nav.locator('a.active').innerText(), /Explorer/);

    const destinationRow = page.locator('[data-path="Destino"]');
    await smokeRow.dragTo(destinationRow);
    await page.waitForFunction(
        () => !document.querySelector('[data-path="Smoke.md"]'),
        null,
        { timeout: 15000 });
    await destinationRow.click();
    await page.waitForFunction(
        () => document.querySelector('[data-path="Destino"]')?.classList.contains('selected'),
        null,
        { timeout: 15000 });
    await page.getByLabel('Ações de Destino').waitFor();
    await page.getByRole('button', { name: 'Abrir pasta', exact: true }).click();
    await page.waitForFunction(
        () => document.querySelector('.explorer-location strong')?.textContent.trim() === 'Destino',
        null,
        { timeout: 15000 });
    await page.locator('[data-path="Destino/Smoke.md"]').waitFor();
    assert.match(
        await page.getByText(/alteração\(ões\) aguardando persistência pelo host/).innerText(),
        /alteração/);

    await page.getByRole('button', { name: '← Raiz', exact: true }).click();

    await page.getByLabel('Nova nota').fill('Outra');
    await page.getByRole('button', { name: 'Criar', exact: true }).click();
    await page.getByRole('heading', { name: 'Outra', exact: true }).waitFor();
    assert.equal(await page.locator('.editor-tab').count(), 3);
    await page.locator('.editor-tab-open').filter({ hasText: 'Smoke' }).click();
    await page.getByRole('heading', { name: 'Smoke', exact: true }).waitFor();

    await nav.getByRole('link', { name: 'Explorer', exact: true }).click();
    await page.getByRole('heading', { name: 'Explorer', exact: true }).waitFor();

    const anotherRow = page.getByRole('button', { name: /Outra\.md/ });
    await anotherRow.dispatchEvent('pointerdown', {
        pointerType: 'touch',
        button: 0,
        isPrimary: true
    });
    await page.waitForTimeout(650);
    await page.getByRole('status').filter({ hasText: /Movendo Outra\.md/ }).waitFor();
    await anotherRow.dispatchEvent('pointerup', {
        pointerType: 'touch',
        button: 0,
        isPrimary: true
    });
    await page.locator('[data-path="Destino"]').click();

    const movedFolder = page.locator('[data-path="Destino"]');
    await movedFolder.click();
    await page.getByLabel('Ações de Destino').waitFor();
    await page.getByRole('button', { name: 'Abrir pasta', exact: true }).click();
    await page.getByRole('button', { name: /Outra\.md/ }).waitFor();
    await page.getByRole('button', { name: /Smoke\.md/ }).waitFor();

    }

    await nav.getByRole('link', { name: 'Cidade', exact: true }).click();
    await page.getByRole('heading', { name: 'Cidade', exact: true }).waitFor();
    assert.match(await nav.locator('a.active').innerText(), /Cidade/);

    await nav.getByRole('link', { name: 'Mais', exact: true }).click();
    await page.getByRole('heading', { name: 'Mais', exact: true }).waitFor();
    assert.match(await nav.locator('a.active').innerText(), /Mais/);

    await nav.getByRole('link', { name: 'Início', exact: true }).click();
    await page.getByRole('heading', { name: 'Urbe', exact: true }).waitFor();
    assert.match(await nav.locator('a.active').innerText(), /Início/);

    // C# preview must not pretend that its in-memory edits survived a host save.
    const persistence = page.getByRole('region', { name: 'Persistência da prévia C#' });
    assert.match(await persistence.innerText(), /Prévia sem salvamento permanente/);
    assert.match(await persistence.innerText(), /Recarregar ou fechar esta prévia pode descartar alterações/);
    const sessionCards = page.getByRole('group', { name: 'Sessão de trabalho' });
    assert.equal(await sessionCards.locator('article').nth(0).locator('span').innerText(), '3');
    assert.equal(await sessionCards.locator('article').nth(1).locator('span').innerText(), '3');
    assert.match(await page.getByText('UC-17 integrado').innerText(), /UC-17 integrado/);

    const metrics = await page.evaluate(() => ({
        width: innerWidth,
        content: document.documentElement.scrollWidth
    }));
    assert.ok(metrics.content <= metrics.width, 'Shell não pode causar overflow horizontal');
}

let browser;
try {
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 800, height: 360 } });
    const errors = [];
    const origin = `http://127.0.0.1:${server.address().port}`;

    // Install the narrower scope first: a root SPA worker would otherwise
    // intercept the first preview navigation.
    for (const base of ['/preview/', '/']) {
        const page = await context.newPage();
        page.setDefaultTimeout(15000);
        page.setDefaultNavigationTimeout(15000);
        page.on('pageerror', error => {
            errors.push(error.message);
            console.error(error.message);
        });
        page.on('console', message => {
            if (message.type() === 'error') console.error(message.text());
        });
        page.on('response', response => {
            if (response.status() >= 400)
                console.error(response.status(), response.url());
        });

        console.log('UC-17/18 shell + editor online:', base);
        await page.goto(origin + base);
        await assertShell(page, base === '/preview/');

        await page.evaluate(async () => {
            await Promise.race([
                navigator.serviceWorker.ready,
                new Promise((_, reject) =>
                    setTimeout(
                        () => reject(new Error('Service worker não ficou pronto')),
                        15000))
            ]);
        });

        await page.reload();
        await page.waitForFunction(
            () => !!navigator.serviceWorker.controller,
            null,
            { timeout: 15000 });
        await assertShell(page, false);
        await page.close();
    }

    await context.setOffline(true);
    for (const base of ['/', '/preview/']) {
        const page = await context.newPage();
        page.on('pageerror', error => {
            errors.push(error.message);
            console.error(error.message);
        });
        page.on('console', message => {
            if (message.type() === 'error') console.error(message.text());
        });
        page.on('response', response => {
            if (response.status() >= 400)
                console.error(response.status(), response.url());
        });

        console.log('UC-17/18 shell + editor offline:', base);
        await page.goto(origin + base);
        await assertShell(page, base === '/');

        assert.match(
            await page.getByRole('status').innerText(),
            /M3 iniciado/);

        await page.close();
    }

    assert.deepEqual(errors, []);
    console.log(
        'UC-17/18 Web: Explorer move mouse/toque + Editor Fonte/Visual + undo/redo + abas + wikilinks + offline OK.');
} finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
}
