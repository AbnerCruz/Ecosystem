// RM-F2-04 (REQ-030): resíduos de UI do 1.x (L10) e o gancho global `window.URBE` (L11) não voltam. "grep zero" sobre o código servido.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { loadManifest, rd } from '../tools/lib/modules.mjs';

const files = ['index.html', 'sw.js', ...loadManifest().styles, ...loadManifest().modules.map((m) => m.file).filter((f) => !f.startsWith('vendor/'))];
const text = new Map(files.map((f) => [f, rd(f)]));
const hits = (re) => [...text].filter(([, s]) => re.test(s)).map(([f]) => f);

// L11: gancho global de inspeção (o mundo legado só existe como serviço de diagnóstico do core)
assert.deepEqual(hits(/\b(?:window|global|globalThis)\.URBE\b/), [], 'window.URBE removido');
assert.ok(rd('src/app.js').includes("core.provide('diagnostics.world'"), 'diagnóstico do mundo legado é serviço do core');

// L10: elementos que nunca apareciam na interface (rodapé do Explorador, botão de importar arquivos, diálogo antigo de Vault/cidade)
for (const id of ['explorerFoot', 'explorerImportFilesBtn', 'newFileBtn', 'newFolderBtn', 'explorerExportBtn', 'vaultDlg', 'vaultClose', 'vaultExport', 'vaultIn', 'vaultMsg', 'cityIn', 'cityExport'])
  assert.deepEqual(hits(new RegExp(`\\b${id}\\b`)), [], `${id}: sem referência`);

// o que continua sendo funcional (seletores de arquivo de importação por pasta/arquivos) segue presente
for (const id of ['vaultFolderIn', 'vaultFilesIn', 'explorerImportFolderBtn']) assert.ok(text.get('index.html').includes(`id="${id}"`), `${id}: continua no index.html`);
console.log('OK   resíduos de UI (L10) e window.URBE (L11) fora do código servido');
