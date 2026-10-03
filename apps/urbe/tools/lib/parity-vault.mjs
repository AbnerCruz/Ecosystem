// UC-2: cenários de vault como dados e adapter de referência. Nenhuma escrita em vault real.
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { ROOT } from './v2-docs.mjs';
import { createEnv } from '../../tests/helpers/vault-env.mjs';

const DIR = 'tests/fixtures/vaults';
const hash = (x) => createHash('sha256').update(x).digest('hex');
const textPath = (p) => /\.(?:md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i.test(p);
const sort = (xs) => [...xs].sort();
function walk(p) {
  return readdirSync(join(ROOT, p), { withFileTypes: true }).flatMap((e) => e.isDirectory() ? walk(`${p}/${e.name}`) : [`${p}/${e.name}`]).sort();
}
function fixture(name) {
  return walk(`${DIR}/${name}`).filter((p) => !p.endsWith('/expect.json')).map((p) => {
    const path = p.slice(`${DIR}/${name}/`.length), b = readFileSync(join(ROOT, p));
    return { path, encoding: textPath(path) ? 'utf8' : 'base64', content: b.toString(textPath(path) ? 'utf8' : 'base64') };
  });
}
function decode(files) { return new Map(files.map((f) => [f.path, f.encoding === 'base64' ? Buffer.from(f.content, 'base64') : f.content])); }
const originals = (files) => Object.fromEntries([...files].map(([p, v]) => [p, hash(v)]));
const V1 = ['.urbe/mapa.json', '.urbe/journal.json', '.urbe/trash.json', '.urbe/history.json', '.urbe/compositions.json', '.urbe/tutorial.json', '.urbe/merged-v1.json'];

// Expectativas vêm das fixtures e do contrato, não das saídas calculadas pelo adapter.
export function makeVaultCases() {
  return readdirSync(join(ROOT, DIR), { withFileTypes: true }).filter((e) => e.isDirectory()).sort((a,b) => a.name.localeCompare(b.name)).map((e) => {
    const name = e.name, encoded = fixture(name), initial = decode(encoded);
    const ex = JSON.parse(readFileSync(join(ROOT, DIR, name, 'expect.json'), 'utf8'));
    const v = initial.has('.urbe/vault.json') ? JSON.parse(initial.get('.urbe/vault.json')) : null;
    const ro = !!ex.readOnly, mapRO = !!ex.futureMapa || ro && JSON.parse(initial.get('.urbe/mapa.json') || '{}').v > 4;
    const sourceHashes = originals(initial);
    const recovered = ex.recovered ? JSON.parse(initial.get('.urbe/journal.json')).documents : [];
    const recoveredHashes = Object.fromEntries(recovered.map((d) => [d.path, hash(d.content)]));
    const watched = sort([...new Set([...ex.notes, ...Object.keys(ex.noteHashes || {}), ...Object.keys(ex.keepHashes || {}), ...Object.keys(ex.futureHashes || {}),
      ...encoded.filter((f) => f.encoding === 'base64').map((f) => f.path)])]);
    const edit = initial.has('Alfa.md') ? { path: 'Alfa.md', content: '# Alfa\n\nEdição de paridade.\n' } : null;
    const diskHashes = Object.fromEntries(watched.map((p) => [p,
      edit && p === edit.path && !ro ? hash(edit.content) : recoveredHashes[p] || ex.noteHashes?.[p] || ex.keepHashes?.[p] || ex.futureHashes?.[p] || sourceHashes[p]]));
    // No caso recuperado, o journal materializa Delta e Alfa; hashes da fixture já descrevem a recuperação.
    const backupSources = sort([...initial.keys()].filter((p) => V1.includes(p) || p.startsWith('.urbe/origens/')));
    const migrate = !v && !ro && backupSources.length > 0;
    const wantedIds = { ...(ex.ids || {}), ...Object.fromEntries(recovered.map((d) => [d.path, d.id])) };
    const fixedIds = Object.fromEntries(Object.keys(wantedIds).sort().map((p) => [p, wantedIds[p]]));
    return { id: `vault-${name}`, operation: 'vault.scenario', requirements: ['REQ-007','REQ-035','REQ-036','REQ-037','REQ-038','REQ-042'],
      source: `${DIR}/${name}/expect.json`, input: { files: encoded, steps: ['load', ...(edit ? ['edit'] : []), 'flush', 'reload', 'flush'],
        edit, probe: { paths: watched, fixedIdPaths: Object.keys(fixedIds), removedPaths: ex.journalRemoved ? ['.urbe/journal.json'] : [] } },
      expected: { loadedPaths: sort(ex.notes), fixedIds, initialReadOnly: ro, initialMapReadOnly: !!mapRO,
        diskHashes, journalsRemoved: !!ex.journalRemoved,
        wholeVaultUnchanged: ro, backupCount: migrate ? 1 : 0,
        backupSources: migrate ? backupSources : [], backupIntegrity: true,
        formatVersion: ro ? v.formatVersion : 2, migrationIdempotent: true, idsStableAfterReload: true } };
  });
}

function backups(env) {
  return [...env.disk()].filter(([p]) => /^\.urbe\/backup\/[^/]+\/manifest\.json$/.test(p)).map(([p, s]) => ({
    dir: p.slice(0, -'/manifest.json'.length), manifest: JSON.parse(s) }));
}
function cleanup(env) { env.p.suspend(true); if (env.p.timer) { clearTimeout(env.p.timer); env.p.timer = null; } }
export async function runVaultCase(input) {
  const supported = ['load', ...(input.edit ? ['edit'] : []), 'flush', 'reload', 'flush'];
  if (JSON.stringify(input.steps) !== JSON.stringify(supported)) throw new Error('sequência de operações não suportada');
  const initial = decode(input.files), before = originals(initial);
  let env = createEnv(initial), current = env;
  try {
    await env.p.load('V');
    const loadedPaths = sort(env.docs.list().map((d) => d.path));
    const fixedIds = Object.fromEntries(input.probe.fixedIdPaths.map((p) => [p, env.docs.get(p)?.id || null]));
    const initialReadOnly = env.p.info().readOnly, initialMapReadOnly = env.p.info().mapaReadonly;
    if (input.edit) {
      const doc = env.docs.get(input.edit.path); if (!doc) throw new Error(`documento ausente: ${input.edit.path}`);
      env.docs.upsert({ ...doc, content: input.edit.content });
    }
    await env.p.flush();
    const firstBackups = backups(env), firstMigrationCount = JSON.parse(env.get('.urbe/vault.json') || '{"migrations":[]}').migrations.length;
    const ids = new Map(env.docs.list().map((d) => [d.path, d.id]));
    const after = env.disk(); cleanup(env);
    current = createEnv(after); await current.p.load('V'); await current.p.flush();
    const disk = current.disk(), bs = backups(current);
    const backupSources = sort(bs.flatMap((b) => b.manifest.files.map((f) => f.path)));
    const backupIntegrity = bs.every((b) => b.manifest.files.every((f) => {
      const copy = disk.get(`${b.dir}/files/${f.path.replace(/^\.urbe\//, 'urbe/')}`);
      return copy != null && !!f.sha256 && hash(copy) === f.sha256 && before[f.path] === f.sha256;
    }));
    const format = JSON.parse(current.get('.urbe/vault.json') || 'null');
    return { loadedPaths, fixedIds, initialReadOnly, initialMapReadOnly,
      diskHashes: Object.fromEntries(input.probe.paths.map((p) => [p, disk.has(p) ? hash(disk.get(p)) : null])),
      journalsRemoved: input.probe.removedPaths.length > 0 && input.probe.removedPaths.every((p) => !disk.has(p)),
      wholeVaultUnchanged: Object.keys(before).length === disk.size && Object.entries(before).every(([p,h]) => disk.has(p) && hash(disk.get(p)) === h),
      backupCount: bs.length, backupSources, backupIntegrity, formatVersion: format?.formatVersion || null,
      migrationIdempotent: bs.length === firstBackups.length && format?.migrations.length === firstMigrationCount,
      idsStableAfterReload: current.docs.list().length === ids.size && current.docs.list().every((d) => ids.get(d.path) === d.id) };
  } finally { cleanup(env); if (current !== env) cleanup(current); }
}

export function makeRestoreCases() {
  const files = fixture('v1-mapa-v4'), original = decode(files), target = '.urbe/history.json';
  return ['valid', 'corrupt', 'missing'].map((mode) => ({
    id: `vault-backup-${mode}`, operation: 'vault.restore', requirements: ['REQ-007','REQ-038'],
    source: 'tests/vault-format.mjs', input: { files, target, mode,
      edit: { path: 'Alfa.md', content: 'edição para disparar migração\n' },
      steps: ['load','edit','flush','corrupt-original', ...(mode === 'valid' ? [] : [`${mode}-backup`]), 'restore'] },
    expected: { backupCreated: true, restoreRejected: mode !== 'valid',
      targetHash: hash(mode === 'valid' ? original.get(target) : 'CORROMPIDO'),
      restoredPaths: mode === 'valid' ? sort([...original.keys()].filter((p) => V1.includes(p))) : [] } }));
}
export async function runRestoreCase(input) {
  if (!['valid','corrupt','missing'].includes(input.mode)) throw new Error('modo de restauração não suportado');
  const supported = ['load','edit','flush','corrupt-original', ...(input.mode === 'valid' ? [] : [`${input.mode}-backup`]), 'restore'];
  if (JSON.stringify(input.steps) !== JSON.stringify(supported)) throw new Error('sequência de restauração não suportada');
  const env = createEnv(decode(input.files));
  try {
    await env.p.load('V');
    env.docs.upsert({ ...env.docs.get(input.edit.path), content: input.edit.content });
    await env.p.flush();
    const backup = backups(env)[0]; if (!backup) throw new Error('migração não criou backup');
    await env.adapter.write('V', input.target, 'CORROMPIDO');
    const copy = `${backup.dir}/files/${input.target.replace(/^\.urbe\//, 'urbe/')}`;
    if (input.mode === 'corrupt') await env.adapter.write('V', copy, 'BACKUP CORROMPIDO');
    if (input.mode === 'missing') await env.adapter.remove('V', copy);
    let restoredPaths = [], restoreRejected = false;
    try { restoredPaths = sort((await env.core.commands.execute('workspace.restoreBackup', { dir: backup.dir, reload: false })).restored); }
    catch { restoreRejected = true; }
    return { backupCreated: true, restoreRejected, targetHash: hash(env.get(input.target)), restoredPaths };
  } finally { cleanup(env); }
}
