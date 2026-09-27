(function(global){
  'use strict';
  /* Ferramentas que os agentes usam no vault. Cada ferramenta declara:
       name, description, parameters (JSON Schema), access ('read'|'write'|'destructive'|'ui'|'memory'),
       label(input) → texto curto para a interface,
       preview(input,ctx) → {path, before, after} (mostrado na aprovação),
       run(input,ctx) → {content:string, changes?:[{id, before:{path,content}|null, after:{path,content}|null}]}.
     Tudo passa pelo DocumentStore (fonte da verdade), então editor, cidade,
     índice de links e gravação em disco se atualizam sozinhos. */
  var LIMIT=24000;

  function services(ctx){var core=ctx.core;return{core:core,docs:core.service('documents'),knowledge:core.service('knowledge'),trash:core.service('trash'),explorer:core.service('explorer'),persistence:core.service('persistence')}}
  function norm(p){return String(p||'').trim().replace(/\\/g,'/').replace(/^\/+|\/+$/g,'').replace(/\/+/g,'/')}
  function fold(s){return String(s||'').normalize('NFD').replace(/[̀-ͯ]/g,'').toLowerCase()}
  function loose(s){return fold(s).replace(/[^\p{L}\p{N}\/]+/gu,'')}
  function words(t){var m=String(t||'').match(/\S+/g);return m?m.length:0}
  function clip(s,n){s=String(s);n=n||LIMIT;return s.length>n?s.slice(0,n)+'\n…[cortado: +'+(s.length-n)+' caracteres; peça um trecho com start_line]':s}
  function ToolError(msg){var e=new Error(msg);e.toolError=true;return e}

  /* resolve caminho, caminho sem .md, título exato ou título sem acento; sugere parecidos */
  function resolveDoc(ctx,ref,must){
    var s=services(ctx),p=norm(ref);if(!p)throw ToolError('Informe o caminho da nota.');
    var d=s.docs.get(p)||s.docs.get(p+'.md');
    if(!d){var t=fold(p.replace(/\.(md|markdown)$/i,'').split('/').pop()),hits=s.docs.list().filter(function(x){return fold(x.title)===t});if(hits.length===1)d=hits[0];
      else if(hits.length>1&&must!==false)throw ToolError('Há '+hits.length+' notas chamadas "'+p+'": '+hits.map(function(x){return x.path}).join(', ')+'. Use o caminho completo.')}
    /* modelos às vezes estragam emoji/símbolos no caminho (“Teste/� Gue.md”): compara só letras e números */
    if(!d){var base=function(x){return x.replace(/\.(md|markdown)$/i,'')},lk=loose(base(p)),lt=loose(base(p).split('/').pop());
      if(lk){var lh=s.docs.list().filter(function(x){return loose(base(x.path))===lk});if(!lh.length&&lt)lh=s.docs.list().filter(function(x){return loose(x.title)===lt||loose(base(x.path).split('/').pop())===lt});if(lh.length===1)d=lh[0]}}
    if(!d&&must!==false){
      var near=s.docs.list().map(function(x){return{x:x,s:score(fold(x.path),fold(p))}}).filter(function(o){return o.s>0}).sort(function(a,b){return b.s-a.s}).slice(0,5).map(function(o){return o.x.path});
      throw ToolError('Nota não encontrada: '+p+(near.length?'. Parecidas: '+near.join(', '):'. Use search_notes ou list_notes para achar o caminho.'));
    }
    return d||null;
  }
  function score(a,b){if(a===b)return 100;if(a.indexOf(b)>=0||b.indexOf(a)>=0)return 50;var n=0;b.split(/[\s\/._-]+/).forEach(function(w){if(w.length>2&&a.indexOf(w)>=0)n+=10});return n}
  function validPath(p){p=norm(p);if(!p)throw ToolError('Caminho vazio.');if(/(^|\/)\.\.?(\/|$)/.test(p)||/^\./.test(p.split('/').pop()))throw ToolError('Caminho inválido: '+p);if(/[:*?"<>|\u0000-\u001f]/.test(p))throw ToolError('Caminho com caracteres não permitidos: '+p);
    if(!/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i.test(p))p+='.md';return p}
  function snap(d){return d?{path:d.path,content:d.content}:null}
  function numbered(content,start,max){var lines=String(content).split('\n'),s=Math.max(1,start||1),e=Math.min(lines.length,s-1+(max||400)),w=String(e).length,out=[];for(var i=s;i<=e;i++)out.push(String(i).padStart(w,' ')+'| '+lines[i-1]);return{text:out.join('\n'),total:lines.length,end:e}}
  function upsert(ctx,data){var s=services(ctx);return s.docs.upsert(data,{source:'ai.agent',agent:ctx.agentId||null})}

  var TOOLS=[];
  function def(t){TOOLS.push(t)}

  /* ---------------- leitura ---------------- */
  def({name:'workspace_overview',access:'read',
    description:'Visão geral do vault: número de notas, pastas, notas recentes, tags mais usadas e a nota aberta no editor. Use no começo de tarefas amplas.',
    parameters:{type:'object',properties:{},additionalProperties:false},
    label:function(){return 'Olhando o vault'},
    run:function(i,ctx){var s=services(ctx),all=s.docs.list(),folders={},tags={};
      all.forEach(function(d){var f=d.path.includes('/')?d.path.slice(0,d.path.lastIndexOf('/')):'(raiz)';folders[f]=(folders[f]||0)+1;(d.tags||[]).forEach(function(t){tags[t]=(tags[t]||0)+1})});
      var recent=all.slice().sort(function(a,b){return String(b.modified||'').localeCompare(String(a.modified||''))}).slice(0,10).map(function(d){return '- '+d.path});
      var st=s.knowledge?s.knowledge.stats():{links:0},open=ctx.editor&&ctx.editor();
      return{content:['Notas: '+all.length+' · ligações: '+st.links+' · pastas: '+Object.keys(folders).length,
        'Nota aberta: '+(open&&open.path||'nenhuma'),
        '\nPastas (notas):\n'+Object.keys(folders).sort().map(function(f){return '- '+f+' ('+folders[f]+')'}).join('\n'),
        '\nRecentes:\n'+recent.join('\n'),
        '\nTags: '+(Object.keys(tags).sort(function(a,b){return tags[b]-tags[a]}).slice(0,20).map(function(t){return '#'+t+' ('+tags[t]+')'}).join(', ')||'nenhuma')].join('\n')}}});

  def({name:'list_notes',access:'read',
    description:'Lista notas de uma pasta (ou do vault inteiro), com tamanho e data. Pastas são o começo do caminho, ex.: "Projetos".',
    parameters:{type:'object',properties:{folder:{type:'string',description:'Pasta; vazio = vault inteiro'},recursive:{type:'boolean',description:'Incluir subpastas (padrão true)'},limit:{type:'integer',minimum:1,maximum:500}},additionalProperties:false},
    label:function(i){return 'Listando '+(i.folder?'“'+i.folder+'”':'o vault')},
    run:function(i,ctx){var s=services(ctx),f=norm(i.folder),rec=i.recursive!==false,lim=i.limit||150;
      var hits=s.docs.list().filter(function(d){if(!f)return rec||!d.path.includes('/');if(d.path.toLowerCase().indexOf(f.toLowerCase()+'/')!==0)return false;return rec||d.path.slice(f.length+1).indexOf('/')<0}).sort(function(a,b){return a.path.localeCompare(b.path)});
      if(!hits.length)return{content:f?'Nenhuma nota em "'+f+'".':'O vault está vazio.'};
      return{content:hits.slice(0,lim).map(function(d){return '- '+d.path+' · '+words(d.content)+' palavras'+(d.modified?' · '+d.modified:'')}).join('\n')+(hits.length>lim?'\n… e mais '+(hits.length-lim):'')}}});

  def({name:'search_notes',access:'read',
    description:'Busca notas por título e conteúdo (ignora acentos, aceita partes de palavras). Devolve caminho e trecho. Use antes de criar notas, para não duplicar.',
    parameters:{type:'object',properties:{query:{type:'string'},limit:{type:'integer',minimum:1,maximum:50}},required:['query'],additionalProperties:false},
    label:function(i){return 'Buscando “'+i.query+'”'},
    run:function(i,ctx){var s=services(ctx),q=String(i.query||'').trim();if(!q)throw ToolError('Busca vazia.');
      var qo=s.core.service('quickOpen'),hits;
      if(qo&&qo.rank)hits=qo.rank(s.docs.list(),q,i.limit||12).map(function(r){return '- '+r.doc.path+(r.snippet?'\n  …'+(r.snippet.before+r.snippet.match+r.snippet.after).replace(/\s+/g,' ').trim().slice(0,160):'')});
      else hits=s.knowledge.search(q,i.limit||12).map(function(d){return '- '+d.path});
      return{content:hits.length?hits.join('\n'):'Nada encontrado para "'+q+'".'}}});

  /* Leitura: a nota vem inteira sempre que cabe no orçamento (proporcional ao contexto do modelo).
     Antes o modelo pedia max_lines:200, recebia metade e respondia com o que tinha. */
  function readBudget(ctx){return Math.max(8000,ctx&&ctx.readChars||24000)}
  function readSlice(d,start,maxLines,budget){
    var lines=String(d.content).split('\n'),total=lines.length,s=Math.min(Math.max(1,start||1),total),rest=lines.slice(s-1).join('\n').length,e;
    e=s-1;var used=0;while(e<total&&(used+lines[e].length+8<=budget||e===s-1)){used+=lines[e].length+8;e++}
    if(rest>budget&&maxLines)e=Math.min(e,s-1+maxLines); /* nota maior que o orçamento: respeita o trecho pedido */
    var w=String(e).length,out=[];for(var i=s;i<=e;i++)out.push(String(i).padStart(w,' ')+'| '+lines[i-1]);
    var head='# '+d.path+' ('+total+' linhas'+(d.tags&&d.tags.length?' · tags: '+d.tags.map(function(t){return '#'+t}).join(' '):'')+')'+(s>1||e<total?' — linhas '+s+'–'+e:' — completa');
    var body=d.content?out.join('\n'):'(nota vazia)',partial=e<total?{path:d.path,next:e+1,total:total}:null;
    if(partial)body+='\n\n⚠ LEITURA PARCIAL: faltam as linhas '+(e+1)+'–'+total+'. Antes de responder sobre esta nota, chame read_note com path="'+d.path+'" e start_line='+(e+1)+'.';
    return{text:head+'\n'+body,partial:partial,done:!partial?d.path:null};
  }
  def({name:'read_note',access:'read',
    description:'Lê uma nota inteira, com números de linha. Notas muito longas vêm em partes: se o resultado disser LEITURA PARCIAL, continue com start_line antes de responder. Leia antes de editar.',
    parameters:{type:'object',properties:{path:{type:'string'},start_line:{type:'integer',minimum:1,description:'Linha inicial (para continuar uma leitura parcial)'},max_lines:{type:'integer',minimum:1,maximum:5000,description:'Opcional; se a nota couber inteira, ela vem inteira mesmo assim'}},required:['path'],additionalProperties:false},
    label:function(i){return 'Lendo '+i.path+(i.start_line>1?' (a partir da linha '+i.start_line+')':'')},
    run:function(i,ctx){var d=resolveDoc(ctx,i.path),r=readSlice(d,i.start_line,i.max_lines,readBudget(ctx));
      return{content:r.text,reads:[{path:d.path,partial:r.partial}]}}});

  def({name:'read_notes',access:'read',
    description:'Lê várias notas de uma vez (até 10), cada uma inteira quando cabe. Use quando já sabe quais notas precisa, em vez de várias chamadas de read_note.',
    parameters:{type:'object',properties:{paths:{type:'array',items:{type:'string'},minItems:1,maxItems:10}},required:['paths'],additionalProperties:false},
    label:function(i){var n=(i.paths||[]).length;return 'Lendo '+n+' nota'+(n===1?'':'s')},
    run:function(i,ctx){var paths=(i.paths||[]).slice(0,10),share=Math.max(4000,Math.floor(readBudget(ctx)/Math.max(1,paths.length))),out=[],reads=[];
      paths.forEach(function(p){try{var d=resolveDoc(ctx,p),r=readSlice(d,1,0,share);out.push(r.text);reads.push({path:d.path,partial:r.partial})}catch(e){out.push('# '+p+'\n(erro: '+e.message+')')}});
      return{content:out.join('\n\n━━━━━━━━\n\n'),reads:reads}}});

  def({name:'grep_notes',access:'read',
    description:'Procura um texto (ou expressão regular) dentro de todas as notas e devolve as linhas exatas com caminho e número da linha. Ignora maiúsculas e acentos no modo texto. Ótimo para achar onde algo é citado, nomes, datas e trechos para editar.',
    parameters:{type:'object',properties:{pattern:{type:'string'},regex:{type:'boolean',description:'Tratar pattern como expressão regular (padrão false)'},folder:{type:'string',description:'Limitar a uma pasta'},context:{type:'integer',minimum:0,maximum:3,description:'Linhas de contexto antes/depois (padrão 0)'},limit:{type:'integer',minimum:1,maximum:200,description:'Máximo de linhas (padrão 60)'}},required:['pattern'],additionalProperties:false},
    label:function(i){return 'Procurando “'+String(i.pattern).slice(0,50)+'” nas notas'},
    run:function(i,ctx){var s=services(ctx),pat=String(i.pattern||''),f=norm(i.folder).toLowerCase(),lim=i.limit||60,cx=i.context||0,test;
      if(!pat.trim())throw ToolError('pattern vazio.');
      if(i.regex){var re;try{re=new RegExp(pat,'i')}catch(e){throw ToolError('Expressão regular inválida: '+e.message)}test=function(l){return re.test(l)}}
      else{var q=fold(pat);test=function(l){return fold(l).indexOf(q)>=0}}
      var out=[],hits=0,files=0,more=false;
      s.docs.list().slice().sort(function(a,b){return a.path.localeCompare(b.path)}).forEach(function(d){if(more||f&&d.path.toLowerCase().indexOf(f+'/')!==0)return;
        var lines=String(d.content).split('\n'),shown=new Set(),block=[];
        lines.forEach(function(l,n){if(more||!test(l))return;if(hits>=lim){more=true;return}hits++;for(var k=Math.max(0,n-cx);k<=Math.min(lines.length-1,n+cx);k++)if(!shown.has(k)){shown.add(k);block.push('  '+(k+1)+(k===n?': ':'- ')+clip(lines[k],300))}});
        if(block.length){files++;out.push(d.path+'\n'+block.join('\n'))}});
      if(!hits)return{content:'Nenhuma linha contém "'+pat+'"'+(f?' em '+i.folder:'')+'.'};
      return{content:hits+' linha(s) em '+files+' nota(s)'+(more?' (limite atingido; refine a busca ou aumente limit)':'')+':\n\n'+out.join('\n\n')}}});

  def({name:'get_links',access:'read',
    description:'Mostra as ligações de uma nota: links [[...]] que ela faz (e quais não existem), notas que apontam para ela (backlinks) e tags.',
    parameters:{type:'object',properties:{path:{type:'string'}},required:['path'],additionalProperties:false},
    label:function(i){return 'Vendo ligações de '+i.path},
    run:function(i,ctx){var s=services(ctx),d=resolveDoc(ctx,i.path),out=s.knowledge.links(d.id).map(function(x){return x.path}),back=s.knowledge.backlinks(d.id).map(function(x){return x.path});
      var resolved=new Set(s.knowledge.links(d.id).map(function(x){return fold(x.title)})),missing=d.links.filter(function(l){return !resolved.has(fold(l.split('/').pop()))});
      return{content:'Links de '+d.path+':\n'+(out.map(function(p){return '- '+p}).join('\n')||'- nenhum')+(missing.length?'\nLinks para notas inexistentes: '+missing.map(function(l){return '[['+l+']]'}).join(', '):'')+'\n\nBacklinks:\n'+(back.map(function(p){return '- '+p}).join('\n')||'- nenhum')+'\n\nTags: '+(d.tags.map(function(t){return '#'+t}).join(' ')||'nenhuma')}}});

  def({name:'find_by_tag',access:'read',
    description:'Lista as notas que têm uma tag (#tag no texto ou tags no frontmatter).',
    parameters:{type:'object',properties:{tag:{type:'string'}},required:['tag'],additionalProperties:false},
    label:function(i){return 'Notas com #'+String(i.tag).replace(/^#/,'')},
    run:function(i,ctx){var s=services(ctx),t=String(i.tag||'').replace(/^#/,''),hits=s.knowledge.tagged(t);return{content:hits.length?hits.map(function(d){return '- '+d.path}).join('\n'):'Nenhuma nota com #'+t+'.'}}});

  def({name:'get_editor_context',access:'read',
    description:'Nota aberta no editor agora e o texto selecionado pelo usuário (se houver). Use quando o pedido falar de “esta nota”, “aqui”, “o trecho selecionado”.',
    parameters:{type:'object',properties:{},additionalProperties:false},
    label:function(){return 'Vendo o que está aberto'},
    run:function(i,ctx){var e=ctx.editor&&ctx.editor();if(!e||!e.path)return{content:'Nenhuma nota aberta no editor.'};
      return{content:'Nota aberta: '+e.path+(e.selection?'\nSeleção do usuário:\n"""\n'+clip(e.selection,4000)+'\n"""':'\nSem texto selecionado.')}}});

  /* ---------------- escrita ---------------- */
  def({name:'create_note',access:'write',
    description:'Cria uma nota nova. Falha se já existir (use edit_note ou write_note). Pastas no caminho são criadas. Use [[Título]] para ligar notas.',
    parameters:{type:'object',properties:{path:{type:'string',description:'Ex.: "Projetos/Plano.md"'},content:{type:'string'}},required:['path','content'],additionalProperties:false},
    label:function(i){return 'Criar '+norm(i.path)},
    preview:function(i,ctx){var p=validPath(i.path);return{path:p,before:null,after:String(i.content||'')}},
    run:function(i,ctx){var s=services(ctx),p=validPath(i.path);if(s.docs.get(p))throw ToolError('Já existe '+p+'. Use edit_note ou write_note.');
      var d=upsert(ctx,{path:p,content:String(i.content||''),created:today(),modified:today()});
      return{content:'Criada: '+d.path+' ('+words(d.content)+' palavras).',changes:[{id:d.id,before:null,after:snap(d)}]}}});

  function applyEdit(content,oldText,newText,all){
    if(!oldText)throw ToolError('old_text vazio. Para trocar a nota inteira use write_note; para acrescentar, append_to_note.');
    var n=content.split(oldText).length-1;
    if(!n){var l=fold(oldText).replace(/\s+/g,' ').trim(),c=fold(content).replace(/\s+/g,' ');
      throw ToolError(c.indexOf(l)>=0?'old_text existe mas com espaços/quebras de linha diferentes. Leia a nota de novo (read_note) e copie o trecho exatamente.':'old_text não aparece na nota. Leia a nota de novo (read_note) e copie o trecho exatamente.')}
    if(n>1&&!all)throw ToolError('old_text aparece '+n+' vezes. Inclua mais contexto para ficar único ou use replace_all.');
    return all?content.split(oldText).join(newText):content.replace(oldText,function(){return newText});
  }
  def({name:'edit_note',access:'write',
    description:'Edita um trecho de uma nota: troca old_text (cópia exata, única na nota) por new_text. Preserva o resto. Prefira esta ferramenta para mudanças parciais.',
    parameters:{type:'object',properties:{path:{type:'string'},old_text:{type:'string'},new_text:{type:'string'},replace_all:{type:'boolean'}},required:['path','old_text','new_text'],additionalProperties:false},
    label:function(i){return 'Editar '+norm(i.path)},
    preview:function(i,ctx){var d=resolveDoc(ctx,i.path);return{path:d.path,before:d.content,after:applyEdit(d.content,String(i.old_text),String(i.new_text),i.replace_all)}},
    run:function(i,ctx){var d=resolveDoc(ctx,i.path),next=applyEdit(d.content,String(i.old_text),String(i.new_text),i.replace_all);
      var u=upsert(ctx,{...d,id:d.id,content:next,modified:today()});return{content:'Editada: '+u.path+'.',changes:[{id:d.id,before:snap(d),after:snap(u)}]}}});

  def({name:'write_note',access:'write',
    description:'Substitui todo o conteúdo de uma nota existente. Use só para reescritas completas; para mudanças parciais use edit_note.',
    parameters:{type:'object',properties:{path:{type:'string'},content:{type:'string'}},required:['path','content'],additionalProperties:false},
    label:function(i){return 'Reescrever '+norm(i.path)},
    preview:function(i,ctx){var d=resolveDoc(ctx,i.path);return{path:d.path,before:d.content,after:String(i.content||'')}},
    run:function(i,ctx){var d=resolveDoc(ctx,i.path),u=upsert(ctx,{...d,id:d.id,content:String(i.content||''),modified:today()});return{content:'Reescrita: '+u.path+' ('+words(u.content)+' palavras).',changes:[{id:d.id,before:snap(d),after:snap(u)}]}}});

  function appendText(content,text,heading){
    text=String(text||'');if(!heading)return content.replace(/\s*$/,'')+(content.trim()?'\n\n':'')+text+'\n';
    var lines=content.split('\n'),h=fold(String(heading).replace(/^#+\s*/,'')),at=-1,level=0;
    for(var i=0;i<lines.length;i++){var m=lines[i].match(/^(#{1,6})\s+(.*)$/);if(m&&fold(m[2]).trim()===h){at=i;level=m[1].length;break}}
    if(at<0)throw ToolError('Seção "'+heading+'" não encontrada. Títulos existentes: '+(lines.filter(function(l){return /^#{1,6}\s/.test(l)}).join(' | ')||'nenhum'));
    var end=lines.length;for(var j=at+1;j<lines.length;j++){var mm=lines[j].match(/^(#{1,6})\s/);if(mm&&mm[1].length<=level){end=j;break}}
    while(end>at+1&&!lines[end-1].trim())end--;
    lines.splice(end,0,text);return lines.join('\n');
  }
  def({name:'append_to_note',access:'write',
    description:'Acrescenta texto ao fim de uma nota, ou ao fim de uma seção (heading) específica.',
    parameters:{type:'object',properties:{path:{type:'string'},content:{type:'string'},heading:{type:'string',description:'Título da seção, sem #'}},required:['path','content'],additionalProperties:false},
    label:function(i){return 'Acrescentar em '+norm(i.path)},
    preview:function(i,ctx){var d=resolveDoc(ctx,i.path);return{path:d.path,before:d.content,after:appendText(d.content,i.content,i.heading)}},
    run:function(i,ctx){var d=resolveDoc(ctx,i.path),u=upsert(ctx,{...d,id:d.id,content:appendText(d.content,i.content,i.heading),modified:today()});return{content:'Texto acrescentado em '+u.path+'.',changes:[{id:d.id,before:snap(d),after:snap(u)}]}}});

  def({name:'rename_note',access:'write',
    description:'Renomeia ou move uma nota (novo caminho completo). Com update_links=true (padrão), atualiza os [[links]] das outras notas.',
    parameters:{type:'object',properties:{path:{type:'string'},new_path:{type:'string'},update_links:{type:'boolean'}},required:['path','new_path'],additionalProperties:false},
    label:function(i){return 'Mover '+norm(i.path)+' → '+norm(i.new_path)},
    preview:function(i,ctx){var d=resolveDoc(ctx,i.path);return{path:d.path+' → '+validPath(i.new_path),before:null,after:null,note:'Renomear/mover'+(i.update_links===false?'':' e atualizar links nas outras notas')}},
    run:function(i,ctx){var s=services(ctx),d=resolveDoc(ctx,i.path),np=validPath(i.new_path);
      if(np.toLowerCase()!==d.path.toLowerCase()&&s.docs.get(np))throw ToolError('Já existe '+np+'.');
      var title=np.split('/').pop().replace(/\.(md|markdown)$/i,''),changes=[],u=upsert(ctx,{...d,id:d.id,path:np,title:title,modified:today()});
      changes.push({id:d.id,before:snap(d),after:snap(u)});
      if(i.update_links!==false&&fold(title)!==fold(d.title)){
        var re=new RegExp('\\[\\[('+d.title.replace(/[.*+?^${}()|[\]\\]/g,'\\$&')+')(\\|[^\\]]*)?(#[^\\]]*)?\\]\\]','gi');
        s.docs.list().forEach(function(o){if(o.id===d.id)return;re.lastIndex=0;if(!re.test(o.content))return;re.lastIndex=0;
          var c=o.content.replace(re,function(m,a,alias,sec){return '[['+title+(sec||'')+(alias||'')+']]'});var uo=upsert(ctx,{...o,id:o.id,content:c});changes.push({id:o.id,before:snap(o),after:snap(uo)})});
      }
      return{content:'Movida para '+np+(changes.length>1?'; links atualizados em '+(changes.length-1)+' nota(s)':'')+'.',changes:changes}}});

  def({name:'delete_note',access:'destructive',
    description:'Move uma nota para a lixeira (pode ser restaurada). Só use quando o usuário pedir claramente.',
    parameters:{type:'object',properties:{path:{type:'string'}},required:['path'],additionalProperties:false},
    label:function(i){return 'Excluir '+norm(i.path)},
    preview:function(i,ctx){var d=resolveDoc(ctx,i.path);return{path:d.path,before:d.content,after:null,note:'Mover para a lixeira'}},
    run:function(i,ctx){var s=services(ctx),d=resolveDoc(ctx,i.path);s.trash.trash(d.id,{source:'ai.agent'});return{content:'Movida para a lixeira: '+d.path+'.',changes:[{id:d.id,before:snap(d),after:null,trashed:true}]}}});

  def({name:'create_folder',access:'write',
    description:'Cria uma pasta (que vira um bairro na cidade).',
    parameters:{type:'object',properties:{path:{type:'string'}},required:['path'],additionalProperties:false},
    label:function(i){return 'Criar pasta '+norm(i.path)},
    preview:function(i){return{path:norm(i.path)+'/',before:null,after:null,note:'Nova pasta'}},
    run:async function(i,ctx){var s=services(ctx),p=norm(i.path);if(!p||/(^|\/)\./.test(p))throw ToolError('Pasta inválida.');
      if(s.persistence&&s.persistence.adapter&&s.persistence.vault&&s.persistence.adapter.createFolder)await s.persistence.adapter.createFolder(s.persistence.vault,p);
      if(s.explorer&&s.explorer.addFolder)s.explorer.addFolder(p);return{content:'Pasta criada: '+p+'.'}}});

  /* ---------------- interface e memória ---------------- */
  def({name:'open_note',access:'ui',
    description:'Abre uma nota no editor para o usuário ver o resultado.',
    parameters:{type:'object',properties:{path:{type:'string'}},required:['path'],additionalProperties:false},
    label:function(i){return 'Abrir '+i.path},
    run:function(i,ctx){var s=services(ctx),d=resolveDoc(ctx,i.path);if(ctx.openNote)ctx.openNote(d.id);else s.core.commands.execute('document.open',{id:d.id,source:'ai.agent'});return{content:'Aberta: '+d.path}}});

  def({name:'update_plan',access:'plan',
    description:'Mostra ao usuário o seu plano como uma lista de passos com status. Use em tarefas com 3 ou mais passos: crie o plano no começo e atualize a cada passo concluído (envie a lista inteira).',
    parameters:{type:'object',properties:{steps:{type:'array',maxItems:20,items:{type:'object',properties:{step:{type:'string'},status:{type:'string',enum:['pending','in_progress','done']}},required:['step','status']}}},required:['steps'],additionalProperties:false},
    label:function(i){var st=i.steps||[],d=st.filter(function(x){return x&&x.status==='done'}).length;return 'Plano: '+d+'/'+st.length+' passos'},
    run:function(i,ctx){var st=(i.steps||[]).filter(function(x){return x&&String(x.step||'').trim()}).slice(0,20).map(function(x){return{step:String(x.step).trim().slice(0,200),status:['pending','in_progress','done'].indexOf(x.status)>=0?x.status:'pending'}});
      if(!st.length)throw ToolError('Envie ao menos um passo.');if(ctx.setPlan)ctx.setPlan(st);
      var d=st.filter(function(x){return x.status==='done'}).length,next=st.find(function(x){return x.status!=='done'});
      return{content:'Plano atualizado: '+d+'/'+st.length+' concluídos.'+(next?' Próximo: '+next.step:' Todos os passos concluídos; responda ao usuário.'),plan:st}}});

  def({name:'remember',access:'memory',
    description:'Guarda um fato durável sobre o usuário ou o vault (preferências, convenções, objetivos) na memória do Assistente, usada em conversas futuras. Não guarde conteúdo de notas.',
    parameters:{type:'object',properties:{fact:{type:'string'}},required:['fact'],additionalProperties:false},
    label:function(i){return 'Memorizar: '+String(i.fact).slice(0,60)},
    run:function(i,ctx){var f=String(i.fact||'').trim();if(!f)throw ToolError('Fato vazio.');if(!ctx.remember)throw ToolError('Memória indisponível.');ctx.remember(f);return{content:'Memorizado.'}}});

  function today(){return new Date().toISOString().slice(0,10)}

  /* ---------- validação leve de argumentos contra o schema ---------- */
  function validate(tool,input){
    var sch=tool.parameters||{},props=sch.properties||{},errs=[];
    if(input==null||typeof input!=='object'||Array.isArray(input))return['os argumentos precisam ser um objeto JSON'];
    if(input.__invalid!=null)return['JSON dos argumentos inválido ou truncado'];
    (sch.required||[]).forEach(function(k){if(input[k]==null||input[k]==='')errs.push('falta "'+k+'"')});
    Object.keys(input).forEach(function(k){var p=props[k];if(!p){if(sch.additionalProperties===false)errs.push('parâmetro desconhecido "'+k+'"');return}
      var v=input[k],t=p.type;
      if(t==='string'&&typeof v!=='string'){if(typeof v==='number')input[k]=String(v);else errs.push('"'+k+'" deve ser texto')}
      if(t==='integer'){var n=Number(v);if(!Number.isInteger(n))errs.push('"'+k+'" deve ser inteiro');else{input[k]=n;if(p.minimum!=null&&n<p.minimum)input[k]=p.minimum;if(p.maximum!=null&&n>p.maximum)input[k]=p.maximum}}
      if(t==='array'&&!Array.isArray(v)){if(typeof v==='string'){try{var pv=JSON.parse(v);if(Array.isArray(pv))input[k]=v=pv}catch(_){input[k]=v=[v]}}if(!Array.isArray(input[k]))errs.push('"'+k+'" deve ser uma lista')}
      if(t==='array'&&Array.isArray(input[k])&&p.items&&p.items.type==='string')input[k]=input[k].map(String);
      if(t==='boolean'&&typeof v!=='boolean'){if(v==='true'||v==='false')input[k]=v==='true';else errs.push('"'+k+'" deve ser true/false')}});
    return errs;
  }

  /* ---------- desfazer: aplica os “antes” em ordem inversa ---------- */
  function revert(ctx,changes){
    var s=services(ctx),report={restored:0,skipped:[]};
    changes.slice().reverse().forEach(function(c){
      var cur=s.docs.get(c.id);
      if(c.after&&cur&&cur.content!==c.after.content){report.skipped.push((cur&&cur.path)||c.after.path);return} /* o usuário mexeu depois */
      if(c.trashed){try{s.trash.restore(c.id);report.restored++}catch(e){report.skipped.push(c.before.path)}return}
      if(!c.before){if(cur){s.trash.trash(cur.id,{source:'ai.undo'});report.restored++}return}
      if(cur){s.docs.upsert({...cur,id:cur.id,path:c.before.path,title:c.before.path.split('/').pop().replace(/\.(md|markdown)$/i,''),content:c.before.content},{source:'ai.undo'});report.restored++}
    });
    return report;
  }

  function list(filter){return TOOLS.filter(function(t){return !filter||filter(t)})}
  function get(name){return TOOLS.find(function(t){return t.name===name})||null}
  function schema(t){return{name:t.name,description:t.description,parameters:t.parameters}}
  global.UrbeAITools={list:list,get:get,schema:schema,validate:validate,revert:revert,register:def,_applyEdit:applyEdit,_appendText:appendText};
})(typeof window!=='undefined'?window:globalThis);
