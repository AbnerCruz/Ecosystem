// Manifesto de módulos (src/modules.json) e análise estática leve dos scripts (REQ-025, REQ-015).
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROOT = process.env.URBE_ROOT || join(dirname(fileURLToPath(import.meta.url)), '..', '..');
export const rd = (f) => readFileSync(join(ROOT, f), 'utf8');
export const MANIFEST = 'src/modules.json';

export const PHASES = ['boot', 'core', 'feature', 'app', 'late'];
// Camadas (SPEC §3.1): as dependências apontam para baixo — app → ui → features → persistence → core/kit/native → vendor.
export const LAYERS = { vendor: 0, kit: 1, core: 1, native: 1, persistence: 2, feature: 3, ui: 4, app: 5 };
export const BOUNDARY_EXCEPTIONS_FILE = 'docs/v2/discovery/BOUNDARY-EXCEPTIONS.md';

const dir2 = (f) => f.split('/')[1];
/** Violações de camada/boundary de um manifesto: [{from,to,why}] (dependência = requires ∪ uses). */
export function boundaryViolations(manifest) {
  const byFile = new Map(manifest.modules.map((m) => [m.file, m]));
  const out = [];
  for (const m of manifest.modules) {
    for (const d of [...(m.requires || []), ...(m.uses || [])]) {
      const t = byFile.get(d); if (!t || t.file === m.file) continue;
      let why = null;
      if (LAYERS[t.layer] > LAYERS[m.layer]) why = `camada ${m.layer} depende de ${t.layer}`;
      else if (m.layer === 'native' && ['persistence', 'feature', 'ui', 'app'].includes(t.layer)) why = `native depende de ${t.layer}`;
      else if (['world', 'pages'].includes(dir2(m.file)) && dir2(t.file) === 'ai' && !/\/ai-tools\.js$/.test(m.file) && m.file !== 'src/pages/studio.js') why = `${dir2(m.file)} depende de ai`;
      else if (['world', 'pages'].includes(dir2(m.file)) && dir2(t.file) === 'ai' && m.file === 'src/pages/studio.js') why = 'pages depende de ai';
      if (why) out.push({ from: m.file, to: t.file, why });
    }
  }
  return out;
}

/** Exceções conhecidas (tabela em BOUNDARY-EXCEPTIONS.md: | `from` | `to` | motivo |). */
export function boundaryExceptions() {
  let md = ''; try { md = rd(BOUNDARY_EXCEPTIONS_FILE); } catch { return []; }
  return [...md.matchAll(/^\|\s*`([^`]+)`\s*\|\s*`([^`]+)`\s*\|/gm)].map((m) => ({ from: m[1], to: m[2] }));
}

const GLOBAL_ASSIGN = /(?:\bglobal|\bwindow|\broot|\bself|\bglobalThis)\.((?:Urbe\w*)|JSZip|katex|URBE)\s*=[^=]/g;
const GLOBAL_REF = /\b(Urbe[A-Z]\w*|JSZip|katex|URBE)\b/g;
const SERVICE_PROVIDE = /\bprovide\(\s*['"]([\w.\-]+)['"]/g;
const SERVICE_REF = /\b(?:service|hasService)\(\s*['"]([\w.\-]+)['"]\s*\)/g;

const uniq = (a) => [...new Set(a)].sort();
const all = (re, s, g = 1) => [...s.matchAll(re)].map((m) => m[g]);

/** Lê provides/refs de um script. */
export function scan(file) {
  const src = rd(file);
  return {
    globals: uniq(all(GLOBAL_ASSIGN, src)),
    services: uniq(all(SERVICE_PROVIDE, src)),
    refGlobals: uniq(all(GLOBAL_REF, src)),
    refServices: uniq(all(SERVICE_REF, src)),
  };
}

export function loadManifest() { return JSON.parse(rd(MANIFEST)); }

export function walk(dir) {
  const out = [];
  for (const e of readdirSync(join(ROOT, dir), { withFileTypes: true })) {
    const rel = dir + '/' + e.name;
    if (e.isDirectory()) out.push(...walk(rel)); else out.push(rel);
  }
  return out;
}

export const BEGIN = { html_styles: '<!-- urbe:styles:begin -->', html_scripts: '<!-- urbe:scripts:begin -->', sw: '/* urbe:shell:begin */' };
export const END = { html_styles: '<!-- urbe:styles:end -->', html_scripts: '<!-- urbe:scripts:end -->', sw: '/* urbe:shell:end */' };

/** Trechos derivados do manifesto. */
export function derive(m) {
  const styles = m.styles.map((f) => `<link rel="stylesheet" href="./${f}">`).join('\n');
  const scripts = m.modules.map((x) => `<script src="./${x.file}"></script>`).join('\n');
  const shell = ['./', './index.html', ...m.styles, ...m.modules.map((x) => x.file), ...m.assets].map((f) => (f.startsWith('./') ? f : './' + f));
  const swLines = shell.map((f) => `  '${f}',`).join('\n');
  return { styles, scripts, swLines };
}

export function between(text, begin, end) {
  const i = text.indexOf(begin), j = text.indexOf(end);
  if (i < 0 || j < 0 || j < i) return null;
  return { i: i + begin.length, j, body: text.slice(i + begin.length, j) };
}

export function replaceBetween(text, begin, end, body) {
  const b = between(text, begin, end);
  if (!b) throw new Error(`marcadores ausentes: ${begin} … ${end}`);
  return text.slice(0, b.i) + '\n' + body + '\n' + text.slice(b.j);
}

export { existsSync, statSync };
