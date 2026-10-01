(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents');if(!core||!docs)return;
  function dir(path){var i=String(path).lastIndexOf('/');return i<0?'':path.slice(0,i)}
  class WorldProjection{
    constructor(events,store){this.events=events;this.store=store;this.spatial=new Map();this.regions=new Map();this.enabled=true;this.revision=0;var self=this;events.on('document:updated',function(evt){if(!evt||!evt.previous||!evt.document||evt.previous.path===evt.document.path)return;var spatial=self.spatial.get(evt.previous.path);if(spatial){self.spatial.delete(evt.previous.path);self.spatial.set(evt.document.path,spatial);self.revision++;self.events.emit('world:projection',{type:'path-migrated',documentId:evt.document.id,from:evt.previous.path,to:evt.document.path,revision:self.revision})}})}
    load(metadata){
      this.spatial.clear();this.regions.clear();var m=metadata||{};
      (m.regioes||[]).forEach(r=>{if(r.caminho)this.regions.set(r.caminho,{...r})});
      var notes=m.notas||{};Object.keys(notes).forEach(path=>this.spatial.set(path,{...notes[path]}));this.revision++;this.events.emit('world:projection',{type:'load',revision:this.revision});return this.snapshot()
    }
    projectDocument(idOrPath){
      var d=this.store.get(idOrPath);if(!d)return null;var saved=this.spatial.get(d.path)||{};
      return{id:d.id,documentId:d.id,path:d.path,title:d.title,folder:dir(d.path),x:Number.isFinite(saved.x)?saved.x:null,y:Number.isFinite(saved.y)?saved.y:null,sprite:saved.sprite||null,tags:d.tags||[],created:d.created||saved.criado||null,modified:d.modified||saved.modificado||null}
    }
    documents(){return this.store.list().map(d=>this.projectDocument(d.id))}
    region(path){return this.regions.get(path)||null}
    setSpatial(idOrPath,patch){
      var d=this.store.get(idOrPath);if(!d)return null;var prev=this.spatial.get(d.path)||{},next={...prev,...patch};this.spatial.set(d.path,next);this.revision++;this.events.emit('world:spatialChanged',{document:d,spatial:{...next},revision:this.revision});return this.projectDocument(d.id)
    }
    metadata(base){
      var out={...(base||{})},notes={};this.store.list().forEach(d=>{var s=this.spatial.get(d.path)||{};notes[d.path]={...s,id:d.id,tags:d.tags||[],criado:d.created||s.criado||null,modificado:d.modified||s.modificado||null}});out.notas=notes;out.regioes=Array.from(this.regions.values()).map(r=>({...r}));return out
    }
    snapshot(){return{enabled:this.enabled,revision:this.revision,documents:this.documents(),regions:Array.from(this.regions.values()).map(r=>({...r}))}}
    setEnabled(value){this.enabled=value!==false;this.events.emit('world:enabled',{enabled:this.enabled});return this.enabled}
  }
  var world=new WorldProjection(core.events,docs);core.provide('world.projection',world);
  core.commands.register('world.enable',{title:'Ativar aquário',category:'Aquário',execute:function(){return world.setEnabled(true)}});
  core.commands.register('world.disable',{title:'Desativar aquário',category:'Aquário',execute:function(){return world.setEnabled(false)}});
  core.commands.register('world.moveDocument',{title:'Mover representação espacial',category:'Aquário',execute:function(ctx){return world.setSpatial(ctx&&ctx.id,{x:ctx&&ctx.x,y:ctx&&ctx.y})}});
  global.UrbeWorldProjection={WorldProjection:WorldProjection};
})(window);
