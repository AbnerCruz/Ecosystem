// UC-17/18: shared shell + Explorer/Editor session smoke.
// This is still not UC-29 human/device acceptance.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { createHash } from 'node:crypto';
import { readFile, stat } from 'node:fs/promises';
import { resolve, sep, extname } from 'node:path';
import { chromium } from 'playwright';

// CI watchdog: surface the last completed stage rather than spending an
// entire runner session on a browser/Blazor interaction that never settles.
let lastSmokeStage = 'bootstrap';
function smokeStage(stage) {
    lastSmokeStage = stage;
    console.log('UC-18 Web smoke stage:', stage);
}
setTimeout(() => {
    console.error('UC-18 Web smoke exceeded seven minutes at stage:', lastSmokeStage);
    process.exit(1);
}, 7 * 60 * 1000).unref();

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
    smokeStage('explorer-open');
    await nav.getByRole('link', { name: 'Explorer', exact: true }).click();
    await page.getByRole('heading', { name: 'Explorer', exact: true }).waitFor();
    assert.match(await nav.locator('a.active').innerText(), /Explorer/);

    // UC-18: prove that the shared in-memory workspace really connects
    // Explorer → folder actions/move → Editor → Markdown domain → Visual.
    smokeStage('create-folder');
    await page.getByLabel('Nova pasta').fill('Destino');
    await page.getByRole('button', { name: 'Criar pasta', exact: true }).click();
    await page.getByLabel('Ações de Destino').waitFor();

    await page.getByLabel('Nova nota').fill('Smoke');
    await page.getByRole('button', { name: 'Criar', exact: true }).click();
    await page.getByRole('heading', { name: 'Smoke', exact: true }).waitFor();
    assert.match(await nav.locator('a.active').innerText(), /Editor/);

    smokeStage('source-edit');
    await page.getByRole('button', { name: 'Fonte', exact: true }).click();
    const source = page.getByLabel('Markdown da nota');
    smokeStage('source-inline-code');
    await source.fill('antes palavra depois');
    await source.evaluate(element => element.setSelectionRange(6, 13));
    await page.getByRole('button', { name: 'Código em linha', exact: true }).click();
    try {
        await page.waitForFunction(
            () => document.querySelector('.editor-source-field textarea')?.value === 'antes `palavra` depois',
            null,
            { timeout: 5500 });
    } catch (error) {
        const alert = await page.getByRole('alert').allInnerTexts();
        const status = await page.getByRole('status').allInnerTexts();
        throw new Error(`Código em linha não atualizou o textarea: alert=${JSON.stringify(alert)}; status=${JSON.stringify(status)}; atual=${JSON.stringify(await source.inputValue())}`, { cause: error });
    }
    assert.equal(await source.inputValue(), 'antes `palavra` depois');
    assert.deepEqual(
        await source.evaluate(element => [element.selectionStart, element.selectionEnd]),
        [7, 14]);
    await page.getByRole('button', { name: 'Código em linha', exact: true }).click();
    assert.equal(await source.inputValue(), 'antes palavra depois');

    await source.evaluate(element => element.setSelectionRange(6, 6));
    await page.getByRole('button', { name: 'Código em linha', exact: true }).click();
    assert.equal(await source.inputValue(), 'antes ``palavra depois');
    assert.deepEqual(
        await source.evaluate(element => [element.selectionStart, element.selectionEnd]),
        [7, 7]);
    await page.getByRole('button', { name: 'Código em linha', exact: true }).click();
    assert.equal(await source.inputValue(), 'antes palavra depois');

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
    smokeStage('split-visual-source');
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


    smokeStage('reference-panel');
    await page.getByRole('button', { name: 'Fixar nota em painel', exact: true }).click();
    await page.getByRole('button', { name: 'Fixar nota em painel', exact: true }).click();
    const referencePanel = page.getByRole('complementary', { name: 'Painel de referência' });
    await referencePanel.waitFor();
    assert.equal(await referencePanel.locator('.reference-entry').count(), 1, 'Repetir fixação não duplica');
    await page.getByRole('button', { name: 'Fixar trecho 2 em painel', exact: true }).click();
    assert.equal(await referencePanel.locator('.reference-entry').count(), 2);
    const pinnedText = await referencePanel.innerText();
    await page.getByRole('button', { name: 'Fonte', exact: true }).click();
    await source.fill(canonical + '\nEdição com referência aberta.\n');
    assert.equal(await referencePanel.innerText(), pinnedText, 'Referência é cópia de consulta, sem mutação da nota');
    await page.getByRole('button', { name: 'Desfazer', exact: true }).click();
    await page.waitForFunction(target => document.querySelector('.editor-source-field textarea')?.value === target, canonical);
    await page.setViewportSize({ width: 390, height: 844 });
    await source.scrollIntoViewIfNeeded();
    const referenceMetrics = await referencePanel.evaluate(panel => ({
        top: panel.getBoundingClientRect().top,
        overflow: document.documentElement.scrollWidth - innerWidth,
        scrollable: getComputedStyle(panel.querySelector('.reference-panel-scroll')).overflowY
    }));
    assert.ok(referenceMetrics.top >= 0 && referenceMetrics.top < 200, 'Painel permanece no topo durante a escrita no celular');
    assert.ok(referenceMetrics.overflow <= 0, 'Painel não provoca overflow horizontal');
    assert.equal(referenceMetrics.scrollable, 'auto');
    await page.setViewportSize(previousViewport);
    await page.getByRole('button', { name: 'Visual', exact: true }).click();

    const createMissingLink = page.getByRole('button', { name: 'Criar Nova ligada', exact: true });
    assert.equal(
        await createMissingLink.isEnabled(),
        true,
        'Criar wikilink não deve depender da possibilidade de edição visual estrutural');
    await createMissingLink.click();
    await page.getByRole('heading', { name: 'Nova ligada', exact: true }).waitFor();
    assert.equal(await referencePanel.locator('.reference-entry').count(), 2, 'Referências sobrevivem à troca de nota');
    await page.getByRole('button', { name: '← Voltar', exact: true }).click();
    await page.getByRole('heading', { name: 'Smoke', exact: true }).waitFor();

    await referencePanel.getByRole('button', { name: 'Remover referência de Smoke', exact: true }).first().click();
    assert.equal(await referencePanel.locator('.reference-entry').count(), 1);
    await page.getByRole('button', { name: 'Fechar painel de referência', exact: true }).click();
    await referencePanel.waitFor({ state: 'hidden' });
    assert.equal(await page.locator('.editor-reference-layout.has-references').count(), 0);

    await page.getByRole('button', { name: '← Voltar', exact: true }).click();
    await page.getByRole('heading', { name: 'Explorer', exact: true }).waitFor();
    const smokeRow = page.getByRole('button', { name: /Smoke\.md/ });
    await smokeRow.waitFor();
    assert.match(await nav.locator('a.active').innerText(), /Explorer/);

    const destinationRow = page.locator('[data-path="Destino"]');
    smokeStage('drag-file-into-folder');
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
    smokeStage('touch-long-press-move');
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

    smokeStage('navigation-after-edits');
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
    // A journey builds three in-memory notes/tabs. Reload and fresh online
    // or offline pages start empty until host persistence is implemented.
    const expectedSessionCount = journey ? '3' : '0';
    assert.equal(await sessionCards.locator('article').nth(0).locator('span').innerText(), expectedSessionCount);
    assert.equal(await sessionCards.locator('article').nth(1).locator('span').innerText(), expectedSessionCount);
    assert.match(await page.getByText('UC-17 integrado').innerText(), /UC-17 integrado/);

    const metrics = await page.evaluate(() => ({
        width: innerWidth,
        content: document.documentElement.scrollWidth
    }));
    assert.ok(metrics.content <= metrics.width, 'Shell não pode causar overflow horizontal');
}

