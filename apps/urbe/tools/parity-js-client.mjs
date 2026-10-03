#!/usr/bin/env node
// Adapter do oráculo JS: apenas Markdown puro. Sem runtime C#, DOM falso ou case.expected como resultado.
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { read } from './lib/csharp-parity.mjs';

const input = JSON.parse(readFileSync(0, 'utf8'));
if (input.schemaVersion !== 1) throw new Error('versão incompatível');
const ctx = { window: {}, console }; ctx.window.window = ctx.window; vm.createContext(ctx);
vm.runInContext(read('src/math/core.js'), ctx);
vm.runInContext(read('src/editor/markdown.js'), ctx);
const results = input.cases.map((c) => {
  if (c.operation !== 'markdown.render') throw new Error(`operação não suportada: ${c.operation}`);
  return { id: c.id, output: { html: ctx.window.UrbeMarkdown.render(c.input.markdown) } };
});
process.stdout.write(JSON.stringify({ schemaVersion: 1, results }));
