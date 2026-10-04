// UC-2: eventos, seleção por Product, instalação manual e recuperação do updater Android/Windows.
import assert from 'node:assert/strict';
import {makeUpdateCases,runUpdateCase} from '../tools/lib/parity-update.mjs';
import {compareResults} from '../tools/lib/csharp-parity.mjs';
const cases=makeUpdateCases(),results=[];
for(const c of cases)results.push({id:c.id,output:await runUpdateCase(c.input)});
compareResults(cases,results);assert.equal(cases.length,19);
for(const [id,mutate] of [
  ['update-android-available-install',o=>o.opened=[]],
  ['update-android-same-version',o=>o.late=[{state:'none'}]],
  ['update-android-mixed-products',o=>o.values[0].version='99.0.0'],
  ['update-android-invalid-releases',o=>o.values[0].state='available'],
  ['update-android-recovery',o=>o.events.splice(2,1)],
  ['update-android-http-denied',o=>o.late=[]],
  ['update-windows-ready-install',o=>o.installed=[]],
  ['update-windows-downloading',o=>o.checks=2],
  ['update-windows-event-error',o=>o.values[0].message='rede\nstack']
]){
  const changed=structuredClone(results);mutate(changed.find(r=>r.id===id).output);
  assert.throws(()=>compareResults(cases,changed),/paridade falhou/);
}
// Entrada modificada produz resultado novo; expected nunca entra no adapter.
const input=structuredClone(cases[0].input);input.currentVersion='99.0.0';
const output=await runUpdateCase(input);
assert.equal(output.values[0].state,'none');assert.deepEqual(output.opened,[]);
await assert.rejects(()=>runUpdateCase({...input,steps:['noop']}),/passo.*desconhecido/);
await assert.rejects(()=>runUpdateCase({...input,steps:[]}),/não consumida/);
console.log('csharp-update-parity: 19 casos, nove mutações e entrada independente ok; host/rede simulados');
