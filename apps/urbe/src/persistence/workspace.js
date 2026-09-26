(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents'),trash=core&&core.service('trash');if(!core||!docs)return;
  class WorkspacePersistence{
    constructor(events,store){this.events=events;this.store=store;this.adapter=null;this.vault=null;this.meta=null;this.snapshot=new Map();this.busy=false;this.pending=false;this.timer=null;this.delay=900;this.suspended=true;this.state='idle';this.lastSavedAt=null}
    configure(adapter){this.adapter=adapter;return this}
    async load(vault){
      if(!this.adapter)throw new Error('Persistence adapter not configured');this.suspended=true;this.vault=vault;
      var paths=await this.adapter.list(vault),md=paths.filter(function(p){return /\.(md|markdown)$/i.test(p)&&!p.startsWith('.urbe/')&&!p.split('/').pop().startsWith('.')});
      var meta=null;try{meta=JSON.parse(await this.adapter.read(vault,'.urbe/mapa.json')||'null')}catch(_){}\n      if(trash){try{trash.import(JSON.parse(await this.adapter.read(vault,'.urbe/trash.json')||'null'))}catch(_){trash.import(null)}}
      var notesMeta=(meta&&meta.notas)||{},items=[];
      for(var i=0;i<md.length;i++){var path=md[i],m=notesMeta[path]||{};items.push({id:m.id||null,path:path,content:(await this.adapter.read(vault,path))||'',created:m.criado||null,modified:m.modificado||null})}
      this.store.replaceAll(items,{source:'persistence.load',vault:vault});this.meta=meta||{};this.snapshot=new Map(items.map(function(d){return[d.path,d.content]}));this.suspended=false;
      this.events.emit('workspace:loaded',{vault:vault,documents:this.store.list(),metadata:meta,paths:paths});return{vault:vault,documents:this.store.list(),metadata:meta,paths:paths}
    }
    desired(metadata){var files=new Map();this.store.list().forEach(function(d){files.set(d.path,d.content)});if(metadata!==undefined)files.set('.urbe/mapa.json',JSON.stringify(metadata||{},null,1));if(trash)files.set('.urbe/trash.json',JSON.stringify(trash.export(),null,1));return files}
    schedule(){if(this.suspended||!this.vault||!this.adapter)return;this.pending=true;this.state='dirty';this.events.emit('workspace:dirty',{vault:this.vault});if(this.timer)return;var self=this;this.timer=setTimeout(function(){self.timer=null;self.flush()},this.delay)}
    async flush(metadata){
      if(this.suspended||!this.vault||!this.adapter)return false;if(this.busy){this.pending=true;return false}this.busy=true;this.pending=true;
      try{while(this.pending){this.pending=false;var desired=this.desired(metadata===undefined?this.meta:metadata);this.state='saving';this.events.emit('workspace:saving',{vault:this.vault});
        for(const pair of desired){if(this.snapshot.get(pair[0])!==pair[1]){await this.adapter.write(this.vault,pair[0],pair[1]);this.snapshot.set(pair[0],pair[1])}}
        for(const path of Array.from(this.snapshot.keys()))if(!desired.has(path)){await this.adapter.remove(this.vault,path);this.snapshot.delete(path)}
      }this.state='saved';this.lastSavedAt=Date.now();this.events.emit('workspace:saved',{vault:this.vault,savedAt:this.lastSavedAt});return true}catch(error){this.state='error';this.events.emit('workspace:saveError',{vault:this.vault,error:error});throw error}finally{this.busy=false}}
    suspend(value){this.suspended=value!==false}
  }
  var service=new WorkspacePersistence(core.events,docs);core.provide('persistence',service);
  if(trash)core.events.on('trash:changed',function(){service.schedule()});\n  core.events.on('document:created',function(){service.schedule()});core.events.on('document:updated',function(){service.schedule()});core.events.on('document:removed',function(){service.schedule()});
  core.commands.register('workspace.flush',{title:'Salvar workspace',category:'Workspace',execute:function(){return service.flush()}});
  global.UrbePersistence={WorkspacePersistence:WorkspacePersistence};
})(window);
