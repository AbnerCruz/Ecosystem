(function(global){
  'use strict';
  var core=global.UrbeCore,session=core&&core.service('editor.session'),context=core&&core.service('editor.context');if(!core||!session||!context)return;
  var host,tabs,side;
  function esc(v){var d=document.createElement('div');d.textContent=String(v);return d.innerHTML}
  function ensure(){
    if(host)return;
    var editor=document.getElementById('editorFull');if(!editor)return;
    host=document.createElement('div');host.id='urbeEditorChrome';
    host.innerHTML='<div class="uec-tabs" role="tablist"></div><aside class="uec-context" hidden><header><strong>Contexto</strong><button type="button" data-close>×</button></header><div class="uec-context-body"></div></aside>';
    editor.appendChild(host);tabs=host.querySelector('.uec-tabs');side=host.querySelector('.uec-context');host.querySelector('[data-close]').onclick=function(){side.hidden=true};
  }
  function renderTabs(){
    ensure();if(!tabs)return;var items=session.listTabs();
    tabs.innerHTML=items.map(function(t){return '<button type="button" role="tab" class="uec-tab'+(t.id===session.activeId?' active':'')+'" data-tab="'+esc(t.id)+'"><span>'+esc(t.document.title)+'</span><span class="uec-x" data-x="'+esc(t.id)+'" role="button" aria-label="Fechar '+esc(t.document.title)+'">×</span></button>'}).join('')+'<button type="button" class="uec-context-btn" title="Estrutura e backlinks" aria-label="Estrutura e backlinks">'+(global.UrbeIcons?global.UrbeIcons.icon('list'):'☷')+'</button>';tabs.classList.toggle('uec-single',items.length<2);
    Array.prototype.forEach.call(tabs.querySelectorAll('[data-tab]'),function(el){el.onclick=function(e){if(e.target.closest('[data-x]'))return;var doc=session.activate(el.getAttribute('data-tab'));if(doc)core.commands.execute('document.open',{id:doc.id,source:'tab'})}});
    Array.prototype.forEach.call(tabs.querySelectorAll('[data-x]'),function(el){el.onclick=function(e){e.stopPropagation();var id=el.getAttribute('data-x'),was=id===session.activeId;session.close(id);renderTabs();if(was&&session.active()){core.commands.execute('document.open',{id:session.activeId,source:'tab-close'})}}});
    var btn=tabs.querySelector('.uec-context-btn');if(btn)btn.onclick=renderContext;
  }
  function renderContext(){
    ensure();var c=context.context(session.activeId);if(!c)return;side.hidden=false;var body=side.querySelector('.uec-context-body');
    body.innerHTML='<section><h3>Outline</h3>'+(c.outline.length?c.outline.map(function(x){return '<button class="uec-outline" data-line="'+x.line+'" style="padding-left:'+(10+(x.level-1)*12)+'px">'+esc(x.title)+'</button>'}).join(''):'<p>Sem títulos.</p>')+'</section><section><h3>Backlinks</h3>'+(c.backlinks.length?c.backlinks.map(function(d){return '<button class="uec-link" data-doc="'+esc(d.id)+'">'+esc(d.title)+'</button>'}).join(''):'<p>Sem backlinks.</p>')+'</section>';
    Array.prototype.forEach.call(body.querySelectorAll('[data-doc]'),function(el){el.onclick=function(){core.commands.execute('document.open',{id:el.getAttribute('data-doc'),source:'backlink'})}});
    Array.prototype.forEach.call(body.querySelectorAll('[data-line]'),function(el){el.onclick=function(){var ta=document.getElementById('bodyEditor');if(!ta)return;var line=Number(el.getAttribute('data-line')),lines=ta.value.split('\n'),pos=0;for(var i=1;i<line;i++)pos+=lines[i-1].length+1;ta.focus();ta.setSelectionRange(pos,pos);side.hidden=true}});
  }
  core.events.on('editor:session',renderTabs);core.events.on('document:updated',function(){if(side&&!side.hidden)renderContext()});
  core.commands.register('editor.contextPanel',{title:'Outline e backlinks',category:'Editor',enabled:function(){return !!session.activeId},execute:renderContext});
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',ensure);else ensure();
})(window);
