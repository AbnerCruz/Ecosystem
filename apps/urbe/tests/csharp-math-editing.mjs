// Supplemental editing corpus: do not silently regenerate expectations in CI.
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
execFileSync(process.execPath, [fileURLToPath(new URL('../csharp/tests/math-oracle.mjs', import.meta.url)), '--check'], {stdio: 'inherit'});
