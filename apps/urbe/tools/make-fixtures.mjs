#!/usr/bin/env node
// Gera tests/fixtures/vaults/* — vaults históricos determinísticos (REQ-037, RM-F1-01).
// Cada fixture é uma pasta de vault real + expect.json (invariantes verificados por tests/vault-migration.mjs).
//   node tools/make-fixtures.mjs           → (re)escreve as fixtures
//   node tools/make-fixtures.mjs --check   → falha se o conteúdo em disco divergir do gerador
import { mkdirSync, writeFileSync, rmSync, readFileSync, existsSync, readdirSync, statSync } from 'node:fs';
import { join, dirname, relative } from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const OUT = join(ROOT, 'tests/fixtures/vaults');
const sha = (s) => createHash('sha256').update(s).digest('hex');
const J = (o, indent = 1) => JSON.stringify(o, null, indent);
const id = (n) => `doc_${String(n).padStart(8, '0')}-0000-4000-8000-000000000000`;

const NOTA_A = '# Alfa\n\nPrimeira nota com [[Beta]] e #tag1.\n';
const NOTA_B = '# Beta\n\nSegunda nota, volta para [[Alfa]].\n';
const NOTA_C = '# Gama\n\nNota numa pasta.\n';
const camera = { x: 120, y: 90, z: 1 };

function mapaV4({ withIds = true, mundo = 'placas-1', version = '1.8.2-beta' } = {}) {
  const nota = (n, x, y, extra = {}) => ({ ...(withIds ? { id: id(n) } : {}), x, y, sprite: 'house1', tags: [], anexos: [], criado: '2026-09-01', modificado: '2026-09-02', aiLocal: null, ...extra });
  return { v: 4, app: 'Urbe', ...(version ? { version } : {}), ...(mundo ? { mundo } : {}), salvo: '2026-09-02T10:00:00.000Z', camera,
    regioes: [{ caminho: 'Pasta', nome: 'Pasta', cor: '#4aa3ff', x: 30, y: 30, w: 12, h: 10, cells: null, descricao: '' }],
    notas: { 'Alfa.md': nota(1, 32, 32), 'Beta.md': nota(2, 36, 33), 'Pasta/Gama.md': nota(3, 34, 36, { tags: ['pasta'] }) },
    construcoes: [{ tipo: 'arquivo', fileClass: 'image', name: 'logo.png', fileName: 'logo.png', caminho: 'Pasta', x: 38, y: 32, w: 3, h: 3, description: '', sprite: 'file-image', parentNoteName: 'Alfa', files: [{ nome: 'logo.png', tipo: 'png', mime: 'image/png', relPath: 'Pasta/logo.png', folderPath: 'Pasta', tamanho: 8 }], anexos: [], created: '2026-09-01', modified: '2026-09-01' }] };
}
const PNG = '\x89PNG\r\n\x1a\n';
const historyV1 = { version: 1, documents: { [id(1)]: [{ timestamp: 1790000000000, path: 'Alfa.md', content: '# Alfa\n\nversão antiga\n' }] } };
const trashV1 = { version: 1, items: [{ document: { id: id(9), path: 'Velha.md', title: 'Velha', content: '# Velha\n', properties: {}, tags: [], links: [], created: '2026-08-01', modified: '2026-08-02', revision: 1 }, deletedAt: 1789000000000, originalPath: 'Velha.md' }] };
const compositionsV1 = { version: 1, items: [{ id: 'cmp_00000001', name: 'Junção', type: 'document', sources: [id(1), id(2)], theme: 'claro', styles: {}, overrides: {}, customCSS: '', htmlSource: '', order: [id(1), id(2)], created: '2026-09-01', modified: '2026-09-02' }] };
const pageV1 = (title) => ({ version: 1, kind: 'urbe-page', meta: { title, lang: 'pt-BR' }, theme: { id: 'claro' }, layout: { format: 'page' }, sections: [{ id: 's1', type: 'hero', props: { title }, style: {} }] });
const temaV1 = { $schema: 'urbe-tema-1', versao: 1, tema: 'escuro', cores: { fundo: '#101418' }, estilos: { 'Personalização/estilos/meu.css': true }, css: '' };
const PLUGIN = "urbe.plugin({nome:'Olá',versao:'1.0.0',descricao:'exemplo',ligar:function(api){api.log('ligado')},desligar:function(){}});\n";

