// UC-2: ações semânticas em dados; seletores ficam exclusivamente no adapter JS.
import fs from 'node:fs';
import path from 'node:path';
import {ROOT} from './v2-docs.mjs';
import {startRuntime} from '../../tests/e2e/app-runtime.mjs';
export function makeUiCases(){return JSON.parse(fs.readFileSync(path.join(ROOT,'docs/csharp/acceptance/ui-cases.json'),'utf8')).cases}
export async function createBrowserReference(){
  const rt=await startRuntime();let visualApp;
  return{
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
          case 'create': {const id=await a.createNote(...args);ids.set(args[0],id);positions.set(args[0],(await a.world()).buildings.find(b=>b.id===id)?.pos);value={idPresent:typeof id==='string'&&!!id};break}
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
          case 'route': {
            value=await a.page.evaluate(([path,content,raw])=>{const d=UrbeCore.service('documents').upsert({path,content});UrbeCore.commands.execute('document.open',{id:d.id,raw:!!raw});const el=document.getElementById('pageStudio');return{studioVisible:!!el&&!el.hidden}},args);break;
          }
          case 'rename': {
            const id=ids.get(args[0]);if(!id)throw new Error('referência UI desconhecida');
            await a.command('explorer.rename',{id,name:args[1]});await a.save();
            const notes=await a.notes(),newPath=Object.keys(notes).find(p=>notes[p].id===id);
            if(!newPath)throw new Error('rename não encontrou documento');
            ids.set(newPath,id);positions.set(newPath,positions.get(args[0]));value={path:newPath};break;
          }
          case 'delete': await a.command('explorer.delete',{ids:[ids.get(args[0])]});await a.save();value=null;break;
          case 'restore': await a.command('trash.restore',{id:ids.get(args[0])});await a.save();value=null;break;
          case 'lifecycleObserve': {
            const p=args[0],id=ids.get(p);if(!id)throw new Error('referência UI desconhecida');
            const notes=await a.notes(),vault=await a.vault(),world=await a.world(),building=world.buildings.find(b=>b.id===id);
            const trash=JSON.parse(vault['.urbe/trash.v2.json']||'{"items":[]}');
            value={content:notes[p]?.content??null,saved:vault[p]??null,idStable:notes[p]?.id===id,inTrash:trash.items.some(i=>i.document.id===id),positionStable:!!building&&JSON.stringify(building.pos)===JSON.stringify(positions.get(p))};break;
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
            value={...value,confirmationExplains:msg.includes('histórico')&&msg.includes('composição'),paletteSafe:palette.includes('workspace.cleanOrphans')&&!palette.includes('workspace.gc')};break;
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
