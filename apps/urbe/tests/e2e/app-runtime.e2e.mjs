// RM-F2-02: o harness de produção cobre boot, abrir fixture, criar nota e mundo — sem ler o texto de `src/app.js`.
import assert from 'node:assert/strict';
import { startRuntime, fixtureExpect } from './app-runtime.mjs';
import { makeVault } from '../../tools/perf/make-vault.mjs';

const rt = await startRuntime();
try {
  // 1) boot: primeira abertura sem vault cria o Tutorial; título e versão coerentes
  const first = await rt.open({ docs: 40 });
  const v = await first.version();
  assert.equal(await first.page.title(), 'Urbe v' + v);
  assert.ok(Object.keys(await first.notes()).filter((p) => p.startsWith('Tutorial/')).length >= 40, 'Tutorial criado');
  first.expectNoErrors();

  // 2) abrir vault fixture histórico: todas as notas esperadas carregam e viram casas no mundo
  const name = 'v1-mapa-v4', expect = fixtureExpect(name);
  const a = await rt.open({ fixture: name });
  const notes = await a.notes();
  for (const rel of expect.notes) assert.ok(rel in notes, `fixture: ${rel} carregado`);
  const world = await a.world();
  for (const rel of expect.notes) assert.ok(world.buildings.some((b) => b.path === rel), `mundo: casa de ${rel}`);

  // 3) criar nota: comando canônico, gravada no vault, com id e posição no mundo; sobrevive ao recarregamento
  const id = await a.createNote('Harness/Nota nova.md', '# Nota nova\n\nCriada pelo harness.\n');
  assert.ok(id, 'id da nota');
  assert.match((await a.vault())['Harness/Nota nova.md'] || '', /Criada pelo harness/, 'nota gravada no vault');
  assert.ok((await a.world()).buildings.some((b) => b.id === id), 'nota projetada no mundo');
  await a.reload({ docs: expect.notes.length + 1 });
  assert.equal((await a.notes())['Harness/Nota nova.md']?.id, id, 'mesmo id após recarregar');
  a.expectNoErrors();

  // 4) vault semeado (S): escala maior abre sem erro
  const seed = makeVault('S');
  const s = await rt.open({ seed });
  assert.ok(Object.keys(await s.notes()).length >= seed.notes);
  s.expectNoErrors();
  console.log('app-runtime.e2e: ok');
} finally { await rt.close(); }
