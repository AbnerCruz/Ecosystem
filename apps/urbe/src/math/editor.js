(function(global){
  'use strict';
  /* Matemática no editor de notas.
     Visual: fórmulas são blocos atômicos (toque para editar num painel com
     prévia ao vivo, símbolos e modelos); digitar $…$ converte na hora.
     Texto: uma barra acompanha o cursor dentro de uma fórmula com a prévia,
     autocompletar de comandos (\fr → \frac{}{}) e símbolos — no celular ela
     fica logo acima do teclado. */
  var doc=global.document,M=global.UrbeMath;if(!doc||!M)return;
  var visual=doc.getElementById('renderedPreview'),source=doc.getElementById('bodyEditor'),scroll=doc.getElementById('editorScroll'),toolbar=doc.getElementById('mdToolbar');
  if(!visual||!source)return;
  var esc=M.esc,coarse=global.matchMedia&&global.matchMedia('(pointer:coarse)').matches;
  function isVisual(){return !!scroll&&scroll.classList.contains('previewMode')}
  function emitVisual(){visual.dispatchEvent(new Event('input',{bubbles:true}))}
  function emitSource(){source.dispatchEvent(new Event('input',{bubbles:true}))}
  function kbInset(){var vv=global.visualViewport;return vv?Math.max(0,global.innerHeight-vv.height-vv.offsetTop):0}

  /* ---------------- serialização (chamada pelo app) ---------------- */
  function serialize(base,root){
    var atoms=[].slice.call(root.querySelectorAll('.umath')),swaps=[];
    atoms.forEach(function(el){var raw=M.rawOf(el),rep;if(el.classList.contains('umath-block')){rep=doc.createElement('p');rep.textContent=raw}else rep=doc.createTextNode(raw);el.parentNode.replaceChild(rep,el);swaps.push([rep,el])});
    try{return base(root)}finally{swaps.forEach(function(p){p[0].parentNode&&p[0].parentNode.replaceChild(p[1],p[0])})}
  }

  /* ---------------- trechos de texto (textarea) ---------------- */
  function insertSnippet(ta,tex,replaceFrom){var sn=M.snippet(tex),a=replaceFrom!=null?replaceFrom:ta.selectionStart,b=ta.selectionEnd;
    ta.setRangeText(sn.text,a,b,'end');var pos=a+sn.caret;ta.setSelectionRange(pos,pos);ta.focus();ta.dispatchEvent(new Event('input',{bubbles:true}))}

  /* Tab pula para o próximo {} vazio (ou argumento de ^ _) dentro da fórmula; sem mais nenhum, sai dela */
  function jumpNext(ta,limit){var pos=ta.selectionEnd,v=ta.value,end=limit!=null?limit:v.length,k=v.indexOf('{}',pos);
    if(k>=0&&k<end){ta.setSelectionRange(k+1,k+1);return true}
    if(limit!=null&&pos<=end){ta.setSelectionRange(end,end);return true}return false}
  /* ---------------- UI compartilhada: símbolos e sugestões ---------------- */
  var tabs=M.SYMBOLS.map(function(g){return g.name}).concat(['Modelos']),tabIdx=0;
  function symbolsHtml(){var g=tabIdx<M.SYMBOLS.length?M.SYMBOLS[tabIdx].items.map(function(s){return '<button type="button" class="mth-sym" data-tex="'+esc(s.tex)+'" title="'+esc(M.snippet(s.tex).text)+'">'+esc(s.label)+'</button>'}).join('')
      :M.TEMPLATES.map(function(t){return '<button type="button" class="mth-tpl" data-tex="'+esc(t.tex)+'"><span>'+M.render('\\displaystyle '+M.snippet(t.tex).text.replace(/\{\}/g,'{\\square}'),false)+'</span><small>'+esc(t.label)+'</small></button>'}).join('');
    return '<div class="mth-tabs" role="tablist">'+tabs.map(function(t,i){return '<button type="button" data-tab="'+i+'" class="'+(i===tabIdx?'on':'')+'">'+esc(t)+'</button>'}).join('')+'</div><div class="mth-grid'+(tabIdx>=M.SYMBOLS.length?' tpl':'')+'">'+g+'</div>'}
  function suggestions(prefix){return M.complete(prefix,8)}
  function sugHtml(list,active){return list.map(function(c,i){return '<button type="button" class="mth-sug'+(i===active?' on':'')+'" data-i="'+i+'"><code>'+esc(c.cmd)+'</code><span class="mth-sug-prev">'+M.render(M.snippet(c.snip).text.replace(/\{\}/g,'{\\square}').replace(/\\to \}/,'\\to \\square}'),false)+'</span><small>'+esc(c.desc)+'</small></button>'}).join('')}
  /* autocompletar ligado a uma textarea: devolve {update(), key(e)} */
  function completer(ta,host,inMath){var st={list:[],i:0,from:0};
    function update(){var pos=ta.selectionStart,before=ta.value.slice(0,pos),m=before.match(/\\[A-Za-z]{1,30}$/);
      if(!m||ta.selectionEnd!==pos||!inMath()){st.list=[];host.innerHTML='';host.hidden=true;return}
      st.from=pos-m[0].length;st.list=suggestions(m[0]);st.i=0;host.hidden=!st.list.length;host.innerHTML=sugHtml(st.list,0)}
    function accept(i){var c=st.list[i];if(!c)return;insertSnippet(ta,c.snip,st.from);st.list=[];host.hidden=true;host.innerHTML=''}
    function key(e){if(!st.list.length||host.hidden)return false;
      if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();st.i=(st.i+(e.key==='ArrowDown'?1:-1)+st.list.length)%st.list.length;host.innerHTML=sugHtml(st.list,st.i);return true}
      if(e.key==='Enter'||e.key==='Tab'){e.preventDefault();accept(st.i);return true}
      if(e.key==='Escape'){e.preventDefault();st.list=[];host.hidden=true;return true}return false}
    host.addEventListener('pointerdown',function(e){e.preventDefault()});
    host.addEventListener('click',function(e){var b=e.target.closest('[data-i]');if(b)accept(+b.dataset.i)});
    return{update:update,key:key,close:function(){st.list=[];host.hidden=true}}}

  /* ---------------- painel de fórmula (inserir / editar) ---------------- */
  var pane=doc.createElement('div');pane.id='mathPane';pane.hidden=true;pane.setAttribute('role','dialog');pane.setAttribute('aria-modal','true');pane.setAttribute('aria-label','Editar fórmula');
  pane.innerHTML='<div class="mth-card"><header><strong data-title>Fórmula</strong><div class="mth-mode" role="radiogroup" aria-label="Tipo"><button type="button" data-mode="inline">No texto</button><button type="button" data-mode="block">Destaque</button></div><button type="button" class="mth-x" data-act="cancel" aria-label="Fechar">×</button></header>'+
    '<div class="mth-preview" aria-live="polite"></div><div class="mth-err" hidden></div>'+
    '<div class="mth-input"><textarea spellcheck="false" autocapitalize="off" autocomplete="off" autocorrect="off" rows="2" aria-label="LaTeX" placeholder="Digite LaTeX: \\frac{a}{b}, x^2, \\sqrt{2}…"></textarea><div class="mth-sugs" hidden></div></div>'+
    '<div class="mth-syms"></div><footer><button type="button" class="mth-del" data-act="remove">Remover</button><span></span><button type="button" data-act="cancel">Cancelar</button><button type="button" class="mth-ok" data-act="ok">Inserir</button></footer></div>';
  doc.body.appendChild(pane);
  var pta=pane.querySelector('textarea'),pst={target:null,range:null,block:false,orig:null};
  var pcomp=completer(pta,pane.querySelector('.mth-sugs'),function(){return true});
  function paneRender(){var tex=pta.value,err=tex.trim()?M.error(tex,pst.block):null;pane.querySelector('.mth-preview').innerHTML=tex.trim()?M.render(tex,pst.block):'<span class="mth-ph">A prévia aparece aqui</span>';
    var e=pane.querySelector('.mth-err');e.hidden=!err;e.textContent=err?'⚠ '+err:'';pane.querySelectorAll('[data-mode]').forEach(function(b){var on=(b.dataset.mode==='block')===pst.block;b.classList.toggle('on',on);b.setAttribute('aria-checked',on)})}
  function paneSyms(){pane.querySelector('.mth-syms').innerHTML=symbolsHtml()}
  function openPane(o){pst.target=o.target||null;pst.range=o.range||null;pst.block=!!o.block;pst.orig=o.tex||'';pta.value=o.tex||'';
    pane.querySelector('[data-title]').textContent=o.target?'Editar fórmula':'Nova fórmula';pane.querySelector('.mth-ok').textContent=o.target?'Salvar':'Inserir';pane.querySelector('.mth-del').hidden=!o.target;
    pane.hidden=false;paneSyms();paneRender();place();setTimeout(function(){pta.focus();var n=pta.value.length;pta.setSelectionRange(n,n)},30)}
  function closePane(){pane.hidden=true;pcomp.close();pst.target=null;pst.range=null}
  function place(){var card=pane.querySelector('.mth-card');card.style.marginBottom=coarse?kbInset()+'px':''}
  function commitPane(){var tex=pta.value.trim();if(!tex){if(pst.target)removeAtom(pst.target);closePane();return}
    if(pst.target)updateAtom(pst.target,tex,pst.block);else insertAtom(tex,pst.block,pst.range);closePane()}
  pane.addEventListener('pointerdown',function(e){if(e.target.closest('button'))e.preventDefault()});
  pane.addEventListener('click',function(e){var b;
    if(e.target===pane)return closePane();
    if((b=e.target.closest('[data-act]'))){var a=b.dataset.act;if(a==='cancel')closePane();else if(a==='ok')commitPane();else if(a==='remove'){if(pst.target)removeAtom(pst.target);closePane()}return}
    if((b=e.target.closest('[data-mode]'))){pst.block=b.dataset.mode==='block';paneRender();return}
    if((b=e.target.closest('[data-tab]'))){tabIdx=+b.dataset.tab;paneSyms();return}
    if((b=e.target.closest('[data-tex]'))){insertSnippet(pta,b.dataset.tex);return}});
  pta.addEventListener('input',function(){paneRender();pcomp.update()});
  pta.addEventListener('keyup',function(e){if(/Arrow|Home|End/.test(e.key))pcomp.update()});
  pta.addEventListener('keydown',function(e){if(pcomp.key(e))return;if(e.key==='Tab'&&!e.shiftKey){e.preventDefault();jumpNext(pta,null)||pta.setSelectionRange(pta.value.length,pta.value.length);return}if(e.key==='Enter'&&!e.shiftKey){e.preventDefault();commitPane()}else if(e.key==='Escape'){e.preventDefault();e.stopPropagation();closePane()}});
  if(global.visualViewport)global.visualViewport.addEventListener('resize',function(){if(!pane.hidden)place();if(!dock.hidden)placeDock()});

  /* ---------------- átomos no editor visual ---------------- */
  function atomEl(tex,block){var t=doc.createElement('div');t.innerHTML=M.atomHtml(tex,block);return t.firstChild}
  function caretAfter(node){var z=doc.createTextNode('\u200b');node.parentNode.insertBefore(z,node.nextSibling);var r=doc.createRange();r.setStart(z,1);r.collapse(true);var s=global.getSelection();s.removeAllRanges();s.addRange(r)}
  function blockOf(node){var el=node&&(node.nodeType===1?node:node.parentElement);while(el&&el.parentElement!==visual&&el!==visual)el=el.parentElement;return el&&el!==visual?el:null}
  function insertAtom(tex,block,range){visual.focus();var s=global.getSelection();if(range){s.removeAllRanges();s.addRange(range)}
    if(!s.rangeCount||!visual.contains(s.anchorNode)){var r0=doc.createRange();r0.selectNodeContents(visual);r0.collapse(false);s.removeAllRanges();s.addRange(r0)}
    var r=s.getRangeAt(0),el=atomEl(tex,block);r.deleteContents();
    if(block){var b=blockOf(r.startContainer),p=doc.createElement('p');p.appendChild(doc.createElement('br'));
      if(b&&!(b.textContent||'').replace(/\u200b/g,'').trim()&&!b.querySelector('.umath,img')){b.replaceWith(el)}else if(b){b.after(el)}else visual.appendChild(el);
      el.after(p);var rr=doc.createRange();rr.setStart(p,0);rr.collapse(true);s.removeAllRanges();s.addRange(rr)}
    else{var sc=r.startContainer,prevCh=sc.nodeType===3?sc.nodeValue.charAt(r.startOffset-1):'';r.insertNode(el);if(prevCh&&!/[\s\u200b(\[{]/.test(prevCh))el.parentNode.insertBefore(doc.createTextNode(' '),el);caretAfter(el)}
    emitVisual()}
  function updateAtom(el,tex,block){var wasBlock=el.classList.contains('umath-block');
    if(wasBlock!==!!block){var n=atomEl(tex,block);if(block){var b=blockOf(el);if(b&&b!==el){b.after(n);el.remove()}else el.replaceWith(n)}else{var p=doc.createElement('p');p.appendChild(n);el.replaceWith(p)}}
    else{el.setAttribute('data-tex',tex);el.innerHTML=M.render(tex,block||el.classList.contains('umath-display'))}
    emitVisual()}
  function removeAtom(el){var b=el.classList.contains('umath-block');if(b){var p=doc.createElement('p');p.appendChild(doc.createElement('br'));el.replaceWith(p)}else el.remove();emitVisual()}
  var lastRange=null;
  doc.addEventListener('selectionchange',function(){var s=global.getSelection();if(s&&s.rangeCount&&visual.contains(s.anchorNode))lastRange=s.getRangeAt(0).cloneRange()});
  visual.addEventListener('click',function(e){var a=e.target.closest&&e.target.closest('.umath');if(!a||!visual.contains(a))return;e.preventDefault();e.stopPropagation();
    openPane({target:a,tex:a.getAttribute('data-tex')||'',block:a.classList.contains('umath-block')})},true);
  /* digitar $…$ (ou $$…$$) no visual vira fórmula assim que fecha */
  var converting=false;
  visual.addEventListener('input',function(){if(converting||!isVisual())return;var s=global.getSelection();if(!s||!s.isCollapsed||!s.anchorNode||s.anchorNode.nodeType!==3)return;
    var node=s.anchorNode,pos=s.anchorOffset,before=node.nodeValue.slice(0,pos),m=before.match(/(^|[^\\$])\$\$([^$\n]+)\$\$$/)||before.match(/(^|[^\\$\w])\$([^\s$][^$\n]*?[^\s\\$]|[^\s$])\$$/);
    if(!m||/\d/.test(node.nodeValue.charAt(pos)))return;var display=m[0].indexOf('$$')>=0&&/\$\$$/.test(m[0]),tex=m[2],start=pos-m[0].length+m[1].length;if(M.error(tex,display))return;
    converting=true;try{var after=node.splitText(start);after.nodeValue=after.nodeValue.slice(pos-start);var el=atomEl(tex,false);if(display){el.classList.add('umath-display');el.setAttribute('data-open','$$');el.setAttribute('data-close','$$');el.innerHTML=M.render(tex,true)}
      node.parentNode.insertBefore(el,after);if(!after.nodeValue)after.nodeValue='\u200b';var r=doc.createRange();r.setStart(after,after.nodeValue==='\u200b'?1:0);r.collapse(true);s.removeAllRanges();s.addRange(r);emitVisual()}finally{converting=false}});

  /* ---------------- modo texto: barra de fórmula ---------------- */
  var dock=doc.createElement('div');dock.id='mathDock';dock.hidden=true;dock.setAttribute('aria-label','Fórmula');
  dock.innerHTML='<div class="mth-dock-top"><div class="mth-dock-prev" aria-live="polite"></div><button type="button" class="mth-dock-toggle" data-act="syms" aria-label="Símbolos">∑</button></div><div class="mth-sugs" hidden></div><div class="mth-syms" hidden></div>';
  doc.body.appendChild(dock);
  var dcomp=completer(source,dock.querySelector('.mth-sugs'),function(){return !!currentMath()}),symsOpen=false;
  function currentMath(){if(isVisual()||doc.activeElement!==source)return null;var i=source.selectionStart;if(i!==source.selectionEnd)return null;return M.findAt(source.value,i)}
  function updateDock(){var it=currentMath(),show=!!it||(symsOpen&&doc.activeElement===source)||false;
    if(!show){dock.hidden=true;dcomp.close();return}
    dock.hidden=false;var prev=dock.querySelector('.mth-dock-prev');
    if(it){var err=M.error(it.tex,it.display);prev.innerHTML=it.tex.trim()?(err?'<span class="mth-dock-err">⚠ '+esc(err)+'</span>':M.render(it.tex,false)):'<span class="mth-ph">Fórmula vazia</span>'}
    else prev.innerHTML='<span class="mth-ph">Toque num símbolo para inserir · <code>$…$</code> cria uma fórmula</span>';
    var sy=dock.querySelector('.mth-syms');sy.hidden=!(symsOpen||coarse&&!!it);if(!sy.hidden&&!sy.innerHTML)sy.innerHTML=symbolsHtml();
    dcomp.update();placeDock()}
  function placeDock(){var ed=doc.getElementById('editorFull');if(coarse){dock.style.bottom=kbInset()+'px';dock.style.left='0';dock.style.right='0';dock.style.width=''}
    else{var r=(scroll||ed||source).getBoundingClientRect();dock.style.left=Math.round(r.left+12)+'px';dock.style.width=Math.round(Math.min(r.width-24,760))+'px';dock.style.right='';dock.style.bottom=Math.max(12,global.innerHeight-r.bottom+12)+'px'}}
  ['input','keyup','click','focus'].forEach(function(ev){source.addEventListener(ev,function(e){if(ev==='keyup'&&/^(Arrow(Up|Down)|Enter|Tab|Escape)$/.test(e.key)&&!dock.querySelector('.mth-sugs').hidden)return;updateDock()})});
  source.addEventListener('blur',function(){setTimeout(function(){if(doc.activeElement!==source&&!dock.contains(doc.activeElement)){dock.hidden=true;dcomp.close()}},150)});
  source.addEventListener('keydown',function(e){if(!dock.hidden&&dcomp.key(e)){e.stopImmediatePropagation();return}
    if(e.key==='Tab'&&!e.shiftKey&&!e.ctrlKey&&!e.altKey){var it=currentMath();if(it){e.preventDefault();e.stopImmediatePropagation();jumpNext(source,it.end-it.close.length)||source.setSelectionRange(it.end,it.end);if(source.selectionStart===it.end-it.close.length){source.setSelectionRange(it.end,it.end)}updateDock()}}},true);
  dock.addEventListener('pointerdown',function(e){e.preventDefault()});
  dock.addEventListener('click',function(e){var b;
    if((b=e.target.closest('[data-act="syms"]'))){symsOpen=!symsOpen;var sy=dock.querySelector('.mth-syms');sy.hidden=!symsOpen;if(symsOpen)sy.innerHTML=symbolsHtml();placeDock();return}
    if((b=e.target.closest('[data-tab]'))){tabIdx=+b.dataset.tab;dock.querySelector('.mth-syms').innerHTML=symbolsHtml();return}
    if((b=e.target.closest('[data-tex]'))){var t=b.dataset.tex;if(!currentMath())t='$'+t+'$';insertSnippet(source,t);updateDock()}});
  global.addEventListener('resize',function(){if(!dock.hidden)placeDock()});

  /* ---------------- botão ∑ na barra e itens do menu "/" ---------------- */
  function startFormula(block){
    if(isVisual()){var s=global.getSelection(),sel=s&&s.rangeCount&&visual.contains(s.anchorNode)?s.toString():'';openPane({block:block,tex:sel.trim(),range:(s&&s.rangeCount&&visual.contains(s.anchorNode))?s.getRangeAt(0).cloneRange():lastRange});return}
    var a=source.selectionStart,b=source.selectionEnd,txt=source.value.slice(a,b);
    if(block){var pre=a>0&&source.value[a-1]!=='\n'?'\n':'';insertSnippet(source,pre+'$$\n'+(txt||'●')+'\n$$\n')}else insertSnippet(source,'$'+(txt||'●')+'$');updateDock()}
  if(toolbar&&!toolbar.querySelector('[data-special="math"]')){var sep=doc.createElement('span');sep.className='mdSep';sep.setAttribute('aria-hidden','true');
    var mb=doc.createElement('button');mb.className='mdBtn mdMath';mb.type='button';mb.dataset.special='math';mb.title='Fórmula (LaTeX) — Ctrl+M';mb.setAttribute('aria-label','Fórmula');mb.innerHTML='<span style="font:600 16px/1 Georgia,serif">∑</span>';
    mb.addEventListener('pointerdown',function(e){e.preventDefault()});mb.addEventListener('click',function(e){e.preventDefault();startFormula(false)});toolbar.appendChild(sep);toolbar.appendChild(mb)}
  doc.addEventListener('keydown',function(e){if((e.ctrlKey||e.metaKey)&&!e.altKey&&(e.key==='m'||e.key==='M')&&(doc.activeElement===source||visual.contains(doc.activeElement)||doc.activeElement===visual)){e.preventDefault();startFormula(e.shiftKey)}});
  var VT=global.UrbeVisualTools;if(VT&&VT.addItem){VT.addItem({label:'Fórmula',hint:'LaTeX no meio do texto',txt:'∑',keys:'formula latex matematica equacao math',run:function(){startFormula(false)}});
    VT.addItem({label:'Fórmula em destaque',hint:'Equação centralizada',txt:'∫',keys:'formula destaque bloco equacao latex math display',run:function(){startFormula(true)}})}

  var core=global.UrbeCore;
  if(core&&!core.commands.has('math.insert'))core.commands.register('math.insert',{title:'Inserir fórmula',category:'Matemática',execute:function(c){startFormula(!!(c&&c.block))}});

  global.UrbeMathEditor={serialize:serialize,open:openPane,insertAtom:insertAtom,start:startFormula,_dock:updateDock};
})(window);
