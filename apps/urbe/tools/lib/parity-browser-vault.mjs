// UC-2: transcrição de fixtures.e2e; expectativas vêm das fixtures congeladas, nunca do app.
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {ROOT} from './v2-docs.mjs';
import {launchApp,openApp,seedKv} from './browser.mjs';
const DIR='tests/fixtures/vaults';
const historical=['v1-mapa-v2','v1-mapa-v4','v1-orfaos','v1-notas-sem-id','v1-cidades-mescladas','v1-personalizacao','v1-paginas','v1-mundo-antigo'];
const forward=['futuro-desconhecido','futuro-v2','vault-futuro'];
const text=p=>/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i.test(p);
const hash=s=>createHash('sha256').update(s).digest('hex');
export function encodedFixture(name){
  const base=path.join(ROOT,DIR,name),out=[];
  function walk(dir,pre){for(const e of fs.readdirSync(dir,{withFileTypes:true}).sort((a,b)=>a.name.localeCompare(b.name))){const p=pre+e.name;if(e.isDirectory())walk(path.join(dir,e.name),p+'/');else if(e.name!=='expect.json'){const b=fs.readFileSync(path.join(dir,e.name));out.push({path:p,encoding:text(p)?'utf8':'base64',content:b.toString(text(p)?'utf8':'base64')})}}}
  walk(base,'');return out;
}
export function makeBrowserVaultCases(){
  return[...historical,...forward].map(name=>{
    const initial=encodedFixture(name),ex=JSON.parse(fs.readFileSync(path.join(ROOT,DIR,name,'expect.json'),'utf8')),future=forward.includes(name);
    const notes=future?['Alfa.md']:ex.notes;
    const hashes=future?ex.futureHashes:Object.fromEntries([...Object.entries(ex.noteHashes).filter(([p])=>text(p)&&!p.startsWith('.urbe/')),...Object.entries(ex.keepHashes)]);
    const legacy=['.urbe/history.json','.urbe/trash.json','.urbe/compositions.json'].filter(p=>initial.some(f=>f.path===p));
    return{id:'browser-vault-'+name,operation:'browser.vault',requirements:['REQ-007','REQ-035','REQ-036','REQ-037','REQ-038'],source:DIR+'/'+name+'/expect.json',
      input:{files:initial,kind:future?'forward':'historical',probe:{notes,hashPaths:Object.keys(hashes),legacyPaths:legacy,readonly:!!ex.readOnly,futureMap:!!ex.futureMapa}},
      expected:future?{loaded:notes.map(path=>({path,present:true})),hashes,editedNotePersisted:!ex.readOnly,futureMapPreserved:ex.futureMapa?true:null,readonlyPreserved:ex.readOnly?true:null,newPaths:ex.readOnly?[]:null}:
        {loaded:notes.map(path=>({path,present:true})),hashes,formatVersion:2,legacyPreserved:true,backupManifestPresent:initial.some(f=>f.path==='.urbe/mapa.json')?true:null,backupMapPreserved:initial.some(f=>f.path==='.urbe/mapa.json')?true:null}};
  });
}
export function makeBrowserLegacyCases(){
  const source=DIR+'/idb-legado-kv-cidade.json',legacy=JSON.parse(fs.readFileSync(path.join(ROOT,source),'utf8'));
  return[{id:'browser-idb-legacy-boot',operation:'browser.idb-legacy',requirements:['REQ-007','REQ-028','REQ-045','REQ-046'],source,input:{legacy},expected:{alphaContent:legacy.buildings.find(b=>b.name==='Alfa').content,legacyKeyRemoved:true}}];
}
export async function runBrowserLegacy(input){
  const app=await launchApp();
  try{
    const h=await app.newPage();await h.page.goto(h.url+'/manifest.webmanifest');
    await seedKv(h.page,[['cidade',input.legacy]]);await openApp(h,{docs:1});
    const output=await h.page.evaluate(async()=>{
      const d=UrbeCore.service('documents').list().find(d=>/Alfa\.md$/.test(d.path));
      const db=await new Promise((ok,err)=>{const r=indexedDB.open('knowledge-city',3);r.onsuccess=()=>ok(r.result);r.onerror=()=>err(r.error)});
      try{const old=await new Promise((ok,err)=>{const r=db.transaction('kv').objectStore('kv').get('cidade');r.onsuccess=()=>ok(r.result);r.onerror=()=>err(r.error)});return{alphaContent:d?.content??null,legacyKeyRemoved:old===undefined}}
      finally{db.close()}
    });
    if(h.errors.length)throw new Error('erros no app: '+h.errors.join(' | '));return output;
  }finally{await app.close()}
}
export async function runBrowserVault(rt,input){
  if(!['historical','forward'].includes(input.kind)||!Array.isArray(input.files))throw new Error('cenário browser vault inválido');
  const seed=new Map(input.files.map(f=>{if(!['utf8','base64'].includes(f.encoding))throw new Error('encoding inválido');return[f.path,f.encoding==='base64'?new Uint8Array(Buffer.from(f.content,'base64')):f.content]}));
  const a=await rt.open({seed,docs:input.probe.notes.length});
  try{
    if(input.kind==='historical')await a.save();
    else{
      await a.page.evaluate(()=>{const docs=UrbeCore.service('documents'),d=docs.list().find(d=>d.path==='Alfa.md');if(!d)throw new Error('Alfa ausente');docs.upsert({...d,content:d.content+'\nedição do teste\n'});UrbeCore.commands.execute('workspace.save')});
      await a.page.waitForTimeout(1500);
    }
    const vault=await a.vault(),notes=await a.notes(),loaded=input.probe.notes.map(path=>({path,present:Object.hasOwn(notes,path)}));
    const hashes=Object.fromEntries(input.probe.hashPaths.map(p=>[p,hash(typeof vault[p]==='string'?vault[p]:'')]));
    let output;
    if(input.kind==='historical'){
      const vj=JSON.parse(vault['.urbe/vault.json']||'null'),map=seed.get('.urbe/mapa.json'),backup=vj?.migrations?.[0]?.backup;
      output={loaded,hashes,formatVersion:vj?.formatVersion??null,legacyPreserved:input.probe.legacyPaths.every(p=>vault[p]===seed.get(p)),backupManifestPresent:map!==undefined?!!vault[backup+'/manifest.json']:null,backupMapPreserved:map!==undefined?vault[backup+'/files/urbe/mapa.json']===map:null};
    }else{
      const ro=input.probe.readonly;
      output={loaded,hashes,editedNotePersisted:(vault['Alfa.md']||'').includes('edição do teste'),futureMapPreserved:input.probe.futureMap?JSON.parse(vault['.urbe/mapa.json']).v===99:null,
        readonlyPreserved:ro?[...seed].every(([p,c])=>typeof c==='string'?vault[p]===c:vault[p]?.blob===true&&vault[p].size===c.length):null,newPaths:ro?Object.keys(vault).filter(p=>!seed.has(p)).sort():null};
    }
    a.expectNoErrors();return output;
  }finally{await a.page.context().close()}
}
