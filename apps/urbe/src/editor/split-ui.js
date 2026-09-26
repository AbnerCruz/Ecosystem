(function(global){
  'use strict';
  var core=global.UrbeCore,split=core&&core.service('editor.split'),docs=core&&core.service('documents');if(!core||!split||!docs)return;
  var pane;
  function esc(v){var d=document.createElement('div');d.textContent=String(v);return d.innerHTML}
  function markdown(text){return esc(text).replace(/^######\s+(.+)$/gm,'<h6>$1</h6>').replace(/^#####\s+(.+)$/gm,'<h5>$1</h5>').replace(/^####\s+(.+)$/gm,'<h4>$1</h4>').replace(/^###\s+(.+)$/gm,'<h3>$1</h3>').replace(/^##\s+(.+)$/gm,'<h2>$1</h2>').replace(/^#\s+(.+)$/gm,'<h1>$1</h1>').replace(/\*\*(.+?)\*\*/g,'<strong>$1</strong>').replace(/\[\[([^\]]+)\]\]/g,'<span class="ues-wiki">[[$1]]</span>').replace(/\n/g,'<br>')}
  function ensure(){if(pane)return;var editor=document.getElementById('editorFull');if(!editor)return;pane=document.createElement('aside');pane.id='urbeSplitPane';pane.hidden=true;pane.innerHTML='<header><strong data-title></strong><div><button data-swap title="Trocar painéis">⇄</button><button data-close title="Fechar">×</button></div></header><article></article>';editor.appendChild(pane);pane.querySelector('[data-close]').onclick=function(){split.close()};pane.querySelector('[data-swap]').onclick=function(){split.swap()}}
  function render(evt){ensure();if(!pane)return;var state=evt&&evt.state||split.snapshot();pane.hidden=!state.enabled;if(!state.enabled)return;var doc=docs.get(state.secondary);if(!doc){split.close();return}pane.style.width=Math.round((1-state.ratio)*100)+'%';pane.querySelector('[data-title]').textContent=doc.title;pane.querySelector('article').innerHTML=markdown(doc.content)}
  core.events.on('editor:split',render);core.events.on('document:updated',function(evt){var s=split.snapshot();if(s.enabled&&evt.document.id===s.secondary)render({state:s})});
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',ensure);else ensure();
})(window);
