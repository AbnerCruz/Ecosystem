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
      const a=await rt.open(input.files?{seed:new Map(Object.entries(input.files)),docs:Object.keys(input.files).length}:{docs:40}),ids=new Map(),values=[];
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
          case 'route': {
            value=await a.page.evaluate(([path,content,raw])=>{const d=UrbeCore.service('documents').upsert({path,content});UrbeCore.commands.execute('document.open',{id:d.id,raw:!!raw});const el=document.getElementById('pageStudio');return{studioVisible:!!el&&!el.hidden}},args);break;
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
