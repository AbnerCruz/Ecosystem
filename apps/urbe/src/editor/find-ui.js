(function(global){
  'use strict';
  var core=global.UrbeCore,workspace=core&&core.service('editor.workspace');if(!core||!workspace)return;
  var root,q,repl,status;
  function ensure(){if(root)return;var main=document.getElementById('editorMain');if(!main)return;root=document.createElement('div');root.id='urbeFindBar';root.hidden=true;root.innerHTML='<input data-q type="search" placeholder="Localizar"><span data-status></span><input data-r type="text" placeholder="Substituir"><button data-next>Próximo</button><button data-one>Substituir</button><button data-all>Todos</button><button data-close>×</button>';main.prepend(root);q=root.querySelector('[data-q]');repl=root.querySelector('[data-r]');status=root.querySelector('[data-status]');q.oninput=update;root.querySelector('[data-next]').onclick=next;root.querySelector('[data-one]').onclick=function(){replace(false)};root.querySelector('[data-all]').onclick=function(){replace(true)};root.querySelector('[data-close]').onclick=close;root.onkeydown=function(e){if(e.key==='Escape'){e.preventDefault();close()}else if(e.key==='Enter'&&e.target===q){e.preventDefault();next()}}}
  function hits(){return workspace.find(q&&q.value||'')}
  function update(){var h=hits();status.textContent=h.length?(h.length+' resultado'+(h.length===1?'':'s')):'Nenhum resultado'}
  function select(hit){var ta=document.getElementById('bodyEditor');if(!ta||!hit)return;ta.focus();ta.setSelectionRange(hit.index,hit.index+hit.length)}
  var cursor=0;
  function next(){var h=hits();if(!h.length)return;select(h[cursor%h.length]);cursor=(cursor+1)%h.length}
  function replace(all){var doc=workspace.replace(q.value,repl.value,null,{all:all});if(!doc)return;var ta=document.getElementById('bodyEditor');if(ta){ta.value=doc.content;ta.dispatchEvent(new Event('input',{bubbles:true}))}cursor=0;update()}
  function open(){ensure();if(!root)return;
    /* localizar trabalha no texto puro: no modo Visual, passa para Fonte */
    var sc=document.getElementById('editorScroll'),fonte=document.querySelector('#editorFull [data-mode="source"]');if(sc&&sc.classList.contains('previewMode')&&fonte)fonte.click();
    root.hidden=false;cursor=0;q.focus();q.select();update()}
  function close(){if(root)root.hidden=true}
  core.commands.register('ui.find.open',{title:'Localizar e substituir',category:'Editor',execute:open});
  core.provide('editor.findBar',{open:open,close:close});
})(window);
