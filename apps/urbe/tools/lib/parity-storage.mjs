// UC-2: operações explícitas do contrato de armazenamento, não implementações C#.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import vm from 'node:vm';
import {createRequire} from 'node:module';
import {ROOT} from './v2-docs.mjs';
const require=createRequire(import.meta.url);
const {createVaultFS}=require('../../native/desktop/vault-fs.js');
const op=(method,...args)=>({method,args});
const definitions=[
 ['text',[op('write','a.md','olá\n'),op('read','a.md'),op('write','a.md','nova versão'),op('read','a.md'),op('read','ausente.md')],[null,'olá\n',null,'nova versão',null]],
 ['nested',[op('write','Pasta/Sub/b.md','# b'),op('write','.urbe/mapa.json','{"v":4}'),op('list'),op('read','.urbe/mapa.json')],[null,null,['.urbe/mapa.json','Pasta/Sub/b.md'],'{"v":4}']],
 ['unicode',[op('write','Diário de bordo/Ação ✔.md','coração 🌊 — ção'),op('read','Diário de bordo/Ação ✔.md'),op('list')],[null,'coração 🌊 — ção',['Diário de bordo/Ação ✔.md']]],
 ['binary',[op('writeBlob','Anexos/x.bin',[0,255,1,2,128,10,13,0]),op('readBlob','Anexos/x.bin'),op('readBlob','Anexos/ausente.bin')],[null,[0,255,1,2,128,10,13,0],null]],
 ['remove',[op('write','a.md','a'),op('write','b.md','b'),op('remove','a.md'),op('remove','a.md'),op('read','a.md'),op('read','b.md'),op('list')],[null,null,null,null,null,'b',['b.md']]],
 ['folders',[op('createFolder','Vazia'),op('createFolder','Vazia'),op('write','Vazia/Filha/c.md','c'),op('removeFolder','Vazia/Filha'),op('read','Vazia/Filha/c.md'),op('remove','Vazia/Filha/c.md'),op('removeFolder','Vazia/Filha'),op('read','Vazia/Filha/c.md')],[null,null,null,null,'c',null,null,null]],
 ['hidden',[op('write','.urbe/history.v2.json','{"version":2}'),op('write','.oculta/a.md','oculto'),op('write','A.md','visível'),op('list')],[null,null,null,['.urbe/history.v2.json','A.md']]],
 ['escape',[{...op('write','../escape.md','não pode'),expectError:true},op('list')],[{rejected:true},[]]],
 ['denied',[{...op('write','a.md','não pode'),expectError:true},op('list')],[{rejected:true},[]],'denied']
];
export function makeStorageCases() {
  return definitions.map(([name,steps,values,permission])=>({id:`storage-${name}`,operation:'storage.scenario',requirements:['REQ-007','REQ-028','REQ-055'],source:name==='escape'?'tests/native-contract.mjs':name==='hidden'||name==='denied'?'docs/v2/contracts/persistence-adapter.md':'tests/lib/adapter-contract-suite.mjs',
    input:{backend:'fsa-native',permission:permission||'granted',steps},expected:{values}}));
}
export async function runStorageCase(input) {
  if(input.backend!=='fsa-native'||!['granted','denied'].includes(input.permission))throw new Error('backend/permissão desconhecido');
  const temp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-portable-storage-'));
  try {
    const F=createVaultFS(()=>temp),doc={documentElement:{classList:{add(){}}},addEventListener(){},getElementById(){return null},querySelectorAll(){return []}};
    if(input.permission==='denied')F.writeBytes=async()=>{throw new DOMException('recusado','NotAllowedError')};
    const win={UrbeNative:{shell:'electron',platform:'linux',vault:async()=>({label:temp,path:temp}),fs:F},navigator:{},document:doc};
    const c={window:win,document:doc,console,File,Blob,TextEncoder,TextDecoder,DOMException,ArrayBuffer,Uint8Array,Symbol,Promise,fetch,setTimeout,clearTimeout};vm.createContext(c);
    for(const p of ['src/core/artifacts.js','src/native/bridge.js','src/persistence/adapters/fsa.js'])vm.runInContext(fs.readFileSync(path.join(ROOT,p),'utf8'),c,{filename:p});
    const root=await win.UrbeNativeFS.root(),adapter=win.UrbeAdapters.fsa.create({root:()=>root}),values=[];
    for(const s of input.steps) {
      if(!['read','readBlob','write','writeBlob','remove','list','createFolder','removeFolder'].includes(s.method)||!Array.isArray(s.args))throw new Error('operação de armazenamento desconhecida');
      const args=[...s.args];if(s.method==='writeBlob')args[1]=new Blob([new Uint8Array(args[1])]);
      let value,rejected=false;
      try {value=await adapter[s.method]('Urbe',...args)}catch(e){if(!s.expectError)throw e;rejected=true}
      if(s.expectError) {if(!rejected)throw new Error('operação deveria ser recusada');values.push({rejected:true});continue}
      if(s.method==='readBlob'&&value!=null)value=Array.from(new Uint8Array(await value.arrayBuffer()));
      if(s.method==='list')value=[...value].sort();
      values.push(value===undefined?null:value);
    }
    return {values};
  }finally{fs.rmSync(temp,{recursive:true,force:true})}
}
