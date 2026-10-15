// UC-21 oracle: the Assistente's page tools of the UNMODIFIED Urbe 1.8.4-beta (src/pages/ai-tools.js) over the real
// page engine and templates in node:vm, on 09/10/2025: page_schema and write_page (templates, full specs, errors,
// paths, preview, the file written). Ids are random in the original, so they are left out. Test-only tool.
//
//   node csharp/tools/assistant-pages-oracle.mjs > csharp/tests/fixtures/assistant/pages-run-a.json
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const T0 = Date.UTC(2025, 9, 9, 12);
const RD = Date;
function FD(...a) { if (!new.target) return RD(); return a.length ? new RD(...a) : new RD(T0); }
FD.prototype = RD.prototype; FD.now = () => T0; FD.UTC = RD.UTC; FD.parse = RD.parse;
const NOTES = [
  { path: 'Projetos/Plano.md', content: '# Plano\n\nPassos.\n' },
  { path: 'Páginas/Existente.page.json', content: '{"version":1,"kind":"urbe-page","meta":{"title":"Velha"},"sections":[]}\n' }
];
function env() {
  let docs = NOTES.map((n, i) => ({ id: 'doc_' + i, title: n.path.split('/').pop().replace(/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i, ''), links: [], tags: [], ...n }));
  let seq = 100;
  const documents = {
    get: k => docs.find(d => d.id === k) || docs.find(d => d.path === k) || null,
    list: () => docs.slice(),
    upsert: data => {
      const at = data.id ? docs.findIndex(d => d.id === data.id) : -1;
      const d = { links: [], tags: [], ...(at >= 0 ? docs[at] : {}), ...data, id: data.id || 'doc_' + (seq++) };
      if (at >= 0) docs[at] = d; else docs.push(d);
      return d;
    }
  };
  const ctx = { console, JSON, Object, Array, String, Number, Math, RegExp, Error, Promise, Map, Set, isFinite, encodeURIComponent, Date: FD };
  ctx.window = ctx; ctx.globalThis = ctx;
  ctx.UrbeArtifacts = { RE: { text: /\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i, note: /\.(md|markdown)$/i, noteText: /\.(md|markdown|txt)$/i } };
  vm.createContext(ctx);
  vm.runInContext(readFileSync(join(root, 'src/ai/tools.js'), 'utf8'), ctx);
  for (const f of ['engine.js', 'free.js', 'templates.js', 'ai-tools.js']) vm.runInContext(readFileSync(join(root, 'src/pages', f), 'utf8'), ctx, { filename: f });
  return { T: ctx.UrbeAITools, c: { core: { service: n => n === 'documents' ? documents : null }, agentId: 'geral' }, docs: () => docs };
}
const strip = v => Array.isArray(v) ? v.map(strip) : v && typeof v === 'object' ? Object.fromEntries(Object.entries(v).filter(([k]) => k !== 'id').map(([k, x]) => [k, strip(x)])) : v;
const fileOf = t => t == null ? null : JSON.stringify(strip(JSON.parse(t)));
const CALLS = [
  ['page_schema', {}],
  ['write_page', { path: 'Meu site', template: 'landing', title: 'Café' }],
  ['write_page', { path: 'Sites/Portfólio.html', template: 'portfolio' }],
  ['write_page', { path: 'Artigo', template: 'article', note: 'Projetos/Plano' }],
  ['write_page', { path: 'Artigo', template: 'article', note: 'Nada' }],
  ['write_page', { path: 'Coleção', template: 'vault-site', folder: 'Projetos' }],
  ['write_page', { path: 'X', template: 'nenhum' }],
  ['write_page', { path: 'Existente', spec: { meta: { title: 'Nova' }, sections: [{ type: 'hero', props: { title: 'Oi' } }, { type: 'text', props: { markdown: 'Olá' } }] } }],
  ['write_page', { path: 'Ruim', spec: '{"sections":[{"type":"naoexiste"}]}' }],
  ['write_page', { path: 'Ruim', spec: '{nao json' }],
  ['write_page', { path: 'Vazio' }],
  ['write_page', { path: '' , template: 'blank' }],
  ['write_page', { path: '../fora', template: 'blank' }],
  ['write_page', { path: 'A/B:c', template: 'blank' }],
  ['write_page', { path: 'modelo.template.json', template: 'empty', title: 'M' }]
];
const calls = [];
for (const [name, input] of CALLS) {
  const e = env(), tool = e.T.get(name);
  let preview = null, out = null, error = null;
  try { if (tool.preview) { const p = tool.preview(JSON.parse(JSON.stringify(input)), e.c); preview = { path: p.path, before: p.before, after: fileOf(p.after), note: p.note }; } } catch (x) { preview = { error: x.message }; }
  try { out = await tool.run(JSON.parse(JSON.stringify(input)), e.c); } catch (x) { error = x.message; }
  calls.push({ name, input, label: tool.label(input), access: tool.access, description: tool.description, parameters: tool.parameters, preview, content: out ? out.content : null, error,
    changes: out && out.changes ? out.changes.map(c => ({ id: c.id, before: c.before, after: { path: c.after.path, content: fileOf(c.after.content) } })) : null,
    files: e.docs().map(d => ({ path: d.path, content: d.path.endsWith('.json') ? fileOf(d.content) : d.content })) });
}
process.stdout.write(JSON.stringify({ source: 'apps/urbe 1.8.4-beta src/pages/ai-tools.js in node:vm, 2025-10-09', notes: NOTES, calls }, null, 1) + '\n');
