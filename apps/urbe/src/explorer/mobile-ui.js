(function(global){
  'use strict';
  var core=global.UrbeCore,model=core&&core.service('explorer'),trash=core&&core.service('trash');if(!core||!model)return;
  var root,list,bar,search,crumb,view='tree',query='',hold=null,HOLD=430;
  var D=global.UrbeDialogs;
  function ic(n){return global.UrbeIcons?global.UrbeIcons.icon(n):''}
  function esc(v){var d=document.createElement('div');d.textContent=String(v);return d.innerHTML}
  function ask(o){return D.prompt(o)}
  function confirmAction(o){return D.confirm(o)}
  function notify(title,message){return D.alert({title:title,message:message})}
  var TITLES={tree:'Notas',recent:'Recentes',favorites:'Favoritos',compositions:'Composições',trash:'Lixeira',search:'Busca'};

  function ensure(){
    if(root)return;
    root=document.createElement('section');root.id='urbeMobileExplorer';root.hidden=true;root.setAttribute('aria-label','Notas');
    root.innerHTML='<header><button type="button" data-back aria-label="Voltar para todas as notas" hidden>'+ic('back')+'</button><strong>Notas</strong><div>'+
      '<button type="button" data-new title="Nova nota" aria-label="Nova nota">'+ic('plus')+'<span>Nova</span></button>'+
      '<button type="button" data-more title="Mais opções" aria-label="Mais opções">'+ic('more')+'</button>'+
      '<button type="button" data-close title="Fechar" aria-label="Fechar">'+ic('close')+'</button></div></header>'+
      '<div class="ume-search"><label>'+ic('search')+'<input type="search" placeholder="Buscar notas" aria-label="Buscar notas" autocomplete="off" spellcheck="false"></label></div>'+
      '<div class="ume-crumb" hidden></div>'+
      '<div class="ume-list" role="tree"></div>'+
      '<nav class="ume-actions" hidden><span data-count></span><button data-open>Abrir</button><button data-favorite>Favoritar</button><button data-compose>Compor</button><button data-duplicate>Duplicar</button><button data-delete>Excluir</button><button data-clear aria-label="Limpar seleção">'+ic('close')+'</button></nav>';
    document.body.appendChild(root);
    list=root.querySelector('.ume-list');bar=root.querySelector('.ume-actions');search=root.querySelector('.ume-search input');crumb=root.querySelector('.ume-crumb');
    root.querySelector('[data-close]').onclick=close;
    root.querySelector('[data-back]').onclick=function(){show('tree')};
    root.querySelector('[data-new]').onclick=newMenu;
    root.querySelector('[data-more]').onclick=moreMenu;
    root.querySelector('[data-clear]').onclick=function(){model.clear()};
    root.querySelector('[data-open]').onclick=openSelected;root.querySelector('[data-compose]').onclick=composeSelected;root.querySelector('[data-favorite]').onclick=favoriteSelected;root.querySelector('[data-duplicate]').onclick=duplicateSelected;root.querySelector('[data-delete]').onclick=deleteSelected;
    search.addEventListener('input',function(){query=search.value.trim();show(query?'search':'tree')});
    root.addEventListener('keydown',function(e){if(e.key==='Escape'&&!document.querySelector('.udlg')){if(query){search.value='';query='';show('tree')}else close()}});
  }

  function row(node,depth,withPath){
    var selected=model.selection.has(node.id),folder=node.kind==='folder',open=folder&&model.isExpanded(node.path);
    return '<button type="button" role="treeitem" class="ume-row'+(selected?' selected':'')+(open?' expanded':'')+'" data-id="'+esc(node.id)+'" data-kind="'+node.kind+'" style="--depth:'+depth+'"'+(folder?' aria-expanded="'+open+'"':'')+'>'+
      '<span class="ume-icon">'+ic(folder?'folder':'file')+'</span><span class="ume-name">'+esc(node.name)+'</span>'+
      (folder?'<span class="ume-caret">'+ic('chevron')+'</span>':'')+(withPath&&!folder&&node.path.includes('/')?'<small>'+esc(node.path.slice(0,node.path.lastIndexOf('/')))+'</small>':'')+'</button>';
  }
  function flatten(node,depth,out){node.children.forEach(function(n){out.push(row(n,depth));if(n.kind==='folder'&&model.isExpanded(n.path))flatten(n,depth+1,out)})}
  function empty(title,text,action){return '<div class="ume-empty"><strong>'+esc(title)+'</strong>'+esc(text)+(action?'<div style="margin-top:18px"><button type="button" class="ui-btn ui-btn-primary" data-empty-action>'+esc(action)+'</button></div>':'')+'</div>'}
  function setHeader(){
    root.querySelector('header strong').textContent=TITLES[view]||'Notas';
    root.querySelector('[data-back]').hidden=view==='tree'||view==='search';
    root.querySelector('.ume-search').hidden=!(view==='tree'||view==='search');
  }
  function show(next){view=next;render()}
  function render(){
    ensure();setHeader();crumb.hidden=true;
    if(view==='search')return renderSearch();
    if(view==='recent')return renderCollection(model.listRecent(),'Nada recente','As notas que você abrir aparecem aqui.');
    if(view==='favorites')return renderCollection(model.listFavorites(),'Nenhum favorito','Selecione notas (toque e segure) e escolha Favoritar.');
    if(view==='compositions')return renderCompositions();
    if(view==='trash')return renderTrash();
    var out=[];flatten(model.tree(),0,out);
    list.innerHTML=out.join('')||empty('Nenhuma nota ainda','Cada nota vira uma casa na sua cidade.','Criar primeira nota');
    bindEmpty();bindRows();renderBar();
  }
  function renderSearch(){
    var q=query.toLowerCase(),docs=core.service('documents'),hits=(docs?docs.list():[]).filter(function(d){return d.path.toLowerCase().includes(q)||(d.title||'').toLowerCase().includes(q)}).slice(0,200);
    list.innerHTML=hits.map(function(d){return row({id:d.id,kind:'document',name:d.title||d.path,path:d.path},0,true)}).join('')||empty('Nada encontrado','Nenhuma nota com “'+query+'”.');
    bindRows();renderBar();
  }
  function renderCollection(nodes,title,text){list.innerHTML=nodes.map(function(n){return row(n,0,true)}).join('')||empty(title,text);bindRows();renderBar()}
  function renderTrash(){
    var items=trash?trash.list():[];
    list.innerHTML=items.map(function(item){var d=item.document;return '<div class="ume-row" data-kind="trash" style="--depth:0"><span class="ume-icon">'+ic('file')+'</span><span class="ume-name">'+esc(d.title)+'</span><button type="button" class="ui-btn" style="grid-row:1/3;grid-column:3;min-height:36px;padding:0 12px;font-size:13px" data-restore="'+esc(d.id)+'">'+ic('restore')+'Restaurar</button><small>'+esc(item.originalPath)+'</small></div>'}).join('')||empty('A lixeira está vazia','Notas excluídas ficam aqui e podem ser restauradas.');
    Array.prototype.forEach.call(list.querySelectorAll('[data-restore]'),function(el){el.onclick=function(){try{trash.restore(el.getAttribute('data-restore'));renderTrash()}catch(err){notify('Não foi possível restaurar',err.message)}}});bar.hidden=true;
  }
  function renderCompositions(){
    var store=core.service('compositions'),items=store?store.list():[];
    list.innerHTML=items.map(function(x){return '<button type="button" class="ume-row" data-composition="'+esc(x.id)+'" style="--depth:0"><span class="ume-icon">'+ic('layers')+'</span><span class="ume-name">'+esc(x.name)+'</span><small>'+x.sources.length+' fonte(s)</small></button>'}).join('')||empty('Nenhuma composição','Selecione notas (toque e segure) e escolha Compor para juntar tudo num documento.');
    list.querySelectorAll('[data-composition]').forEach(function(el){el.onclick=function(){var id=el.dataset.composition;close();core.commands.execute('ui.composition.open',{id:id})}});bar.hidden=true;
  }
  function bindEmpty(){var b=list.querySelector('[data-empty-action]');if(b)b.onclick=createNote}
  function bindRows(){Array.prototype.forEach.call(list.querySelectorAll('.ume-row'),function(el){
    var id=el.getAttribute('data-id'),kind=el.getAttribute('data-kind'),moved=false;
    var origin=null,longPressed=false,lastPointer=0;
    el.addEventListener('pointerdown',function(e){origin={x:e.clientX,y:e.clientY,id:e.pointerId};moved=false;longPressed=false;hold=setTimeout(function(){hold=null;longPressed=true;model.select(id,'toggle');navigator.vibrate&&navigator.vibrate(18)},HOLD)});
    el.addEventListener('pointermove',function(e){if(!origin||e.pointerId!==origin.id)return;if(Math.hypot(e.clientX-origin.x,e.clientY-origin.y)>12){moved=true;if(hold){clearTimeout(hold);hold=null}}});
    el.addEventListener('pointerup',function(e){lastPointer=Date.now();if(e.pointerType==='mouse'&&e.button!==0)return;if(hold){clearTimeout(hold);hold=null;if(moved)return;if(model.selection.size){model.select(id,'toggle');return}if(kind==='folder'){var n=model.node(id);model.expand(n.path,!model.isExpanded(n.path));return}openId(id)}origin=null});
    el.addEventListener('pointercancel',function(){if(hold)clearTimeout(hold);hold=null});
    el.addEventListener('click',function(){if(Date.now()-lastPointer<600||longPressed)return;if(kind==='folder'){var n=model.node(id);model.expand(n.path,!model.isExpanded(n.path))}else openId(id)});
  })}
  function renderBar(){var n=model.selection.size;bar.hidden=!n;bar.querySelector('[data-count]').textContent=n+' selecionada'+(n===1?'':'s')}
  function openId(id){var n=model.node(id);if(!n||n.kind!=='document')return;model.touchRecent(id);core.commands.execute('document.open',{id:id,source:'mobile-explorer'});close()}
  function openSelected(){var n=model.selected().find(x=>x.kind==='document');if(n)openId(n.id)}
  function favoriteSelected(){model.selected().forEach(n=>model.favorite(n.id,true));model.clear()}
  function composeSelected(){var ids=model.selected().filter(n=>n.kind==='document').map(n=>n.id);if(ids.length)core.commands.execute('ui.composition.create',{ids:ids,type:'document'});model.clear();close()}
  function duplicateSelected(){model.selected().filter(n=>n.kind==='document').forEach(n=>core.commands.execute('explorer.duplicate',{id:n.id}));model.clear()}
  function deleteSelected(){var ids=model.selected().filter(n=>n.kind==='document').map(n=>n.id);if(!ids.length)return;
    confirmAction({title:'Mover para a lixeira?',message:ids.length===1?'A nota poderá ser restaurada pela Lixeira.':ids.length+' notas poderão ser restauradas pela Lixeira.',confirm:'Mover para a lixeira',danger:true}).then(function(ok){if(ok)core.commands.execute('explorer.delete',{ids:ids})})}
  function currentFolder(){var folder=model.selected().find(n=>n.kind==='folder');return folder?folder.path:''}
  function cleanName(s){return String(s||'').trim().replace(/[\\/:*?"<>|\u0000-\u001f]/g,'-').replace(/^\.+|\.+$/g,'').slice(0,90)}
  function where(folder){return folder?'Em “'+folder+'”.':'Na raiz das notas.'}
  function newMenu(){
    D.menu('Criar',[{icon:'file',label:'Nova nota',detail:where(currentFolder()),run:createNote},{icon:'folder',label:'Nova pasta',detail:'Vira uma região na cidade.',run:createFolder}]);
  }
  function moreMenu(){
    var storage=core.service('workspace.storage');
    var items=[
      {icon:'clock',label:'Recentes',run:function(){show('recent')}},
      {icon:'star',label:'Favoritos',run:function(){show('favorites')}},
      {icon:'layers',label:'Composições',run:function(){show('compositions')}},
      trash?{icon:'trash',label:'Lixeira',detail:(trash.list().length||'Nenhum')+' item(ns)',run:function(){show('trash')}}:null,
      storage?{icon:'storage',label:'Pasta do Urbe no dispositivo',detail:'Escolher onde os arquivos ficam salvos',run:function(){storage.chooseRoot()}}:null
    ];
    D.menu('Notas',items);
  }
  async function createFolder(){
    var folder=currentFolder();
    var raw=await ask({title:'Nova pasta',label:'Nome da pasta',hint:where(folder)+' Cada pasta vira uma região da cidade.',confirm:'Criar pasta',placeholder:'Ex.: Projetos',validate:function(v){return cleanName(v)?'':'Use um nome válido.'}});
    var name=cleanName(raw);if(!name)return;
    var path=(folder?folder+'/':'')+name,p=core.service('persistence');
    try{if(p&&p.adapter&&p.vault)await p.adapter.createFolder(p.vault,path);model.addFolder(path);model.expand(path,true);model.clear();show('tree')}catch(e){notify('Não foi possível criar a pasta',e.message)}
  }
  async function createNote(){
    var folder=currentFolder(),docs=core.service('documents');
    var raw=await ask({title:'Nova nota',label:'Nome',value:'',placeholder:'Ex.: Ideias',hint:where(folder)+' Sem extensão, será salva como .md.',confirm:'Criar nota',
      validate:function(v){var n=cleanName(v);if(!n)return 'Use um nome válido.';if(!/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i.test(n))n+='.md';return docs.get((folder?folder+'/':'')+n)?'Já existe uma nota com esse nome aqui.':''}});
    var name=cleanName(raw);if(!name)return;
    if(!/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i.test(name))name+='.md';
    var path=(folder?folder+'/':'')+name;if(docs.get(path))return;
    var doc=docs.upsert({path:path,content:''},{source:'explorer.create'});model.clear();core.commands.execute('document.open',{id:doc.id,source:'mobile-explorer'});close();
  }
  function open(){ensure();root.hidden=false;if(view!=='tree'&&view!=='search')view='tree';render();core.events.emit('explorer:visibility',{open:true})}
  function close(){if(root&&!root.hidden){root.hidden=true;core.events.emit('explorer:visibility',{open:false})}}
  core.events.on('explorer:changed',function(){if(root&&!root.hidden)render()});
  core.events.on('trash:changed',function(){if(root&&!root.hidden&&view==='trash')render()});
  // Abrir um documento por qualquer caminho (atalho, paleta, link) revela o editor.
  core.events.on('command:after',function(e){if(e&&e.id==='document.open')close()});
  core.commands.register('ui.explorer.open',{title:'Abrir notas',category:'Navegação',execute:open});
  core.provide('explorer.ui',{open:open,close:close,isOpen:function(){return !!root&&!root.hidden},createNote:createNote,createFolder:createFolder});
})(window);
