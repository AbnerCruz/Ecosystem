(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents'),explorer=core&&core.service('explorer');if(!core||!docs||!explorer)return;
  function norm(p){return String(p||'').replace(/\\/g,'/').replace(/^\/+|\/+$/g,'').replace(/\/+/g,'/')}
  function safeName(s){return String(s||'').replace(/[\\/:*?"<>|\u0000-\u001f]/g,'-').replace(/\s+/g,' ').trim().replace(/^\.+|\.+$/g,'').slice(0,90)||'sem-nome'}
  function unique(path,exceptId){var base=norm(path),i=2,candidate=base,lower;while((lower=docs.get(candidate))&&lower.id!==exceptId){var dot=base.lastIndexOf('.'),stem=dot>base.lastIndexOf('/')?base.slice(0,dot):base,ext=dot>base.lastIndexOf('/')?base.slice(dot):'';candidate=stem+' ('+(i++)+')'+ext}return candidate}
  function rename(ctx){var doc=docs.get(ctx&&ctx.id);if(!doc)return null;var name=safeName(ctx.name).replace(/\.md$/i,''),dir=doc.path.includes('/')?doc.path.slice(0,doc.path.lastIndexOf('/')):'',path=unique((dir?dir+'/':'')+name+'.md',doc.id);var next=docs.upsert({...doc,id:doc.id,path:path,title:name},{source:'explorer.rename'});core.events.emit('explorer:operation',{type:'rename',from:doc,to:next});return next}
  function move(ctx){var doc=docs.get(ctx&&ctx.id);if(!doc)return null;var folder=norm(ctx.folder||''),file=doc.path.split('/').pop(),path=unique((folder?folder+'/':'')+file,doc.id);var next=docs.upsert({...doc,id:doc.id,path:path},{source:'explorer.move'});core.events.emit('explorer:operation',{type:'move',from:doc,to:next});return next}
  function duplicate(ctx){var doc=docs.get(ctx&&ctx.id);if(!doc)return null;var dot=doc.path.lastIndexOf('.'),stem=dot>=0?doc.path.slice(0,dot):doc.path,ext=dot>=0?doc.path.slice(dot):'',path=unique(stem+' cópia'+ext);var next=docs.upsert({...doc,id:null,path:path,title:path.split('/').pop().replace(/\.(md|markdown)$/i,'')},{source:'explorer.duplicate'});core.events.emit('explorer:operation',{type:'duplicate',from:doc,to:next});return next}
  function remove(ctx){var ids=(ctx&&ctx.ids)||explorer.selected().filter(x=>x.kind==='document').map(x=>x.id);var removed=[];ids.forEach(function(id){var d=docs.get(id);if(d&&docs.remove(id,{source:'explorer.delete'}))removed.push(d)});explorer.clear();core.events.emit('explorer:operation',{type:'delete',documents:removed});return removed}
  core.commands.register('explorer.rename',{title:'Renomear',category:'Arquivos',execute:rename});
  core.commands.register('explorer.move',{title:'Mover',category:'Arquivos',execute:move});
  core.commands.register('explorer.duplicate',{title:'Duplicar',category:'Arquivos',execute:duplicate});
  core.commands.register('explorer.delete',{title:'Excluir',category:'Arquivos',execute:remove});
  core.provide('explorer.operations',{rename:rename,move:move,duplicate:duplicate,remove:remove});
})(window);
