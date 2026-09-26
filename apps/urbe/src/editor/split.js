(function(global){
  'use strict';
  var core=global.UrbeCore,session=core&&core.service('editor.session');if(!core||!session)return;
  var state={enabled:false,primary:null,secondary:null,ratio:.5};
  function emit(type){core.events.emit('editor:split',{type:type,state:snapshot()});return snapshot()}
  function snapshot(){return{enabled:state.enabled,primary:state.primary,secondary:state.secondary,ratio:state.ratio}}
  function openSecondary(idOrPath){var docs=core.service('documents'),doc=docs&&docs.get(idOrPath);if(!doc)return null;state.enabled=true;state.primary=session.activeId||doc.id;state.secondary=doc.id;return emit('open')}
  function close(){state.enabled=false;state.secondary=null;return emit('close')}
  function swap(){if(!state.enabled)return snapshot();var x=state.primary;state.primary=state.secondary;state.secondary=x;return emit('swap')}
  function ratio(value){state.ratio=Math.max(.25,Math.min(.75,Number(value)||.5));return emit('resize')}
  core.provide('editor.split',{snapshot:snapshot,open:openSecondary,close:close,swap:swap,ratio:ratio});
  core.commands.register('editor.split.open',{title:'Abrir em painel dividido',category:'Editor',execute:function(ctx){return openSecondary(ctx&&ctx.id||session.activeId)}});
  core.commands.register('editor.split.close',{title:'Fechar painel dividido',category:'Editor',enabled:function(){return state.enabled},execute:close});
  core.commands.register('editor.split.swap',{title:'Trocar painéis',category:'Editor',enabled:function(){return state.enabled},execute:swap});
})(window);
