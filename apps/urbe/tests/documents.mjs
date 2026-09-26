import fs from 'node:fs';
import vm from 'node:vm';
const context={window:{}};vm.createContext(context);
for(const file of ['src/core/core.js','src/core/documents.js','src/core/knowledge-index.js']){
  vm.runInContext(fs.readFileSync(new URL('../'+file,import.meta.url),'utf8'),context);
}
const core=context.window.UrbeCore,docs=core.service('documents'),knowledge=core.service('knowledge');
docs.replaceAll([
 {path:'História/Roma.md',content:'---\ntags: [historia, roma]\n---\n# Roma\nVeja [[César]].'},
 {path:'História/César.md',content:'# César\nLigado a [[Roma]]. #biografia'},
 {path:'Inbox.md',content:'Texto sem links'}
],{source:'test'});
if(docs.list().length!==3)throw new Error('document count');
if(docs.get('História/Roma.md').title!=='Roma')throw new Error('title derivation');
if(!docs.get('História/Roma.md').links.includes('César'))throw new Error('wikilink parsing');
if(knowledge.links('História/Roma.md')[0]?.title!=='César')throw new Error('outgoing graph');
if(knowledge.backlinks('História/Roma.md')[0]?.title!=='César')throw new Error('backlink graph');
if(!knowledge.tagged('biografia').some(d=>d.title==='César'))throw new Error('tag index');
if(!knowledge.search('Roma').some(d=>d.title==='Roma'))throw new Error('search index');
docs.upsert({path:'Inbox.md',content:'Agora [[Roma]]'});
if(!knowledge.links('Inbox.md').some(d=>d.title==='Roma'))throw new Error('incremental reindex');
console.log('OK   canonical documents');
console.log('OK   wikilinks and backlinks');
console.log('OK   tags');
console.log('OK   local search');
console.log('OK   reindex on document update');
