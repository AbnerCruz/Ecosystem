// Harness de produção do monólito (RM-F2-02, REQ-064, REQ-003): carrega o `index.html` REAL num Chromium e expõe boot/abertura
// por comportamento, para os testes de extração da F2 não dependerem do TEXTO de `src/app.js` (nem de fatiar por marcadores).
// Camada fina sobre tools/lib/browser.mjs (servidor estático + Playwright). Não é um cenário: `run-e2e` só executa `*.e2e.mjs`.
//
//   const rt = await startRuntime();
//   const app = await rt.open({ fixture: 'v1-mapa-v4' });   // ou { seed } (makeVault) ou nada (primeira abertura)
//   await app.createNote('Pasta/Nota.md', '# Olá');         // comando canônico + salvar
//   (await app.world()).buildings                           // projeção do mundo (documentos → casas)
//   await rt.close();
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { launchApp, openApp, readVault, waitSaved, waitCityLoaded } from '../../tools/lib/browser.mjs';

const FIXTURES = new URL('../fixtures/vaults/', import.meta.url).pathname;
const TEXT = /\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i;

/** Lê um vault histórico de tests/fixtures/vaults/<nome> como Map caminho → string/bytes (sem `expect.json`). */
export function fixtureFiles(name) {
  const out = new Map();
  (function walk(dir, pre) {
    for (const e of readdirSync(dir, { withFileTypes: true })) {
      if (e.isDirectory()) walk(join(dir, e.name), pre + e.name + '/');
      else if (e.name !== 'expect.json') { const buf = readFileSync(join(dir, e.name)); out.set(pre + e.name, TEXT.test(e.name) ? buf.toString('utf8') : new Uint8Array(buf)); }
    }
  })(join(FIXTURES, name), '');
  return out;
}
export const fixtureExpect = (name) => JSON.parse(readFileSync(join(FIXTURES, name, 'expect.json'), 'utf8'));

/** Sobe servidor + Chromium. `open()` cria uma página isolada com o app aberto e devolve os helpers de comportamento. */
export async function startRuntime(options = {}) {
  const launched = await launchApp(options);
  return {
    url: launched.url,
    async open({ fixture = null, seed = null, docs, mobile = false, initScript = null } = {}) {
      const files = seed || (fixture ? fixtureFiles(fixture) : null);
      const want = docs ?? (fixture ? fixtureExpect(fixture).notes.length : seed?.notes ?? 1);
      const h = await launched.newPage({ seed: files, mobile, initScript });
      await openApp(h, { docs: want });
      return wrap(h);
    },
    close: () => launched.close(),
  };
}

function wrap(h) {
  const { page } = h;
  const api = {
    page, errors: h.errors,
    /** Executa um comando canônico do core no app aberto. */
    command: (name, args) => page.evaluate(([n, a]) => UrbeCore.commands.execute(n, a), [name, args]),
    /** Cria (ou atualiza) uma nota, abre e salva; devolve o id. */
    async createNote(path, content) {
      const id = await page.evaluate(([p, c]) => { const d = UrbeCore.commands.execute('document.update', { path: p, content: c }); UrbeCore.commands.execute('document.open', { id: d.id }); return d.id; }, [path, content]);
      await api.save();
      return id;
    },
    async save() { await page.evaluate(() => UrbeCore.commands.execute('workspace.save')); await waitSaved(page); },
    notes: () => page.evaluate(() => Object.fromEntries(UrbeCore.service('documents').list().map((d) => [d.path, { id: d.id, content: d.content }]))),
    /** Projeção do mundo: casas derivadas dos documentos (serviço canônico `world.projection`). */
    world: () => page.evaluate(() => {
      const docs = UrbeCore.service('documents').list(), proj = UrbeCore.service('world.projection');
      return { buildings: docs.map((d) => ({ path: d.path, id: d.id, pos: (proj.projectDocument(d.id) || null) && { x: proj.projectDocument(d.id).x, y: proj.projectDocument(d.id).y } })) };
    }),
    vault: () => readVault(page),
    /** Recarrega a página e espera o core e a cidade prontos. */
    async reload({ docs = 1 } = {}) {
      await page.reload();
      await page.waitForFunction((n) => window.UrbeCore && UrbeCore.state.select('ready') && UrbeCore.service('documents').list().length >= n, docs, { timeout: 60000 });
      await waitCityLoaded(page);
    },
    version: () => page.evaluate(() => UrbeCore.version),
    /** Falha o teste se houve erro de console/página (a regra de todo cenário de produção). */
    expectNoErrors() { if (h.errors.length) throw new Error('erros no app: ' + h.errors.join(' | ')); },
  };
  return api;
}
