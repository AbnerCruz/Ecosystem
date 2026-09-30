// Modelo de artefatos (REQ-014, REQ-039): classificação por caminho e fonte única das extensões.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const ctx = { window: {} }; vm.createContext(ctx);
vm.runInContext(fs.readFileSync(new URL('../src/core/artifacts.js', import.meta.url), 'utf8'), ctx);
const A = ctx.window.UrbeArtifacts;
const t = (p) => A.classify(p).type;

const cases = {
  'Alfa.md': 'note', 'Pasta/Beta.markdown': 'note', 'Diário.txt': 'note',
  'Web/index.html': 'text', 'src/x.js': 'text', 'dados.csv': 'text', 'cfg.yaml': 'text', 'cfg.yml': 'text', 'a.json': 'text', 'a.css': 'text', 'a.mjs': 'text', 'a.htm': 'text',
  'Personalização/tema.json': 'theme', 'Personalização/temas/noite.json': 'theme', 'Personalização/estilos/meu.css': 'style', 'Personalização/texturas/g.json': 'texture', 'Personalização/plugins/ola.js': 'plugin', 'Personalização/outro.md': 'personalization',
  'Páginas/Inicio.page.json': 'page', 'Páginas/Modelos/Blog.template.json': 'page-template', 'Páginas/Blocos/Rodape.block.json': 'page-block', 'x/Y.page.json': 'page',
  '.urbe/mapa.json': 'system', '.urbe/backup/x/manifest.json': 'system', 'Pasta/.pasta': 'system', '.git/config': 'system',
  'imagem.png': 'asset', 'doc.pdf': 'asset', 'canvas/quadro.canvas': 'asset', 'Anexos/foto.JPG': 'asset',
};
for (const [p, type] of Object.entries(cases)) assert.equal(t(p), type, `${p} → ${type}`);

// editável = textual e não-sistema; só note é nota
assert.equal(A.classify('Personalização/plugins/ola.js').editable, true);
assert.equal(A.classify('.urbe/mapa.json').editable, false);
assert.equal(A.classify('imagem.png').editable, false);
assert.equal(A.isNote('a.md'), true); assert.equal(A.isNote('a.js'), false);
assert.equal(A.classify('A.MD').note, true, 'extensão sem diferenciar caixa');
assert.equal(A.classify('\\Pasta\\a.md').note, true, 'barras invertidas normalizadas');

// as regexes exportadas são a fonte única
assert.ok(A.RE.text.test('x.yml') && !A.RE.text.test('x.canvas') && !A.RE.text.test('x.png'));
assert.ok(A.RE.note.test('x.markdown') && !A.RE.note.test('x.txt'));
const m = 'Nome.js'.match(A.RE.splitText); assert.deepEqual([m[1], m[2]], ['Nome', '.js']);

// roteamento de abertura por tipo (substitui o wrapper de document.open do Studio)
const calls = [];
A.registerOpener('page', (d) => { calls.push('open:' + d.path); return 'estudio'; });
A.onBeforeOpen((d, c) => calls.push('before:' + d.path + (c.raw ? ':raw' : '')));
assert.deepEqual({ ...A.route({ path: 'P/Inicio.page.json' }, {}) }, { handled: true, result: 'estudio' });
assert.equal(A.route({ path: 'P/Inicio.page.json' }, { raw: true }).handled, false, 'raw força o editor comum');
assert.equal(A.route({ path: 'a.md' }, {}).handled, false, 'nota segue o fluxo normal');
assert.deepEqual(calls, ['before:P/Inicio.page.json', 'open:P/Inicio.page.json', 'before:P/Inicio.page.json:raw', 'before:a.md']);

// só arquivos de dentro do Urbe não são alvo de [[link]]
for (const p of ['Personalização/plugins/x.js', 'Personalização/tema.json', '.urbe/mapa.json', 'Personalização/estilos/a.css']) assert.equal(A.linkable(p), false, p);
for (const p of ['Alfa.md', 'Páginas/Inicio.page.json', 'web/index.html']) assert.equal(A.linkable(p), true, p);

// nenhuma cópia da lista de extensões fora de artifacts.js (REQ-039): a lista literal só existe aqui
const walk = (d) => fs.readdirSync(new URL('../' + d, import.meta.url), { withFileTypes: true }).flatMap((e) => (e.isDirectory() ? walk(d + '/' + e.name) : [d + '/' + e.name]));
const offenders = walk('src').filter((f) => f.endsWith('.js') && f !== 'src/core/artifacts.js' && f !== 'src/tutorial/content.js').filter((f) => /md\|markdown\|txt\|html\?\|js\|mjs\|css\|json\|ya\?ml\|csv/.test(fs.readFileSync(new URL('../' + f, import.meta.url), 'utf8')));
assert.deepEqual(offenders, [], 'lista de extensões duplicada em: ' + offenders.join(', '));
console.log('artifacts: ok');
