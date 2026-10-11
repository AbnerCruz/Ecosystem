// UC-19 oracle for the "Nova nota" names: the UNMODIFIED 1.8.4-beta artifacts.js safeName and
// the final app.js v21ExtFromName, v21Stem, v21UniqueName, extNota and separarNomeExtNota,
// evaluated in Node for each typed name against a neighbourhood that already has some notes.
// Prints [{typed, unique, name, ext}]. Test-only: JavaScript never runs inside the native app.
//   node note-name-oracle.mjs <apps/urbe/src> '<json array of typed names>' '<json array of taken file names>'
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const [src, typedJson, takenJson] = process.argv.slice(2);
const ctx = { window: {}, Math, String, RegExp, Set, Array, Object, JSON };
ctx.window.window = ctx.window;
runInNewContext(readFileSync(`${src}/core/artifacts.js`, 'utf8'), ctx.window, { filename: 'artifacts.js' });
const app = readFileSync(`${src}/app.js`, 'utf8');
const pick = (start) => { const i = app.indexOf(start); if (i < 0) throw new Error('not found: ' + start); return app.slice(i, app.indexOf('\n', i)); };
const pickBlock = (start, endMarker) => { const i = app.indexOf(start); const j = app.indexOf(endMarker, i); return app.slice(i, j); };
const code = [
  'var nomeSeguro=window.UrbeArtifacts.safeName;',
  'var V21_EXT_RE=window.UrbeArtifacts.RE.text;',
  pickBlock('function v21ExtFromName(', 'function v21Stem('),
  pick('function v21Stem('),
  pickBlock('function v21UniqueName(', 'function v21FileAccent('),
  'var extNota,separarNomeExtNota,nomeCompletoNota;',
  pick('extNota=function(b)'), pick('nomeCompletoNota=function(b)'), pick('separarNomeExtNota=function(valor,atual)')
].join('\n');
const typed = JSON.parse(typedJson), taken = JSON.parse(takenJson);
const world = { buildings: taken.map(f => { const m = f.match(/^(.*?)(\.[^.]+)$/); return { tipo: 'nota', regionId: 'r1', name: m ? m[1] : f, ext: m ? m[2] : '.md' }; }) };
const sandbox = Object.assign(ctx.window, { world });
runInNewContext(code, sandbox, { filename: 'app.js#names' });
const out = typed.map(t => { const unique = sandbox.v21UniqueName(t, 'r1', null); const p = sandbox.separarNomeExtNota(unique, { ext: '.md' }); return { typed: t, unique, name: p.name, ext: p.ext }; });
process.stdout.write(JSON.stringify(out));
