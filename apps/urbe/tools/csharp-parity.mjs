#!/usr/bin/env node
// UC-1/UC-2: projeção e verificação; nunca regenera o oráculo implicitamente.
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { ROOT } from './lib/v2-docs.mjs';
import { read, json, verifyOracle, validateCorpus, renderParity, compareResults } from './lib/csharp-parity.mjs';

const mode = process.argv[2] || 'check';
try {
  const corpus = json('docs/csharp/acceptance/cases.json');
  verifyOracle(json('docs/csharp/acceptance/oracle.json'));
  validateCorpus(corpus);
  const rendered = renderParity(corpus);
  if (mode === 'render') {
    writeFileSync(join(ROOT, 'docs/csharp/PARITY.md'), rendered);
    console.log('PARITY.md gerado a partir das autoridades e do corpus.');
  } else if (mode === 'check') {
    if (read('docs/csharp/PARITY.md') !== rendered) throw new Error('PARITY.md desatualizado; execute render');
    console.log(`Paridade: fontes congeladas intactas; ${corpus.cases.length} casos portáveis; matriz atualizada. C# não verificado.`);
  } else if (mode === 'run') {
    // argv, sem shell; o cliente recebe JSON em stdin e devolve somente JSON em stdout.
    const args = process.argv.slice(3), op = args.shift(), command = args.shift();
    const cases = op === 'all' ? corpus.cases : corpus.cases.filter((c) => c.operation === op);
    if (!cases.length || !command) throw new Error('uso: run <all|operação> <executável> [argumentos]');
    const r = spawnSync(command, args, { cwd: ROOT, input: JSON.stringify({ schemaVersion: 1, cases }),
      encoding: 'utf8', timeout: op === 'all' ? 300000 : 120000, maxBuffer: 8 * 1024 * 1024 });
    if (r.error || r.status !== 0) throw new Error(`cliente falhou: ${r.error?.message || r.stderr || r.status}`);
    const results = JSON.parse(r.stdout);
    if (results.schemaVersion !== 1) throw new Error('versão de resultado incompatível');
    compareResults(cases, results.results);
    console.log(`${cases.length}/${cases.length} casos iguais ao oráculo (${op}); prova apenas do cliente/escopo executado.`);
  } else throw new Error(`modo desconhecido: ${mode}`);
} catch (e) { console.error(e.message); process.exitCode = 1; }