/** nome → { files: {path: string}, expect: {...} } */
export const FIXTURES = {
  'v1-mapa-v2': {
    files: { 'Alfa.md': NOTA_A, 'Beta.md': NOTA_B, '.urbe/mapa.json': J({ v: 2, app: 'Urbe', camera, regioes: [], notas: { 'Alfa.md': { x: 10, y: 10, sprite: 'house1', tags: [], anexos: [], criado: '2026-08-01', modificado: '2026-08-02' }, 'Beta.md': { x: 14, y: 10, sprite: 'house2', tags: [], anexos: [], criado: '2026-08-01', modificado: '2026-08-02' } }, construcoes: [] }) },
    expect: { notes: ['Alfa.md', 'Beta.md'], ids: {}, positions: { 'Alfa.md': [10, 10], 'Beta.md': [14, 10] } },
  },
  'v1-mapa-v4': {
    files: { 'Alfa.md': NOTA_A, 'Beta.md': NOTA_B, 'Pasta/Gama.md': NOTA_C, 'Pasta/logo.png': PNG, '.urbe/mapa.json': J(mapaV4()), '.urbe/history.json': JSON.stringify(historyV1), '.urbe/trash.json': J(trashV1), '.urbe/compositions.json': JSON.stringify(compositionsV1) },
    expect: { notes: ['Alfa.md', 'Beta.md', 'Pasta/Gama.md'], ids: { 'Alfa.md': id(1), 'Beta.md': id(2), 'Pasta/Gama.md': id(3) }, positions: { 'Alfa.md': [32, 32], 'Pasta/Gama.md': [34, 36] }, trash: 1, history: 1, compositions: 1 },
  },
  'v1-notas-sem-id': {
    files: { 'Alfa.md': NOTA_A, 'Beta.md': NOTA_B, '.urbe/mapa.json': J(mapaV4({ withIds: false })) },
    expect: { notes: ['Alfa.md', 'Beta.md'], ids: {}, generatedIds: true, positions: { 'Alfa.md': [32, 32] } },
  },
  'v1-journal-pendente': {
    files: { 'Alfa.md': 'conteúdo antigo em disco\n', '.urbe/mapa.json': J(mapaV4()), '.urbe/journal.json': JSON.stringify({ version: 1, timestamp: 1790000100000, documents: [{ id: id(1), path: 'Alfa.md', content: '# Alfa\n\nrecuperada do journal\n', tags: [], created: '2026-09-01', modified: '2026-09-02' }, { id: id(4), path: 'Delta.md', content: '# Delta\n', tags: [], created: '2026-09-02', modified: '2026-09-02' }], metadata: mapaV4(), trash: null, history: null, compositions: null }) },
    expect: { recovered: true, notes: ['Alfa.md', 'Delta.md'], contents: { 'Alfa.md': '# Alfa\n\nrecuperada do journal\n' }, ids: { 'Alfa.md': id(1) }, journalRemoved: true },
  },
  'v1-cidades-mescladas': {
    files: { 'Cidades/Norte/Um.md': '# Um\n', 'Cidades/Sul/Dois.md': '# Dois\n', '.urbe/merged-v1.json': J({ Norte: { folder: 'Cidades/Norte', importedAt: '2026-09-10T00:00:00.000Z' }, Sul: { folder: 'Cidades/Sul', importedAt: '2026-09-10T00:00:00.000Z' } }), '.urbe/origens/Norte.json': J({ v: 4, notas: {} }), '.urbe/origens/Sul.json': J({ v: 4, notas: {} }), '.urbe/mapa.json': J(mapaV4({ withIds: false, mundo: null, version: null })) },
    expect: { notes: ['Cidades/Norte/Um.md', 'Cidades/Sul/Dois.md'], ids: {}, keepFiles: ['.urbe/merged-v1.json', '.urbe/origens/Norte.json', '.urbe/origens/Sul.json'] },
  },
  'v1-personalizacao': {
    files: { 'Alfa.md': NOTA_A, 'Personalização/tema.json': J(temaV1, 2), 'Personalização/temas/noite.json': J({ nome: 'Noite', claro: false, base: 'escuro', cores: { fundo: '#000' } }, 2), 'Personalização/estilos/meu.css': 'body{--x:1}\n', 'Personalização/texturas/grama.json': J({ nome: 'Grama', chao: {}, construcoes: {} }, 2), 'Personalização/plugins/ola.js': PLUGIN, '.urbe/mapa.json': J(mapaV4()) },
    expect: { notes: ['Alfa.md', 'Personalização/estilos/meu.css', 'Personalização/plugins/ola.js', 'Personalização/temas/noite.json', 'Personalização/texturas/grama.json', 'Personalização/tema.json'], keepFiles: [] },
  },
  'v1-paginas': {
    files: { 'Páginas/Inicio.page.json': J(pageV1('Início'), 2), 'Páginas/Modelos/Blog.template.json': J({ ...pageV1('Blog'), kind: 'urbe-template', template: { name: 'Blog', description: 'modelo' } }, 2), 'Páginas/Blocos/Rodape.block.json': J({ kind: 'urbe-block', name: 'Rodapé', description: '', section: { type: 'text', props: {}, style: {} } }, 2), '.urbe/mapa.json': J(mapaV4()) },
    expect: { notes: ['Páginas/Inicio.page.json', 'Páginas/Modelos/Blog.template.json', 'Páginas/Blocos/Rodape.block.json'] },
  },
  'v1-mundo-antigo': {
    files: { 'Alfa.md': NOTA_A, 'Beta.md': NOTA_B, '.urbe/mapa.json': J(mapaV4({ mundo: 'placas-0', version: '1.5.0-beta' })) },
    expect: { notes: ['Alfa.md', 'Beta.md'], ids: { 'Alfa.md': id(1), 'Beta.md': id(2) }, mundo: 'placas-0' },
  },
  'futuro-desconhecido': {
    files: { 'Alfa.md': NOTA_A,
      '.urbe/mapa.json': J({ ...mapaV4(), v: 99 }),
      '.urbe/history.json': JSON.stringify({ version: 99, documents: { [id(1)]: [{ timestamp: 1, path: 'Alfa.md', content: 'futuro' }] }, campoFuturo: true }),
      '.urbe/trash.json': J({ version: 99, items: [], campoFuturo: true }),
      '.urbe/compositions.json': JSON.stringify({ version: 99, items: [{ id: 'cmp_futuro', novoCampo: 1 }] }),
      '.urbe/journal.json': JSON.stringify({ version: 99, timestamp: 1, documents: [{ id: id(1), path: 'Alfa.md', content: 'NÃO USAR', tags: [], created: '', modified: '' }], campoFuturo: true }),
      'Personalização/tema.json': J({ ...temaV1, versao: 99, campoFuturo: 'x' }, 2),
      'Páginas/Futura.page.json': J({ ...pageV1('Futura'), version: 99, campoFuturo: { a: 1 } }, 2) },
    expect: { notes: ['Alfa.md', 'Personalização/tema.json', 'Páginas/Futura.page.json'], futureFiles: ['.urbe/history.json', '.urbe/trash.json', '.urbe/compositions.json', '.urbe/journal.json', 'Personalização/tema.json', 'Páginas/Futura.page.json'], futureMapa: true, ids: { 'Alfa.md': id(1) } },
  },
};
// fixture 1 do IDB legado (mapa v1: só existia em IndexedDB kv["cidade"]) — usada pelo teste de migrarAntiga
export const IDB_LEGADO = { v: 1, camera, regions: [{ id: 'r1', name: 'Pasta', color: '#4aa3ff', x: 30, y: 30, w: 12, h: 10, cells: null }], buildings: [{ id: 'b1', regionId: 'r1', name: 'Alfa', x: 32, y: 32, w: 3, h: 3, content: NOTA_A, sprite: 'house1', tipo: 'nota', tags: [], created: '2026-08-01', modified: '2026-08-02' }] };

