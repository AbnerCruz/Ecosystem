// UC-21 oracle: the tools of the UNMODIFIED Urbe 1.8.4-beta assistant (src/ai/tools.js) in node:vm over a small
// documents service (get by id or path, list, upsert; title = file name), a trash and the editor context. For each
// call it records the text for the model (or the error), the changes (before/after), the reads and the notes after;
// plus validate(), applyEdit() and appendText() on their own. The tools that need the link index or the quick-open
// ranking (workspace_overview, get_links, find_by_tag, search_notes) are left to the native tests.
// Test-only tool: never part of the native app.
//
//   node csharp/tools/assistant-tools-oracle.mjs > csharp/tests/fixtures/assistant/tools-run-a.json
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
  { path: 'Comece.md', content: '# Comece\n\nVeja [[Plano]] e [[Ideias|as ideias]].\n\n## Tarefas\n- uma\n- duas\n\n## Fim\nTchau.\n', modified: '2025-10-01', tags: [] },
  { path: 'Projetos/Plano.md', content: '# Plano\n\nPassos em [[Tarefas]]. Volta para [[Comece]].\nAção com acentuação.\nação repetida ação\n', modified: '2025-10-03', tags: [] },
  { path: 'Projetos/Antigos/Rascunho.md', content: '# Rascunho\n\nDe [[Plano|o plano]] e [[Plano#Passos]].\n', modified: '2025-09-01', tags: [] },
  { path: 'Área/Ideias.md', content: Array.from({ length: 60 }, (_, i) => 'Linha ' + (i + 1) + ' ' + 'x'.repeat(i % 7 * 40)).join('\n'), modified: '2025-10-02', tags: [] },
  { path: 'Vazia.md', content: '', modified: '2025-08-01', tags: [] },
  { path: 'Duas/Nota.md', content: 'a', modified: '', tags: [] },
  { path: 'Outra/Nota.md', content: 'b', modified: '', tags: [] }
];

function makeCore(editor) {
  let docs = NOTES.map((n, i) => ({ id: 'doc_' + i, title: n.path.split('/').pop().replace(/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i, ''), links: [], ...JSON.parse(JSON.stringify(n)) }));
  let seq = 100;
  const documents = {
    get: k => docs.find(d => d.id === k) || docs.find(d => d.path === k) || null,
    list: () => docs.slice(),
    upsert: data => {
      const at = data.id ? docs.findIndex(d => d.id === data.id) : -1;
      const title = data.title || data.path.split('/').pop().replace(/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i, '');
      const d = { links: [], tags: [], ...(at >= 0 ? docs[at] : {}), ...data, id: data.id || 'doc_' + (seq++), title };
      if (at >= 0) docs[at] = d; else docs.push(d);
      return d;
    }
  };
  const trash = { trash: id => { docs = docs.filter(d => d.id !== id); } };
  const folders = [];
  const core = { service: n => ({ documents, trash, explorer: { addFolder: p => folders.push(p) }, knowledge: null, persistence: null }[n] ?? null), commands: { execute() {} } };
  const plans = [], memory = [], opened = [];
  const ctx = { core, agentId: 'geral', editor: () => editor, readChars: 2000, setPlan: s => plans.push(s), remember: f => memory.push(f), openNote: id => opened.push(documents.get(id).path) };
  return { ctx, notes: () => docs.map(d => ({ path: d.path, content: d.content })), folders, plans, memory, opened };
}

const sandbox = { console, JSON, Object, Array, String, Number, Math, RegExp, Error, Promise, Map, Set, Date: FD, isFinite };
sandbox.window = sandbox; sandbox.globalThis = sandbox;
sandbox.UrbeArtifacts = { RE: { text: /\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i, note: /\.(md|markdown)$/i, noteText: /\.(md|markdown|txt)$/i } };
vm.createContext(sandbox);
vm.runInContext(readFileSync(join(root, 'src/ai/tools.js'), 'utf8'), sandbox, { filename: 'tools.js' });
const T = sandbox.UrbeAITools;

