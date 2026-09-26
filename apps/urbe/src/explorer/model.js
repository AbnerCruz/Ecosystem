(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents');if(!core||!docs)return;
  function norm(p){return String(p||'').replace(/\\/g,'/').replace(/^\/+|\/+$/g,'').replace(/\/+/g,'/')}
  function dirname(p){p=norm(p);var i=p.lastIndexOf('/');return i<0?'':p.slice(0,i)}
  function basename(p){p=norm(p);var i=p.lastIndexOf('/');return i<0?p:p.slice(i+1)}
  function folderId(path){return'folder:'+norm(path)}
  class ExplorerModel{
    constructor(events,store){this.events=events;this.store=store;this.selection=new Set();this.favorites=new Set();this.recent=[];this.folders=new Set();this.expanded=new Set(['']);this.revision=0;this.storageKey='urbe.explorer.v2';this.restore();events.on('document:created',()=>this.changed('documents'));events.on('document:updated',()=>this.changed('documents'));events.on('document:removed',()=>this.changed('documents'));events.on('documents:reset',()=>this.changed('documents'));events.on('workspace:loaded',x=>this.loadFolders(x))}
    loadFolders(x){this.selection.clear();this.folders=new Set();(x&&x.paths||[]).forEach(p=>{if(p.endsWith('/.pasta'))this.folders.add(dirname(p))});(x&&x.metadata&&x.metadata.regioes||[]).forEach(r=>{if(r.caminho)this.folders.add(r.caminho)});this.changed('folders')}
    addFolder(path){path=norm(path);if(!path)return false;this.folders.add(path);this.expand(dirname(path),true);this.changed('folders');this.events.emit('explorer:folderCreated',{path});return true}
    persist(){try{if(global.localStorage)global.localStorage.setItem(this.storageKey,JSON.stringify({favorites:Array.from(this.favorites),recent:this.recent,expanded:Array.from(this.expanded)}))}catch(_){}}
    restore(){try{if(!global.localStorage)return;var x=JSON.parse(global.localStorage.getItem(this.storageKey)||'null');if(!x)return;this.favorites=new Set(x.favorites||[]);this.recent=(x.recent||[]).slice(0,30);this.expanded=new Set(x.expanded||['']);this.expanded.add('')}catch(_){}}
    changed(type){if(type!=='selection')this.persist();this.revision++;this.events.emit('explorer:changed',{type:type,revision:this.revision,selection:this.selected()})}
    tree(){var root={id:folderId(''),kind:'folder',name:'Workspace',path:'',children:[]},folders=new Map([['',root]]);
      function ensure(path){path=norm(path);if(folders.has(path))return folders.get(path);var parent=ensure(dirname(path)),node={id:folderId(path),kind:'folder',name:basename(path),path:path,children:[]};folders.set(path,node);parent.children.push(node);return node}
      this.folders.forEach(ensure);this.store.list().forEach(function(d){var parent=ensure(dirname(d.path));parent.children.push({id:d.id,kind:'document',name:d.title,path:d.path,document:d})});
      function sort(n){n.children.sort(function(a,b){if(a.kind!==b.kind)return a.kind==='folder'?-1:1;return a.name.localeCompare(b.name)});n.children.filter(x=>x.kind==='folder').forEach(sort)}sort(root);return root}
    node(id){if(String(id).startsWith('folder:')){var path=String(id).slice(7),found=null;function walk(n){if(n.id===id)found=n;else if(n.children)n.children.forEach(walk)}walk(this.tree());return found}var d=this.store.get(id);return d?{id:d.id,kind:'document',name:d.title,path:d.path,document:d}:null}
    select(id,mode){if(mode==='replace')this.selection.clear();if(mode==='toggle'){this.selection.has(id)?this.selection.delete(id):this.selection.add(id)}else this.selection.add(id);this.changed('selection');return this.selected()}
    clear(){this.selection.clear();this.changed('selection')}
    selected(){return Array.from(this.selection).map(id=>this.node(id)).filter(Boolean)}
    expand(path,value){path=norm(path);if(value===false)this.expanded.delete(path);else this.expanded.add(path);this.changed('expand')}
    isExpanded(path){return this.expanded.has(norm(path))}
    favorite(id,value){if(value===false)this.favorites.delete(id);else this.favorites.add(id);this.changed('favorite')}
    touchRecent(id){this.recent=this.recent.filter(x=>x!==id);this.recent.unshift(id);this.recent=this.recent.slice(0,30);this.changed('recent')}
    listFavorites(){return Array.from(this.favorites).map(id=>this.node(id)).filter(Boolean)}
    listRecent(){return this.recent.map(id=>this.node(id)).filter(Boolean)}
  }
  var model=new ExplorerModel(core.events,docs);core.provide('explorer',model);
  core.commands.register('explorer.selection.clear',{title:'Limpar seleção',category:'Arquivos',execute:function(){return model.clear()}});
  core.commands.register('explorer.favorite',{title:'Favoritar',category:'Arquivos',execute:function(ctx){return model.favorite(ctx&&ctx.id,ctx&&ctx.value)}});
  global.UrbeExplorerModel={ExplorerModel:ExplorerModel};
})(window);
