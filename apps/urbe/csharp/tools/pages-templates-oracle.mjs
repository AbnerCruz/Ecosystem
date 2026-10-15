// UC-21/UC-35 oracle: the built-in page templates of the UNMODIFIED Urbe 1.8.4-beta (src/pages/templates.js) built
// as pages/ai-tools.js builds them, for several title/folder/note combinations, on 09/10/2025. Ids are random in the
// original (sections and free-layout pieces), so they are left out. Test-only tool.
//
//   node csharp/tools/pages-templates-oracle.mjs > csharp/tests/fixtures/pages/templates-run-a.json
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const T0 = Date.UTC(2025, 9, 9, 12);
const RD = Date;
function FD(...a) { if (!new.target) return RD(); return a.length ? new RD(...a) : new RD(T0); }
FD.prototype = RD.prototype; FD.now = () => T0; FD.UTC = RD.UTC; FD.parse = RD.parse;
const ctx = { console, JSON, Object, Array, String, Number, Math, RegExp, Error, isFinite, encodeURIComponent, Date: FD };
ctx.window = ctx; ctx.globalThis = ctx;
vm.createContext(ctx);
for (const f of ['engine.js', 'free.js', 'templates.js']) vm.runInContext(readFileSync(join(root, 'src/pages', f), 'utf8'), ctx, { filename: f });
const TPL = ctx.UrbePageTemplates;
const strip = v => Array.isArray(v) ? v.map(strip) : v && typeof v === 'object' ? Object.fromEntries(Object.entries(v).filter(([k]) => k !== 'id').map(([k, x]) => [k, strip(x)])) : v;
const CTX = [{}, { title: 'Meu "Site" <novo>' }, { folder: 'Projetos/Antigos' }, { folder: 'Raiz' }, { note: { title: 'Plano', path: 'Projetos/Plano.md' } },
  { title: 'Título', folder: 'Docs' }, { title: 'Outro', note: { title: 'Plano', path: 'Plano.md' } }];
const runs = [];
for (const t of TPL.list()) for (const c0 of CTX) {
  const c = { title: c0.title || '', folder: c0.folder || '' };
  if (c0.note) { c.note = c0.note; c.title = c.title || c0.note.title; }
  runs.push({ id: t.id, input: c0, spec: strip(JSON.parse(JSON.stringify(TPL.build(t.id, c)))) });
}
process.stdout.write(JSON.stringify({ source: 'apps/urbe 1.8.4-beta src/pages/templates.js in node:vm, 2025-10-09', list: TPL.list().map(t => ({ id: t.id, name: t.name, icon: t.icon, description: t.description, needs: t.needs || null })), runs }, null, 1) + '\n');
