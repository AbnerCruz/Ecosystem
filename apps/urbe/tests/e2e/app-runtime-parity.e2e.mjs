// UC-2: cliente unificado executa o mesmo corpus portável, incluindo UI e DOM Visual reais.
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {ROOT} from '../../tools/lib/v2-docs.mjs';
const r=spawnSync(process.execPath,['tools/csharp-parity.mjs','run','all',process.execPath,'tools/parity-reference-client.mjs'],{cwd:ROOT,encoding:'utf8',timeout:180000});
assert.equal(r.status,0,r.stderr+'\n'+r.stdout);
console.log(r.stdout.trim());
