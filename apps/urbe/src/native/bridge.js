/* Urbe instalado (Windows/Android): ponte entre o app e o sistema.
   No navegador este arquivo não faz nada. No app instalado, window.UrbeNative é fornecido
   pela casca nativa (Electron no computador, Capacitor no Android) e aqui vira:
   - UrbeNativeFS: "handles" de pasta com a mesma interface do File System Access do Chrome
     (getDirectoryHandle, getFileHandle, entries, createWritable…). O resto do app já sabe usar
     esses handles, então notas, anexos, mapa e Tutorial passam a ser arquivos de verdade na
     pasta do Urbe (por padrão Documentos/Urbe), sem outra mudança;
   - downloads, janelas e links externos que o WebView do Android não faz sozinho. */
(function(global){
  'use strict';
  var N=global.UrbeNative||null;
  var cap=global.Capacitor;
  if(!N&&cap&&typeof cap.isNativePlatform==='function'&&cap.isNativePlatform())N=global.UrbeNative=capacitorNative(cap);
  if(!N||!N.fs)return;

  /* ---------------- utilidades ---------------- */
  function err(name,msg){try{return new DOMException(msg||name,name)}catch(_){var e=new Error(msg||name);e.name=name;return e}}
  function join(a,b){return a?a+'/'+b:b}
  function checkName(n){n=String(n==null?'':n);if(!n||n==='.'||n==='..'||/[\/\\\u0000]/.test(n))throw new TypeError('Nome inválido: '+n);return n}
  var MIME={md:'text/markdown',markdown:'text/markdown',txt:'text/plain',json:'application/json',html:'text/html',htm:'text/html',css:'text/css',js:'text/javascript',mjs:'text/javascript',csv:'text/csv',svg:'image/svg+xml',
    png:'image/png',jpg:'image/jpeg',jpeg:'image/jpeg',gif:'image/gif',webp:'image/webp',avif:'image/avif',bmp:'image/bmp',ico:'image/x-icon',pdf:'application/pdf',mp3:'audio/mpeg',wav:'audio/wav',ogg:'audio/ogg',m4a:'audio/mp4',
    mp4:'video/mp4',webm:'video/webm',mov:'video/quicktime',zip:'application/zip',docx:'application/vnd.openxmlformats-officedocument.wordprocessingml.document',xlsx:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',pptx:'application/vnd.openxmlformats-officedocument.presentationml.presentation'};
  function mimeOf(name){var m=/\.([a-z0-9]+)$/i.exec(name||'');return m&&MIME[m[1].toLowerCase()]||''}
  async function toBytes(d){
    if(d==null)return new Uint8Array(0);
    if(typeof d==='string')return new TextEncoder().encode(d);
    if(d instanceof Uint8Array)return d;
    if(d instanceof ArrayBuffer)return new Uint8Array(d);
    if(ArrayBuffer.isView(d))return new Uint8Array(d.buffer,d.byteOffset,d.byteLength);
    if(typeof Blob!=='undefined'&&d instanceof Blob)return new Uint8Array(await d.arrayBuffer());
    if(typeof d==='object'&&d.type==='write')return toBytes(d.data);
    return new TextEncoder().encode(String(d));
  }
  function concat(parts){var n=0,i,o=0;for(i=0;i<parts.length;i++)n+=parts[i].length;var out=new Uint8Array(n);for(i=0;i<parts.length;i++){out.set(parts[i],o);o+=parts[i].length}return out}

  /* ---------------- espelho da pasta em memória ----------------
     Abrir o app lia arquivo por arquivo pela ponte: no Android eram ~560 chamadas (verificar
     cada pasta, cada arquivo, ler, e tudo duas vezes), o "Lendo 9 / 54" de toda abertura.
     Agora a pasta inteira vem em 2 chamadas (lista + textos) e fica num espelho: verificar,
     listar e ler textos saem da memória; gravar e apagar vão ao disco e atualizam o espelho.
     Mudanças feitas por fora: no Android o espelho é recarregado ao voltar para o app; no
     computador ele é desligado depois de abrir (o vigia da pasta avisa arquivo a arquivo). */
  var raw=N.fs,mir=null;
  var TEXTO=/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv|canvas)$/i;
  function pai(r){var i=r.lastIndexOf('/');return i<0?'':r.slice(0,i)}
  function nome(r){return r.slice(r.lastIndexOf('/')+1)}
  function mirAdd(r,kind){if(!mir||!r)return;var p=pai(r);if(p&&!mir.kinds.has(p))mirAdd(p,'directory');mir.kinds.set(r,kind);if(!mir.kids.has(p))mir.kids.set(p,new Set());mir.kids.get(p).add(nome(r));if(kind==='directory'&&!mir.kids.has(r))mir.kids.set(r,new Set())}
  function mirDel(r){if(!mir)return;var k=mir.kids.get(r);if(k)Array.from(k).forEach(function(n){mirDel(r?r+'/'+n:n)});mir.kids.delete(r);mir.kinds.delete(r);mir.texts.delete(r);var s2=mir.kids.get(pai(r));if(s2)s2.delete(nome(r))}
  async function prefetch(){
    if(!raw.tree||!raw.readTexts)return false;
    try{var list=await raw.tree(),m={kinds:new Map([['','directory']]),kids:new Map([['',new Set()]]),texts:new Map()},want=[];
      mir=m;(list||[]).forEach(function(e){mirAdd(e.path,e.kind);if(e.kind==='file'&&TEXTO.test(e.path)&&(e.size||0)<=4194304)want.push(e.path)});
      var got=want.length?await raw.readTexts(want):{};
      want.forEach(function(p){if(Object.prototype.hasOwnProperty.call(got,p))m.texts.set(p,got[p])});
      return true}catch(e){console.info('espelho da pasta indisponível; lendo arquivo a arquivo',e&&e.message);mir=null;return false}
  }
  var enc=new TextEncoder(),dec=new TextDecoder();
  var fs={
    stat:async function(r){if(mir){var k=mir.kinds.get(r);if(!k)return null;if(k==='directory')return{kind:k,size:0,mtime:0};if(mir.texts.has(r))return{kind:'file',size:mir.texts.get(r).length,mtime:0}}return raw.stat(r)},
    list:async function(r){if(mir&&mir.kinds.get(r||'')==='directory'){var out=[];(mir.kids.get(r||'')||new Set()).forEach(function(n){var c=r?r+'/'+n:n;out.push({name:n,kind:mir.kinds.get(c)||'file'})});return out}return raw.list(r)},
    readBytes:async function(r){if(mir&&mir.texts.has(r))return enc.encode(mir.texts.get(r));return raw.readBytes(r)},
    writeBytes:async function(r,b){await raw.writeBytes(r,b);if(mir){mirAdd(r,'file');if(TEXTO.test(r)&&b.length<=4194304)mir.texts.set(r,dec.decode(b));else mir.texts.delete(r)}},
    mkdir:async function(r){await raw.mkdir(r);mirAdd(r,'directory')},
    remove:async function(r,rec){await raw.remove(r,rec);mirDel(r)}
  };

  /* ---------------- handles compatíveis com File System Access ---------------- */
  function FileH(rel,name){this.kind='file';this.name=name;this._rel=rel}
  FileH.prototype.getFile=async function(){
    var st=await fs.stat(this._rel);if(!st||st.kind!=='file')throw err('NotFoundError','Arquivo não encontrado: '+this._rel);
    var bytes=await fs.readBytes(this._rel);
    return new File([bytes],this.name,{type:mimeOf(this.name),lastModified:st.mtime||Date.now()});
  };
  FileH.prototype.createWritable=async function(opts){
    var rel=this._rel,parts=[],closed=false;
    if(opts&&opts.keepExistingData){try{parts.push(await fs.readBytes(rel))}catch(_){}}
    return{
      write:async function(d){if(closed)throw err('InvalidStateError');parts.push(await toBytes(d))},
      truncate:async function(n){var all=concat(parts);parts=[all.slice(0,n)]},
      close:async function(){if(closed)return;closed=true;await fs.writeBytes(rel,concat(parts))},
      abort:async function(){closed=true;parts=[]}
    };
  };
  FileH.prototype.isSameEntry=async function(o){return !!o&&o.kind==='file'&&o._rel===this._rel};
  FileH.prototype.queryPermission=FileH.prototype.requestPermission=async function(){return 'granted'};

  function DirH(rel,name){this.kind='directory';this.name=name;this._rel=rel}
  DirH.prototype.getDirectoryHandle=async function(name,o){
    var p=join(this._rel,checkName(name)),st=await fs.stat(p);
    if(!st){if(!(o&&o.create))throw err('NotFoundError','Pasta não encontrada: '+p);await fs.mkdir(p)}
    else if(st.kind!=='directory')throw err('TypeMismatchError',p+' é um arquivo');
    return new DirH(p,name);
  };
  DirH.prototype.getFileHandle=async function(name,o){
    var p=join(this._rel,checkName(name)),st=await fs.stat(p);
    if(!st){if(!(o&&o.create))throw err('NotFoundError','Arquivo não encontrado: '+p);await fs.writeBytes(p,new Uint8Array(0))}
    else if(st.kind!=='file')throw err('TypeMismatchError',p+' é uma pasta');
    return new FileH(p,name);
  };
  DirH.prototype.removeEntry=async function(name,o){
    var p=join(this._rel,checkName(name)),st=await fs.stat(p);
    if(!st)throw err('NotFoundError','Não encontrado: '+p);
    if(st.kind==='directory'&&!(o&&o.recursive)&&(await fs.list(p)).length)throw err('InvalidModificationError','A pasta não está vazia: '+p);
    await fs.remove(p,!!(o&&o.recursive));
  };
  DirH.prototype.entries=async function*(){
    var list=await fs.list(this._rel);
    for(var i=0;i<list.length;i++){var e=list[i],p=join(this._rel,e.name);yield [e.name,e.kind==='directory'?new DirH(p,e.name):new FileH(p,e.name)]}
  };
  DirH.prototype.values=async function*(){for await(var e of this.entries())yield e[1]};
  DirH.prototype.keys=async function*(){for await(var e of this.entries())yield e[0]};
  DirH.prototype[Symbol.asyncIterator]=function(){return this.entries()};
  DirH.prototype.isSameEntry=async function(o){return !!o&&o.kind==='directory'&&o._rel===this._rel};
  DirH.prototype.resolve=async function(o){if(!o||typeof o._rel!=='string')return null;if(!this._rel)return o._rel?o._rel.split('/'):[];return o._rel.indexOf(this._rel+'/')===0?o._rel.slice(this._rel.length+1).split('/'):null};
  DirH.prototype.queryPermission=DirH.prototype.requestPermission=async function(){return 'granted'};

  /* A raiz que o app enxerga guarda exatamente um vault, "Urbe", que é a pasta escolhida.
     Assim nenhuma outra pasta do aparelho vira "cidade" nem é tocada. */
  var VAULT='Urbe';
  function RootH(label){this.kind='directory';this.name=label||'Urbe';this._rel=null}
  RootH.prototype.getDirectoryHandle=async function(name){if(name===VAULT)return new DirH('',VAULT);throw err('NotAllowedError','O app instalado guarda um vault só.')};
  RootH.prototype.getFileHandle=async function(){throw err('NotAllowedError','Use a pasta do vault.')};
  RootH.prototype.removeEntry=async function(){throw err('NotAllowedError','A pasta do Urbe não é apagada pelo app.')};
  RootH.prototype.entries=async function*(){yield [VAULT,new DirH('',VAULT)]};
  RootH.prototype.values=async function*(){yield new DirH('',VAULT)};
  RootH.prototype.keys=async function*(){yield VAULT};
  RootH.prototype[Symbol.asyncIterator]=function(){return this.entries()};
  RootH.prototype.isSameEntry=async function(o){return o instanceof RootH};
  RootH.prototype.queryPermission=RootH.prototype.requestPermission=async function(){return 'granted'};

  var soltarNoLoad=false;
  async function root(){var v=await N.vault();if(!mir)await prefetch();
    /* no computador o espelho só serve para abrir rápido: depois o vigia da pasta cuida das mudanças */
    if(N.onVaultChanged&&!soltarNoLoad){soltarNoLoad=true;try{global.UrbeCore.events.on('workspace:loaded',function(){setTimeout(function(){mir=null},0)})}catch(_){}}
    return new RootH(v&&v.label)}

  global.UrbeNativeFS={root:root,DirH:DirH,FileH:FileH,RootH:RootH,mimeOf:mimeOf,toBytes:toBytes,
    prefetch:prefetch,release:function(){mir=null},mirrored:function(){return !!mir}};
  /* trocar de pasta: o espelho é da pasta antiga */
  if(N.pickVault){var pick0=N.pickVault;global.showDirectoryPicker=async function(){var v=await pick0();if(!v)throw err('AbortError','Cancelado');mir=null;await prefetch();return new RootH(v.label)}}
  /* o seletor de pasta do app passa a abrir o do sistema (e lembra a escolha do lado nativo) */

  /* o app instalado se atualiza pelo instalador, não pelo service worker */
  try{if(global.navigator&&global.navigator.serviceWorker){var reg=global.navigator.serviceWorker.getRegistrations;if(reg)reg.call(global.navigator.serviceWorker).then(function(rs){rs.forEach(function(r){r.unregister()})}).catch(function(){})}}catch(_){}
  try{document.documentElement.classList.add('urbe-native','urbe-'+(N.platform||'app'))}catch(_){}

  /* ---------------- mudanças feitas por fora do app ---------------- */
  function persistence(){var c=global.UrbeCore;return c&&c.service&&c.service('persistence')}
  function sync(paths){var p=persistence();if(!p||!p.syncFromDisk)return;p.syncFromDisk(paths).catch(function(e){console.warn('sincronizar com a pasta',e)})}
  /* pasta (sem extensão) mudou ou sumiu: relê tudo; senão só os arquivos avisados */
  if(N.onVaultChanged)N.onVaultChanged(function(paths){sync(paths&&paths.length&&paths.every(function(p){return /\.[^\/.]+$/.test(p)})?paths:null)});
  else{var lastScan=0;document.addEventListener('visibilitychange',function(){if(document.visibilityState!=='visible')return;var t=Date.now();if(t-lastScan<3000)return;lastScan=t;
    setTimeout(function(){(mir?prefetch():Promise.resolve()).then(function(){sync(null)})},400)})}

  /* ---------------- downloads, janelas e links (quando a casca não faz sozinha) ---------------- */
  function toast(m){var t=document.getElementById('toast');if(!t)return;t.textContent=m;t.classList.add('show');clearTimeout(toast._t);toast._t=setTimeout(function(){t.classList.remove('show')},3200)}
  global.UrbeNativeToast=toast;
  if(N.saveFile){
    var saveFrom=function(href,name){
      var p=fetch(href).then(function(r){return r.blob()});
      return p.then(async function(b){var bytes=await toBytes(b);var r=await N.saveFile(name||'arquivo',bytes,b.type||mimeOf(name));toast(r&&r.where?'Salvo em '+r.where:'Arquivo salvo.')})
        .catch(function(e){toast('Não consegui salvar: '+(e&&e.message||e))});
    };
    var click=HTMLAnchorElement.prototype.click;
    HTMLAnchorElement.prototype.click=function(){
      if(this.hasAttribute('download')&&/^(blob|data):/i.test(this.href)){saveFrom(this.href,this.getAttribute('download')||this.download);return}
      return click.apply(this,arguments);
    };
    document.addEventListener('click',function(e){
      var a=e.target&&e.target.closest&&e.target.closest('a[download]');
      if(a&&/^(blob|data):/i.test(a.href)){e.preventDefault();e.stopPropagation();saveFrom(a.href,a.getAttribute('download'))}
    },true);
  }
  if(N.openExternal){
    document.addEventListener('click',function(e){
      if(e.defaultPrevented)return;var a=e.target&&e.target.closest&&e.target.closest('a[href]');if(!a)return;
      var h=a.getAttribute('href')||'';if(/^(https?:|mailto:|tel:)/i.test(h)&&(a.target==='_blank'||!/^https?:\/\/(localhost|urbe)\b/i.test(h))){e.preventDefault();N.openExternal(a.href)}
    },false);
  }
  if(N.shell==='capacitor'){
    /* o WebView do Android não abre janelas: http vai para o navegador; conteúdo do próprio app
       (blob:) abre por cima do app, com um botão para fechar */
    var open0=global.open;
    global.open=function(url){
      url=String(url||'');
      if(/^(https?:|mailto:|tel:)/i.test(url)){N.openExternal(url);return fakeWin()}
      if(/^blob:/i.test(url))return overlay(url);
      try{return open0.apply(global,arguments)}catch(_){return null}
    };
  }
  if(N.shell==='capacitor'){
    /* "voltar" do Android: fecha o que está por cima; senão sai da tela atual; na cidade, minimiza */
    var state=function(){return document.body.innerHTML.length+':'+document.querySelectorAll('.open,[aria-modal="true"]').length};
    var shown=function(el){if(!el)return false;var r=el.getBoundingClientRect();return r.width>0&&r.height>0&&getComputedStyle(el).visibility!=='hidden'};
    document.addEventListener('urbeBack',function(){
      var ov=document.querySelectorAll('.urbe-native-view');if(ov.length){ov[ov.length-1].remove();return}
      var before=state(),t=document.activeElement&&document.activeElement!==document.body?document.activeElement:document;
      t.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',code:'Escape',keyCode:27,which:27,bubbles:true,cancelable:true}));
      setTimeout(function(){
        if(state()!==before)return;
        var back=[].slice.call(document.querySelectorAll('button[aria-label="Voltar"],button[aria-label="Fechar"]')).filter(shown).pop();
        if(back){back.click();return}
        N.minimize&&N.minimize();
      },150);
    });
  }
  function fakeWin(){return{closed:false,close:function(){},focus:function(){},document:null}}
  function overlay(url){
    var o=document.createElement('div');o.className='urbe-native-view';
    o.style.cssText='position:fixed;inset:0;z-index:2147483000;background:#0b0e14;display:flex;flex-direction:column;padding-top:env(safe-area-inset-top)';
    o.innerHTML='<div style="display:flex;justify-content:flex-end;gap:8px;padding:8px"><button type="button" data-save style="padding:10px 14px;border-radius:10px;border:1px solid #334;background:#161b26;color:#e6e9f2;font:600 14px system-ui">Salvar</button><button type="button" data-x style="padding:10px 14px;border-radius:10px;border:0;background:#7c9bff;color:#0b0e14;font:700 14px system-ui">Fechar</button></div>';
    var f=document.createElement('iframe');f.src=url;f.setAttribute('sandbox','allow-scripts allow-popups allow-forms');f.style.cssText='flex:1;border:0;background:#fff;width:100%';o.appendChild(f);
    o.querySelector('[data-x]').onclick=function(){o.remove()};
    o.querySelector('[data-save]').onclick=function(){fetch(url).then(function(r){return r.blob()}).then(async function(b){var ext=(b.type.split('/')[1]||'bin').replace(/\W.*/,'');var r=await N.saveFile('urbe.'+ext,await toBytes(b),b.type);toast(r&&r.where?'Salvo em '+r.where:'Arquivo salvo.')}).catch(function(){toast('Não consegui salvar.')})};
    document.body.appendChild(o);var w=fakeWin();w.close=function(){o.remove()};return w;
  }

  /* ---------------- Android (Capacitor): monta o UrbeNative sobre os plugins ---------------- */
  function capacitorNative(cap){
    function call(plugin,method,opts){
      if(typeof cap.nativePromise==='function')return cap.nativePromise(plugin,method,opts||{});
      var P=cap.Plugins&&cap.Plugins[plugin];
      if(P&&typeof P[method]==='function')return P[method](opts||{});
      return Promise.reject(new Error('Plugin indisponível: '+plugin));
    }
    var DIR='DOCUMENTS',BASE='Urbe';
    function path(rel){return rel?BASE+'/'+rel:BASE}
    function b64(bytes){var s='',i,ch=0x8000;for(i=0;i<bytes.length;i+=ch)s+=String.fromCharCode.apply(null,bytes.subarray(i,i+ch));return btoa(s)}
    function unb64(s){var bin=atob(s||''),out=new Uint8Array(bin.length);for(var i=0;i<bin.length;i++)out[i]=bin.charCodeAt(i);return out}
    function missing(e){var m=String(e&&(e.message||e.errorMessage)||e);return /does not exist|not exist|no such|ENOENT|not found|FileNotFound/i.test(m)}
    var fsA={
      stat:async function(rel){try{var s=await call('Filesystem','stat',{path:path(rel),directory:DIR});return{kind:s.type==='directory'?'directory':'file',size:s.size||0,mtime:+s.mtime||0}}catch(e){if(missing(e))return null;throw e}},
      list:async function(rel){var r=await call('Filesystem','readdir',{path:path(rel),directory:DIR});return (r.files||[]).map(function(f){return typeof f==='string'?{name:f,kind:'file'}:{name:f.name,kind:f.type==='directory'?'directory':'file',size:f.size||0,mtime:+f.mtime||0}})},
      readBytes:async function(rel){var r=await call('Filesystem','readFile',{path:path(rel),directory:DIR});return typeof r.data==='string'?unb64(r.data):await toBytes(r.data)},
      writeBytes:async function(rel,bytes){await call('Filesystem','writeFile',{path:path(rel),directory:DIR,data:b64(bytes),recursive:true})},
      mkdir:async function(rel){try{await call('Filesystem','mkdir',{path:path(rel),directory:DIR,recursive:true})}catch(e){if(!/exist/i.test(String(e&&e.message)))throw e}},
      tree:async function(){var r=await call('UrbeAndroid','listTree');if(!r||r.exists===false){await fsA.mkdir('');return[]}return r.entries||[]},
      readTexts:async function(paths){var r=await call('UrbeAndroid','readTexts',{paths:paths});return (r&&r.files)||{}},
      remove:async function(rel,recursive){var s=await fsA.stat(rel);if(!s)return;if(s.kind==='directory')await call('Filesystem','rmdir',{path:path(rel),directory:DIR,recursive:!!recursive});else await call('Filesystem','deleteFile',{path:path(rel),directory:DIR})}
    };
    var info=null;
    /* atualização: compara com o último lançamento do GitHub e baixa o APK pelo navegador
       (o Android mostra "Instalar" quando o download termina) */
    var REPO='AbnerCruz/Urbe',last={state:'none'},subs=[];
    function emit(st){last=st;subs.forEach(function(f){try{f(st)}catch(_){}})}
    function parts(v){var m=/^v?(\d+)\.(\d+)\.(\d+)(?:-([\w.]+))?/.exec(String(v||''));return m?[+m[1],+m[2],+m[3],m[4]||'']:null}
    function newer(a,b){a=parts(a);b=parts(b);if(!a||!b)return false;for(var i=0;i<3;i++)if(a[i]!==b[i])return a[i]>b[i];if(a[3]===b[3])return false;if(!a[3])return true;if(!b[3])return false;return a[3].localeCompare(b[3],undefined,{numeric:true})>0}
    var update={
      check:async function(){
        try{emit({state:'checking'});
          var cur=(await (info?Promise.resolve(info):call('UrbeAndroid','getInfo').then(function(r){info=r;return r}))).version;
          var r=await fetch('https://api.github.com/repos/'+REPO+'/releases/latest',{cache:'no-store',headers:{Accept:'application/vnd.github+json'}});
          if(!r.ok)throw new Error('GitHub respondeu '+r.status);
          var rel=await r.json(),apk=(rel.assets||[]).filter(function(a){return /\.apk$/i.test(a.name)})[0],ver=String(rel.tag_name||'').replace(/^v/,'');
          if(apk&&newer(ver,cur))emit({state:'available',manualInstall:true,version:ver,url:apk.browser_download_url,notes:rel.body||''});
          else emit({state:'none',version:cur});
        }catch(e){emit({state:'error',message:String(e&&e.message||e)})}
        return last;
      },
      install:function(){if(last.url)return call('UrbeAndroid','openUrl',{url:last.url})},
      onStatus:function(fn){if(typeof fn==='function'){subs.push(fn);if(last.state!=='none')fn(last)}},
      _newer:newer
    };
    return{
      shell:'capacitor',platform:'android',
      info:async function(){if(!info)info=await call('UrbeAndroid','getInfo');return info},
      vault:async function(){await fsA.mkdir('');return{label:'Documentos/'+BASE,path:'Documents/'+BASE}},
      fs:fsA,
      storage:{
        status:function(){return call('UrbeAndroid','storageStatus')},
        requestAllFiles:function(){return call('UrbeAndroid','requestAllFiles')},
        requestLegacy:function(){return call('Filesystem','requestPermissions',{permissions:['publicStorage']}).catch(function(){})}
      },
      openExternal:function(url){return call('UrbeAndroid','openUrl',{url:String(url)})},
      saveFile:function(name,bytes,mime){return call('UrbeAndroid','saveFile',{name:String(name),data:b64(bytes),mime:mime||''})},
      printHtml:function(html,name){return call('UrbeAndroid','printHtml',{html:String(html),name:String(name||'Urbe')})},
      minimize:function(){return call('UrbeAndroid','minimize')},
      update:update
    };
  }
})(window);
