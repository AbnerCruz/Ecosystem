#!/usr/bin/env node
// Adapter do oráculo JS para o núcleo de persistência: arquivos do corpus, nunca vault real.
import { readFileSync } from 'node:fs';
import { runVaultCase, runRestoreCase } from './lib/parity-vault.mjs';
const input = JSON.parse(readFileSync(0, 'utf8'));
if (input.schemaVersion !== 1) throw new Error('versão incompatível');
const results = [];
for (const c of input.cases) {
  const run = { 'vault.scenario': runVaultCase, 'vault.restore': runRestoreCase }[c.operation];
  if (!run) throw new Error(`operação não suportada: ${c.operation}`);
  results.push({ id: c.id, output: await run(c.input) });
}
process.stdout.write(JSON.stringify({ schemaVersion: 1, results }));
