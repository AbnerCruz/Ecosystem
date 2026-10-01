(function(global){
  'use strict';
  /* Diagnóstico de toques (Configurações → Diagnóstico de toques).
     Para cada toque registra onde o dedo caiu, qual elemento estava ali, se
     algum código cancelou o toque e para onde o clique foi. Na tela, um ponto
     verde = o clique chegou ao controle tocado; vermelho = não chegou.
     “Copiar relatório” gera um texto para mandar a quem está corrigindo. */
  var doc=global.document,KEY='urbe.touchDebug';if(!doc)return;
  var on=false,log=[],cur=null,panel=null;
  function desc(el){if(!el||el.nodeType!==1)return String(el&&el.nodeName||'—');var s=el.tagName.toLowerCase()+(el.id?'#'+el.id:'')+(typeof el.className==='string'&&el.className.trim()?'.'+el.className.trim().split(/\s+/).slice(0,2).join('.'):'');
    var lbl=(el.getAttribute('aria-label')||el.getAttribute('title')||el.textContent||'').trim().replace(/\s+/g,' ').slice(0,24);return s+(lbl?' “'+lbl+'”':'')}
  function control(el){return el&&el.closest?el.closest('button,a[href],[role=button],summary,input,select,textarea,label,[data-nav],.umath,.ume-row,[onclick]'):null}
  function dot(x,y,okc){var d=doc.createElement('div');d.className='utd-dot '+(okc?'ok':'bad');d.style.left=x+'px';d.style.top=y+'px';doc.body.appendChild(d);setTimeout(function(){d.remove()},okc?900:4000)}
  function start(e){if(!on||e.isPrimary===false)return;if(panel&&panel.contains(e.target))return;var t=e.target,c=control(t);
    cur={t0:performance.now(),x:Math.round(e.clientX),y:Math.round(e.clientY),type:e.pointerType,target:desc(t),control:c?desc(c):'(nenhum controle)',el:c,prevented:[],moved:0,click:null,cancel:false}}
  function move(e){if(cur&&e.isPrimary!==false)cur.moved=Math.max(cur.moved,Math.round(Math.hypot(e.clientX-cur.x,e.clientY-cur.y)))}
  function late(ev){return function(e){if(cur&&e.defaultPrevented&&cur.prevented.indexOf(ev)<0)cur.prevented.push(ev)}}
  function cancel(){if(cur)cur.cancel=true}
  function up(){if(!cur)return;var c=cur;c.dur=Math.round(performance.now()-c.t0);setTimeout(function(){finish(c)},450)}
  function click(e){if(!on||!cur)return;if(panel&&panel.contains(e.target))return;cur.click={target:desc(e.target),same:!!(cur.el&&(cur.el===e.target||cur.el.contains(e.target))),dt:Math.round(performance.now()-cur.t0)}}
  function finish(c){if(!on)return;if(cur===c)cur=null;var okc=!c.el||(c.click&&c.click.same);
    var line=(okc?'✓ ':'✗ ')+c.control+' · '+c.dur+'ms'+(c.moved?' · deslizou '+c.moved+'px':'')+(c.cancel?' · CANCELADO pelo navegador':'')+(c.prevented.length?' · bloqueado em '+c.prevented.join(','):'')+(c.click?(c.click.same?'':' · clique foi para '+c.click.target):(c.el?' · SEM CLIQUE':''));
    log.push({time:new Date().toISOString().slice(11,19),line:line,x:c.x,y:c.y,target:c.target});if(log.length>60)log.shift();dot(c.x,c.y,okc);render()}
  function render(){if(!panel)return;panel.querySelector('.utd-log').innerHTML=log.slice(-6).reverse().map(function(l){return '<div class="'+(l.line[0]==='✓'?'ok':'bad')+'">'+l.line.replace(/[&<>]/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;'}[c]})+'</div>'}).join('')||'<div>Toque nos botões que não funcionam.</div>'}
  function report(){var n=global.navigator,v=global.visualViewport,rt=global.UrbeCore&&global.UrbeCore.service('legacy.runtime');
    return ['Relatório de toques — Urbe '+(rt&&rt.version?rt.version():''),'Aparelho: '+n.userAgent,'Tela: '+global.innerWidth+'×'+global.innerHeight+' @'+global.devicePixelRatio+'x'+(v?' · viewport '+Math.round(v.width)+'×'+Math.round(v.height):'')+' · toque máx '+(n.maxTouchPoints||0)+' · standalone '+(global.matchMedia&&global.matchMedia('(display-mode: standalone)').matches),
      '',log.map(function(l){return l.time+'  '+l.line+'  @'+l.x+','+l.y+(l.target&&l.line.indexOf(l.target)<0?'  [alvo: '+l.target+']':'')}).join('\n')||'(nenhum toque registrado)'].join('\n')}
  function copy(){var t=report();(global.navigator.clipboard?global.navigator.clipboard.writeText(t):Promise.reject()).then(function(){flash('Relatório copiado. Cole na conversa.')},function(){var ta=doc.createElement('textarea');ta.value=t;doc.body.appendChild(ta);ta.select();try{doc.execCommand('copy');flash('Relatório copiado.')}catch(_){flash('Não deu para copiar.')}ta.remove()})}
  function flash(m){if(!panel)return;var f=panel.querySelector('.utd-flash');f.textContent=m;setTimeout(function(){f.textContent=''},2500)}
  function ensurePanel(){if(panel)return;panel=doc.createElement('div');panel.id='urbeTouchDebug';panel.innerHTML='<div class="utd-head"><strong>Diagnóstico de toques</strong><span class="utd-flash"></span><button type="button" data-a="copy">Copiar relatório</button><button type="button" data-a="min" aria-label="Minimizar">–</button><button type="button" data-a="off" aria-label="Desligar">×</button></div><div class="utd-log"></div>';
    doc.body.appendChild(panel);panel.addEventListener('click',function(e){var b=e.target.closest('[data-a]');if(!b)return;if(b.dataset.a==='copy')copy();else if(b.dataset.a==='off')set(false);else panel.classList.toggle('min')});render()}
  function set(v){on=!!v;try{v?localStorage.setItem(KEY,'1'):localStorage.removeItem(KEY)}catch(_){}
    if(on){ensurePanel();panel.hidden=false}else if(panel)panel.hidden=true}
  /* captura (antes de tudo) e bolha em window (depois de tudo, para ver quem cancelou) */
  doc.addEventListener('pointerdown',start,true);doc.addEventListener('pointermove',move,true);doc.addEventListener('pointerup',up,true);doc.addEventListener('pointercancel',cancel,true);doc.addEventListener('click',click,true);
  global.addEventListener('pointerdown',late('pointerdown'));global.addEventListener('touchstart',late('touchstart'),{passive:true});global.addEventListener('touchend',late('touchend'),{passive:true});global.addEventListener('pointerup',late('pointerup'));
  var st=doc.createElement('style');st.textContent='#urbeTouchDebug{position:fixed;left:8px;right:8px;top:calc(8px + env(safe-area-inset-top));z-index:2147483000;background:rgba(12,13,16,.94);color:#e8e8ee;border:1px solid #3a3d46;border-radius:12px;font:12px/1.35 ui-monospace,Menlo,monospace;box-shadow:0 10px 30px rgba(0,0,0,.5);pointer-events:auto}#urbeTouchDebug[hidden]{display:none}'+
    '.utd-head{display:flex;align-items:center;gap:6px;padding:6px 6px 6px 10px;border-bottom:1px solid #2a2c33}.utd-head strong{flex:1;font:600 12.5px system-ui,sans-serif}.utd-flash{color:#7fdca6;font:12px system-ui}.utd-head button{min-height:32px;padding:0 10px;border-radius:8px;border:1px solid #3a3d46;background:#1e2026;color:#fff;font:600 12px system-ui}'+
    '.utd-log{padding:6px 10px;max-height:28vh;overflow:auto}.utd-log div{padding:2px 0;word-break:break-word}.utd-log .ok{color:#9fe0b9}.utd-log .bad{color:#ff8e98}#urbeTouchDebug.min .utd-log{display:none}'+
    '.utd-dot{position:fixed;width:26px;height:26px;margin:-13px 0 0 -13px;border-radius:50%;z-index:2147482999;pointer-events:none;border:3px solid}.utd-dot.ok{border-color:#3ddc84;background:rgba(61,220,132,.25)}.utd-dot.bad{border-color:#ff4d5e;background:rgba(255,77,94,.3)}';
  doc.head.appendChild(st);
  try{if(localStorage.getItem(KEY)==='1')setTimeout(function(){set(true)},0)}catch(_){}
  global.UrbeTouchDebug={set:set,isOn:function(){return on},report:report};
})(window);