function render() {
  const out = new Map();
  for (const [name, f] of Object.entries(FIXTURES)) {
    for (const [p, c] of Object.entries(f.files)) out.set(`${name}/${p}`, c);
    const notes = Object.fromEntries(Object.entries(f.files).filter(([p]) => !p.startsWith('.urbe/')).map(([p, c]) => [p, sha(c)]));
    const future = Object.fromEntries((f.expect.futureFiles || []).map((p) => [p, sha(f.files[p])]));
    const keep = Object.fromEntries((f.expect.keepFiles || []).map((p) => [p, sha(f.files[p])]));
    out.set(`${name}/expect.json`, J({ ...f.expect, noteHashes: notes, futureHashes: future, keepHashes: keep }, 2) + '\n');
  }
  out.set('idb-legado-kv-cidade.json', J(IDB_LEGADO, 2) + '\n');
  out.set('README.md', `# Fixtures de vaults históricos

Geradas por \`node tools/make-fixtures.mjs\` (REQ-037). Cada pasta é um vault; \`expect.json\` lista os invariantes que \`tests/vault-migration.mjs\` verifica
(bytes das notas, IDs, arquivos de versão futura, recuperação de journal). \`idb-legado-kv-cidade.json\` é o formato v1 que só existia no IndexedDB (\`kv["cidade"]\`).

| Fixture | Cobre |
|---|---|
| v1-mapa-v2 | mapa v:2 (sem id, sem mundo/version), leitura defensiva |
| v1-mapa-v4 | mapa v4 completo (ids, regiões, construções/assets), history/trash/compositions v1 |
| v1-notas-sem-id | notas sem \`id\` no mapa (R-2: ID novo a cada carga) |
| v1-journal-pendente | journal.json v1 de uma operação interrompida (recuperação) |
| v1-cidades-mescladas | migração multi-cidade (\`Cidades/\`, \`.urbe/origens/\`, \`merged-v1.json\`) |
| v1-personalizacao | tema.json, temas, estilos, texturas, plugins |
| v1-paginas | páginas, modelos e blocos v1 |
| v1-mundo-antigo | mapa com \`mundo\` antigo (dispara reorganização na 1.8.2) |
| futuro-desconhecido | versões maiores em todos os formatos (proteção forward, R-1/R-14) |
`);
  return out;
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const rendered = render();
  if (process.argv.includes('--check')) {
    let bad = 0;
    for (const [p, c] of rendered) { const f = join(OUT, p); if (!existsSync(f) || readFileSync(f, 'utf8') !== c) { console.error('divergente/ausente: ' + p); bad++; } }
    const walk = (d) => readdirSync(d).flatMap((e) => (statSync(join(d, e)).isDirectory() ? walk(join(d, e)) : [relative(OUT, join(d, e))]));
    for (const p of existsSync(OUT) ? walk(OUT) : []) if (!rendered.has(p.replaceAll('\\', '/'))) { console.error('arquivo extra: ' + p); bad++; }
    if (bad) process.exit(1);
    console.log(`OK: ${rendered.size} arquivos de fixture em dia`);
  } else {
    rmSync(OUT, { recursive: true, force: true });
    for (const [p, c] of rendered) { const f = join(OUT, p); mkdirSync(dirname(f), { recursive: true }); writeFileSync(f, c); }
    console.log(`${rendered.size} arquivos em ${relative(ROOT, OUT)}`);
  }
}
