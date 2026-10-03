import assert from 'node:assert/strict';
import {makeStorageCases,runStorageCase,makeLegacyIdbCases,runLegacyIdbCase} from '../tools/lib/parity-storage.mjs';
import {compareResults} from '../tools/lib/csharp-parity.mjs';

const storageCases=makeStorageCases(),legacyCases=makeLegacyIdbCases(),cases=[...storageCases,...legacyCases],results=[];
for(const c of cases){
  const run=c.operation==='storage.scenario'?runStorageCase:c.operation==='idb.legacy-city'?runLegacyIdbCase:null;
  if(!run)throw new Error('operação de teste desconhecida: '+c.operation);
  results.push({id:c.id,output:await run(c.input)});
}
compareResults(cases,results);
assert.equal(storageCases.length,13);
assert.equal(storageCases.filter(c=>c.input.backend==='idb').length,4);
assert.equal(legacyCases.length,1);
assert.equal(cases.length,14);

const bad=structuredClone(results);
bad.find(r=>r.id==='storage-binary').output.values[1][1]=0;
assert.throws(()=>compareResults(cases,bad));

const lost=structuredClone(results);
lost.find(r=>r.id==='storage-remove').output.values[5]='';
assert.throws(()=>compareResults(cases,lost));

const denied=structuredClone(results);
denied.find(r=>r.id==='storage-denied').output.values[0]={rejected:false};
assert.throws(()=>compareResults(cases,denied));

const existing=results.find(r=>r.id==='storage-idb-existing');
assert.deepEqual(existing.output.values[0],['Cidade Antiga','Outra']);
assert.equal(existing.output.values[2],'legado');

const vaults=results.find(r=>r.id==='storage-idb-vaults');
assert.deepEqual(vaults.output.values[9],['Beta']);
assert.equal(vaults.output.values[10],null);
assert.equal(vaults.output.values[11][1],'b.md');

const folders=results.find(r=>r.id==='storage-idb-folders');
assert.deepEqual(folders.output.values.at(-1),['.urbe/mapa.json'],'pasta vazia some sem apagar o vault');

const idbMutation=structuredClone(results);
idbMutation.find(r=>r.id==='storage-idb-existing').output.values[1].pop();
assert.throws(()=>compareResults(cases,idbMutation));

const migration=results.find(r=>r.id==='idb-legacy-kv-cidade');
assert.equal(migration.output.legacyKeyDeleted,true);
assert.equal(migration.output.sourceVaultRetained,true);
assert.deepEqual(migration.output.folders,['Cidades/Cidade anterior/Pasta']);
assert.equal(migration.output.notes[0].path,'Cidades/Cidade anterior/Pasta/Alfa.md');
assert.match(migration.output.notes[0].content,/Primeira nota com \[\[Beta\]\]/);

const migrationMutation=structuredClone(results);
migrationMutation.find(r=>r.id==='idb-legacy-kv-cidade').output.notes[0].content='corrompido';
assert.throws(()=>compareResults(cases,migrationMutation));

await assert.rejects(()=>runStorageCase({backend:'fsa-native',permission:'granted',steps:[{method:'noop',args:[]}]}));
await assert.rejects(()=>runStorageCase({...storageCases[0].input,backend:'desconhecido'}));
await assert.rejects(()=>runLegacyIdbCase({key:'outra',city:{buildings:[]}}),/inválido/);

console.log('csharp-storage-parity: 14 casos (9 FSA/native + 4 IDB atuais + 1 migração IDB v1), vaults/bytes/pastas/legado e mutações ok');
