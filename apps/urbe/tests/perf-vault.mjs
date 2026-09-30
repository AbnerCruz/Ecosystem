// Vaults sintéticos S/M/L: determinísticos e com a forma prometida (REQ-070).
import assert from 'node:assert/strict';
import { makeVault, SIZES } from '../tools/perf/make-vault.mjs';

const a = makeVault('S'), b = makeVault('S');
assert.equal(a.hash, b.hash, 'mesma semente → mesmo conteúdo');
assert.notEqual(a.hash, makeVault('S', 2).hash, 'outra semente → outro conteúdo');
assert.equal(a.notes, SIZES.S); assert.equal(makeVault('M').notes, 500); assert.equal(SIZES.L, 5000);
const md = [...a.files.keys()].filter((p) => p.endsWith('.md')).length; assert.equal(md, 50);
assert.equal(a.assets, Math.floor(50 * 0.05));
const avg = [...a.files].filter(([p]) => p.endsWith('.md')).reduce((s, [, c]) => s + c.length, 0) / md;
assert.ok(avg > 2500 && avg < 6500, 'média ~4 KB por nota: ' + avg);
const withLinks = [...a.files].filter(([, c]) => typeof c === 'string' && c.includes('[[')).length;
assert.ok(withLinks >= 1 && withLinks <= 15, 'algumas notas com links: ' + withLinks);
console.log('perf-vault: ok');
