// UC-35/UC-18 oracle: the page engine of the UNMODIFIED Urbe 1.8.4-beta (src/pages/engine.js, free.js and
// templates.js are pure: no DOM). Every built-in template (and the simple/skeleton variants of three) is built, normalized
// and rendered with a small vault, plus hand-written specs for Markdown, notes and book blocks; the normalized spec
// (with its section ids) with the input it came from, the compact form, the warnings/errors and the HTML are recorded (each distinct <style>
// and <script> once, in styles[], referenced from the HTML as @@style:n@@).
// Test-only tool: never part of the native app.
//
//   node csharp/tools/pages-engine-oracle.mjs > csharp/tests/fixtures/pages/engine-run-a.json
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const T0 = 1760000000000;
let s = 0x12345678 >>> 0;
const ctx = {
  console,
  Math: Object.create(Math, { random: { value: () => { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; } } }),
  encodeURIComponent, JSON, String, Number, Object, Array, RegExp, isFinite, Error, Set, Map, URL
};
const RD = Date;
function FD(...a) { if (!new.target) return RD(); return a.length ? new RD(...a) : new RD(T0); }
FD.prototype = RD.prototype; FD.now = () => T0; FD.UTC = RD.UTC; FD.parse = RD.parse;
ctx.Date = FD;
ctx.window = ctx;
ctx.globalThis = ctx;
ctx.UrbeArtifacts = { RE: { noteText: /\.(md|markdown|txt)$/i, note: /\.(md|markdown)$/i } };
vm.createContext(ctx);
for (const f of ['src/pages/engine.js', 'src/pages/free.js', 'src/pages/templates.js'])
  vm.runInContext(readFileSync(join(root, f), 'utf8'), ctx, { filename: f });
const P = ctx.UrbePages, TPL = ctx.UrbePageTemplates;

const notes = [
  { path: 'Comece.md', title: 'Comece', modified: '2025-10-01', tags: ['inicio'], content: '# Comece\n\nVeja [[Plano]] e [[Ideias|as ideias]].\n\n- um\n- [x] feito\n- [ ] por fazer\n' },
  { path: 'Projetos/Plano.md', title: 'Plano', modified: '2025-10-03', tags: ['meta', 'plano'], content: '---\ntags: [meta, plano]\n---\n# Plano\n\n## Passos\n\n1. Ler\n2. Escrever **forte** e *leve*\n\n> [!note] Lembrete\n> Voltar para [[Comece]].\n\n| a | b |\n|:-|-:|\n| 1 | 2 |\n' },
  { path: 'Projetos/Tarefas.md', title: 'Tarefas', modified: '2025-10-02', tags: ['plano'], content: '# Tarefas\n\n```js\nconsole.log("oi")\n```\n\n~~velho~~ ==novo== `código` <https://exemplo.com>\n' },
  { path: 'Área/Ideias.md', title: 'Ideias', modified: '2025-09-30', tags: [], content: '# Ideias\n\nUma ideia com ![img](https://exemplo.com/a.png "t") e [link](https://exemplo.com).\n\n***\n\nFim.  \nQuebra.\n' }
];
const documents = { get: p => notes.find(d => d.path === p) || null, list: () => notes.slice() };

const cases = [];
function add(name, spec, opts) {
  const n = P.normalize(spec);
  const html = P.render(n.spec, { documents, preview: !!(opts && opts.preview) });
  cases.push({ name, input: JSON.parse(JSON.stringify(spec)), preview: !!(opts && opts.preview), spec: n.spec, errors: n.errors, warnings: n.warnings, compact: P.compact(n.spec), html });
}

