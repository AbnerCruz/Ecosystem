import fs from 'node:fs';import vm from 'node:vm';
const read=p=>fs.readFileSync(new URL('../'+p,import.meta.url),'utf8');
function test(name,fn){return Promise.resolve().then(fn).then(()=>console.log('OK  ',name),e=>{console.error('FAIL',name,e.message);process.exitCode=1})}

await test('pagehide grava operação pendente sem esperar o debounce',async()=>{
  const files=new Map(),listeners={},docListeners={};
  const adapter={async list(){return[]},async read(v,p){return files.get(p)??null},async write(v,p,c){files.set(p,c)},async remove(v,p){files.delete(p)}};
  const document={hidden:false,addEventListener(t,f){(docListeners[t]||=[]).push(f)}};
  const window={document,addEventListener(t,f){(listeners[t]||=[]).push(f)}};
  const context={window,setTimeout,clearTimeout};vm.createContext(context);
  for(const f of ['src/core/artifacts.js','src/core/core.js','src/core/documents.js','src/core/trash.js','src/persistence/vault-meta.js','src/persistence/backup.js','src/persistence/identity.js','src/persistence/workspace.js'])vm.runInContext(read(f),context);
  const core=window.UrbeCore,p=core.service('persistence'),docs=core.service('documents');
  p.configure(adapter);await p.load('V');
  docs.upsert({path:'A.md',content:'alpha'});
  if(!p.timer)throw new Error('save deveria estar agendado');
  listeners.pagehide.forEach(f=>f());
  await new Promise(r=>setTimeout(r,20));
  if(files.get('A.md')!=='alpha')throw new Error('pagehide não gravou');
  if(p.timer)throw new Error('timer pendente não foi limpo');
  docs.upsert({path:'B.md',content:'beta'});document.hidden=true;docListeners.visibilitychange.forEach(f=>f());
  await new Promise(r=>setTimeout(r,20));
  if(files.get('B.md')!=='beta')throw new Error('segundo plano não gravou');
});

await test('editor visual não grava placeholder nem ressuscita nota excluída',()=>{
  const app=read('src/app.js'),css=read('src/styles/base.css');
  if(app.includes('<p style="color:#8296a3">Esta nota está vazia.</p>'))throw new Error('placeholder editável no conteúdo');
  if(!css.includes('#renderedPreview.isEmpty::before'))throw new Error('placeholder visual ausente');
  if(!/var oldVisualSync=syncVisualToMarkdown;[\s\S]{0,200}syncBuilding\(currentFile,'editor.input'\)/.test(app))throw new Error('modo Visual não sincroniza DocumentStore');
  if(!app.includes('renderedPreview.addEventListener("input",()=>'))throw new Error('listener usa função antiga');
  if(!app.includes("if(b.documentId&&!docs.get(b.documentId))return null;"))throw new Error('syncBuilding pode recriar nota excluída');
});

await test('primeira instalação do service worker não recarrega a página',()=>{
  const app=read('src/app.js');
  if(!/controllerchange',\(\)=>\{if\(recarregando\|\|!tinhaControlador\)return;/.test(app))throw new Error('reload incondicional em controllerchange');
});
