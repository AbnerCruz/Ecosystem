(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents');if(!core||!docs)return;
  var root,input,list,results=[],active=0,lastFocus=null;
  function esc(v){return String(v==null?'':v).replace(/[&<>"]/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]})}
  function ic(n){return global.UrbeIcons?global.UrbeIcons.icon(n):''}
  /* Comparação sem acentos e sem caixa; os índices continuam válidos no texto
     original porque a remoção de diacríticos é feita caractere a caractere. */
  function fold(s){return String(s||'').split('').map(function(c){return c.normalize('NFD').replace(/[̀-ͯ]/g,'')||c}).join('').toLowerCase()}

  /* Pontuação: título igual > começa com > contém > todas as palavras no título > conteúdo. */
  function rank(all,query,limit){
    var q=fold(query).trim(),max=limit||50;if(!q)return [];
    var words=q.split(/\s+/).filter(Boolean),out=[];
    all.forEach(function(d){
      /* o cabeçalho que repete o título não conta como trecho do conteúdo */
      var content=String(d.content||''),head=content.match(/^\s*#\s+(.+)\n?/),skip=head&&fold(head[1]).trim()===fold(d.title).trim()?head[0].length:0;
      var title=fold(d.title),path=fold(d.path),body=fold(content.slice(skip)),score=0,snippet=null;d={id:d.id,title:d.title,path:d.path,content:content.slice(skip),source:d};
      if(title===q)score=1000;else if(title.startsWith(q))score=700;else if(title.includes(q))score=500;
      else if(words.every(function(w){return title.includes(w)}))score=400;
      else if(path.includes(q))score=300;
      var at=body.indexOf(q);
      if(at<0&&words.length>1&&words.every(function(w){return body.includes(w)}))at=body.indexOf(words[0]);
      if(at>=0){
        if(!score)score=100;
        score+=Math.min(50,(body.split(q).length-1)*5);
        var len=at===body.indexOf(q)?q.length:words[0].length,start=Math.max(0,at-36),end=Math.min(d.content.length,at+len+64);
        snippet={before:(start>0?'…':'')+d.content.slice(start,at).replace(/\s+/g,' ').replace(/^#+\s*/,''),match:d.content.slice(at,at+len),after:d.content.slice(at+len,end).replace(/\s+/g,' ')+(end<d.content.length?'…':'')};
      }
      if(score)out.push({doc:d.source,score:score,snippet:snippet});
    });
    return out.sort(function(a,b){return b.score-a.score||a.doc.title.localeCompare(b.doc.title)}).slice(0,max);
  }

  function ensure(){
    if(root)return;
    root=document.createElement('div');root.id='urbeQuickOpen';root.hidden=true;
    root.innerHTML='<div class="uqo-backdrop"></div><section class="uqo-panel" role="dialog" aria-modal="true" aria-label="Buscar notas"><label class="uqo-field">'+ic('search')+'<input class="uqo-input" type="search" autocomplete="off" spellcheck="false" placeholder="Buscar ou criar nota…" aria-controls="uqoList"></label><div class="uqo-list" id="uqoList" role="listbox"></div><footer class="uqo-foot"><span><kbd>↑</kbd><kbd>↓</kbd> navegar</span><span><kbd>Enter</kbd> abrir</span><span><kbd>Esc</kbd> fechar</span></footer></section>';
    document.body.appendChild(root);input=root.querySelector('.uqo-input');list=root.querySelector('.uqo-list');
    root.querySelector('.uqo-backdrop').onclick=close;input.oninput=function(){active=0;render()};
    input.onkeydown=function(e){if(e.key==='Escape'){e.preventDefault();close()}else if(e.key==='ArrowDown'){e.preventDefault();active=Math.min(active+1,results.length-1);paint()}else if(e.key==='ArrowUp'){e.preventDefault();active=Math.max(0,active-1);paint()}else if(e.key==='Enter'&&results[active]){e.preventDefault();pick(results[active])}};
  }
  function recentDocs(){
    var ex=core.service('explorer'),seen=new Set(),out=[];
    (ex&&ex.recent||[]).forEach(function(id){var d=docs.get(id);if(d&&!seen.has(d.id)){seen.add(d.id);out.push(d)}});
    docs.list().slice().sort(function(a,b){return String(b.modified||'').localeCompare(String(a.modified||''))}).forEach(function(d){if(!seen.has(d.id)){seen.add(d.id);out.push(d)}});
    return out.slice(0,12);
  }
  function row(r,i){
    if(r.create)return '<button type="button" role="option" class="uqo-item uqo-create'+(i===active?' active':'')+'" data-i="'+i+'"><span class="uqo-icon">'+ic('plus')+'</span><span class="uqo-text"><strong>Criar nota “'+esc(r.create)+'”</strong><small>Na raiz · vira uma casa nova na cidade</small></span></button>';
    var d=r.doc,folder=d.path.includes('/')?d.path.slice(0,d.path.lastIndexOf('/')):'';
    return '<button type="button" role="option" class="uqo-item'+(i===active?' active':'')+'" data-i="'+i+'" aria-selected="'+(i===active)+'"><span class="uqo-icon">'+ic('file')+'</span><span class="uqo-text"><strong>'+esc(d.title)+'</strong>'+
      (r.snippet?'<small class="uqo-snippet">'+esc(r.snippet.before)+'<mark>'+esc(r.snippet.match)+'</mark>'+esc(r.snippet.after)+'</small>':(folder?'<small>'+esc(folder)+'</small>':''))+'</span></button>';
  }
  function render(){
    var q=input.value.trim(),html='';
    if(!q){results=recentDocs().map(function(d){return{doc:d}});html=results.length?'<div class="uqo-group">Recentes</div>':''}
    else{
      results=rank(docs.list(),q,40);
      var exact=results.some(function(r){return fold(r.doc.title)===fold(q)});
      if(!exact&&!/[\\/:*?"<>|]/.test(q))results.push({create:q});
    }
    html+=results.map(row).join('')||'<div class="uqo-empty">Nenhuma nota ainda. Digite um nome para criar a primeira.</div>';
    list.innerHTML=html;active=Math.min(active,Math.max(0,results.length-1));
    Array.prototype.forEach.call(list.querySelectorAll('[data-i]'),function(el){el.onclick=function(){pick(results[+el.dataset.i])}});
  }
  function paint(){Array.prototype.forEach.call(list.querySelectorAll('.uqo-item'),function(el,i){el.classList.toggle('active',i===active);el.setAttribute('aria-selected',i===active)});var el=list.querySelectorAll('.uqo-item')[active];if(el)el.scrollIntoView({block:'nearest'})}
  function pick(r){
    if(!r)return;
    if(r.create){var name=r.create.replace(/\.(md|markdown)$/i,''),path=name+'.md',existing=docs.get(path);var d=existing||docs.upsert({path:path,content:''},{source:'quick-open.create'});openDoc(d);return}
    openDoc(r.doc);
  }
  function open(){ensure();lastFocus=document.activeElement;root.hidden=false;input.value='';active=0;render();input.focus()}
  function close(){if(root)root.hidden=true;if(lastFocus&&lastFocus.focus&&document.contains(lastFocus))lastFocus.focus()}
  function openDoc(doc){if(!doc)return;var ex=core.service('explorer');if(ex&&ex.touchRecent)ex.touchRecent(doc.id);lastFocus=null;close();core.commands.execute('document.open',{id:doc.id,path:doc.path,source:'quick-open'})}
  core.commands.register('ui.quickOpen.open',{title:'Buscar notas',category:'Navegação',execute:open});
  core.provide('quickOpen',{open:open,close:close,rank:rank});
})(window);
