// UC-2: operações portáveis executam ponte/preload/main reais sobre hosts simulados.
import assert from 'node:assert/strict';
import {makeNativeCases,runNativeCase} from '../tools/lib/parity-native.mjs';
import {compareResults,makeCorpus,validateCorpus} from '../tools/lib/csharp-parity.mjs';
const cases=makeNativeCases(),results=[];
for(const c of cases)results.push({id:c.id,output:await runNativeCase(c.input)});
compareResults(cases,results);
assert.equal(cases.length,24);
assert.equal(makeCorpus().cases.length,256);
validateCorpus(makeCorpus());
for(const [id,change] of [
  ['native-web-contract',o=>o.values[0].capabilities.push('fs')],
  ['native-windows-contract',o=>o.values[0].api.pop()],
  ['native-android-bytes',o=>o.values[1][1]=0],
  ['native-windows-save-cancel',o=>o.values[0].bytes=[1,2]],
  ['native-android-links',o=>o.values.at(-1).opened=['javascript:alert(1)']],
  ['native-windows-denied-write',o=>o.values[0]={rejected:false}],
  ['native-windows-print',o=>o.values[0].isolated=false],
  ['native-android-storage-status',o=>o.values[0].allFiles=true]
]){
  const mutated=structuredClone(results);change(mutated.find(r=>r.id===id).output);
  assert.throws(()=>compareResults(cases,mutated),/paridade falhou/);
}
// Calcula os bytes dados em input; não devolve expected nem um golden da operação.
const save=structuredClone(cases.find(c=>c.id==='native-windows-save'));
save.input.steps[0].args[1]=[42,0,200];save.expected.values[0].bytes=[9];
assert.deepEqual((await runNativeCase(save.input)).values[0].bytes,[42,0,200]);
await assert.rejects(()=>runNativeCase({surface:'desconhecida',steps:[]}),/superfície desconhecida/);
await assert.rejects(()=>runNativeCase({surface:'web',steps:[{method:'noop',args:[],expectError:true}]}),/operação nativa desconhecida/);
const unexpected=structuredClone(cases.find(c=>c.id==='native-windows-bytes').input);
unexpected.steps=[{method:'stat',args:['ausente.md'],expectError:true}];
await assert.rejects(()=>runNativeCase(unexpected),/deveria ser recusada/);
console.log('csharp-native-parity: 24 casos, APIs presentes/ausentes, bytes, recusa/cancelamento, links/impressão e mutações ok; não valida aparelho');

// Como native-contract.mjs: encerra a casca simulada depois de todas as asserções.
process.exit(0);
