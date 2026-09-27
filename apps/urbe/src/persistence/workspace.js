(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents'),trash=core&&core.service('trash'),history=core&&core.service('history'),compositions=core&&core.service('compositions');if(!core||!docs)return;
  class WorkspacePersistence{
    constructor(events,store){this.events=events;this.store=store;this.adapter=null;this.vault=null;this.meta=null;this.snapshot=new Map();this.busy=false;this.pending=false;this.timer=null;this.delay=900;this.suspended=true;this.state='idle';this.lastSavedAt=null}
    configure(adapter){this.adapter=adapter;return this}
    async load(vault){
      if(!this.adapter)throw new Error('Persistence adapter not configured');this.suspended=true;this.vault=vault;
      var paths=await this.adapter.list(vault),md=paths.filter(function(p){return /\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i.test(p)&&!p.split('/').some(function(part){return part.startsWith('.')})});
      var meta=null;try{meta=JSON.parse(await this.adapter.read(vault,'.urbe/mapa.json')||'null')}catch(_){}
      var journal=null;try{journal=JSON.parse(await this.adapter.read(vault,'.urbe/journal.json')||'null')}catch(_){}
      if(trash){try{trash.import(JSON.parse(await this.adapter.read(vault,'.urbe/trash.json')||'null'))}catch(_){trash.import(null)}}
      if(history){try{history.import(JSON.parse(await this.adapter.read(vault,'.urbe/history.json')||'null'))}catch(_){history.import(null)}}
      if(compositions){try{compositions.import(JSON.parse(await this.adapter.read(vault,'.urbe/compositions.json')||'null'))}catch(_){compositions.import(null)}}
      var notesMeta=(meta&&meta.notas)||{},items=[];
      for(var i=0;i<md.length;i++){var path=md[i],m=notesMeta[path]||{};items.push({id:m.id||null,path:path,content:(await this.adapter.read(vault,path))||'',tags:m.tags||null,created:m.criado||null,modified:m.modificado||null})}
      var physical=new Map(items.map(function(d){return[d.path,d.content]}));for(const path of ['.urbe/mapa.json','.urbe/trash.json','.urbe/history.json','.urbe/compositions.json'])if(paths.includes(path))physical.set(path,await this.adapter.read(vault,path));
      if(journal&&journal.version===1&&Array.isArray(journal.documents)){items=journal.documents.filter(function(d){return d&&d.path});if(compositions&&journal.compositions)compositions.import(journal.compositions);if(journal.metadata)meta=journal.metadata;if(trash&&journal.trash)trash.import(journal.trash);if(history&&journal.history)history.import(journal.history);this.events.emit('workspace:recovered',{vault:vault,count:items.length,timestamp:journal.timestamp||null})}
      this.store.replaceAll(items,{source:'persistence.load',vault:vault});this.meta=meta||{};this.snapshot=physical;this.suspended=false;
      if(journal){await this.flush(this.meta)}
      this.events.emit('workspace:loaded',{vault:vault,documents:this.store.list(),metadata:meta,paths:paths});return{vault:vault,documents:this.store.list(),metadata:meta,paths:paths}
    }
    desired(metadata){var files=new Map();this.store.list().forEach(function(d){files.set(d.path,d.content)});if(metadata!==undefined)files.set('.urbe/mapa.json',JSON.stringify(metadata||{},null,1));if(trash)files.set('.urbe/trash.json',JSON.stringify(trash.export(),null,1));if(history)files.set('.urbe/history.json',JSON.stringify(history.export()));if(compositions)files.set('.urbe/compositions.json',JSON.stringify(compositions.export()));return files}
    schedule(){if(this.suspended||!this.vault||!this.adapter)return;this.pending=true;this.state='dirty';this.events.emit('workspace:dirty',{vault:this.vault});if(this.timer)return;var self=this;this.timer=setTimeout(function(){self.timer=null;self.flush()},this.delay)}
    async flush(metadata){
      if(this.suspended||!this.vault||!this.adapter)return false;if(this.busy){this.pending=true;return false}this.busy=true;this.pending=true;
      try{while(this.pending){this.pending=false;var desired=this.desired(metadata===undefined?this.meta:metadata);this.state='saving';this.events.emit('workspace:saving',{vault:this.vault});
        var changed=[];for(const pair of desired)if(this.snapshot.get(pair[0])!==pair[1]&&!pair[0].startsWith('.urbe/journal'))changed.push(pair[0]);
        var removed=Array.from(this.snapshot.keys()).filter(path=>!desired.has(path)&&!path.startsWith('.urbe/'));
        if(changed.length||removed.length){var journal={version:1,timestamp:Date.now(),documents:this.store.list().map(function(d){return{id:d.id,path:d.path,content:d.content,tags:d.tags,created:d.created,modified:d.modified}}),metadata:metadata===undefined?this.meta:metadata,trash:trash?trash.export():null,history:history?history.export():null,compositions:compositions?compositions.export():null};await this.adapter.write(this.vault,'.urbe/journal.json',JSON.stringify(journal))}
        for(const pair of desired){if(this.snapshot.get(pair[0])!==pair[1]){await this.adapter.write(this.vault,pair[0],pair[1]);this.snapshot.set(pair[0],pair[1])}}
        for(const path of Array.from(this.snapshot.keys()))if(!desired.has(path)&&path!=='.urbe/journal.json'){await this.adapter.remove(this.vault,path);this.snapshot.delete(path)}
        if(changed.length||removed.length)await this.adapter.remove(this.vault,'.urbe/journal.json');
      }this.state='saved';this.lastSavedAt=Date.now();this.events.emit('workspace:saved',{vault:this.vault,savedAt:this.lastSavedAt});return true}catch(error){this.state='error';this.events.emit('workspace:saveError',{vault:this.vault,error:error});throw error}finally{this.busy=false}}
    suspend(value){this.suspended=value!==false}
    /* Relê o que mudou na pasta por fora do app (Explorer, Obsidian, OneDrive, outro aparelho).
       Uma mudança de fora só entra se a nota não tem edição local ainda não gravada: nada se perde.
       paths: só esses caminhos (o app de computador avisa quais mudaram); sem paths: a pasta toda. */
    async syncFromDisk(paths){
      if(this.suspended||!this.vault||!this.adapter)return 0;
      if(this.busy||this.timer){var self=this;return new Promise(function(ok){setTimeout(function(){self.syncFromDisk(paths).then(ok,function(){ok(0)})},1200)})}
      var editable=function(p){return /\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i.test(p)&&!p.split('/').some(function(part){return part.startsWith('.')})};
      var all=!paths,onDisk;
      if(all){onDisk=new Set((await this.adapter.list(this.vault)).filter(editable));paths=Array.from(onDisk)}
      else paths=paths.map(function(p){return String(p).replace(/\\/g,'/')}).filter(editable);
      var byPath=new Map(this.store.list().map(function(d){return[d.path,d]})),changed=0;
      for(var i=0;i<paths.length;i++){var path=paths[i],disk=await this.adapter.read(this.vault,path),snap=this.snapshot.get(path),d=byPath.get(path);
        if(disk==null){if(!all&&d&&d.content===snap){this.snapshot.delete(path);this.store.remove(d.id,{source:'disk'});changed++}continue}
        if(disk===snap)continue;
        if(d&&snap!==undefined&&d.content!==snap)continue;
        this.snapshot.set(path,disk);
        if(d){if(d.content!==disk){this.store.upsert(Object.assign({},d,{content:disk}),{source:'disk'});changed++}}
        else{this.store.upsert({path:path,content:disk},{source:'disk'});changed++}}
      if(all)for(const pair of Array.from(this.snapshot)){var pth=pair[0];if(!editable(pth)||onDisk.has(pth))continue;var doc=byPath.get(pth);if(doc&&doc.content===pair[1]){this.snapshot.delete(pth);this.store.remove(doc.id,{source:'disk'});changed++}}
      if(changed)this.events.emit('workspace:external',{vault:this.vault,changed:changed});
      return changed;
    }
  }
  var service=new WorkspacePersistence(core.events,docs);core.provide('persistence',service);
  if(trash)core.events.on('trash:changed',function(){service.schedule()});if(history)core.events.on('history:changed',function(){service.schedule()});if(compositions){core.events.on('composition:created',function(){service.schedule()});core.events.on('composition:updated',function(){service.schedule()});core.events.on('composition:removed',function(){service.schedule()})}
  core.events.on('document:created',function(){service.schedule()});core.events.on('document:updated',function(){service.schedule()});core.events.on('document:removed',function(){service.schedule()});
  /* O debounce não pode perder a última operação quando o app é fechado ou vai para segundo plano. */
  function flushPending(){if(!service.timer&&!service.pending)return;if(service.timer){clearTimeout(service.timer);service.timer=null}service.flush().catch(function(){})}
  if(global.addEventListener)global.addEventListener('pagehide',flushPending);
  if(global.document&&global.document.addEventListener)global.document.addEventListener('visibilitychange',function(){if(global.document.hidden)flushPending()});
  core.commands.register('workspace.flush',{title:'Salvar workspace',category:'Workspace',execute:function(){return service.flush()}});
  global.UrbePersistence={WorkspacePersistence:WorkspacePersistence};
})(window);
