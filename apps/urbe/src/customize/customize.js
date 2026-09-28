(function(global){
  'use strict';
  /* Personalização do Urbe.
     Tudo mora em arquivos do vault, na pasta "Personalização/":
       tema.json            → aparência, texto, forma, editor e cidade (este módulo)
       temas/*.json         → temas salvos (cores prontas para reaplicar ou compartilhar)
       estilos/*.css        → trechos de CSS ligáveis um a um
       texturas/*.json      → pacotes de texturas do chão e desenhos das construções
       plugins/*.js         → plugins (src/customize/plugins.js)
     O que vale para a tela (cores, fontes, tamanhos) também fica guardado neste aparelho
     para o app já abrir com a cara certa, antes de o vault carregar. */
  var core=global.UrbeCore,doc=global.document;if(!core||!doc)return;
  var docs=core.service('documents'),root=doc.documentElement;
  var PASTA='Personalização',ARQ=PASTA+'/tema.json',CACHE='urbe.aparencia.v1';

  /* ---------- temas prontos ---------- */
  var PRESETS={
    escuro:{nome:'Escuro',claro:false,cores:{fundo:'#0e0f11',superficie:'#15161a',superficie2:'#1b1d22',superficie3:'#24262c',linha:'#25272d',linha2:'#33363e',texto:'#ececf0',texto2:'#a3a6af',texto3:'#6d7079',destaque:'#8fb3ff',perigo:'#ff7b86',sucesso:'#7fdca6',aviso:'#f1c46e',codigo:'#f0d9a8'}},
    'meia-noite':{nome:'Meia-noite',claro:false,cores:{fundo:'#0b0d1a',superficie:'#111427',superficie2:'#171a31',superficie3:'#20243f',linha:'#1f2340',linha2:'#2c3156',texto:'#e8e9f7',texto2:'#a2a6c9',texto3:'#6b6f96',destaque:'#b39cff',perigo:'#ff7b9c',sucesso:'#7fe0c0',aviso:'#f3c778',codigo:'#ffd59e'}},
    floresta:{nome:'Floresta',claro:false,cores:{fundo:'#0d120e',superficie:'#131a14',superficie2:'#19221a',superficie3:'#223024',linha:'#1f2a20',linha2:'#2e3d2f',texto:'#e7efe4',texto2:'#a4b5a0',texto3:'#6c7d69',destaque:'#8fd18a',perigo:'#ff8a7a',sucesso:'#9be08f',aviso:'#e9c66e',codigo:'#e8d9a0'}},
    oceano:{nome:'Oceano',claro:false,cores:{fundo:'#081317',superficie:'#0d1b21',superficie2:'#12242b',superficie3:'#193139',linha:'#162a31',linha2:'#23404a',texto:'#e3f1f4',texto2:'#9dbac1',texto3:'#62818a',destaque:'#5fd0e0',perigo:'#ff8490',sucesso:'#78e0b0',aviso:'#f0c674',codigo:'#ffe0a3'}},
    vinho:{nome:'Vinho',claro:false,cores:{fundo:'#140c0e',superficie:'#1c1114',superficie2:'#23161a',superficie3:'#301e23',linha:'#2a1a1e',linha2:'#40282e',texto:'#f3e7e9',texto2:'#c0a4aa',texto3:'#86686f',destaque:'#f08aa0',perigo:'#ff8d7a',sucesso:'#8fdcab',aviso:'#f1c46e',codigo:'#f6d6a8'}},
    claro:{nome:'Claro',claro:true,cores:{fundo:'#f6f6f8',superficie:'#ffffff',superficie2:'#f1f2f5',superficie3:'#e7e8ed',linha:'#e3e4e9',linha2:'#d2d4db',texto:'#17181c',texto2:'#555862',texto3:'#8a8d97',destaque:'#3b6fe0',perigo:'#d23a4b',sucesso:'#1e9a5a',aviso:'#b77a09',codigo:'#9a4d12'}},
    sepia:{nome:'Sépia',claro:true,cores:{fundo:'#f4ecdc',superficie:'#fbf5e8',superficie2:'#efe5d1',superficie3:'#e6dac2',linha:'#e2d5bb',linha2:'#d3c3a3',texto:'#3b3024',texto2:'#6b5b47',texto3:'#9a876d',destaque:'#a0582a',perigo:'#b8382e',sucesso:'#4f7d3a',aviso:'#a36f12',codigo:'#8a3f1c'}},
    'alto-contraste':{nome:'Alto contraste',claro:false,cores:{fundo:'#000000',superficie:'#0a0a0a',superficie2:'#121212',superficie3:'#1e1e1e',linha:'#5a5a5a',linha2:'#8a8a8a',texto:'#ffffff',texto2:'#f0f0f0',texto3:'#c8c8c8',destaque:'#ffd400',perigo:'#ff5c5c',sucesso:'#4dff88',aviso:'#ffd400',codigo:'#ffe680'}}
  };
  var CORES=['fundo','superficie','superficie2','superficie3','linha','linha2','texto','texto2','texto3','destaque','perigo','sucesso','aviso','codigo'];
  var TOKEN={fundo:'--ui-bg',superficie:'--ui-surface',superficie2:'--ui-surface-2',superficie3:'--ui-surface-3',linha:'--ui-line',linha2:'--ui-line-2',texto:'--ui-text',texto2:'--ui-text-2',texto3:'--ui-text-3',destaque:'--ui-accent',perigo:'--ui-danger',sucesso:'--ui-ok',aviso:'--ui-warn',codigo:'--ui-code'};
  var FONTES={
    sistema:{nome:'Do sistema',css:'-apple-system,BlinkMacSystemFont,"SF Pro Text","Segoe UI Variable","Segoe UI",Roboto,"Inter","Helvetica Neue",Arial,sans-serif'},
    arredondada:{nome:'Arredondada',css:'ui-rounded,"SF Pro Rounded","Nunito","Varela Round","Quicksand","Segoe UI",system-ui,sans-serif'},
    humanista:{nome:'Humanista',css:'"Segoe UI",Candara,Optima,"Gill Sans","Trebuchet MS",Ubuntu,sans-serif'},
    serifada:{nome:'Serifada',css:'"Iowan Old Style","Palatino Linotype",Palatino,"Book Antiqua",Georgia,serif'},
    leitura:{nome:'Leitura',css:'Charter,"Bitstream Charter","Sitka Text",Cambria,Georgia,serif'},
    mono:{nome:'Monoespaçada',css:'ui-monospace,"SF Mono",SFMono-Regular,"JetBrains Mono",Menlo,Consolas,monospace'},
    manuscrita:{nome:'Manuscrita',css:'"Segoe Print","Bradley Hand","Comic Neue","Comic Sans MS",cursive'}
  };
  var CANTOS={retos:[3,4,6,8],suaves:[8,12,16,22],redondos:[12,18,24,30]};
  var LARGURAS={estreita:'600px',media:'720px',larga:'920px',total:'100%'};
  var AMBIENTES=['dia','entardecer','noite','auto','ciclo'];

  var DEFAULT={
    versao:1,tema:'escuro',cores:{},
    texto:{fonte:'sistema',fonteEditor:'sistema',tamanho:15,tamanhoEditor:17,alturaLinha:1.7},
    forma:{cantos:'suaves',densidade:'normal',vidro:true},
    animacoes:'sistema',
    editor:{largura:'media',modoInicial:'visual',ortografia:true},
    cidade:{moradores:true,fauna:true,clima:true,eventos:true,nomes:true,bairros:true,ambiente:'ciclo',texturas:'',paleta:{}},
    estilos:{},css:''
  };

  /* nomes em português para os biomas e as construções (o motor usa os ids em inglês) */
  var BIOMAS={grama:'grass',campo:'grass',prado:'meadow',floresta:'forest',mata:'dense','mata-fechada':'dense',pantano:'swamp','pântano':'swamp',taiga:'taiga',tundra:'tundra',neve:'snow',colinas:'hills',montanha:'mountain',pico:'peak',deserto:'desert',savana:'savanna',estepe:'steppe',praia:'beach',mar:'sea',profundo:'deep',oceano:'deep',rio:'river',lago:'lake'};
  var GRUPOS={agua:['river','lake','sea','deep'],'água':['river','lake','sea','deep']};
  var BIOMAS_NOME={grass:'Grama',meadow:'Prado',forest:'Floresta',dense:'Mata fechada',swamp:'Pântano',taiga:'Taiga',tundra:'Tundra',snow:'Neve',hills:'Colinas',mountain:'Montanha',peak:'Pico',desert:'Deserto',savanna:'Savana',steppe:'Estepe',beach:'Praia',sea:'Mar',deep:'Mar profundo',river:'Rio',lake:'Lago'};
  var CONSTRUCOES={casa:'house',salao:'hall','salão':'hall',oficina:'workshop',tinturaria:'dyer',torre:'tower',mercado:'market',armazem:'store','armazém':'store',loja:'store'};
  var CONSTRUCOES_NOME={house:'Casa (notas .md)',hall:'Salão (.html)',workshop:'Oficina (.js)',dyer:'Tinturaria (.css)',tower:'Torre (.json/.yaml)',market:'Mercado (.csv)',store:'Armazém (outros arquivos)'};
  function biomaId(k){k=String(k||'').toLowerCase().trim();if(BIOMAS_NOME[k])return[k];if(BIOMAS[k])return[BIOMAS[k]];if(GRUPOS[k])return GRUPOS[k].slice();return null}
  function construcaoId(k){k=String(k||'').toLowerCase().trim();return CONSTRUCOES_NOME[k]?k:(CONSTRUCOES[k]||null)}

  /* ---------- utilidades ---------- */
  function clone(o){return JSON.parse(JSON.stringify(o))}
  function isObj(v){return v&&typeof v==='object'&&!Array.isArray(v)}
  function merge(a,b){var out=clone(a);Object.keys(b||{}).forEach(function(k){if(isObj(b[k])&&isObj(out[k]))out[k]=merge(out[k],b[k]);else out[k]=clone(b[k])});return out}
  function hex(c){if(typeof c!=='string')return null;c=c.trim();var m=/^#([0-9a-f]{3})$/i.exec(c);if(m)return('#'+m[1].split('').map(function(x){return x+x}).join('')).toLowerCase();return /^#[0-9a-f]{6}$/i.test(c)?c.toLowerCase():null}
  function rgb(h){h=hex(h)||'#000000';return[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]}
  function toHex(a){return'#'+a.map(function(v){return('0'+Math.max(0,Math.min(255,Math.round(v))).toString(16)).slice(-2)}).join('')}
  function mix(a,b,t){var x=rgb(a),y=rgb(b);return toHex([0,1,2].map(function(i){return x[i]+(y[i]-x[i])*t}))}
  function lum(h){var c=rgb(h).map(function(v){v/=255;return v<=.03928?v/12.92:Math.pow((v+.055)/1.055,2.4)});return .2126*c[0]+.7152*c[1]+.0722*c[2]}
  function contraste(a,b){var x=lum(a),y=lum(b);return(Math.max(x,y)+.05)/(Math.min(x,y)+.05)}
  function fonteCss(v){if(FONTES[v])return FONTES[v].css;v=String(v||'').replace(/[;{}<>\\]/g,'').trim();return v||FONTES.sistema.css}
  function num(v,min,max,def){v=Number(v);return isFinite(v)?Math.max(min,Math.min(max,v)):def}
  function store(){try{return global.localStorage}catch(_){return null}}

  /* ---------- validação: devolve a configuração limpa, erros e avisos por caminho ---------- */
  function normalize(input){
    var errors=[],warnings=[],src=input;
    if(typeof src==='string'){try{src=src.trim()?JSON.parse(src):{}}catch(e){return{config:clone(DEFAULT),errors:[{path:'',message:'JSON inválido: '+e.message}],warnings:[]}}}
    if(!isObj(src))src={};
    var c=clone(DEFAULT);
    function known(obj,base,keys){Object.keys(obj||{}).forEach(function(k){if(keys.indexOf(k)<0)warnings.push({path:base+k,message:'propriedade desconhecida (ignorada)'})})}
    known(src,'',Object.keys(DEFAULT).concat(['$schema','descricao','descrição']));
    if(src.tema!=null){if(typeof src.tema==='string'&&src.tema.trim())c.tema=src.tema.trim();else errors.push({path:'tema',message:'use o nome de um tema pronto ('+Object.keys(PRESETS).join(', ')+') ou o caminho de um tema salvo'})}
    if(src.cores!=null){if(!isObj(src.cores))errors.push({path:'cores',message:'deve ser um objeto {nome: "#rrggbb"}'});else{known(src.cores,'cores.',CORES);
      CORES.forEach(function(k){var v=src.cores[k];if(v==null||v==='')return;var h=hex(v);if(h)c.cores[k]=h;else errors.push({path:'cores.'+k,message:'cor inválida "'+v+'" (use #rrggbb)'})})}}
    function sec(name,fn){var o=src[name];if(o==null)return;if(!isObj(o)){errors.push({path:name,message:'deve ser um objeto'});return}fn(o)}
    function pick(o,base,key,list){if(o[key]==null)return;if(list.indexOf(o[key])>=0)c[base][key]=o[key];else errors.push({path:base+'.'+key,message:'use um de: '+list.join(', ')})}
    function bool(o,base,key){if(o[key]==null)return;if(typeof o[key]==='boolean')c[base][key]=o[key];else errors.push({path:base+'.'+key,message:'use true ou false'})}
    function range(o,base,key,min,max){if(o[key]==null)return;var v=Number(o[key]);if(isFinite(v)&&v>=min&&v<=max)c[base][key]=v;else errors.push({path:base+'.'+key,message:'número entre '+min+' e '+max})}
    sec('texto',function(o){known(o,'texto.',Object.keys(DEFAULT.texto));['fonte','fonteEditor'].forEach(function(k){if(o[k]==null)return;if(typeof o[k]==='string'&&o[k].trim()){c.texto[k]=o[k].trim();if(!FONTES[o[k]])warnings.push({path:'texto.'+k,message:'fonte própria: "'+o[k]+'" — precisa estar instalada no aparelho ou declarada num estilo CSS'})}else errors.push({path:'texto.'+k,message:'use '+Object.keys(FONTES).join(', ')+' ou o nome de uma fonte'})});
      range(o,'texto','tamanho',12,22);range(o,'texto','tamanhoEditor',13,28);range(o,'texto','alturaLinha',1.2,2.2)});
    sec('forma',function(o){known(o,'forma.',Object.keys(DEFAULT.forma));pick(o,'forma','cantos',Object.keys(CANTOS));pick(o,'forma','densidade',['compacta','normal','confortavel']);bool(o,'forma','vidro')});
    if(src.animacoes!=null){if(['sistema','ligadas','reduzidas'].indexOf(src.animacoes)>=0)c.animacoes=src.animacoes;else errors.push({path:'animacoes',message:'use sistema, ligadas ou reduzidas'})}
    sec('editor',function(o){known(o,'editor.',Object.keys(DEFAULT.editor));pick(o,'editor','largura',Object.keys(LARGURAS));pick(o,'editor','modoInicial',['visual','texto']);bool(o,'editor','ortografia')});
    sec('cidade',function(o){known(o,'cidade.',Object.keys(DEFAULT.cidade));['moradores','fauna','clima','eventos','nomes','bairros'].forEach(function(k){bool(o,'cidade',k)});pick(o,'cidade','ambiente',AMBIENTES);
      if(o.texturas!=null){if(typeof o.texturas==='string')c.cidade.texturas=o.texturas.trim();else errors.push({path:'cidade.texturas',message:'caminho do pacote de texturas (texto) ou "" para nenhum'})}
      if(o.paleta!=null){if(!isObj(o.paleta))errors.push({path:'cidade.paleta',message:'objeto {bioma: ["#base", "#sombra", "#luz", "#detalhe"]}'});else Object.keys(o.paleta).forEach(function(k){var ids=biomaId(k),v=o.paleta[k];
        if(!ids){errors.push({path:'cidade.paleta.'+k,message:'bioma desconhecido; use '+Object.keys(BIOMAS).concat(Object.keys(GRUPOS)).join(', ')});return}
        var arr=(Array.isArray(v)?v:[v]).map(hex);if(!arr.length||arr.some(function(x){return!x})){errors.push({path:'cidade.paleta.'+k,message:'lista de 1 a 4 cores #rrggbb'});return}c.cidade.paleta[k]=arr.slice(0,4)})}});
    if(src.estilos!=null){if(!isObj(src.estilos))errors.push({path:'estilos',message:'objeto {"Personalização/estilos/arquivo.css": true}'});else Object.keys(src.estilos).forEach(function(k){if(typeof src.estilos[k]==='boolean')c.estilos[k]=src.estilos[k];else errors.push({path:'estilos.'+k,message:'use true ou false'})})}
    if(src.css!=null){if(typeof src.css==='string')c.css=src.css.slice(0,40000);else errors.push({path:'css',message:'texto com regras CSS'})}
    if(!PRESETS[c.tema]&&!temaSalvo(c.tema))warnings.push({path:'tema',message:'tema "'+c.tema+'" não encontrado; usando Escuro'});
    return{config:c,errors:errors,warnings:warnings};
  }

  /* ---------- temas salvos no vault ---------- */
  function caminhoTema(nome){var p=String(nome||'').trim();if(!p)return'';if(p.indexOf('/')<0)p=PASTA+'/temas/'+p;if(!/\.json$/i.test(p))p+='.json';return p}
  function temaSalvo(nome){if(!docs||PRESETS[nome])return null;var d=docs.get(caminhoTema(nome));if(!d)return null;try{var t=JSON.parse(d.content);return isObj(t)?t:null}catch(_){return null}}
  function temasDoVault(){if(!docs)return[];return docs.list().filter(function(d){return d.path.indexOf(PASTA+'/temas/')===0&&/\.json$/i.test(d.path)}).map(function(d){var t=null;try{t=JSON.parse(d.content)}catch(_){}
    return{id:d.path,nome:(t&&t.nome)||d.title.replace(/\.json$/i,''),claro:!!(t&&t.claro),cores:(t&&isObj(t.cores))?t.cores:{},valido:!!t}})}

  /* ---------- das configurações para variáveis CSS ---------- */
  function paleta(c){
    var base=PRESETS[c.tema]||null,salvo=base?null:temaSalvo(c.tema);if(!base&&!salvo)base=PRESETS.escuro;
    var claro=base?base.claro:(typeof salvo.claro==='boolean'?salvo.claro:null),cores=Object.assign({},base?base.cores:{});
    if(salvo){var ref=PRESETS[salvo.base]||null;if(claro==null)claro=ref?ref.claro:false;cores=Object.assign({},(ref||(claro?PRESETS.claro:PRESETS.escuro)).cores);Object.keys(salvo.cores||{}).forEach(function(k){var h=hex(salvo.cores[k]);if(h&&TOKEN[k])cores[k]=h})}
    Object.keys(c.cores||{}).forEach(function(k){var h=hex(c.cores[k]);if(h)cores[k]=h});
    /* o fundo trocado decide se o tema é claro ou escuro */
    if(c.cores&&c.cores.fundo)claro=lum(cores.fundo)>.4;
    return{claro:!!claro,cores:cores};
  }
  function variaveis(c){
    var p=paleta(c),k=p.cores,claro=p.claro,v={};
    Object.keys(TOKEN).forEach(function(n){if(k[n])v[TOKEN[n]]=k[n]});
    var a=k.destaque,ink=contraste(a,'#0b1325')>=contraste(a,'#ffffff')?'#0b1325':'#ffffff';
    v['--ui-accent-hover']=claro?mix(a,'#000000',.12):mix(a,'#ffffff',.18);v['--ui-accent-ink']=ink;
    v['--ui-accent-soft']='rgba('+rgb(a).join(',')+','+(claro?.12:.14)+')';v['--ui-danger-soft']='rgba('+rgb(k.perigo).join(',')+','+(claro?.1:.12)+')';
    [['--ui-accent-rgb',a],['--ui-surface-rgb',k.superficie],['--ui-surface-2-rgb',k.superficie2],['--ui-surface-3-rgb',k.superficie3],['--ui-bg-rgb',k.fundo],['--ui-ok-rgb',k.sucesso],['--ui-danger-rgb',k.perigo],['--ui-warn-rgb',k.aviso]].forEach(function(x){v[x[0]]=rgb(x[1]).join(',')});
    v['--ui-tint']=claro?'0,0,0':'255,255,255';
    v['--ui-shadow-1']=claro?'0 1px 2px rgba(20,22,30,.08),0 6px 20px rgba(20,22,30,.08)':'0 1px 2px rgba(0,0,0,.35),0 6px 20px rgba(0,0,0,.28)';
    v['--ui-shadow-2']=claro?'0 18px 60px rgba(20,22,30,.18)':'0 18px 60px rgba(0,0,0,.55)';
    var r=CANTOS[c.forma.cantos]||CANTOS.suaves;v['--ui-r-s']=r[0]+'px';v['--ui-r-m']=r[1]+'px';v['--ui-r-l']=r[2]+'px';v['--ui-r-xl']=r[3]+'px';
    v['--ui-font']=fonteCss(c.texto.fonte);v['--ui-editor-font']=fonteCss(c.texto.fonteEditor);
    v['--ui-font-size']=num(c.texto.tamanho,12,22,15)+'px';v['--ui-editor-size']=num(c.texto.tamanhoEditor,13,28,17)+'px';
    v['--ui-code-size']=Math.round(num(c.texto.tamanhoEditor,13,28,17)*.88)+'px';v['--ui-editor-line']=String(num(c.texto.alturaLinha,1.2,2.2,1.7));
    v['--ui-editor-width']=LARGURAS[c.editor.largura]||LARGURAS.media;
    var attrs={'data-tema':claro?'claro':'escuro','data-densidade':c.forma.densidade,'data-vidro':c.forma.vidro?'sim':'nao','data-animacoes':c.animacoes};
    return{vars:v,attrs:attrs,claro:claro,fundo:k.fundo};
  }

  /* ---------- aplicar ---------- */
  var atual=clone(DEFAULT),aplicadas=[],estilosEls=new Map(),seguro=false,ultimoTerreno='',carregado=false;
  try{seguro=/[?&]seguro=1\b/.test(global.location.search)||(store()&&store().getItem('urbe.modoSeguro')==='1')}catch(_){}
  function aplicarTela(c){
    var r=variaveis(c),s=root.style;
    aplicadas.forEach(function(n){if(!(n in r.vars))s.removeProperty(n)});aplicadas=Object.keys(r.vars);
    aplicadas.forEach(function(n){s.setProperty(n,r.vars[n])});
    Object.keys(r.attrs).forEach(function(a){root.setAttribute(a,r.attrs[a])});
    root.style.colorScheme=r.claro?'light':'dark';
    var meta=doc.querySelector('meta[name="theme-color"]');if(meta)meta.setAttribute('content',r.fundo);
    ['bodyEditor','renderedPreview'].forEach(function(id){var el=doc.getElementById(id);if(el)el.spellcheck=c.editor.ortografia!==false});
    try{var st=store();if(st)st.setItem(CACHE,JSON.stringify({vars:r.vars,attrs:r.attrs,scheme:r.claro?'light':'dark'}))}catch(_){}
  }
  function mundo(){return core.service('world.custom')}
  function aplicarCidade(c){
    var w=mundo();if(!w)return;
    w.options({moradores:c.cidade.moradores,fauna:c.cidade.fauna,clima:c.cidade.clima,eventos:c.cidade.eventos,nomes:c.cidade.nomes,bairros:c.cidade.bairros,ambiente:c.cidade.ambiente,editor:c.editor.modoInicial});
    carregarTexturas(c).then(function(t){
      var chave=JSON.stringify([c.cidade.paleta,c.cidade.texturas,t.assinatura]);if(chave===ultimoTerreno)return;ultimoTerreno=chave;
      var pal={};Object.keys(c.cidade.paleta||{}).forEach(function(k){(biomaId(k)||[]).forEach(function(id){pal[id]=c.cidade.paleta[k]})});
      Object.keys(t.paleta).forEach(function(id){if(!pal[id])pal[id]=t.paleta[id]});
      w.terrain({palette:pal,textures:t.chao});w.sprites(t.construcoes);
    }).catch(function(e){console.warn('texturas',e)});
  }
  function aplicarEstilos(c){
    var querer=new Map();
    if(!seguro){Object.keys(c.estilos||{}).forEach(function(p){if(!c.estilos[p])return;var d=docs&&docs.get(p);if(d)querer.set('arquivo:'+d.path,d.content)});
      if(c.css&&c.css.trim())querer.set('tema.json',c.css)}
    estilosEls.forEach(function(el,k){if(!querer.has(k)){el.remove();estilosEls.delete(k)}});
    querer.forEach(function(css,k){var el=estilosEls.get(k);if(!el){el=doc.createElement('style');el.setAttribute('data-urbe-estilo',k);doc.head.appendChild(el);estilosEls.set(k,el)}if(el.textContent!==css)el.textContent=css});
  }
  function aplicar(c){atual=c;aplicarTela(c);aplicarEstilos(c);aplicarCidade(c);core.events.emit('customize:applied',{config:clone(c)})}

  /* ---------- texturas: pacote JSON → pixels 16×16 e imagens das construções ---------- */
  var cacheImg=new Map();
  function imagem(src){if(cacheImg.has(src))return cacheImg.get(src);
    var p=new Promise(function(ok,no){var im=new Image();im.onload=function(){ok(im)};im.onerror=function(){no(new Error('imagem inválida'))};im.src=src});cacheImg.set(src,p);if(cacheImg.size>80)cacheImg.delete(cacheImg.keys().next().value);return p}
  function grade(spec,padrao){
    /* {cores:{a:"#..."}, pixels:["aab...", ...]} → canvas do tamanho da grade */
    var rows=Array.isArray(spec.pixels)?spec.pixels.map(String):[];if(!rows.length)throw new Error('pixels vazio');
    var h=Math.min(64,rows.length),w=Math.min(64,Math.max.apply(null,rows.map(function(r){return r.length}))),cores=spec.cores||{},cv=doc.createElement('canvas');cv.width=w;cv.height=h;
    var x=cv.getContext('2d'),img=x.createImageData(w,h);
    for(var j=0;j<h;j++)for(var i=0;i<w;i++){var ch=rows[j][i]||'.',c=hex(cores[ch]),o=(j*w+i)*4;if(!c){if(padrao){c=padrao}else continue}var q=rgb(c);img.data[o]=q[0];img.data[o+1]=q[1];img.data[o+2]=q[2];img.data[o+3]=255}
    x.putImageData(img,0,0);return cv;
  }
  function pixels16(fonte){var cv=doc.createElement('canvas');cv.width=16;cv.height=16;var x=cv.getContext('2d');x.imageSmoothingEnabled=fonte.width>16;x.drawImage(fonte,0,0,16,16);return Array.from(x.getImageData(0,0,16,16).data)}
  function variacoes(v){return(Array.isArray(v)?v:[v]).filter(isObj).slice(0,4)}
  async function fonteDe(spec,padrao){if(spec.imagem){if(!/^data:image\/(png|jpe?g|gif|webp|svg\+xml);base64,/i.test(spec.imagem))throw new Error('imagem precisa ser data:image/...;base64');return imagem(spec.imagem)}if(spec.pixels)return grade(spec,padrao);throw new Error('use "imagem" ou "pixels"')}
  function lerPacote(caminho){if(!caminho||!docs)return null;var d=docs.get(caminho)||docs.get(PASTA+'/texturas/'+caminho)||docs.get(PASTA+'/texturas/'+caminho+'.json');if(!d)return null;try{return{doc:d,dados:JSON.parse(d.content)}}catch(e){return{doc:d,erro:'JSON inválido: '+e.message}}}
  async function carregarTexturas(c){
    var out={chao:{},paleta:{},construcoes:{},assinatura:'',erros:[]},p=lerPacote(c.cidade.texturas);if(!p)return out;
    out.assinatura=p.doc.path+'#'+p.doc.revision+'#'+p.doc.content.length;if(p.erro){out.erros.push(p.erro);return out}
    var dados=p.dados||{},A=global.UrbeArt;
    if(isObj(dados.paleta))Object.keys(dados.paleta).forEach(function(k){(biomaId(k)||[]).forEach(function(id){var arr=(Array.isArray(dados.paleta[k])?dados.paleta[k]:[dados.paleta[k]]).map(hex).filter(Boolean);if(arr.length)out.paleta[id]=arr})});
    for(var k of Object.keys(dados.chao||{})){var ids=biomaId(k);if(!ids){out.erros.push('chao.'+k+': bioma desconhecido');continue}
      var lista=[];for(var v of variacoes(dados.chao[k])){try{var base=A&&A.PAL[ids[0]]?A.PAL[ids[0]][0]:'#000000';lista.push(pixels16(await fonteDe(v,base)))}catch(e){out.erros.push('chao.'+k+': '+e.message)}}
      if(lista.length)ids.forEach(function(id){out.chao[id]=lista})}
    for(var b of Object.keys(dados.construcoes||{})){var id=construcaoId(b);if(!id){out.erros.push('construcoes.'+b+': tipo desconhecido');continue}
      try{out.construcoes[id]=await fonteDe(variacoes(dados.construcoes[b])[0]||{},null)}catch(e){out.erros.push('construcoes.'+b+': '+e.message)}}
    return out;
  }

  /* ---------- carregar do vault e gravar ---------- */
  function lerArquivo(){var d=docs&&docs.get(ARQ);return d?d.content:null}
  function recarregar(){var txt=lerArquivo(),n=normalize(txt||{});carregado=true;aplicar(n.config);ultimoEstado={errors:n.errors,warnings:n.warnings,existe:txt!=null};return n}
  var ultimoEstado={errors:[],warnings:[],existe:false};
  function podeGravar(){var p=core.service('persistence');return !!(docs&&(!p||p.vault))}
  function gravar(c){
    var n=normalize(c);if(n.errors.length)return n;
    if(podeGravar()){var d=docs.get(ARQ),txt=JSON.stringify(Object.assign({'$schema':'urbe-tema-1'},semPadroes(n.config)),null,2)+'\n';
      if(!d||d.content!==txt)docs.upsert(Object.assign({},d||{},{id:d?d.id:undefined,path:ARQ,content:txt}),{source:'customize'})}
    aplicar(n.config);return n;
  }
  /* o arquivo guarda só o que difere do padrão: fica curto e fácil de ler */
  function semPadroes(c){var out={};Object.keys(c).forEach(function(k){if(k==='versao'){out.versao=1;return}var a=c[k],b=DEFAULT[k];
    if(isObj(a)&&isObj(b)){var o={};Object.keys(a).forEach(function(x){if(JSON.stringify(a[x])!==JSON.stringify(b[x]))o[x]=a[x]});if(Object.keys(o).length)out[k]=o}
    else if(JSON.stringify(a)!==JSON.stringify(b))out[k]=a});return out}
  function set(patch){return gravar(merge(atual,patch||{}))}
  /* prévia ao vivo (arrastando uma cor ou um controle): aplica na tela sem gravar */
  function preview(patch){var n=normalize(merge(atual,patch||{}));if(!n.errors.length){aplicarTela(n.config);aplicarEstilos(n.config)}return n}
  function reset(secao){if(!secao)return gravar(clone(DEFAULT));var o=clone(atual);o[secao]=clone(DEFAULT[secao]);return gravar(o)}

  function salvarTema(nome){
    nome=String(nome||'').trim();if(!nome)throw new Error('Dê um nome ao tema.');var p=caminhoTema(nome.replace(/[\\/:*?"<>|]/g,'-')),pal=paleta(atual);
    var dados={nome:nome,claro:pal.claro,base:PRESETS[atual.tema]?atual.tema:(pal.claro?'claro':'escuro'),cores:pal.cores},d=docs.get(p);
    docs.upsert(Object.assign({},d||{},{id:d?d.id:undefined,path:p,content:JSON.stringify(dados,null,2)+'\n'}),{source:'customize'});return p;
  }
  function estilos(){if(!docs)return[];return docs.list().filter(function(d){return /\.css$/i.test(d.path)&&d.path.indexOf(PASTA+'/')===0}).map(function(d){return{path:d.path,nome:d.title,ligado:!!atual.estilos[d.path],tamanho:d.content.length}})}
  function pacotes(){if(!docs)return[];return docs.list().filter(function(d){return d.path.indexOf(PASTA+'/texturas/')===0&&/\.json$/i.test(d.path)}).map(function(d){var nome=d.title;try{var j=JSON.parse(d.content);if(j&&j.nome)nome=j.nome}catch(_){}return{path:d.path,nome:nome,ativo:atual.cidade.texturas===d.path}})}

  /* ---------- descrição do formato (Assistente e Tutorial) ---------- */
  function schemaText(){
    return['ARQUIVO: '+ARQ+' (JSON). Só escreva o que quiser mudar; o resto usa o padrão. Salvar o arquivo aplica na hora.',
      '{',
      '  "tema": "'+Object.keys(PRESETS).join('" | "')+'" | "Personalização/temas/<nome>.json",',
      '  "cores": { '+CORES.map(function(k){return '"'+k+'": "#rrggbb"'}).join(', ')+' },   // sobrepõem as do tema; trocar "fundo" decide se é claro ou escuro',
      '  "texto": { "fonte": "'+Object.keys(FONTES).join('" | "')+'" | "nome de fonte", "fonteEditor": (igual), "tamanho": 12-22, "tamanhoEditor": 13-28, "alturaLinha": 1.2-2.2 },',
      '  "forma": { "cantos": "retos" | "suaves" | "redondos", "densidade": "compacta" | "normal" | "confortavel", "vidro": true|false },',
      '  "animacoes": "sistema" | "ligadas" | "reduzidas",',
      '  "editor": { "largura": "estreita" | "media" | "larga" | "total", "modoInicial": "visual" | "texto", "ortografia": true|false },',
      '  "cidade": { "moradores": bool, "fauna": bool, "clima": bool, "eventos": bool, "nomes": bool, "bairros": bool, "ambiente": "dia" | "entardecer" | "noite" | "auto" | "ciclo",',
      '              "texturas": "Personalização/texturas/<pacote>.json" | "", "paleta": { "<bioma>": ["#base", "#sombra", "#luz", "#detalhe"] } },',
      '  "estilos": { "Personalização/estilos/<arquivo>.css": true|false },',
      '  "css": "regras CSS extras aplicadas por último"',
      '}',
      'Biomas: '+Object.keys(BIOMAS).join(', ')+', agua (rio+lago+mar).',
      '',
      'TEMA SALVO (Personalização/temas/<nome>.json): { "nome": "...", "claro": bool, "base": "<tema pronto>", "cores": { ...mesmas chaves de "cores"... } }',
      '',
      'PACOTE DE TEXTURAS (Personalização/texturas/<nome>.json):',
      '{ "nome": "...",',
      '  "paleta": { "<bioma>": ["#base", ...] },',
      '  "chao": { "<bioma>": { "cores": { "a": "#rrggbb", "b": "#rrggbb" }, "pixels": ["16 letras", ... 16 linhas] }  |  { "imagem": "data:image/png;base64,..." }  |  [até 4 variações] },',
      '  "construcoes": { "casa" | "salao" | "oficina" | "tinturaria" | "torre" | "mercado" | "armazem": { "imagem": "data:..." } | { "cores": {...}, "pixels": [...] } } }',
      'Pixels: cada letra é uma cor de "cores"; "." usa a cor base do bioma. Grades de até 64×64 são reduzidas para 16×16 no chão.',
      'Construções: desenho de 48×56 (3×3,5 tiles) fica igual às originais; outros tamanhos são esticados.',
      '',
      'ESTILOS: arquivos .css em Personalização/estilos/. Variáveis úteis: '+Object.keys(TOKEN).map(function(k){return TOKEN[k]}).join(', ')+', --ui-font, --ui-editor-font, --ui-editor-size, --ui-r-m.',
      'PLUGINS: arquivos .js em Personalização/plugins/ — veja plugin_guide.'].join('\n');
  }

  /* ---------- ciclo de vida ---------- */
  var timer=null;function agendar(){clearTimeout(timer);timer=setTimeout(recarregar,120)}
  function relevante(e){var d=e&&(e.document||e.previous);return d&&typeof d.path==='string'&&d.path.indexOf(PASTA+'/')===0}
  core.events.on('workspace:loaded',function(){ultimoTerreno='';recarregar()});
  ['document:created','document:updated','document:removed'].forEach(function(t){core.events.on(t,function(e){if(relevante(e)&&!(e.meta&&e.meta.source==='customize'&&e.document&&e.document.path===ARQ))agendar()})});
  core.events.on('documents:reset',function(){agendar()});
  /* o mundo e o editor podem nascer depois deste módulo */
  core.events.on('service:provided',function(e){if(e&&e.name==='world.custom'&&carregado)aplicarCidade(atual)});

  function modoSeguro(v){if(v===undefined)return seguro;seguro=!!v;try{var s=store();if(s){if(seguro)s.setItem('urbe.modoSeguro','1');else s.removeItem('urbe.modoSeguro')}}catch(_){}aplicarEstilos(atual);core.events.emit('customize:safe-mode',{on:seguro});return seguro}

  var api={
    FILE:ARQ,FOLDER:PASTA,PRESETS:PRESETS,FONTS:FONTES,CORNERS:CANTOS,WIDTHS:LARGURAS,COLORS:CORES,BIOMES:BIOMAS_NOME,BUILDINGS:CONSTRUCOES_NOME,
    get:function(){return clone(atual)},state:function(){return clone(ultimoEstado)},set:set,replace:function(c){return gravar(c)},preview:preview,reset:reset,normalize:normalize,reload:recarregar,
    palette:function(){return paleta(atual)},variables:function(c){return variaveis(c||atual)},
    themes:temasDoVault,saveTheme:salvarTema,styles:estilos,packs:pacotes,loadTextures:function(){return carregarTexturas(atual)},
    biomeId:biomaId,buildingId:construcaoId,schemaText:schemaText,safeMode:modoSeguro,contrast:contraste
  };
  core.provide('customize',api);global.UrbeCustomize=api;
  core.commands.register('customize.reset',{title:'Restaurar aparência padrão',category:'Personalização',execute:function(){return reset()}});
  core.commands.register('customize.edit-file',{title:'Abrir tema.json',category:'Personalização',execute:function(){if(!docs.get(ARQ))gravar(atual);if(!docs.get(ARQ))docs.upsert({path:ARQ,content:'{\n  "$schema": "urbe-tema-1"\n}\n'},{source:'customize'});return core.commands.has('document.open')?core.commands.execute('document.open',{path:ARQ}):null}});
  /* antes de qualquer vault: o que ficou salvo neste aparelho */
  try{var cache=store()&&JSON.parse(store().getItem(CACHE)||'null');if(!cache)aplicarTela(atual)}catch(_){aplicarTela(atual)}
})(window);