const CALLS = [
  ['list_notes', {}], ['list_notes', { folder: 'Projetos', recursive: false }], ['list_notes', { folder: '/Projetos/', limit: 1 }], ['list_notes', { folder: 'Nada' }],
  ['read_note', { path: 'Comece' }], ['read_note', { path: 'projetos/plano' }], ['read_note', { path: 'Ideias' }], ['read_note', { path: 'Área/Ideias.md', start_line: 30 }],
  ['read_note', { path: 'Área/Ideias.md', max_lines: 5 }], ['read_note', { path: 'Vazia' }], ['read_note', { path: 'Nota' }], ['read_note', { path: 'Plnao' }],
  ['read_note', { path: 'Projetos/� Plano.md' }], ['read_note', { path: '' }],
  ['read_notes', { paths: ['Comece', 'Nada', 'Ideias'] }],
  ['grep_notes', { pattern: 'acao' }], ['grep_notes', { pattern: 'plano', context: 1, limit: 2 }], ['grep_notes', { pattern: '^-\\s', regex: true }],
  ['grep_notes', { pattern: '[', regex: true }], ['grep_notes', { pattern: 'zzz', folder: 'Projetos' }], ['grep_notes', { pattern: '  ' }],
  ['get_editor_context', {}],
  ['create_note', { path: 'Novas/Ideia', content: 'Uma ideia\ncom duas linhas' }], ['create_note', { path: 'Comece.md', content: 'x' }], ['create_note', { path: '../fora', content: 'x' }],
  ['create_note', { path: 'Pasta/.oculta', content: 'x' }], ['create_note', { path: 'a:b', content: 'x' }], ['create_note', { path: 'site.html', content: '<p>oi</p>' }],
  ['edit_note', { path: 'Comece', old_text: '- uma', new_text: '- primeira' }], ['edit_note', { path: 'Plano', old_text: 'ação', new_text: 'AÇÃO' }],
  ['edit_note', { path: 'Plano', old_text: 'ação', new_text: 'X', replace_all: true }], ['edit_note', { path: 'Plano', old_text: 'Passos  em', new_text: 'x' }],
  ['edit_note', { path: 'Plano', old_text: 'não existe', new_text: 'x' }], ['edit_note', { path: 'Plano', old_text: '', new_text: 'x' }],
  ['edit_note', { path: 'Plano', old_text: 'Passos', new_text: '$& e $1' }],
  ['write_note', { path: 'Vazia', content: 'Agora cheia' }],
  ['append_to_note', { path: 'Comece', content: 'Mais texto' }], ['append_to_note', { path: 'Comece', content: '- três', heading: 'tarefas' }],
  ['append_to_note', { path: 'Comece', content: 'x', heading: '## Fim' }], ['append_to_note', { path: 'Comece', content: 'x', heading: 'Nenhuma' }], ['append_to_note', { path: 'Vazia', content: 'só isso' }],
  ['rename_note', { path: 'Plano', new_path: 'Projetos/Roteiro' }], ['rename_note', { path: 'Plano', new_path: 'Arquivo/Plano', update_links: true }],
  ['rename_note', { path: 'Plano', new_path: 'Novo', update_links: false }], ['rename_note', { path: 'Plano', new_path: 'Comece' }],
  ['delete_note', { path: 'Rascunho' }], ['create_folder', { path: 'Nova/Pasta/' }], ['create_folder', { path: '.oculta' }],
  ['open_note', { path: 'Comece' }],
  ['update_plan', { steps: [{ step: ' Ler ', status: 'done' }, { step: 'Escrever', status: 'in_progress' }, { step: '', status: 'pending' }, { step: 'Revisar', status: 'x' }] }],
  ['update_plan', { steps: [{ step: 'A', status: 'done' }] }], ['update_plan', { steps: [] }],
  ['remember', { fact: '  Prefere português  ' }], ['remember', { fact: ' ' }]
];

const results = [];
for (const [name, input] of CALLS) {
  const env = makeCore({ path: 'Comece.md', selection: 'Veja [[Plano]]' });
  const tool = T.get(name);
  const label = tool.label(input);
  let preview = null, out = null, error = null;
  try { if (tool.preview) preview = await tool.preview(JSON.parse(JSON.stringify(input)), env.ctx); } catch (e) { preview = { error: e.message }; }
  try { out = await tool.run(JSON.parse(JSON.stringify(input)), env.ctx); } catch (e) { error = e.message; }
  results.push({ name, input, label, preview, content: out ? out.content : null, error, changes: out && out.changes || null, reads: out && out.reads || null,
    notes: env.notes(), folders: env.folders, plans: env.plans, memory: env.memory, opened: env.opened });
}

const validations = [];
const VAL = [
  ['read_note', { path: 'a', start_line: '3' }], ['read_note', { path: 'a', start_line: 2.5 }], ['read_note', { path: 'a', start_line: 0, max_lines: 9000 }],
  ['read_note', { path: 5 }], ['read_note', { path: true }], ['read_note', {}], ['read_note', { path: '' }], ['read_note', { path: 'a', extra: 1 }],
  ['read_notes', { paths: 'Comece' }], ['read_notes', { paths: '["A","B"]' }], ['read_notes', { paths: '{"a":1}' }], ['read_notes', { paths: [1, 'b'] }],
  ['list_notes', { recursive: 'false' }], ['list_notes', { recursive: 'sim' }], ['read_note', { __invalid: '{"pa' }], ['read_note', null], ['read_note', [1]]
];
for (const [name, input] of VAL) {
  const copy = input == null ? input : JSON.parse(JSON.stringify(input));
  const errs = T.validate(T.get(name), copy);
  validations.push({ name, input, errors: errs, after: copy });
}

const edits = [];
for (const [content, oldText, newText, all] of [['a b a', 'a', 'c', false], ['a b a', 'a', 'c', true], ['x', 'x', '$&$&', false], ['ação', 'acao', 'y', false]]) {
  let out = null, error = null;
  try { out = T._applyEdit(content, oldText, newText, all); } catch (e) { error = e.message; }
  edits.push({ content, oldText, newText, all, out, error });
}
const appends = [];
for (const [content, text, heading] of [['', 'novo', null], ['texto\n\n\n', 'novo', null], ['# A\nx\n## B\ny\n\n# C\nz', 'w', 'b'], ['# A\nx\n## B\ny', 'w', 'a'], ['sem', 'w', 'X']]) {
  let out = null, error = null;
  try { out = T._appendText(content, text, heading); } catch (e) { error = e.message; }
  appends.push({ content, text, heading, out, error });
}

const tools = T.list().map(t => ({ name: t.name, access: t.access, description: t.description, parameters: t.parameters }));
process.stdout.write(JSON.stringify({ source: 'apps/urbe 1.8.4-beta src/ai/tools.js in node:vm', notes: NOTES, tools, calls: results, validations, edits, appends }, null, 1) + '\n');
