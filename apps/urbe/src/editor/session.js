(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents');if(!core||!docs)return;

  class EditorSession {
    constructor(events,store,options){
      this.events=events;this.store=store;this.tabs=[];this.activeId=null;this.maxTabs=(options&&options.maxTabs)||12;
      this.history=new Map();this.historyLimit=(options&&options.historyLimit)||100;
    }
    open(idOrPath,options){
      var doc=this.store.get(idOrPath);if(!doc)return null;
      var existing=this.tabs.find(t=>t.id===doc.id);
      if(!existing){this.tabs.push({id:doc.id,pinned:!!(options&&options.pinned)});if(this.tabs.length>this.maxTabs){var i=this.tabs.findIndex(t=>!t.pinned&&t.id!==this.activeId);if(i>=0)this.tabs.splice(i,1)}}
      this.activeId=doc.id;this.ensureHistory(doc);this.events.emit('editor:session',{type:'open',document:doc,tabs:this.listTabs(),activeId:this.activeId});return doc;
    }
    close(id){
      var resolved=this.store.get(id),target=resolved?resolved.id:id;var i=this.tabs.findIndex(t=>t.id===target);if(i<0)return false;var was=this.activeId===target;this.tabs.splice(i,1);
      if(was)this.activeId=this.tabs[Math.min(i,this.tabs.length-1)]?.id||null;
      this.events.emit('editor:session',{type:'close',id:target,tabs:this.listTabs(),activeId:this.activeId});return true;
    }
    activate(id){var d=this.store.get(id),target=d?d.id:id;if(!this.tabs.some(t=>t.id===target))return this.open(id);this.activeId=target;this.events.emit('editor:session',{type:'activate',id:target,tabs:this.listTabs(),activeId:target});return this.store.get(target);}
    active(){return this.activeId?this.store.get(this.activeId):null;}
    listTabs(){return this.tabs.map(t=>({id:t.id,pinned:t.pinned,document:this.store.get(t.id)})).filter(t=>t.document);}
    pin(id,value){var t=this.tabs.find(x=>x.id===id);if(!t)return false;t.pinned=value!==false;this.events.emit('editor:session',{type:'pin',id:id,tabs:this.listTabs(),activeId:this.activeId});return true;}
    ensureHistory(doc){if(!this.history.has(doc.id))this.history.set(doc.id,{past:[],present:doc.content,future:[],lastAt:0});return this.history.get(doc.id);}
    record(idOrPath,content,meta){
      var doc=this.store.get(idOrPath);if(!doc)return null;var h=this.ensureHistory(doc),next=String(content||''),now=Date.now();
      if(next===h.present)return doc;
      if(h.present!==undefined)h.past.push(h.present);if(h.past.length>this.historyLimit)h.past.shift();
      h.present=next;h.future=[];h.lastAt=now;
      var updated=this.store.upsert({...doc,content:next,modified:meta&&meta.modified||doc.modified},{source:meta&&meta.source||'editor'});
      this.events.emit('editor:changed',{document:updated,canUndo:h.past.length>0,canRedo:false});return updated;
    }
    undo(idOrPath){
      var doc=this.store.get(idOrPath||this.activeId);if(!doc)return null;var h=this.ensureHistory(doc);if(!h.past.length)return doc;
      h.future.push(h.present);h.present=h.past.pop();var updated=this.store.upsert({...doc,content:h.present},{source:'editor.undo'});
      this.events.emit('editor:history',{type:'undo',document:updated,canUndo:h.past.length>0,canRedo:true});return updated;
    }
    redo(idOrPath){
      var doc=this.store.get(idOrPath||this.activeId);if(!doc)return null;var h=this.ensureHistory(doc);if(!h.future.length)return doc;
      h.past.push(h.present);h.present=h.future.pop();var updated=this.store.upsert({...doc,content:h.present},{source:'editor.redo'});
      this.events.emit('editor:history',{type:'redo',document:updated,canUndo:true,canRedo:h.future.length>0});return updated;
    }
    status(idOrPath){var doc=this.store.get(idOrPath||this.activeId);if(!doc)return{canUndo:false,canRedo:false};var h=this.ensureHistory(doc);return{canUndo:!!h.past.length,canRedo:!!h.future.length};}
  }

  var session=new EditorSession(core.events,docs);core.provide('editor.session',session);
  core.commands.register('editor.undo',{title:'Desfazer',category:'Editor',enabled:function(){return session.status().canUndo},execute:function(){return session.undo()}});
  core.commands.register('editor.redo',{title:'Refazer',category:'Editor',enabled:function(){return session.status().canRedo},execute:function(){return session.redo()}});
  core.commands.register('editor.closeTab',{title:'Fechar aba',category:'Editor',enabled:function(){return !!session.activeId},execute:function(){return session.close(session.activeId)}});
  global.UrbeEditorModel={EditorSession:EditorSession};
})(window);
