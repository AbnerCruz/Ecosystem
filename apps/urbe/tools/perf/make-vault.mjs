#!/usr/bin/env node
// Vaults sintéticos determinísticos para performance/E2E (REQ-070): S=50, M=500, L=5000 notas.
// Média ~4 KB/nota; ~10% das notas têm [[links]]; ~5% dos itens são assets binários (PNG mínimo).
//   node tools/perf/make-vault.mjs S --out /caminho    → escreve os arquivos em disco
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { createHash } from 'node:crypto';

export const SIZES = { S: 50, M: 500, L: 5000 };
const WORDS = ('cidade rua casa bairro mapa nota texto ideia projeto leitura escrita estudo resumo tarefa lista plano meta livro pagina '
  + 'tema cor forma luz sombra rio ponte parque praça morador clima tempo hora dia noite chuva vento sol lua estrela caminho destino '
  + 'memória conexão grafo índice busca título tag pasta arquivo cópia versão histórico lixeira recuperação sincronia cálculo fórmula').split(' ');
const PNG = Uint8Array.from(Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==', 'base64'));

function rng(seed) { let a = seed >>> 0; return () => { a |= 0; a = (a + 0x6d2b79f5) | 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }

/** @returns {{files: Map<string,string|Uint8Array>, notes:number, assets:number, bytes:number, hash:string}} */
export function makeVault(size = 'S', seed = 1) {
  const n = SIZES[size] ?? Number(size);
  const r = rng(seed * 7919 + n);
  const pick = (a) => a[Math.floor(r() * a.length)];
  const areas = Math.max(2, Math.round(Math.sqrt(n) / 2)), themes = Math.max(2, Math.round(Math.sqrt(n) / 3));
  const paths = Array.from({ length: n }, (_, i) => `Area ${String(i % areas).padStart(2, '0')}/Tema ${String(Math.floor(i / areas) % themes).padStart(2, '0')}/Nota ${String(i).padStart(5, '0')}.md`);
  const files = new Map();
  for (let i = 0; i < n; i++) {
    const len = 350 + Math.floor(r() * 450); // ~4 KB de média
    const words = Array.from({ length: len }, () => pick(WORDS));
    let body = `# Nota ${String(i).padStart(5, '0')}\n\n`;
    for (let p = 0; p < words.length; p += 40) body += words.slice(p, p + 40).join(' ') + '.\n\n';
    if (r() < 0.10) { const k = 1 + Math.floor(r() * 3); body += 'Ver também: ' + Array.from({ length: k }, () => `[[Nota ${String(Math.floor(r() * n)).padStart(5, '0')}]]`).join(', ') + '\n'; }
    if (r() < 0.20) body += `\n#tag${Math.floor(r() * 12)} #tema${Math.floor(r() * 6)}\n`;
    files.set(paths[i], body);
  }
  const assets = Math.floor(n * 0.05);
  for (let a = 0; a < assets; a++) files.set(`Anexos/imagem-${String(a).padStart(4, '0')}.png`, PNG);
  const h = createHash('sha256');
  for (const [p, c] of [...files].sort(([x], [y]) => (x < y ? -1 : 1))) { h.update(p); h.update(typeof c === 'string' ? c : Buffer.from(c)); }
  const bytes = [...files.values()].reduce((s, c) => s + (typeof c === 'string' ? Buffer.byteLength(c) : c.length), 0);
  return { files, notes: n, assets, bytes, hash: h.digest('hex') };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const size = process.argv[2] || 'S'; const oi = process.argv.indexOf('--out');
  const v = makeVault(size);
  console.log(`${size}: ${v.notes} notas, ${v.assets} assets, ${(v.bytes / 1024).toFixed(0)} KB, sha256 ${v.hash.slice(0, 16)}…`);
  if (oi > 0) { const out = process.argv[oi + 1]; for (const [p, c] of v.files) { const f = join(out, p); mkdirSync(dirname(f), { recursive: true }); writeFileSync(f, c); } console.log('escrito em ' + out); }
}
