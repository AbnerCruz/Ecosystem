// UC-35 oracle: "Converter em layout livre" of the UNMODIFIED Urbe 1.8.4-beta studio (src/pages/free.js fromBlock):
// every convertible block, with its defaults and with sample props, becomes the same free layout (ids aside).
// Test-only tool: never part of the native app.
//
//   node csharp/tools/pages-convert-oracle.mjs > csharp/tests/fixtures/pages/convert-run-a.json
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
for (const f of ['engine.js', 'free.js']) vm.runInContext(readFileSync(join(root, 'src/pages', f), 'utf8'), ctx, { filename: f });
const P = ctx.UrbePages, X = P.free;
const strip = v => Array.isArray(v) ? v.map(strip) : v && typeof v === 'object' ? Object.fromEntries(Object.entries(v).filter(([k]) => k !== 'id').map(([k, x]) => [k, strip(x)])) : v;
const notes = { 'Diário/Hoje.md': { title: 'Hoje', path: 'Diário/Hoje.md', content: '# Hoje\n\nPrimeiro parágrafo.\n\n## Seção\n\n> uma citação\n\n---\n\n![foto](img/a.png)\n\nFim.' } };
const note = p => notes[p] || null;
const md = 'Um parágrafo com **negrito**.\n\n### Título interno\n\n> citação\n> segunda linha\n\n> [!nota] callout fica texto\n\n***\n\n![alt](https://x.y/z.png)\n\nÚltimo.';
const samples = {
  text: { title: 'Sobre', markdown: md },
  hero: { eyebrow: 'Novidade', title: 'Grande título', subtitle: 'Linha um.\n\nLinha dois.', image: 'https://x.y/h.png', layout: 'center', height: 'screen', buttons: [{ label: 'Ir', url: '#a', variant: 'primary' }, { label: '', url: '#b' }] },
  quote: { text: 'Frase', author: 'Ana', role: 'Autora' },
  image: { src: 'https://x.y/i.png', alt: '', caption: 'Legenda' },
  bookcover: { title: 'Livro', subtitle: 'Sub', author: 'Autor', publisher: 'Editora' },
  titlepage: { title: 'Livro', subtitle: '', author: 'Autor', publisher: 'Ed', place: 'Rio', year: '2025' },
  dedication: { markdown: 'Para você.' }, copyright: { markdown: 'Copyright.' }, colophon: { title: 'Colofão', markdown: 'Feito.' },
  about: { title: 'Sobre o autor', markdown: md, author: 'Fulano' }, part: { title: 'Início', markdown: 'Abertura.' },
  chapter: { title: '', source: 'note', path: 'Diário/Hoje.md', epigraph: 'Epígrafe', epigraphAuthor: 'X' },
  note: { path: 'Diário/Hoje.md', showTitle: true }
};
const runs = [];
for (const t of X.CONVERTIBLE) for (const props of [undefined, samples[t]]) {
  const sec = P.newSection(t, props ? JSON.parse(JSON.stringify(props)) : undefined);
  const free = X.fromBlock(sec, { note });
  runs.push({ type: t, props: props || null, section: strip(JSON.parse(JSON.stringify(sec))), free: free ? strip(JSON.parse(JSON.stringify(free))) : null });
}
const paragraphs = strip(JSON.parse(JSON.stringify(X.paragraphs(md))));
process.stdout.write(JSON.stringify({ source: 'apps/urbe 1.8.4-beta src/pages/free.js in node:vm', notes, paragraphs, md, runs }, null, 1) + '\n');
