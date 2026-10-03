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
const fsaDefinitions=[
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
const idbDefinitions=[
 ['idb-existing','Cidade Antiga',{fs:[
   ['Cidade Antiga/Nota.md','legado'],
   ['Cidade Antiga/.urbe/mapa.json','{"v":3}'],
   ['Outra/Outra.md','x']
 ]},[
   op('cities'),op('list'),op('read','Nota.md'),op('read','.urbe/mapa.json')
 ],[
   ['Cidade Antiga','Outra'],['.urbe/mapa.json','Nota.md'],'legado','{"v":3}'
 ]],
 ['idb-vaults','Alpha',{},[
   op('createCity','Alpha'),op('createCity','Alpha'),op('createCity','Beta'),op('cities'),
   {...op('write','a.md','A'),vault:'Alpha'},{...op('write','b.md','B'),vault:'Beta'},
   {...op('read','a.md'),vault:'Alpha'},{...op('read','b.md'),vault:'Beta'},
   op('removeCity','Alpha'),op('cities'),{...op('read','a.md'),vault:'Alpha'},{...op('list'),vault:'Beta'}
 ],[
   null,null,null,['Alpha','Beta'],null,null,'A','B',null,['Beta'],null,['.urbe/mapa.json','b.md']
 ]],
 ['idb-folders','Pastas',{},[
   op('createCity','Pastas'),op('createFolder','Vazia'),op('createFolder','Vazia'),op('createFolder','Vazia/Filha'),
   op('list'),op('removeFolder','Vazia/Filha'),op('list'),op('write','Vazia/c.md','c'),op('read','Vazia/c.md'),op('list'),
   op('remove','Vazia/c.md'),op('removeFolder','Vazia'),op('list')
 ],[
   null,null,null,null,
   ['.urbe/mapa.json','Vazia/.pasta','Vazia/Filha/.pasta'],
   null,['.urbe/mapa.json','Vazia/.pasta'],null,'c',['.urbe/mapa.json','Vazia/.pasta','Vazia/c.md'],
   null,null,['.urbe/mapa.json']
 ]],
 ['idb-binary','Binário',{},[
   op('createCity','Binário'),op('writeBlob','Anexos/x.bin',[0,255,1,2,128,10,13,0]),
   op('readBlob','Anexos/x.bin'),op('readBlob','Anexos/ausente.bin'),op('list')
 ],[
   null,null,[0,255,1,2,128,10,13,0],null,['.urbe/mapa.json','Anexos/x.bin']
 ]]
];

export function makeStorageCases() {
  const fsa=fsaDefinitions.map(([name,steps,values,permission])=>({id:`storage-${name}`,operation:'storage.scenario',requirements:['REQ-007','REQ-028','REQ-055'],source:name==='escape'?'tests/native-contract.mjs':name==='hidden'||name==='denied'?'docs/v2/contracts/persistence-adapter.md':'tests/lib/adapter-contract-suite.mjs',
    input:{backend:'fsa-native',permission:permission||'granted',steps},expected:{values}}));
  const idb=idbDefinitions.map(([name,vault,seed,steps,values])=>({id:`storage-${name}`,operation:'storage.scenario',requirements:['REQ-007','REQ-028','REQ-046'],source:'src/persistence/adapters/idb.js',
    input:{backend:'idb',permission:'granted',dbName:`uc2-${name}`,vault,seed,steps},expected:{values}}));
  return [...fsa,...idb];
}

const LEGACY_IDB_FIXTURE='tests/fixtures/vaults/idb-legado-kv-cidade.json';
export function makeLegacyIdbCases() {
  const city=JSON.parse(fs.readFileSync(path.join(ROOT,LEGACY_IDB_FIXTURE),'utf8'));
  const note=city.buildings.find((b)=>b.tipo==='nota'&&b.name==='Alfa');
  if(!note)throw new Error('fixture IDB legado sem nota Alfa');
  return [{
    id:'idb-legacy-kv-cidade',
    operation:'idb.legacy-city',
    requirements:['REQ-007','REQ-028','REQ-037','REQ-045','REQ-046'],
    source:LEGACY_IDB_FIXTURE,
    input:{key:'cidade',city},
    expected:{
      targetVault:'Urbe',
      sourceVault:'Cidade anterior',
      sourceVaultRetained:true,
      legacyKeyDeleted:true,
      folders:['Cidades/Cidade anterior/Pasta'],
      notes:[{path:'Cidades/Cidade anterior/Pasta/Alfa.md',content:note.content}]
    }
  }];
}

export async function runLegacyIdbCase(input) {
  if(input.key!=='cidade'||!input.city||!Array.isArray(input.city.buildings))throw new Error('formato IDB legado inválido');
  const sourceVault='Cidade anterior',targetVault='Urbe',prefix='Cidades/'+sourceVault;
  const kv=new Map([[input.key,input.city]]),vaults=new Map([[targetVault,new Map()]]);
  const regions=new Map((input.city.regions||[]).map((r)=>[r.id,r])),cache=new Map();
  function regionPath(id,seen) {
    if(!id)return'';
    if(cache.has(id))return cache.get(id);
    const r=regions.get(id);if(!r)throw new Error('região legada ausente: '+id);
    seen=seen||new Set();if(seen.has(id))throw new Error('ciclo de regiões no IDB legado');seen.add(id);
    const name=String(r.name||'').trim();if(!name||/[\\/]/.test(name))throw new Error('nome de região legado não portável');
    const parent=r.parentId?regionPath(r.parentId,new Set(seen)):'',out=parent?parent+'/'+name:name;cache.set(id,out);return out;
  }

  const source=new Map(),folderPaths=[...new Set([...regions.keys()].map((id)=>regionPath(id)).filter(Boolean))].sort();
  for(const folder of folderPaths)source.set(folder+'/.pasta','');
  const used=new Set();
  for(const b of input.city.buildings){
    if(b.tipo!=='nota')continue;
    const name=String(b.name||'').trim();if(!name||/[\\/]/.test(name))throw new Error('nome de nota legado não portável');
    const ext=typeof b.ext==='string'&&/^\.[A-Za-z0-9]+$/.test(b.ext)?b.ext:'.md';
    const folder=b.regionId?regionPath(b.regionId):'',rel=[folder,name+ext].filter(Boolean).join('/');
    if(used.has(rel.toLowerCase()))throw new Error('colisão de notas no IDB legado');used.add(rel.toLowerCase());
    source.set(rel,String(b.content||''));
  }
  vaults.set(sourceVault,source);
  kv.delete(input.key);

  const target=vaults.get(targetVault);
  for(const [rel,value] of source){
    if(rel==='.pasta'||rel.endsWith('/.pasta'))target.set(prefix+'/'+rel,'');
    else if(!rel.startsWith('.urbe/'))target.set(prefix+'/'+rel,value);
  }
  const folders=[...target.keys()].filter((p)=>p.endsWith('/.pasta')).map((p)=>p.slice(0,-'/.pasta'.length)).sort();
  const notes=[...target].filter(([p])=>/\.md$/i.test(p)).map(([path,content])=>({path,content})).sort((a,b)=>a.path.localeCompare(b.path));
  return{targetVault,sourceVault,sourceVaultRetained:vaults.has(sourceVault),legacyKeyDeleted:!kv.has(input.key),folders,notes};
}

function createMemoryIndexedDB(seed) {
  const databases=new Map();
  const initial=seed||{};
  const request=(fn)=>{
    const r={result:undefined,error:null,onsuccess:null,onerror:null};
    queueMicrotask(()=>{try{r.result=fn();if(r.onsuccess)r.onsuccess({target:r})}catch(e){r.error=e;if(r.onerror)r.onerror({target:r})}});
    return r;
  };
  const apiFor=(record,name)=>{
    const map=record.stores.get(name);
    if(!map)throw new Error('object store ausente: '+name);
    return{
      get:(key)=>request(()=>map.get(key)),
      put:(value,key)=>request(()=>{map.set(key,value);return key}),
      delete:(key)=>request(()=>{map.delete(key);return undefined}),
      getAllKeys:()=>request(()=>Array.from(map.keys()))
    };
  };
  return{
    open(name,version){
      const fresh=!databases.has(name);
      if(fresh)databases.set(name,{version,stores:new Map(),seeded:false});
      const record=databases.get(name),r={result:null,error:null,onupgradeneeded:null,onsuccess:null,onerror:null};
      queueMicrotask(()=>{
        try{
          const db={
            objectStoreNames:{contains:(store)=>record.stores.has(store)},
            createObjectStore(store){if(!record.stores.has(store))record.stores.set(store,new Map());return apiFor(record,store)},
            transaction(store){return{objectStore:(requested)=>apiFor(record,requested||store)}}
          };
          r.result=db;
          if(fresh&&r.onupgradeneeded)r.onupgradeneeded({target:r});
          if(!record.seeded){
            for(const store of ['kv','fs','blobs']){
              if(!record.stores.has(store))record.stores.set(store,new Map());
              for(const entry of initial[store]||[])record.stores.get(store).set(entry[0],entry[1]);
            }
            record.seeded=true;
          }
          if(r.onsuccess)r.onsuccess({target:r});
        }catch(e){r.error=e;if(r.onerror)r.onerror({target:r})}
      });
      return r;
    }
  };
}

async function createAdapter(input) {
  if(input.backend==='fsa-native'){
    if(!['granted','denied'].includes(input.permission))throw new Error('permissão desconhecida');
    const temp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-portable-storage-'));
    const F=createVaultFS(()=>temp),doc={documentElement:{classList:{add(){}}},addEventListener(){},getElementById(){return null},querySelectorAll(){return []}};
    if(input.permission==='denied')F.writeBytes=async()=>{throw new DOMException('recusado','NotAllowedError')};
    const win={UrbeNative:{shell:'electron',platform:'linux',vault:async()=>({label:temp,path:temp}),fs:F},navigator:{},document:doc};
    const c={window:win,document:doc,console,File,Blob,TextEncoder,TextDecoder,DOMException,ArrayBuffer,Uint8Array,Symbol,Promise,fetch,setTimeout,clearTimeout};vm.createContext(c);
    for(const p of ['src/core/artifacts.js','src/native/bridge.js','src/persistence/adapters/fsa.js'])vm.runInContext(fs.readFileSync(path.join(ROOT,p),'utf8'),c,{filename:p});
    const root=await win.UrbeNativeFS.root();
    return{adapter:win.UrbeAdapters.fsa.create({root:()=>root}),cleanup:()=>fs.rmSync(temp,{recursive:true,force:true})};
  }
  if(input.backend==='idb'){
    if(input.permission!=='granted')throw new Error('permissão IDB desconhecida');
    const doc={documentElement:{classList:{add(){}}},addEventListener(){},getElementById(){return null},querySelectorAll(){return []}};
    const indexedDB=createMemoryIndexedDB(input.seed),win={indexedDB,Blob,document:doc};
    const c={window:win,document:doc,console,Blob,TextEncoder,TextDecoder,DOMException,ArrayBuffer,Uint8Array,Symbol,Promise,queueMicrotask};vm.createContext(c);
    vm.runInContext(fs.readFileSync(path.join(ROOT,'src/persistence/adapters/idb.js'),'utf8'),c,{filename:'src/persistence/adapters/idb.js'});
    return{adapter:win.UrbeAdapters.idb.create({dbName:input.dbName||'knowledge-city',mime:()=>''}),cleanup:()=>{}};
  }
  throw new Error('backend desconhecido');
}

export async function runStorageCase(input) {
  const {adapter,cleanup}=await createAdapter(input);
  try {
    const values=[],cityMethods=new Set(['cities','createCity','removeCity']);
    for(const s of input.steps) {
      if(!['cities','createCity','removeCity','read','readBlob','write','writeBlob','remove','list','createFolder','removeFolder'].includes(s.method)||!Array.isArray(s.args))throw new Error('operação de armazenamento desconhecida');
      const args=[...s.args];if(s.method==='writeBlob')args[1]=new Blob([new Uint8Array(args[1])]);
      let value,rejected=false;
      try{
        value=cityMethods.has(s.method)?await adapter[s.method](...args):await adapter[s.method](s.vault||input.vault||'Urbe',...args);
      }catch(e){if(!s.expectError)throw e;rejected=true}
      if(s.expectError){if(!rejected)throw new Error('operação deveria ser recusada');values.push({rejected:true});continue}
      if(s.method==='readBlob'&&value!=null)value=Array.from(new Uint8Array(await value.arrayBuffer()));
      if((s.method==='list'||s.method==='cities')&&Array.isArray(value))value=[...value].sort();
      values.push(value===undefined?null:value);
    }
    return{values};
  }finally{cleanup()}
}
