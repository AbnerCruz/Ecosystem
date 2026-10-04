// UC-2: identidade/region/asset/layout como fatos observáveis, sem IDs aleatórios no golden.
import {encodedFixture} from './parity-browser-vault.mjs';
import {waitCityLoaded} from './browser.mjs';
const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
const decode=files=>new Map(files.map(f=>[f.path,f.encoding==='base64'?new Uint8Array(Buffer.from(f.content,'base64')):f.content]));
const definitions=[
  ['stable-ids','v1-mapa-v4',['REQ-041'],{regionHasId:true,assetHasId:true,parentIdMatches:true,parentNoteName:'Alfa',legacyFieldsPresent:true,stableAfterReload:true,renamedPath:'Pasta nova',parentPreserved:true}],
  ['layout-old','v1-mundo-antigo',['REQ-043'],{dialogExplains:true,moved:true,undone:true,backupCount:1,backupReason:'mundo',backupMapOriginal:true,persistedPosition:true,world:'placas-1',maintenance:['mundo','placas-0','placas-1'],noRepeatedDialog:true,positionAfterReload:true}],
  ['layout-keep','v1-mapa-v2',['REQ-043'],{dialogExplains:true,backupCount:1,world:'placas-1'}],
  ['layout-command','v1-mapa-v4',['REQ-043'],{undoEnabledBefore:false,positionRestored:true,backupCount:1,backupReason:'reorganizar',persistedPosition:true,positionAfterReorganizePresent:true}],
  ['external-identity','v1-mapa-v4',['REQ-041','REQ-042'],{closedNoteIdentity:true,closedHouseStable:true,closedFolderNoteStable:true,regionStable:true,assetStable:true,binaryMoved:true,sidecarPathCorrect:true,oldPathsAbsent:true,newPathsPresent:true,openNoteIdentity:true,openOneHouse:true,openMovePersisted:true,openMapIdentity:true}]
];
export function makeBrowserWorldCases(){return definitions.map(([scenario,fixture,requirements,expected])=>({id:'browser-world-'+scenario,operation:'browser.world',requirements,source:'tests/e2e/'+({'stable-ids':'stable-ids','external-identity':'identity'}[scenario]||'layout')+'.e2e.mjs',input:{scenario,files:encodedFixture(fixture),docs:fixture==='v1-mapa-v4'?3:fixture==='v1-mundo-antigo'?2:1},expected}))}
async function save(a){await a.save();return a.vault()}
const mapa=v=>JSON.parse(v['.urbe/mapa.json']);
const layouts=v=>Object.keys(v).filter(p=>/^\.urbe\/backup\/[^/]+-layout-layout[^/]*\/manifest\.json$/.test(p)).map(p=>({dir:p.slice(0,-'/manifest.json'.length),manifest:JSON.parse(v[p])}));
const house=(a,path)=>a.page.evaluate(path=>{const d=UrbeCore.service('documents').get(path),b=d&&UrbeCore.service('diagnostics.world').legacy().buildings.filter(b=>b.tipo==='nota'&&b.documentId===d.id);return d?{id:d.id,count:b.length,x:b[0]?.x,y:b[0]?.y}:null},path);
const pos=async(a,p)=>{const b=await house(a,p);if(!b||b.count!==1)throw new Error('casa única ausente');return[b.x,b.y]};
async function action(a,name){await a.page.evaluate(name=>{window.__parityAction=UrbeCore.commands.execute(name)},name);await a.page.waitForSelector('.udlg [data-primary]');await a.page.click('.udlg [data-primary]');await a.page.evaluate(()=>window.__parityAction)}
async function move(a,moves){await a.page.evaluate(async moves=>{const db=await new Promise((ok,err)=>{const r=indexedDB.open('knowledge-city',3);r.onsuccess=()=>ok(r.result);r.onerror=()=>err(r.error)});try{await new Promise((ok,err)=>{const t=db.transaction('fs','readwrite'),s=t.objectStore('fs');for(const [from,to] of moves){const r=s.get('Urbe/'+from);r.onsuccess=()=>{if(r.result===undefined)return;s.put(r.result,'Urbe/'+to);s.delete('Urbe/'+from)}}t.oncomplete=ok;t.onerror=()=>err(t.error||new Error('falha ao mover arquivo'))})}finally{db.close()}},moves)}
export async function runBrowserWorld(rt,input){
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
