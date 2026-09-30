// Abertura e migração dos vaults históricos (REQ-037, REQ-035, REQ-023): nenhuma nota é alterada, IDs e recuperação preservados,
// e as lacunas conhecidas da 1.8.2 (proteção forward) são rastreadas até serem corrigidas.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createEnv, readFixture, readExpect } from './helpers/vault-env.mjs';

const sha = (s) => createHash('sha256').update(s).digest('hex');
const NAMES = ['v1-mapa-v2', 'v1-mapa-v4', 'v1-notas-sem-id', 'v1-journal-pendente', 'v1-cidades-mescladas', 'v1-personalizacao', 'v1-paginas', 'v1-mundo-antigo', 'futuro-desconhecido'];

/* Lacunas conhecidas (comportamento 1.8.2) → item do ROADMAP que as fecha. Quando uma passa a ser satisfeita, o teste FALHA
   pedindo a remoção da linha (catraca: a lista só encolhe). */
export const KNOWN_GAPS = new Map([
  ['futuro-desconhecido: history/trash/compositions/journal de versão maior são preservados', 'RM-F1-05 (REQ-035)'],
]);
const seenGaps = new Set();
function gap(name, fn) {
  let ok = true; try { fn(); } catch { ok = false; }
  if (!KNOWN_GAPS.has(name)) { fn(); return; }
  seenGaps.add(name);
  if (ok) throw new Error(`lacuna fechada: "${name}" agora passa — remova de KNOWN_GAPS (${KNOWN_GAPS.get(name)})`);
  console.log(`gap conhecido (${KNOWN_GAPS.get(name)}): ${name}`);
}

for (const name of NAMES) {
  const files = readFixture(name), expect = readExpect(name);
  const env = createEnv(files);
  const { p, docs } = env;
  const loaded = await p.load('V');
  const paths = Array.from(docs.list(), (d) => d.path).sort();
  assert.deepEqual(paths, [...expect.notes].sort(), `${name}: documentos carregados`);

  // IDs: os do mapa/journal são preservados; sem ID no mapa, gera-se doc_<uuid> (novo a cada carga — R-2)
  for (const [rel, want] of Object.entries(expect.ids || {})) assert.equal(docs.get(rel).id, want, `${name}: ID de ${rel}`);
  if (expect.generatedIds) {
    for (const d of docs.list()) assert.match(d.id, /^doc_[0-9a-f-]{36}$/, `${name}: ID gerado`);
    const again = createEnv(files); await again.p.load('V');
    assert.notEqual(again.docs.get('Alfa.md').id, docs.get('Alfa.md').id, `${name}: R-2 — ID novo a cada carga sem mapa (comportamento 1.8.2)`);
  }
  // posições espaciais vindas do mapa
  if (expect.positions) {
    env.world.load(loaded.metadata);
    for (const [rel, [x, y]] of Object.entries(expect.positions)) { const pr = env.world.projectDocument(docs.get(rel).id); assert.deepEqual([pr.x, pr.y], [x, y], `${name}: posição de ${rel}`); }
  }
  // side-files v1
  if (expect.trash != null) assert.equal(env.trash.list().length, expect.trash, `${name}: itens da lixeira`);
  if (expect.history != null) assert.equal(env.history.export().documents ? Object.keys(env.history.export().documents).length : 0, expect.history, `${name}: histórico`);
  if (expect.compositions != null) assert.equal(env.compositions.export().items.length, expect.compositions, `${name}: composições`);
  // recuperação de journal
  if (expect.recovered) {
    for (const [rel, c] of Object.entries(expect.contents)) assert.equal(docs.get(rel).content, c, `${name}: conteúdo recuperado de ${rel}`);
    assert.equal(env.has('.urbe/journal.json'), false, `${name}: journal removido após recuperar`);
  }

  // nada nas notas muda após um ciclo de escrita (sem edição): bytes idênticos
  await p.flush();
  if (!expect.recovered && !expect.futureMapa) {
    for (const [rel, h] of Object.entries(expect.noteHashes)) if (!expect.recovered) assert.equal(sha(env.get(rel) ?? ''), h, `${name}: bytes de ${rel} inalterados`);
  }
  // arquivos que a migração multi-cidade deixa (nunca apaga origem)
  for (const [rel, h] of Object.entries(expect.keepHashes || {})) assert.equal(sha(env.get(rel) ?? ''), h, `${name}: ${rel} preservado`);

  // proteção forward (lacunas na 1.8.2)
  if (name === 'futuro-desconhecido') {
    // uma edição obriga o flush a reescrever os arquivos .urbe
    docs.upsert({ ...docs.get('Alfa.md'), content: docs.get('Alfa.md').content + '\nedição' });
    await p.flush();
    gap('futuro-desconhecido: history/trash/compositions/journal de versão maior são preservados', () => {
      for (const rel of ['.urbe/history.json', '.urbe/trash.json', '.urbe/compositions.json', '.urbe/journal.json']) assert.equal(sha(env.get(rel) ?? ''), expect.futureHashes[rel], `${rel} alterado/apagado`);
    });
    // o mapa v99 sobrevive à camada de persistência; quem o reescreve é o serializador legado de app.js (E2E: futuro.e2e, RM-F1-06/13)
    assert.equal(JSON.parse(env.get('.urbe/mapa.json')).v, 99, 'mapa v99 preservado pela persistência');
    // tema e página futuros são documentos comuns: os bytes só mudariam se o app os normalizasse (RM-F1-06/23)
    for (const rel of ['Personalização/tema.json', 'Páginas/Futura.page.json']) assert.equal(sha(env.get(rel) ?? ''), expect.futureHashes[rel], `${rel}: bytes preservados pela persistência`);
  }
}
for (const g of KNOWN_GAPS.keys()) assert.ok(seenGaps.has(g), 'lacuna listada mas não exercitada: ' + g);
console.log('vault-migration: ok');
