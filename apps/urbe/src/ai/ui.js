(function(global){
  'use strict';
  /* Interface do Assistente: conversa com agentes que usam ferramentas.
     Mostra o que o agente está fazendo (cada ferramenta vira um cartão), pede
     aprovação com prévia das mudanças, permite parar, desfazer e trocar de
     agente, provedor ou modelo a qualquer momento. */
  var doc=global.document,P=global.UrbeAIProviders,T=global.UrbeAITools,A=global.UrbeAgent,S=global.UrbeAIStore,D=global.UrbeDialogs;
  if(!P||!T||!A||!S)return;
  var core=global.UrbeCore,docs=core&&core.service('documents');
  function ic(n,c){return global.UrbeIcons?global.UrbeIcons.icon(n,c):''}
  function esc(v){return String(v==null?'':v).replace(/[&<>"']/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]})}
  var coarse=global.matchMedia&&global.matchMedia('(pointer:coarse)').matches;

  var hooks={vaultName:function(){return'Urbe'},editor:function(){return null},openNote:null};
  var st={root:null,cfg:null,conv:null,running:null,pending:null,view:'chat',dirty:new Set(),raf:0,lastSel:null,useOpen:true,ready:null};

  /* ---------- seleção do editor: guardada antes de o foco ir para o painel ---------- */
  doc.addEventListener('selectionchange',function(){
    var ae=doc.activeElement,s=global.getSelection(),text='';
    if(ae&&ae.id==='bodyEditor'&&ae.selectionEnd>ae.selectionStart)text=ae.value.slice(ae.selectionStart,ae.selectionEnd);
    else if(s&&s.rangeCount&&!s.isCollapsed){var pv=doc.getElementById('renderedPreview');if(pv&&pv.contains(s.anchorNode))text=s.toString()}
    else if(ae&&(ae.id==='bodyEditor'||ae.id==='renderedPreview'))text='';
    else return;
    var e=hooks.editor();st.lastSel=text?{path:e&&e.path,text:text,at:Date.now()}:null;
  });
  function editorInfo(){var e=hooks.editor();if(!e)return null;var sel=st.lastSel&&st.lastSel.path===e.path&&Date.now()-st.lastSel.at<10*60000?st.lastSel.text:'';return{path:e.path,id:e.id,selection:sel}}

  /* ---------- configuração ---------- */
  function provider(){var c=st.cfg;return c.providers.find(function(p){return p.id===(st.conv&&st.conv.providerId||c.providerId)})||c.providers[0]||null}
  function modelId(){return st.conv&&st.conv.model||st.cfg.model||''}
  function saveCfg(){return S.setConfig(st.cfg)}
  function memoryList(){var m=st.cfg.memory||{},v=hooks.vaultName()||'Urbe';return(m['*']||[]).concat(m[v]||[])}
  function remember(f){var v=hooks.vaultName()||'Urbe',m=st.cfg.memory||(st.cfg.memory={});(m[v]=m[v]||[]).push(f);saveCfg()}
  function modelMeta(){var p=provider(),cache=p&&st.cfg.modelCache[p.id];return cache&&cache.list&&cache.list.find(function(m){return m.id===modelId()})||null}

  /* ---------- conversa ---------- */
  function newConversation(){return{id:A.uid('c'),title:'',created:Date.now(),updated:Date.now(),agentId:st.cfg.agentId||'geral',providerId:st.cfg.providerId,model:st.cfg.model,policy:st.cfg.policy||'ask',vault:hooks.vaultName(),messages:[],runs:[],usage:{input:0,output:0,cost:null}}}
  function persist(){if(st.conv&&st.conv.messages.length)return S.saveConversation(st.conv)}

  /* ================= montagem ================= */
  function mount(el){
    st.root=el;el.classList.add('ag-host');
    el.innerHTML='<div class="ag">'+
      '<header class="ag-head"><button type="button" class="ag-agent" data-agent aria-label="Trocar de agente"></button><span class="ag-spacer"></span>'+
      '<button type="button" class="ag-ib" data-history title="Conversas" aria-label="Conversas">'+ic('clock')+'</button>'+
      '<button type="button" class="ag-ib" data-new title="Nova conversa" aria-label="Nova conversa">'+ic('plus')+'</button>'+
      '<button type="button" class="ag-ib" data-settings title="Configurações do Assistente" aria-label="Configurações do Assistente">'+ic('settings')+'</button>'+
      '<button type="button" class="ag-ib" data-close title="Fechar" aria-label="Fechar">'+ic('close')+'</button></header>'+
      '<div class="ag-bar"><button type="button" class="ag-chip" data-model></button><button type="button" class="ag-chip" data-policy></button><span class="ag-usage" data-usage></span></div>'+
      '<div class="ag-log" role="log" aria-live="polite" aria-relevant="additions"></div>'+
      '<form class="ag-compose"><div class="ag-ctx" data-ctx hidden></div><div class="ag-row"><textarea rows="1" placeholder="Peça algo ao Assistente…" aria-label="Mensagem para o Assistente"></textarea>'+
      '<button type="submit" class="ag-send" data-send aria-label="Enviar">'+ic('arrowUp')+'</button></div></form>'+
      '<section class="ag-view" data-view-panel hidden></section></div>';
    var q=function(s){return el.querySelector(s)};
    q('[data-close]').onclick=function(){el.classList.remove('open')};
    q('[data-new]').onclick=function(){if(st.running)return;st.conv=newConversation();renderAll()};
    q('[data-history]').onclick=showHistory;q('[data-settings]').onclick=function(){showSettings()};
    q('[data-agent]').onclick=pickAgent;q('[data-model]').onclick=pickModel;q('[data-policy]').onclick=pickPolicy;
    var ta=q('textarea');
    ta.addEventListener('input',autosize);
    ta.addEventListener('keydown',function(e){if(e.key==='Enter'&&!e.shiftKey&&!coarse&&!e.isComposing){e.preventDefault();submit()}});
    q('form').addEventListener('submit',function(e){e.preventDefault();if(st.running)stop();else submit()});
    q('.ag-log').addEventListener('click',onLogClick);
    st.ready=S.getConfig().then(function(c){st.cfg=c;return S.listConversations()}).then(function(list){
      var v=hooks.vaultName(),last=(list||[]).find(function(x){return x.vault===v});
      st.conv=last&&Date.now()-last.updated<6*3600e3?last:newConversation();renderAll();
    }).catch(function(e){console.warn('assistente',e);st.cfg=JSON.parse(JSON.stringify(S.DEFAULT_CONFIG));st.conv=newConversation();renderAll()});
  }
  function autosize(){var ta=st.root.querySelector('textarea');ta.style.height='auto';ta.style.height=Math.min(180,ta.scrollHeight)+'px'}

  /* ================= renderização ================= */
  function renderChrome(){
    if(!st.cfg||!st.conv)return;var r=st.root,a=A.agentById(st.conv.agentId,st.cfg.customAgents),p=provider();
    r.querySelector('[data-agent]').innerHTML=ic(a.icon||'sparkle')+'<span>'+esc(a.name)+'</span>'+ic('chevron','ag-chev');
    r.querySelector('[data-model]').innerHTML=p?'<span class="ag-dot"></span>'+esc(shortModel(modelId())||'Escolher modelo'):'Conectar um modelo';
    r.querySelector('[data-model]').title=p?(p.name+' · '+modelId()):'';
    r.querySelector('[data-policy]').innerHTML=esc({ask:'Pede aprovação',auto:'Edita sozinho',readonly:'Só leitura'}[st.conv.policy]||'Pede aprovação');
    var u=st.conv.usage||{},cost=u.cost!=null?u.cost:estimate(u);r.querySelector('[data-usage]').textContent=u.input||u.output?fmtTokens(u.input+u.output)+(cost?' · US$ '+cost.toFixed(cost<.01?4:2):''):'';
    var send=r.querySelector('[data-send]');send.innerHTML=st.running?ic('close'):ic('arrowUp');send.classList.toggle('stop',!!st.running);send.setAttribute('aria-label',st.running?'Parar':'Enviar');
    var ctx=r.querySelector('[data-ctx]'),e=editorInfo();
    if(e&&e.path){ctx.hidden=false;ctx.innerHTML='<button type="button" class="ag-ctxchip'+(st.useOpen?' on':'')+'" data-toggle-open title="Incluir a nota aberta no pedido">'+ic('file')+'<span>'+esc(e.path.split('/').pop())+(e.selection?' · seleção':'')+'</span></button>';
      ctx.querySelector('[data-toggle-open]').onclick=function(){st.useOpen=!st.useOpen;renderChrome()}}else ctx.hidden=true;
  }
  function shortModel(m){return String(m||'').split('/').pop()}
  function fmtTokens(n){return n>=1000?(n/1000).toFixed(n>=1e5?0:1)+'k tokens':n+' tokens'}
  function estimate(u){var m=modelMeta();if(!m||!m.price)return null;return(u.input*m.price.input+u.output*m.price.output)/1e6}

  function renderAll(){
    if(!st.conv||!st.cfg)return;renderChrome();var log=st.root.querySelector('.ag-log');
    if(!st.cfg.providers.length){log.innerHTML=onboardingHtml();return}
    if(!st.conv.messages.length){log.innerHTML=emptyHtml();return}
    log.innerHTML=st.conv.messages.map(messageHtml).join('')+runsTailHtml();
    scrollEnd(true);
  }
  function renderMessage(m){ /* atualização pontual durante o streaming */
    var el=st.root.querySelector('[data-mid="'+m.id+'"]');
    if(!el){renderAll();return}
    var tmp=doc.createElement('div');tmp.innerHTML=messageHtml(m);var n=tmp.firstElementChild;if(n)el.replaceWith(n);else el.remove();
  }
  function schedule(m){if(m)st.dirty.add(m);if(st.raf)return;st.raf=requestAnimationFrame(function(){st.raf=0;var near=nearEnd();st.dirty.forEach(renderMessage);st.dirty.clear();renderTail();renderChrome();if(near)scrollEnd()})}
  function nearEnd(){var l=st.root.querySelector('.ag-log');return l.scrollHeight-l.scrollTop-l.clientHeight<140}
  function scrollEnd(force){var l=st.root.querySelector('.ag-log');if(force||nearEnd())l.scrollTop=l.scrollHeight}
  function renderTail(){var log=st.root.querySelector('.ag-log'),old=log.querySelector('.ag-tail');if(old)old.remove();log.insertAdjacentHTML('beforeend',runsTailHtml())}

  function resultFor(id){var ms=st.conv.messages;for(var i=ms.length-1;i>=0;i--){var m=ms[i];if(m.role!=='user')continue;for(var j=0;j<m.content.length;j++){var b=m.content[j];if(b.type==='tool_result'&&b.tool_use_id===id)return b}}return null}
  function messageHtml(m){
    if(m.role==='user'){if(m.meta&&m.meta.toolResults)return '<div data-mid="'+m.id+'" hidden></div>';
      var t=m.content.filter(function(b){return b.type==='text'&&!b._hidden}).map(function(b){return b.text}).join('\n\n');
      var ctx=m.meta&&m.meta.context?'<div class="ag-uctx">'+ic('file')+esc(m.meta.context)+'</div>':'';
      return '<div class="ag-msg ag-user" data-mid="'+m.id+'">'+ctx+'<div class="ag-bubble">'+esc(t).replace(/\n/g,'<br>')+'</div></div>'}
    var h='';
    if(m.meta&&m.meta.reasoning)h+='<details class="ag-think"><summary>'+(m.meta.streaming&&!m.content.length?'Pensando…':'Raciocínio')+'</summary><div>'+esc(m.meta.reasoning).replace(/\n/g,'<br>')+'</div></details>';
    /* resposta dada antes de terminar a leitura: fica recolhida, a versão revisada vem em seguida */
    m.content.forEach(function(b){if(b.type==='text')h+=m.meta&&m.meta.draft?'<details class="ag-draft"><summary>Resposta preliminar (substituída pela revisada abaixo)</summary><div class="ag-md">'+md(b.text)+'</div></details>':'<div class="ag-md">'+md(b.text)+'</div>';else if(b.type==='tool_use')h+=toolCard(b,m)});
    if(m.meta&&m.meta.streaming&&!m.content.length&&!m.meta.reasoning)h+='<div class="ag-typing" aria-label="Pensando"><i></i><i></i><i></i></div>';
    return '<div class="ag-msg ag-ai" data-mid="'+m.id+'">'+h+'</div>';
  }
  var ACCESS_ICON={read:'search',write:'edit',destructive:'trash',ui:'open',memory:'star',plan:'check'};
  function latestPlanId(){var ms=st.conv?st.conv.messages:[];for(var i=ms.length-1;i>=0;i--){var c=ms[i].content||[];for(var j=c.length-1;j>=0;j--)if(c[j].type==='tool_use'&&c[j].name==='update_plan'&&!c[j]._pending)return c[j].id}return null}
  function planHtml(steps){var mark={done:'✓',in_progress:'◐',pending:'○'};
    return '<ol class="ag-plan">'+(steps||[]).map(function(x){var s=x&&x.status||'pending';return '<li class="p-'+esc(s)+'"><span>'+(mark[s]||'○')+'</span>'+esc(x&&x.step||'')+'</li>'}).join('')+'</ol>'}
  function toolCard(b,m){
    var tool=T.get(b.name)||(b.name==='delegate_research'?{access:'read',label:function(i){return 'Pesquisador: '+String(i.task||'').slice(0,70)}}:null);
    var label=tool&&tool.label&&!b._pending?safeLabel(tool,b.input):(b._pending?'Preparando '+b.name+'…':b.name);
    var r=resultFor(b.id),pend=st.pending&&st.pending.call.id===b.id,status=r?(r._status||(r.is_error?'error':'done')):(pend?'waiting':(m.meta&&m.meta.streaming)?'pending':(st.running?'running':'aborted'));
    var stTxt={done:'',error:'falhou',rejected:'recusado',aborted:'interrompido',waiting:'aguardando você',running:'',pending:''}[status];
    var prog=st.progress&&st.progress[b.id];
    var h='<div class="ag-tool s-'+status+'" data-tool="'+esc(b.id)+'"><div class="ag-tool-head"><span class="ag-tool-ic">'+ic(ACCESS_ICON[tool&&tool.access]||'layers')+'</span><span class="ag-tool-label">'+esc(label)+'</span>'+
      (status==='running'||status==='pending'?'<span class="ag-spin"></span>':status==='done'?'<span class="ag-ok">'+ic('check')+'</span>':'<span class="ag-st">'+esc(stTxt)+'</span>')+'</div>';
    if(prog&&!r)h+='<div class="ag-tool-prog">'+esc(prog)+'</div>';
    /* só o plano mais recente aparece aberto, como checklist */
    if(b.name==='update_plan'&&!b._pending&&b.id===latestPlanId())h+=planHtml(b.input&&b.input.steps);
    if(pend)h+=approvalHtml(st.pending);
    else if(r||!b._pending){h+='<details class="ag-tool-more"><summary>Detalhes</summary>'+(b._pending?'':'<pre class="ag-pre">'+esc(JSON.stringify(b.input,null,2)).slice(0,3000)+'</pre>')+(r?'<pre class="ag-pre ag-res'+(r.is_error?' err':'')+'">'+esc(String(r.content).slice(0,60000))+(String(r.content).length>60000?'\n…':'')+'</pre>':'')+'</details>'}
    return h+'</div>';
  }
  function safeLabel(t,i){try{return t.label(i||{})}catch(_){return t.name}}

  /* ---------- aprovação com prévia ---------- */
  function approvalHtml(p){
    var pv=p.preview,body='';
    if(pv){if(pv.note)body+='<p class="ag-apr-note">'+esc(pv.note)+'</p>';
      if(pv.before!=null||pv.after!=null)body+=diffHtml(pv.before,pv.after)}
    var danger=p.access==='destructive';
    return '<div class="ag-apr"><div class="ag-apr-title">'+(danger?'Confirmar exclusão':'Aprovar alteração')+(pv&&pv.path?' · <code>'+esc(pv.path)+'</code>':'')+'</div>'+body+
      '<div class="ag-apr-actions"><button type="button" class="ui-btn" data-apr="reject">Recusar</button>'+(danger?'':'<button type="button" class="ui-btn" data-apr="all" title="Aprovar esta e as próximas alterações desta resposta">Aprovar todas</button>')+
      '<button type="button" class="ui-btn '+(danger?'ui-btn-danger':'ui-btn-primary')+'" data-apr="ok">'+(danger?'Excluir':'Aprovar')+'</button></div></div>';
  }
  function diffHtml(a,b){
    var x=a==null?[]:String(a).split('\n'),y=b==null?[]:String(b).split('\n'),ops=diffLines(x,y),out=[],shown=0,MAX=160;
    var keep=new Array(ops.length).fill(false);ops.forEach(function(o,i){if(o[0]!==' ')for(var k=Math.max(0,i-2);k<=Math.min(ops.length-1,i+2);k++)keep[k]=true});
    var gap=false;
    for(var i=0;i<ops.length&&shown<MAX;i++){if(!keep[i]){if(!gap){out.push('<div class="dl gap">⋯</div>');gap=true}continue}gap=false;shown++;
      out.push('<div class="dl '+(ops[i][0]==='+'?'add':ops[i][0]==='-'?'del':'')+'"><span>'+ops[i][0]+'</span>'+(esc(ops[i][1])||'&nbsp;')+'</div>')}
    if(shown>=MAX)out.push('<div class="dl gap">… mais linhas</div>');
    var add=ops.filter(function(o){return o[0]==='+'}).length,del=ops.filter(function(o){return o[0]==='-'}).length;
    return '<div class="ag-diff-stat"><b class="a">+'+add+'</b> <b class="d">−'+del+'</b></div><div class="ag-diff">'+out.join('')+'</div>';
  }
  function diffLines(a,b){ /* LCS com corte: arquivos enormes caem num diff simples */
    if(a.length*b.length>4e6){return a.map(function(l){return['-',l]}).concat(b.map(function(l){return['+',l]}))}
    var n=a.length,m=b.length,dp=[];for(var i=0;i<=n;i++){dp[i]=new Uint32Array(m+1)}
    for(i=n-1;i>=0;i--)for(var j=m-1;j>=0;j--)dp[i][j]=a[i]===b[j]?dp[i+1][j+1]+1:Math.max(dp[i+1][j],dp[i][j+1]);
    var out=[];i=0;j=0;while(i<n&&j<m){if(a[i]===b[j]){out.push([' ',a[i]]);i++;j++}else if(dp[i+1][j]>=dp[i][j+1])out.push(['-',a[i++]]);else out.push(['+',b[j++]])}
    while(i<n)out.push(['-',a[i++]]);while(j<m)out.push(['+',b[j++]]);return out;
  }

  /* ---------- rodapé da última execução: desfazer, erros, continuar ---------- */
  function runsTailHtml(){
    var runs=st.conv&&st.conv.runs||[],r=runs[runs.length-1];if(!r||st.running)return '';
    var h='';
    if(r.error)h+='<div class="ag-note err">'+esc(r.error)+'<button type="button" class="ag-link" data-retry>Tentar de novo</button></div>';
    else if(r.stopped==='aborted')h+='<div class="ag-note">Interrompido.<button type="button" class="ag-link" data-continue>Continuar</button></div>';
    else if(r.stopped==='max_steps')h+='<div class="ag-note">Parei no limite de '+(st.cfg.maxSteps||30)+' passos.<button type="button" class="ag-link" data-continue>Continuar</button></div>';
    else if(r.stopped==='loop')h+='<div class="ag-note">Parei: o agente repetia uma ação que falhava.<button type="button" class="ag-link" data-continue>Continuar</button></div>';
    else if(r.stopped==='max_tokens')h+='<div class="ag-note">A resposta foi cortada pelo limite do modelo.<button type="button" class="ag-link" data-continue>Continuar</button></div>';
    var n=r.changes&&r.changes.length;
    if(n){var files=[];r.changes.forEach(function(c){var p=(c.after||c.before||{}).path;if(p&&files.indexOf(p)<0)files.push(p)});
      h+='<div class="ag-changes'+(r.reverted?' reverted':'')+'"><span>'+ic('edit')+(r.reverted?'Alterações desfeitas':files.length+' nota'+(files.length>1?'s':'')+' alterada'+(files.length>1?'s':''))+'</span>'+
        (r.reverted?'':'<button type="button" class="ag-link" data-undo>Desfazer</button>')+'<div class="ag-files">'+files.slice(0,8).map(function(f){return '<a class="ag-wiki" data-path="'+esc(f)+'">'+esc(f)+'</a>'}).join('')+'</div></div>'}
    return h?'<div class="ag-tail">'+h+'</div>':'';
  }

  /* ---------- estados vazios ---------- */
  var PRESET_ORDER=['openrouter','anthropic','openai','gemini','ollama','custom'];
  function onboardingHtml(){
    return '<div class="ag-empty"><h2>Conecte um modelo</h2><p>O Assistente funciona com qualquer modelo: escolha um provedor e cole sua chave. Ela fica salva só neste aparelho.</p><div class="ag-presets">'+
      PRESET_ORDER.map(function(k){var p=P.PRESETS[k];return '<button type="button" class="ag-preset" data-preset="'+k+'"><strong>'+esc(p.name)+'</strong><small>'+esc({openrouter:'Recomendado · Claude, GPT, Gemini, Llama e outros com uma chave',anthropic:'Claude direto da Anthropic',openai:'GPT direto da OpenAI',gemini:'Gemini do Google',ollama:'Modelos no seu computador, sem internet',custom:'LM Studio, vLLM, Groq… (API compatível)'}[k])+'</small></button>'}).join('')+'</div></div>';
  }
  function emptyHtml(){
    var e=editorInfo(),a=A.agentById(st.conv.agentId,st.cfg.customAgents),sug=[];
    if(e&&e.path){sug.push('Resuma esta nota em tópicos','Revise o texto desta nota: clareza e erros','Encontre notas relacionadas e sugira [[links]] para esta')}
    sug.push('O que tem no meu vault? Dê uma visão geral','Encontre notas sem nenhuma ligação e sugira conexões','Crie uma nota-índice que ligue as notas por tema');
    return '<div class="ag-empty"><div class="ag-hello">'+ic(a.icon||'sparkle')+'</div><h2>'+esc(a.name)+'</h2><p>'+esc(a.description||'')+'</p><div class="ag-sugs">'+sug.slice(0,5).map(function(s){return '<button type="button" class="ag-sug" data-sug="'+esc(s)+'">'+esc(s)+'</button>'}).join('')+'</div></div>';
  }

  /* ---------- markdown seguro (escapa tudo antes de formatar) ---------- */
  function inline(s){
    var codes=[];s=s.replace(/`([^`\n]+)`/g,function(_,c){codes.push(c);return '\u0000'+(codes.length-1)+'\u0000'});
    s=esc(s);
    s=s.replace(/\[\[([^\]\n|#]+)(?:#[^\]\n|]*)?(?:\|([^\]\n]+))?\]\]/g,function(_,t,al){return '<a class="ag-wiki" data-note="'+t.trim()+'">'+(al||t).trim()+'</a>'});
    s=s.replace(/\[([^\]\n]+)\]\((https?:\/\/[^\s)]+)\)/g,'<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>');
    s=s.replace(/\*\*([^*\n]+)\*\*/g,'<strong>$1</strong>').replace(/(^|[^*\w])\*([^*\n]+)\*(?!\w)/g,'$1<em>$2</em>').replace(/(^|[^\w])_([^_\n]+)_(?!\w)/g,'$1<em>$2</em>').replace(/~~([^~\n]+)~~/g,'<del>$1</del>');
    return s.replace(/\u0000(\d+)\u0000/g,function(_,i){return '<code>'+esc(codes[+i])+'</code>'});
  }
  /* fórmulas LaTeX nas respostas ($…$, $$…$$) quando o módulo de matemática está carregado */
  function md(src){var Mth=global.UrbeMath;return Mth?Mth.renderWith(mdPlain,src):mdPlain(src)}
  function mdPlain(src){
    var lines=String(src||'').replace(/\r\n?/g,'\n').split('\n'),out=[],i=0,list=null;
    function closeList(){if(list){out.push('</'+list+'>');list=null}}
    while(i<lines.length){var l=lines[i],m;
      if((m=l.match(/^```(\w*)/))){closeList();var buf=[];i++;while(i<lines.length&&!/^```/.test(lines[i]))buf.push(lines[i++]);i++;out.push('<pre class="ag-code"><code>'+esc(buf.join('\n'))+'</code></pre>');continue}
      if((m=l.match(/^(#{1,4})\s+(.*)$/))){closeList();out.push('<h'+(m[1].length+2)+'>'+inline(m[2])+'</h'+(m[1].length+2)+'>');i++;continue}
      if((m=l.match(/^\s*[-*+]\s+(\[[ xX]\]\s+)?(.*)$/))){if(list!=='ul'){closeList();out.push('<ul>');list='ul'}out.push('<li>'+(m[1]?(/x/i.test(m[1])?'☑ ':'☐ '):'')+inline(m[2])+'</li>');i++;continue}
      if((m=l.match(/^\s*\d+[.)]\s+(.*)$/))){if(list!=='ol'){closeList();out.push('<ol>');list='ol'}out.push('<li>'+inline(m[1])+'</li>');i++;continue}
      closeList();
      if((m=l.match(/^>\s?(.*)$/))){out.push('<blockquote>'+inline(m[1])+'</blockquote>');i++;continue}
      if(/^\s*(---|\*\*\*)\s*$/.test(l)){out.push('<hr>');i++;continue}
      if(/^\|.*\|\s*$/.test(l)&&i+1<lines.length&&/^\|?\s*:?-{2,}/.test(lines[i+1])){var rows=[];while(i<lines.length&&/^\|.*\|\s*$/.test(lines[i]))rows.push(lines[i++]);
        var cells=function(r){return r.replace(/^\||\|\s*$/g,'').split('|').map(function(c){return inline(c.trim())})};
        out.push('<div class="ag-table"><table><thead><tr>'+cells(rows[0]).map(function(c){return '<th>'+c+'</th>'}).join('')+'</tr></thead><tbody>'+rows.slice(2).map(function(r){return '<tr>'+cells(r).map(function(c){return '<td>'+c+'</td>'}).join('')+'</tr>'}).join('')+'</tbody></table></div>');continue}
      if(!l.trim()){i++;continue}
      var para=[l];i++;while(i<lines.length&&lines[i].trim()&&!/^(#{1,4}\s|```|\s*[-*+]\s|\s*\d+[.)]\s|>|\|)/.test(lines[i]))para.push(lines[i++]);
      out.push('<p>'+para.map(inline).join('<br>')+'</p>');
    }
    closeList();return out.join('');
  }

  /* ================= ações ================= */
  function onLogClick(e){
    var t=e.target.closest('[data-apr],[data-sug],[data-preset],[data-undo],[data-retry],[data-continue],.ag-wiki');if(!t)return;
    if(t.dataset.apr&&st.pending){var p=st.pending;st.pending=null;p.resolve(t.dataset.apr==='ok'?'approve':t.dataset.apr==='all'?'approve_all':{reject:true});schedule(msgOf(p.call.id));return}
    if(t.dataset.sug){var ta=st.root.querySelector('textarea');ta.value=t.dataset.sug;autosize();submit();return}
    if(t.dataset.preset){showSettings({addPreset:t.dataset.preset});return}
    if(t.hasAttribute('data-undo')){undoLast();return}
    if(t.hasAttribute('data-retry')){start();return}
    if(t.hasAttribute('data-continue')){send('Continue de onde parou.');return}
    if(t.classList.contains('ag-wiki')){openRef(t.dataset.path||t.dataset.note)}
  }
  function msgOf(toolId){return st.conv.messages.find(function(m){return m.role==='assistant'&&m.content.some(function(b){return b.id===toolId})})}
  function openRef(ref){if(!ref||!docs)return;var d=docs.get(ref)||docs.get(ref+'.md');
    if(!d){var t=String(ref).toLowerCase(),hits=docs.list().filter(function(x){return x.title.toLowerCase()===t});d=hits[0]}
    if(!d){toast('A nota “'+ref+'” não existe.');return}
    if(hooks.openNote)hooks.openNote(d.id);else core.commands.execute('document.open',{id:d.id,source:'ai.link'});
    if(global.innerWidth<900)st.root.classList.remove('open');
  }
  function toast(t){var el=doc.getElementById('toast');if(!el)return;el.textContent=t;el.classList.add('show');clearTimeout(toast._t);toast._t=setTimeout(function(){el.classList.remove('show')},2600)}

  function submit(){var ta=st.root.querySelector('textarea'),text=ta.value.trim();if(!text||st.running)return;ta.value='';autosize();send(text)}
  function send(text){
    if(!st.cfg.providers.length){showSettings({addPreset:'openrouter'});return}
    if(!modelId()){pickModel();return}
    var e=editorInfo(),msg={id:A.uid('m'),role:'user',content:[{type:'text',text:text}],meta:{}};
    if(e&&e.path&&st.useOpen){msg.meta.context=e.path+(e.selection?' · seleção':'');msg.content.push({type:'text',text:'[Contexto automático: nota aberta no editor = '+e.path+(e.selection?'; texto selecionado pelo usuário:\n"""\n'+e.selection.slice(0,4000)+'\n"""':'')+']',_hidden:true})}
    if(!st.conv.title)st.conv.title=A.titleFrom(text);
    st.conv.messages.push(msg);renderAll();start();
  }
  function stop(){if(st.running){st.running.abort();if(st.pending){var p=st.pending;st.pending=null;p.resolve({reject:true})}}}
  async function start(){
    if(st.running)return;var p=provider();if(!p){showSettings();return}
    var ac=new AbortController(),conv=st.conv,meta=modelMeta();st.running=ac;st.progress={};renderChrome();renderTail();
    var run={id:A.uid('r'),started:Date.now(),changes:[],stopped:null,error:null};
    try{
      var r=await A.run({core:core,provider:p,model:modelId(),messages:conv.messages,agent:A.agentById(conv.agentId,st.cfg.customAgents),policy:conv.policy,signal:ac.signal,
        maxSteps:st.cfg.maxSteps||30,contextTokens:meta&&meta.context||128000,instructions:st.cfg.instructions,memory:memoryList(),vaultName:hooks.vaultName(),
        editor:editorInfo,openNote:hooks.openNote,remember:remember,approve:approve,onUpdate:onUpdate});
      run.changes=r.changes;run.stopped=r.stopped;run.error=r.error;
      var u=conv.usage||(conv.usage={input:0,output:0,cost:null});u.input+=r.usage.input;u.output+=r.usage.output;if(r.usage.cost!=null)u.cost=(u.cost||0)+r.usage.cost;
    }catch(e){run.error=e&&e.message||String(e);run.stopped='error'}
    finally{st.running=null;st.pending=null;conv.runs=(conv.runs||[]).concat([run]).slice(-20);if(conv===st.conv){renderAll();}persist()}
  }
  function onUpdate(e){
    if(e.type==='message'){var log=st.root.querySelector('.ag-log');if(log.querySelector('.ag-empty'))renderAll();else{var t=doc.createElement('div');t.innerHTML=messageHtml(e.message);var tail=log.querySelector('.ag-tail');if(t.firstElementChild)log.insertBefore(t.firstElementChild,tail||null)}scrollEnd();return}
    if(e.type==='removed'){var el=st.root.querySelector('[data-mid="'+e.message.id+'"]');if(el)el.remove();return}
    if(e.type==='tool_progress'){st.progress[e.call.id]=e.text}
    if(e.call){schedule(msgOf(e.call.id));return}
    schedule(e.message);
  }
  function approve(req,signal){
    return new Promise(function(resolve){
      st.pending={call:req.call,preview:req.preview,access:req.access,resolve:resolve};
      if(signal)signal.addEventListener('abort',function(){if(st.pending&&st.pending.call===req.call){st.pending=null;resolve({reject:true})}},{once:true});
      schedule(msgOf(req.call.id));
      if(!st.root.classList.contains('open'))toast('O Assistente está esperando sua aprovação.');
    });
  }
  async function undoLast(){
    var r=st.conv.runs[st.conv.runs.length-1];if(!r||!r.changes.length||r.reverted)return;
    var ok=await D.confirm({title:'Desfazer as alterações?',message:'As notas voltam ao estado de antes desta resposta.',confirm:'Desfazer'});if(!ok)return;
    var rep=T.revert({core:core},r.changes);r.reverted=true;persist();renderAll();
    if(rep.skipped.length)D.alert({title:'Algumas notas foram mantidas',message:'Você editou estas notas depois do Assistente, então elas não foram revertidas:\n'+rep.skipped.join('\n')});
    else toast('Alterações desfeitas.');
  }

  /* ---------- seletores ---------- */
  function pickAgent(){if(st.running)return;var list=A.AGENTS.concat(st.cfg.customAgents||[]);
    D.choose({title:'Agente',options:list.map(function(a){return{value:a.id,label:a.name,detail:a.description||(a.tools==='read'?'Só leitura':'Personalizado'),icon:a.icon||'sparkle',current:a.id===st.conv.agentId}})}).then(function(id){if(!id)return;st.conv.agentId=id;st.cfg.agentId=id;saveCfg();renderAll()})}
  function pickPolicy(){if(st.running)return;
    D.choose({title:'Permissões nesta conversa',options:[{value:'ask',label:'Pede aprovação',detail:'Lê à vontade; mostra cada alteração antes de aplicar',icon:'check',current:st.conv.policy==='ask'},{value:'auto',label:'Edita sozinho',detail:'Aplica alterações direto (dá para desfazer). Exclusões ainda pedem aprovação',icon:'edit',current:st.conv.policy==='auto'},{value:'readonly',label:'Só leitura',detail:'Nunca altera notas',icon:'search',current:st.conv.policy==='readonly'}]}).then(function(v){if(!v)return;st.conv.policy=v;st.cfg.policy=v;saveCfg();renderChrome()})}

  /* seletor de modelo: lista do provedor, busca, preço e aviso de ferramentas */
  async function pickModel(){
    if(!st.cfg.providers.length){showSettings({addPreset:'openrouter'});return}
    var v=openView('<header class="ag-vhead"><button type="button" class="ag-ib" data-back aria-label="Voltar">'+ic('back')+'</button><h2>Modelo</h2></header>'+
      '<div class="ag-tabs">'+st.cfg.providers.map(function(p){return '<button type="button" data-prov="'+esc(p.id)+'" class="'+(p.id===provider().id?'on':'')+'">'+esc(p.name)+'</button>'}).join('')+'</div>'+
      '<label class="ag-search">'+ic('search')+'<input type="search" placeholder="Buscar modelo (ex.: claude, gpt, gemini)"></label><div class="ag-mlist"><div class="ag-loading">Carregando modelos…</div></div>'+
      '<div class="ag-manual"><input type="text" placeholder="Ou digite o ID do modelo"><button type="button" class="ui-btn">Usar</button></div>');
    var prov=provider(),input=v.querySelector('.ag-search input'),list=v.querySelector('.ag-mlist'),models=[];
    async function load(){list.innerHTML='<div class="ag-loading">Carregando modelos…</div>';
      var cache=st.cfg.modelCache[prov.id];
      if(cache&&Date.now()-cache.at<6*3600e3)models=cache.list;
      else{try{models=await P.listModels(prov);st.cfg.modelCache[prov.id]={at:Date.now(),list:models};saveCfg()}catch(e){list.innerHTML='<div class="ag-loading err">'+esc(e.message)+'</div>';models=[];return}}
      paint()}
    function paint(){var q=input.value.trim().toLowerCase(),cur=modelId(),hits=models.filter(function(m){return !q||(m.id+' '+m.name).toLowerCase().indexOf(q)>=0});
      hits.sort(function(a,b){return(b.tools===true)-(a.tools===true)||rank(a.id)-rank(b.id)||a.name.localeCompare(b.name)});
      list.innerHTML=hits.slice(0,200).map(function(m){return '<button type="button" class="ag-model'+(m.id===cur&&prov.id===provider().id?' on':'')+'" data-m="'+esc(m.id)+'"><strong>'+esc(m.name)+'</strong><small>'+esc(m.id)+(m.context?' · '+Math.round(m.context/1000)+'k contexto':'')+(m.price?' · US$ '+m.price.input.toFixed(2)+'/'+m.price.output.toFixed(2)+' por 1M':'')+(m.tools===false?' · <b class="warn">sem ferramentas</b>':'')+'</small></button>'}).join('')||'<div class="ag-loading">Nenhum modelo.</div>'}
    function rank(id){var s=['claude','gpt-5','gpt-4','gemini','qwen','llama','mistral','deepseek'];for(var i=0;i<s.length;i++)if(id.toLowerCase().indexOf(s[i])>=0)return i;return 99}
    function choose(id){st.cfg.providerId=prov.id;st.cfg.model=id;st.conv.providerId=prov.id;st.conv.model=id;saveCfg();closeView();renderAll()}
    input.oninput=paint;list.onclick=function(e){var b=e.target.closest('[data-m]');if(b)choose(b.dataset.m)};
    v.querySelector('.ag-tabs').onclick=function(e){var b=e.target.closest('[data-prov]');if(!b)return;prov=st.cfg.providers.find(function(p){return p.id===b.dataset.prov});v.querySelectorAll('.ag-tabs button').forEach(function(x){x.classList.toggle('on',x===b)});load()};
    v.querySelector('.ag-manual button').onclick=function(){var id=v.querySelector('.ag-manual input').value.trim();if(id)choose(id)};
    load();setTimeout(function(){input.focus()},30);
  }

  /* ---------- vistas sobrepostas (conversas, configurações) ---------- */
  function openView(html){var v=st.root.querySelector('[data-view-panel]');v.innerHTML=html;v.hidden=false;st.root.querySelector('.ag').classList.add('ag-viewing');var b=v.querySelector('[data-back]');if(b)b.onclick=closeView;return v}
  function closeView(){var v=st.root.querySelector('[data-view-panel]');v.hidden=true;v.innerHTML='';st.root.querySelector('.ag').classList.remove('ag-viewing')}

  async function showHistory(){
    var list=await S.listConversations(),v=hooks.vaultName();
    var view=openView('<header class="ag-vhead"><button type="button" class="ag-ib" data-back aria-label="Voltar">'+ic('back')+'</button><h2>Conversas</h2></header><div class="ag-hist">'+
      (list.length?list.map(function(c){var n=c.messages.filter(function(m){return m.role==='user'&&!(m.meta&&m.meta.toolResults)}).length,a=A.agentById(c.agentId,st.cfg.customAgents);
        return '<div class="ag-hrow'+(st.conv&&c.id===st.conv.id?' on':'')+'"><button type="button" class="ag-hopen" data-open="'+esc(c.id)+'"><strong>'+esc(c.title||'Conversa')+'</strong><small>'+esc(a.name)+' · '+n+' pedido'+(n===1?'':'s')+' · '+new Date(c.updated).toLocaleString('pt-BR',{dateStyle:'short',timeStyle:'short'})+(c.vault&&c.vault!==v?' · '+esc(c.vault):'')+'</small></button><button type="button" class="ag-ib" data-del="'+esc(c.id)+'" aria-label="Excluir conversa">'+ic('trash')+'</button></div>'}).join(''):'<div class="ag-loading">Nenhuma conversa ainda.</div>')+'</div>');
    view.querySelector('.ag-hist').onclick=async function(e){var o=e.target.closest('[data-open]'),d=e.target.closest('[data-del]');
      if(o&&!st.running){st.conv=await S.getConversation(o.dataset.open);closeView();renderAll()}
      if(d){if(!(await D.confirm({title:'Excluir esta conversa?',confirm:'Excluir',danger:true})))return;await S.deleteConversation(d.dataset.del);if(st.conv&&st.conv.id===d.dataset.del)st.conv=newConversation();showHistory();renderAll()}};
  }

  function showSettings(opts){
    opts=opts||{};if(!st.cfg){(st.ready||Promise.resolve()).then(function(){showSettings(opts)});return}
    var c=st.cfg,v=hooks.vaultName(),mem=(c.memory&&c.memory[v]||[]).map(function(f,i){return{f:f,k:v,i:i}}).concat((c.memory&&c.memory['*']||[]).map(function(f,i){return{f:f,k:'*',i:i}}));
    var view=openView('<header class="ag-vhead"><button type="button" class="ag-ib" data-back aria-label="Voltar">'+ic('back')+'</button><h2>Configurações do Assistente</h2></header><div class="ag-set">'+
      '<section><h3>Provedores de modelo</h3><p class="ag-hint">As chaves ficam salvas só neste aparelho e vão direto para o provedor.</p><div class="ag-provs">'+
        (c.providers.map(function(p){var pre=P.PRESETS[p.preset]||P.PRESETS.custom;return '<div class="ag-prov"><div><strong>'+esc(p.name)+'</strong><small>'+esc(p.baseUrl||pre.baseUrl)+(p.apiKey?' · chave •••'+esc(p.apiKey.slice(-4)):'')+'</small><small class="ag-test" data-test-out="'+esc(p.id)+'"></small></div>'+
          '<button type="button" class="ui-btn" data-test="'+esc(p.id)+'">Testar</button><button type="button" class="ui-btn" data-edit="'+esc(p.id)+'">Editar</button><button type="button" class="ag-ib" data-rm="'+esc(p.id)+'" aria-label="Remover">'+ic('trash')+'</button></div>'}).join('')||'<p class="ag-hint">Nenhum provedor ainda.</p>')+
      '</div><button type="button" class="ui-btn" data-add-prov>'+ic('plus')+' Adicionar provedor</button></section>'+
      '<section><h3>Instruções para todos os agentes</h3><textarea data-instr rows="4" placeholder="Ex.: escreva em português do Brasil, frases curtas; use a pasta Diário para registros do dia.">'+esc(c.instructions||'')+'</textarea></section>'+
      '<section><h3>Memória</h3><p class="ag-hint">Fatos que você pediu para o Assistente lembrar. Valem para este vault ('+esc(v)+') e, os marcados como gerais, para todos.</p><div class="ag-mem">'+
        (mem.map(function(m){return '<div class="ag-memrow"><span>'+esc(m.f)+(m.k==='*'?' <small>geral</small>':'')+'</span><button type="button" class="ag-ib" data-forget="'+esc(m.k)+'|'+m.i+'" aria-label="Esquecer">'+ic('close')+'</button></div>'}).join('')||'<p class="ag-hint">Vazia. Diga “lembre que…” numa conversa.</p>')+'</div></section>'+
      '<section><h3>Agentes personalizados</h3><div class="ag-custom">'+((c.customAgents||[]).map(function(a){return '<div class="ag-prov"><div><strong>'+esc(a.name)+'</strong><small>'+esc({all:'Tudo',write:'Lê e escreve (sem excluir)',read:'Só leitura'}[a.tools]||'')+'</small></div><button type="button" class="ui-btn" data-edit-agent="'+esc(a.id)+'">Editar</button><button type="button" class="ag-ib" data-rm-agent="'+esc(a.id)+'" aria-label="Remover">'+ic('trash')+'</button></div>'}).join('')||'<p class="ag-hint">Crie agentes com instruções próprias, ex.: “Revisor acadêmico”.</p>')+
        '</div><button type="button" class="ui-btn" data-add-agent>'+ic('plus')+' Novo agente</button></section>'+
      '<section><h3>Limite de passos por resposta</h3><input type="number" min="3" max="100" data-steps value="'+(c.maxSteps||30)+'"><p class="ag-hint">Cada leitura, busca ou edição é um passo.</p></section>'+
      '</div>');
    var instr=view.querySelector('[data-instr]');instr.onchange=function(){c.instructions=instr.value;saveCfg()};
    var steps=view.querySelector('[data-steps]');steps.onchange=function(){c.maxSteps=Math.max(3,Math.min(100,+steps.value||30));saveCfg()};
    view.onclick=async function(e){var t=e.target.closest('button');if(!t)return;
      if(t.hasAttribute('data-add-prov')){var pre=await D.choose({title:'Qual provedor?',options:PRESET_ORDER.map(function(k){return{value:k,label:P.PRESETS[k].name,icon:'storage'}})});if(pre)editProvider(null,pre)}
      else if(t.dataset.edit)editProvider(c.providers.find(function(p){return p.id===t.dataset.edit}));
      else if(t.dataset.rm){if(!(await D.confirm({title:'Remover este provedor?',message:'A chave salva será apagada deste aparelho.',confirm:'Remover',danger:true})))return;c.providers=c.providers.filter(function(p){return p.id!==t.dataset.rm});if(c.providerId===t.dataset.rm){c.providerId=c.providers[0]&&c.providers[0].id||null;c.model=''}await saveCfg();showSettings()}
      else if(t.dataset.test){var p=c.providers.find(function(x){return x.id===t.dataset.test}),out=view.querySelector('[data-test-out="'+t.dataset.test+'"]'),m=p.id===provider().id&&modelId()||(P.PRESETS[p.preset]&&P.PRESETS[p.preset].models||[])[0];
        out.className='ag-test';out.textContent='Testando…';
        try{if(!m){var ms=await P.listModels(p);out.textContent='Conectado · '+ms.length+' modelos disponíveis';out.classList.add('ok');return}var r=await P.test(p,m);out.textContent='Funcionando · '+r.model;out.classList.add('ok')}catch(err){out.textContent=err.message;out.classList.add('err')}}
      else if(t.dataset.forget){var parts=t.dataset.forget.split('|');(c.memory[parts[0]]||[]).splice(+parts[1],1);await saveCfg();showSettings()}
      else if(t.hasAttribute('data-add-agent'))editAgent(null);
      else if(t.dataset.editAgent)editAgent(c.customAgents.find(function(a){return a.id===t.dataset.editAgent}));
      else if(t.dataset.rmAgent){c.customAgents=c.customAgents.filter(function(a){return a.id!==t.dataset.rmAgent});await saveCfg();showSettings()}
    };
    if(opts.addPreset)editProvider(null,opts.addPreset);
  }
  function editProvider(p,preset){
    var pre=P.PRESETS[p?p.preset:preset]||P.PRESETS.custom,isNew=!p;p=p||{id:A.uid('p'),preset:preset,name:pre.name,baseUrl:'',apiKey:''};
    var view=openView('<header class="ag-vhead"><button type="button" class="ag-ib" data-back aria-label="Voltar">'+ic('back')+'</button><h2>'+esc(isNew?'Conectar '+pre.name:p.name)+'</h2></header><form class="ag-set ag-form">'+
      '<label><span>Nome</span><input name="name" value="'+esc(p.name)+'"></label>'+
      '<label><span>Endereço da API</span><input name="baseUrl" value="'+esc(p.baseUrl||pre.baseUrl)+'" placeholder="https://…/v1" autocapitalize="none" spellcheck="false"></label>'+
      '<label><span>Chave da API'+(pre.needsKey?'':' (opcional)')+'</span><input name="apiKey" type="password" value="'+esc(p.apiKey)+'" placeholder="'+esc(pre.keyHint||'')+'" autocomplete="off" autocapitalize="none" spellcheck="false"></label>'+
      (pre.site?'<p class="ag-hint">Crie uma chave em <a href="'+esc(pre.site)+'" target="_blank" rel="noopener">'+esc(pre.site.replace(/^https:\/\//,''))+'</a>.</p>':'')+
      (p.preset==='ollama'?'<p class="ag-hint">Abra o Ollama com <code>OLLAMA_ORIGINS=*</code> para o navegador conseguir acessar, e use um modelo com suporte a ferramentas (ex.: qwen2.5, llama3.1).</p>':'')+
      '<p class="ag-test" data-out></p><div class="ag-row2"><button type="button" class="ui-btn" data-back2>Cancelar</button><button type="submit" class="ui-btn ui-btn-primary">Salvar e escolher modelo</button></div></form>');
    view.querySelector('[data-back]').onclick=function(){showSettings()};view.querySelector('[data-back2]').onclick=function(){showSettings()};
    view.querySelector('form').onsubmit=async function(e){e.preventDefault();var f=e.target,out=view.querySelector('[data-out]');
      p.name=f.name.value.trim()||pre.name;p.baseUrl=f.baseUrl.value.trim()===pre.baseUrl?'':f.baseUrl.value.trim();p.apiKey=f.apiKey.value.trim();
      if(pre.needsKey&&!p.apiKey){out.className='ag-test err';out.textContent='Cole a chave da API.';return}
      if(!P.resolve(p).baseUrl){out.className='ag-test err';out.textContent='Informe o endereço da API.';return}
      out.className='ag-test';out.textContent='Conectando…';
      try{var list=await P.listModels(p);st.cfg.modelCache[p.id]={at:Date.now(),list:list}}catch(err){if(err.status===401||err.status===403){out.className='ag-test err';out.textContent=err.message;return}}
      if(isNew)st.cfg.providers.push(p);if(!st.cfg.providerId||isNew){st.cfg.providerId=p.id;st.cfg.model='';if(st.conv){st.conv.providerId=p.id;st.conv.model=''}}
      await saveCfg();closeView();renderAll();pickModel();
    };
  }
  function editAgent(a){
    var isNew=!a;a=a||{id:A.uid('a'),name:'',instructions:'',tools:'all',icon:'sparkle'};
    var view=openView('<header class="ag-vhead"><button type="button" class="ag-ib" data-back aria-label="Voltar">'+ic('back')+'</button><h2>'+(isNew?'Novo agente':esc(a.name))+'</h2></header><form class="ag-set ag-form">'+
      '<label><span>Nome</span><input name="name" value="'+esc(a.name)+'" placeholder="Ex.: Revisor acadêmico" required></label>'+
      '<label><span>Instruções</span><textarea name="instructions" rows="7" placeholder="Como este agente deve pensar e trabalhar.">'+esc(a.prompt||'')+'</textarea></label>'+
      '<label><span>O que pode fazer</span><select name="tools"><option value="all">Tudo (inclusive excluir, com aprovação)</option><option value="write">Ler e escrever, sem excluir</option><option value="read">Só ler</option></select></label>'+
      '<div class="ag-row2"><button type="button" class="ui-btn" data-back2>Cancelar</button><button type="submit" class="ui-btn ui-btn-primary">Salvar</button></div></form>');
    view.querySelector('select').value=a.tools;view.querySelector('[data-back]').onclick=function(){showSettings()};view.querySelector('[data-back2]').onclick=function(){showSettings()};
    view.querySelector('form').onsubmit=async function(e){e.preventDefault();var f=e.target;a.name=f.name.value.trim();if(!a.name)return;a.prompt=f.instructions.value.trim();a.tools=f.tools.value;a.description=a.prompt.slice(0,80);a.icon=a.tools==='read'?'search':'sparkle';a.delegate=a.tools==='all';
      if(isNew)st.cfg.customAgents=(st.cfg.customAgents||[]).concat([a]);await saveCfg();showSettings()};
  }

  function opened(){if(!st.root)return;(st.ready||Promise.resolve()).then(function(){renderAll();var ta=st.root.querySelector('textarea');if(ta&&!coarse)setTimeout(function(){ta.focus()},60)})}

  /* abre o composer com um começo de pedido (ex.: vindo do estúdio de páginas) */
  function prefill(text){if(!st.root)return;var ta=st.root.querySelector('textarea');if(!ta)return;if(!ta.value.trim())ta.value=String(text||'');autosize();setTimeout(function(){ta.focus();ta.setSelectionRange(ta.value.length,ta.value.length)},60)}
  global.UrbeAgentUI={mount:mount,opened:opened,prefill:prefill,showSettings:function(o){showSettings(o)},configure:function(h){Object.assign(hooks,h)},_md:md,_diff:diffLines,state:st};
})(window);
