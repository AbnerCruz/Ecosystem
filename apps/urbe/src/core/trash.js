(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents');if(!core||!docs)return;
  class TrashStore{
    constructor(events,store){this.events=events;this.store=store;this.items=new Map()}
    trash(idOrPath,meta){var d=this.store.get(idOrPath);if(!d)return null;var item={document:{...d,properties:{...d.properties},tags:[...d.tags],links:[...d.links]},deletedAt:Date.now(),originalPath:d.path};this.items.set(d.id,item);this.store.remove(d.id,{source:'trash',meta:meta||null});this.events.emit('trash:changed',{type:'trash',item:item});return item}
    restore(id,options){var item=this.items.get(String(id));if(!item)return null;var path=(options&&options.path)||item.originalPath,existing=this.store.get(path);if(existing)throw new Error('Já existe um documento em '+path);var d=this.store.upsert({...item.document,id:item.document.id,path:path},{source:'trash.restore'});this.items.delete(String(id));this.events.emit('trash:changed',{type:'restore',document:d});return d}
    purge(id){var ok=this.items.delete(String(id));if(ok)this.events.emit('trash:changed',{type:'purge',id:String(id)});return ok}
    clear(){this.items.clear();this.events.emit('trash:changed',{type:'clear'})}
    list(){return Array.from(this.items.values()).sort((a,b)=>b.deletedAt-a.deletedAt)}
    export(){return{version:1,items:this.list()}}
    import(data){this.items.clear();((data&&data.items)||[]).forEach(item=>{if(item&&item.document&&item.document.id)this.items.set(String(item.document.id),item)});this.events.emit('trash:changed',{type:'load'});return this.list()}
  }
  var trash=new TrashStore(core.events,docs);core.provide('trash',trash);
  core.commands.register('trash.restore',{title:'Restaurar da lixeira',category:'Arquivos',execute:function(ctx){return trash.restore(ctx&&ctx.id,ctx)}});
  core.commands.register('trash.purge',{title:'Excluir permanentemente',category:'Arquivos',execute:function(ctx){return trash.purge(ctx&&ctx.id)}});
  global.UrbeTrash={TrashStore:TrashStore};
})(window);
