(function(global){
  'use strict';
  /* Motor de páginas do Urbe.
     Uma página é um JSON (arquivo .page.json no vault) com meta, tema, layout e
     uma lista de seções. Este arquivo é puro (sem DOM): valida/normaliza o JSON,
     descreve os blocos (o estúdio gera os formulários a partir daqui e o
     Assistente recebe o schema em texto) e renderiza um HTML completo, bonito e
     independente — o mesmo HTML da prévia é o que se exporta. */
  var VERSION=1;

  /* ---------------- utilidades ---------------- */
  function esc(v){return String(v==null?'':v).replace(/[&<>"']/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]})}
  function safeUrl(u,image){
    u=String(u==null?'':u).trim();if(!u||/[\u0000-\u001f]/.test(u))return '';
    if(/^data:/i.test(u))return image&&/^data:image\/(png|jpe?g|gif|webp|svg\+xml|avif);base64,[a-z0-9+/=\s]+$/i.test(u)?u:'';
    if(/^[a-z][\w+.-]*:/i.test(u)&&!/^(https?:|mailto:|tel:)/i.test(u))return '';
    return u;
  }
  function slug(s){return String(s||'').normalize('NFD').replace(/[̀-ͯ]/g,'').toLowerCase().replace(/[^\p{L}\p{N}]+/gu,'-').replace(/^-+|-+$/g,'').slice(0,60)||'secao'}
  function clamp(n,a,b){return Math.max(a,Math.min(b,n))}
  var seq=0;function uid(p){seq=(seq+1)%1e6;return(p||'s')+'_'+Date.now().toString(36).slice(-5)+seq.toString(36)+Math.random().toString(36).slice(2,5)}
  function clone(x){return JSON.parse(JSON.stringify(x))}
  function isObj(x){return x!=null&&typeof x==='object'&&!Array.isArray(x)}
  var COLOR_RE=/^(#[0-9a-f]{3,8}|(rgb|hsl)a?\([\d\s.,%/+-]+\)|transparent|currentColor|[a-z]{3,20})$/i;

  /* ---------------- markdown (GFM essencial + wikilinks + callouts) ---------------- */
  function inline(s,o){
    var keep=[];function hold(h){keep.push(h);return '\u0000'+(keep.length-1)+'\u0001'}
    s=String(s);
    s=s.replace(/`([^`\n]+)`/g,function(_,c){return hold('<code>'+esc(c)+'</code>')});
    s=s.replace(/!\[\[([^\]]+)\]\]/g,function(_,t){return hold(o.wikilink?o.wikilink(t.split('|')[0].trim(),t.split('|')[1]):esc(t))});
    s=s.replace(/!\[([^\]]*)\]\(([^)\s]+)(?:\s+"([^"]*)")?\)/g,function(_,alt,src,title){var u=safeUrl(src,true);return u?hold('<img src="'+esc(u)+'" alt="'+esc(alt)+'"'+(title?' title="'+esc(title)+'"':'')+' loading="lazy">'):''});
    s=s.replace(/\[\[([^\]|#]+)(?:#[^\]|]*)?(?:\|([^\]]+))?\]\]/g,function(_,t,label){return hold(o.wikilink?o.wikilink(t.trim(),label&&label.trim()):esc(label||t))});
    s=s.replace(/\[([^\]]+)\]\(([^)\s]+)(?:\s+"([^"]*)")?\)/g,function(_,label,href){var u=safeUrl(href);return hold(u?'<a href="'+esc(u)+'"'+(/^https?:/i.test(u)?' target="_blank" rel="noopener"':'')+'>'+inline(label,o)+'</a>':esc(label))});
    s=s.replace(/<(https?:\/\/[^>\s]+)>/g,function(_,u){return hold('<a href="'+esc(u)+'" target="_blank" rel="noopener">'+esc(u)+'</a>')});
    s=esc(s);
    s=s.replace(/\*\*([^*]+)\*\*/g,'<strong>$1</strong>').replace(/__([^_]+)__/g,'<strong>$1</strong>')
      .replace(/(^|[^*])\*([^*\s][^*]*)\*/g,'$1<em>$2</em>').replace(/(^|[\s(])_([^_\s][^_]*)_(?=[\s).,!?:;]|$)/g,'$1<em>$2</em>')
      .replace(/~~([^~]+)~~/g,'<del>$1</del>').replace(/==([^=]+)==/g,'<mark>$1</mark>');
    return s.replace(/\u0000(\d+)\u0001/g,function(_,i){return keep[+i]});
  }
  function markdown(src,o){
    o=o||{};var lines=String(src==null?'':src).replace(/\r\n?/g,'\n').split('\n'),out=[],i=0;
    if(lines[0]==='---'){var fm=lines.indexOf('---',1);if(fm>0)i=fm+1}
    var headings=o.headings;
    function para(buf){if(!buf.length)return;out.push('<p>'+buf.map(function(l,k){return inline(l.replace(/\s+$/,''),o)+(k<buf.length-1&&/ {2,}$/.test(l)?'<br>':'')}).join(' ')+'</p>')}
    var buf=[];
    function flush(){para(buf);buf=[]}
    while(i<lines.length){
      var line=lines[i],m;
      if(!line.trim()){flush();i++;continue}
      if((m=line.match(/^(\s*)(`{3,}|~{3,})\s*([\w+-]*)/))){flush();var fence=m[2],lang=m[3],code=[];i++;while(i<lines.length&&lines[i].trim().indexOf(fence)!==0)code.push(lines[i++]);i++;
        out.push('<pre class="code"'+(lang?' data-lang="'+esc(lang)+'"':'')+'><code>'+esc(code.join('\n'))+'</code></pre>');continue}
      if((m=line.match(/^(#{1,6})\s+(.+?)\s*#*\s*$/))){flush();var lvl=m[1].length+(o.shift||0),id=slug(m[2]);lvl=clamp(lvl,1,6);if(headings)headings.push({level:lvl,text:m[2],id:id});
        out.push('<h'+lvl+' id="'+esc((o.idPrefix||'')+id)+'">'+inline(m[2],o)+'</h'+lvl+'>');i++;continue}
      if(/^\s{0,3}([-*_])(\s*\1){2,}\s*$/.test(line)){flush();out.push('<hr>');i++;continue}
      if(/^\s*>/.test(line)){flush();var q=[];while(i<lines.length&&/^\s*>/.test(lines[i]))q.push(lines[i++].replace(/^\s*>\s?/,''));
        var call=q[0]&&q[0].match(/^\[!(\w+)\][+-]?\s*(.*)$/);
        if(call){var kind=call[1].toLowerCase();out.push('<aside class="callout callout-'+esc(kind)+'"><strong class="callout-title">'+inline(call[2]||call[1],o)+'</strong>'+markdown(q.slice(1).join('\n'),o)+'</aside>')}
        else out.push('<blockquote>'+markdown(q.join('\n'),o)+'</blockquote>');continue}
      if(/^\s*\|.*\|\s*$/.test(line)&&i+1<lines.length&&/^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$/.test(lines[i+1])){flush();
        var cells=function(l){return l.trim().replace(/^\||\|$/g,'').split('|').map(function(c){return c.trim()})};
        var head=cells(line),aligns=cells(lines[i+1]).map(function(c){return /^:-+:$/.test(c)?'center':/-:$/.test(c)?'right':''});i+=2;var rows=[];
        while(i<lines.length&&/^\s*\|.*\|\s*$/.test(lines[i]))rows.push(cells(lines[i++]));
        var al=function(k){return aligns[k]?' style="text-align:'+aligns[k]+'"':''};
        out.push('<div class="table"><table><thead><tr>'+head.map(function(c,k){return '<th'+al(k)+'>'+inline(c,o)+'</th>'}).join('')+'</tr></thead><tbody>'+rows.map(function(r){return '<tr>'+head.map(function(_,k){return '<td'+al(k)+'>'+inline(r[k]||'',o)+'</td>'}).join('')+'</tr>'}).join('')+'</tbody></table></div>');continue}
      if(/^\s*([-*+]|\d+[.)])\s+/.test(line)){flush();var items=[];while(i<lines.length&&(/^\s*([-*+]|\d+[.)])\s+/.test(lines[i])||(/^\s{2,}\S/.test(lines[i])&&items.length))){items.push(lines[i]);i++}
        out.push(list(items,o));continue}
      buf.push(line);i++;
    }
    flush();return out.join('\n');
  }
  function list(lines,o){
    function ind(l){return l.match(/^\s*/)[0].replace(/\t/g,'  ').length}
    var base=ind(lines[0]),ordered=/^\s*\d+[.)]/.test(lines[0]),items=[],cur=null;
    lines.forEach(function(l){var d=ind(l),m=l.match(/^\s*([-*+]|\d+[.)])\s+(.*)$/);if(m&&d<=base+1){cur={text:m[2],sub:[]};items.push(cur)}else if(cur)cur.sub.push(l)});
    return '<'+(ordered?'ol':'ul')+'>'+items.map(function(it){var t=it.text,task=t.match(/^\[([ xX])\]\s+(.*)$/),sub=it.sub.filter(function(s){return s.trim()}),inner;
      inner=task?'<span class="task'+(task[1]!==' '?' done':'')+'"><input type="checkbox" disabled'+(task[1]!==' '?' checked':'')+'> '+inline(task[2],o)+'</span>':inline(t,o);
      if(sub.length){if(/^\s*([-*+]|\d+[.)])\s+/.test(sub[0]))inner+=list(sub,o);else inner+='<br>'+sub.map(function(s){return inline(s.trim(),o)}).join('<br>')}
      return '<li'+(task?' class="task-item"':'')+'>'+inner+'</li>'}).join('')+'</'+(ordered?'ol':'ul')+'>';
  }
  function plain(md){return String(md||'').replace(/^---[\s\S]*?\n---\n?/,'').replace(/^\s*#\s+.*\n?/,'').replace(/\[![\w-]+\][+-]?/g,'').replace(/^\s*[-*+]\s+\[[ xX]\]\s+/gm,'').replace(/```[\s\S]*?```/g,' ').replace(/!\[[^\]]*\]\([^)]*\)/g,'').replace(/\[\[([^\]|]+)(?:\|([^\]]+))?\]\]/g,function(_,a,b){return b||a}).replace(/\[([^\]]+)\]\([^)]*\)/g,'$1').replace(/^#+\s+/gm,'').replace(/[*_`>~=#|-]+/g,' ').replace(/\s+/g,' ').trim()}
  function excerpt(md,n){var t=plain(md);return t.length>n?t.slice(0,n).replace(/\s+\S*$/,'')+'…':t}

  /* ---------------- tipografia e temas ---------------- */
  var FONTS={
    inter:{label:'Inter',stack:"'Inter',system-ui,sans-serif",g:'Inter:wght@400;500;600;700;800'},
    manrope:{label:'Manrope',stack:"'Manrope',system-ui,sans-serif",g:'Manrope:wght@400;500;600;700;800'},
    grotesk:{label:'Space Grotesk',stack:"'Space Grotesk',system-ui,sans-serif",g:'Space+Grotesk:wght@400;500;600;700'},
    dmsans:{label:'DM Sans',stack:"'DM Sans',system-ui,sans-serif",g:'DM+Sans:wght@400;500;700'},
    outfit:{label:'Outfit',stack:"'Outfit',system-ui,sans-serif",g:'Outfit:wght@400;500;600;700;800'},
    playfair:{label:'Playfair Display',stack:"'Playfair Display',Georgia,serif",g:'Playfair+Display:wght@500;600;700;800'},
    fraunces:{label:'Fraunces',stack:"'Fraunces',Georgia,serif",g:'Fraunces:wght@400;600;700;800'},
    lora:{label:'Lora',stack:"'Lora',Georgia,serif",g:'Lora:wght@400;500;600;700'},
    merriweather:{label:'Merriweather',stack:"'Merriweather',Georgia,serif",g:'Merriweather:wght@400;700'},
    mono:{label:'JetBrains Mono',stack:"'JetBrains Mono',ui-monospace,monospace",g:'JetBrains+Mono:wght@400;600;700'},
    system:{label:'Do sistema',stack:"system-ui,-apple-system,'Segoe UI',Roboto,sans-serif",g:''},
    serif:{label:'Serifa do sistema',stack:"Georgia,'Times New Roman',serif",g:''}
  };
  function pal(bg,surface,text,muted,primary,accent,border,ink){return{bg:bg,surface:surface,text:text,muted:muted,primary:primary,accent:accent,border:border,onPrimary:ink||'#ffffff'}}
  var THEMES={
    aurora:{label:'Aurora',mode:'dark',fonts:{heading:'outfit',body:'inter'},radius:18,background:'mesh',
      dark:pal('#0a0e1a','#121a2e','#e9edf8','#9aa5c4','#7c9cff','#c084fc','rgba(148,163,212,.16)','#0a0e1a'),light:pal('#f6f7fd','#ffffff','#141a2e','#5b6380','#4f6bff','#a855f7','rgba(20,26,46,.1)')},
    papel:{label:'Papel',mode:'light',fonts:{heading:'playfair',body:'lora'},radius:6,background:'plain',
      light:pal('#fbf8f3','#ffffff','#1f1b16','#6b6257','#b4532a','#2f6f5e','rgba(31,27,22,.12)'),dark:pal('#1a1714','#231f1b','#f1ebe2','#b3a898','#e0845a','#6fbfa7','rgba(241,235,226,.12)','#1a1714')},
    grafite:{label:'Grafite',mode:'dark',fonts:{heading:'inter',body:'inter'},radius:10,background:'plain',
      dark:pal('#0c0c0e','#16161a','#f4f4f5','#8e8e97','#fafafa','#a1a1aa','rgba(255,255,255,.1)','#0c0c0e'),light:pal('#fafafa','#ffffff','#111113','#6b6b73','#111113','#52525b','rgba(0,0,0,.1)')},
    oceano:{label:'Oceano',mode:'light',fonts:{heading:'manrope',body:'manrope'},radius:16,background:'gradient',
      light:pal('#f2f8fb','#ffffff','#0b2233','#4e6878','#0077b6','#00b4d8','rgba(11,34,51,.1)'),dark:pal('#07161f','#0d2230','#e3f2f9','#8fb0c2','#38bdf8','#22d3ee','rgba(227,242,249,.12)','#07161f')},
    floresta:{label:'Floresta',mode:'dark',fonts:{heading:'fraunces',body:'dmsans'},radius:14,background:'mesh',
      dark:pal('#0b1310','#121e19','#e7f2ec','#94ab9f','#4ade80','#facc15','rgba(231,242,236,.12)','#0b1310'),light:pal('#f4f8f5','#ffffff','#10231a','#51685c','#15803d','#ca8a04','rgba(16,35,26,.1)')},
    entardecer:{label:'Entardecer',mode:'light',fonts:{heading:'grotesk',body:'dmsans'},radius:20,background:'gradient',
      light:pal('#fff7ef','#ffffff','#2a1409','#7a5a48','#ea580c','#db2777','rgba(42,20,9,.1)'),dark:pal('#1a0f0a','#261711','#fbe9dd','#c4a594','#fb923c','#f472b6','rgba(251,233,221,.12)','#1a0f0a')},
    neon:{label:'Neon',mode:'dark',fonts:{heading:'grotesk',body:'inter'},radius:12,background:'dots',
      dark:pal('#06060b','#0f0f19','#eef0ff','#8d90b3','#22d3ee','#f472b6','rgba(34,211,238,.18)','#06060b'),light:pal('#f7f7ff','#ffffff','#10102a','#5c5f87','#0891b2','#db2777','rgba(16,16,42,.1)')},
    lavanda:{label:'Lavanda',mode:'light',fonts:{heading:'manrope',body:'inter'},radius:18,background:'mesh',
      light:pal('#faf7ff','#ffffff','#1c1433','#6a5f86','#7c3aed','#0ea5e9','rgba(28,20,51,.1)'),dark:pal('#110c1d','#1a132b','#efe9ff','#a89bc7','#a78bfa','#38bdf8','rgba(239,233,255,.12)','#110c1d')}
  };

  /* ---------------- campos (o estúdio gera formulários a partir deles) ---------------- */
  function F(key,label,type,extra){var f={key:key,label:label,type:type};if(extra)for(var k in extra)f[k]=extra[k];return f}
  var BUTTONS=F('buttons','Botões','list',{itemLabel:'label',max:4,fields:[F('label','Texto','text',{default:'Saiba mais'}),F('url','Link','url',{default:'#'}),F('variant','Estilo','select',{options:['primary','secondary','ghost'],labels:['Principal','Secundário','Discreto'],default:'primary'})]});
  var TITLE=F('title','Título','text'),SUB=F('subtitle','Subtítulo','textarea'),COLS=function(d){return F('columns','Colunas','select',{options:[1,2,3,4],default:d||3})};

  /* ---------------- blocos ---------------- */
  var BLOCKS={};
  function block(type,def){def.type=type;BLOCKS[type]=def}
  function head(p){return (p.title||p.subtitle)?'<div class="sec-head">'+(p.title?'<h2>'+inline(p.title,{})+'</h2>':'')+(p.subtitle?'<p class="sub">'+inline(p.subtitle,{})+'</p>':'')+'</div>':''}
  function btns(list){list=(list||[]).filter(function(b){return b&&b.label});if(!list.length)return '';return '<div class="btns">'+list.map(function(b){var u=safeUrl(b.url)||'#';return '<a class="btn btn-'+esc(b.variant||'primary')+'" href="'+esc(u)+'"'+(/^https?:/i.test(u)?' target="_blank" rel="noopener"':'')+'>'+esc(b.label)+'</a>'}).join('')+'</div>'}
  function img(src,alt,cls){var u=safeUrl(src,true);return u?'<img'+(cls?' class="'+cls+'"':'')+' src="'+esc(u)+'" alt="'+esc(alt||'')+'" loading="lazy">':''}
  function grid(n,items,fn,cls){return '<div class="grid g'+clamp(+n||3,1,4)+(cls?' '+cls:'')+'">'+items.map(fn).join('')+'</div>'}

  block('hero',{label:'Capa',icon:'✦',group:'Estrutura',description:'Abertura com título grande, texto, botões e imagem opcional.',
    fields:[F('eyebrow','Chamada acima do título','text'),F('title','Título','text',{default:'Um título que diz tudo'}),F('subtitle','Texto','markdown',{default:'Uma frase curta que explica o que é, para quem é e por que importa.'}),BUTTONS,F('image','Imagem','image'),
      F('layout','Disposição','select',{options:['center','left','split'],labels:['Centralizada','À esquerda','Texto + imagem'],default:'center'}),F('height','Altura','select',{options:['auto','tall','screen'],labels:['Natural','Alta','Tela cheia'],default:'tall'}),F('stack','Botões empilhados (estilo lista de links)','boolean',{default:false})],
    render:function(p,c){return '<div class="hero l-'+esc(p.layout)+' h-'+esc(p.height)+(p.stack?' stack-btns':'')+'"><div class="hero-text">'+(p.eyebrow?'<p class="eyebrow">'+inline(p.eyebrow,c.md)+'</p>':'')+'<h1>'+inline(p.title,c.md)+'</h1>'+(p.subtitle?'<div class="lead">'+markdown(p.subtitle,c.md)+'</div>':'')+btns(p.buttons)+'</div>'+(p.image?'<figure class="hero-media">'+img(p.image,p.title)+'</figure>':'')+'</div>'}});

  block('text',{label:'Texto',icon:'¶',group:'Conteúdo',description:'Texto livre em Markdown (títulos, listas, tabelas, citações, código, [[links]]).',
    fields:[TITLE,F('markdown','Conteúdo','markdown',{default:'Escreva aqui em **Markdown**.'}),F('columns','Colunas de texto','select',{options:[1,2],default:1})],
    render:function(p,c){return head(p)+'<div class="prose'+(p.columns==2?' cols2':'')+'">'+markdown(p.markdown,c.md)+'</div>'}});

  block('note',{label:'Nota',icon:'▤',group:'Notas',description:'Mostra o conteúdo de uma nota do vault; atualiza sozinho quando a nota muda.',
    fields:[F('path','Nota','note',{required:true}),F('showTitle','Mostrar título','boolean',{default:true}),F('showMeta','Mostrar data e tags','boolean',{default:false})],
    render:function(p,c){var d=c.note(p.path);if(!d)return '<div class="missing">Nota não encontrada: '+esc(p.path||'(escolha uma nota)')+'</div>';
      return '<article class="prose note-article">'+(p.showTitle?'<h1 class="note-title">'+esc(d.title)+'</h1>':'')+(p.showMeta?'<p class="meta">'+esc([d.modified,(d.tags||[]).map(function(t){return '#'+t}).join(' ')].filter(Boolean).join(' · '))+'</p>':'')+markdown(stripTitle(d.content,d.title,p.showTitle),c.mdNote(d))+'</article>'}});

  block('notes',{label:'Coleção de notas',icon:'▦',group:'Notas',description:'Cartões ou lista de várias notas (de uma pasta, uma tag ou escolhidas), com resumo; pode incluir o texto completo de cada uma logo abaixo.',
    fields:[TITLE,SUB,F('source','Origem','select',{options:['folder','tag','list','recent'],labels:['Pasta','Tag','Escolhidas','Recentes'],default:'folder'}),F('folder','Pasta','folder'),F('tag','Tag','tag'),F('paths','Notas (uma por linha)','textarea'),
      F('layout','Aparência','select',{options:['cards','list','grid'],labels:['Cartões','Lista','Grade compacta'],default:'cards'}),COLS(3),F('limit','Máximo','number',{min:1,max:200,default:24}),
      F('sort','Ordem','select',{options:['title','modified','path'],labels:['Título','Mais recentes','Caminho'],default:'title'}),F('excerpt','Mostrar resumo','boolean',{default:true}),F('expand','Incluir o texto completo abaixo','boolean',{default:false})],
    render:function(p,c){var list=c.notes(p);if(!list.length)return head(p)+'<div class="missing">Nenhuma nota encontrada para esta coleção.</div>';
      var cards=list.map(function(d){var href=p.expand?'#'+c.noteAnchor(d):'';return '<a class="card note-card"'+(href?' href="'+esc(href)+'"':'')+'><h3>'+esc(d.title)+'</h3>'+(p.excerpt?'<p>'+esc(excerpt(d.content,p.layout==='list'?220:140))+'</p>':'')+((d.tags||[]).length?'<div class="tags">'+d.tags.slice(0,4).map(function(t){return '<span>#'+esc(t)+'</span>'}).join('')+'</div>':'')+'</a>'});
      var body=p.layout==='list'?'<div class="stack">'+cards.join('')+'</div>':grid(p.layout==='grid'?Math.min(4,(+p.columns||3)+1):p.columns,cards,function(x){return x},p.layout==='grid'?'compact':'');
      var full=p.expand?'<div class="note-full">'+list.map(function(d){return '<article class="prose note-article" id="'+esc(c.noteAnchor(d))+'"><h2 class="note-title">'+esc(d.title)+'</h2>'+markdown(stripTitle(d.content,d.title,true),c.mdNote(d,1))+'</article>'}).join('')+'</div>':'';
      return head(p)+body+full}});

  block('features',{label:'Destaques',icon:'◇',group:'Conteúdo',description:'Grade de benefícios/recursos com ícone (emoji), título e texto.',
    fields:[TITLE,SUB,COLS(3),F('items','Itens','list',{itemLabel:'title',fields:[F('icon','Ícone (emoji)','text',{default:'✨'}),F('title','Título','text',{default:'Recurso'}),F('text','Texto','textarea',{default:'Uma frase sobre este recurso.'})],default:[{icon:'⚡',title:'Rápido',text:'Tudo acontece na hora.'},{icon:'🔒',title:'Seguro',text:'Seus dados ficam com você.'},{icon:'🎨',title:'Bonito',text:'Visual moderno em qualquer tela.'}]})],
    render:function(p,c){return head(p)+grid(p.columns,p.items,function(it){return '<div class="card feature">'+(it.icon?'<div class="ficon">'+esc(it.icon)+'</div>':'')+'<h3>'+inline(it.title,c.md)+'</h3><p>'+inline(it.text,c.md)+'</p></div>'})}});

  block('cards',{label:'Cartões',icon:'▭',group:'Conteúdo',description:'Cartões com imagem, título, texto, etiqueta e link (projetos, posts, produtos).',
    fields:[TITLE,SUB,COLS(3),F('items','Cartões','list',{itemLabel:'title',fields:[F('image','Imagem','image'),F('tag','Etiqueta','text'),F('title','Título','text',{default:'Título do cartão'}),F('text','Texto','textarea'),F('url','Link','url')],default:[{title:'Projeto um',text:'Descrição curta.',tag:'Design'},{title:'Projeto dois',text:'Descrição curta.',tag:'Código'},{title:'Projeto três',text:'Descrição curta.',tag:'Escrita'}]})],
    render:function(p,c){return head(p)+grid(p.columns,p.items,function(it){var u=safeUrl(it.url),tag=u?'a':'div';return '<'+tag+' class="card media-card"'+(u?' href="'+esc(u)+'"'+(/^https?:/i.test(u)?' target="_blank" rel="noopener"':''):'')+'>'+(it.image?'<div class="card-img">'+img(it.image,it.title)+'</div>':'')+'<div class="card-body">'+(it.tag?'<span class="pill">'+esc(it.tag)+'</span>':'')+'<h3>'+inline(it.title,c.md)+'</h3>'+(it.text?'<p>'+inline(it.text,c.md)+'</p>':'')+'</div></'+tag+'>'})}});

  block('gallery',{label:'Galeria',icon:'▣',group:'Mídia',description:'Grade de imagens com legenda; toque abre a imagem ampliada.',
    fields:[TITLE,COLS(3),F('items','Imagens','list',{itemLabel:'caption',fields:[F('image','Imagem','image'),F('caption','Legenda','text')]})],
    render:function(p){var items=p.items.filter(function(i){return safeUrl(i.image,true)});if(!items.length)return head(p)+'<div class="missing">Adicione imagens à galeria.</div>';
      return head(p)+grid(p.columns,items,function(it){var u=safeUrl(it.image,true);return '<figure class="shot"><a href="'+esc(u)+'" target="_blank" rel="noopener" data-lightbox>'+img(u,it.caption)+'</a>'+(it.caption?'<figcaption>'+esc(it.caption)+'</figcaption>':'')+'</figure>'},'gallery')}});

  block('image',{label:'Imagem',icon:'◐',group:'Mídia',description:'Uma imagem com legenda.',
    fields:[F('src','Imagem','image',{required:true}),F('alt','Descrição (acessibilidade)','text'),F('caption','Legenda','text'),F('size','Tamanho','select',{options:['normal','wide','full'],labels:['Normal','Larga','Tela inteira'],default:'wide'}),F('rounded','Cantos arredondados','boolean',{default:true})],
    render:function(p){return p.src&&safeUrl(p.src,true)?'<figure class="figure s-'+esc(p.size)+(p.rounded?' rounded':'')+'">'+img(p.src,p.alt||p.caption)+(p.caption?'<figcaption>'+esc(p.caption)+'</figcaption>':'')+'</figure>':'<div class="missing">Escolha uma imagem.</div>'}});

  block('video',{label:'Vídeo',icon:'▶',group:'Mídia',description:'Vídeo do YouTube, Vimeo ou arquivo .mp4/.webm.',
    fields:[F('url','Link do vídeo','url',{required:true}),F('caption','Legenda','text')],
    render:function(p){var u=safeUrl(p.url),m,src='';if(!u)return '<div class="missing">Informe o link do vídeo.</div>';
      if((m=u.match(/(?:youtube\.com\/(?:watch\?v=|embed\/|shorts\/)|youtu\.be\/)([\w-]{6,})/)))src='https://www.youtube-nocookie.com/embed/'+m[1];
      else if((m=u.match(/vimeo\.com\/(\d+)/)))src='https://player.vimeo.com/video/'+m[1];
      var media=src?'<iframe src="'+esc(src)+'" title="'+esc(p.caption||'Vídeo')+'" loading="lazy" allow="accelerometer; encrypted-media; picture-in-picture; fullscreen" allowfullscreen></iframe>':'<video src="'+esc(u)+'" controls preload="metadata"></video>';
      return '<figure class="figure s-wide rounded"><div class="ratio">'+media+'</div>'+(p.caption?'<figcaption>'+esc(p.caption)+'</figcaption>':'')+'</figure>'}});

  block('quote',{label:'Citação',icon:'❝',group:'Conteúdo',description:'Citação em destaque com autor.',
    fields:[F('text','Citação','textarea',{default:'Simplicidade é o último grau de sofisticação.'}),F('author','Autor','text',{default:'Leonardo da Vinci'}),F('role','Cargo / origem','text'),F('image','Foto','image')],
    render:function(p,c){return '<figure class="bigquote"><blockquote>'+inline(p.text,c.md)+'</blockquote>'+(p.author?'<figcaption>'+(p.image?img(p.image,p.author,'avatar'):'')+'<span><strong>'+esc(p.author)+'</strong>'+(p.role?'<small>'+esc(p.role)+'</small>':'')+'</span></figcaption>':'')+'</figure>'}});

  block('testimonials',{label:'Depoimentos',icon:'☺',group:'Conteúdo',description:'Vários depoimentos em cartões.',
    fields:[TITLE,COLS(3),F('items','Depoimentos','list',{itemLabel:'author',fields:[F('text','Texto','textarea',{default:'Mudou meu jeito de trabalhar.'}),F('author','Nome','text',{default:'Ana'}),F('role','Cargo','text'),F('image','Foto','image')]})],
    render:function(p,c){return head(p)+grid(p.columns,p.items,function(it){return '<figure class="card testimonial"><blockquote>“'+inline(it.text,c.md)+'”</blockquote><figcaption>'+(it.image?img(it.image,it.author,'avatar'):'<span class="avatar ph">'+esc(String(it.author||'?').slice(0,1))+'</span>')+'<span><strong>'+esc(it.author)+'</strong>'+(it.role?'<small>'+esc(it.role)+'</small>':'')+'</span></figcaption></figure>'})}});

  block('stats',{label:'Números',icon:'#',group:'Conteúdo',description:'Números grandes com rótulo (métricas, conquistas).',
    fields:[TITLE,F('items','Números','list',{itemLabel:'label',fields:[F('value','Valor','text',{default:'100+'}),F('label','Rótulo','text',{default:'clientes'})],default:[{value:'120+',label:'notas'},{value:'8',label:'projetos'},{value:'99%',label:'satisfação'}]})],
    render:function(p){return head(p)+'<div class="stats">'+p.items.map(function(it){return '<div class="stat"><strong>'+esc(it.value)+'</strong><span>'+esc(it.label)+'</span></div>'}).join('')+'</div>'}});

  block('timeline',{label:'Linha do tempo',icon:'┆',group:'Conteúdo',description:'Eventos em ordem, com data, título e texto (história, roteiro, carreira).',
    fields:[TITLE,SUB,F('items','Eventos','list',{itemLabel:'title',fields:[F('date','Data','text',{default:'2025'}),F('title','Título','text',{default:'Marco'}),F('text','Texto','markdown')]})],
    render:function(p,c){return head(p)+'<ol class="timeline">'+p.items.map(function(it){return '<li><span class="when">'+esc(it.date)+'</span><div><h3>'+inline(it.title,c.md)+'</h3>'+(it.text?markdown(it.text,c.md):'')+'</div></li>'}).join('')+'</ol>'}});

  block('faq',{label:'Perguntas',icon:'?',group:'Conteúdo',description:'Perguntas frequentes que abrem e fecham.',
    fields:[TITLE,SUB,F('items','Perguntas','list',{itemLabel:'q',fields:[F('q','Pergunta','text',{default:'Como funciona?'}),F('a','Resposta','markdown',{default:'Explique aqui.'})]}),F('openFirst','Primeira aberta','boolean',{default:true})],
    render:function(p,c){return head(p)+'<div class="faq">'+p.items.map(function(it,k){return '<details'+(p.openFirst&&!k?' open':'')+'><summary>'+inline(it.q,c.md)+'</summary><div class="prose">'+markdown(it.a,c.md)+'</div></details>'}).join('')+'</div>'}});

  block('cta',{label:'Chamada',icon:'➜',group:'Estrutura',description:'Faixa de chamada para ação com botões.',
    fields:[F('title','Título','text',{default:'Pronto para começar?'}),F('text','Texto','textarea',{default:'Leva menos de um minuto.'}),BUTTONS],
    render:function(p,c){return '<div class="cta-box"><div><h2>'+inline(p.title,c.md)+'</h2>'+(p.text?'<p>'+inline(p.text,c.md)+'</p>':'')+'</div>'+btns(p.buttons)+'</div>'}});

  block('columns',{label:'Colunas',icon:'⫴',group:'Conteúdo',description:'Duas a quatro colunas de texto em Markdown.',
    fields:[TITLE,COLS(2),F('items','Colunas','list',{itemLabel:'title',fields:[F('title','Título','text'),F('markdown','Texto','markdown',{default:'Texto da coluna.'})],default:[{title:'Antes',markdown:'Como era.'},{title:'Depois',markdown:'Como ficou.'}]})],
    render:function(p,c){return head(p)+grid(p.columns,p.items,function(it){return '<div class="prose col">'+(it.title?'<h3>'+inline(it.title,c.md)+'</h3>':'')+markdown(it.markdown,c.md)+'</div>'})}});

  block('pricing',{label:'Planos',icon:'$',group:'Conteúdo',description:'Tabela de planos/preços com lista de itens e botão.',
    fields:[TITLE,SUB,F('items','Planos','list',{itemLabel:'name',fields:[F('name','Nome','text',{default:'Plano'}),F('price','Preço','text',{default:'R$ 0'}),F('period','Período','text',{default:'/mês'}),F('description','Descrição','text'),F('features','Itens (um por linha)','textarea',{default:'Item um\nItem dois'}),F('buttonLabel','Botão','text',{default:'Escolher'}),F('url','Link','url',{default:'#'}),F('highlight','Destacar','boolean',{default:false})]})],
    render:function(p){return head(p)+grid(Math.min(4,Math.max(1,p.items.length)),p.items,function(it){return '<div class="card plan'+(it.highlight?' hot':'')+'">'+(it.highlight?'<span class="pill">Mais escolhido</span>':'')+'<h3>'+esc(it.name)+'</h3><div class="price"><strong>'+esc(it.price)+'</strong><span>'+esc(it.period)+'</span></div>'+(it.description?'<p>'+esc(it.description)+'</p>':'')+'<ul class="checks">'+String(it.features||'').split('\n').filter(function(x){return x.trim()}).map(function(x){return '<li>'+esc(x.trim())+'</li>'}).join('')+'</ul>'+btns([{label:it.buttonLabel,url:it.url,variant:it.highlight?'primary':'secondary'}])+'</div>'})}});

  block('contact',{label:'Contato',icon:'@',group:'Estrutura',description:'Contato com e-mail, telefone e links (redes, portfólio).',
    fields:[F('title','Título','text',{default:'Vamos conversar'}),F('text','Texto','textarea',{default:'Respondo em até um dia.'}),F('email','E-mail','text'),F('phone','Telefone','text'),F('links','Links','list',{itemLabel:'label',fields:[F('label','Nome','text',{default:'GitHub'}),F('url','Link','url')]})],
    render:function(p,c){var b=[];if(p.email)b.push({label:'✉ '+p.email,url:'mailto:'+p.email,variant:'primary'});if(p.phone)b.push({label:'☏ '+p.phone,url:'tel:'+String(p.phone).replace(/[^\d+]/g,''),variant:'secondary'});
      return '<div class="contact"><h2>'+inline(p.title,c.md)+'</h2>'+(p.text?'<p class="sub">'+inline(p.text,c.md)+'</p>':'')+btns(b)+(p.links.length?'<div class="links">'+p.links.map(function(l){var u=safeUrl(l.url);return u?'<a href="'+esc(u)+'" target="_blank" rel="noopener">'+esc(l.label||u)+' ↗</a>':''}).join('')+'</div>':'')+'</div>'}});

  block('countdown',{label:'Contagem regressiva',icon:'⏱',group:'Estrutura',description:'Contagem até uma data (eventos, lançamentos).',
    fields:[F('title','Título','text',{default:'Falta pouco'}),F('date','Data e hora (AAAA-MM-DD HH:MM)','text',{default:'2030-01-01 09:00'}),F('done','Texto quando chegar','text',{default:'Começou!'})],
    render:function(p,c){return '<div class="countdown" data-until="'+esc(String(p.date).replace(' ','T'))+'" data-done="'+esc(p.done)+'"><h2>'+inline(p.title,c.md)+'</h2><div class="cd">'+['dias','horas','min','seg'].map(function(u){return '<div><strong>--</strong><span>'+u+'</span></div>'}).join('')+'</div></div>'}});

  block('code',{label:'Código',icon:'</>',group:'Conteúdo',description:'Bloco de código com botão de copiar.',
    fields:[TITLE,F('language','Linguagem','text',{default:'js'}),F('code','Código','code',{default:'console.log("olá")'})],
    render:function(p){return head(p)+'<pre class="code" data-lang="'+esc(p.language)+'"><button class="copy" type="button">Copiar</button><code>'+esc(p.code)+'</code></pre>'}});

  block('toc',{label:'Sumário',icon:'≡',group:'Estrutura',description:'Lista automática das seções com título desta página.',
    fields:[F('title','Título','text',{default:'Nesta página'})],
    render:function(p,c){var items=c.toc().filter(function(t){return t.id!==c.section.id});return '<nav class="toc"><strong>'+esc(p.title)+'</strong><ol>'+items.map(function(t){return '<li><a href="#'+esc(t.anchor)+'">'+esc(t.title)+'</a></li>'}).join('')+'</ol></nav>'}});

  block('divider',{label:'Divisor',icon:'—',group:'Estrutura',description:'Separador visual ou espaço.',
    fields:[F('style','Estilo','select',{options:['line','dots','wave','space'],labels:['Linha','Pontos','Onda','Só espaço'],default:'line'})],
    render:function(p){return p.style==='wave'?'<svg class="wave" viewBox="0 0 1200 40" preserveAspectRatio="none" aria-hidden="true"><path d="M0 20 Q150 0 300 20 T600 20 T900 20 T1200 20" fill="none" stroke="currentColor" stroke-width="2"/></svg>':'<div class="divider d-'+esc(p.style)+'" role="separator"></div>'}});

  block('html',{label:'HTML livre',icon:'{ }',group:'Avançado',description:'HTML/CSS/JS próprio, inserido como está (use com cuidado).',
    fields:[F('code','HTML','code',{default:'<div style="text-align:center">Olá!</div>'})],
    render:function(p){return String(p.code||'')}});

  function stripTitle(content,title,shown){if(!shown)return content;var t=String(content||''),m=t.replace(/^---[\s\S]*?\n---\n?/,'').match(/^\s*#\s+(.+)\n?/);if(m&&m[1].trim().toLowerCase()===String(title).trim().toLowerCase())return t.replace(/^(---[\s\S]*?\n---\n?)?\s*#\s+.+\n?/,'$1');return t}

  /* ---------------- tema, layout e estilo de seção ---------------- */
  var THEME_FIELDS=[F('preset','Tema','select',{options:Object.keys(THEMES),labels:Object.keys(THEMES).map(function(k){return THEMES[k].label}),default:'aurora'}),
    F('mode','Modo','select',{options:['','dark','light','auto'],labels:['Do tema','Escuro','Claro','Auto'],default:''}),
    F('headingFont','Fonte dos títulos','select',{options:[''].concat(Object.keys(FONTS)),labels:['Do tema'].concat(Object.keys(FONTS).map(function(k){return FONTS[k].label})),default:''}),
    F('bodyFont','Fonte do texto','select',{options:[''].concat(Object.keys(FONTS)),labels:['Do tema'].concat(Object.keys(FONTS).map(function(k){return FONTS[k].label})),default:''}),
    F('primary','Cor principal','color'),F('accent','Cor de destaque','color'),F('bg','Fundo','color'),F('surface','Superfícies','color'),F('text','Texto','color'),F('muted','Texto secundário','color'),
    F('background','Fundo da página','select',{options:['','plain','gradient','mesh','dots','grid'],labels:['Do tema','Liso','Degradê','Aurora','Pontos','Grade'],default:''}),
    F('radius','Arredondamento','number',{min:0,max:32,default:null}),F('scale','Tamanho do texto','number',{min:.85,max:1.3,step:.05,default:1}),
    F('spacing','Espaçamento','select',{options:['compact','comfortable','airy'],labels:['Compacto','Confortável','Arejado'],default:'comfortable'}),
    F('width','Largura máxima (px)','number',{min:640,max:1600,step:20,default:1120}),F('shadow','Sombras','select',{options:['none','soft','strong'],labels:['Sem','Suaves','Fortes'],default:'soft'}),
    F('animations','Animações ao rolar','boolean',{default:true})];
  var LAYOUT_FIELDS=[F('nav','Barra de navegação','boolean',{default:true}),F('brand','Nome na barra','text'),F('sticky','Barra fixa no topo','boolean',{default:true}),
    F('navCta','Botão da barra','text'),F('navCtaUrl','Link do botão da barra','url'),
    F('footer','Rodapé (Markdown)','markdown',{default:''}),F('themeToggle','Botão claro/escuro','boolean',{default:true}),F('backToTop','Botão voltar ao topo','boolean',{default:true}),F('progress','Barra de progresso de leitura','boolean',{default:false})];
  var META_FIELDS=[F('title','Título da página','text',{default:'Página sem título'}),F('description','Descrição (buscadores e compartilhamento)','textarea'),F('lang','Idioma','text',{default:'pt-BR'}),F('icon','Ícone (emoji)','text',{default:'✦'}),F('image','Imagem de compartilhamento','image')];
  var SECTION_FIELDS=[F('anchor','Âncora (#link) e item do menu','text'),F('menu','Mostrar no menu','boolean',{default:false}),
    F('background','Fundo','select',{options:['none','surface','primary','gradient','image','inverse'],labels:['Transparente','Superfície','Cor principal','Degradê','Imagem','Invertido'],default:'none'}),
    F('image','Imagem de fundo','image'),F('padding','Espaço vertical','select',{options:['none','s','m','l','xl'],labels:['Nenhum','Pequeno','Médio','Grande','Enorme'],default:'m'}),
    F('width','Largura do conteúdo','select',{options:['narrow','normal','wide','full'],labels:['Estreita','Normal','Larga','Tela inteira'],default:'normal'}),
    F('align','Alinhamento','select',{options:['left','center'],labels:['Esquerda','Centro'],default:'left'}),F('hidden','Ocultar seção','boolean',{default:false})];

  /* ---------------- validação / normalização ---------------- */
  function coerce(f,v,path,rep){
    var t=f.type,def=f.default;
    if(v==null||v===''){if(t==='list')return clone(def||[]);if(t==='boolean')return def!=null?def:false;if(f.required&&v!=='')rep.err(path,'"'+f.label+'" é obrigatório.');return def!=null?clone(def):(t==='number'?null:'')}
    if(t==='boolean'){if(typeof v==='boolean')return v;if(v==='true'||v==='false')return v==='true';rep.err(path,'deve ser true ou false.');return !!def}
    if(t==='number'){var n=Number(v);if(!isFinite(n)){rep.err(path,'deve ser um número.');return def}return clamp(n,f.min!=null?f.min:-1e9,f.max!=null?f.max:1e9)}
    if(t==='select'){var ok=f.options.some(function(o){return String(o)===String(v)});if(!ok){rep.err(path,'valor "'+v+'" inválido; use um de: '+f.options.filter(function(o){return o!==''}).join(', ')+'.');return def}var opt=f.options.find(function(o){return String(o)===String(v)});return opt}
    if(t==='color'){v=String(v).trim();if(!COLOR_RE.test(v)){rep.err(path,'cor inválida "'+v+'" (use #rrggbb, rgb() ou hsl()).');return ''}return v}
    if(t==='list'){if(!Array.isArray(v)){rep.err(path,'deve ser uma lista.');return clone(def||[])}
      return v.slice(0,f.max||60).map(function(item,k){if(!isObj(item)){rep.err(path+'['+k+']','cada item deve ser um objeto.');item={}}return fields(f.fields,item,path+'['+k+']',rep)})}
    if(typeof v==='object'){rep.err(path,'deve ser texto.');return def||''}
    return String(v);
  }
  function fields(list,src,path,rep){var out={},known={};src=isObj(src)?src:{};
    list.forEach(function(f){known[f.key]=1;out[f.key]=coerce(f,src[f.key],path+'.'+f.key,rep)});
    Object.keys(src).forEach(function(k){if(!known[k])rep.warn(path+'.'+k,'propriedade desconhecida, ignorada.')});return out}
  function normalize(input){
    var errors=[],warnings=[],rep={err:function(p,m){errors.push({path:p,message:m})},warn:function(p,m){warnings.push({path:p,message:m})}},raw=input;
    if(typeof raw==='string'){try{raw=JSON.parse(raw)}catch(e){return{spec:blank(),errors:[{path:'(json)',message:'JSON inválido: '+e.message}],warnings:[]}}}
    if(!isObj(raw))return{spec:blank(),errors:[{path:'(raiz)',message:'A página precisa ser um objeto JSON.'}],warnings:[]};
    var s={version:VERSION,kind:'urbe-page'};
    if(raw.kind&&raw.kind!=='urbe-page'&&raw.kind!=='urbe-template')rep.warn('kind','esperado "urbe-page".');if(raw.kind==='urbe-template')s.kind='urbe-template';
    if(raw.template&&isObj(raw.template))s.template={name:String(raw.template.name||''),description:String(raw.template.description||'')};
    s.meta=fields(META_FIELDS,raw.meta,'meta',rep);
    var th=isObj(raw.theme)?raw.theme:{};if(th.preset&&!THEMES[th.preset]){rep.err('theme.preset','tema "'+th.preset+'" não existe; use um de: '+Object.keys(THEMES).join(', ')+'.');th=Object.assign({},th,{preset:'aurora'})}
    s.theme=fields(THEME_FIELDS,th,'theme',rep);
    s.layout=fields(LAYOUT_FIELDS,raw.layout,'layout',rep);
    var secs=Array.isArray(raw.sections)?raw.sections:(raw.sections==null?[]:(rep.err('sections','deve ser uma lista de seções.'),[])),ids={};
    s.sections=secs.slice(0,200).map(function(sec,i){var p='sections['+i+']';
      if(!isObj(sec)){rep.err(p,'cada seção deve ser um objeto {type, props}.');return null}
      var b=BLOCKS[sec.type];if(!b){rep.err(p+'.type','tipo "'+sec.type+'" não existe. Tipos: '+Object.keys(BLOCKS).join(', ')+'.');return null}
      var id=typeof sec.id==='string'&&/^[\w-]{1,40}$/.test(sec.id)&&!ids[sec.id]?sec.id:uid('s');ids[id]=1;
      Object.keys(sec).forEach(function(k){if(['id','type','props','style'].indexOf(k)<0)rep.warn(p+'.'+k,'propriedade desconhecida (use props ou style).')});
      return{id:id,type:sec.type,props:fields(b.fields,sec.props,p+'.props',rep),style:fields(SECTION_FIELDS,sec.style,p+'.style',rep)}}).filter(Boolean);
    return{spec:s,errors:errors,warnings:warnings};
  }
  function blank(){return{version:VERSION,kind:'urbe-page',meta:fields(META_FIELDS,{},'',noop),theme:fields(THEME_FIELDS,{},'',noop),layout:fields(LAYOUT_FIELDS,{},'',noop),sections:[]}}
  var noop={err:function(){},warn:function(){}};
  function newSection(type,props){var b=BLOCKS[type];if(!b)throw new Error('Bloco desconhecido: '+type);return{id:uid('s'),type:type,props:fields(b.fields,props||{},'',noop),style:fields(SECTION_FIELDS,type==='hero'?{padding:'l'}:{},'',noop)}}

  /* ---------------- CSS ---------------- */
  function palette(t){var pr=THEMES[t.preset]||THEMES.aurora,mode=t.mode||pr.mode,over={};['bg','surface','text','muted','primary','accent'].forEach(function(k){if(t[k])over[k]=t[k]});
    function v(p){var o=Object.assign({},p,over);return '--bg:'+o.bg+';--surface:'+o.surface+';--text:'+o.text+';--muted:'+o.muted+';--primary:'+o.primary+';--accent:'+o.accent+';--border:'+o.border+';--on-primary:'+o.onPrimary+';'}
    return{mode:mode,dark:v(pr.dark),light:v(pr.light)}}
  function fontOf(k,fallback){return FONTS[k]||FONTS[fallback]||FONTS.inter}
  function css(s){
    var t=s.theme,pr=THEMES[t.preset]||THEMES.aurora,p=palette(t),hf=fontOf(t.headingFont,pr.fonts.heading),bf=fontOf(t.bodyFont,pr.fonts.body),r=t.radius!=null?t.radius:pr.radius,bg=t.background||pr.background;
    var sp={compact:.7,comfortable:1,airy:1.35}[t.spacing]||1,sh={none:'none',soft:'0 1px 2px rgba(0,0,0,.06),0 8px 28px -12px rgba(0,0,0,.28)',strong:'0 2px 4px rgba(0,0,0,.1),0 22px 50px -16px rgba(0,0,0,.5)'}[t.shadow]||'none';
    var vars=':root{'+(p.mode==='dark'?p.dark:p.light)+'--r:'+r+'px;--rs:'+Math.round(r*.6)+'px;--w:'+(t.width||1120)+'px;--sp:'+sp+';--shadow:'+sh+';--fh:'+hf.stack+';--fb:'+bf.stack+';--fs:'+(t.scale||1)+';color-scheme:'+(p.mode==='dark'?'dark':'light')+'}'+
      (p.mode==='auto'?'@media (prefers-color-scheme:dark){:root{'+p.dark+'color-scheme:dark}}':'')+
      ':root[data-theme=dark]{'+p.dark+'color-scheme:dark}:root[data-theme=light]{'+p.light+'color-scheme:light}';
    var bgs={plain:'',gradient:'body{background:radial-gradient(1200px 600px at 10% -10%,color-mix(in srgb,var(--primary) 18%,transparent),transparent 60%),radial-gradient(900px 500px at 110% 10%,color-mix(in srgb,var(--accent) 14%,transparent),transparent 60%),var(--bg);background-attachment:fixed}',
      mesh:'body{background:radial-gradient(60vw 50vh at 0% 0%,color-mix(in srgb,var(--primary) 22%,transparent),transparent 70%),radial-gradient(50vw 50vh at 100% 20%,color-mix(in srgb,var(--accent) 18%,transparent),transparent 70%),radial-gradient(60vw 40vh at 50% 110%,color-mix(in srgb,var(--primary) 12%,transparent),transparent 70%),var(--bg);background-attachment:fixed}',
      dots:'body{background:radial-gradient(color-mix(in srgb,var(--text) 14%,transparent) 1px,transparent 1.4px) 0 0/22px 22px,var(--bg)}',
      grid:'body{background:linear-gradient(color-mix(in srgb,var(--text) 6%,transparent) 1px,transparent 1px) 0 0/40px 40px,linear-gradient(90deg,color-mix(in srgb,var(--text) 6%,transparent) 1px,transparent 1px) 0 0/40px 40px,var(--bg)}'}[bg]||'';
    return vars+BASE_CSS+bgs+(t.animations?ANIM_CSS:'');
  }
  var BASE_CSS=[
    '*,*::before,*::after{box-sizing:border-box}html{-webkit-text-size-adjust:100%;scroll-behavior:smooth;scroll-padding-top:80px}',
    'body{margin:0;background:var(--bg);color:var(--text);font:calc(17px*var(--fs))/1.65 var(--fb);-webkit-font-smoothing:antialiased;text-rendering:optimizeLegibility;overflow-x:hidden}',
    'img,video,iframe{max-width:100%;display:block}a{color:var(--primary);text-decoration-thickness:.08em;text-underline-offset:.18em}a:hover{color:var(--accent)}',
    'h1,h2,h3,h4{font-family:var(--fh);line-height:1.15;letter-spacing:-.02em;margin:0 0 .5em;text-wrap:balance}h1{font-size:clamp(2.2rem,5.2vw,4.2rem);font-weight:800}h2{font-size:clamp(1.6rem,3.2vw,2.5rem);font-weight:750}h3{font-size:1.2rem;font-weight:700}',
    'figure{margin:0}h1 strong,h2 strong{background:linear-gradient(120deg,var(--primary),var(--accent));-webkit-background-clip:text;background-clip:text;color:transparent}.a-center .card,.a-center .faq{text-align:left}p{margin:0 0 1em}.sub{color:var(--muted);font-size:1.1em;max-width:62ch}',
    /* seções */
    '.sec{position:relative;padding:calc(72px*var(--sp)) 20px}.sec.p-none{padding:0 20px}.sec.p-s{padding:calc(36px*var(--sp)) 20px}.sec.p-l{padding:calc(110px*var(--sp)) 20px}.sec.p-xl{padding:calc(160px*var(--sp)) 20px}',
    '.wrap{max-width:var(--w);margin:0 auto}.w-narrow .wrap{max-width:min(720px,var(--w))}.w-wide .wrap{max-width:calc(var(--w) + 200px)}.w-full .wrap{max-width:none}.w-full{padding-left:0;padding-right:0}',
    '.a-center{text-align:center}.a-center .sec-head,.a-center .sub{margin-left:auto;margin-right:auto}.a-center .btns{justify-content:center}',
    '.bg-surface{background:var(--surface)}.bg-primary{background:var(--primary);color:var(--on-primary)}.bg-primary .sub,.bg-primary p{color:inherit;opacity:.9}.bg-primary a:not(.btn){color:inherit}.bg-primary .btn-primary{background:var(--on-primary);color:var(--primary)}',
    '.bg-gradient{background:linear-gradient(135deg,color-mix(in srgb,var(--primary) 85%,#000 0%),var(--accent));color:#fff}.bg-gradient .sub,.bg-gradient p{color:inherit;opacity:.92}.bg-gradient .btn-primary{background:#fff;color:#111}.bg-gradient .btn-secondary,.bg-image .btn-secondary,.bg-primary .btn-secondary{border-color:rgba(255,255,255,.55);color:#fff;background:rgba(255,255,255,.12)}',
    '.bg-inverse{background:var(--text);color:var(--bg)}.bg-inverse .sub,.bg-inverse .muted{color:color-mix(in srgb,var(--bg) 70%,transparent)}',
    '.bg-image{background-size:cover;background-position:center;color:#fff}.bg-image::before{content:"";position:absolute;inset:0;background:linear-gradient(180deg,rgba(0,0,0,.35),rgba(0,0,0,.6))}.bg-image>.wrap{position:relative}.bg-image .sub,.bg-image p{color:#f1f1f1}',
    '.sec-head{margin-bottom:calc(36px*var(--sp));max-width:760px}',
    /* botões */
    '.btns{display:flex;flex-wrap:wrap;gap:12px;margin-top:28px}.btn{display:inline-flex;align-items:center;gap:8px;min-height:48px;padding:0 22px;border-radius:calc(var(--rs) + 4px);font:600 1rem var(--fb);text-decoration:none;transition:transform .15s,box-shadow .2s,background .2s,color .2s;border:1px solid transparent}',
    '.btn-primary{background:var(--primary);color:var(--on-primary);box-shadow:0 8px 24px -10px color-mix(in srgb,var(--primary) 70%,transparent)}.btn-primary:hover{color:var(--on-primary);transform:translateY(-2px);box-shadow:0 14px 30px -12px color-mix(in srgb,var(--primary) 80%,transparent)}',
    '.btn-secondary{border-color:var(--border);color:var(--text);background:color-mix(in srgb,var(--surface) 70%,transparent)}.btn-secondary:hover{color:var(--text);border-color:var(--primary)}.btn-ghost{color:var(--primary)}.btn-ghost:hover{background:color-mix(in srgb,var(--primary) 10%,transparent)}',
    /* capa */
    '.hero{display:grid;gap:48px;align-items:center}.hero.h-tall{min-height:min(72vh,760px)}.hero.h-screen{min-height:calc(100svh - 64px)}.hero.l-center{text-align:center;justify-items:center}.hero.l-center .lead{margin:0 auto}.hero.l-center .btns{justify-content:center}',
    '.hero.l-split{grid-template-columns:1.1fr 1fr}.hero h1{margin-bottom:.3em}.hero .lead{font-size:clamp(1.08rem,1.6vw,1.3rem);color:var(--muted);max-width:60ch}.bg-gradient .hero .lead,.bg-primary .hero .lead,.bg-image .hero .lead{color:inherit;opacity:.9}',
    '.eyebrow{display:inline-block;margin:0 0 18px;padding:6px 14px;border-radius:99px;border:1px solid var(--border);background:color-mix(in srgb,var(--primary) 12%,transparent);color:var(--primary);font:600 .82rem var(--fb);letter-spacing:.04em;text-transform:uppercase}',
    '.stack-btns .btns{flex-direction:column;align-items:stretch;width:min(100%,440px);margin-left:auto;margin-right:auto}.stack-btns .btn{justify-content:center;min-height:54px}.hero-media img{width:100%;border-radius:var(--r);box-shadow:var(--shadow);border:1px solid var(--border)}.hero.l-center .hero-media{max-width:960px;width:100%}',
    /* cartões e grades */
    '.grid{display:grid;gap:20px}.g2{grid-template-columns:repeat(2,minmax(0,1fr))}.g3{grid-template-columns:repeat(3,minmax(0,1fr))}.g4{grid-template-columns:repeat(4,minmax(0,1fr))}.grid.compact{gap:12px}',
    '.card{display:block;position:relative;padding:24px;border-radius:var(--r);background:var(--surface);border:1px solid var(--border);box-shadow:var(--shadow);color:inherit;text-decoration:none;transition:transform .2s,border-color .2s,box-shadow .2s}a.card:hover{transform:translateY(-4px);border-color:color-mix(in srgb,var(--primary) 55%,var(--border));color:inherit}',
    '.card p{color:var(--muted);margin:0}.card h3{margin-bottom:.35em}.feature .ficon{font-size:1.6rem;width:52px;height:52px;display:grid;place-items:center;border-radius:calc(var(--rs) + 2px);background:color-mix(in srgb,var(--primary) 14%,transparent);margin-bottom:16px}',
    '.media-card{padding:0;overflow:hidden}.card-img{aspect-ratio:16/10;overflow:hidden;background:color-mix(in srgb,var(--primary) 10%,var(--surface))}.card-img img{width:100%;height:100%;object-fit:cover;transition:transform .5s}a.media-card:hover .card-img img{transform:scale(1.05)}.card-body{padding:20px 22px 24px}',
    '.pill{display:inline-block;margin-bottom:10px;padding:3px 10px;border-radius:99px;background:color-mix(in srgb,var(--accent) 16%,transparent);color:var(--accent);font:600 .75rem var(--fb);letter-spacing:.02em}',
    '.note-card h3{font-size:1.08rem}.note-card p{font-size:.95rem}.tags{display:flex;flex-wrap:wrap;gap:6px;margin-top:12px}.tags span{font-size:.75rem;color:var(--primary)}.stack{display:grid;gap:12px}.compact .card{padding:16px}',
    '.note-full{margin-top:calc(64px*var(--sp));display:grid;gap:calc(64px*var(--sp))}.note-full article{padding-top:calc(32px*var(--sp));border-top:1px solid var(--border)}',
    /* texto */
    '.prose{max-width:72ch}.a-center .prose{margin:0 auto}.prose.cols2{columns:2;column-gap:48px;max-width:none}.prose h1{font-size:clamp(2rem,4vw,3rem)}.prose h2{margin-top:1.6em}.prose h3{margin-top:1.4em}.prose>:first-child{margin-top:0}',
    '.prose ul,.prose ol{padding-left:1.3em;margin:0 0 1em}.prose li{margin:.3em 0}.prose li::marker{color:var(--primary)}.prose img{border-radius:var(--rs);margin:1.2em 0}.prose hr{border:0;border-top:1px solid var(--border);margin:2.4em 0}',
    '.prose blockquote{margin:1.4em 0;padding:.2em 0 .2em 1.2em;border-left:3px solid var(--primary);color:var(--muted);font-style:italic}.prose code{font:.88em ui-monospace,SFMono-Regular,Menlo,monospace;background:color-mix(in srgb,var(--text) 8%,transparent);padding:.15em .4em;border-radius:6px}',
    '.prose mark{background:color-mix(in srgb,var(--accent) 30%,transparent);color:inherit;padding:0 .2em;border-radius:3px}.task{list-style:none}.task-item{list-style:none;margin-left:-1.3em}.task.done{color:var(--muted);text-decoration:line-through}',
    '.table{overflow-x:auto;margin:1.4em 0;border:1px solid var(--border);border-radius:var(--rs)}table{border-collapse:collapse;width:100%;font-size:.95em}th,td{padding:10px 14px;border-bottom:1px solid var(--border);text-align:left}th{background:color-mix(in srgb,var(--text) 5%,transparent);font-weight:650}tr:last-child td{border-bottom:0}',
    '.callout{margin:1.4em 0;padding:14px 18px;border-radius:var(--rs);border:1px solid color-mix(in srgb,var(--primary) 40%,transparent);background:color-mix(in srgb,var(--primary) 9%,transparent)}.callout-title{display:block;margin-bottom:6px}.callout-warning,.callout-caution{border-color:#e0a82e66;background:#e0a82e14}.callout-danger,.callout-error{border-color:#e5484d66;background:#e5484d14}.callout-tip,.callout-success{border-color:#30a46c66;background:#30a46c14}',
    'pre.code{position:relative;margin:1.4em 0;padding:18px 20px;border-radius:var(--rs);background:color-mix(in srgb,var(--text) 92%,var(--bg));color:var(--bg);overflow:auto;font:.9rem/1.6 ui-monospace,SFMono-Regular,Menlo,monospace}pre.code code{background:none;padding:0;color:inherit}',
    'pre.code .copy{position:absolute;top:10px;right:10px;border:1px solid color-mix(in srgb,var(--bg) 30%,transparent);background:transparent;color:inherit;font:600 .75rem var(--fb);border-radius:8px;padding:4px 10px;cursor:pointer;opacity:.75}pre.code .copy:hover{opacity:1}',
    '.note-title{margin-bottom:.4em}.meta{color:var(--muted);font-size:.9rem}.wikilink{color:var(--primary);border-bottom:1px dashed currentColor;text-decoration:none}span.wikilink{color:inherit;border-bottom-color:var(--muted)}',
    /* mídia */
    '.figure{margin:0 auto}.figure.s-normal{max-width:760px}.figure.s-full{max-width:none}.figure img,.figure video{width:100%}.figure.rounded img,.figure.rounded .ratio{border-radius:var(--r);overflow:hidden}figcaption{margin-top:10px;color:var(--muted);font-size:.9rem;text-align:center}',
    '.ratio{position:relative;aspect-ratio:16/9;background:#000}.ratio iframe,.ratio video{position:absolute;inset:0;width:100%;height:100%;border:0}',
    '.gallery .shot{margin:0}.gallery img{width:100%;aspect-ratio:1;object-fit:cover;border-radius:var(--rs);transition:transform .3s,filter .3s}.gallery a:hover img{transform:scale(1.02);filter:brightness(1.05)}',
    '.bigquote{margin:0 auto;max-width:860px;text-align:center}.bigquote blockquote{margin:0 0 24px;font:italic 600 clamp(1.4rem,3vw,2.2rem)/1.35 var(--fh);letter-spacing:-.01em}.bigquote figcaption,.testimonial figcaption{display:flex;align-items:center;justify-content:center;gap:12px;text-align:left}',
    '.bigquote small,.testimonial small{display:block;color:var(--muted);font-weight:400}.avatar{width:44px;height:44px;border-radius:50%;object-fit:cover}.avatar.ph{display:grid;place-items:center;background:color-mix(in srgb,var(--primary) 20%,transparent);color:var(--primary);font-weight:700}',
    '.testimonial blockquote{margin:0 0 18px;font-size:1.02rem}.testimonial figcaption{justify-content:flex-start}',
    '.stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:24px}.stat{padding:8px 0}.stat strong{display:block;font:800 clamp(2.2rem,5vw,3.4rem)/1 var(--fh);letter-spacing:-.03em;background:linear-gradient(135deg,var(--primary),var(--accent));-webkit-background-clip:text;background-clip:text;color:transparent}.stat span{color:var(--muted)}',
    '.timeline{list-style:none;margin:0;padding:0;position:relative;display:grid;gap:28px}.timeline::before{content:"";position:absolute;left:7px;top:6px;bottom:6px;width:2px;background:var(--border)}.timeline li{position:relative;display:grid;grid-template-columns:130px 1fr;gap:18px;padding-left:34px}',
    '.timeline li::before{content:"";position:absolute;left:0;top:6px;width:16px;height:16px;border-radius:50%;background:var(--bg);border:3px solid var(--primary)}.when{color:var(--primary);font-weight:700}.timeline h3{margin-bottom:.2em}.timeline p{color:var(--muted)}',
    '.faq{display:grid;gap:10px;max-width:860px}.a-center .faq{margin:0 auto;text-align:left}.faq details{border:1px solid var(--border);border-radius:var(--rs);background:var(--surface);transition:border-color .2s}.faq details[open]{border-color:color-mix(in srgb,var(--primary) 50%,var(--border))}',
    '.faq summary{cursor:pointer;list-style:none;padding:18px 52px 18px 20px;font-weight:650;position:relative}.faq summary::-webkit-details-marker{display:none}.faq summary::after{content:"+";position:absolute;right:20px;top:50%;transform:translateY(-50%);font-size:1.4rem;color:var(--primary);transition:transform .2s}.faq details[open] summary::after{transform:translateY(-50%) rotate(45deg)}.faq .prose{padding:0 20px 16px;color:var(--muted)}',
    '.cta-box{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:24px;padding:clamp(28px,5vw,56px);border-radius:calc(var(--r) + 6px);background:linear-gradient(135deg,var(--primary),var(--accent));color:#fff;box-shadow:var(--shadow)}.cta-box h2{margin:0 0 .2em}.cta-box p{margin:0;opacity:.92}.cta-box .btns{margin:0}.cta-box .btn-primary{background:#fff;color:#111}.cta-box .btn-secondary{color:#fff;border-color:rgba(255,255,255,.55);background:transparent}',
    '.a-center .cta-box{flex-direction:column;text-align:center}',
    '.plan{display:flex;flex-direction:column}.plan.hot{border:2px solid var(--primary);transform:scale(1.02)}.price{display:flex;align-items:baseline;gap:6px;margin:6px 0 12px}.price strong{font:800 2.4rem/1 var(--fh);letter-spacing:-.03em}.price span{color:var(--muted)}',
    '.checks{list-style:none;padding:0;margin:10px 0 0;display:grid;gap:8px;flex:1}.checks li{padding-left:26px;position:relative}.checks li::before{content:"✓";position:absolute;left:0;color:var(--primary);font-weight:800}.plan .btns{margin-top:22px}.plan .btn{width:100%;justify-content:center}',
    '.contact{text-align:center;max-width:720px;margin:0 auto}.contact .sub{margin:0 auto}.contact .btns{justify-content:center}.links{display:flex;flex-wrap:wrap;gap:10px 22px;justify-content:center;margin-top:24px}.links a{text-decoration:none;font-weight:600}',
    '.countdown{text-align:center}.cd{display:flex;justify-content:center;gap:14px;flex-wrap:wrap}.cd div{min-width:86px;padding:16px 10px;border-radius:var(--r);background:var(--surface);border:1px solid var(--border)}.cd strong{display:block;font:800 2.4rem/1 var(--fh);font-variant-numeric:tabular-nums}.cd span{color:var(--muted);font-size:.85rem}',
    '.toc{padding:22px 24px;border-radius:var(--r);border:1px solid var(--border);background:var(--surface);max-width:560px}.toc ol{margin:10px 0 0;padding-left:1.2em;display:grid;gap:6px}',
    '.divider{margin:0 auto;max-width:var(--w)}.d-line{border-top:1px solid var(--border)}.d-dots{height:6px;background:radial-gradient(circle,var(--muted) 1.5px,transparent 2px) center/18px 6px repeat-x;opacity:.6}.d-space{height:24px}.wave{width:100%;height:40px;color:var(--border)}',
    '.missing{padding:20px;border:1px dashed var(--border);border-radius:var(--rs);color:var(--muted);text-align:center}',
    /* navegação, rodapé, extras */
    '.nav{position:relative;z-index:50;background:color-mix(in srgb,var(--bg) 78%,transparent);-webkit-backdrop-filter:saturate(1.6) blur(14px);backdrop-filter:saturate(1.6) blur(14px);border-bottom:1px solid var(--border)}.nav.sticky{position:sticky;top:0}',
    '.nav .wrap{display:flex;align-items:center;gap:20px;min-height:64px;padding:0 20px}.brand{display:flex;align-items:center;gap:10px;font:800 1.1rem var(--fh);color:var(--text);text-decoration:none;letter-spacing:-.02em}.brand:hover{color:var(--text)}.nav-links{display:flex;gap:4px;margin-left:auto;align-items:center}',
    '.nav-links a{padding:8px 12px;border-radius:10px;color:var(--muted);text-decoration:none;font-weight:550;font-size:.95rem}.nav-links a:hover{color:var(--text);background:color-mix(in srgb,var(--text) 6%,transparent)}.nav-links .btn{min-height:40px;padding:0 16px;margin-left:8px;color:var(--on-primary)}',
    '#nav-t{display:none}.nav-burger{display:none;margin-left:auto;width:44px;height:44px;border-radius:12px;cursor:pointer;align-items:center;justify-content:center}.nav-burger span,.nav-burger span::before,.nav-burger span::after{display:block;width:20px;height:2px;background:var(--text);border-radius:2px;position:relative;transition:.2s}.nav-burger span::before,.nav-burger span::after{content:"";position:absolute}.nav-burger span::before{top:-6px}.nav-burger span::after{top:6px}',
    '.foot{padding:48px 20px;border-top:1px solid var(--border);color:var(--muted);font-size:.92rem}.foot .wrap{display:flex;flex-wrap:wrap;gap:12px 24px;justify-content:space-between;align-items:center}.foot p{margin:0}',
    '.fab{position:fixed;right:18px;width:46px;height:46px;border-radius:50%;border:1px solid var(--border);background:var(--surface);color:var(--text);box-shadow:var(--shadow);display:grid;place-items:center;cursor:pointer;z-index:60;font-size:1.1rem;transition:opacity .25s,transform .25s}.fab-top{bottom:18px;opacity:0;pointer-events:none;transform:translateY(10px)}.fab-top.on{opacity:1;pointer-events:auto;transform:none}.fab-theme{bottom:74px}',
    '.progress{position:fixed;left:0;top:0;height:3px;width:0;background:linear-gradient(90deg,var(--primary),var(--accent));z-index:70}',
    '@media (max-width:860px){.g3,.g4{grid-template-columns:repeat(2,minmax(0,1fr))}.hero.l-split{grid-template-columns:1fr}.prose.cols2{columns:1}.timeline li{grid-template-columns:1fr;gap:4px}',
    '.nav-burger{display:inline-flex}.nav-links{display:none;position:absolute;left:0;right:0;top:100%;flex-direction:column;align-items:stretch;padding:10px 16px 18px;background:var(--bg);border-bottom:1px solid var(--border)}#nav-t:checked~.nav-links{display:flex}.nav-links a{padding:12px}.nav-links .btn{margin:6px 0 0;justify-content:center}}',
    '@media (max-width:560px){body{font-size:calc(16px*var(--fs))}.g2,.g3,.g4{grid-template-columns:1fr}.sec{padding:calc(52px*var(--sp)) 18px}.sec.p-l{padding:calc(76px*var(--sp)) 18px}.sec.p-xl{padding:calc(104px*var(--sp)) 18px}.btns .btn{flex:1 1 auto;justify-content:center}.cd div{min-width:68px}.plan.hot{transform:none}}',
    '@media print{.nav,.fab,.progress{display:none!important}.sec{padding:24px 0}.card{box-shadow:none}}'
  ].join('');
  var ANIM_CSS='@media (prefers-reduced-motion:no-preference){.rv{opacity:0;transform:translateY(22px);transition:opacity .7s cubic-bezier(.2,.7,.2,1),transform .7s cubic-bezier(.2,.7,.2,1)}.rv.in{opacity:1;transform:none}}';

  /* pequeno script da página: tema, voltar ao topo, progresso, animações, contagem, copiar código */
  var RUNTIME='(function(){var d=document,r=d.documentElement;try{var s=localStorage.getItem("urbe-page-theme");if(s)r.dataset.theme=s}catch(e){}'+
    'var t=d.querySelector(".fab-theme");if(t)t.onclick=function(){var dark=r.dataset.theme?r.dataset.theme==="dark":r.classList.contains("dark-default")?true:r.classList.contains("light-default")?false:matchMedia("(prefers-color-scheme: dark)").matches;r.dataset.theme=dark?"light":"dark";try{localStorage.setItem("urbe-page-theme",r.dataset.theme)}catch(e){}};'+
    'var top=d.querySelector(".fab-top"),bar=d.querySelector(".progress");function sc(){var y=scrollY,h=r.scrollHeight-innerHeight;if(top)top.classList.toggle("on",y>600);if(bar)bar.style.width=(h>0?y/h*100:0)+"%"}addEventListener("scroll",sc,{passive:true});sc();if(top)top.onclick=function(){scrollTo({top:0,behavior:"smooth"})};'+
    'var rv=d.querySelectorAll(".rv");if("IntersectionObserver"in window){var io=new IntersectionObserver(function(es){es.forEach(function(e){if(e.isIntersecting){e.target.classList.add("in");io.unobserve(e.target)}})},{rootMargin:"0px 0px -8% 0px"});rv.forEach(function(e){io.observe(e)})}else rv.forEach(function(e){e.classList.add("in")});'+
    'd.querySelectorAll(".nav-links a").forEach(function(a){a.addEventListener("click",function(){var c=d.getElementById("nav-t");if(c)c.checked=false})});'+
    'd.querySelectorAll("pre.code .copy").forEach(function(b){b.onclick=function(){var c=b.parentNode.querySelector("code").textContent;(navigator.clipboard?navigator.clipboard.writeText(c):Promise.reject()).then(function(){b.textContent="Copiado!";setTimeout(function(){b.textContent="Copiar"},1400)},function(){})}});'+
    'd.querySelectorAll(".countdown").forEach(function(el){var until=new Date(el.dataset.until).getTime(),cells=el.querySelectorAll(".cd strong");function tick(){var s=Math.max(0,Math.floor((until-Date.now())/1000));if(isNaN(until))return;if(!s){el.querySelector(".cd").textContent=el.dataset.done;return}[Math.floor(s/86400),Math.floor(s/3600)%24,Math.floor(s/60)%60,s%60].forEach(function(v,i){cells[i].textContent=String(v).padStart(2,"0")});setTimeout(tick,1000)}tick()})})();';

  /* modo prévia do estúdio: seleção por toque/clique e rolagem comandada pelo estúdio (iframe isolado, sem acesso ao app) */
  var PREVIEW='(function(){var d=document;d.addEventListener("click",function(e){var a=e.target.closest("a");if(a){var h=a.getAttribute("href")||"";if(h.charAt(0)!=="#"||e.target.closest("[data-sid]"))e.preventDefault()}var s=e.target.closest("[data-sid]");if(s){e.preventDefault();parent.postMessage({urbePage:"select",id:s.dataset.sid},"*")}},true);'+
    'd.querySelectorAll("details").forEach(function(x){x.addEventListener("toggle",function(e){e.stopPropagation()})});'+
    'addEventListener("message",function(e){var m=e.data||{};if(m.urbePage==="select"){d.querySelectorAll(".urbe-sel").forEach(function(x){x.classList.remove("urbe-sel")});var el=m.id&&d.querySelector("[data-sid=\\""+m.id+"\\"]");if(el){el.classList.add("urbe-sel");if(m.scroll){var r=el.getBoundingClientRect();if(r.top<0||r.top>innerHeight*.6)el.scrollIntoView({behavior:m.instant?"auto":"smooth",block:"start"})}}}if(m.urbePage==="scrollTo"){scrollTo(0,m.y||0)}});'+
    'var t;addEventListener("scroll",function(){clearTimeout(t);t=setTimeout(function(){parent.postMessage({urbePage:"scroll",y:scrollY},"*")},120)},{passive:true});parent.postMessage({urbePage:"ready"},"*")})();';
  var PREVIEW_CSS='[data-sid]{cursor:pointer;transition:outline-color .15s}[data-sid]:hover{outline:2px dashed color-mix(in srgb,var(--primary) 55%,transparent);outline-offset:-2px}.urbe-sel{outline:2px solid var(--primary)!important;outline-offset:-2px}.rv{opacity:1!important;transform:none!important}';

  /* ---------------- render ---------------- */
  function render(input,ctx){
    ctx=ctx||{};var s=normalize(input).spec,preview=!!ctx.preview;
    var docs=ctx.documents||null,anchors={};
    function noteAnchor(d){return 'nota-'+slug(d.title)}
    function note(path){if(!path||!docs)return null;var p=String(path).trim();return docs.get(p)||docs.get(p+'.md')||docs.list().find(function(d){return d.title.toLowerCase()===p.replace(/\.md$/i,'').toLowerCase()})||null}
    var included={};s.sections.forEach(function(sec){if(sec.style.hidden)return;if(sec.type==='notes'&&sec.props.expand)notesFor(sec.props).forEach(function(d){included[d.title.toLowerCase()]=noteAnchor(d)})});
    function wikilink(target,label){var key=String(target).replace(/\.md$/i,'').split('/').pop().toLowerCase(),a=included[key];return a?'<a class="wikilink" href="#'+esc(a)+'">'+esc(label||target)+'</a>':'<span class="wikilink">'+esc(label||String(target).split('/').pop())+'</span>'}
    function notesFor(p){if(!docs)return[];var all=docs.list().filter(function(d){return /\.(md|markdown|txt)$/i.test(d.path)}),out;
      if(p.source==='folder'){var f=String(p.folder||'').replace(/^\/+|\/+$/g,'').toLowerCase();out=all.filter(function(d){return !f||d.path.toLowerCase().indexOf(f+'/')===0})}
      else if(p.source==='tag'){var tg=String(p.tag||'').replace(/^#/,'').toLowerCase();out=all.filter(function(d){return (d.tags||[]).some(function(t){return t.toLowerCase()===tg})})}
      else if(p.source==='list'){out=String(p.paths||'').split('\n').map(function(x){return note(x)}).filter(Boolean)}
      else out=all.slice().sort(function(a,b){return String(b.modified||'').localeCompare(String(a.modified||''))});
      if(p.source!=='list'&&p.source!=='recent')out=out.slice().sort(p.sort==='modified'?function(a,b){return String(b.modified||'').localeCompare(String(a.modified||''))}:p.sort==='path'?function(a,b){return a.path.localeCompare(b.path)}:function(a,b){return a.title.localeCompare(b.title)});
      return out.slice(0,p.limit||24)}
    var md={wikilink:wikilink},toc=[];
    s.sections.forEach(function(sec){if(sec.style.hidden)return;var t=sec.props.title||(sec.type==='note'&&note(sec.props.path)||{}).title;var a=sec.style.anchor?slug(sec.style.anchor):(t?slug(t):'');if(a){while(anchors[a])a+='-2';anchors[a]=1}sec._anchor=a;if(t&&a)toc.push({id:sec.id,anchor:a,title:sec.style.anchor||t,menu:sec.style.menu})});
    var c={md:md,note:note,notes:notesFor,noteAnchor:noteAnchor,toc:function(){return toc},mdNote:function(d,shift){return{wikilink:wikilink,shift:shift||0,idPrefix:noteAnchor(d)+'-'}}};
    var body=s.sections.filter(function(sec){return !sec.style.hidden||preview}).map(function(sec){var b=BLOCKS[sec.type],st=sec.style,html;c.section=sec;
      try{html=b.render(sec.props,c,sec)}catch(e){html='<div class="missing">Erro ao montar “'+esc(b.label)+'”: '+esc(e.message)+'</div>'}
      var cls='sec b-'+sec.type+' p-'+st.padding+' w-'+st.width+' a-'+st.align+(st.background!=='none'?' bg-'+st.background:'')+(s.theme.animations&&sec.type!=='hero'?' rv':'')+(st.hidden?' is-hidden':'');
      var bgi=st.background==='image'&&safeUrl(st.image,true)?' style="background-image:url(\''+esc(safeUrl(st.image,true)).replace(/'/g,'%27')+'\')"':'';
      return '<section class="'+cls+'"'+(sec._anchor?' id="'+esc(sec._anchor)+'"':'')+(preview?' data-sid="'+esc(sec.id)+'"':'')+bgi+'><div class="wrap">'+html+'</div></section>'}).join('\n');
    var L=s.layout,M=s.meta,menu=toc.filter(function(t){return t.menu});
    var nav=L.nav?'<header class="nav'+(L.sticky?' sticky':'')+'"><div class="wrap"><a class="brand" href="#top">'+(M.icon?'<span>'+esc(M.icon)+'</span>':'')+esc(L.brand||M.title)+'</a><input type="checkbox" id="nav-t" aria-hidden="true"><label class="nav-burger" for="nav-t" aria-label="Menu"><span></span></label><nav class="nav-links">'+
      menu.map(function(t){return '<a href="#'+esc(t.anchor)+'">'+esc(t.title)+'</a>'}).join('')+(L.navCta?'<a class="btn btn-primary" href="'+esc(safeUrl(L.navCtaUrl)||'#')+'">'+esc(L.navCta)+'</a>':'')+'</nav></div></header>':'';
    var foot=L.footer?'<footer class="foot"><div class="wrap">'+markdown(L.footer,md)+'</div></footer>':'';
    var pr=THEMES[s.theme.preset]||THEMES.aurora,fonts=[fontOf(s.theme.headingFont,pr.fonts.heading),fontOf(s.theme.bodyFont,pr.fonts.body)].filter(function(f,i,a){return f.g&&a.indexOf(f)===i}).map(function(f){return 'family='+f.g});
    var mode=s.theme.mode||pr.mode,iconSvg="data:image/svg+xml,"+encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><text y=".9em" font-size="90">'+String(M.icon||'✦').replace(/[<>&]/g,'')+'</text></svg>');
    return '<!doctype html>\n<html lang="'+esc(M.lang||'pt-BR')+'" class="'+(mode==='dark'?'dark-default':mode==='light'?'light-default':'')+'">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n<title>'+esc(M.title)+'</title>\n'+
      (M.description?'<meta name="description" content="'+esc(M.description)+'">\n<meta property="og:description" content="'+esc(M.description)+'">\n':'')+'<meta property="og:title" content="'+esc(M.title)+'">\n'+(M.image&&safeUrl(M.image,true)&&!/^data:/.test(M.image)?'<meta property="og:image" content="'+esc(safeUrl(M.image,true))+'">\n':'')+
      '<meta name="generator" content="Urbe">\n<link rel="icon" href="'+esc(iconSvg)+'">\n'+(fonts.length?'<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>\n<link rel="stylesheet" href="https://fonts.googleapis.com/css2?'+fonts.join('&')+'&display=swap">\n':'')+
      '<style>'+css(s)+(preview?PREVIEW_CSS+'.is-hidden{opacity:.35}':'')+'</style>\n</head>\n<body id="top">\n'+(L.progress?'<div class="progress" aria-hidden="true"></div>':'')+nav+'<main>\n'+body+'\n</main>\n'+foot+
      (L.themeToggle?'<button class="fab fab-theme" type="button" aria-label="Alternar tema claro/escuro"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/></svg></button>':'')+(L.backToTop?'<button class="fab fab-top" type="button" aria-label="Voltar ao topo"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 19V5M5 12l7-7 7 7"/></svg></button>':'')+
      '\n<script>'+RUNTIME+(preview?PREVIEW:'')+'<\/script>\n</body>\n</html>\n';
  }

  /* ---------------- schema em texto (para o Assistente e para a ajuda) ---------------- */
  function fieldText(f,ind){var t=f.type==='select'?f.options.filter(function(o){return o!==''}).map(function(o){return JSON.stringify(o)}).join('|'):f.type==='list'?'lista de objetos':f.type==='markdown'?'texto Markdown':f.type==='note'?'caminho de nota':f.type==='image'?'URL de imagem':f.type;
    var s=ind+'- '+f.key+' ('+t+(f.required?', obrigatório':'')+(f.default!=null&&f.type!=='list'&&f.default!==''?', padrão '+JSON.stringify(f.default):'')+'): '+f.label;
    if(f.type==='list')s+='\n'+f.fields.map(function(x){return fieldText(x,ind+'    ')}).join('\n');return s}
  function schemaText(){
    return ['FORMATO DE PÁGINA DO URBE (arquivo .page.json)',
      '{"version":1,"kind":"urbe-page","meta":{...},"theme":{...},"layout":{...},"sections":[{"type":"<bloco>","props":{...},"style":{...}}]}',
      '','meta:',META_FIELDS.map(function(f){return fieldText(f,'  ')}).join('\n'),
      '','theme (tudo opcional; comece pelo preset):',THEME_FIELDS.map(function(f){return fieldText(f,'  ')}).join('\n'),
      '','layout:',LAYOUT_FIELDS.map(function(f){return fieldText(f,'  ')}).join('\n'),
      '','style de cada seção (opcional):',SECTION_FIELDS.map(function(f){return fieldText(f,'  ')}).join('\n'),
      '','BLOCOS (type → props):',Object.keys(BLOCKS).map(function(k){var b=BLOCKS[k];return '\n'+k+' — '+b.label+': '+b.description+'\n'+b.fields.map(function(f){return fieldText(f,'  ')}).join('\n')}).join('\n'),
      '','Dicas: textos longos em Markdown aceitam [[links]] para notas. Blocos "note" e "notes" puxam o conteúdo real das notas e se atualizam sozinhos. Para aparecer no menu da barra, dê "anchor" e "menu":true no style da seção.'].join('\n');
  }

  global.UrbePages={VERSION:VERSION,BLOCKS:BLOCKS,THEMES:THEMES,FONTS:FONTS,THEME_FIELDS:THEME_FIELDS,LAYOUT_FIELDS:LAYOUT_FIELDS,META_FIELDS:META_FIELDS,SECTION_FIELDS:SECTION_FIELDS,
    normalize:normalize,render:render,newSection:newSection,blank:blank,schemaText:schemaText,markdown:markdown,excerpt:excerpt,slug:slug,safeUrl:safeUrl,esc:esc,uid:uid,
    isPagePath:function(p){return /\.(page|template)\.json$/i.test(String(p||''))},isTemplatePath:function(p){return /\.template\.json$/i.test(String(p||''))}};
})(typeof window!=='undefined'?window:globalThis);
