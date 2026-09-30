import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';

const source=file=>fs.readFileSync(new URL('../'+file,import.meta.url),'utf8');
const index=source('index.html');
const scripts=[...index.matchAll(/<script[^>]+src="\.\/([^"]+)"/g)].map(x=>x[1]);
for(const file of ['src/composition/store.js','src/composition/compiler.js','src/composition/ui.js'])
  assert.equal(scripts.filter(x=>x===file).length,1,`${file} carregado mais de uma vez`);

const ctx={window:{},Date,Math,setTimeout,clearTimeout,console};vm.createContext(ctx);
for(const file of ['src/core/artifacts.js','src/core/core.js','src/core/documents.js','src/core/trash.js','src/core/history.js','src/core/knowledge-index.js','src/composition/store.js','src/composition/compiler.js','src/persistence/vault-meta.js','src/persistence/backup.js','src/persistence/identity.js','src/persistence/workspace.js'])vm.runInContext(source(file),ctx,{filename:file});
const core=ctx.window.UrbeCore,docs=core.service('documents'),p=core.service('persistence'),indexer=core.service('knowledge');
const vaults=new Map([['A',new Map([['Nota.md','[[Outra]] #antiga'],['Outra.md','Destino']])],['B',new Map([['Nota.md','Outro vault']])]]);
const adapter={async list(v){return [...vaults.get(v).keys()]},async read(v,f){return vaults.get(v).get(f)??null},async write(v,f,x){vaults.get(v).set(f,x)},async remove(v,f){vaults.get(v).delete(f)}};
p.configure(adapter);await p.load('A');let note=docs.get('Nota.md');
assert.equal(indexer.links(note.id)[0].title,'Outra');
docs.upsert({...note,content:'[[Outra]] #nova'});assert.deepEqual(Array.from(docs.get(note.id).tags),['nova']);
const id=docs.get('Nota.md').id;await p.flush({notas:{'Nota.md':{id},'Outra.md':{id:docs.get('Outra.md').id}}});
await p.load('B');assert.equal(docs.list().length,1);assert.equal(docs.get('Nota.md').content,'Outro vault');
await p.load('A');assert.equal(docs.get('Nota.md').id,id);

const a=vaults.get('A');a.set('Nota.md','old physical');a.set('removed.md','obsolete');a.set('.urbe/journal.json',JSON.stringify({version:1,documents:[{id,path:'Nota.md',content:'recovered'},{id:'fresh',path:'new.md',content:'fresh'}],metadata:{notas:{'Nota.md':{id},'new.md':{id:'fresh'}}}}));
await p.load('A');assert.equal(a.get('Nota.md'),'recovered');assert.equal(a.get('new.md'),'fresh');assert.equal(a.has('removed.md'),false);assert.equal(a.has('.urbe/journal.json'),false);
await p.load('A');assert.equal(docs.get('Nota.md').content,'recovered');

const app=source('src/app.js'); // migração multi-cidade: tests/multi-city.mjs
const backupFiles=[],backups=new Map([['.urbe/mapa.json',new Blob(['{}'])],['Cidades/Cidade A/capitulo.md',new Blob(['texto'])],['Cidades/Cidade A/imagem.png',new Blob(['imagem'])]]);
const ex={Disco:{cidade:'Urbe'},V21_VERSION:'teste',window:{crypto:globalThis.crypto,UrbeCore:{service:()=>({busy:false,flush:async()=>{}})}},FS:{listar:async()=>[...backups.keys()],lerBlob:async(v,k)=>backups.get(k)},rodarSinc:async()=>{},estadoDesejado:()=>({binarios:new Map([['Cidades/Cidade A/imagem.png',{}]])}),exigirJSZip:async()=>class{file(k){backupFiles.push(k)}async generateAsync(){return new Blob(['zip'])}},baixarBlob:()=>{},toast:()=>{},Blob,setTimeout,console};vm.createContext(ex);vm.runInContext(source('src/persistence/export-manifest.js'),ex);const exportStart=app.indexOf('async function exportarVault(){'),exportEnd=app.indexOf('\nfunction baixarBlob',exportStart);vm.runInContext(app.slice(exportStart,exportEnd),ex);await vm.runInContext('exportarVault()',ex);assert.deepEqual(backupFiles,[...backups.keys(),'urbe-export.json'],'todos os arquivos + manifesto');
const ui=source('src/explorer/mobile-ui.js'),gesture=ui.slice(ui.indexOf('  function bindRows()'),ui.indexOf('  function renderBar()'));
const handlers={},row={getAttribute:k=>k==='data-id'?'n':'document',addEventListener:(name,fn)=>{handlers[name]=fn}},touch={hold:null,HOLD:430,list:{querySelectorAll:()=>[row]},model:{selection:new Set(),select(){touch.selected=(touch.selected||0)+1}},activate:(...a)=>touch.openId(a[0]),navigator:{},setTimeout,clearTimeout,opened:0};touch.openId=()=>touch.opened++;
vm.createContext(touch);vm.runInContext(gesture+';bindRows()',touch);touch.Date=Date;handlers.pointerdown({clientX:20,clientY:20,pointerId:1});handlers.pointermove({clientX:23,clientY:21,pointerId:1});handlers.pointerup({pointerType:'touch',pointerId:1});handlers.click({preventDefault(){}});assert.equal(touch.opened,1);
/* toque longo seleciona e o click seguinte não abre a nota */
await new Promise(r=>{handlers.pointerdown({clientX:20,clientY:20,pointerId:2});setTimeout(r,480)});handlers.pointerup({pointerType:'touch',pointerId:2});handlers.click({preventDefault(){}});assert.equal(touch.opened,1);assert.equal(touch.selected,1);
console.log('OK   produção: scripts únicos, metadados derivados, isolamento, recovery, migração, backup e toque');
