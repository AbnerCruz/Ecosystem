(function(global){
  'use strict';
  /* Adaptador de pasta real: File System Access API (Chrome/Edge) e também os "handles" que a ponte nativa
     emula no Windows (Electron) e no Android (src/native/bridge.js). Contrato: docs/v2/contracts/persistence-adapter.md.
     Extraído de `FS`/`fsa*` de app.js (REQ-028, RM-F1-11/12). root(): FileSystemDirectoryHandle que contém uma pasta por vault. */
  var A=global.UrbeAdapters=global.UrbeAdapters||{};
  A.fsa={create:function(opts){
    var root=opts.root,cache={name:null,h:null};
    async function dir(base,parts,create){var d=base;for(var i=0;i<parts.length;i++){var p=parts[i];if(!p)continue;d=await d.getDirectoryHandle(p,{create:!!create})}return d}
    async function walk(d,prefix,out){
      for await(var pair of d.entries()){var nome=pair[0],h=pair[1],rel=prefix?prefix+'/'+nome:nome;
        if(h.kind==='directory'){if(nome!=='.urbe'&&nome.charAt(0)==='.')continue;await walk(h,rel,out)}else out.push(rel)}
      return out;
    }
    async function city(name,create){if(cache.name===name&&cache.h)return cache.h;var h=await root().getDirectoryHandle(name,{create:!!create});cache={name:name,h:h};return h}
    function split(rel){var seg=rel.split('/'),file=seg.pop();return{seg:seg,file:file}}
    async function put(v,rel,data){var s=split(rel),d=await dir(await city(v),s.seg,true),fh=await d.getFileHandle(s.file,{create:true}),w=await fh.createWritable();await w.write(data);await w.close()}
    return{
      kind:'fsa',
      resetCache:function(){cache={name:null,h:null}},
      async cities(){var out=[];for await(var pair of root().entries())if(pair[1].kind==='directory'&&pair[0].charAt(0)!=='.')out.push(pair[0]);return out.sort(function(a,b){return a.localeCompare(b)})},
      async createCity(n){await root().getDirectoryHandle(n,{create:true})},
      async removeCity(n){await root().removeEntry(n,{recursive:true});if(cache.name===n)cache={name:null,h:null}},
      async list(v){return walk(await city(v),'',[])},
      async read(v,rel){try{var s=split(rel),d=await dir(await city(v),s.seg,false);return await(await(await d.getFileHandle(s.file)).getFile()).text()}catch(_){return null}},
      async readBlob(v,rel){try{var s=split(rel),d=await dir(await city(v),s.seg,false);return await(await d.getFileHandle(s.file)).getFile()}catch(_){return null}},
      write:function(v,rel,text){return put(v,rel,text)},
      writeBlob:function(v,rel,blob){return put(v,rel,blob)},
      async remove(v,rel){try{var s=split(rel),d=await dir(await city(v),s.seg,false);await d.removeEntry(s.file)}catch(_){}},
      async createFolder(v,path){if(!path)return;await dir(await city(v),path.split('/'),true)},
      async removeFolder(v,path){if(!path)return;var seg=path.split('/'),alvo=seg.pop();try{var d=await dir(await city(v),seg,false);await d.removeEntry(alvo)}catch(_){}}
    };
  }};
})(window);
