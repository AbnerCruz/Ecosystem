// UC-19 oracle for the 1.8.4-beta search: the UNMODIFIED src/ui/quick-open.js, src/ui/command-palette.js,
// src/core/keymap.js and src/core/artifacts.js run by Node against a minimal fake DOM and UrbeCore.
// Test-only: JavaScript never runs inside the native app.
//   node search-oracle.mjs <apps/urbe/src> < cases.json
// cases: { rank:[{docs,query,limit}], view:[{docs,recent,query}], palette:[{commands,query}],
//          keys:[{event,editable,available}], bindings:true, create:[names], fold:[strings] }
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const src = process.argv[2];
const cases = JSON.parse(readFileSync(0, 'utf8'));
const load = (rel, win) => runInNewContext(readFileSync(`${src}/${rel}`, 'utf8'), win, { filename: rel });
const unesc = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&amp;/g, '&');

class El {
  constructor(tag) { this.tagName = (tag || 'DIV').toUpperCase(); this.hidden = false; this.value = ''; this.innerHTML = ''; this.listeners = {}; this.kids = {}; this.dataset = {}; this.classList = { toggle() { } }; }
  set textContent(v) { this.innerHTML = String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }
  appendChild(el) { this.appended = el; }
  querySelector(s) { return this.kids[s] || (this.kids[s] = new El()); }
  querySelectorAll() { return []; }
  addEventListener(t, f) { this.listeners[t] = f; }
  focus() { }
}
function page(services, commands) {
  const doc = new El('html');
  doc.createElement = t => new El(t);
  doc.body = new El('body');
  doc.activeElement = null;
  doc.contains = () => true;
  const provided = {};
  const listeners = {};
  doc.addEventListener = (t, f) => { listeners[t] = f; };
  const win = {
    document: doc, requestAnimationFrame: f => f(), console, Math, String, Array, Object, JSON, Set, Map, RegExp, Promise,
    UrbeCore: { service: n => services[n] || null, provide: (n, api) => { provided[n] = api; }, commands }
  };
  win.window = win;
  return { win, doc, provided, listeners };
}
const mkDocs = docs => ({ list: () => docs, get: id => docs.find(d => d.id === id) || null });
const out = {};

// ---- quick-open
out.rank = (cases.rank || []).map(c => {
  const p = page({ documents: mkDocs(c.docs) }, { register() { } });
  load('ui/quick-open.js', p.win);
  return p.provided.quickOpen.rank(c.docs, c.query, c.limit).map(r => ({ id: r.doc.id, score: r.score, snippet: r.snippet }));
});
out.view = (cases.view || []).map(c => {
  const reg = {};
  const p = page({ documents: mkDocs(c.docs), explorer: { recent: c.recent || [] } }, { register: (id, d) => { reg[id] = d; } });
  load('ui/quick-open.js', p.win);
  reg['ui.quickOpen.open'].execute();
  const root = p.doc.body.appended;
  const input = root.querySelector('.uqo-input'), list = root.querySelector('.uqo-list');
  input.value = c.query;
  input.oninput();
  const html = list.innerHTML;
  const rows = [...html.matchAll(/<button[^>]*class="uqo-item( uqo-create)?[^"]*"[^>]*>([\s\S]*?)<\/button>/g)].map(m => {
    const strong = unesc(m[2].match(/<strong>([\s\S]*?)<\/strong>/)[1]);
    if (m[1]) return { create: strong.replace(/^Criar nota “/, '').replace(/”$/, '') };
    const sn = m[2].match(/<small class="uqo-snippet">([\s\S]*?)<mark>([\s\S]*?)<\/mark>([\s\S]*?)<\/small>/);
    const fo = m[2].match(/<small>([\s\S]*?)<\/small>/);
    return { title: strong, snippet: sn ? { before: unesc(sn[1]), match: unesc(sn[2]), after: unesc(sn[3]) } : null, folder: !sn && fo ? unesc(fo[1]) : null };
  });
  return { rows, group: html.includes('uqo-group'), empty: html.includes('uqo-empty') };
});
{
  const reg = {};
  const p = page({ documents: mkDocs([]) }, { register: (id, d) => { reg[id] = d; } });
  load('core/artifacts.js', p.win);
  out.create = (cases.create || []).map(q => q.replace(p.win.UrbeArtifacts.RE.note, '') + '.md');
}

// ---- command palette
out.palette = (cases.palette || []).map(c => {
  const reg = {};
  const commands = {
    register: (id, d) => { reg[id] = d; },
    list: () => c.commands.map(x => Object.freeze({ id: x.id, title: x.title, category: x.category, enabled: () => x.enabled !== false })),
    execute() { }
  };
  const p = page({}, commands);
  load('ui/command-palette.js', p.win);
  reg['ui.commandPalette.open'].execute();
  const root = p.doc.body.appended;
  const input = root.querySelector('.ucp-input'), list = root.querySelector('.ucp-list');
  input.value = c.query;
  input.listeners.input();
  const html = list.innerHTML;
  return { ids: [...html.matchAll(/data-command="([^"]*)"/g)].map(m => unesc(m[1])), empty: html.includes('ucp-empty') };
});

// ---- keymap
{
  const executed = [];
  const available = new Set();
  const commands = { has: id => available.has(id), execute: (id, ctx) => { executed.push(id); } };
  const p = page({}, commands);
  load('core/keymap.js', p.win);
  const km = p.provided.keymap;
  if (cases.bindings) out.bindings = km.list();
  out.keys = (cases.keys || []).map(k => {
    available.clear();
    (k.available || []).forEach(a => available.add(a));
    executed.length = 0;
    let prevented = false;
    p.listeners.keydown({ ...k.event, target: k.editable ? { tagName: k.editable, isContentEditable: k.editable === 'CE' } : { tagName: 'DIV' }, preventDefault() { prevented = true; } });
    return { executed: executed[0] ?? null, prevented };
  });
}

process.stdout.write(JSON.stringify(out));
