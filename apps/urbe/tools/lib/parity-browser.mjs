// UC-2: ações semânticas em dados; seletores ficam exclusivamente no adapter JS.
import fs from 'node:fs';
import path from 'node:path';
import {ROOT} from './v2-docs.mjs';
import {startRuntime} from '../../tests/e2e/app-runtime.mjs';
import {runBrowserVault,runBrowserLegacy} from './parity-browser-vault.mjs';
export function makeUiCases(){return JSON.parse(fs.readFileSync(path.join(ROOT,'docs/csharp/acceptance/ui-cases.json'),'utf8')).cases}
export async function createBrowserReference(){
  const rt=await startRuntime();let visualApp;
  return{
    vault:input=>runBrowserVault(rt,input),
    legacy:runBrowserLegacy,
    async visual(input){
      visualApp ||= await rt.open({docs:40});
      return visualApp.page.evaluate(input=>{
        const tpl=document.createElement('template');tpl.innerHTML=input.html;
        return{markdown:UrbeVisual.markdownFromVisual(tpl.content,{frontmatter:UrbeMarkdown.splitFrontmatter(input.bodyEditor).raw})};
      },input);
    },
    async ui(input){
      if(!Array.isArray(input.steps))throw new Error('passos UI inválidos');
      const options=input.files?{seed:new Map(Object.entries(input.files)),docs:input.docs??Object.keys(input.files).length}:{docs:40};
      if(input.now)options.initScript=`Date.now=()=>${Number(input.now)};`;
      const a=await rt.open(options),ids=new Map(),positions=new Map(),values=[];
      for(const s of input.steps){
        if(!Array.isArray(s.args))throw new Error('argumentos UI inválidos');
        let value;const args=s.args;
        switch(s.method){
          case 'boot': value={titleMatches:await a.page.title()==='Urbe v'+await a.version(),tutorialPresent:Object.keys(await a.notes()).filter(p=>p.startsWith('Tutorial/')).length>=40};break;
          case 'create': {const id=await a.createNote(...args);ids.set(args[0],id);value={idPresent:typeof id==='string'&&!!id};break}
          case 'reload': await a.reload();value=null;break;
          case 'observe': {
            const notes=await a.notes(),world=await a.world(),vault=await a.vault();
            value=args.map(p=>({path:p,content:notes[p]?.content??null,saved:vault[p]??null,idStable:!!ids.get(p)&&notes[p]?.id===ids.get(p),inWorld:world.buildings.some(b=>b.path===p&&b.id===notes[p]?.id)}));break;
          }
          case 'visualAppend': {
            await a.page.waitForFunction(()=>document.getElementById('editorFull').classList.contains('open'));
            await a.page.evaluate(html=>{const p=document.createElement('p');p.innerHTML=html;const el=document.getElementById('renderedPreview');el.appendChild(p);el.dispatchEvent(new Event('input',{bubbles:true}))},args[0]);
            await a.page.waitForFunction(text=>document.getElementById('bodyEditor').value.includes(text),args[1]);await a.save();value=null;break;
          }
          case 'preview': {
            await a.page.evaluate(()=>document.getElementById('viewModeBtn').click());
            await a.page.waitForFunction(()=>document.getElementById('renderedPreview').querySelector('table'));
            value=await a.page.evaluate(()=>{const root=document.getElementById('renderedPreview');return{table:!!root.querySelector('table[data-md-table="1"][data-align="left,right"]'),callout:root.querySelector('[data-callout]')?.getAttribute('data-callout'),wikilink:root.querySelector('.wikilink')?.getAttribute('data-note-name'),unsafeHref:!!root.querySelector('[href^="javascript:"]'),safeHref:root.querySelector('a[href^="https:"]')?.getAttribute('href')}});break;
          }
          case 'route': {
            value=await a.page.evaluate(([path,content,raw])=>{const d=UrbeCore.service('documents').upsert({path,content});UrbeCore.commands.execute('document.open',{id:d.id,raw:!!raw});const el=document.getElementById('pageStudio');return{studioVisible:!!el&&!el.hidden}},args);break;
          }
          case 'linkTargets': {
            value=await a.page.evaluate(([title,pluginPath,pluginContent])=>{const docs=UrbeCore.service('documents');docs.upsert({path:pluginPath,content:pluginContent});const k=UrbeCore.service('knowledge');return{paths:[...(k.byTitle.get(title)||[])].map(id=>docs.get(id).path)}},args);break;
          }
          case 'rename': {
            const id=ids.get(args[0]);if(!id)throw new Error('referência UI desconhecida');
            await a.command('explorer.rename',{id,name:args[1]});await a.save();
            const notes=await a.notes(),newPath=Object.keys(notes).find(p=>notes[p].id===id);
            if(!newPath)throw new Error('rename não encontrou documento');
            ids.set(newPath,id);value={path:newPath};break;
          }
          case 'delete': await a.command('explorer.delete',{ids:[ids.get(args[0])]});await a.save();value=null;break;
          case 'restore': await a.command('trash.restore',{id:ids.get(args[0])});await a.save();value=null;break;
          case 'lifecycleObserve': {
            const p=args[0],id=ids.get(p);if(!id)throw new Error('referência UI desconhecida');
            const notes=await a.notes(),vault=await a.vault();
            const trash=JSON.parse(vault['.urbe/trash.v2.json']||'{"items":[]}');
            value={content:notes[p]?.content??null,saved:vault[p]??null,idStable:notes[p]?.id===id,inTrash:trash.items.some(i=>i.document.id===id)};break;
          }
          case 'rememberPosition': {
            const vault=await a.vault(),note=JSON.parse(vault['.urbe/mapa.json']).notas[args[0]];
            if(!note)throw new Error('posição persistida ausente');
            positions.set(args[0],{x:note.x,y:note.y});
            value={positionPresent:Number.isFinite(note.x)&&Number.isFinite(note.y),mapIdStable:note.id===ids.get(args[0]),oldAbsent:!Object.hasOwn(vault,args[1])};break;
          }
          case 'positionStable': {
            if(!positions.has(args[0]))throw new Error('posição de referência ausente');
            const building=(await a.world()).buildings.find(b=>b.id===ids.get(args[0]));
            value={stable:!!building&&JSON.stringify(building.pos)===JSON.stringify(positions.get(args[0]))};break;
          }
          case 'editHistory': {
            const id=ids.get(args[0]);if(!id)throw new Error('referência UI desconhecida');
            value=await a.page.evaluate(([id,content])=>{const docs=UrbeCore.service('documents'),hist=UrbeCore.service('history'),d=docs.get(id);docs.upsert({...d,content},{source:'parity'});return{historyPresent:(hist.list?hist.list(id):(hist.byDoc.get(id)||[])).length>=1}},[id,args[1]]);await a.save();break;
          }
          case 'gc': {
            const [decision,old,keep]=args;if(!['cancel','apply'].includes(decision))throw new Error('decisão GC inválida');
            const palette=await a.page.evaluate(()=>UrbeCore.commands.list().filter(c=>c.enabled({source:'palette'})).map(c=>c.id));
            await a.page.evaluate(()=>{window.__parityGc=UrbeCore.commands.execute('workspace.cleanOrphans',{source:'palette'})});
            await a.page.waitForSelector('.udlg [data-primary]');const msg=await a.page.textContent('.udlg-msg');
            await a.page.click(decision==='apply'?'.udlg [data-primary]':'.udlg [data-cancel].ui-btn');await a.page.evaluate(()=>window.__parityGc);await a.save();
            value=await a.page.evaluate(([old,keep])=>{const hist=UrbeCore.service('history').export().documents,cmp=UrbeCore.service('compositions').export();return{oldPresent:Object.hasOwn(hist,old),kept:keep.map(id=>Object.hasOwn(hist,id)),sources:cmp.items[0].sources}},[old,keep]);
            const vault=await a.vault();const maintenance=JSON.parse(vault['.urbe/vault.json']).maintenance||[];
            value={...value,maintenanceRecorded:maintenance.some(m=>m.kind==='gc'),confirmationExplains:msg.includes('histórico')&&msg.includes('composição'),paletteSafe:palette.includes('workspace.cleanOrphans')&&!palette.includes('workspace.gc')};break;
          }
          default:throw new Error('ação UI desconhecida');
        }
        values.push(value);
      }
      a.expectNoErrors();await a.page.context().close();return{values};
    },
    close:()=>rt.close()
  };
}
