(function(global){
  'use strict';
  var core=global.UrbeCore;
  if(!core)return;

  var root,input,list,active=0,visible=[],lastFocus=null;
  function ensure(){
    if(root)return;
    root=document.createElement('div');
    root.id='urbeCommandPalette';
    root.hidden=true;
    root.innerHTML='<div class="ucp-backdrop"></div><section class="ucp-panel" role="dialog" aria-modal="true" aria-label="Comandos"><input class="ucp-input" type="search" autocomplete="off" spellcheck="false" placeholder="Digite um comando…"><div class="ucp-list" role="listbox"></div></section>';
    document.body.appendChild(root);
    input=root.querySelector('.ucp-input'); list=root.querySelector('.ucp-list');
    root.querySelector('.ucp-backdrop').addEventListener('click',close);
    input.addEventListener('input',render);
    input.addEventListener('keydown',function(e){
      if(e.key==='Escape'){e.preventDefault();close();return}
      if(e.key==='ArrowDown'){e.preventDefault();active=Math.min(active+1,visible.length-1);paint();return}
      if(e.key==='ArrowUp'){e.preventDefault();active=Math.max(active-1,0);paint();return}
      if(e.key==='Enter'&&visible[active]){e.preventDefault();run(visible[active].id)}
    });
  }
  function score(command,q){
    var text=(command.title+' '+command.category+' '+command.id).toLowerCase();
    if(!q)return 1;
    if(text.startsWith(q))return 4;
    if(command.title.toLowerCase().includes(q))return 3;
    return text.includes(q)?2:0;
  }
  function render(){
    var q=(input.value||'').trim().toLowerCase();
    visible=core.commands.list().map(function(c){return {command:c,score:score(c,q)}}).filter(function(x){return x.score>0&&x.command.enabled({source:'palette'})}).sort(function(a,b){return b.score-a.score||a.command.title.localeCompare(b.command.title)}).map(function(x){return x.command}).filter(function(c,i,all){var key=(c.category||'')+'|'+c.title;return !all.some(function(o,j){return j>i&&(o.category||'')+'|'+o.title===key})}).slice(0,40);
    active=Math.min(active,Math.max(0,visible.length-1));
    list.innerHTML=visible.length?visible.map(function(c,i){return '<button type="button" class="ucp-item'+(i===active?' active':'')+'" data-command="'+escapeHtml(c.id)+'" role="option" aria-selected="'+(i===active)+'"><span>'+escapeHtml(c.title)+'</span><small>'+escapeHtml(c.category)+'</small></button>'}).join(''):'<div class="ucp-empty">Nenhum comando encontrado</div>';
    Array.prototype.forEach.call(list.querySelectorAll('[data-command]'),function(el){el.addEventListener('click',function(){run(el.getAttribute('data-command'))})});
  }
  function paint(){Array.prototype.forEach.call(list.querySelectorAll('.ucp-item'),function(el,i){el.classList.toggle('active',i===active);el.setAttribute('aria-selected',i===active?'true':'false')});var el=list.querySelectorAll('.ucp-item')[active];if(el)el.scrollIntoView({block:'nearest'})}
  function escapeHtml(v){var d=document.createElement('div');d.textContent=String(v);return d.innerHTML}
  function open(){ensure();lastFocus=document.activeElement;root.hidden=false;input.value='';active=0;render();requestAnimationFrame(function(){input.focus()})}
  function close(){if(!root||root.hidden)return;root.hidden=true;if(lastFocus&&lastFocus.focus)lastFocus.focus()}
  function run(id){close();Promise.resolve(core.commands.execute(id,{source:'palette'})).catch(function(err){console.error('command',id,err)})}

  core.commands.register('ui.commandPalette.open',{title:'Abrir comandos',category:'Interface',execute:open});
  core.provide('commandPalette',{open:open,close:close});
})(window);
