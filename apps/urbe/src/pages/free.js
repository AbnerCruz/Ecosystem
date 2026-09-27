(function(global){
  'use strict';
  /* Layout livre das páginas: uma árvore de elementos (containers com filhos,
     títulos, parágrafos, imagens, botões…) em que cada peça tem o próprio estilo.
     Responsivo de verdade: cada estilo pode ter um valor para computador (base),
     tablet (até 900px) e celular (até 600px). Containers empilham, ficam lado a
     lado (e quebram linha sozinhos quando falta espaço) ou formam grade: nada de
     coordenadas fixas, então o arranjo se adapta a qualquer tela.
     Arquivo puro (sem DOM): valida, normaliza e gera HTML + CSS. */
  var P=global.UrbePages;if(!P)return;
  var esc=P.esc,F=P.F;

  /* ---------------- tipos de elemento ---------------- */
  var TYPES={
    box:{label:'Container',icon:'▢',box:true,content:[F('tag','Tipo','select',{options:['div','section','header','footer','article','aside','nav'],labels:['Caixa','Seção','Cabeçalho','Rodapé','Artigo','Lateral','Navegação'],default:'div'}),F('link','Link (a caixa inteira vira link)','url')]},
    heading:{label:'Título',icon:'H',text:true,content:[F('text','Texto','text',{default:'Um título'}),F('level','Nível','select',{options:[1,2,3,4,5,6],labels:['H1','H2','H3','H4','H5','H6'],default:2})]},
    text:{label:'Parágrafo',icon:'¶',text:true,content:[F('text','Texto (Markdown)','markdown',{default:'Escreva aqui.'})]},
    image:{label:'Imagem',icon:'◐',content:[F('src','Imagem','image'),F('alt','Descrição (para leitores de tela)','text'),F('caption','Legenda','text'),F('link','Link','url')]},
    button:{label:'Botão',icon:'⬭',content:[F('label','Texto','text',{default:'Saiba mais'}),F('url','Link','url',{default:'#'}),F('variant','Estilo','select',{options:['primary','secondary','ghost'],labels:['Principal','Secundário','Discreto'],default:'primary'})]},
    list:{label:'Lista',icon:'☰',content:[F('items','Itens (um por linha)','textarea',{default:'Primeiro item\nSegundo item'}),F('ordered','Numerada','boolean',{default:false})]},
    quote:{label:'Citação',icon:'❝',content:[F('text','Citação','textarea',{default:'Uma frase marcante.'}),F('author','Autor','text')]},
    icon:{label:'Ícone',icon:'★',content:[F('emoji','Emoji ou símbolo','text',{default:'✨'})]},
    divider:{label:'Divisor',icon:'—',content:[]},
    spacer:{label:'Espaço',icon:'↕',content:[]},
    video:{label:'Vídeo',icon:'▶',content:[F('url','Link do vídeo (YouTube, Vimeo, .mp4)','url')]},
    note:{label:'Nota do vault',icon:'▤',content:[F('path','Nota','note'),F('showTitle','Mostrar título','boolean',{default:false})]},
    html:{label:'HTML livre',icon:'{ }',content:[F('code','HTML','code',{default:'<div>Olá</div>'})]},
    pagebreak:{label:'Quebra de página (livro)',icon:'⤓',content:[]}
  };

  /* ---------------- estilos (todos opcionais, todos validados) ---------------- */
  var LEN='(-?(\\d+(\\.\\d+)?|\\.\\d+)(px|rem|em|%|vw|vh|svh|dvh|ch)?|auto|0)',LENS=new RegExp('^'+LEN+'(\\s+'+LEN+'){0,3}$'),ONE=new RegExp('^'+LEN+'$');
  function opt(key,label,options,labels,group){return{key:key,label:label,type:'select',options:['',].concat(options),labels:['—'].concat(labels),group:group}}
  function len(key,label,group,many,hint){return{key:key,label:label,type:'length',many:!!many,group:group,hint:hint}}
  var STYLE=[
    opt('display','Arranjo dos filhos',['stack','row','grid','none'],['Empilhados','Lado a lado','Grade','Escondido'],'layout'),
    len('gap','Espaço entre os filhos','layout'),
    {key:'columns',label:'Colunas da grade (0 = automático)',type:'number',min:0,max:12,group:'layout'},
    len('minCol','Largura mínima de cada filho (quebra linha abaixo disso)','layout',false,'Ex.: 220px'),
    opt('justify','Distribuir no eixo principal',['start','center','end','between','around','evenly'],['Início','Centro','Fim','Espalhar','Em volta','Igual'],'layout'),
    opt('align','Alinhar no outro eixo',['stretch','start','center','end','baseline'],['Esticar','Início','Centro','Fim','Linha de base'],'layout'),
    opt('self','Este elemento no container',['auto','start','center','end','stretch'],['Automático','Início','Centro','Fim','Esticar'],'layout'),
    {key:'order',label:'Ordem (muda a posição sem mexer na árvore)',type:'number',min:-10,max:10,group:'layout'},
    len('width','Largura','size',false,'auto, 100%, 320px, 50vw…'),len('maxWidth','Largura máxima','size'),len('minHeight','Altura mínima','size'),len('height','Altura','size'),
    opt('aspect','Proporção',['1/1','4/3','3/2','16/9','21/9','3/4','2/3','9/16'],['Quadrado','4:3','3:2','16:9','21:9','3:4','2:3','9:16'],'size'),
    opt('fit','Imagem no espaço',['cover','contain'],['Preencher (corta)','Caber inteira'],'size'),
    len('padding','Espaço interno (1 a 4 valores)','space',true,'Ex.: 24px ou 16px 32px'),len('margin','Espaço externo (1 a 4 valores)','space',true,'Ex.: 0 auto para centralizar'),
    {key:'bg',label:'Cor de fundo',type:'color',group:'look'},{key:'bg2',label:'Segunda cor (degradê)',type:'color',group:'look'},
    {key:'angle',label:'Ângulo do degradê',type:'number',min:0,max:360,group:'look'},
    {key:'bgImage',label:'Imagem de fundo',type:'image',group:'look'},
    {key:'color',label:'Cor do texto',type:'color',group:'look'},
    len('border','Espessura da borda','look'),opt('borderStyle','Tipo de borda',['solid','dashed','dotted','double'],['Linha','Tracejada','Pontilhada','Dupla'],'look'),{key:'borderColor',label:'Cor da borda',type:'color',group:'look'},
    len('radius','Cantos arredondados (1 a 4 valores)','look',true),opt('shadow','Sombra',['none','soft','strong','glow'],['Nenhuma','Suave','Forte','Brilho'],'look'),
    {key:'opacity',label:'Opacidade',type:'number',min:0,max:1,step:.05,group:'look'},
    opt('font','Fonte',['heading','body'].concat(Object.keys(P.FONTS)),['Dos títulos','Do texto'].concat(Object.keys(P.FONTS).map(function(k){return P.FONTS[k].label})),'type'),
    len('size','Tamanho da letra','type',false,'Ex.: 18px, 1.4rem, 5vw'),opt('weight','Peso da letra',['300','400','500','600','700','800','900'],['Fino','Normal','Médio','Semi','Negrito','Forte','Pesado'],'type'),
    {key:'lineHeight',label:'Altura da linha',type:'number',min:.8,max:3,step:.05,group:'type'},len('spacing','Espaço entre letras','type',false,'Ex.: .05em'),
    opt('textAlign','Alinhamento do texto',['left','center','right','justify'],['Esquerda','Centro','Direita','Justificado'],'type'),
    opt('transform','Maiúsculas',['none','uppercase','lowercase','capitalize'],['Normal','MAIÚSCULAS','minúsculas','Primeira Letra'],'type'),
    opt('italic','Itálico',['yes','no'],['Sim','Não'],'type'),
    {key:'className',label:'Classe CSS',type:'text',group:'adv'},{key:'css',label:'CSS deste elemento (& = o elemento)',type:'code',group:'adv'}
  ];
  var STYLE_BY={};STYLE.forEach(function(f){STYLE_BY[f.key]=f});
  var GROUPS=[['layout','Arranjo e alinhamento'],['size','Tamanho'],['space','Espaçamento'],['look','Fundo, borda e sombra'],['type','Texto'],['adv','Avançado']];
  var BREAKS={tablet:900,mobile:600};

  function cleanStyle(src,path,rep){var out={};if(!src||typeof src!=='object'||Array.isArray(src))return out;
    Object.keys(src).forEach(function(k){var f=STYLE_BY[k],v=src[k];if(v==null||v==='')return;if(!f){rep.warn(path+'.'+k,'estilo desconhecido, ignorado.');return}
      if(f.type==='select'){if(f.options.indexOf(String(v))>0)out[k]=String(v);else rep.err(path+'.'+k,'valor "'+v+'" inválido; use um de: '+f.options.slice(1).join(', ')+'.');return}
      if(f.type==='number'){var n=Number(v);if(isFinite(n))out[k]=Math.max(f.min,Math.min(f.max,n));else rep.err(path+'.'+k,'deve ser um número.');return}
      if(f.type==='length'){v=String(v).trim().replace(/\s+/g,' ');if((f.many?LENS:ONE).test(v))out[k]=v;else rep.err(path+'.'+k,'medida inválida "'+v+'" (use px, rem, em, %, vw, vh ou auto).');return}
      if(f.type==='color'){v=String(v).trim();if(P.COLOR_RE.test(v))out[k]=v;else rep.err(path+'.'+k,'cor inválida "'+v+'".');return}
      /* a URL entra no CSS: aspas, parênteses, espaços e < > são codificados (não dá para sair do url(...) nem do <style>) */
      if(f.type==='image'){var u=P.safeUrl(v,true);if(u)out[k]=u.replace(/[\s'"()<>\\]/g,function(ch){return '%'+ch.charCodeAt(0).toString(16).toUpperCase().padStart(2,'0')});return}
      if(k==='className'){out[k]=String(v).replace(/[^\w\s-]/g,'').trim().slice(0,80);return}
      out[k]=String(v).slice(0,4000)});
    return out}
  var seq=0;function nid(){seq=(seq+1)%1e6;return 'n'+Date.now().toString(36).slice(-4)+seq.toString(36)+Math.random().toString(36).slice(2,4)}
  function cleanContent(type,src){var out={},c=src&&typeof src==='object'?src:{};TYPES[type].content.forEach(function(f){var v=c[f.key];
    if(v==null||v===''){if(f.default!=null)out[f.key]=f.default;return}
    if(f.type==='boolean')out[f.key]=!!v;else if(f.type==='select')out[f.key]=f.options.some(function(o){return String(o)===String(v)})?f.options.find(function(o){return String(o)===String(v)}):f.default;
    else out[f.key]=String(v)});return out}
  /* normaliza a árvore: tipos válidos, ids únicos, só containers têm filhos, profundidade e tamanho limitados */
  function normalizeTree(v,path,rep){var ids={},count=0;
    function node(x,p,depth){if(!x||typeof x!=='object'||Array.isArray(x)){rep.err(p,'cada elemento deve ser um objeto {type, …}.');return null}
      var type=TYPES[x.type]?x.type:null;if(!type){rep.err(p+'.type','tipo "'+x.type+'" não existe. Tipos: '+Object.keys(TYPES).join(', ')+'.');return null}
      if(++count>1500){rep.err(p,'elementos demais (máximo 1500).');return null}
      var id=typeof x.id==='string'&&/^[\w-]{1,24}$/.test(x.id)&&!ids[x.id]?x.id:nid();ids[id]=1;
      var o={id:id,type:type};var c=cleanContent(type,x.content);if(Object.keys(c).length)o.content=c;
      var s=cleanStyle(x.style,p+'.style',rep);if(Object.keys(s).length)o.style=s;
      ['tablet','mobile'].forEach(function(b){var r=cleanStyle(x[b],p+'.'+b,rep);if(Object.keys(r).length)o[b]=r});
      if(TYPES[type].box){var kids=Array.isArray(x.children)?x.children:[];if(depth>24){rep.err(p,'aninhamento fundo demais.');kids=[]}
        o.children=kids.map(function(k,i){return node(k,p+'.children['+i+']',depth+1)}).filter(Boolean)}
      else if(x.children&&x.children.length)rep.warn(p+'.children','só containers têm filhos; ignorados.');
      return o}
    if(v==null||v==='')return starter();
    var r=node(v,path,0);if(!r)return starter();if(r.type!=='box'){r={id:nid(),type:'box',children:[r]}}return r}

  /* ---------------- CSS de cada elemento ---------------- */
  var SHADOW={none:'none',soft:'0 1px 2px rgba(0,0,0,.08),0 10px 30px -14px rgba(0,0,0,.35)',strong:'0 3px 6px rgba(0,0,0,.14),0 24px 50px -18px rgba(0,0,0,.6)',glow:'0 0 0 1px color-mix(in srgb,var(--primary) 40%,transparent),0 10px 40px -6px color-mix(in srgb,var(--primary) 55%,transparent)'};
  var JUST={start:'flex-start',center:'center',end:'flex-end',between:'space-between',around:'space-around',evenly:'space-evenly'};
  function decl(s,box){var d=[];if(!s)return '';
    if(s.display==='none')d.push('display:none');
    else if(box&&s.display==='row')d.push('display:flex;flex-direction:row;flex-wrap:wrap');
    else if(box&&s.display==='grid')d.push('display:grid;grid-template-columns:'+(s.columns?'repeat('+s.columns+',minmax(0,1fr))':'repeat(auto-fit,minmax(min(100%,'+(s.minCol||'220px')+'),1fr))'));
    else if(box&&s.display==='stack')d.push('display:flex;flex-direction:column;flex-wrap:nowrap');
    /* só as colunas mudaram neste tamanho de tela (a grade vem de um tamanho maior) */
    if(box&&s.display!=='grid'&&s.display!=='row'&&s.display!=='stack'&&s.display!=='none'&&(s.columns!=null||s.minCol))d.push('grid-template-columns:'+(s.columns?'repeat('+s.columns+',minmax(0,1fr))':'repeat(auto-fit,minmax(min(100%,'+s.minCol+'),1fr))'));
    if(s.gap)d.push('gap:'+s.gap);if(s.minCol)d.push('--fx-basis:'+s.minCol);
    if(s.justify)d.push('justify-content:'+JUST[s.justify]);if(s.align)d.push('align-items:'+(JUST[s.align]||s.align));
    if(s.self)d.push('align-self:'+(JUST[s.self]||s.self));if(s.order!=null)d.push('order:'+s.order);
    if(s.width)d.push('width:'+(s.width==='full'?'100%':s.width)+';flex-basis:'+(s.width==='auto'?'auto':s.width)+';flex-grow:0');
    if(s.maxWidth)d.push('max-width:'+s.maxWidth);if(s.minHeight)d.push('min-height:'+s.minHeight);if(s.height)d.push('height:'+s.height);
    if(s.aspect)d.push('aspect-ratio:'+s.aspect);if(s.fit)d.push('--fx-fit:'+s.fit);
    if(s.padding)d.push('padding:'+s.padding);if(s.margin)d.push('margin:'+s.margin);
    var bgs=[];if(s.bg&&s.bg2)bgs.push('linear-gradient('+(s.angle!=null?s.angle:135)+'deg,'+s.bg+','+s.bg2+')');if(s.bgImage)bgs.push("url('"+String(s.bgImage).replace(/[\s'"()<>\\]/g,encodeURIComponent)+"') center/cover no-repeat");
    if(bgs.length)d.push('background:'+bgs.join(',')+(s.bg&&!s.bg2?' '+s.bg:''));else if(s.bg)d.push('background:'+s.bg);
    if(s.color)d.push('color:'+s.color+';--text:'+s.color+';--muted:color-mix(in srgb,'+s.color+' 72%,transparent)');
    if(s.border)d.push('border:'+s.border+' '+(s.borderStyle||'solid')+' '+(s.borderColor||'var(--border)'));else{if(s.borderStyle)d.push('border-style:'+s.borderStyle);if(s.borderColor)d.push('border-color:'+s.borderColor)}
    if(s.radius)d.push('border-radius:'+s.radius);if(s.shadow)d.push('box-shadow:'+SHADOW[s.shadow]);if(s.opacity!=null)d.push('opacity:'+s.opacity);
    if(s.font)d.push('font-family:'+(s.font==='heading'?'var(--fh)':s.font==='body'?'var(--fb)':P.FONTS[s.font].stack));
    if(s.size)d.push('font-size:'+s.size);if(s.weight)d.push('font-weight:'+s.weight);if(s.lineHeight!=null)d.push('line-height:'+s.lineHeight);
    if(s.spacing)d.push('letter-spacing:'+s.spacing);if(s.textAlign)d.push('text-align:'+s.textAlign);if(s.transform)d.push('text-transform:'+s.transform);
    if(s.italic)d.push('font-style:'+(s.italic==='yes'?'italic':'normal'));
    return d.join(';')}
  function nodeCss(n,out,pre){var sel='.fx-'+pre+n.id,box=!!TYPES[n.type].box;
    var b=decl(n.style,box);if(b)out.base.push(sel+'{'+b+'}');
    if(n.tablet){var t=decl(n.tablet,box);if(t)out.tablet.push(sel+'{'+t+'}')}
    if(n.mobile){var m=decl(n.mobile,box);if(m)out.mobile.push(sel+'{'+m+'}')}
    /* filhos de "lado a lado" dividem a linha e quebram sozinhos; em "empilhados"/"grade" voltam ao normal */
    [n.style,n.tablet,n.mobile].forEach(function(s,i){if(!s||!box)return;var dst=i===0?out.base:i===1?out.tablet:out.mobile;
      if(s.display==='row')dst.push(sel+'>*{flex:1 1 var(--fx-basis,220px)}');else if(s.display==='stack'||s.display==='grid')dst.push(sel+'>*{flex:0 1 auto}')});
    [n.style,n.tablet,n.mobile].forEach(function(s,i){if(s&&s.css){var c=P.cssSafe(s.css);c=/[{}]/.test(c)?c.replace(/&/g,sel):sel+'{'+c+'}';(i===0?out.base:i===1?out.tablet:out.mobile).push(c)}});
    (n.children||[]).forEach(function(k){nodeCss(k,out,pre)})}
  /* base com :where() (prioridade zero): o estilo de cada peça sempre vence, em qualquer ordem */
  var FREE_CSS=':where(.fx){box-sizing:border-box;min-width:0}:where(.fx-box){display:flex;flex-direction:column;gap:16px}:where(.fx-box>.fx){max-width:100%}'+
    ':where(.btn.fx){width:fit-content}:where(.sheet>.fx-free){height:100%}:where(.sheet>.fx-free>.fx-box){min-height:100%}'+
    '.fx-text>:first-child{margin-top:0}.fx-text>:last-child{margin-bottom:0}.fx-text p{margin:0 0 .8em}'+
    '.fx h1,.fx h2,.fx h3,.fx h4,.fx h5,.fx h6,h1.fx,h2.fx,h3.fx,h4.fx,h5.fx,h6.fx{margin:0}'+
    '.fx-img{margin:0;display:block}.fx-img img{width:100%;height:100%;object-fit:var(--fx-fit,cover);border-radius:inherit;display:block}.fx-img figcaption{font-size:.85em;color:var(--muted);margin-top:.5em}'+
    '.fx-list{margin:0;padding-left:1.3em}.fx-quote{margin:0;padding:0 0 0 1em;border-left:3px solid var(--primary);font-style:italic}.fx-quote cite{display:block;font-style:normal;font-size:.85em;color:var(--muted);margin-top:.4em}'+
    '.fx-divider{border:0;border-top:1px solid var(--border);margin:0;width:100%}.fx-spacer{height:32px}.fx-icon{font-size:2rem;line-height:1}'+
    'a.fx-box{color:inherit;text-decoration:none}.fx-video{position:relative;width:100%;aspect-ratio:16/9}.fx-video iframe,.fx-video video{position:absolute;inset:0;width:100%;height:100%;border:0;border-radius:inherit}'+
    '.fx-break{height:0}@media print{.fx-break{break-before:page;page-break-before:always}}';

  /* ---------------- HTML de cada elemento ---------------- */
  function renderNode(n,c,prev,pre){var ct=n.content||{},T=TYPES[n.type],cls='fx fx-'+pre+n.id+(n.style&&n.style.className?' '+esc(n.style.className):''),
    at=' class="'+cls+'"',da=prev?' data-nid="'+esc(n.id)+'"':'';
    switch(n.type){
      case 'box':{var tag=ct.tag||'div',rowx='',inner=(n.children||[]).map(function(k){return renderNode(k,c,prev,pre)}).join('');
        var u=ct.link&&P.safeUrl(ct.link);if(u)return '<a href="'+esc(u)+'" class="'+cls+' fx-box'+rowx+'"'+da+'>'+inner+'</a>';
        return '<'+tag+' class="'+cls+' fx-box'+rowx+'"'+da+'>'+inner+'</'+tag+'>'}
      case 'heading':{var lv=Math.max(1,Math.min(6,+ct.level||2)),plain=!/[*_`\[\]#>|~=]/.test(ct.text||'');return '<h'+lv+at+da+(prev&&plain?' data-edit="1"':'')+'>'+P.inline(ct.text||'',c.md)+'</h'+lv+'>'}
      case 'text':{var pl=!/[*_`\[\]#>|~=-]/.test(ct.text||'');return '<div class="'+cls+' fx-text"'+da+(prev&&pl?' data-edit="1"':'')+'>'+P.markdown(ct.text||'',c.md)+'</div>'}
      case 'image':{var src=P.safeUrl(ct.src,true);var im=src?'<img src="'+esc(src)+'" alt="'+esc(ct.alt||'')+'" loading="lazy">':'<div class="missing">Escolha uma imagem</div>';var lk=ct.link&&P.safeUrl(ct.link);
        return '<figure class="'+cls+' fx-img"'+da+'>'+(lk?'<a href="'+esc(lk)+'">'+im+'</a>':im)+(ct.caption?'<figcaption>'+P.inline(ct.caption,c.md)+'</figcaption>':'')+'</figure>'}
      case 'button':{var bu=P.safeUrl(ct.url)||'#';return '<a class="'+cls+' btn btn-'+esc(ct.variant||'primary')+'" href="'+esc(bu)+'"'+(/^https?:/i.test(bu)?' target="_blank" rel="noopener"':'')+da+'>'+esc(ct.label||'')+'</a>'}
      case 'list':{var tg=ct.ordered?'ol':'ul';return '<'+tg+' class="'+cls+' fx-list"'+da+'>'+String(ct.items||'').split('\n').filter(function(x){return x.trim()}).map(function(x){return '<li>'+P.inline(x.replace(/^\s*([-*+]|\d+[.)])\s+/,''),c.md)+'</li>'}).join('')+'</'+tg+'>'}
      case 'quote':return '<blockquote class="'+cls+' fx-quote"'+da+'>'+P.markdown(ct.text||'',c.md)+(ct.author?'<cite>— '+P.inline(ct.author,c.md)+'</cite>':'')+'</blockquote>';
      case 'icon':return '<span class="'+cls+' fx-icon"'+da+'>'+esc(ct.emoji||'')+'</span>';
      case 'divider':return '<hr class="'+cls+' fx-divider"'+da+'>';
      case 'spacer':return '<div class="'+cls+' fx-spacer"'+da+' aria-hidden="true"></div>';
      case 'video':{var v=P.BLOCKS.video?P.BLOCKS.video.render({url:ct.url,caption:''},c):'';return '<div class="'+cls+' fx-video-wrap"'+da+'>'+(ct.url?v:'<div class="missing">Cole o link de um vídeo</div>')+'</div>'}
      case 'note':{var d=c.note(ct.path);if(!d)return '<div class="'+cls+' missing"'+da+'>Nota não encontrada: '+esc(ct.path||'(escolha uma nota)')+'</div>';
        return '<div class="'+cls+' fx-text"'+da+'>'+(ct.showTitle?'<h2>'+esc(d.title)+'</h2>':'')+P.markdown(P.stripTitle(d.content,d.title,true),c.mdNote(d,ct.showTitle?1:0))+'</div>'}
      case 'html':return '<div class="'+cls+'"'+da+'>'+String(ct.code||'')+'</div>';
      case 'pagebreak':return '<div class="'+cls+' fx-break"'+da+' aria-hidden="true"></div>';
    }return ''}
  function render(root,c){var pre=(c.section&&c.section.id?String(c.section.id).replace(/[^\w-]/g,''):'x')+'-',out={base:[],tablet:[],mobile:[]};nodeCss(root,out,pre);
    c.addCss((c._fxBase?'':(c._fxBase=FREE_CSS))+out.base.join('')+(out.tablet.length?'@media (max-width:'+BREAKS.tablet+'px){'+out.tablet.join('')+'}':'')+(out.mobile.length?'@media (max-width:'+BREAKS.mobile+'px){'+out.mobile.join('')+'}':''));
    return '<div class="fx-free">'+renderNode(root,c,c.preview,pre)+'</div>'}

  /* ---------------- árvore: utilidades usadas pelo estúdio ---------------- */
  function make(type,content,style,children){var n={id:nid(),type:type};var c=cleanContent(type,content||{});if(Object.keys(c).length)n.content=c;if(style)n.style=style;if(TYPES[type].box)n.children=children||[];return n}
  function starter(){return make('box',{},{padding:'24px 0',gap:'16px'},[make('heading',{text:'Um título',level:2}),make('text',{text:'Toque num elemento da prévia para editar. Toque de novo num texto para escrever direto nele.'})])}
  function walk(n,fn,parent,depth){if(fn(n,parent,depth||0)===false)return false;var k=n.children||[];for(var i=0;i<k.length;i++)if(walk(k[i],fn,n,(depth||0)+1)===false)return false}
  function find(root,id){var hit=null;walk(root,function(n,p){if(n.id===id){hit={node:n,parent:p};return false}});return hit}
  function reid(n){n.id=nid();(n.children||[]).forEach(reid);return n}
  function clone(n){return reid(JSON.parse(JSON.stringify(n)))}

  /* composições prontas (atalhos para montar rápido) */
  var PRESETS={
    'duas-colunas':{label:'Duas colunas',icon:'◫',build:function(){return make('box',{},{display:'row',gap:'24px',minCol:'260px'},[make('box',{},{gap:'12px'},[make('heading',{text:'Coluna 1',level:3}),make('text',{text:'Texto da primeira coluna.'})]),make('box',{},{gap:'12px'},[make('heading',{text:'Coluna 2',level:3}),make('text',{text:'Texto da segunda coluna.'})])])}},
    'imagem-texto':{label:'Imagem + texto',icon:'◧',build:function(){return make('box',{},{display:'row',gap:'28px',align:'center',minCol:'260px'},[make('image',{alt:''},{radius:'16px',aspect:'4/3'}),make('box',{},{gap:'12px'},[make('heading',{text:'Um título forte',level:2}),make('text',{text:'Uma explicação curta ao lado da imagem.'}),make('button',{label:'Saiba mais'})])])}},
    'grade-cartoes':{label:'Grade de cartões',icon:'▦',build:function(){function card(t){return make('box',{},{padding:'22px',radius:'16px',bg:'var(--surface)',border:'1px',shadow:'soft',gap:'8px'},[make('icon',{emoji:'✨'}),make('heading',{text:t,level:3}),make('text',{text:'Uma frase sobre isto.'})])}
      return make('box',{},{display:'grid',gap:'18px',minCol:'220px'},[card('Primeiro'),card('Segundo'),card('Terceiro')])}},
    'capa':{label:'Capa centralizada',icon:'✦',build:function(){return make('box',{tag:'header'},{minHeight:'60vh',justify:'center',align:'center',textAlign:'center',gap:'18px',padding:'48px 20px'},[make('heading',{text:'Um título grande',level:1}),make('text',{text:'Uma frase que convida a continuar.'}),make('button',{label:'Começar'})])}},
    'cartao':{label:'Cartão',icon:'▭',build:function(){return make('box',{},{padding:'24px',radius:'18px',bg:'var(--surface)',border:'1px',shadow:'soft',gap:'10px',maxWidth:'520px'},[make('heading',{text:'Título do cartão',level:3}),make('text',{text:'Conteúdo do cartão.'})])}},
    'destaque':{label:'Caixa de destaque',icon:'❗',build:function(){return make('box',{},{padding:'18px 22px',radius:'12px',bg:'color-mix(in srgb,var(--primary) 12%,transparent)',border:'0 0 0 4px',gap:'6px'},[make('heading',{text:'Importante',level:4}),make('text',{text:'Um aviso ou uma ideia que merece destaque.'})])}},
    'pagina-livro':{label:'Página de livro',icon:'📄',build:function(){return make('box',{},{gap:'14px'},[make('heading',{text:'Título da página',level:2},{textAlign:'center',margin:'0 0 12px'}),make('text',{text:'Primeiro parágrafo.'}),make('text',{text:'Segundo parágrafo.'})])}}
  };
  /* "destaque" usa borda só à esquerda: a medida de 4 valores não serve para border; corrige */
  PRESETS.destaque.build=function(){var b=make('box',{},{padding:'18px 22px',radius:'12px',bg:'color-mix(in srgb,var(--primary) 12%,transparent)',gap:'6px',css:'border-left:4px solid var(--primary)'},[make('heading',{text:'Importante',level:4}),make('text',{text:'Um aviso ou uma ideia que merece destaque.'})]);return b};
  PRESETS['grade-cartoes'].build=function(){function card(t){return make('box',{},{padding:'22px',radius:'16px',bg:'var(--surface)',border:'1px',shadow:'soft',gap:'8px'},[make('icon',{emoji:'✨'}),make('heading',{text:t,level:3}),make('text',{text:'Uma frase sobre isto.'})])}
    return make('box',{},{display:'grid',gap:'18px',minCol:'220px'},[card('Primeiro'),card('Segundo'),card('Terceiro')])};
  /* bg com var()/color-mix não passa em COLOR_RE: as composições usam CSS do elemento para isso */
  function fixVarColors(n){['style','tablet','mobile'].forEach(function(k){var s=n[k];if(!s)return;['bg'].forEach(function(p){if(s[p]&&!P.COLOR_RE.test(s[p])){s.css=(s.css?s.css+';':'')+'background:'+s[p];delete s[p]}})});(n.children||[]).forEach(fixVarColors);return n}
  Object.keys(PRESETS).forEach(function(k){var b=PRESETS[k].build;PRESETS[k].build=function(){return fixVarColors(b())}});

  /* ---------------- converter blocos fechados em layout livre ---------------- */
  function paragraphs(md){var out=[],buf=[];String(md||'').replace(/\r\n?/g,'\n').split(/\n{2,}/).forEach(function(par){var t=par.trim();if(!t)return;var m=t.match(/^(#{1,6})\s+(.+)$/);
      if(m&&t.indexOf('\n')<0)out.push(make('heading',{text:m[2],level:Math.min(6,m[1].length+1)}));
      else if(/^(\*\s*){3}$|^-{3,}$/.test(t))out.push(make('divider'));
      else if(/^>\s?/.test(t)&&!/^>\s*\[!/.test(t))out.push(make('quote',{text:t.replace(/^>\s?/gm,'')}));
      else if(/^!\[[^\]]*\]\([^)]+\)$/.test(t)){var im=t.match(/^!\[([^\]]*)\]\(([^)\s]+)/);out.push(make('image',{src:im[2],alt:im[1]}))}
      else out.push(make('text',{text:t}))});return out}
  function fromBlock(sec,c){var p=sec.props||{},t=sec.type,kids=[];
    if(t==='text')return make('box',{},{gap:'14px'},(p.title?[make('heading',{text:p.title,level:2})]:[]).concat(paragraphs(p.markdown)));
    if(t==='hero'){if(p.eyebrow)kids.push(make('text',{text:p.eyebrow},{transform:'uppercase',spacing:'.12em',size:'.85rem',weight:'700'}));kids.push(make('heading',{text:p.title,level:1}));if(p.subtitle)kids=kids.concat(paragraphs(p.subtitle));
      if(p.image)kids.push(make('image',{src:p.image,alt:p.title},{radius:'16px'}));var bs=(p.buttons||[]).filter(function(b){return b.label});
      if(bs.length)kids.push(make('box',{},{display:'row',gap:'12px',minCol:'160px'},bs.map(function(b){return make('button',{label:b.label,url:b.url,variant:b.variant})})));
      return make('box',{tag:'header'},{gap:'18px',align:p.layout==='center'?'center':'stretch',textAlign:p.layout==='center'?'center':null,minHeight:p.height==='screen'?'100vh':p.height==='tall'?'70vh':null,justify:'center'},kids)}
    if(t==='quote')return make('box',{},{},[make('quote',{text:p.text,author:[p.author,p.role].filter(Boolean).join(', ')})]);
    if(t==='image')return make('box',{},{},[make('image',{src:p.src||p.image,alt:p.alt||p.caption,caption:p.caption})]);
    if(t==='bookcover')return make('box',{tag:'header'},{minHeight:'100%',padding:'12% 10%',gap:'14px',textAlign:'center',align:'center',bg:'#7a2e1f',color:'#f6efe2'},[make('heading',{text:p.title,level:1},{size:'2.6em'}),p.subtitle?make('text',{text:'*'+p.subtitle+'*'}):null,make('spacer',{},{css:'flex:1'}),make('text',{text:p.author},{transform:'uppercase',spacing:'.18em'}),p.publisher?make('text',{text:p.publisher},{size:'.78em',transform:'uppercase',spacing:'.14em',opacity:.75}):null].filter(Boolean));
    if(t==='titlepage')return make('box',{},{minHeight:'100%',gap:'14px',textAlign:'center',align:'center',justify:'between'},[make('text',{text:p.author},{transform:'uppercase',spacing:'.18em'}),make('box',{},{gap:'8px',align:'center'},[make('heading',{text:p.title,level:1}),p.subtitle?make('text',{text:'*'+p.subtitle+'*'}):null].filter(Boolean)),make('text',{text:[p.publisher,[p.place,p.year].filter(Boolean).join(' · ')].filter(Boolean).join('\n\n')},{size:'.85em'})]);
    if(t==='dedication'||t==='copyright'||t==='colophon'||t==='about'||t==='part')return make('box',{},{gap:'12px',justify:t==='copyright'||t==='colophon'?'end':'center',minHeight:'100%',textAlign:t==='dedication'||t==='part'||t==='colophon'?'center':null},(p.title?[make('heading',{text:(t==='part'?'Parte '+(sec._num||'')+' · ':'')+p.title,level:2})]:[]).concat(paragraphs(p.markdown)).concat(p.author?[make('text',{text:'— '+p.author})]:[]));
    if(t==='chapter'){var body=p.source==='note'&&c&&c.note(p.path)?(function(){var d=c.note(p.path);return P.stripTitle(d.content,d.title,true)})():p.markdown;
      return make('box',{tag:'article'},{gap:'14px'},[make('heading',{text:p.title||(c&&c.note(p.path)||{}).title||'Capítulo',level:2},{textAlign:'center',margin:'8% 0 6%'})].concat(p.epigraph?[make('quote',{text:p.epigraph,author:p.epigraphAuthor})]:[]).concat(paragraphs(body)))}
    if(t==='note'&&c){var d=c.note(p.path);return make('box',{tag:'article'},{gap:'14px'},(d&&p.showTitle?[make('heading',{text:d.title,level:1})]:[]).concat(paragraphs(d?P.stripTitle(d.content,d.title,true):'')))}
    return null}
  function cleanNull(n){if(n.style)Object.keys(n.style).forEach(function(k){if(n.style[k]==null)delete n.style[k]});(n.children||[]).forEach(cleanNull);return fixVarColors(n)}
  var CONVERTIBLE=['text','hero','quote','image','bookcover','titlepage','dedication','copyright','colophon','about','part','chapter','note'];

  /* ---------------- o bloco ---------------- */
  P.block('free',{label:'Layout livre',icon:'⊞',group:'Livre',description:'Monte peça por peça: containers (empilhados, lado a lado ou em grade) com títulos, parágrafos, imagens, botões e mais. Cada peça tem o próprio estilo, com valores diferentes para computador, tablet e celular.',
    fields:[F('root','Elementos','tree',{coerce:function(v,path,rep){return normalizeTree(v,path,rep)}}),
      F('sheet','No livro','select',{options:['flow','page','full'],labels:['Continua o texto','Folha própria','Página inteira'],default:'page'})],
    render:function(p,c){var html=render(p.root||starter(),c);
      if(c.book&&c.bookFormat)return '<div class="sheet'+(p.sheet==='flow'?' flow':' bk-free')+(p.sheet==='full'?' bk-full bk-bleed':'')+'">'+html+'</div>';
      return html}});

  P.free={TYPES:TYPES,STYLE:STYLE,STYLE_BY:STYLE_BY,GROUPS:GROUPS,BREAKS:BREAKS,PRESETS:PRESETS,CONVERTIBLE:CONVERTIBLE,
    make:make,starter:starter,walk:walk,find:find,clone:clone,nid:nid,normalizeTree:normalizeTree,
    fromBlock:function(sec,c){var n=fromBlock(sec,c);return n?cleanNull(n):null},paragraphs:paragraphs};
})(typeof window!=='undefined'?window:globalThis);
