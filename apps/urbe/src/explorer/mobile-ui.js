(function(global){
  'use strict';
  var core=global.UrbeCore,model=core&&core.service('explorer'),trash=core&&core.service('trash');if(!core||!model)return;
  var root,list,bar,hold=null,HOLD=430;
  function esc(v){var d=document.createElement('div');d.textContent=String(v);return d.innerHTML}
  function ensure(){
    if(root)return;
    root=document.createElement('section');root.id='urbeMobileExplorer';root.hidden=true;
    root.innerHTML='<header><strong>Arquivos</strong><div><button data-note title="Nova nota">＋ Nota</button><button data-folder title="Nova pasta">＋ Pasta</button><button data-compositions title="Composições">Composições</button><button data-recent title="Recentes">Recentes</button><button data-fav title="Favoritos">★</button><button data-trash title="Lixeira">Lixeira</button><button data-storage title="Pasta do Urbe">▣</button><button data-close aria-label="Fechar">×</button></div></header><div class="ume-list"></div><nav class="ume-actions" hidden><span data-count></span><button data-open>Abrir</button><button data-favorite>★</button><button data-compose>Compor</button><button data-duplicate>Duplicar</button><button data-delete>Excluir</button><button data-clear>×</button></nav>';
    document.body.appendChild(root);list=root.querySelector('.ume-list');bar=root.querySelector('.ume-actions');
    root.querySelector('[data-close]').onclick=close;root.querySelector('[data-clear]').onclick=function(){model.clear()};
    root.querySelector('[data-open]').onclick=openSelected;root.querySelector('[data-compose]').onclick=composeSelected;root.querySelector('[data-favorite]').onclick=favoriteSelected;root.querySelector('[data-duplicate]').onclick=duplicateSelected;root.querySelector('[data-delete]').onclick=deleteSelected;
    root.querySelector('[data-note]').onclick=createNote;root.querySelector('[data-folder]').onclick=createFolder;root.querySelector('[data-compositions]').onclick=renderCompositions;root.querySelector('[data-storage]').onclick=function(){var storage=core.service('workspace.storage');if(storage)storage.chooseRoot()};
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
  function renderCompositions(){ensure();root.querySelector('header strong').textContent='Composições';var store=core.service('compositions'),items=store?store.list():[];list.innerHTML=items.map(function(x){return '<button type="button" class="ume-row" data-composition="'+esc(x.id)+'"><span class="ume-icon">▤</span><span class="ume-name">'+esc(x.name)+'</span><small>'+x.sources.length+' fonte(s)</small></button>'}).join('')||'<div class="ume-empty">Selecione notas e toque em Compor.</div>';list.querySelectorAll('[data-composition]').forEach(function(el){el.onclick=function(){var id=el.dataset.composition;close();core.commands.execute('ui.composition.open',{id:id})}});bar.hidden=true}
  function bindRows(){Array.prototype.forEach.call(list.querySelectorAll('.ume-row'),function(el){
    var id=el.getAttribute('data-id'),kind=el.getAttribute('data-kind'),moved=false;
    var origin=null,longPressed=false,lastPointer=0;
    el.addEventListener('pointerdown',function(e){origin={x:e.clientX,y:e.clientY,id:e.pointerId};moved=false;longPressed=false;hold=setTimeout(function(){hold=null;longPressed=true;model.select(id,'toggle');navigator.vibrate&&navigator.vibrate(18)},HOLD)});
    el.addEventListener('pointermove',function(e){if(!origin||e.pointerId!==origin.id)return;if(Math.hypot(e.clientX-origin.x,e.clientY-origin.y)>12){moved=true;if(hold){clearTimeout(hold);hold=null}}});
    el.addEventListener('pointerup',function(e){lastPointer=Date.now();if(e.pointerType==='mouse'&&e.button!==0)return;if(hold){clearTimeout(hold);hold=null;if(moved)return;if(model.selection.size){model.select(id,'toggle');return}if(kind==='folder'){var n=model.node(id);model.expand(n.path,!model.isExpanded(n.path));return}openId(id)}origin=null});
    el.addEventListener('pointercancel',function(){if(hold)clearTimeout(hold);hold=null});
    el.addEventListener('click',function(){if(Date.now()-lastPointer<600||longPressed)return;if(kind==='folder'){var n=model.node(id);model.expand(n.path,!model.isExpanded(n.path))}else openId(id)});
  })}
  function renderBar(){var n=model.selection.size;bar.hidden=!n;bar.querySelector('[data-count]').textContent=n+' selecionado'+(n===1?'':'s')}
  function openId(id){var n=model.node(id);if(!n||n.kind!=='document')return;model.touchRecent(id);core.commands.execute('document.open',{id:id,source:'mobile-explorer'});close()}
  function openSelected(){var n=model.selected().find(x=>x.kind==='document');if(n)openId(n.id)}
  function favoriteSelected(){model.selected().forEach(n=>model.favorite(n.id,true))}
  function composeSelected(){var ids=model.selected().filter(n=>n.kind==='document').map(n=>n.id);if(ids.length)core.commands.execute('ui.composition.create',{ids:ids,type:'document'});model.clear();close()}
  function duplicateSelected(){model.selected().filter(n=>n.kind==='document').forEach(n=>core.commands.execute('explorer.duplicate',{id:n.id}));model.clear()}
  function deleteSelected(){var ids=model.selected().filter(n=>n.kind==='document').map(n=>n.id);if(!ids.length)return;if(global.confirm&& !confirm('Excluir '+ids.length+' nota'+(ids.length===1?'':'s')+'?'))return;core.commands.execute('explorer.delete',{ids:ids})}
  function currentFolder(){var folder=model.selected().find(n=>n.kind==='folder');return folder?folder.path:''}
  function cleanName(s){return String(s||'').trim().replace(/[\\/:*?"<>|\u0000-\u001f]/g,'-').replace(/^\.+|\.+$/g,'').slice(0,90)}
  async function createFolder(){var name=cleanName(global.prompt('Nome da pasta:'));if(!name)return;var folder=currentFolder(),path=(folder?folder+'/':'')+name,p=core.service('persistence');try{if(p&&p.adapter&&p.vault)await p.adapter.createFolder(p.vault,path);model.addFolder(path);model.clear();render()}catch(e){global.alert('Não consegui criar a pasta: '+e.message)}}
  function createNote(){var name=cleanName(global.prompt('Nome do arquivo:', 'Nova nota.md'));if(!name)return;if(!/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i.test(name))name+='.md';var folder=currentFolder(),path=(folder?folder+'/':'')+name,docs=core.service('documents');if(docs.get(path)){global.alert('Já existe um arquivo com esse nome nesta pasta.');return}var doc=docs.upsert({path:path,content:''},{source:'explorer.create'});model.clear();core.commands.execute('document.open',{id:doc.id,source:'mobile-explorer'});close()}
  function open(){ensure();root.hidden=false;root.querySelector('header strong').textContent='Arquivos';render()}
  function close(){if(root)root.hidden=true}
  core.events.on('explorer:changed',function(){if(root&&!root.hidden)render()});
  // Abrir um documento por qualquer caminho (atalho, paleta, link) revela o editor.
  core.events.on('command:after',function(e){if(e&&e.id==='document.open')close()});
  core.commands.register('ui.explorer.open',{title:'Abrir arquivos',category:'Navegação',execute:open});
  core.provide('explorer.ui',{open:open,close:close,isOpen:function(){return !!root&&!root.hidden}});
})(window);