const tctx = { note: { title: 'Plano', path: 'Projetos/Plano.md' }, title: 'Plano', excerpt: 'Passos', folder: 'Projetos' };
for (const t of TPL.list()) {
  const spec = TPL.build(t.id, tctx);
  add('template:' + t.id, spec);
  if (['landing', 'book', 'resume'].includes(t.id)) for (const mode of ['simple', 'skeleton']) add('template:' + t.id + ':' + mode, TPL.variant(spec, mode));
}
add('markdown', { meta: { title: 'Markdown <&>', description: 'd "q"', icon: '★' }, theme: { preset: 'papel', mode: 'auto', primary: '#123456', radius: 4, spacing: 'airy', shadow: 'strong' },
  layout: { brand: 'Marca', navCta: 'Entrar', navCtaUrl: 'https://exemplo.com', footer: 'Feito com **Urbe** · [[Comece]]', progress: true },
  sections: [
    { id: 's1', type: 'text', props: { title: 'Texto', markdown: notes[1].content + '\n' + notes[3].content, columns: 2 }, style: { anchor: 'Texto', menu: true, background: 'surface', css: 'color:red' } },
    { id: 's2', type: 'note', props: { path: 'Comece.md', showMeta: true } },
    { id: 's3', type: 'notes', props: { title: 'Coleção', source: 'folder', folder: 'Projetos', expand: true, sort: 'modified' }, style: { animation: 'zoom', boxed: true, minHeight: 'half', textColor: '#eeeeee', bgColor: '#101010', className: 'x y!' } },
    { id: 's4', type: 'notes', props: { source: 'tag', tag: '#plano', layout: 'list' } },
    { id: 's5', type: 'notes', props: { source: 'recent', layout: 'grid', columns: 3 } },
    { id: 's6', type: 'notes', props: { source: 'list', paths: 'Ideias\nProjetos/Plano.md\nnada' } },
    { id: 's7', type: 'toc', props: {} },
    { id: 's8', type: 'video', props: { url: 'https://youtu.be/abcdefgh', caption: 'v' } },
    { id: 's9', type: 'video', props: { url: 'https://vimeo.com/123' } },
    { id: 's10', type: 'video', props: { url: 'https://exemplo.com/a.mp4' } },
    { id: 's11', type: 'gallery', props: { items: [{ image: 'https://exemplo.com/1.png', caption: 'um' }, { image: 'javascript:alert(1)' }] } },
    { id: 's12', type: 'image', props: { src: 'data:image/png;base64,AAAA', caption: 'c', size: 'full', rounded: false }, style: { background: 'image', image: "https://exemplo.com/b'g.png" } },
    { id: 's13', type: 'contact', props: { email: 'a@b.c', phone: '+55 (11) 9999', links: [{ label: 'GitHub', url: 'https://github.com' }, { label: 'x', url: 'javascript:1' }] } },
    { id: 's14', type: 'countdown', props: {} },
    { id: 's15', type: 'code', props: { title: 'C', code: '<b>&</b>' } },
    { id: 's16', type: 'divider', props: { style: 'wave' } },
    { id: 's17', type: 'divider', props: { style: 'dots' } },
    { id: 's18', type: 'html', props: { code: '<p>livre</p>' }, style: { hidden: true } },
    { id: 's19', type: 'pricing', props: { items: [{ name: 'A', highlight: true }, { name: 'B', features: ' x \n\ny' }] } },
    { id: 's20', type: 'testimonials', props: { items: [{ text: 't', author: 'Zé' }, { text: 'u', author: '', image: 'https://exemplo.com/f.png' }] } },
    { id: 's21', type: 'faq', props: { items: [{ q: 'P?', a: 'R' }, { q: 'Q?', a: 'S' }] } },
    { id: 's22', type: 'quote', props: { image: 'https://exemplo.com/f.png', role: 'r' } },
    { id: 's23', type: 'stats', props: {} },
    { id: 's24', type: 'timeline', props: { items: [{ date: '2024', title: 'A', text: '**b**' }] } },
    { id: 's25', type: 'columns', props: {} },
    { id: 's26', type: 'cta', props: { buttons: [{ label: 'Ir', url: 'mailto:a@b.c' }, { label: '' }] } },
    { id: 's27', type: 'hero', props: { eyebrow: 'Ei', image: 'https://exemplo.com/h.png', layout: 'split', height: 'screen', stack: true, buttons: [{ label: 'x', url: 'https://a.b', variant: 'ghost' }] }, style: { css: '& h1{color:blue}' } },
    { id: 's28', type: 'cards', props: { items: [{ title: 'c', url: 'https://a.b', image: 'https://exemplo.com/c.png', tag: 't' }, { title: 'd' }] } },
    { id: 's29', type: 'features', props: {} }
  ] });
