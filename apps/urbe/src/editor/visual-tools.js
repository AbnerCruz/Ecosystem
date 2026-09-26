(function(global){
  'use strict';
  /* Ferramentas do editor Visual:
     - bolha de formatação sobre o texto selecionado;
     - menu "/" no início de um bloco vazio para trocar o tipo do bloco.
     Ambas reaproveitam os botões da barra (#mdToolbar), que já sabem agir no
     modo Visual e sincronizar o Markdown; aqui só há apresentação e atalhos. */
  var doc=global.document,editor=doc.getElementById('renderedPreview'),toolbar=doc.getElementById('mdToolbar');
  if(!editor||!toolbar)return;
  function ic(n){return global.UrbeIcons?global.UrbeIcons.icon(n):''}
  function esc(v){return String(v).replace(/[&<>"]/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]})}
  var SELECTOR={h1:'[data-md="# "]',h2:'[data-md="## "]',h3:'[data-md="### "]',bold:'[data-wrap="**"]',italic:'[data-wrap="_"]',code:'[data-wrap="`"]',
    list:'[data-md="- "]',task:'[data-md="- [ ] "]',quote:'[data-md="> "]',noteLink:'[data-special="noteLink"]',link:'[data-special="link"]',rule:'[data-icon="rule"]'};
  function visual(){var s=doc.getElementById('editorScroll');return !!s&&s.classList.contains('previewMode')&&editor.isContentEditable}
  function run(action){var b=toolbar.querySelector(SELECTOR[action]);if(b)b.click()}
  function blockOf(node){var el=node&&(node.nodeType===1?node:node.parentElement);while(el&&el!==editor){if(/^(P|DIV|H[1-6]|LI|BLOCKQUOTE)$/.test(el.tagName))return el;el=el.parentElement}return null}
  function caretRect(){var s=global.getSelection();if(!s||!s.rangeCount)return null;var r=s.getRangeAt(0).cloneRange();r.collapse(false);var rect=r.getBoundingClientRect();if(rect&&(rect.width||rect.height||rect.top))return rect;var b=blockOf(s.anchorNode);return b?b.getBoundingClientRect():null}
  var coarse=global.matchMedia&&global.matchMedia('(pointer:coarse)').matches;

  /* ---------- bolha de seleção ---------- */
  var bubble=doc.createElement('div');bubble.id='urbeFormatBubble';bubble.setAttribute('role','toolbar');bubble.setAttribute('aria-label','Formatar seleção');bubble.hidden=true;
  bubble.innerHTML=[['bold','<b>B</b>','Negrito'],['italic','<i>I</i>','Itálico'],['code',ic('code'),'Código'],['sep'],['wiki',ic('wikilink'),'Transformar em link de nota'],['link',ic('link'),'Link externo'],['sep'],['h2','H','Título'],['quote',ic('quote'),'Citação']]
    .map(function(x){return x[0]==='sep'?'<span class="ufb-sep"></span>':'<button type="button" data-act="'+x[0]+'" title="'+x[2]+'" aria-label="'+x[2]+'">'+x[1]+'</button>'}).join('');
  doc.body.appendChild(bubble);
  bubble.addEventListener('pointerdown',function(e){e.preventDefault()});
  bubble.addEventListener('click',function(e){
    var b=e.target.closest('[data-act]');if(!b)return;var act=b.dataset.act;
    if(act==='wiki'){var s=global.getSelection(),t=s?s.toString().trim():'';if(t){doc.execCommand('insertHTML',false,'<span class="wikilink" data-note-name="'+esc(t)+'">'+esc(t)+'</span>&#8203;');editor.dispatchEvent(new Event('input',{bubbles:true}))}hideBubble();return}
    run(act);if(act==='h2'||act==='quote'||act==='link')hideBubble();else setTimeout(placeBubble,0);
  });
  function hideBubble(){bubble.hidden=true}
  function placeBubble(){
    var s=global.getSelection();
    if(!visual()||!s||s.isCollapsed||!s.rangeCount||!editor.contains(s.anchorNode)||!editor.contains(s.focusNode)||!s.toString().trim()){hideBubble();return}
    var r=s.getRangeAt(0).getBoundingClientRect();if(!r||(!r.width&&!r.height)){hideBubble();return}
    bubble.hidden=false;var w=bubble.offsetWidth,h=bubble.offsetHeight,vw=global.innerWidth;
    var top=coarse?r.bottom+12:r.top-h-10;if(top<64)top=r.bottom+12;
    var left=Math.max(8,Math.min(vw-w-8,r.left+r.width/2-w/2));
    bubble.style.top=Math.round(top)+'px';bubble.style.left=Math.round(left)+'px';
  }
  var raf=0;doc.addEventListener('selectionchange',function(){cancelAnimationFrame(raf);raf=requestAnimationFrame(placeBubble)});
  var scroller=doc.getElementById('editorScroll');if(scroller)scroller.addEventListener('scroll',function(){if(!bubble.hidden)placeBubble()},{passive:true});
  global.addEventListener('resize',function(){if(!bubble.hidden)placeBubble()});

  /* ---------- menu "/" ---------- */
  var ITEMS=[
    {act:'h1',label:'Título 1',hint:'Título grande',icon:null,txt:'H1',keys:'titulo heading h1 grande'},
    {act:'h2',label:'Título 2',hint:'Seção',txt:'H2',keys:'titulo heading h2 secao'},
    {act:'h3',label:'Título 3',hint:'Subseção',txt:'H3',keys:'titulo heading h3 subsecao'},
    {act:'list',label:'Lista',hint:'Itens com marcador',icon:'list',keys:'lista bullet itens'},
    {act:'task',label:'Tarefa',hint:'Caixa de seleção',icon:'task',keys:'tarefa todo checkbox check'},
    {act:'quote',label:'Citação',hint:'Destaque de trecho',icon:'quote',keys:'citacao quote'},
    {act:'rule',label:'Divisória',hint:'Linha horizontal',icon:'rule',keys:'divisoria linha separador hr'},
    {act:'noteLink',label:'Link para nota',hint:'Cria uma rua na cidade',icon:'wikilink',keys:'link nota wiki rua'},
    {act:'link',label:'Link externo',hint:'Endereço da web',icon:'link',keys:'link url web externo'}
  ];
  var menu=doc.createElement('div');menu.id='urbeSlashMenu';menu.setAttribute('role','listbox');menu.setAttribute('aria-label','Inserir bloco');menu.hidden=true;doc.body.appendChild(menu);
  var state={open:false,block:null,items:[],index:0};
  function fold(s){return String(s).normalize('NFD').replace(/[̀-ͯ]/g,'').toLowerCase()}
  function slashQuery(){
    var s=global.getSelection();if(!visual()||!s||!s.isCollapsed||!s.rangeCount)return null;
    var b=blockOf(s.anchorNode);if(!b)return null;
    var t=(b.textContent||'').replace(/​/g,'');var m=t.match(/^\/([^\s\/]{0,24})$/);return m?{block:b,q:m[1]}:null;
  }
  function renderMenu(){
    menu.innerHTML=state.items.length?state.items.map(function(x,i){return '<button type="button" role="option" data-i="'+i+'" class="usm-item'+(i===state.index?' active':'')+'" aria-selected="'+(i===state.index)+'"><span class="usm-icon">'+(x.icon?ic(x.icon):'<b>'+x.txt+'</b>')+'</span><span class="usm-text"><strong>'+x.label+'</strong><small>'+x.hint+'</small></span></button>'}).join(''):'<div class="usm-empty">Nenhum bloco com esse nome</div>';
    var a=menu.querySelector('.usm-item.active');if(a)a.scrollIntoView({block:'nearest'});
  }
  function openMenu(info){
    var q=fold(info.q);state.block=info.block;state.items=ITEMS.filter(function(x){return !q||fold(x.label+' '+x.keys).includes(q)});state.index=Math.min(state.index,Math.max(0,state.items.length-1));
    state.open=true;menu.hidden=false;renderMenu();
    var r=caretRect()||info.block.getBoundingClientRect(),h=menu.offsetHeight,w=menu.offsetWidth,vh=global.innerHeight,vw=global.innerWidth;
    var top=r.bottom+8;if(top+h>vh-12)top=Math.max(12,r.top-h-8);
    menu.style.top=Math.round(top)+'px';menu.style.left=Math.round(Math.max(8,Math.min(vw-w-8,r.left)))+'px';
  }
  function closeMenu(){state.open=false;state.index=0;menu.hidden=true}
  function choose(i){
    var x=state.items[i],b=state.block;closeMenu();if(!x||!b)return;
    if(x.act==='rule'){
      var hr=doc.createElement('hr'),next=doc.createElement('p');next.appendChild(doc.createElement('br'));
      if(b.tagName==='LI'){var list=b.parentElement;b.remove();list.after(hr,next);if(!list.querySelector('li'))list.remove()}else b.replaceWith(hr,next);
      var rr=doc.createRange();rr.setStart(next,0);rr.collapse(true);var ss=global.getSelection();ss.removeAllRanges();ss.addRange(rr);
      editor.dispatchEvent(new Event('input',{bubbles:true}));return;
    }
    b.textContent='';b.appendChild(doc.createElement('br'));
    var r=doc.createRange();r.setStart(b,0);r.collapse(true);var s=global.getSelection();s.removeAllRanges();s.addRange(r);
    run(x.act);
  }
  menu.addEventListener('pointerdown',function(e){e.preventDefault()});
  menu.addEventListener('click',function(e){var b=e.target.closest('[data-i]');if(b)choose(+b.dataset.i)});
  editor.addEventListener('input',function(){var info=slashQuery();if(info)openMenu(info);else if(state.open)closeMenu()});
  editor.addEventListener('keydown',function(e){
    if(!state.open)return;
    if(e.key==='ArrowDown'){e.preventDefault();e.stopImmediatePropagation();state.index=(state.index+1)%Math.max(1,state.items.length);renderMenu()}
    else if(e.key==='ArrowUp'){e.preventDefault();e.stopImmediatePropagation();state.index=(state.index-1+state.items.length)%Math.max(1,state.items.length);renderMenu()}
    else if(e.key==='Enter'||e.key==='Tab'){if(state.items.length){e.preventDefault();e.stopImmediatePropagation();choose(state.index)}}
    else if(e.key==='Escape'){e.preventDefault();e.stopImmediatePropagation();closeMenu()}
  },true);
  editor.addEventListener('blur',function(){setTimeout(function(){if(doc.activeElement!==editor)closeMenu()},120)});
  doc.addEventListener('selectionchange',function(){if(state.open&&!slashQuery())closeMenu()});

  global.UrbeVisualTools={run:run,isSlashOpen:function(){return state.open}};
})(window);
