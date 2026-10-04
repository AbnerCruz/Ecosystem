// UC-2: identidade/region/asset/layout como fatos observáveis, sem IDs aleatórios no golden.
import {encodedFixture} from './parity-browser-vault.mjs';
import {launchApp,openApp,readVault,seedVault,waitCityLoaded,waitSaved} from './browser.mjs';
import {makeVault} from '../perf/make-vault.mjs';
const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
const decode=files=>new Map(files.map(f=>[f.path,f.encoding==='base64'?new Uint8Array(Buffer.from(f.content,'base64')):f.content]));
const definitions=[
  ['stable-ids','v1-mapa-v4',['REQ-041'],{regionHasId:true,assetHasId:true,parentIdMatches:true,parentNoteName:'Alfa',legacyFieldsPresent:true,stableAfterReload:true,renamedPath:'Pasta nova',parentPreserved:true}],
  ['layout-old','v1-mundo-antigo',['REQ-043'],{dialogExplains:true,moved:true,undone:true,backupCount:1,backupReason:'mundo',backupMapOriginal:true,persistedPosition:true,world:'placas-1',maintenance:['mundo','placas-0','placas-1'],noRepeatedDialog:true,positionAfterReload:true}],
  ['layout-keep','v1-mapa-v2',['REQ-043'],{dialogExplains:true,backupCount:1,world:'placas-1'}],
  ['layout-command','v1-mapa-v4',['REQ-043'],{undoEnabledBefore:false,positionRestored:true,backupCount:1,backupReason:'reorganizar',persistedPosition:true,positionAfterReorganizePresent:true}],
  ['external-identity','v1-mapa-v4',['REQ-041','REQ-042'],{closedNoteIdentity:true,closedHouseStable:true,closedFolderNoteStable:true,regionStable:true,assetStable:true,binaryMoved:true,sidecarPathCorrect:true,oldPathsAbsent:true,newPathsPresent:true,openNoteIdentity:true,openOneHouse:true,openMovePersisted:true,openMapIdentity:true}],
  ['map-writer','v1-mapa-v4',['REQ-040'],{createdInMap:true,idStable:true,reorganized:true,mapVersion:4,mapWritesAtLeast2:true,mapWriterOnly:true}]
];
const scaleCase={id:'browser-world-scale-s',operation:'browser.world',requirements:['REQ-070'],source:'tests/e2e/app-runtime.e2e.mjs',input:{scenario:'scale-s',size:'S',seed:1,docs:50,assets:2},expected:{documentsAtLeast:true,worldBuildingsAtLeast:true,assetCount:2,titleMatches:true}};
const multiCityCase={id:'browser-world-multi-city',operation:'browser.world',requirements:['REQ-045'],source:'tests/e2e/multi-city.e2e.mjs',input:{scenario:'multi-city',cities:[{name:'Norte',files:[{path:'Bairro/Um.md',encoding:'utf8',content:'# Um\n'},{path:'.urbe/mapa.json',encoding:'utf8',content:JSON.stringify({v:4,mundo:'placas-1',regioes:[{caminho:'Bairro',nome:'Bairro',cor:'#4aa3ff',x:30,y:30,w:12,h:10,cells:null}],notas:{'Bairro/Um.md':{id:'doc_norte_1',x:32,y:32,sprite:'house1'}},construcoes:[]})}]},{name:'Sul',files:[{path:'Dois.md',encoding:'utf8',content:'# Dois\n'}]}],docs:2},expected:{originIdPreserved:true,southPresent:true,geometryMerged:true,mapIdPreserved:true,migrationOnce:true,migrationSources:['Norte'],boot2Stable:true,boot3Stable:true,mapWritesPresent:true,mapWriterOnly:true,archived:['Norte','Sul'],stores:['Norte','Sul','Urbe']}};
export function makeBrowserWorldCases(){return [...definitions.map(([scenario,fixture,requirements,expected])=>({id:'browser-world-'+scenario,operation:'browser.world',requirements,source:'tests/e2e/'+({'stable-ids':'stable-ids','external-identity':'identity','map-writer':'map-writer'}[scenario]||'layout')+'.e2e.mjs',input:{scenario,files:encodedFixture(fixture),docs:fixture==='v1-mapa-v4'?3:fixture==='v1-mundo-antigo'?2:1},expected})),scaleCase,multiCityCase]}
async function save(a){await a.save();return a.vault()}
const mapa=v=>JSON.parse(v['.urbe/mapa.json']);
const layouts=v=>Object.keys(v).filter(p=>/^\.urbe\/backup\/[^/]+-layout-layout[^/]*\/manifest\.json$/.test(p)).map(p=>({dir:p.slice(0,-'/manifest.json'.length),manifest:JSON.parse(v[p])}));
const house=(a,path)=>a.page.evaluate(path=>{const d=UrbeCore.service('documents').get(path),b=d&&UrbeCore.service('diagnostics.world').legacy().buildings.filter(b=>b.tipo==='nota'&&b.documentId===d.id);return d?{id:d.id,count:b.length,x:b[0]?.x,y:b[0]?.y}:null},path);
const pos=async(a,p)=>{const b=await house(a,p);if(!b||b.count!==1)throw new Error('casa única ausente');return[b.x,b.y]};
async function action(a,name){await a.page.evaluate(name=>{window.__parityAction=UrbeCore.commands.execute(name)},name);await a.page.waitForSelector('.udlg [data-primary]');await a.page.click('.udlg [data-primary]');await a.page.evaluate(()=>window.__parityAction)}
async function move(a,moves){await a.page.evaluate(async moves=>{const db=await new Promise((ok,err)=>{const r=indexedDB.open('knowledge-city',3);r.onsuccess=()=>ok(r.result);r.onerror=()=>err(r.error)});try{await new Promise((ok,err)=>{const t=db.transaction('fs','readwrite'),s=t.objectStore('fs');for(const [from,to] of moves){const r=s.get('Urbe/'+from);r.onsuccess=()=>{if(r.result===undefined)return;s.put(r.result,'Urbe/'+to);s.delete('Urbe/'+from)}}t.oncomplete=ok;t.onerror=()=>err(t.error||new Error('falha ao mover arquivo'))})}finally{db.close()}},moves)}
async function runMultiCity(input){
  const app=await launchApp();
  try{
    const h=await app.newPage(),{page}=h;
    await page.goto(h.url+'/manifest.webmanifest');
    for(const city of input.cities)await seedVault(page,decode(city.files),city.name);
    await page.addInitScript(()=>{window.__mapaWrites=[];const put=IDBObjectStore.prototype.put;IDBObjectStore.prototype.put=function(v,k){if(String(k)==='Urbe/.urbe/mapa.json')window.__mapaWrites.push(new Error('mapa').stack);return put.apply(this,arguments)}});
    const snapshot=async()=>{const v=await readVault(page);return JSON.stringify(Object.keys(v).filter(k=>!k.startsWith('.urbe/backup/')&&!k.startsWith('Tutorial/')).sort().map(k=>[k,k==='.urbe/vault.json'?JSON.parse(v[k]).migrations.map(m=>m.id):k.endsWith('.md')?v[k]:k==='.urbe/mapa.json'?JSON.parse(v[k]).regioes.map(r=>r.caminho).sort():1]))};
    const boot=async first=>{if(first)await openApp(h,{docs:input.docs});else{await page.reload();await page.waitForFunction(n=>window.UrbeCore&&UrbeCore.state.select('ready')&&UrbeCore.service('documents').list().length>=n,input.docs,{timeout:60000});await waitCityLoaded(page)}await page.evaluate(()=>UrbeMultiCity.finishing);await waitSaved(page);return snapshot()};
    const s1=await boot(true);
    const docs=Object.fromEntries(await page.evaluate(()=>UrbeCore.service('documents').list().filter(d=>d.path.startsWith('Cidades/')).map(d=>[d.path,d.id])));
    const v1=await readVault(page),mapa1=JSON.parse(v1['.urbe/mapa.json']),mig=JSON.parse(v1['.urbe/vault.json']).migrations.filter(m=>m.id==='multi-city');
    const s2=await boot(false),s3=await boot(false),writes=await page.evaluate(()=>window.__mapaWrites);
    await page.evaluate(()=>UrbeCore.commands.execute('workspace.archiveOldCities'));
    const archived=await page.evaluate(()=>[...UrbeMultiCity.archived(UrbeCore.service('persistence'))].sort());
    const stores=await page.evaluate(async()=>{const db=await new Promise((ok,err)=>{const r=indexedDB.open('knowledge-city',3);r.onsuccess=()=>ok(r.result);r.onerror=()=>err(r.error)});try{return await new Promise((ok,err)=>{const t=db.transaction('fs'),q=t.objectStore('fs').getAllKeys();q.onsuccess=()=>ok([...new Set(q.result.map(k=>String(k).split('/')[0]))].sort());q.onerror=()=>err(q.error)})}finally{db.close()}});
    if(h.errors.length)throw new Error('erros no app: '+h.errors.join(' | '));
    return{originIdPreserved:docs['Cidades/Norte/Bairro/Um.md']==='doc_norte_1',southPresent:Object.hasOwn(docs,'Cidades/Sul/Dois.md'),geometryMerged:mapa1.regioes.some(r=>r.caminho==='Cidades/Norte/Bairro'),mapIdPreserved:mapa1.notas['Cidades/Norte/Bairro/Um.md']?.id==='doc_norte_1',migrationOnce:mig.length===1,migrationSources:mig[0]?.sources||null,boot2Stable:s2===s1,boot3Stable:s3===s1,mapWritesPresent:writes.length>=1,mapWriterOnly:writes.every(s=>/persistence\/workspace\.js/.test(s)),archived,stores};
  }finally{await app.close()}
}
export async function runBrowserWorld(rt,input){
  if(input.scenario==='multi-city')return runMultiCity(input);
  if(input.scenario==='scale-s'){
    const generated=makeVault(input.size,input.seed),a=await rt.open({seed:generated,docs:input.docs});
    try{
      const notes=await a.notes(),world=await a.world(),vault=await a.vault(),version=await a.version();
      const output={documentsAtLeast:Object.keys(notes).filter(p=>!p.startsWith('Tutorial/')).length>=input.docs,worldBuildingsAtLeast:world.buildings.filter(b=>!b.path.startsWith('Tutorial/')).length>=input.docs,assetCount:Object.keys(vault).filter(p=>p.startsWith('Anexos/')&&vault[p]?.blob===true).length,titleMatches:await a.page.title()==='Urbe v'+version};
      a.expectNoErrors();return output;
    }finally{await a.page.context().close()}
  }
  if(!definitions.some(d=>d[0]===input.scenario))throw new Error('cenário de mundo desconhecido');
  const seed=decode(input.files),a=await rt.open({seed,docs:input.docs});
  try{
    let output;
    if(input.scenario==='stable-ids'){
      let m=mapa(await save(a));const reg=m.regioes.find(r=>r.caminho==='Pasta'),ast=m.construcoes.find(c=>c.name==='logo.png'),alfa=(await house(a,'Alfa.md')).id;
      output={regionHasId:/^reg_/.test(reg?.id||''),assetHasId:/^ast_/.test(ast?.id||''),parentIdMatches:ast?.parentId===alfa,parentNoteName:ast?.parentNoteName??null,legacyFieldsPresent:['caminho','nome','x','y','w','h'].every(k=>Object.hasOwn(reg,k))};
      await a.reload({docs:3});m=mapa(await save(a));output.stableAfterReload=m.regioes.find(r=>r.caminho==='Pasta')?.id===reg.id&&m.construcoes.find(c=>c.name==='logo.png')?.id===ast.id;
      await a.page.evaluate(()=>{const r=UrbeCore.service('diagnostics.world').legacy().regions.find(r=>r.name==='Pasta');if(!r)throw new Error('região ausente');r.name='Pasta nova'});m=mapa(await save(a));
      output.renamedPath=m.regioes.find(r=>r.id===reg.id)?.caminho??null;output.parentPreserved=m.construcoes.find(c=>c.id===ast.id)?.parentId===alfa;
    }else if(input.scenario==='layout-old'){
      const original=JSON.parse(seed.get('.urbe/mapa.json')),before=[original.notas['Alfa.md'].x,original.notas['Alfa.md'].y];
      await a.page.waitForSelector('.udlg [data-primary]');const msg=await a.page.textContent('.udlg-msg'),moved=await pos(a,'Alfa.md');await a.page.click('.udlg [data-primary]');const undone=await pos(a,'Alfa.md');
      const vault=await save(a),backups=layouts(vault),m=mapa(vault),log=JSON.parse(vault['.urbe/vault.json']).maintenance.filter(x=>x.kind==='layout');
      output={dialogExplains:msg.includes('placas-0'),moved:!equal(moved,before),undone:equal(undone,before),backupCount:backups.length,backupReason:backups[0]?.manifest.reason??null,backupMapOriginal:backups.length===1&&vault[backups[0].dir+'/files/urbe/mapa.json']===seed.get('.urbe/mapa.json'),persistedPosition:equal([m.notas['Alfa.md'].x,m.notas['Alfa.md'].y],before),world:m.mundo,maintenance:log[0]?[log[0].reason,log[0].from,log[0].to]:null};
      await a.reload({docs:2});output.noRepeatedDialog=await a.page.$('.udlg [data-primary]')===null;output.positionAfterReload=equal(await pos(a,'Alfa.md'),before);
    }else if(input.scenario==='layout-keep'){
      await a.page.waitForSelector('.udlg [data-cancel].ui-btn');const msg=await a.page.textContent('.udlg-msg');await a.page.click('.udlg [data-cancel].ui-btn');const vault=await save(a);output={dialogExplains:msg.includes('sem versão'),backupCount:layouts(vault).length,world:mapa(vault).mundo};
    }else if(input.scenario==='layout-command'){
      await save(a);const before=await pos(a,'Pasta/Gama.md'),enabled=await a.page.evaluate(()=>UrbeCore.commands.list().find(c=>c.id==='city.undoReorganize').enabled());
      await action(a,'city.reorganize');const after=await pos(a,'Pasta/Gama.md');await a.command('city.undoReorganize');const restored=await pos(a,'Pasta/Gama.md'),vault=await save(a),backups=layouts(vault),n=mapa(vault).notas['Pasta/Gama.md'];
      output={undoEnabledBefore:enabled,positionRestored:equal(restored,before),backupCount:backups.length,backupReason:backups[0]?.manifest.reason??null,persistedPosition:equal([n.x,n.y],before),positionAfterReorganizePresent:!!after};
    }else if(input.scenario==='map-writer'){
      await a.page.evaluate(()=>{window.__mapaWrites=[];const put=IDBObjectStore.prototype.put;IDBObjectStore.prototype.put=function(value,key){if(String(key).endsWith('/.urbe/mapa.json'))window.__mapaWrites.push(new Error('mapa').stack);return put.apply(this,arguments)}});
      const id=await a.page.evaluate(()=>UrbeCore.commands.execute('document.update',{path:'Projetos/Só API.md',content:'# Só API\n'}).id);
      await a.page.waitForFunction(id=>!!UrbeCore.service('world.projection').projectDocument(id),id,{timeout:10000}).catch(()=>{});
      await a.page.waitForTimeout(1500);await waitSaved(a.page);
      let vault=await a.vault(),m=mapa(vault),before=vault['.urbe/mapa.json'],note=m.notas['Projetos/Só API.md'];
      await a.page.evaluate(()=>{window.__reorg=UrbeCore.commands.execute('city.reorganize')});await a.page.click('.udlg [data-primary]');await a.page.evaluate(()=>window.__reorg);
      await a.page.waitForTimeout(1500);await waitSaved(a.page);vault=await a.vault();m=mapa(vault);
      const writes=await a.page.evaluate(()=>window.__mapaWrites);
      output={createdInMap:!!note,idStable:note?.id===id,reorganized:vault['.urbe/mapa.json']!==before,mapVersion:m.v,mapWritesAtLeast2:writes.length>=2,mapWriterOnly:writes.every(s=>/persistence\/workspace\.js/.test(s))};
    }else{
      const m0=mapa(await save(a)),alfa0=await house(a,'Alfa.md'),gama0=await house(a,'Pasta/Gama.md'),reg0=m0.regioes.find(r=>r.caminho==='Pasta'),ast0=m0.construcoes.find(c=>c.name==='logo.png');
      await a.page.waitForFunction(()=>!navigator.serviceWorker||navigator.serviceWorker.controller);await a.page.goto(rt.url+'/package.json');
      await move(a,[['Alfa.md','Alfa nova.md'],['Pasta/Gama.md','Projetos/Gama.md'],['Pasta/logo.png','Projetos/logo.png'],['Pasta/.pasta','Projetos/.pasta']]);await a.page.goto(rt.url+'/index.html');await a.page.waitForFunction(()=>window.UrbeCore&&UrbeCore.state.select('ready')&&UrbeCore.service('documents').list().length>=3);await waitCityLoaded(a.page);
      const alfa1=await house(a,'Alfa nova.md'),gama1=await house(a,'Projetos/Gama.md'),vault=await save(a),m1=mapa(vault),reg1=m1.regioes.find(r=>r.id===reg0.id),ast1=m1.construcoes.find(c=>c.id===ast0.id);
      output={closedNoteIdentity:alfa1?.id===alfa0.id,closedHouseStable:equal([alfa1?.x,alfa1?.y,alfa1?.count],[alfa0.x,alfa0.y,1]),closedFolderNoteStable:gama1?.id===gama0.id&&equal([gama1?.x,gama1?.y],[gama0.x,gama0.y]),regionStable:reg1?.caminho==='Projetos'&&equal([reg1.x,reg1.y,reg1.w,reg1.h],[reg0.x,reg0.y,reg0.w,reg0.h]),assetStable:ast1?.files?.[0]?.relPath==='Projetos/logo.png'&&ast1.parentId===alfa0.id,binaryMoved:vault['Projetos/logo.png']?.blob===true&&!Object.hasOwn(vault,'Pasta/logo.png'),sidecarPathCorrect:JSON.parse(vault['.urbe/identity.json']).docs[alfa0.id]?.path==='Alfa nova.md',oldPathsAbsent:!Object.hasOwn(vault,'Alfa.md')&&!Object.hasOwn(vault,'Pasta/Gama.md'),newPathsPresent:!!vault['Alfa nova.md']&&!!vault['Projetos/Gama.md']};
      const beta0=await house(a,'Beta.md');await move(a,[['Beta.md','Arquivo/Beta.md']]);await a.page.evaluate(()=>UrbeCore.service('persistence').syncFromDisk());await a.page.waitForTimeout(600);const beta1=await house(a,'Arquivo/Beta.md'),v2=await save(a);
      Object.assign(output,{openNoteIdentity:beta1?.id===beta0.id,openOneHouse:beta1?.count===1,openMovePersisted:!!v2['Arquivo/Beta.md']&&!Object.hasOwn(v2,'Beta.md'),openMapIdentity:mapa(v2).notas['Arquivo/Beta.md']?.id===beta0.id});
    }
    a.expectNoErrors();return output;
  }finally{await a.page.context().close()}
}