add('preview', { meta: { title: 'Prévia' }, sections: [{ id: 'p1', type: 'text', props: { markdown: 'oi' } }, { id: 'p2', type: 'html', style: { hidden: true } }] }, { preview: true });
add('book', { meta: { title: 'Livro' }, theme: { preset: 'livro' }, layout: { format: 'book', pageSize: '6x9', margins: 'wide', runningHead: 'Cabeça "q"', chapterStyle: 'words', recto: true },
  sections: [
    { id: 'b1', type: 'bookcover', props: { style: 'image', image: 'https://exemplo.com/capa.png', publisher: 'P' } },
    { id: 'b2', type: 'titlepage', props: { place: 'Recife' } },
    { id: 'b3', type: 'booktoc', props: {} },
    { id: 'b4', type: 'part', props: { title: 'Começo', markdown: 'abertura' } },
    { id: 'b5', type: 'chapter', props: { title: 'Um', epigraph: 'frase', epigraphAuthor: 'Alguém' } },
    { id: 'b6', type: 'chapter', props: { source: 'note', path: 'Projetos/Plano.md', title: '' } },
    { id: 'b7', type: 'chapter', props: { title: 'Sem número', numbered: false, dropCap: false } },
    { id: 'b8', type: 'chapters', props: { folder: 'Projetos', sort: 'title' } },
    { id: 'b9', type: 'text', props: { markdown: 'solto' }, style: { css: 'font-size:12pt' } },
    { id: 'b10', type: 'about', props: {} },
    { id: 'b11', type: 'colophon', props: {} }
  ] });
add('book-in-web', { meta: { title: 'Misto' }, sections: [{ id: 'm1', type: 'chapter', props: { title: 'Cap' } }, { id: 'm2', type: 'text', props: {} }] });
add('invalid', { meta: { title: 5, extra: 1 }, theme: { preset: 'nada', primary: 'url(x)', radius: 99, scale: 'x' }, layout: { nav: 'sim', format: 'outro' },
  sections: [{ type: 'nada' }, 3, { id: 'dup', type: 'text', props: { columns: 3, markdown: { a: 1 } }, extra: 1 }, { id: 'dup', type: 'stats', props: { items: 'x' } }] });
add('json-string', '{"meta":{"title":"Do texto"},"sections":[{"id":"j1","type":"text","props":{"markdown":"# T\\n\\nP"}}]}');
add('not-json', '{nada');

// the <style> of a page is mostly the same long stylesheet: each distinct one is kept once
const styles = [];
for (const c of cases) c.html = c.html.replace(/<style>([\s\S]*?)<\/style>/, (_, css) => {
  let i = styles.indexOf(css); if (i < 0) { styles.push(css); i = styles.length - 1; } return '<style>@@style:' + i + '@@</style>';
}).replace(/<script>([\s\S]*?)<\/script>/, (_, js) => {
  let i = styles.indexOf(js); if (i < 0) { styles.push(js); i = styles.length - 1; } return '<script>@@style:' + i + '@@</script>';
});
process.stdout.write(JSON.stringify({ styles, source: 'apps/urbe 1.8.4-beta src/pages (engine.js, free.js, templates.js) in node:vm, Date.now ' + T0 + ', Math.random LCG(0x12345678)', notes, cases }) + '\n');
