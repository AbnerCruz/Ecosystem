// UC-2: mesmos passos de storage, agora com IndexedDB/OPFS reais; nenhum JS em input.
import {makeStorageCases} from './parity-storage.mjs';
const op=(method,...args)=>({method,args});
export function makeBrowserStorageCases(){
  const cases=makeStorageCases().filter(c=>c.input.backend==='idb'||c.input.permission==='granted').map(c=>({
    ...structuredClone(c),id:'browser-'+c.id,operation:'browser.storage',requirements:['REQ-007','REQ-028','REQ-046'],
    input:{...structuredClone(c.input),backend:c.input.backend==='idb'?'idb-real':'fsa-opfs',dbName:'browser-'+c.id,vault:c.input.vault||'Vault'}
  }));
  for(const backend of ['idb-real','fsa-opfs']){
    const steps=[op('createCity','Alpha'),op('createCity','Beta'),op('cities'),{...op('write','a.md','A'),vault:'Alpha'},{...op('write','b.md','B'),vault:'Beta'},{...op('read','a.md'),vault:'Alpha'},{...op('read','a.md'),vault:'Beta'},op('removeCity','Alpha'),op('cities')];
    cases.push({id:'browser-storage-'+backend+'-cities',operation:'browser.storage',requirements:['REQ-007','REQ-028','REQ-046'],source:'tests/lib/adapter-contract-suite.mjs',input:{backend,dbName:backend+'-cities',vault:'Alpha',steps},expected:{values:[null,null,['Alpha','Beta'],null,null,'A',null,null,['Beta']]}});
    const batch=Array.from({length:40},(_,i)=>op('write','Lote/n'+i+'.md','n'+i));batch.push(op('list'));
    cases.push({id:'browser-storage-'+backend+'-batch',operation:'browser.storage',requirements:['REQ-028'],source:'tests/lib/adapter-contract-suite.mjs',input:{backend,dbName:backend+'-batch',vault:'Batch',steps:batch},expected:{values:[...Array(40).fill(null),[...(backend==='idb-real'?['.urbe/mapa.json']:[]),...Array.from({length:40},(_,i)=>'Lote/n'+i+'.md')].sort()]}});
  }
  return cases;
}
export async function runBrowserStorage(a,input){
  if(!['idb-real','fsa-opfs'].includes(input.backend)||!Array.isArray(input.steps))throw new Error('storage browser inválido');
  const output=await a.page.evaluate(async input=>{
    const allowed=['cities','createCity','removeCity','list','read','readBlob','write','writeBlob','remove','createFolder','removeFolder'];
    let adapter;
    if(input.backend==='idb-real'){
      adapter=UrbeAdapters.idb.create({dbName:input.dbName,mime:()=>''});
      for(const [store,entries] of Object.entries(input.seed||{}))for(const [key,value] of entries){const fn={fs:'fSet',kv:'set',blobs:'bSet'}[store];if(!fn)throw new Error('store desconhecido');await adapter.store[fn](key,value)}
    }else{
      const root=await(await navigator.storage.getDirectory()).getDirectoryHandle(input.dbName,{create:true});adapter=UrbeAdapters.fsa.create({root:()=>root});
    }
    UrbeAdapters.assertAdapter(adapter);
    const vault=input.vault||'Vault';if(!input.seed||!Object.keys(input.seed).length)await adapter.createCity(vault);
    const values=[];
    for(const s of input.steps){
      if(!allowed.includes(s.method)||!Array.isArray(s.args))throw new Error('operação storage desconhecida');
      let value;
      try{
        const args=[...s.args];if(s.method==='writeBlob')args[1]=new Blob([new Uint8Array(args[1])]);
        const global=['cities','createCity','removeCity'].includes(s.method);
        value=await adapter[s.method](...(global?args:[s.vault||vault,...args]));
        if(s.method==='readBlob'&&value!==null)value=Array.from(new Uint8Array(await value.arrayBuffer()));
        if(['list','cities'].includes(s.method))value=value.sort();
        if(value===undefined)value=null;
      }catch(e){if(!s.expectError)throw e;value={rejected:true}}
      if(s.expectError&&value?.rejected!==true)throw new Error('operação deveria ser recusada');
      values.push(value);
    }
    return{values};
  },input);
  a.expectNoErrors();return output;
}
