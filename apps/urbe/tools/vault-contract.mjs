import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT } from './lib/v2-docs.mjs';
import { manifest, renderContract, verifyContract } from './lib/vault-contract.mjs';
const mp=join(ROOT,'docs/csharp/acceptance/vault-manifest.json'),dp=join(ROOT,'docs/csharp/VAULT-CONTRACT.md');
if(process.argv[2]==='render') { const m=manifest();writeFileSync(mp,JSON.stringify(m,null,2)+'\n');writeFileSync(dp,renderContract(m));console.log('Contrato/manifesto projetados; revisar o diff.'); }
else if(process.argv[2]==='check') {verifyContract(JSON.parse(readFileSync(mp,'utf8')),readFileSync(dp,'utf8'));console.log('UC-3: contrato e 12 fixtures íntegros.');}
else {throw new Error('uso: vault-contract.mjs render|check')}
