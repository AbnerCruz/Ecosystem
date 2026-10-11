// Oracle for the "Importar" selection rules: the UNMODIFIED 1.8.4 src/persistence/import.js
// inspect() over non-ZIP selections [{rel, text|b64}] (path validation, duplicate/conflict
// handling, UTF-8, kind and counts). Prints [{ok, error?, kind, documents, assets, files:[path],
// conflicts:[{path, candidates:[path]}]}]. Test-only: JavaScript never runs inside the native app.
//   node import-oracle.mjs <apps/urbe/src> '<json array of selections>' '<directory true|false>'
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { webcrypto } from 'node:crypto';

const [src, selectionsJson, directory] = process.argv.slice(2);
const global = { crypto: webcrypto, TextDecoder, Uint8Array, Map, Set, Promise, Error, JSON, Math, Number, Array, Object, RegExp, String, Date, console };
global.window = global;
for (const f of ['core/artifacts.js', 'persistence/export-manifest.js', 'persistence/import.js'])
  runInNewContext(readFileSync(`${src}/${f}`, 'utf8'), global, { filename: f });
const out = [];
for (const selection of JSON.parse(selectionsJson)) {
  const entries = selection.map(e => {
    const bytes = e.b64 ? new Uint8Array(Buffer.from(e.b64, 'base64')) : new Uint8Array(Buffer.from(e.text, 'utf8'));
    return { rel: e.rel, file: { name: e.rel.split('/').pop(), size: bytes.length, arrayBuffer: async () => bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.length) } };
  });
  try {
    const i = await global.UrbeImport.inspect(entries, { directory: directory === 'true' });
    out.push({ ok: true, kind: i.kind, documents: i.documents, assets: i.assets, files: [...i.files.keys()],
      conflicts: i.sourceConflicts.map(c => ({ path: c.path, candidates: c.candidates.map(x => x.path) })) });
  } catch (e) { out.push({ ok: false, error: String(e.message) }); }
}
process.stdout.write(JSON.stringify(out));
