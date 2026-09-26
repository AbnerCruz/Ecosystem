(function(global){
  'use strict';
  var core=global.UrbeCore,knowledge=core&&core.service('knowledge');if(!core||!knowledge)return;
  var root,input,list,results=[],active=0,lastFocus=null;
  function esc(v){var d=document.createElement('div');d.textContent=String(v);return d.innerHTML}
  function ensure(){
    if(root)return;
    root=document.createElement('div');root.id='urbeQuickOpen';root.hidden=true;
    root.innerHTML='<div class="uqo-backdrop"></div><section class="uqo-panel" role="dialog" aria-modal="true" aria-label="Abrir nota"><input class="uqo-input" type="search" autocomplete="off" spellcheck="false" placeholder="Abrir nota…"><div class="uqo-list" role="listbox"></div></section>';
    document.body.appendChild(root);input=root.querySelector('.uqo-input');list=root.querySelector('.uqo-list');
    root.querySelector('.uqo-backdrop').onclick=close;input.oninput=render;
    input.onkeydown=function(e){if(e.key==='Escape'){e.preventDefault();close()}else if(e.key==='ArrowDown'){e.preventDefault();active=Math.min(active+1,results.length-1);paint()}else if(e.key==='ArrowUp'){e.preventDefault();active=Math.max(0,active-1);paint()}else if(e.key==='Enter'&&results[active]){e.preventDefault();openDoc(results[active])}};
  }
  function render(){results=knowledge.search(input.value,60);active=Math.min(active,Math.max(0,results.length-1));list.innerHTML=results.length?results.map(function(d,i){return '<button type="button" class="uqo-item'+(i===active?' active':'')+'" data-id="'+esc(d.id)+'"><span>'+esc(d.title)+'</span><small>'+esc(d.path)+'</small></button>'}).join(''):'<div class="uqo-empty">Nenhuma nota encontrada</div>';Array.prototype.forEach.call(list.querySelectorAll('[data-id]'),function(el){el.onclick=function(){openDoc(core.service('documents').get(el.getAttribute('data-id')))}})}
  function paint(){Array.prototype.forEach.call(list.querySelectorAll('.uqo-item'),function(el,i){el.classList.toggle('active',i===active)});var el=list.querySelectorAll('.uqo-item')[active];if(el)el.scrollIntoView({block:'nearest'})}
  function open(){ensure();lastFocus=document.activeElement;root.hidden=false;input.value='';render();requestAnimationFrame(function(){input.focus()})}
  function close(){if(root)root.hidden=true;if(lastFocus&&lastFocus.focus)lastFocus.focus()}
  function openDoc(doc){if(!doc)return;close();core.commands.execute('document.open',{id:doc.id,path:doc.path,source:'quick-open'})}
  core.commands.register('ui.quickOpen.open',{title:'Abrir nota rapidamente',category:'Navegação',execute:open});
  core.provide('quickOpen',{open:open,close:close});
})(window);
