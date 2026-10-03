import assert from 'node:assert/strict';
import { manifest, renderContract, verifyContract } from '../tools/lib/vault-contract.mjs';
const m=manifest(),doc=renderContract(m);
verifyContract(m,doc);assert.equal(m.fixtures.length,12);
assert(m.fixtures.some(f=>f.files.some(p=>p.path.startsWith('.urbe/'))));
assert(m.fixtures.some(f=>f.files.some(p=>p.path.endsWith('.png'))));
for(const mutate of [x=>x.fixtures.pop(),x=>x.fixtures[0].files.pop(),x=>x.fixtures[0].files[0].sha256='0'.repeat(64),x=>x.sources[0].sha256='0'.repeat(64)]) {
  const x=structuredClone(m);mutate(x);assert.throws(()=>verifyContract(x,doc));
}
assert.throws(()=>verifyContract(m,doc.replace('UTF-16','UTF-8')));
console.log('csharp-vault-contract: integridade, ocultos, binários e mutações ok');
