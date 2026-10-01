(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents'),session=core&&core.service('editor.session');if(!core||!docs||!session)return;
  var back=[],forward=[],restoring=false,MAX=80,KEY='urbe.editor.workspace.v1';
  function snapshot(){return{tabs:session.listTabs().map(function(t){return{id:t.id,pinned:t.pinned}}),activeId:session.activeId,back:back.slice(-MAX),forward:forward.slice(-MAX)}}
  function persist(){try{localStorage.setItem(KEY,JSON.stringify(snapshot()))}catch(_){}}
  function restore(){try{var raw=localStorage.getItem(KEY);if(!raw)return false;var state=JSON.parse(raw);restoring=true;(state.tabs||[]).forEach(function(t){if(docs.get(t.id))session.open(t.id,{pinned:!!t.pinned})});if(state.activeId&&docs.get(state.activeId))session.activate(state.activeId);back=(state.back||[]).filter(function(id){return docs.get(id)}).slice(-MAX);forward=(state.forward||[]).filter(function(id){return docs.get(id)}).slice(-MAX);restoring=false;core.events.emit('editor:workspace',{type:'restored',state:snapshot()});return true}catch(_){restoring=false;return false}}
  function visit(id){if(restoring||!id)return null;if(session.activeId&&session.activeId!==id){back.push(session.activeId);if(back.length>MAX)back.shift();forward=[]}var doc=session.activate(id);persist();return doc}
  function go(from,to,type){while(from.length){var id=from.pop();if(!docs.get(id)||id===session.activeId)continue;if(session.activeId)to.push(session.activeId);session.activate(id);persist();core.events.emit('editor:navigation',{type:type,id:id});return docs.get(id)}return null}
  function find(query,idOrPath,options){var doc=docs.get(idOrPath||session.activeId);if(!doc||!query)return[];var q=String(query),text=doc.content,cs=!!(options&&options.caseSensitive),needle=cs?q:q.toLowerCase(),hay=cs?text:text.toLowerCase(),out=[],at=0;while((at=hay.indexOf(needle,at))>=0){out.push({index:at,length:q.length,line:text.slice(0,at).split('\n').length});at+=Math.max(1,q.length);if(out.length>=1000)break}return out}
  function replace(query,replacement,idOrPath,options){var doc=docs.get(idOrPath||session.activeId);if(!doc||!query)return null;var hits=find(query,doc.id,options);if(!hits.length)return doc;var all=!!(options&&options.all),next=doc.content,repl=String(replacement);if(all){for(var i=hits.length-1;i>=0;i--){var h=hits[i];next=next.slice(0,h.index)+repl+next.slice(h.index+h.length)}}else{var h=hits[0];next=next.slice(0,h.index)+repl+next.slice(h.index+h.length)}return session.record(doc.id,next,{source:'editor.replace'})}
  core.events.on('editor:session',function(){if(!restoring)persist()});core.events.on('document:updated',function(){if(!restoring)persist()});
  core.provide('editor.workspace',{snapshot:snapshot,restore:restore,visit:visit,back:function(){return go(back,forward,'back')},forward:function(){return go(forward,back,'forward')},find:find,replace:replace,persist:persist});
  core.commands.register('editor.back',{title:'Voltar',category:'Navegação',enabled:function(){return back.length>0},execute:function(){return go(back,forward,'back')}});
  core.commands.register('editor.forward',{title:'Avançar',category:'Navegação',enabled:function(){return forward.length>0},execute:function(){return go(forward,back,'forward')}});
  core.commands.register('editor.find',{title:'Localizar no documento',category:'Editor',execute:function(ctx){return find(ctx&&ctx.query||'',ctx&&ctx.id,ctx)}});
  core.commands.register('editor.replace',{title:'Substituir no documento',category:'Editor',execute:function(ctx){return replace(ctx&&ctx.query||'',ctx&&ctx.replacement||'',ctx&&ctx.id,ctx)}});
  core.commands.register('editor.session.restore',{title:'Restaurar sessão',category:'Editor',execute:restore});
})(window);
