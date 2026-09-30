// Carrega o Playwright do projeto (devDependency) ou, na falta dele, o instalado globalmente (ambientes de CI/sessão).
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import { join } from 'node:path';

export async function loadPlaywright() {
  try { return await import('playwright'); } catch {}
  try {
    const globalRoot = execSync('npm root -g', { encoding: 'utf8' }).trim();
    return createRequire(join(globalRoot, 'x.js'))('playwright');
  } catch { throw new Error('Playwright não encontrado: rode `npm install` (devDependency `playwright`) e `npx playwright install chromium`'); }
}
