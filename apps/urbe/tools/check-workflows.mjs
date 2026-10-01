#!/usr/bin/env node
// Regras dos workflows (REQ-006, REQ-066): publicação só em release.yml (tag), permissões mínimas, sem publicar em push a main.
import { readFileSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = process.env.URBE_ROOT || join(dirname(fileURLToPath(import.meta.url)), '..');
export function check(files = null) {
  const dir = join(ROOT, '.github/workflows');
  files ??= Object.fromEntries(readdirSync(dir).filter((f) => f.endsWith('.yml')).map((f) => [f, readFileSync(join(dir, f), 'utf8')]));
  const errors = [];
  for (const [name, text] of Object.entries(files)) {
    const publishes = /softprops\/action-gh-release|gh release create|gh release upload|contents:\s*write/.test(text);
    if (publishes && name !== 'release.yml') errors.push(`${name}: só release.yml pode publicar (contents: write / criar release)`);
    if (!/^permissions:\s*\n\s+contents:\s*read/m.test(text)) errors.push(`${name}: falta \`permissions: contents: read\` no nível do workflow`);
    if (name === 'release.yml') {
      if (!/tags:\s*\[\s*'v\*'\s*\]/.test(text)) errors.push('release.yml: deve disparar por tag v*');
      if (/branches:\s*\[/.test(text.split('jobs:')[0])) errors.push('release.yml: não pode disparar por push em branch');
      if (!/npm run check/.test(text)) errors.push('release.yml: deve rodar `npm run check` antes de publicar');
      if (!/needs:\s*\[verificar, construir\]/.test(text)) errors.push('release.yml: publicar deve depender de verificar e construir');
    } else if (/on:[\s\S]*?push:[\s\S]*?branches:[\s\S]*?main/.test(text.split('jobs:')[0]) && publishes) {
      errors.push(`${name}: push em main não pode publicar`);
    }
  }
  if (!('release.yml' in files)) errors.push('release.yml ausente');
  return errors;
}
if (import.meta.url === `file://${process.argv[1]}`) {
  const e = check();
  if (e.length) { console.error(e.map((x) => 'ERRO: ' + x).join('\n')); process.exit(1); }
  console.log('OK: workflows respeitam a separação integração × publicação');
}
