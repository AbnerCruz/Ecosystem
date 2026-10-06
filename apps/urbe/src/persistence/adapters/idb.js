(function(global){
  'use strict';
  /* Adaptador IndexedDB (modo "interno" do navegador): vault guardado no próprio navegador.
     Contrato: docs/v2/contracts/persistence-adapter.md. Extraído de `FS`/`DBK` de app.js (REQ-028, RM-F1-11).
     Lojas: kv (configurações), fs (arquivos do vault: chave "<vault>/<caminho>"), blobs (cache offline de anexos). */
  var A=global.UrbeAdapters=global.UrbeAdapters||{};
  A.idb={create:function(opts){
    opts=opts||{};var name=opts.dbName||'knowledge-city',mime=opts.mime||function(){return ''},pr=null;
    function open(){if(pr)return pr;pr=new Promise(function(ok,err){var r=global.indexedDB.open(name,3);
      r.onupgradeneeded=function(e){var d=e.target.result;['kv','fs','blobs'].forEach(function(s){if(!d.objectStoreNames.contains(s))d.createObjectStore(s)})};
      r.onsuccess=function(){ok(r.result)};r.onerror=function(){err(r.error)}});return pr}
    function op(store,mode,fn){return open().then(function(d){return new Promise(function(ok,err){var t=d.transaction(store,mode),q=fn(t.objectStore(store));var result;q.onsuccess=function(){result=q.result};q.onerror=function(){err(q.error)};t.oncomplete=function(){ok(result)};t.onerror=function(){err(t.error)};t.onabort=function(){err(t.error||new Error('Gravação cancelada pelo armazenamento.'))}})})}
    /* mesma API (nomes) do antigo DBK: usada pelo restante do app para configurações e cache de anexos */
    var store={
      get:function(k){return op('kv','readonly',function(s){return s.get(k)})},set:function(k,v){return op('kv','readwrite',function(s){return s.put(v,k)})},del:function(k){return op('kv','readwrite',function(s){return s.delete(k)})},
      fGet:function(k){return op('fs','readonly',function(s){return s.get(k)})},fSet:function(k,v){return op('fs','readwrite',function(s){return s.put(v,k)})},fDel:function(k){return op('fs','readwrite',function(s){return s.delete(k)})},fChaves:function(){return op('fs','readonly',function(s){return s.getAllKeys()})},
      bGet:function(k){return op('blobs','readonly',function(s){return s.get(k)})},bSet:function(k,v){return op('blobs','readwrite',function(s){return s.put(v,k)})},bDel:function(k){return op('blobs','readwrite',function(s){return s.delete(k)})},bChaves:function(){return op('blobs','readonly',function(s){return s.getAllKeys()})}
    };
    return{
      kind:'idb',store:store,
      async cities(){var ks=await store.fChaves(),s=new Set();ks.forEach(function(k){var i=k.indexOf('/');if(i>0)s.add(k.slice(0,i))});return Array.from(s).sort(function(a,b){return a.localeCompare(b)})},
      async createCity(n){await store.fSet(n+'/.urbe/mapa.json','{}')},
      async removeCity(n){
        try{var bs=await store.bChaves();for(var i=0;i<bs.length;i++)if(String(bs[i]).indexOf(n+'|')===0)await store.bDel(bs[i])}catch(_){}
        var ks=await store.fChaves();for(var j=0;j<ks.length;j++)if(ks[j]===n||ks[j].indexOf(n+'/')===0)await store.fDel(ks[j]);
      },
      async list(v){var ks=await store.fChaves(),p=v+'/';return ks.filter(function(k){return k.indexOf(p)===0}).map(function(k){return k.slice(p.length)})},
      async read(v,rel){var x=await store.fGet(v+'/'+rel);return x==null?null:(x instanceof global.Blob?await x.text():x)},
      async readBlob(v,rel){var x=await store.fGet(v+'/'+rel);if(x==null)return null;return x instanceof global.Blob?x:new global.Blob([x],{type:mime(rel)})},
      async write(v,rel,text){await store.fSet(v+'/'+rel,text)},
      async writeBlob(v,rel,blob){await store.fSet(v+'/'+rel,blob)},
      async remove(v,rel){await store.fDel(v+'/'+rel)},
      async createFolder(v,path){if(path)await store.fSet(v+'/'+path+'/.pasta','')},
      async removeFolder(v,path){if(path)await store.fDel(v+'/'+path+'/.pasta')}
    };
  }};
})(window);