async function assertNoteTemplates(page) {
    const nav = page.getByRole('navigation', { name: 'Navegação principal' });
    smokeStage('note-template-create-source');
    await nav.getByRole('link', { name: 'Explorer', exact: true }).click();
    await page.getByLabel('Nova nota').fill('Modelo base');
    await page.getByRole('button', { name: 'Criar', exact: true }).click();
    await page.getByRole('heading', { name: 'Modelo base', exact: true }).waitFor();

    await page.getByRole('button', { name: 'Fonte', exact: true }).click();
    await page.getByLabel('Markdown da nota').fill('# {{Tema}}\n\nPessoa: {{Pessoa}}\n{{Tema}}');
    await page.getByRole('button', { name: 'Salvar como modelo', exact: true }).click();
    await page.getByRole('status').filter({ hasText: /Modelo criado: Modelos\/Modelo base\.md/ }).waitFor();

    smokeStage('note-template-fill');
    await nav.getByRole('link', { name: 'Explorer', exact: true }).click();
    await page.getByRole('heading', { name: 'Explorer', exact: true }).waitFor();
    await page.getByRole('combobox', { name: 'Selecionar modelo de nota' })
        .selectOption('Modelos/Modelo base.md');
    await page.getByLabel('Tema', { exact: true }).fill('Teste');
    await page.getByLabel('Pessoa', { exact: true }).fill('Alice $&');
    await page.getByLabel('Nova nota').fill('Nota gerada');
    await page.getByRole('button', { name: 'Criar', exact: true }).click();
    await page.getByRole('heading', { name: 'Nota gerada', exact: true }).waitFor();

    await page.getByRole('button', { name: 'Fonte', exact: true }).click();
    assert.equal(
        await page.getByLabel('Markdown da nota').inputValue(),
        '# Teste\n\nPessoa: Alice $&\nTeste');

    const metrics = await page.evaluate(() => ({
        width: innerWidth,
        content: document.documentElement.scrollWidth
    }));
    assert.ok(metrics.content <= metrics.width, 'Campos de modelo não devem provocar overflow');

    // The in-memory preview deliberately cannot restore an editor session
    // across a reload. Return Home before the existing PWA reload checks.
    await nav.getByRole('link', { name: 'Início', exact: true }).click();
    await page.getByRole('heading', { name: 'Urbe', exact: true }).waitFor();
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

        smokeStage('online-' + base);
        console.log('UC-17/18 shell + editor online:', base);
        await page.goto(origin + base);
        await assertShell(page, base === '/preview/');
        if (base === '/preview/')
            await assertNoteTemplates(page);

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

        smokeStage('offline-' + base);
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
