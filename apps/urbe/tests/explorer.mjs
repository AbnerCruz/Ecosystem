import fs from 'node:fs';import vm from 'node:vm';
const context={window:{},Date};vm.createContext(context);
for(const file of ['src/core/core.js','src/core/documents.js','src/explorer/model.js','src/explorer/operations.js'])vm.runInContext(fs.readFileSync(new URL('../'+file,import.meta.url),'utf8'),context);
const core=context.window.UrbeCore,docs=core.service('documents'),ex=core.service('explorer');
docs.replaceAll([{path:'A.md',content:'a'},{path:'Pasta/B.md',content:'b'},{path:'Pasta/Sub/C.md',content:'c'}]);
const a=docs.get('A.md'),b=docs.get('Pasta/B.md'),c=docs.get('Pasta/Sub/C.md');
if(!a.id.startsWith('doc_')||a.id===a.path)throw new Error('stable identity');
const tree=ex.tree();if(tree.children.length!==2||tree.children[0].kind!=='folder')throw new Error('tree');
ex.select(a.id,'replace');ex.select(b.id,'toggle');if(ex.selected().length!==2)throw new Error('multi selection');
ex.favorite(a.id,true);if(ex.listFavorites()[0]?.id!==a.id)throw new Error('favorite');
ex.touchRecent(b.id);if(ex.listRecent()[0]?.id!==b.id)throw new Error('recent');
let d=core.commands.execute('explorer.rename',{id:a.id,name:'Renomeada'});if(d.id!==a.id||!docs.get('Renomeada.md')||docs.get('A.md'))throw new Error('rename identity');
d=core.commands.execute('explorer.move',{id:a.id,folder:'Pasta'});if(d.id!==a.id||!docs.get('Pasta/Renomeada.md'))throw new Error('move identity');
d=core.commands.execute('explorer.duplicate',{id:b.id});if(d.content!=='b'||d.id===b.id)throw new Error('duplicate identity');
core.commands.execute('explorer.delete',{ids:[c.id]});if(docs.get(c.id))throw new Error('delete');
console.log('OK   stable document identity');console.log('OK   explorer tree');console.log('OK   multi selection');console.log('OK   favorites and recent');console.log('OK   rename move duplicate delete');
{ /* excluir pasta: conteúdo (inclusive subpastas) sai, a pasta some do modelo e o evento avisa a cidade */
  ex.addFolder('Vazia/Dentro');let evt=null;core.events.on('explorer:folderRemoved',e=>{evt=e});
  const r=core.commands.execute('explorer.deleteFolder',{path:'Pasta'});
  if(docs.list().some(x=>x.path.startsWith('Pasta/'))||r.documents.length<2)throw new Error('deleteFolder conteúdo');
  if(!evt||evt.path!=='Pasta'||ex.tree().children.some(n=>n.path==='Pasta'))throw new Error('deleteFolder evento/árvore');
  core.commands.execute('explorer.deleteFolder',{path:'Vazia'});if([...ex.folders].some(f=>f.startsWith('Vazia')))throw new Error('deleteFolder subpastas');
  console.log('OK   delete folder');
}
