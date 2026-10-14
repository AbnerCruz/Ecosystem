// Generates csharp/src/Urbe.Core/Pages/PageEngineText.g.cs: the literal stylesheets and scripts of the 1.8.4 page
// engine (src/pages/engine.js and free.js), evaluated from the original source so the native engine writes them byte for byte.
// Only constant expressions are taken (BASE_CSS, SECTION_CSS, ANIM_CSS, the page backgrounds, RUNTIME, PREVIEW,
// PREVIEW_CSS and the constant lines of bookCss) and the engine's data as JSON (FONTS, THEMES, the fields of every
// block and of theme/layout/meta/section); everything that depends on the page is ported by hand. Two defaults
// carry the year the engine loaded (titlepage.year, copyright.markdown): GeneratedYear says which, so the native
// engine puts its own year there.
//
//   node csharp/tools/pages-engine-constants.mjs > csharp/src/Urbe.Core/Pages/PageEngineText.g.cs
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const src = readFileSync(join(root, 'src/pages/engine.js'), 'utf8');

function between(start, end) {
  const i = src.indexOf(start);
  if (i < 0) throw new Error('not found: ' + start);
  const j = src.indexOf(end, i + start.length);
  if (j < 0) throw new Error('not found: ' + end);
  return src.slice(i + start.length, j);
}
const evaluate = expr => Function('"use strict";return (' + expr + ')')();

const consts = {
  BaseCss: evaluate('[' + between('var BASE_CSS=[', "].join('');") + "].join('')"),
  SectionCss: evaluate(between('var SECTION_CSS=', ';\n')),
  AnimCss: evaluate(between('var ANIM_CSS=', ';\n')),
  Runtime: evaluate(between('var RUNTIME=', ';\n\n')),
  Preview: evaluate(between('var PREVIEW=', ';\n  var PREVIEW_CSS')),
  PreviewCss: evaluate(between('var PREVIEW_CSS=', ';\n\n'))
};
const bgs = evaluate(between('var bgs=', "[bg]||'';"));
// bookCss: the literal runs of its array (the lines with the page's size, margins and options are ported)
const book = between('function bookCss(s,book){', '\n  var ANIM_CSS');
function run(from, to) {
  const i = book.indexOf(from), j = book.indexOf(to, i);
  if (i < 0 || j < 0) throw new Error('book run: ' + from);
  return evaluate('[' + book.slice(i, j).replace(/,\s*$/, '') + "].join('')");
}
consts.BookSheet = run("'.sheet h1,.sheet h2,.sheet h3,.sheet h4{letter-spacing:0}", "'.sheet .prose{max-width:none");
consts.BookProse = run("'.sheet .prose h3,.sheet .prose h4{", '\n    ];');
consts.BookScreen = run("'html.book{scroll-behavior:auto}", "'@page{size:");
consts.BookNamedPages = run("'@page cover{margin:0;", "'@media print{");
consts.BookPrint = run("'@media print{", "'.sheet.flow{");
consts.BookPrintEnd = evaluate(book.slice(book.indexOf("'.sheet h2,.sheet h3,.bk-chhead{"), book.indexOf("');\n    }") + 1));
for (const k of Object.keys(bgs)) consts['Background_' + k] = bgs[k];

// the data of the engine (fonts, themes, every block's fields with their defaults, the theme/layout/meta/section
// fields), taken from the loaded engine as JSON: render functions and the free tree's coerce are ported
const ctx = { console, JSON, Object, Array, String, Number, Math, RegExp, Error, isFinite, encodeURIComponent, Date };
ctx.window = ctx; ctx.globalThis = ctx;
vm.createContext(ctx);
vm.runInContext(src, ctx, { filename: 'engine.js' });
const free = readFileSync(join(root, 'src/pages/free.js'), 'utf8');
vm.runInContext(free, ctx, { filename: 'free.js' });
const P = ctx.UrbePages;
const fieldsOf = list => list.map(f => { const o = {}; for (const k of Object.keys(f)) if (k !== 'coerce') o[k] = k === 'fields' ? fieldsOf(f[k]) : f[k]; return o; });
consts.FontsJson = JSON.stringify(P.FONTS);
consts.ThemesJson = JSON.stringify(P.THEMES);
consts.BlocksJson = JSON.stringify(Object.keys(P.BLOCKS).map(k => { const b = P.BLOCKS[k]; return { type: k, label: b.label, icon: b.icon, group: b.group, description: b.description, fields: fieldsOf(b.fields) }; }));
consts.ThemeFieldsJson = JSON.stringify(fieldsOf(P.THEME_FIELDS));
consts.LayoutFieldsJson = JSON.stringify(fieldsOf(P.LAYOUT_FIELDS));
consts.MetaFieldsJson = JSON.stringify(fieldsOf(P.META_FIELDS));
consts.SectionFieldsJson = JSON.stringify(fieldsOf(P.SECTION_FIELDS));
consts.GeneratedYear = String(new Date().getFullYear());
// free.js: its element types (content fields) and styles, and its literal stylesheet and shadows
consts.FreeTypesJson = JSON.stringify(Object.keys(P.free.TYPES).map(k => { const t = P.free.TYPES[k]; return { type: k, label: t.label, box: !!t.box, content: fieldsOf(t.content) }; }));
consts.FreeStyleJson = JSON.stringify(P.free.STYLE);
consts.FreeCss = evaluate(free.slice(free.indexOf('var FREE_CSS=') + 'var FREE_CSS='.length, free.indexOf(';\n', free.indexOf('var FREE_CSS='))));
const shadow = evaluate(free.slice(free.indexOf('var SHADOW=') + 'var SHADOW='.length, free.indexOf(';', free.indexOf('var SHADOW='))));
for (const k of Object.keys(shadow)) consts['FreeShadow_' + k] = shadow[k];

function cs(s) {
  // a C# regular string literal
  return '"' + Array.from(s).map(c => {
    const n = c.codePointAt(0);
    if (c === '\\') return '\\\\';
    if (c === '"') return '\\"';
    if (c === '\n') return '\\n';
    if (c === '\r') return '\\r';
    if (c === '\t') return '\\t';
    if (n < 32 || n > 126) return n > 0xffff ? '\\U' + n.toString(16).padStart(8, '0') : '\\u' + n.toString(16).padStart(4, '0');
    return c;
  }).join('') + '"';
}
let out = '// <auto-generated>\n// Generated by csharp/tools/pages-engine-constants.mjs from apps/urbe/src/pages/engine.js (Urbe 1.8.4-beta).\n// Do not edit: regenerate.\n// </auto-generated>\nnamespace Urbe.Core.Pages;\n\ninternal static class PageEngineText\n{\n';
for (const [k, v] of Object.entries(consts)) out += '    public const string ' + k + ' = ' + cs(v) + ';\n';
out += '}\n';
process.stdout.write(out);
