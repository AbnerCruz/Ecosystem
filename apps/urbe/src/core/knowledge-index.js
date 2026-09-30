(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents');if(!core||!docs)return;

  function norm(v){return String(v||'').trim().toLowerCase();}
  class KnowledgeIndex{
    constructor(events,store){this.events=events;this.store=store;this.byTitle=new Map();this.byTag=new Map();this.outgoing=new Map();this.incoming=new Map();this.tokens=new Map();this.rebuild();events.on('document:created',()=>this.rebuild());events.on('document:updated',()=>this.rebuild());events.on('document:removed',()=>this.rebuild());events.on('documents:reset',()=>this.rebuild());}
    add(map,key,value){key=norm(key);if(!key)return;var set=map.get(key)||new Set();set.add(value);map.set(key,set);}
    rebuild(){
      this.byTitle.clear();this.byTag.clear();this.outgoing.clear();this.incoming.clear();this.tokens.clear();
      var all=this.store.list(),aliases=new Map();
      all.filter(d=>window.UrbeArtifacts.linkable(d.path)).forEach(d=>{this.add(this.byTitle,d.title,d.id);aliases.set(norm(d.title),d.id);aliases.set(norm(d.path.replace(window.UrbeArtifacts.RE.note,'')),d.id);aliases.set(norm(d.path),d.id);});
      all.forEach(d=>{
        d.tags.forEach(tag=>this.add(this.byTag,tag,d.id));
        var out=new Set();d.links.forEach(raw=>{var target=aliases.get(norm(raw))||null;if(target){out.add(target);this.add(this.incoming,target,d.id)}});
        this.outgoing.set(d.id,out);
        var words=(d.title+' '+d.path+' '+d.content).toLowerCase().match(/[\p{L}\p{N}_-]{2,}/gu)||[];
        new Set(words).forEach(word=>this.add(this.tokens,word,d.id));
      });
      this.events.emit('knowledge:indexed',{documents:all.length,links:Array.from(this.outgoing.values()).reduce((n,s)=>n+s.size,0)});
    }
    backlinks(idOrPath){var d=this.store.get(idOrPath);if(!d)return[];return Array.from(this.incoming.get(norm(d.id))||this.incoming.get(d.id)||[]).map(id=>this.store.get(id)).filter(Boolean);}
    links(idOrPath){var d=this.store.get(idOrPath);if(!d)return[];return Array.from(this.outgoing.get(d.id)||[]).map(id=>this.store.get(id)).filter(Boolean);}
    tagged(tag){return Array.from(this.byTag.get(norm(tag))||[]).map(id=>this.store.get(id)).filter(Boolean);}
    search(query,limit){
      var q=norm(query),max=limit||50;if(!q)return this.store.list().slice(0,max);
      var terms=q.match(/[\p{L}\p{N}_-]{2,}/gu)||[],scores=new Map(),all=this.store.list();
      all.forEach(d=>{var hay=(d.title+' '+d.path).toLowerCase();if(hay.includes(q))scores.set(d.id,(scores.get(d.id)||0)+10)});
      terms.forEach(term=>{(this.tokens.get(term)||[]).forEach(id=>scores.set(id,(scores.get(id)||0)+1))});
      return Array.from(scores.entries()).sort((a,b)=>b[1]-a[1]).slice(0,max).map(x=>this.store.get(x[0])).filter(Boolean);
    }
    stats(){return{documents:this.store.list().length,links:Array.from(this.outgoing.values()).reduce((n,s)=>n+s.size,0),tags:this.byTag.size,tokens:this.tokens.size};}
  }
  var index=new KnowledgeIndex(core.events,docs);core.provide('knowledge',index);
  core.commands.register('workspace.search',{title:'Pesquisar workspace',category:'Pesquisa',execute:function(ctx){return index.search(ctx&&ctx.query||'',ctx&&ctx.limit)}});
})(window);
