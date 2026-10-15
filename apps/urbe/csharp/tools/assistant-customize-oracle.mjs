// UC-21 oracle: the Assistente's appearance tools of the UNMODIFIED Urbe 1.8.4-beta (src/customize/ai-tools.js) over the
// real src/customize/customize.js and src/ai/tools.js in node:vm: appearance_schema (the format, presets, fonts, saved
// themes, texture packs and the current configuration) and set_appearance (merge or replace, validation, preview and
// the tema.json written). plugin_guide is not ported: the native app runs no JavaScript plugins (ADD-0021).
// Test-only tool: never part of the native app.
//
//   node csharp/tools/assistant-customize-oracle.mjs > csharp/tests/fixtures/assistant/customize-run-a.json
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const FILES = {
  'Personalização/tema.json': '{\n  "tema": "floresta",\n  "texto": { "tamanho": 16 }\n}\n',
  'Personalização/temas/Minha.json': '{ "nome": "Minha", "claro": false, "base": "escuro", "cores": { "destaque": "#ff8800" } }',
  'Personalização/texturas/Pacote.json': '{ "nome": "Pacote" }'
};

function env() {
  const files = new Map(Object.entries(FILES));
  const ids = new Map([...files.keys()].map((p, i) => [p, 'doc_' + i]));
  const handlers = {};
  const el = () => ({ style: { setProperty() { }, removeProperty() { } }, setAttribute() { }, remove() { }, textContent: '' });
  const doc = p => files.has(p) ? { id: ids.get(p), path: p, title: p.split('/').pop().replace(/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i, ''), content: files.get(p), revision: 1 } : null;
  const services = {
    documents: {
      get: k => doc(k) || doc([...ids.entries()].find(([, v]) => v === k)?.[0]),
      list: () => [...files.keys()].map(doc),
      upsert: d => { if (!ids.has(d.path)) ids.set(d.path, 'doc_' + ids.size); files.set(d.path, d.content); return doc(d.path); }
    },
    persistence: { vault: 'Urbe' }
  };
  const ctx = {
    Math, JSON, Object, Array, String, Number, Map, Set, Promise, RegExp, Error, isFinite, console, setTimeout() { }, clearTimeout() { },
    document: { documentElement: Object.assign(el(), { style: { setProperty() { }, removeProperty() { }, colorScheme: '' } }), querySelector: () => null, getElementById: () => null, createElement: el, head: { appendChild() { } } },
    location: { search: '' }, localStorage: { getItem: () => null, setItem() { }, removeItem() { } },
    UrbeArtifacts: { RE: { text: /\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i, note: /\.(md|markdown)$/i, noteText: /\.(md|markdown|txt)$/i } },
    UrbeCore: {
      service: n => services[n] || null, provide: (n, api) => { services[n] = api; },
      events: { on: (n, f) => { (handlers[n] = handlers[n] || []).push(f); }, emit() { } },
      commands: { register() { } }
    }
  };
  ctx.window = ctx; ctx.globalThis = ctx;
  vm.createContext(ctx);
  vm.runInContext(readFileSync(join(root, 'src/customize/customize.js'), 'utf8'), ctx, { filename: 'customize.js' });
  (handlers['workspace:loaded'] || []).forEach(f => f());
  vm.runInContext(readFileSync(join(root, 'src/ai/tools.js'), 'utf8'), ctx, { filename: 'tools.js' });
  vm.runInContext(readFileSync(join(root, 'src/customize/ai-tools.js'), 'utf8'), ctx, { filename: 'customize-ai-tools.js' });
  const core = { service: n => services[n] || null };
  return { T: ctx.UrbeAITools, ctx: { core, agentId: 'geral' }, files };
}

const CALLS = [
  ['appearance_schema', {}],
  ['set_appearance', { config: { tema: 'sepia', texto: { fonte: 'serifada' } } }],
  ['set_appearance', { config: { cores: { destaque: '#123456' } } }],
  ['set_appearance', { config: { tema: 'claro' }, replace: true }],
  ['set_appearance', { config: '{"cidade":{"fauna":false,"ambiente":"noite"}}' }],
  ['set_appearance', { config: '{ruim' }],
  ['set_appearance', { config: 5 }],
  ['set_appearance', { config: { tema: 'Personalização/temas/Minha.json' } }],
  ['set_appearance', { config: { tema: 'inexistente', texto: { tamanho: 99 } } }],
  ['set_appearance', { config: { texto: 'grande', forma: { cantos: 'quadrados' } } }],
  ['set_appearance', { config: { tema: 'floresta', texto: { tamanho: 16 } } }]
];

const calls = [];
for (const [name, input] of CALLS) {
  const e = env();
  const tool = e.T.get(name);
  let preview = null, out = null, error = null;
  try { if (tool.preview) preview = tool.preview(JSON.parse(JSON.stringify(input)), e.ctx); } catch (x) { preview = { error: x.message }; }
  try { out = await tool.run(JSON.parse(JSON.stringify(input)), e.ctx); } catch (x) { error = x.message; }
  calls.push({ name, input, label: tool.label(input), access: tool.access, description: tool.description, parameters: tool.parameters, preview,
    content: out ? out.content : null, error, changes: out ? out.changes || null : null, file: e.files.get('Personalização/tema.json') ?? null });
}
process.stdout.write(JSON.stringify({ source: 'apps/urbe 1.8.4-beta src/customize/ai-tools.js + customize.js in node:vm', files: FILES, calls }, null, 1) + '\n');
