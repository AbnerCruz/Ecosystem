(function(global){
  'use strict';
  /* Identidade documental em sidecar (REQ-042, ADR-0006, RM-F1-15): `.urbe/identity.json`
       {version:1, docs:{<docId>:{path, fingerprint, seen}}}
     Nada é escrito dentro das notas. Serve para reconhecer um rename/move feito fora do app (Explorer, Obsidian,
     sincronizador): a nota some de um caminho e aparece em outro com o mesmo conteúdo ⇒ mantém o ID, a casa e os vínculos.
     Regras: só casa quando o fingerprint é único dos dois lados (cópias idênticas = ambíguo = ID novo); nota vazia não casa. */
  var PATH='.urbe/identity.json',VERSION=1;

  function normalize(text){return String(text==null?'':text).replace(/\r\n?/g,'\n').replace(/[ \t]+$/gm,'').replace(/\n+$/,'')}
  /** Hash de conteúdo normalizado (dois FNV-1a de 32 bits + tamanho); '' para conteúdo vazio (não participa da reconciliação). */
  function fingerprint(text){
    var s=normalize(text);if(!s)return '';
    var h1=2166136261,h2=0x811c9dc5^0x5bd1e995;
    for(var i=0;i<s.length;i++){var c=s.charCodeAt(i);h1^=c;h1=Math.imul(h1,16777619);h2^=c+i;h2=Math.imul(h2,16777619)}
    return (h1>>>0).toString(16).padStart(8,'0')+(h2>>>0).toString(16).padStart(8,'0')+':'+s.length.toString(36);
  }
  function parse(raw){
    if(raw==null)return{state:'absent',data:null};
    var d;try{d=JSON.parse(raw)}catch(_){return{state:'corrupt',data:null}}
    if(!d||typeof d!=='object'||typeof d.version!=='number')return{state:'corrupt',data:null};
    if(d.version>VERSION)return{state:'future',data:d};
    if(d.version!==VERSION||!d.docs||typeof d.docs!=='object')return{state:'corrupt',data:null};
    return{state:'current',data:d};
  }
  /** Constrói o sidecar a partir dos documentos atuais, preservando `seen` de quem não mudou (o arquivo não é regravado à toa). */
  function build(docs,previous,now,cache){
    var prev=(previous&&previous.docs)||{},out={};
    docs.forEach(function(d){
      var fp=cached(cache,d),p=prev[d.id];
      out[d.id]={path:d.path,fingerprint:fp,seen:p&&p.path===d.path&&p.fingerprint===fp&&p.seen?p.seen:now};
    });
    return{version:VERSION,docs:out};
  }
  function cached(cache,d){if(!cache)return fingerprint(d.content);var c=cache.get(d.id);if(c&&c.content===d.content)return c.fp;var fp=fingerprint(d.content);cache.set(d.id,{content:d.content,fp:fp});return fp}
  function serialize(data){var keys=Object.keys(data.docs).sort(),docs={};keys.forEach(function(k){docs[k]=data.docs[k]});return JSON.stringify({version:data.version,docs:docs},null,1)}

  /* Pares (antigo → novo) por fingerprint único. vanished: [{id,path,fingerprint}]; appeared: [{path,content}|{path,fingerprint}] */
  function pair(vanished,appeared){
    var byFpOld=new Map(),byFpNew=new Map(),out=[],ambiguous=[];
    vanished.forEach(function(v){if(!v.fingerprint)return;var l=byFpOld.get(v.fingerprint)||[];l.push(v);byFpOld.set(v.fingerprint,l)});
    appeared.forEach(function(a){var fp=a.fingerprint!=null?a.fingerprint:fingerprint(a.content);if(!fp)return;var l=byFpNew.get(fp)||[];l.push(a);byFpNew.set(fp,l)});
    byFpNew.forEach(function(news,fp){var olds=byFpOld.get(fp);if(!olds)return;
      if(olds.length===1&&news.length===1)out.push({id:olds[0].id,from:olds[0].path,to:news[0].path});
      else ambiguous.push({fingerprint:fp,from:olds.map(function(o){return o.path}),to:news.map(function(n){return n.path})})});
    return{pairs:out,ambiguous:ambiguous};
  }

  /** Reconciliação na abertura. items: documentos lidos do disco ({id|null,path,content}); meta: mapa (é ajustado: notas, regiões e assets
      acompanham o rename); identity: sidecar lido. Devolve {renames, ambiguous}. Não toca nos arquivos. */
  function reconcile(items,meta,identity,onDisk){
    var docs=(identity&&identity.docs)||{},presentPaths=new Set(items.map(function(i){return i.path})),used=new Set();
    items.forEach(function(i){if(i.id)used.add(i.id)});
    // 1) mesmo caminho: o sidecar devolve o ID quando o mapa não tem (vault sem mapa, mapa antigo)
    var byPath=new Map();Object.keys(docs).forEach(function(id){var e=docs[id];if(e&&typeof e.path==='string')byPath.set(e.path,id)});
    items.forEach(function(i){if(!i.id){var id=byPath.get(i.path);if(id&&!used.has(id)){i.id=id;used.add(id)}}});
    // 2) caminho sumiu + nota nova com o mesmo conteúdo = rename/move externo
    var vanished=Object.keys(docs).filter(function(id){var e=docs[id];return e&&!used.has(id)&&!presentPaths.has(e.path)}).map(function(id){return{id:id,path:docs[id].path,fingerprint:docs[id].fingerprint}});
    var appeared=items.filter(function(i){return !i.id});
    var r=pair(vanished,appeared),byNew=new Map(appeared.map(function(a){return[a.path,a]}));
    r.pairs.forEach(function(p){var it=byNew.get(p.to);it.id=p.id;used.add(p.id)});
    if(meta&&r.pairs.length)moveMeta(meta,r.pairs,onDisk||presentPaths);
    return{renames:r.pairs,ambiguous:r.ambiguous};
  }
  function dirOf(p){var i=p.lastIndexOf('/');return i>0?p.slice(0,i):''}
  /* Casa, região e assets seguem a nota renomeada. Uma pasta inteira renomeada vira a mesma região (mesmo `reg_`, mesma forma),
     se o caminho antigo sumiu e o novo não tem região. A casa só guarda a posição quando a nota continua no mesmo bairro
     (rename simples ou pasta renomeada); movida para outra pasta, ela mantém ID e metadados e ganha lugar no bairro novo. */
  function moveMeta(meta,pairs,onDisk){
    var notas=meta.notas&&typeof meta.notas==='object'?meta.notas:null,regioes=Array.isArray(meta.regioes)?meta.regioes:[];
    var existsDir=function(dir){var pre=dir+'/';for(const p of onDisk)if(p.indexOf(pre)===0&&!p.slice(pre.length).split('/').some(function(x){return x.charAt(0)==='.'}))return true;return false};
    var regionPaths=new Set(regioes.map(function(r){return r&&r.caminho})),dirMoves=new Map();
    pairs.forEach(function(p){
      var a=p.from.split('/'),b=p.to.split('/');a.pop();b.pop();
      // pasta renomeada = sufixos iguais; o prefixo que difere é o que foi renomeado (A/B/x → C/B/x: A → C)
      while(a.length&&b.length&&a[a.length-1]===b[b.length-1]){a.pop();b.pop()}
      var from=a.join('/'),to=b.join('/');if(!from||!to||from===to)return;
      if(!regionPaths.has(from)||regionPaths.has(to)||existsDir(from))return;
      dirMoves.set(from,to);
    });
    var mv=function(path){if(typeof path!=='string')return path;for(const [f,t] of dirMoves){if(path===f)return t;if(path.indexOf(f+'/')===0)return t+path.slice(f.length)}return path};
    pairs.forEach(function(p){
      if(!notas||!notas[p.from]||notas[p.to])return;
      var n=notas[p.from];delete notas[p.from];
      if(mv(dirOf(p.from))!==dirOf(p.to)){n=Object.assign({},n);delete n.x;delete n.y}
      notas[p.to]=n;
    });
    if(!dirMoves.size)return;
    // a própria pasta renomeada também muda de nome (o mundo deriva o caminho do nome); as de dentro só mudam de prefixo
    regioes.forEach(function(r){if(!r||typeof r.caminho!=='string')return;if(dirMoves.has(r.caminho))r.nome=dirMoves.get(r.caminho).split('/').pop();r.caminho=mv(r.caminho)});
    var fixFiles=function(list){(Array.isArray(list)?list:[]).forEach(function(f){if(!f)return;if(f.relPath)f.relPath=mv(f.relPath);if(typeof f.folderPath==='string')f.folderPath=mv(f.folderPath)})};
    (Array.isArray(meta.construcoes)?meta.construcoes:[]).forEach(function(c){if(!c)return;if(typeof c.caminho==='string')c.caminho=mv(c.caminho);fixFiles(c.files);fixFiles(c.anexos)});
    if(notas)Object.keys(notas).forEach(function(k){var n=notas[k];if(n)fixFiles(n.anexos)});
  }

  global.UrbeIdentity={PATH:PATH,VERSION:VERSION,normalize:normalize,fingerprint:fingerprint,parse:parse,build:build,serialize:serialize,reconcile:reconcile,pair:pair};
})(window);
