(function(global){
  'use strict';
  var core=global.UrbeCore,model=core&&core.service('explorer'),trash=core&&core.service('trash');if(!core||!model)return;
  var root,list,bar,hold=null,HOLD=430;
  function esc(v){var d=document.createElement('div');d.textContent=String(v);return d.innerHTML}
  function ensure(){
    if(root)return;
    root=document.createElement('section');root.id='urbeMobileExplorer';root.hidden=true;
    root.innerHTML='<header><strong>Arquivos</strong><div><button data-recent title="Recentes">Recentes</button><button data-fav title="Favoritos">★</button><button data-trash title="Lixeira">Lixeira</button><button data-close aria-label="Fechar">×</button></div></header><div class="ume-list"></div><nav class="ume-actions" hidden><span data-count></span><button data-open>Abrir</button><button data-favorite>★</button><button data-compose>Compor</button><button data-duplicate>Duplicar</button><button data-delete>Excluir</button><button data-clear>×</button></nav>';
    document.body.appendChild(root);list=root.querySelector('.ume-list');bar=root.querySelector('.ume-actions');
    root.querySelector('[data-close]').onclick=close;root.querySelector('[data-clear]').onclick=function(){model.clear()};
    root.querySelector('[data-open]').onclick=openSelected;root.querySelector('[data-compose]').onclick=composeSelected;root.querySelector('[data-favorite]').onclick=favoriteSelected;root.querySelector('[data-duplicate]').onclick=duplicateSelected;root.querySelector('[data-delete]').onclick=deleteSelected;
    root.querySelector('[data-recent]').onclick=function(){renderCollection('Recentes',model.listRecent())};root.querySelector('[data-fav]').onclick=function(){renderCollection('Favoritos',model.listFavorites())};if(trash)root.querySelector('[data-trash]').onclick=renderTrash;
  }
  function row(node,depth){
    var selected=model.selection.has(node.id),folder=node.kind==='folder';
    return '<button type="button" class="ume-row'+(selected?' selected':'')+'" data-id="'+esc(node.id)+'" data-kind="'+node.kind+'" style="--depth:'+depth+'"><span class="ume-icon">'+(folder?(model.isExpanded(node.path)?'▾':'▸'):'▤')+'</span><span class="ume-name">'+esc(node.name)+'</span>'+(folder?'':'<small>'+esc(node.path)+'</small>')+'</button>';
  }
  function flatten(node,depth,out){node.children.forEach(function(n){out.push(row(n,depth));if(n.kind==='folder'&&model.isExpanded(n.path))flatten(n,depth+1,out)})}
  function render(){
    ensure();var out=[];flatten(model.tree(),0,out);list.innerHTML=out.join('')||'<div class="ume-empty">Nenhuma nota.</div>';bindRows();renderBar();
  }
  function renderCollection(title,nodes){ensure();root.querySelector('header strong').textContent=title;list.innerHTML=nodes.map(function(n){return row(n,0)}).join('')||'<div class="ume-empty">Nada aqui ainda.</div>';bindRows();renderBar()}
  function renderTrash(){ensure();root.querySelector('header strong').textContent='Lixeira';var items=trash?trash.list():[];list.innerHTML=items.map(function(item){var d=item.document;return '<button type="button" class="ume-row" data-restore="'+esc(d.id)+'"><span class="ume-icon">↶</span><span class="ume-name">'+esc(d.title)+'</span><small>'+esc(item.originalPath)+'</small></button>'}).join('')||'<div class="ume-empty">A lixeira está vazia.</div>';Array.prototype.forEach.call(list.querySelectorAll('[data-restore]'),function(el){el.onclick=function(){try{trash.restore(el.getAttribute('data-restore'));renderTrash()}catch(err){global.alert&&alert(err.message)}}});bar.hidden=true}
  function bindRows(){Array.prototype.forEach.call(list.querySelectorAll('.ume-row'),function(el){
    var id=el.getAttribute('data-id'),kind=el.getAttribute('data-kind'),moved=false;
    el.addEventListener('pointerdown',function(){moved=false;hold=setTimeout(function(){hold=null;model.select(id,'toggle');navigator.vibrate&&navigator.vibrate(18)},HOLD)});
    el.addEventListener('pointermove',function(){moved=true;if(hold){clearTimeout(hold);hold=null}});
    el.addEventListener('pointerup',function(){if(hold){clearTimeout(hold);hold=null;if(moved)return;if(model.selection.size){model.select(id,'toggle');return}if(kind==='folder'){var n=model.node(id);model.expand(n.path,!model.isExpanded(n.path));return}openId(id)}});
    el.addEventListener('pointercancel',function(){if(hold)clearTimeout(hold);hold=null});
  })}
  function renderBar(){var n=model.selection.size;bar.hidden=!n;bar.querySelector('[data-count]').textContent=n+' selecionado'+(n===1?'':'s')}
  function openId(id){var n=model.node(id);if(!n||n.kind!=='document')return;model.touchRecent(id);core.commands.execute('document.open',{id:id,source:'mobile-explorer'});close()}
  function openSelected(){var n=model.selected().find(x=>x.kind==='document');if(n)openId(n.id)}
  function favoriteSelected(){model.selected().forEach(n=>model.favorite(n.id,true))}
  function composeSelected(){var ids=model.selected().filter(n=>n.kind==='document').map(n=>n.id);if(ids.length)core.commands.execute('ui.composition.create',{ids:ids,type:'document'});model.clear();close()}
  function duplicateSelected(){model.selected().filter(n=>n.kind==='document').forEach(n=>core.commands.execute('explorer.duplicate',{id:n.id}));model.clear()}
  function deleteSelected(){var ids=model.selected().filter(n=>n.kind==='document').map(n=>n.id);if(!ids.length)return;if(global.confirm&& !confirm('Excluir '+ids.length+' nota'+(ids.length===1?'':'s')+'?'))return;core.commands.execute('explorer.delete',{ids:ids})}
  function open(){ensure();root.hidden=false;root.querySelector('header strong').textContent='Arquivos';render()}
  function close(){if(root)root.hidden=true}
  core.events.on('explorer:changed',function(){if(root&&!root.hidden)render()});
  core.commands.register('ui.explorer.open',{title:'Abrir arquivos',category:'Navegação',execute:open});
  core.provide('explorer.ui',{open:open,close:close});
})(window);
