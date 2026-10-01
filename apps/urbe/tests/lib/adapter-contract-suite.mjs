// Suíte única de conformidade do adaptador de persistência (docs/v2/contracts/persistence-adapter.md, REQ-028).
// `suite` é autocontida (sem imports): roda em Node e é serializada (`suiteSource`) para rodar dentro do navegador.
async function suite(adapter, opts) {
  opts = opts || {};
  const vault = opts.vault || 'suite-c1', names = [];
  const eq = (a, b, m) => { if (JSON.stringify(a) !== JSON.stringify(b)) throw new Error((m || 'diferente') + ': ' + JSON.stringify(a) + ' ≠ ' + JSON.stringify(b)); };
  const ok = (v, m) => { if (!v) throw new Error(m || 'falhou'); };
  const t = async (name, fn) => { try { await fn(); names.push('ok  ' + name); } catch (e) { names.push('FAIL ' + name + ' — ' + (e && e.message || e)); } };
  const bytes = async (blob) => Array.from(new Uint8Array(await blob.arrayBuffer()));
  for (const m of ['cities', 'createCity', 'removeCity', 'list', 'read', 'readBlob', 'write', 'writeBlob', 'remove', 'createFolder', 'removeFolder']) if (typeof adapter[m] !== 'function') throw new Error('adaptador sem ' + m);

  if (opts.manageCities !== false) await t('cidades: criar, listar, apagar', async () => {
    await adapter.createCity(vault);
    ok((await adapter.cities()).includes(vault), 'cities() inclui a cidade criada');
    await adapter.createCity(vault); // idempotente
  });
  await t('texto: gravar, ler, sobrescrever; ausente = null', async () => {
    await adapter.write(vault, 'a.md', 'olá\n');
    eq(await adapter.read(vault, 'a.md'), 'olá\n');
    await adapter.write(vault, 'a.md', 'nova versão');
    eq(await adapter.read(vault, 'a.md'), 'nova versão');
    eq(await adapter.read(vault, 'nao-existe.md'), null);
    eq(await adapter.read(vault, 'Pasta/nao-existe.md'), null);
  });
  await t('pastas intermediárias são criadas ao gravar; list inclui os arquivos', async () => {
    await adapter.write(vault, 'Pasta/Sub/b.md', '# b');
    await adapter.write(vault, '.urbe/mapa.json', '{"v":4}');
    const l = await adapter.list(vault);
    for (const p of ['a.md', 'Pasta/Sub/b.md', '.urbe/mapa.json']) ok(l.includes(p), 'list inclui ' + p + ' — ' + l.join(','));
    eq(await adapter.read(vault, '.urbe/mapa.json'), '{"v":4}');
  });
  await t('unicode: acentos, emoji e espaços no caminho e no conteúdo', async () => {
    await adapter.write(vault, 'Diário de bordo/Ação ✔.md', 'coração 🌊 — ção');
    eq(await adapter.read(vault, 'Diário de bordo/Ação ✔.md'), 'coração 🌊 — ção');
    ok((await adapter.list(vault)).includes('Diário de bordo/Ação ✔.md'));
  });
  await t('binário: writeBlob/readBlob preservam os bytes', async () => {
    const data = [0, 255, 1, 2, 128, 10, 13, 0];
    await adapter.writeBlob(vault, 'Anexos/x.bin', new Blob([new Uint8Array(data)]));
    eq(await bytes(await adapter.readBlob(vault, 'Anexos/x.bin')), data);
    eq(await adapter.readBlob(vault, 'Anexos/ausente.bin'), null);
    ok((await adapter.list(vault)).includes('Anexos/x.bin'));
  });
  await t('remove: apaga; ausente não falha; o resto permanece', async () => {
    await adapter.remove(vault, 'a.md');
    eq(await adapter.read(vault, 'a.md'), null);
    await adapter.remove(vault, 'a.md'); await adapter.remove(vault, 'Pasta/nunca-existiu.md');
    eq(await adapter.read(vault, 'Pasta/Sub/b.md'), '# b');
    ok(!(await adapter.list(vault)).includes('a.md'));
  });
  await t('pastas: createFolder idempotente; removeFolder só remove vazia', async () => {
    await adapter.createFolder(vault, 'Vazia'); await adapter.createFolder(vault, 'Vazia');
    await adapter.createFolder(vault, 'Vazia/Filha');
    await adapter.write(vault, 'Vazia/Filha/c.md', 'c');
    eq(await adapter.read(vault, 'Vazia/Filha/c.md'), 'c');
    await adapter.removeFolder(vault, 'Vazia/Filha'); // não vazia: não pode perder o arquivo
    eq(await adapter.read(vault, 'Vazia/Filha/c.md'), 'c', 'pasta não vazia preservada');
    await adapter.remove(vault, 'Vazia/Filha/c.md');
    await adapter.removeFolder(vault, 'Vazia/Filha');
    await adapter.removeFolder(vault, 'Pasta-que-nao-existe');
  });
  await t('grava muitos arquivos e lista todos', async () => {
    for (let i = 0; i < 40; i++) await adapter.write(vault, 'Lote/n' + i + '.md', 'n' + i);
    const l = (await adapter.list(vault)).filter((p) => p.indexOf('Lote/') === 0);
    eq(l.length, 40);
  });
  if (opts.manageCities !== false) await t('apagar cidade remove tudo', async () => {
    await adapter.removeCity(vault);
    ok(!(await adapter.cities()).includes(vault), 'cidade some de cities()');
  });
  return names;
}
export default suite;
export const suiteSource = suite.toString();
