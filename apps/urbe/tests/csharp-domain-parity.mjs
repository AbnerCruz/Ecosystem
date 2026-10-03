import assert from 'node:assert/strict';
import {makeDomainCases,runDomainCase} from '../tools/lib/parity-domain.mjs';
import {compareResults} from '../tools/lib/csharp-parity.mjs';
const cases=makeDomainCases(),results=[];
for(const c of cases)results.push({id:c.id,output:await runDomainCase(c.operation,c.input)});
compareResults(cases,results);assert(cases.length>=20);
for(const id of ['identity-text-emoji','identity-pair-ambiguous','gc-default']) {
  const rs=structuredClone(results);rs.find(r=>r.id===id).output={};assert.throws(()=>compareResults(cases,rs));
}
await assert.rejects(()=>runDomainCase('unknown',{}));
console.log(`csharp-domain-parity: ${cases.length} casos calculados; mutações recusadas`);
