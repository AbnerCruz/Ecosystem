// UC-18 oracle for the note editor: the UNMODIFIED 1.8.4-beta app.js / visual-tools.js / markdown.js functions,
// evaluated in Node against a minimal <textarea> stub (setRangeText per the HTML spec, mode "end").
// Test-only: JavaScript never runs inside the native app.
//   node editor-oracle.mjs <apps/urbe/src> <cases.json>
// cases: [{op:'insert',value,start,end,prefix,suffix} | {op:'wiki',value,caret} | {op:'commitWiki',value,caret,name}
//         | {op:'indent',value,start,end} | {op:'format',value,ext} | {op:'stats',value} | {op:'slash',query}
//         | {op:'slashQuery',text} | {op:'markdown',md}]
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const [src, casesFile] = process.argv.slice(2);
const cases = JSON.parse(readFileSync(casesFile, 'utf8'));
const app = readFileSync(`${src}/app.js`, 'utf8');
const vt = readFileSync(`${src}/editor/visual-tools.js`, 'utf8');
const pick = (start) => { const i = app.indexOf(start); if (i < 0) throw new Error('not found: ' + start); return app.slice(i, app.indexOf('\n', i)); };
const pickBlock = (start, end) => { const i = app.indexOf(start); if (i < 0) throw new Error('not found: ' + start); return app.slice(i, app.indexOf(end, i)); };

function textarea() {
  return {
    value: '', selectionStart: 0, selectionEnd: 0, focus() {},
    setRangeText(r, s, e, mode) {
      this.value = this.value.slice(0, s) + r + this.value.slice(e);
      if (mode === 'end') this.selectionStart = this.selectionEnd = s + r.length;
    },
    setSelectionRange(s, e) { this.selectionStart = s; this.selectionEnd = e; }
  };
}
const ta = textarea();
let changed = 0, toasts = [], kind = 'md';
const code = [
  'var bodyEditor=ta;', pickBlock('function getWikiContext(', 'function wikiCandidates'),
  pick('function insertAtCursor('), pick('function v21IndentSelection('), pick('function v21FormatCurrent('),
  'var currentFile={};'
].join('\n');
const sb = { ta, markChanged() { changed++; }, toast(m) { toasts.push(m); }, v21Kind() { return kind; }, JSON, String, Array, Math };
runInNewContext(code, sb, { filename: 'app.js#editor' });

// slash menu: ITEMS and fold from visual-tools.js
const itemsSrc = vt.slice(vt.indexOf('var ITEMS=['), vt.indexOf('];', vt.indexOf('var ITEMS=[')) + 2);
const foldSrc = vt.slice(vt.indexOf('function fold('), vt.indexOf('\n', vt.indexOf('function fold(')));
const slashSb = {}; runInNewContext(itemsSrc + '\n' + foldSrc, slashSb);
const slashRe = /^\/([^\s\/]{0,24})$/;
let md = null;
let mdFlanking = null;
const out = cases.map(c => {
  ta.value = c.value ?? ''; ta.selectionStart = c.start ?? c.caret ?? 0; ta.selectionEnd = c.end ?? c.caret ?? 0;
  switch (c.op) {
    case 'insert': sb.insertAtCursor(c.prefix, c.suffix ?? ''); return { value: ta.value, start: ta.selectionStart, end: ta.selectionEnd };
    case 'wiki': return sb.getWikiContext();
    case 'commitWiki': { const x = sb.getWikiContext(); if (!x) return null; ta.setRangeText('[[' + c.name + ']]', x.start, ta.selectionStart, 'end'); return { value: ta.value, start: ta.selectionStart, end: ta.selectionEnd }; }
    case 'indent': sb.v21IndentSelection(); return { value: ta.value, start: ta.selectionStart, end: ta.selectionEnd };
    case 'format': kind = c.ext; toasts = []; changed = 0; sb.v21FormatCurrent(); return { value: ta.value, changed, toast: toasts[0] ?? null };
    case 'stats': { const txt = c.value.trim(); return { lines: c.value.split('\n').length, words: txt ? txt.split(/\s+/).length : 0 }; }
    case 'slash': { const q = slashSb.fold(c.query); return slashSb.ITEMS.filter(x => !q || slashSb.fold(x.label + ' ' + x.keys).includes(q)).map(x => x.act); }
    case 'slashQuery': { const m = c.text.replace(/​/g, '').match(slashRe); return m ? m[1] : null; }
    case 'markdown': {
      if (!md) { const w = {}; w.window = w; runInNewContext(readFileSync(`${src}/editor/markdown.js`, 'utf8'), w, { filename: 'markdown.js' }); md = w.UrbeMarkdown; }
      return md.render(c.md);
    }
    // CST-128: the same markdown.js with only inlineEmphasis following the CommonMark flanking rules (an opener is
    // followed by a non-space, a closer preceded by one, "_" never inside a word). Everything else stays the original's.
    case 'markdownFlanking': {
      if (!mdFlanking) {
        const original = readFileSync(`${src}/editor/markdown.js`, 'utf8');
        const from = original.indexOf('function inlineEmphasis(t){'), to = original.indexOf('function safeUrl(u){');
        if (from < 0 || to < from) throw new Error('inlineEmphasis not found in markdown.js');
        const flanking = `function inlineEmphasis(t){
  return t.replace(/\\*\\*(?!\\s)([^*\\n]+?)(?<!\\s)\\*\\*/g,'<strong>$1</strong>')
          .replace(/(?<![\\p{L}\\p{N}_])__(?!\\s)([^_\\n]+?)(?<!\\s)__(?![\\p{L}\\p{N}_])/gu,'<strong>$1</strong>')
          .replace(/(?<!\\*)\\*(?![\\s*])([^*\\n]+?)(?<![\\s*])\\*(?!\\*)/g,'<em>$1</em>')
          .replace(/(?<![\\p{L}\\p{N}_])_(?![\\s_])([^_\\n]+?)(?<![\\s_])_(?![\\p{L}\\p{N}_])/gu,'<em>$1</em>')
          .replace(/~~(?!\\s)([^~\\n]+?)(?<!\\s)~~/g,'<del>$1</del>');
}
`;
        const w = {}; w.window = w;
        runInNewContext(original.slice(0, from) + flanking + original.slice(to), w, { filename: 'markdown.js' });
        mdFlanking = w.UrbeMarkdown;
      }
      return mdFlanking.render(c.md);
    }
    default: throw new Error('op ' + c.op);
  }
});
process.stdout.write(JSON.stringify(out));
