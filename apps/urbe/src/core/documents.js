(function(global){
  'use strict';

  function normalizePath(path){
    return String(path||'').replace(/\\/g,'/').replace(/^\/+|\/+$/g,'').replace(/\/+/g,'/');
  }
  var _idSeq=0;
  function createDocumentId(){
    if(global.crypto&&typeof global.crypto.randomUUID==='function')return 'doc_'+global.crypto.randomUUID();
    _idSeq++;return 'doc_'+Date.now().toString(36)+'_'+_idSeq.toString(36);
  }
  function titleFromPath(path){
    var file=normalizePath(path).split('/').pop()||'';
    return file.replace(/\.(md|markdown)$/i,'');
  }
  function parseFrontmatter(content){
    var text=String(content||'');
    if(!text.startsWith('---\n'))return {};
    var end=text.indexOf('\n---',4); if(end<0)return {};
    var out={};
    text.slice(4,end).split('\n').forEach(function(line){
      var i=line.indexOf(':'); if(i<=0)return;
      var key=line.slice(0,i).trim(),value=line.slice(i+1).trim();
      if(key)out[key]=value;
    });
    return out;
  }
  /* código (blocos ``` e trechos `...`) não conta: exemplos de [[link]] e #tag dentro de código são só texto */
  function semCodigo(text){return String(text||'').replace(/^(```|~~~)[^\n]*\n[\s\S]*?^\1[ \t]*$/gm,' ').replace(/`[^`\n]*`/g,' ')}
  function parseLinks(content){
    var out=[],seen=new Set(),re=/\[\[([^\]\n]+)\]\]/g,m,text=semCodigo(content);
    while((m=re.exec(text))){
      var raw=m[1],target=raw.split('|')[0].split('#')[0].trim();
      if(!target)continue;
      var key=target.toLowerCase(); if(seen.has(key))continue;
      seen.add(key);out.push(target);
    }
    return out;
  }
  function parseTags(content,properties){
    var out=new Set(),re=/(^|\s)#([\p{L}\p{N}_/-]+)/gu,m,text=semCodigo(content);
    while((m=re.exec(text)))out.add(m[2]);
    var raw=properties.tags||properties.tag||'';
    String(raw).replace(/^\[|\]$/g,'').split(',').map(function(x){return x.trim().replace(/^#/,'')}).filter(Boolean).forEach(function(x){out.add(x)});
    return Array.from(out);
  }

  class DocumentStore {
    constructor(events){this.events=events;this.docs=new Map();this.pathIndex=new Map();this.revision=0;}
    make(input){
      var path=normalizePath(input.path),content=String(input.content||''),properties=input.properties||parseFrontmatter(content);
      return Object.freeze({
        id:String(input.id||createDocumentId()),path:path,title:String(input.title||titleFromPath(path)),content:content,
        properties:Object.freeze({...properties}),tags:Object.freeze((input.tags||parseTags(content,properties)).slice()),
        links:Object.freeze((input.links||parseLinks(content)).slice()),created:input.created||null,modified:input.modified||null,
        revision:Number(input.revision||0)
      });
    }
    upsert(input,meta){
      if(!input||!input.path)throw new TypeError('document path is required');
      var path=normalizePath(input.path),existingId=this.pathIndex.get(path.toLowerCase()),id=String(input.id||existingId||createDocumentId());
      var previous=this.docs.get(id)||null,data={...input,id:id,revision:(previous?previous.revision:0)+1};
      if(previous&&String(input.content||'')!==previous.content){delete data.tags;delete data.links;delete data.properties}
      var next=this.make(data);
      if(previous&&previous.path.toLowerCase()!==path.toLowerCase())this.pathIndex.delete(previous.path.toLowerCase());
      this.docs.set(id,next);this.pathIndex.set(path.toLowerCase(),id);this.revision++;
      this.events.emit(previous?'document:updated':'document:created',{document:next,previous:previous,meta:meta||null,storeRevision:this.revision});
      return next;
    }
    remove(idOrPath,meta){
      var doc=this.get(idOrPath);if(!doc)return false;
      this.docs.delete(doc.id);this.pathIndex.delete(doc.path.toLowerCase());this.revision++;
      this.events.emit('document:removed',{document:doc,meta:meta||null,storeRevision:this.revision});return true;
    }
    get(idOrPath){var key=String(idOrPath||'');return this.docs.get(key)||this.docs.get(this.pathIndex.get(normalizePath(key).toLowerCase()))||null;}
    list(){return Array.from(this.docs.values());}
    clear(meta){var old=this.list();this.docs.clear();this.pathIndex.clear();this.revision++;this.events.emit('documents:reset',{previous:old,meta:meta||null,storeRevision:this.revision});}
    replaceAll(items,meta){this.docs.clear();this.pathIndex.clear();var self=this;(items||[]).forEach(function(x){var d=self.make({...x,revision:1});self.docs.set(d.id,d);self.pathIndex.set(d.path.toLowerCase(),d.id)});this.revision++;this.events.emit('documents:reset',{documents:this.list(),meta:meta||null,storeRevision:this.revision});return this.list();}
  }

  var core=global.UrbeCore;if(!core)return;
  var store=new DocumentStore(core.events);
  core.provide('documents',store);
  core.commands.register('document.update',{title:'Atualizar documento',category:'Documento',execute:function(ctx){if(!ctx||!ctx.path)throw new TypeError('document.update requires path');return store.upsert(ctx,{source:ctx.source||'command'})}});
  core.commands.register('document.remove',{title:'Remover documento',category:'Documento',execute:function(ctx){return !!ctx&&store.remove(ctx.id||ctx.path,{source:ctx.source||'command'})}});
  global.UrbeDocumentModel={DocumentStore:DocumentStore,parseLinks:parseLinks,parseTags:parseTags,parseFrontmatter:parseFrontmatter,normalizePath:normalizePath,createDocumentId:createDocumentId};
})(window);
