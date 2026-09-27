(function(global){
  'use strict';
  /* Estúdio de páginas: editor visual das páginas (.page.json) do vault.
     Prévia ao vivo (a mesma saída do exportador) num iframe isolado, lista de
     seções com reordenação por arraste, inspetor gerado a partir dos campos de
     cada bloco, tema completo, JSON com validação, desfazer/refazer, salvamento
     automático, modelos e atalhos de teclado. Funciona igual no celular (abas e
     folha inferior) e no computador (painéis laterais). */
  var doc=global.document,core=global.UrbeCore,P=global.UrbePages,TPL=global.UrbePageTemplates,D=global.UrbeDialogs;
  if(!doc||!core||!P||!TPL)return;
  var docs=core.service('documents');if(!docs)return;
  function ic(n){return global.UrbeIcons?global.UrbeIcons.icon(n):''}
  var esc=P.esc;
  var SVG={
    undo:'<svg viewBox="0 0 24 24" class="ui-icon"><path d="M9 14 4 9l5-5"/><path d="M4 9h10.5a5.5 5.5 0 0 1 0 11H11"/></svg>',
    redo:'<svg viewBox="0 0 24 24" class="ui-icon"><path d="m15 14 5-5-5-5"/><path d="M20 9H9.5a5.5 5.5 0 0 0 0 11H13"/></svg>',
    phone:'<svg viewBox="0 0 24 24" class="ui-icon"><rect x="7" y="2.5" width="10" height="19" rx="2.5"/><path d="M11 18.5h2"/></svg>',
    tablet:'<svg viewBox="0 0 24 24" class="ui-icon"><rect x="4.5" y="2.5" width="15" height="19" rx="2.5"/><path d="M11 18.5h2"/></svg>',
    desktop:'<svg viewBox="0 0 24 24" class="ui-icon"><rect x="2.5" y="4" width="19" height="12.5" rx="2"/><path d="M8.5 20.5h7M12 16.5v4"/></svg>',
    eye:'<svg viewBox="0 0 24 24" class="ui-icon"><path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/></svg>',
    eyeOff:'<svg viewBox="0 0 24 24" class="ui-icon"><path d="M3 3l18 18"/><path d="M10.6 5.1A10.4 10.4 0 0 1 12 5c6.4 0 10 7 10 7a17 17 0 0 1-3.2 4.1M6.6 6.6C3.8 8.4 2 12 2 12s3.6 7 10 7a9.8 9.8 0 0 0 5.4-1.6"/><path d="M9.9 9.9a3 3 0 0 0 4.2 4.2"/></svg>',
    grip:'<svg viewBox="0 0 24 24" class="ui-icon"><circle cx="9" cy="6" r="1.4"/><circle cx="15" cy="6" r="1.4"/><circle cx="9" cy="12" r="1.4"/><circle cx="15" cy="12" r="1.4"/><circle cx="9" cy="18" r="1.4"/><circle cx="15" cy="18" r="1.4"/></svg>',
    palette:'<svg viewBox="0 0 24 24" class="ui-icon"><path d="M12 3a9 9 0 1 0 0 18c1.1 0 1.7-.8 1.7-1.7 0-.5-.2-.9-.5-1.2-.3-.3-.5-.7-.5-1.2 0-.9.8-1.7 1.7-1.7h2A4.6 4.6 0 0 0 21 10.6C21 6.4 17 3 12 3z"/><circle cx="7.5" cy="11" r="1.2"/><circle cx="10" cy="7" r="1.2"/><circle cx="15" cy="7" r="1.2"/></svg>',
    page:'<svg viewBox="0 0 24 24" class="ui-icon"><rect x="3.5" y="3.5" width="17" height="17" rx="3"/><path d="M3.5 9h17M9 20.5V9"/></svg>',
    json:'<svg viewBox="0 0 24 24" class="ui-icon"><path d="M8 3H7a2 2 0 0 0-2 2v4a2 2 0 0 1-2 2 2 2 0 0 1 2 2v4a2 2 0 0 0 2 2h1M16 3h1a2 2 0 0 1 2 2v4a2 2 0 0 0 2 2 2 2 0 0 0-2 2v4a2 2 0 0 1-2 2h-1"/></svg>',
    play:'<svg viewBox="0 0 24 24" class="ui-icon"><path d="M7 4.5v15l12-7.5z"/></svg>',
    keyboard:'<svg viewBox="0 0 24 24" class="ui-icon"><rect x="2.5" y="6" width="19" height="12" rx="2"/><path d="M6.5 10h.01M10 10h.01M14 10h.01M17.5 10h.01M7 14h10"/></svg>'};
  function sv(n){return SVG[n]||ic(n)}
  var isMac=/Mac|iPhone|iPad/.test(global.navigator&&global.navigator.platform||'');
  var MOD=isMac?'⌘':'Ctrl';

  var st={root:null,mode:'closed',docId:null,spec:null,sel:null,hist:[],fut:[],lastKey:'',lastAt:0,leftTab:'sections',sheet:null,device:'desktop',status:'saved',saveT:0,renderT:0,frames:[],front:0,y:0,savedText:'',filter:''};
  var wide=function(){return global.innerWidth>=1000};

  /* ---------------- utilidades ---------------- */
  function pathGet(o,p){return p.split('.').reduce(function(a,k){return a==null?a:a[k]},o)}
  function pathSet(o,p,v){var ks=p.split('.'),last=ks.pop(),t=ks.reduce(function(a,k){return a[k]},o);t[last]=v}
  function snapshot(){return JSON.stringify(st.spec)}
  function section(id){return st.spec&&st.spec.sections.find(function(s){return s.id===id})||null}
  function secIndex(id){return st.spec.sections.findIndex(function(s){return s.id===id})}
  function secTitle(s){var b=P.BLOCKS[s.type],p=s.props,t=p.title||p.name||p.q||(s.type==='note'?String(p.path||'').split('/').pop().replace(/\.md$/i,''):'')||(s.type==='text'?P.excerpt(p.markdown,40):'')||(s.type==='quote'?P.excerpt(p.text,40):'');return{label:b?b.label:s.type,title:String(t||'').replace(/\*\*/g,''),icon:b?b.icon:'?'}}
  function pageDocs(){return docs.list().filter(function(d){return P.isPagePath(d.path)&&!P.isTemplatePath(d.path)}).sort(function(a,b){return String(b.modified||'').localeCompare(String(a.modified||''))})}
  function templateDocs(){return docs.list().filter(function(d){return P.isTemplatePath(d.path)})}
  function parse(d){try{return JSON.parse(d.content||'{}')}catch(_){return null}}
  function cleanName(s){return String(s||'').trim().replace(/[\\/:*?"<>|\u0000-\u001f]/g,'-').replace(/^\.+|\.+$/g,'').slice(0,80)}
  function uniquePath(base){var p=base,i=2;while(docs.get(p))p=base.replace(/(\.(page|template)\.json)$/i,' ('+(i++)+')$1');return p}
  function toast(msg,action){[].forEach.call((st.root||doc).querySelectorAll('.ps-toast'),function(x){x.remove()});var t=doc.createElement('div');t.className='ps-toast';t.innerHTML='<span>'+esc(msg)+'</span>'+(action?'<button type="button">'+esc(action.label)+'</button>':'');(st.root||doc.body).appendChild(t);
    if(action)t.querySelector('button').onclick=function(){action.run();t.remove()};requestAnimationFrame(function(){t.classList.add('on')});setTimeout(function(){t.classList.remove('on');setTimeout(function(){t.remove()},300)},action?5000:2200)}
  function typing(e){var t=e.target;return t&&(t.tagName==='INPUT'||t.tagName==='TEXTAREA'||t.tagName==='SELECT'||t.isContentEditable)}

  /* ---------------- montagem ---------------- */
  function ensure(){
    if(st.root)return;
    var r=doc.createElement('section');r.id='pageStudio';r.hidden=true;r.setAttribute('aria-label','Estúdio de páginas');
    r.innerHTML='<header class="ps-top"><button type="button" class="ps-ib" data-a="back" aria-label="Voltar" title="Voltar (Esc)">'+ic('back')+'</button>'+
      '<div class="ps-name"><input data-name aria-label="Título da página" spellcheck="false"><small data-status></small></div>'+
      '<div class="ps-devices" role="radiogroup" aria-label="Tamanho da prévia"><button type="button" data-dev="mobile" title="Celular (1)">'+sv('phone')+'</button><button type="button" data-dev="tablet" title="Tablet (2)">'+sv('tablet')+'</button><button type="button" data-dev="desktop" title="Computador (3)">'+sv('desktop')+'</button></div>'+
      '<div class="ps-acts"><button type="button" class="ps-ib" data-a="undo" title="Desfazer ('+MOD+'+Z)" aria-label="Desfazer">'+sv('undo')+'</button><button type="button" class="ps-ib" data-a="redo" title="Refazer ('+MOD+'+Shift+Z)" aria-label="Refazer">'+sv('redo')+'</button>'+
      '<button type="button" class="ps-ib ps-ai" data-a="ai" title="Pedir ao Assistente" aria-label="Pedir ao Assistente">'+ic('sparkle')+'</button><button type="button" class="ps-ib ps-hide-s" data-a="json" title="JSON ('+MOD+'+J)" aria-label="JSON">'+sv('json')+'</button>'+
      '<button type="button" class="ps-ib ps-hide-s" data-a="play" title="Abrir em nova aba ('+MOD+'+P)" aria-label="Abrir em nova aba">'+sv('play')+'</button><button type="button" class="ps-btn" data-a="export" title="Exportar ('+MOD+'+E)">'+ic('download')+'<span>Exportar</span></button><button type="button" class="ps-ib" data-a="more" aria-label="Mais opções">'+ic('more')+'</button></div></header>'+
      '<div class="ps-body"><aside class="ps-left"><div class="ps-tabs" role="tablist"><button type="button" data-lt="sections">Seções</button><button type="button" data-lt="theme">Tema</button><button type="button" data-lt="page">Página</button></div><div class="ps-panel" data-panel="left"></div></aside>'+
      '<main class="ps-stage"><div class="ps-frame" data-device="desktop"><iframe title="Prévia da página" sandbox="allow-scripts allow-popups"></iframe><iframe title="Prévia da página" sandbox="allow-scripts allow-popups" class="back"></iframe></div></main>'+
      '<aside class="ps-right"><div class="ps-panel" data-panel="right"></div></aside></div>'+
      '<div class="ps-sheet" hidden><div class="ps-grab" aria-hidden="true"><span></span></div><div class="ps-sheet-head"></div><div class="ps-panel" data-panel="sheet"></div></div>'+
      '<nav class="ps-tabbar" aria-label="Ferramentas"><button type="button" data-t="sections">'+ic('list')+'<span>Seções</span></button><button type="button" data-t="edit">'+ic('edit')+'<span>Editar</span></button><button type="button" class="ps-add" data-a="add" aria-label="Adicionar bloco">'+ic('plus')+'</button><button type="button" data-t="theme">'+sv('palette')+'<span>Tema</span></button><button type="button" data-t="page">'+sv('page')+'<span>Página</span></button></nav>'+
      '<div class="ps-home" hidden></div><div class="ps-json" hidden></div>';
    doc.body.appendChild(r);st.root=r;st.frames=[].slice.call(r.querySelectorAll('iframe'));
    r.addEventListener('click',onClick);
    r.querySelector('[data-name]').addEventListener('input',function(e){commit(function(s){s.meta.title=e.target.value},{key:'meta.title',panels:false})});
    r.addEventListener('input',onInput);r.addEventListener('change',onChange);
    global.addEventListener('message',onFrameMessage);
    doc.addEventListener('keydown',onKey,true);
    global.addEventListener('resize',function(){if(st.mode==='edit'){layout();renderPanels()}});
    sheetGestures(r.querySelector('.ps-sheet'));
  }

  /* ---------------- abrir / fechar ---------------- */
  function open(id){
    var d=docs.get(id);if(!d)return false;ensure();var raw=parse(d);
    if(raw==null){D.alert({title:'JSON inválido',message:'O arquivo '+d.path+' não é um JSON válido. Abra como texto para corrigir, ou use “JSON” para colar uma versão válida.'});raw={}}
    var n=P.normalize(raw);st.docId=d.id;st.spec=n.spec;st.hist=[];st.fut=[];st.sel=null;st.savedText=d.content;st.y=0;st.mode='edit';st.sheet=null;
    st.root.hidden=false;st.root.querySelector('.ps-home').hidden=true;st.root.classList.remove('is-home');
    if(n.errors.length)toast(n.errors.length+' problema(s) no arquivo foram corrigidos ao abrir.');
    if(!wide()&&st.device==='desktop')st.device='mobile';
    layout();renderAll();core.events.emit('pages:studio',{open:true,id:d.id});return true;
  }
  function close(){if(!st.root||st.root.hidden)return;flush();st.root.hidden=true;st.mode='closed';closeJson();core.events.emit('pages:studio',{open:false})}
  function current(){if(st.mode!=='edit'||!st.docId)return null;var d=docs.get(st.docId);return d?{id:d.id,path:d.path}:null}

  /* ---------------- histórico e salvamento ---------------- */
  function commit(fn,o){
    o=o||{};var now=Date.now(),snap=snapshot();
    if(!(o.key&&o.key===st.lastKey&&now-st.lastAt<900)){st.hist.push(snap);if(st.hist.length>150)st.hist.shift();st.fut=[]}
    st.lastKey=o.key||'';st.lastAt=now;fn(st.spec);
    if(o.panels!==false)renderPanels();else renderChrome();
    schedulePreview(o.instant);scheduleSave();
  }
  function undo(){if(!st.hist.length)return toast('Nada para desfazer.');st.fut.push(snapshot());st.spec=JSON.parse(st.hist.pop());st.lastKey='';if(st.sel&&!section(st.sel))st.sel=null;renderAll();scheduleSave()}
  function redo(){if(!st.fut.length)return toast('Nada para refazer.');st.hist.push(snapshot());st.spec=JSON.parse(st.fut.pop());st.lastKey='';renderAll();scheduleSave()}
  function scheduleSave(){st.status='dirty';renderStatus();clearTimeout(st.saveT);st.saveT=setTimeout(save,600)}
  function save(){clearTimeout(st.saveT);var d=docs.get(st.docId);if(!d||!st.spec)return;var text=JSON.stringify(st.spec,null,2)+'\n';
    if(text!==d.content){st.savedText=text;docs.upsert(Object.assign({},d,{id:d.id,content:text}),{source:'pages.studio'})}st.status='saved';renderStatus()}
  function flush(){if(st.status==='dirty')save()}
  function renderStatus(){var el=st.root&&st.root.querySelector('[data-status]');if(el)el.textContent=st.status==='dirty'?'Salvando…':'Salvo'}

  /* ---------------- prévia (dois iframes: troca sem piscar) ---------------- */
  function html(preview){return P.render(st.spec,{documents:docs,preview:preview})}
  function schedulePreview(instant){clearTimeout(st.renderT);st.renderT=setTimeout(renderPreview,instant?0:140)}
  function renderPreview(){if(st.mode!=='edit')return;var back=st.frames[1-st.front];back.srcdoc=html(true);back.dataset.pending='1'}
  function onFrameMessage(e){
    if(!st.root||st.mode!=='edit')return;var m=e.data||{},fi=st.frames.findIndex(function(f){return f.contentWindow===e.source});if(fi<0||!m.urbePage)return;
    var f=st.frames[fi];
    if(m.urbePage==='ready'&&f.dataset.pending){delete f.dataset.pending;f.contentWindow.postMessage({urbePage:'scrollTo',y:st.y},'*');if(st.sel)f.contentWindow.postMessage({urbePage:'select',id:st.sel},'*');
      requestAnimationFrame(function(){f.classList.remove('back');st.frames[1-fi].classList.add('back');st.front=fi})}
    else if(m.urbePage==='scroll'&&fi===st.front)st.y=m.y||0;
    else if(m.urbePage==='select'&&fi===st.front){select(m.id,{from:'preview'})}
  }
  function postFront(msg){var f=st.frames[st.front];if(f&&f.contentWindow)f.contentWindow.postMessage(msg,'*')}
  function select(id,o){o=o||{};st.sel=id&&section(id)?id:null;postFront({urbePage:'select',id:st.sel,scroll:o.from!=='preview',instant:o.instant});
    if(!wide()&&st.sel&&o.from!=='keys')openSheet('edit');renderPanels();
    if(st.sel&&o.from!=='list'){var row=st.root.querySelector('.ps-sec[data-id="'+st.sel+'"]');if(row)row.scrollIntoView({block:'nearest'})}}

  /* ---------------- layout: painéis (computador) ou folha (celular) ---------------- */
  function layout(){var r=st.root;r.classList.toggle('is-wide',wide());var fr=r.querySelector('.ps-frame');fr.dataset.device=st.device;
    r.querySelectorAll('[data-dev]').forEach(function(b){b.classList.toggle('on',b.dataset.dev===st.device);b.setAttribute('aria-checked',b.dataset.dev===st.device)});
    r.querySelectorAll('[data-lt]').forEach(function(b){b.classList.toggle('on',b.dataset.lt===st.leftTab)});
    r.querySelectorAll('[data-t]').forEach(function(b){b.classList.toggle('on',b.dataset.t===st.sheet)});
    var sh=r.querySelector('.ps-sheet');sh.hidden=wide()||!st.sheet;r.classList.toggle('sheet-open',!sh.hidden)}
  function openSheet(name){if(wide()){if(name!=='edit'){st.leftTab=name}layout();renderPanels();return}st.sheet=st.sheet===name&&name!=='edit'?null:name;var sh=st.root.querySelector('.ps-sheet');sh.classList.remove('full');layout();renderPanels()}
  function closeSheet(){st.sheet=null;layout()}
  function sheetGestures(sh){var y0=null,h0=0;var grab=sh.querySelector('.ps-grab');
    grab.addEventListener('pointerdown',function(e){y0=e.clientY;h0=sh.getBoundingClientRect().height;grab.setPointerCapture(e.pointerId);sh.style.transition='none'});
    grab.addEventListener('pointermove',function(e){if(y0==null)return;var dy=e.clientY-y0;sh.style.height=Math.max(120,Math.min(global.innerHeight-60,h0-dy))+'px'});
    function end(e){if(y0==null)return;var dy=e.clientY-y0;y0=null;sh.style.transition='';sh.style.height='';if(dy>80)closeSheet();else if(dy<-60)sh.classList.add('full');else if(Math.abs(dy)<6)sh.classList.toggle('full')}
    grab.addEventListener('pointerup',end);grab.addEventListener('pointercancel',end)}

  function renderAll(){renderChrome();renderPanels();schedulePreview(true)}
  function renderChrome(){var r=st.root,n=r.querySelector('[data-name]');if(doc.activeElement!==n)n.value=st.spec.meta.title||'';renderStatus();
    r.querySelector('[data-a="undo"]').disabled=!st.hist.length;r.querySelector('[data-a="redo"]').disabled=!st.fut.length}
  function renderPanels(){if(st.mode!=='edit')return;renderChrome();layout();var r=st.root;
    if(wide()){fill(r.querySelector('[data-panel="left"]'),st.leftTab);fill(r.querySelector('[data-panel="right"]'),'edit')}
    else if(st.sheet){var titles={sections:'Seções',edit:st.sel?secTitle(section(st.sel)).label:'Editar',theme:'Tema',page:'Página'};r.querySelector('.ps-sheet-head').innerHTML='<strong>'+esc(titles[st.sheet])+'</strong><button type="button" class="ps-ib" data-a="sheet-close" aria-label="Fechar">'+ic('close')+'</button>';fill(r.querySelector('[data-panel="sheet"]'),st.sheet)}}
  function fill(el,name){var keep=el.dataset.name===name?el.scrollTop:0;el.dataset.name=name;el.innerHTML=name==='sections'?sectionsHtml():name==='theme'?presetHtml()+'<h4 class="ps-sub">Ajustes</h4>'+formHtml(P.THEME_FIELDS.filter(function(f){return f.key!=='preset'}),st.spec.theme,'theme'):name==='page'?pageHtml():editHtml();el.scrollTop=keep;
    el.querySelectorAll('textarea').forEach(autosize);if(name==='sections')bindDrag(el)}

  /* ---------------- painel: seções ---------------- */
  function sectionsHtml(){var list=st.spec.sections;
    return '<div class="ps-seclist" role="list">'+(list.length?list.map(function(s,i){var t=secTitle(s);return '<div class="ps-sec'+(s.id===st.sel?' on':'')+(s.style.hidden?' off':'')+'" role="listitem" data-id="'+esc(s.id)+'"><span class="ps-grip" data-grip title="Arraste para reordenar">'+sv('grip')+'</span><span class="ps-bi">'+esc(t.icon)+'</span><button type="button" class="ps-sec-main" data-selsec="'+esc(s.id)+'"><strong>'+esc(t.label)+'</strong><small>'+esc(t.title||'—')+'</small></button>'+
      '<button type="button" class="ps-ib sm" data-hide="'+esc(s.id)+'" title="'+(s.style.hidden?'Mostrar':'Ocultar')+'">'+sv(s.style.hidden?'eyeOff':'eye')+'</button><button type="button" class="ps-ib sm" data-secmenu="'+esc(s.id)+'" aria-label="Opções da seção">'+ic('more')+'</button></div>'}).join(''):'<div class="ps-empty"><strong>Página vazia</strong>Adicione o primeiro bloco.</div>')+'</div>'+
      '<button type="button" class="ps-addrow" data-a="add">'+ic('plus')+'Adicionar bloco <kbd>/</kbd></button>'}
  function bindDrag(el){var list=el.querySelector('.ps-seclist');if(!list)return;
    list.querySelectorAll('[data-grip]').forEach(function(g){g.addEventListener('pointerdown',function(e){e.preventDefault();var row=g.closest('.ps-sec'),id=row.dataset.id,rows=[].slice.call(list.children),start=e.clientY,rect=row.getBoundingClientRect(),h=rect.height+6,from=rows.indexOf(row),to=from;
      g.setPointerCapture(e.pointerId);row.classList.add('drag');
      function mv(ev){var dy=ev.clientY-start;row.style.transform='translateY('+dy+'px)';to=Math.max(0,Math.min(rows.length-1,from+Math.round(dy/h)));rows.forEach(function(x,k){if(x===row)return;var sh=0;if(from<to&&k>from&&k<=to)sh=-h;if(from>to&&k<from&&k>=to)sh=h;x.style.transform=sh?'translateY('+sh+'px)':''})}
      function up(){g.removeEventListener('pointermove',mv);g.removeEventListener('pointerup',up);g.removeEventListener('pointercancel',up);rows.forEach(function(x){x.style.transform=''});row.classList.remove('drag');if(to!==from)moveSection(id,to-from)}
      g.addEventListener('pointermove',mv);g.addEventListener('pointerup',up);g.addEventListener('pointercancel',up)})})}
  function moveSection(id,delta){var i=secIndex(id),j=Math.max(0,Math.min(st.spec.sections.length-1,i+delta));if(i<0||i===j)return;commit(function(s){var x=s.sections.splice(i,1)[0];s.sections.splice(j,0,x)});postFront({urbePage:'select',id:id,scroll:true})}
  function duplicateSection(id){var i=secIndex(id);if(i<0)return;var copy=JSON.parse(JSON.stringify(st.spec.sections[i]));copy.id=P.uid('s');if(copy.style.anchor)copy.style.anchor+=' 2';commit(function(s){s.sections.splice(i+1,0,copy)});select(copy.id,{from:'keys'})}
  function removeSection(id){var i=secIndex(id);if(i<0)return;var t=secTitle(st.spec.sections[i]);commit(function(s){s.sections.splice(i,1)});if(st.sel===id)st.sel=(st.spec.sections[i]||st.spec.sections[i-1]||{}).id||null;renderPanels();toast(t.label+' removida.',{label:'Desfazer',run:undo})}
  function sectionMenu(id){var i=secIndex(id),n=st.spec.sections.length;D.menu(secTitle(section(id)).label,[
    {icon:'edit',label:'Editar',run:function(){select(id)}},
    i>0?{icon:'arrowUp',label:'Mover para cima',detail:'Alt+↑',run:function(){moveSection(id,-1)}}:null,
    i<n-1?{icon:'arrowUp',label:'Mover para baixo',detail:'Alt+↓',run:function(){moveSection(id,1)}}:null,
    {icon:'copy',label:'Duplicar',detail:MOD+'+D',run:function(){duplicateSection(id)}},
    {icon:'layers',label:'Trocar tipo de bloco…',run:function(){pickBlock(function(type){var old=section(id),nb=P.newSection(type,old.props);commit(function(s){var k=secIndex(id);nb.id=id;nb.style=old.style;s.sections[k]=nb})})}},
    {icon:'trash',label:'Remover',detail:'Delete',danger:true,run:function(){removeSection(id)}}].filter(Boolean))}

  /* ---------------- biblioteca de blocos ---------------- */
  function pickBlock(done){
    var ov=doc.createElement('div');ov.className='ps-lib';var groups={};Object.keys(P.BLOCKS).forEach(function(k){var b=P.BLOCKS[k];(groups[b.group]=groups[b.group]||[]).push(b)});
    ov.innerHTML='<div class="ps-lib-card" role="dialog" aria-modal="true" aria-label="Adicionar bloco"><header><input type="search" placeholder="Buscar bloco…" aria-label="Buscar bloco"><button type="button" class="ps-ib" data-x aria-label="Fechar">'+ic('close')+'</button></header><div class="ps-lib-body">'+
      Object.keys(groups).map(function(g){return '<h4>'+esc(g)+'</h4><div class="ps-lib-grid">'+groups[g].map(function(b){return '<button type="button" data-type="'+b.type+'" data-q="'+esc((b.label+' '+b.description+' '+b.type).toLowerCase())+'"><span class="ps-bi">'+esc(b.icon)+'</span><strong>'+esc(b.label)+'</strong><small>'+esc(b.description)+'</small></button>'}).join('')+'</div>'}).join('')+'</div></div>';
    st.root.appendChild(ov);var q=ov.querySelector('input');if(wide())q.focus();
    function shut(){ov.remove()}
    ov.addEventListener('click',function(e){if(e.target===ov||e.target.closest('[data-x]'))return shut();var b=e.target.closest('[data-type]');if(b){shut();done(b.dataset.type)}});
    ov.addEventListener('keydown',function(e){if(e.key==='Escape'){e.stopPropagation();shut()}if(e.key==='Enter'){var f=ov.querySelector('[data-type]:not([hidden])');if(f){shut();done(f.dataset.type)}}});
    q.addEventListener('input',function(){var v=q.value.trim().toLowerCase();ov.querySelectorAll('[data-type]').forEach(function(b){b.hidden=v&&b.dataset.q.indexOf(v)<0});ov.querySelectorAll('h4').forEach(function(h){h.hidden=!!v})});
  }
  function addBlock(){pickBlock(function(type){var s=P.newSection(type),i=st.sel?secIndex(st.sel)+1:st.spec.sections.length;commit(function(sp){sp.sections.splice(i,0,s)});select(s.id)})}

  /* ---------------- formulários gerados dos campos ---------------- */
  function editHtml(){var s=st.sel&&section(st.sel);
    if(!s)return '<div class="ps-empty"><strong>Nada selecionado</strong>'+(wide()?'Clique numa parte da prévia ou numa seção à esquerda.':'Toque numa parte da prévia para editar.')+'</div>';
    var b=P.BLOCKS[s.type],base='sections.'+secIndex(s.id);
    return '<div class="ps-edit-head"><span class="ps-bi">'+esc(b.icon)+'</span><div><strong>'+esc(b.label)+'</strong><small>'+esc(b.description)+'</small></div></div>'+formHtml(b.fields,s.props,base+'.props')+
      '<details class="ps-group"><summary>Aparência da seção</summary>'+formHtml(P.SECTION_FIELDS,s.style,base+'.style')+'</details>'+
      '<div class="ps-edit-foot"><button type="button" class="ps-btn ghost" data-dup="'+esc(s.id)+'">'+ic('copy')+'Duplicar</button><button type="button" class="ps-btn ghost danger" data-del="'+esc(s.id)+'">'+ic('trash')+'Remover</button></div>'}
  function pageHtml(){return '<p class="ps-hint">Título, descrição e ícone aparecem na aba do navegador e ao compartilhar.</p>'+formHtml(P.META_FIELDS,st.spec.meta,'meta')+'<h4 class="ps-sub">Estrutura</h4>'+formHtml(P.LAYOUT_FIELDS,st.spec.layout,'layout')}
  function presetHtml(){var cur=st.spec.theme.preset;return '<h4 class="ps-sub" style="margin-top:4px">Temas prontos</h4><div class="ps-presets">'+Object.keys(P.THEMES).map(function(k){var t=P.THEMES[k],p=t[t.mode];return '<button type="button" data-preset="'+k+'" class="'+(k===cur?'on':'')+'" style="--a:'+p.bg+';--b:'+p.primary+';--c:'+p.accent+';--d:'+p.text+'"><span class="sw"><i></i><i></i><i></i></span><b>'+esc(t.label)+'</b></button>'}).join('')+'</div>'}
  function formHtml(fields,obj,base){return fields.map(function(f){return fieldHtml(f,obj[f.key],base+'.'+f.key)}).join('')}
  function fieldHtml(f,v,path){
    var id='f'+path.replace(/\W/g,'_'),lab='<label for="'+id+'">'+esc(f.label)+(f.required?' <i>*</i>':'')+'</label>',t=f.type,p=' data-path="'+esc(path)+'" data-type="'+t+'"';
    if(t==='boolean')return '<div class="ps-f ps-f-bool"><label class="ps-switch" for="'+id+'"><input type="checkbox" id="'+id+'"'+p+(v?' checked':'')+'><span></span><em>'+esc(f.label)+'</em></label></div>';
    if(t==='select'){var opts=f.options,labs=f.labels||opts;
      if(opts.length<=4&&labs.every(function(l){return String(l).length<=12})&&!/Font|preset/i.test(f.key))return '<div class="ps-f">'+'<span class="ps-l">'+esc(f.label)+'</span><div class="ps-seg" role="radiogroup">'+opts.map(function(o,k){return '<button type="button" role="radio" aria-checked="'+(String(o)===String(v))+'" class="'+(String(o)===String(v)?'on':'')+'" data-seg="'+esc(path)+'" data-val="'+esc(o)+'">'+esc(labs[k]===''?'Padrão':labs[k])+'</button>'}).join('')+'</div></div>';
      return '<div class="ps-f">'+lab+'<select id="'+id+'"'+p+'>'+opts.map(function(o,k){return '<option value="'+esc(o)+'"'+(String(o)===String(v)?' selected':'')+'>'+esc(labs[k]===''||o===''?'Padrão do tema':labs[k])+'</option>'}).join('')+'</select></div>'}
    if(t==='number'){var min=f.min!=null?f.min:0,max=f.max!=null?f.max:100,step=f.step||1,val=v==null||v===''?(f.default!=null?f.default:''):v;
      return '<div class="ps-f">'+lab+'<div class="ps-range"><input type="range" min="'+min+'" max="'+max+'" step="'+step+'" value="'+esc(val===''?min:val)+'"'+p+'><input type="number" id="'+id+'" min="'+min+'" max="'+max+'" step="'+step+'" value="'+esc(val)+'" placeholder="auto"'+p+'></div></div>'}
    if(t==='color'){var th=P.THEMES[st.spec.theme.preset]||P.THEMES.aurora,md=st.spec.theme.mode&&st.spec.theme.mode!=='auto'?st.spec.theme.mode:th.mode,base=(th[md]||{})[f.key]||'#888888';return '<div class="ps-f">'+lab+'<div class="ps-color"><input type="color" value="'+esc(/^#[0-9a-f]{6}$/i.test(v)?v:(/^#[0-9a-f]{6}$/i.test(base)?base:'#888888'))+'"'+p+' aria-label="'+esc(f.label)+'"><input type="text" id="'+id+'" value="'+esc(v||'')+'" placeholder="do tema"'+p+'>'+(v?'<button type="button" class="ps-ib sm" data-clear="'+esc(path)+'" aria-label="Voltar ao tema">'+ic('close')+'</button>':'')+'</div></div>'}
    if(t==='list')return listHtml(f,v||[],path);
    if(t==='image')return '<div class="ps-f">'+lab+'<div class="ps-img">'+(v?'<img src="'+esc(P.safeUrl(v,true))+'" alt="">':'')+'<input type="text" id="'+id+'" value="'+esc(String(v||'').slice(0,300))+'" placeholder="https://… ou escolha"'+p+(String(v||'').length>300?' readonly':'')+'><button type="button" class="ps-btn ghost sm" data-pickimg="'+esc(path)+'">'+ic('upload')+'Escolher</button>'+(v?'<button type="button" class="ps-ib sm" data-clear="'+esc(path)+'" aria-label="Remover imagem">'+ic('close')+'</button>':'')+'</div></div>';
    if(t==='textarea'||t==='markdown'||t==='code')return '<div class="ps-f">'+lab+'<textarea id="'+id+'" rows="'+(t==='text'?2:3)+'"'+p+(t==='code'?' class="mono" spellcheck="false"':'')+'>'+esc(v||'')+'</textarea>'+(t==='markdown'?'<small class="ps-help">Markdown: **negrito**, *itálico*, [link](url), [[Nota]], listas e tabelas.</small>':'')+'</div>';
    var list=t==='note'?'ps-dl-notes':t==='folder'?'ps-dl-folders':t==='tag'?'ps-dl-tags':'';if(list)ensureLists();
    return '<div class="ps-f">'+lab+'<input type="text" id="'+id+'" value="'+esc(v==null?'':v)+'"'+p+(list?' list="'+list+'" autocomplete="off"':'')+(t==='url'?' inputmode="url" placeholder="https://… ou #secao"':'')+'></div>';
  }
  function listHtml(f,items,path){var lbl=f.itemLabel;
    return '<div class="ps-f ps-list"><span class="ps-l">'+esc(f.label)+' <small>'+items.length+'</small></span>'+items.map(function(it,k){var name=String(it[lbl]||'').replace(/\*\*/g,'')||('Item '+(k+1));
      return '<details class="ps-item"'+(items.length<=2?' open':'')+'><summary><span>'+esc(name)+'</span><span class="ps-item-acts"><button type="button" class="ps-ib sm" data-li="'+esc(path)+'" data-op="up" data-k="'+k+'" aria-label="Subir"'+(k?'':' disabled')+'>↑</button><button type="button" class="ps-ib sm" data-li="'+esc(path)+'" data-op="down" data-k="'+k+'" aria-label="Descer"'+(k<items.length-1?'':' disabled')+'>↓</button><button type="button" class="ps-ib sm" data-li="'+esc(path)+'" data-op="dup" data-k="'+k+'" aria-label="Duplicar">'+ic('copy')+'</button><button type="button" class="ps-ib sm danger" data-li="'+esc(path)+'" data-op="del" data-k="'+k+'" aria-label="Remover">'+ic('trash')+'</button></span></summary>'+
        '<div class="ps-item-body">'+f.fields.map(function(sf){return fieldHtml(sf,it[sf.key],path+'.'+k+'.'+sf.key)}).join('')+'</div></details>'}).join('')+
      (items.length<(f.max||60)?'<button type="button" class="ps-btn ghost sm" data-li="'+esc(path)+'" data-op="add">'+ic('plus')+'Adicionar</button>':'')+'</div>'}
  function listOp(path,op,k){var f=fieldAt(path);commit(function(s){var arr=pathGet(s,path);if(!Array.isArray(arr)){arr=[];pathSet(s,path,arr)}
    if(op==='add'){var item={};f.fields.forEach(function(sf){item[sf.key]=sf.default!=null?JSON.parse(JSON.stringify(sf.default)):(sf.type==='boolean'?false:'')});arr.push(item)}
    else if(op==='del')arr.splice(k,1);else if(op==='dup')arr.splice(k+1,0,JSON.parse(JSON.stringify(arr[k])));
    else if(op==='up'&&k>0)arr.splice(k-1,0,arr.splice(k,1)[0]);else if(op==='down'&&k<arr.length-1)arr.splice(k+1,0,arr.splice(k,1)[0])})
    if(op==='add'){var last=st.root.querySelectorAll('.ps-list [data-li="'+path+'"][data-op="add"]');last.forEach(function(b){var it=b.previousElementSibling;if(it&&it.tagName==='DETAILS'){it.open=true;var inp=it.querySelector('input,textarea');if(inp&&wide())inp.focus()}})}}
  function fieldAt(path){var ks=path.split('.');
    if(ks[0]==='theme')return P.THEME_FIELDS.find(function(f){return f.key===ks[1]});if(ks[0]==='meta')return P.META_FIELDS.find(function(f){return f.key===ks[1]});if(ks[0]==='layout')return P.LAYOUT_FIELDS.find(function(f){return f.key===ks[1]});
    var sec=st.spec.sections[+ks[1]];if(!sec)return null;var list=ks[2]==='style'?P.SECTION_FIELDS:P.BLOCKS[sec.type].fields,f=null;
    for(var i=3;i<ks.length;i++){if(/^\d+$/.test(ks[i]))continue;f=list.find(function(x){return x.key===ks[i]});if(!f)return null;if(f.type==='list')list=f.fields}
    return f}
  function coerceVal(f,raw){if(!f)return raw;if(f.type==='boolean')return !!raw;if(f.type==='number'){if(raw===''||raw==null)return f.default!=null?f.default:null;var n=Number(raw);return isFinite(n)?Math.max(f.min!=null?f.min:-1e9,Math.min(f.max!=null?f.max:1e9,n)):f.default}
    if(f.type==='select'){var o=f.options.find(function(x){return String(x)===String(raw)});return o===undefined?f.default:o}return raw}
  function onInput(e){var el=e.target,path=el.dataset&&el.dataset.path;if(!path||st.mode!=='edit')return;var f=fieldAt(path),v=el.type==='checkbox'?el.checked:el.value;
    if(el.tagName==='TEXTAREA')autosize(el);
    if(f&&f.type==='color'&&el.type==='text'&&v&&!/^(#[0-9a-f]{3,8}|(rgb|hsl)a?\(.+\))$/i.test(v))return;
    if(f&&f.type==='number'){var twin=el.parentNode.querySelector(el.type==='range'?'input[type=number]':'input[type=range]');if(twin&&twin!==el)twin.value=v}
    if(f&&f.type==='color'){var tw=el.parentNode.querySelector(el.type==='color'?'input[type=text]':'input[type=color]');if(tw&&/^#[0-9a-f]{6}$/i.test(v))tw.value=v}
    var structural=/\.(title|name|q|label|author|caption|path)$/.test(path);
    commit(function(s){pathSet(s,path,coerceVal(f,v))},{key:path,panels:false});
    if(structural)refreshLabels()}
  function onChange(e){var el=e.target,path=el.dataset&&el.dataset.path;if(!path||st.mode!=='edit')return;
    if(el.type==='checkbox'||el.tagName==='SELECT'){var f=fieldAt(path);st.lastKey='';commit(function(s){pathSet(s,path,coerceVal(f,el.type==='checkbox'?el.checked:el.value))})}}
  function refreshLabels(){st.root.querySelectorAll('.ps-sec').forEach(function(row){var s=section(row.dataset.id);if(!s)return;var t=secTitle(s);row.querySelector('small').textContent=t.title||'—'});
    st.root.querySelectorAll('.ps-item').forEach(function(d){var inp=d.querySelector('.ps-item-body [data-path]');if(!inp)return;var path=inp.dataset.path.replace(/\.[^.]+$/,''),listPath=path.replace(/\.\d+$/,''),f=fieldAt(listPath+'.0.x')&&null;var lf=fieldAt(listPath);if(!lf)return;var it=pathGet(st.spec,path);if(it)d.querySelector('summary span').textContent=String(it[lf.itemLabel]||'').replace(/\*\*/g,'')||'Item'})}
  function autosize(t){t.style.height='auto';t.style.height=Math.min(420,t.scrollHeight+2)+'px'}
  function ensureLists(){var host=st.root.querySelector('.ps-datalists');if(!host){host=doc.createElement('div');host.className='ps-datalists';host.hidden=true;st.root.appendChild(host)}
    var all=docs.list(),notes=all.filter(function(d){return /\.(md|markdown|txt)$/i.test(d.path)}),folders={},tags={};all.forEach(function(d){var p=d.path.split('/');p.pop();while(p.length){folders[p.join('/')]=1;p.pop()}(d.tags||[]).forEach(function(t){tags[t]=1})});
    host.innerHTML='<datalist id="ps-dl-notes">'+notes.slice(0,800).map(function(d){return '<option value="'+esc(d.path)+'">'+esc(d.title)+'</option>'}).join('')+'</datalist><datalist id="ps-dl-folders">'+Object.keys(folders).sort().map(function(f){return '<option value="'+esc(f)+'">'}).join('')+'</datalist><datalist id="ps-dl-tags">'+Object.keys(tags).sort().map(function(t){return '<option value="'+esc(t)+'">'}).join('')+'</datalist>'}

  /* imagens: reduz no próprio aparelho e guarda na página (funciona offline e no HTML exportado) */
  function pickImage(path){var inp=doc.createElement('input');inp.type='file';inp.accept='image/*';inp.onchange=function(){var file=inp.files&&inp.files[0];if(!file)return;
    shrink(file).then(function(url){if(url.length>2.5e6)toast('Imagem grande: a página ficará pesada.');commit(function(s){pathSet(s,path,url)})}).catch(function(e){D.alert({title:'Não foi possível usar a imagem',message:e.message||String(e)})})};inp.click()}
  function shrink(file){return new Promise(function(res,rej){if(/svg/.test(file.type)){var fr=new FileReader();fr.onload=function(){res(fr.result)};fr.onerror=rej;fr.readAsDataURL(file);return}
    var url=URL.createObjectURL(file),im=new Image();im.onload=function(){var max=1600,k=Math.min(1,max/Math.max(im.width,im.height)),c=doc.createElement('canvas');c.width=Math.round(im.width*k);c.height=Math.round(im.height*k);c.getContext('2d').drawImage(im,0,0,c.width,c.height);URL.revokeObjectURL(url);
      var png=/png|gif/.test(file.type)&&file.size<400e3;res(c.toDataURL(png?'image/png':'image/webp',.86))};im.onerror=function(){URL.revokeObjectURL(url);rej(new Error('Formato de imagem não suportado.'))};im.src=url})}

  /* ---------------- cliques ---------------- */
  function onClick(e){var t=e.target,b;
    if((b=t.closest('[data-a]'))){act(b.dataset.a,b);return}
    if((b=t.closest('[data-dev]'))){st.device=b.dataset.dev;layout();return}
    if((b=t.closest('[data-lt]'))){st.leftTab=b.dataset.lt;layout();renderPanels();return}
    if((b=t.closest('[data-t]'))){openSheet(b.dataset.t);return}
    if((b=t.closest('[data-selsec]'))){select(b.dataset.selsec,{from:'list'});if(!wide())openSheet('edit');return}
    if((b=t.closest('[data-hide]'))){var id=b.dataset.hide;commit(function(s){var x=s.sections[secIndex(id)];x.style.hidden=!x.style.hidden});return}
    if((b=t.closest('[data-secmenu]'))){sectionMenu(b.dataset.secmenu);return}
    if((b=t.closest('[data-dup]'))){duplicateSection(b.dataset.dup);return}
    if((b=t.closest('[data-del]'))){removeSection(b.dataset.del);return}
    if((b=t.closest('[data-seg]'))){var path=b.dataset.seg,f=fieldAt(path);st.lastKey='';commit(function(s){pathSet(s,path,coerceVal(f,b.dataset.val))});return}
    if((b=t.closest('[data-preset]'))){var k=b.dataset.preset;commit(function(s){s.theme.preset=k;['bg','surface','text','muted','primary','accent','background','headingFont','bodyFont','mode'].forEach(function(x){s.theme[x]=''});s.theme.radius=null});return}
    if((b=t.closest('[data-clear]'))){var cp=b.dataset.clear;commit(function(s){pathSet(s,cp,'')});return}
    if((b=t.closest('[data-pickimg]'))){pickImage(b.dataset.pickimg);return}
    if((b=t.closest('[data-li]'))){e.preventDefault();listOp(b.dataset.li,b.dataset.op,+b.dataset.k);return}
    if((b=t.closest('[data-open-page]'))){open(b.dataset.openPage);return}
    if((b=t.closest('[data-new]'))){newPage(b.dataset.new);return}
    if((b=t.closest('[data-page-menu]'))){e.stopPropagation();pageMenu(b.dataset.pageMenu);return}
  }
  function act(a){
    if(a==='back'){if(st.mode==='home'||!homeReturn)close();else home();return}
    if(a==='undo')return undo();if(a==='redo')return redo();
    if(a==='add')return addBlock();if(a==='json')return openJson();if(a==='play')return playNewTab();
    if(a==='export')return exportMenu();if(a==='more')return moreMenu();if(a==='ai')return askAI();
    if(a==='sheet-close')return closeSheet();if(a==='home-new')return newPage();if(a==='home-close')return close();
  }
  var homeReturn=true;

  /* ---------------- teclado ---------------- */
  function onKey(e){
    if(!st.root||st.root.hidden)return;if(doc.querySelector('.udlg'))return;
    var mod=e.ctrlKey||e.metaKey,k=e.key.toLowerCase(),inField=typing(e);
    if(st.mode==='home'){if(e.key==='Escape'){e.preventDefault();close()}else if(!inField&&k==='n'){e.preventDefault();newPage()}return}
    if(st.mode!=='edit')return;
    if(!st.root.querySelector('.ps-json').hidden){if(e.key==='Escape'){e.preventDefault();closeJson()}else if(mod&&e.key==='Enter'){e.preventDefault();applyJson()}return}
    if(st.root.querySelector('.ps-lib'))return;
    var hit=function(){e.preventDefault();e.stopPropagation()};
    if(mod&&k==='z'){hit();e.shiftKey?redo():undo();return}
    if(mod&&k==='y'){hit();redo();return}
    if(mod&&k==='s'){hit();save();toast('Página salva.');return}
    if(mod&&k==='e'){hit();exportMenu();return}
    if(mod&&k==='j'){hit();openJson();return}
    if(mod&&k==='p'){hit();playNewTab();return}
    if(mod&&k==='d'&&st.sel){hit();duplicateSection(st.sel);return}
    if(e.altKey&&(e.key==='ArrowUp'||e.key==='ArrowDown')&&st.sel){hit();moveSection(st.sel,e.key==='ArrowUp'?-1:1);return}
    if(e.key==='Escape'){hit();if(inField){e.target.blur();return}if(st.sheet){closeSheet();return}if(st.sel){st.sel=null;postFront({urbePage:'select',id:null});renderPanels();return}close();return}
    if(inField||mod||e.altKey)return;
    if(e.key==='/'||k==='a'){hit();addBlock();return}
    if((e.key==='Delete'||e.key==='Backspace')&&st.sel){hit();removeSection(st.sel);return}
    if(e.key==='ArrowDown'||k==='j'||e.key==='ArrowUp'||k==='k'){var list=st.spec.sections;if(!list.length)return;hit();var i=st.sel?secIndex(st.sel):-1,d=(e.key==='ArrowUp'||k==='k')?-1:1;i=i<0?(d>0?0:list.length-1):Math.max(0,Math.min(list.length-1,i+d));select(list[i].id,{from:'keys'});return}
    if(e.key==='Enter'&&st.sel){hit();var first=st.root.querySelector('[data-panel="right"] [data-path]');if(first)first.focus();return}
    if(k==='1'||k==='2'||k==='3'){hit();st.device=['mobile','tablet','desktop'][+k-1];layout();return}
    if(k==='t'){hit();st.leftTab='theme';openSheet('theme');return}
    if(e.key==='?'){hit();help();return}
  }
  function help(){var rows=[['/ ou A','Adicionar bloco'],['↑ ↓ ou J K','Selecionar seção'],['Enter','Editar a seção selecionada'],['Alt+↑ / Alt+↓','Mover seção'],[MOD+'+D','Duplicar seção'],['Delete','Remover seção'],[MOD+'+Z / '+MOD+'+Shift+Z','Desfazer / refazer'],[MOD+'+S','Salvar agora'],[MOD+'+J','Editar JSON'],[MOD+'+E','Exportar'],[MOD+'+P','Abrir em nova aba'],['1 2 3','Celular, tablet, computador'],['T','Tema'],['Esc','Sair do campo / fechar / voltar']];
    D.alert({title:'Atalhos do estúdio',message:rows.map(function(r){return r[0]+' — '+r[1]}).join('\n')+'\n\nNo celular: toque numa parte da prévia para editar, arraste ⋮⋮ para reordenar e puxe a alça da folha para expandir ou fechar.'})}

  /* ---------------- JSON ---------------- */
  function openJson(){var j=st.root.querySelector('.ps-json');j.hidden=false;
    j.innerHTML='<div class="ps-json-card"><header><strong>JSON da página</strong><span class="ps-json-state"></span><div><button type="button" class="ps-btn ghost sm" data-j="format">Formatar</button><button type="button" class="ps-btn ghost sm" data-j="schema">Referência</button><button type="button" class="ps-btn ghost sm" data-j="copy">'+ic('copy')+'Copiar</button><button type="button" class="ps-ib" data-j="close" aria-label="Fechar">'+ic('close')+'</button></div></header>'+
      '<textarea class="mono" spellcheck="false" autocapitalize="off" autocomplete="off"></textarea><ul class="ps-json-errs"></ul><footer><small>'+MOD+'+Enter aplica · Esc fecha. O Assistente também pode criar e editar este arquivo.</small><button type="button" class="ps-btn" data-j="apply">Aplicar</button></footer></div>';
    var ta=j.querySelector('textarea');ta.value=JSON.stringify(st.spec,null,2);validateJson();
    ta.addEventListener('input',function(){clearTimeout(ta._t);ta._t=setTimeout(validateJson,250)});
    ta.addEventListener('keydown',function(e){if(e.key==='Tab'){e.preventDefault();var s=ta.selectionStart;ta.setRangeText('  ',s,ta.selectionEnd,'end')}});
    j.onclick=function(e){var b=e.target.closest('[data-j]');if(!b)return;var w=b.dataset.j;
      if(w==='close')closeJson();else if(w==='apply')applyJson();else if(w==='format'){try{ta.value=JSON.stringify(JSON.parse(ta.value),null,2)}catch(_){}}
      else if(w==='copy'){copy(ta.value);toast('JSON copiado.')}else if(w==='schema'){D.alert({title:'Referência do formato',message:P.schemaText()})}};
    if(wide())ta.focus()}
  function validateJson(){var j=st.root.querySelector('.ps-json'),ta=j.querySelector('textarea'),n=P.normalize(ta.value),ul=j.querySelector('.ps-json-errs'),state=j.querySelector('.ps-json-state');
    ul.innerHTML=n.errors.concat(n.warnings.map(function(w){return Object.assign({warn:true},w)})).slice(0,30).map(function(x){return '<li class="'+(x.warn?'warn':'err')+'"><code>'+esc(x.path)+'</code> '+esc(x.message)+'</li>'}).join('');
    state.textContent=n.errors.length?n.errors.length+' erro(s)':n.warnings.length?'válido · '+n.warnings.length+' aviso(s)':'válido';state.className='ps-json-state '+(n.errors.length?'bad':'ok');return n}
  function applyJson(){var n=validateJson();if(n.errors.length){toast('Corrija os erros antes de aplicar.');return}commit(function(s){var keep=s;Object.keys(keep).forEach(function(k){delete keep[k]});Object.assign(keep,n.spec)});if(st.sel&&!section(st.sel))st.sel=null;closeJson();renderAll();toast('JSON aplicado.')}
  function closeJson(){var j=st.root&&st.root.querySelector('.ps-json');if(j){j.hidden=true;j.innerHTML=''}}
  function copy(text){if(global.navigator.clipboard)global.navigator.clipboard.writeText(text).catch(function(){});}

  /* ---------------- exportar e mais ---------------- */
  function fileBase(){var d=docs.get(st.docId);return(d?d.path.split('/').pop():'pagina').replace(/\.(page|template)\.json$/i,'')}
  function download(){var blob=new Blob([html(false)],{type:'text/html;charset=utf-8'}),a=doc.createElement('a');a.href=URL.createObjectURL(blob);a.download=fileBase()+'.html';doc.body.appendChild(a);a.click();a.remove();setTimeout(function(){URL.revokeObjectURL(a.href)},4000);toast('HTML baixado.')}
  function playNewTab(){var url=URL.createObjectURL(new Blob([html(false)],{type:'text/html'}));var w=global.open(url,'_blank');if(!w)toast('O navegador bloqueou a nova aba.');setTimeout(function(){URL.revokeObjectURL(url)},60000)}
  function saveHtmlToVault(){var d=docs.get(st.docId);if(!d)return;var p=d.path.replace(/\.page\.json$/i,'.html'),ex=docs.get(p);docs.upsert(Object.assign({},ex||{},{id:ex?ex.id:undefined,path:p,content:html(false)}),{source:'pages.export'});toast('Salvo como '+p.split('/').pop()+'.')}
  function exportMenu(){D.menu('Exportar',[
    {icon:'download',label:'Baixar HTML',detail:'Arquivo único, funciona em qualquer hospedagem',run:download},
    {icon:'open',label:'Abrir em nova aba',detail:MOD+'+P',run:playNewTab},
    {icon:'file',label:'Salvar HTML no vault',detail:'Ao lado desta página',run:saveHtmlToVault},
    {icon:'copy',label:'Copiar HTML',run:function(){copy(html(false));toast('HTML copiado.')}}])}
  function moreMenu(){D.menu('Página',[
    {icon:'layers',label:'Salvar como modelo',detail:'Reutilize em novas páginas',run:saveTemplate},
    {icon:'copy',label:'Duplicar página',run:duplicatePage},
    {icon:'code',label:'Editar JSON',detail:MOD+'+J',run:openJson},
    {icon:'open',label:'Abrir em nova aba',detail:MOD+'+P',run:playNewTab},
    {icon:'notes',label:'Todas as páginas',run:home},
    wide()?{icon:'select',label:'Atalhos de teclado',detail:'?',run:help}:null,
    {icon:'trash',label:'Mover página para a lixeira',danger:true,run:trashPage}].filter(Boolean))}
  async function saveTemplate(){var name=await D.prompt({title:'Salvar como modelo',label:'Nome do modelo',value:st.spec.meta.title,confirm:'Salvar modelo',validate:function(v){return cleanName(v)?'':'Use um nome válido.'}});name=cleanName(name);if(!name)return;
    var desc=await D.prompt({title:'Descrição (opcional)',label:'Para que serve este modelo?',value:'',confirm:'Salvar'});
    var spec=JSON.parse(JSON.stringify(st.spec));spec.kind='urbe-template';spec.template={name:name,description:desc||''};
    var p=uniquePath('Páginas/Modelos/'+name+'.template.json');docs.upsert({path:p,content:JSON.stringify(spec,null,2)+'\n'},{source:'pages.template'});toast('Modelo “'+name+'” salvo.')}
  async function duplicatePage(){flush();var d=docs.get(st.docId);if(!d)return;var p=uniquePath(d.path.replace(/(\.page\.json)$/i,' cópia$1')),spec=JSON.parse(JSON.stringify(st.spec));spec.meta.title+=' (cópia)';var nd=docs.upsert({path:p,content:JSON.stringify(spec,null,2)+'\n'},{source:'pages.duplicate'});open(nd.id);toast('Cópia criada.')}
  async function trashPage(){var d=docs.get(st.docId);if(!d)return;if(!(await D.confirm({title:'Mover para a lixeira?',message:'“'+st.spec.meta.title+'” poderá ser restaurada pela Lixeira.',confirm:'Mover para a lixeira',danger:true})))return;
    st.status='saved';clearTimeout(st.saveT);var tr=core.service('trash');if(tr)tr.trash(d.id,{source:'pages'});else docs.remove(d.id);home()}
  function askAI(){flush();var btn=doc.getElementById('openAI');if(btn&&btn.onclick){btn.onclick()}var panel=doc.getElementById('aiPanel');if(panel)panel.classList.add('open','ps-over');
    var d=docs.get(st.docId),ui=global.UrbeAgentUI;if(ui&&ui.prefill)ui.prefill('Na página '+(d?d.path:'aberta')+', ');}

  /* ---------------- galeria de páginas e modelos ---------------- */
  function home(){flush();ensure();st.mode='home';st.docId=null;st.root.hidden=false;st.root.classList.add('is-home');closeJson();var h=st.root.querySelector('.ps-home');h.hidden=false;renderHome();core.events.emit('pages:studio',{open:true,home:true})}
  function thumb(spec){var t=P.THEMES[spec.theme&&spec.theme.preset]||P.THEMES.aurora,m=(spec.theme&&spec.theme.mode)||t.mode,p=t[m==='auto'?t.mode:m]||t.dark;
    return 'style="--a:'+esc(spec.theme.bg||p.bg)+';--b:'+esc(spec.theme.primary||p.primary)+';--c:'+esc(spec.theme.accent||p.accent)+';--d:'+esc(spec.theme.text||p.text)+'"'}
  function renderHome(){var h=st.root.querySelector('.ps-home'),pages=pageDocs(),mine=templateDocs();
    h.innerHTML='<header class="ps-home-top"><button type="button" class="ps-ib" data-a="home-close" aria-label="Fechar">'+ic('back')+'</button><div><strong>Páginas</strong><small>Sites e páginas HTML feitos a partir do vault</small></div><button type="button" class="ps-btn" data-a="home-new">'+ic('plus')+'<span>Nova página</span></button></header>'+
      '<div class="ps-home-body">'+(pages.length?'<h3>Suas páginas <small>'+pages.length+'</small></h3><div class="ps-cards">'+pages.map(function(d){var raw=parse(d)||{},sp=P.normalize(raw).spec;return '<div class="ps-card"><button type="button" class="ps-card-open" data-open-page="'+esc(d.id)+'"><div class="ps-thumb" '+thumb(sp)+'><i></i><b>'+esc(sp.meta.icon||'✦')+'</b><span>'+esc(sp.meta.title)+'</span><em></em><em></em></div><strong>'+esc(sp.meta.title)+'</strong><small>'+esc(d.path)+' · '+sp.sections.length+' seções</small></button><button type="button" class="ps-ib sm" data-page-menu="'+esc(d.id)+'" aria-label="Opções">'+ic('more')+'</button></div>'}).join('')+'</div>':
        '<div class="ps-hero-empty"><div class="ps-hero-art">✦</div><strong>Crie sua primeira página</strong><p>Landing pages, portfólios, currículos, sites de uma pasta de notas… Tudo com visual moderno, prévia ao vivo e exportação em um único arquivo HTML.</p><button type="button" class="ps-btn" data-a="home-new">'+ic('plus')+'Nova página</button></div>')+
      '<h3>Começar de um modelo</h3><div class="ps-tpls">'+TPL.list().map(function(t){var sp=TPL.build(t.id,{title:t.name,folder:'',note:{title:'Nota',path:''}});return '<button type="button" class="ps-tpl" data-new="'+t.id+'"><div class="ps-thumb sm" '+thumb(sp)+'><i></i><b>'+esc(t.icon)+'</b><em></em><em></em></div><strong>'+esc(t.name)+'</strong><small>'+esc(t.description)+'</small></button>'}).join('')+
        mine.map(function(d){var raw=parse(d)||{},sp=P.normalize(raw).spec,tp=raw.template||{};return '<button type="button" class="ps-tpl mine" data-new="doc:'+esc(d.id)+'"><div class="ps-thumb sm" '+thumb(sp)+'><i></i><b>'+esc(sp.meta.icon||'★')+'</b><em></em><em></em></div><strong>'+esc(tp.name||sp.meta.title)+'</strong><small>'+esc(tp.description||'Seu modelo')+'</small></button>'}).join('')+'</div>'+
      '<p class="ps-foot-hint">Dica: peça ao Assistente “crie uma página sobre …” — ele monta o arquivo .page.json com os blocos certos.</p></div>'}
  function pageMenu(id){var d=docs.get(id);if(!d)return;D.menu(d.path.split('/').pop(),[{icon:'edit',label:'Abrir no estúdio',run:function(){open(id)}},{icon:'file',label:'Abrir como texto (JSON)',run:function(){close();core.commands.execute('document.open',{id:id,raw:true,source:'pages'})}},
    {icon:'trash',label:'Mover para a lixeira',danger:true,run:async function(){if(!(await D.confirm({title:'Mover para a lixeira?',message:d.path,confirm:'Mover',danger:true})))return;var tr=core.service('trash');if(tr)tr.trash(id,{source:'pages'});else docs.remove(id);renderHome()}}])}
  async function newPage(tplId){
    if(!tplId){var choice=await D.choose({title:'Nova página',options:TPL.list().map(function(t){return{value:t.id,label:t.icon+'  '+t.name,detail:t.description}}).concat(templateDocs().map(function(d){var r=parse(d)||{};return{value:'doc:'+d.id,label:'★  '+((r.template&&r.template.name)||d.path.split('/').pop()),detail:(r.template&&r.template.description)||'Seu modelo'}}))});if(!choice)return;tplId=choice}
    var ctx={},spec;
    if(tplId.indexOf('doc:')===0){var td=docs.get(tplId.slice(4));if(!td)return;spec=P.normalize(parse(td)||{}).spec;spec.kind='urbe-page';delete spec.template}
    else{var t=TPL.get(tplId);if(!t)return;
      if(t.needs==='note'){var notes=docs.list().filter(function(d){return /\.(md|markdown)$/i.test(d.path)}).sort(function(a,b){return String(b.modified||'').localeCompare(String(a.modified||''))});if(!notes.length)return D.alert({title:'Nenhuma nota',message:'Crie uma nota primeiro.'});
        var pick=await D.choose({title:'Qual nota publicar?',options:notes.slice(0,200).map(function(d){return{value:d.id,label:d.title,icon:'file',detail:d.path}})});if(!pick)return;var nd=docs.get(pick);ctx.note={title:nd.title,path:nd.path};ctx.title=nd.title;ctx.excerpt=P.excerpt(nd.content,150)}
      if(t.needs==='folder'){var fs={};docs.list().forEach(function(d){var p=d.path.split('/');p.pop();while(p.length){fs[p.join('/')]=1;p.pop()}});var list=Object.keys(fs).filter(function(f){return !/^Páginas(\/|$)/.test(f)}).sort();
        var fp=await D.choose({title:'Qual pasta?',options:[{value:'',label:'Vault inteiro',icon:'notes'}].concat(list.map(function(f){return{value:f,label:f.split('/').pop(),icon:'folder',detail:f}}))});if(fp===undefined||fp===null)return;ctx.folder=fp;ctx.title=fp?fp.split('/').pop():'Minhas notas'}
    }
    var t2=tplId.indexOf('doc:')===0?null:TPL.get(tplId);
    var name=await D.prompt({title:'Nome da página',label:'Nome',value:ctx.title||(spec&&spec.meta.title)||(t2&&t2.id!=='blank'?'':'Nova página'),placeholder:'Ex.: Meu portfólio',confirm:'Criar página',validate:function(v){return cleanName(v)?'':'Use um nome válido.'}});name=cleanName(name);if(!name)return;
    if(t2){var person=tplId==='portfolio'||tplId==='resume';if(!person)ctx.title=ctx.title||name;spec=TPL.build(tplId,ctx);spec.meta.title=name;if(spec.layout.brand&&!person)spec.layout.brand=name;if(tplId==='blank')spec.sections[0].props.title=name}
    else spec.meta.title=name;
    var p=uniquePath('Páginas/'+name+'.page.json'),d=docs.upsert({path:p,content:JSON.stringify(spec,null,2)+'\n'},{source:'pages.new'});
    var ex=core.service('explorer');if(ex&&ex.addFolder&&!ex.folders.has('Páginas'))try{ex.addFolder('Páginas')}catch(_){}
    open(d.id);toast('Página criada. Toque em qualquer parte para editar.');
  }

  /* ---------------- sincronização com o vault ---------------- */
  core.events.on('document:updated',function(e){if(st.mode!=='edit'||!e||!e.document)return;var d=e.document;
    if(d.id===st.docId){if(d.content===st.savedText||(e.meta&&e.meta.source==='pages.studio'))return;
      var raw;try{raw=JSON.parse(d.content)}catch(_){return}st.hist.push(snapshot());st.spec=P.normalize(raw).spec;st.savedText=d.content;if(st.sel&&!section(st.sel))st.sel=null;renderAll();toast('Página atualizada por fora (Assistente ou outro editor).',{label:'Desfazer',run:undo});return}
    if(st.spec&&st.spec.sections.some(function(s){return s.type==='note'||s.type==='notes'||/\[\[/.test(JSON.stringify(s.props))}))schedulePreview()});
  core.events.on('document:removed',function(e){if(st.mode==='edit'&&e&&e.document&&e.document.id===st.docId){toast('A página foi removida.');home()}});

  /* ---------------- registro ---------------- */
  var api={open:open,close:close,home:home,newPage:newPage,current:current,isOpen:function(){return !!st.root&&!st.root.hidden},render:function(id){var d=docs.get(id);return d?P.render(parse(d)||{},{documents:docs}):''},_state:st};
  core.provide('pages.studio',api);
  core.commands.register('pages.home',{title:'Páginas',category:'Páginas',execute:home});
  core.commands.register('pages.new',{title:'Nova página',category:'Páginas',execute:function(c){return newPage(c&&c.template)}});
  core.commands.register('pages.open',{title:'Abrir página no estúdio',category:'Páginas',execute:function(c){return open(c&&c.id)}});
  /* abrir um .page.json por qualquer caminho (explorador, busca, link, Assistente) leva ao estúdio */
  var hooked=false;
  function hookOpen(){var cmd=core.commands.get('document.open');if(!cmd||hooked)return !!cmd;var orig=cmd.execute;hooked=true;
    core.commands.unregister('document.open');core.commands.register('document.open',{title:cmd.title,category:cmd.category,execute:function(ctx){var d=docs.get(ctx&&(ctx.id||ctx.path));
      if(d&&P.isPagePath(d.path)&&!(ctx&&ctx.raw))return open(d.id);if(st.root&&!st.root.hidden)close();return orig(ctx)}});return true}
  if(!hookOpen())core.events.on('command:registered',function(c){if(c&&c.id==='document.open'&&!hooked)setTimeout(hookOpen,0)});
  global.UrbePageStudio=api;
})(window);
