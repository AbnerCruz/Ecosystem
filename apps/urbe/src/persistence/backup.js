(function(global){
  'use strict';
  /* Backup restaurável antes de migrar formato (REQ-038, ADR-0004).
     Layout: .urbe/backup/<AAAAMMDD-HHMMSS>-<de>-<para>/manifest.json e /files/<caminho>, com `.urbe/` gravado como `urbe/`
     (evita segmentos iniciados por ponto dentro do backup). Usa apenas o adaptador do vault: list/read/write/remove. */
  var ROOT='.urbe/backup';
  function pad(n){return String(n).padStart(2,'0')}
  function stamp(ms){var d=new Date(ms);return d.getUTCFullYear()+pad(d.getUTCMonth()+1)+pad(d.getUTCDate())+'-'+pad(d.getUTCHours())+pad(d.getUTCMinutes())+pad(d.getUTCSeconds())}
  function mapPath(p){return p.indexOf('.urbe/')===0?'urbe/'+p.slice(6):p}
  function unmapPath(p){return p.indexOf('urbe/')===0?'.urbe/'+p.slice(5):p}
  async function sha256(text){
    try{var subtle=global.crypto&&global.crypto.subtle;if(!subtle)return null;var buf=await subtle.digest('SHA-256',new TextEncoder().encode(text));return Array.from(new Uint8Array(buf)).map(function(b){return b.toString(16).padStart(2,'0')}).join('')}catch(_){return null}
  }
  /** paths: caminhos do vault a copiar (os inexistentes são registrados em `absent`). */
  async function create(adapter,vault,paths,opts){
    opts=opts||{};var now=opts.now||Date.now(),dir=ROOT+'/'+stamp(now)+'-'+opts.from+'-'+opts.to;
    var existing=new Set(await adapter.list(vault)),n=2,base=dir;while(existing.has(dir+'/manifest.json'))dir=base+'-'+(n++);
    var files=[],absent=[],given=opts.contents; // contents: Map caminho→texto capturado antes de qualquer escrita (opcional)
    for(var i=0;i<paths.length;i++){var p=paths[i],text=given&&given.has(p)?given.get(p):(existing.has(p)?await adapter.read(vault,p):null);
      if(text==null){absent.push(p);continue}
      await adapter.write(vault,dir+'/files/'+mapPath(p),text);files.push({path:p,size:text.length,sha256:await sha256(text)});
    }
    var manifest={version:1,from:opts.from,to:opts.to,reason:opts.reason||'migration',at:new Date(now).toISOString(),files:files,absent:absent};
    await adapter.write(vault,dir+'/manifest.json',JSON.stringify(manifest,null,1));
    return{dir:dir,manifest:manifest};
  }
  async function list(adapter,vault){
    var out=[];var paths=await adapter.list(vault);
    for(var i=0;i<paths.length;i++){var m=paths[i].match(/^\.urbe\/backup\/([^/]+)\/manifest\.json$/);if(!m)continue;
      try{out.push({dir:ROOT+'/'+m[1],manifest:JSON.parse(await adapter.read(vault,paths[i]))})}catch(_){}}
    return out.sort(function(a,b){return String(b.manifest.at).localeCompare(String(a.manifest.at))});
  }
  /** Restaura os arquivos do backup e remove os que não existiam na época (`absent`). Verifica o hash quando disponível. */
  async function restore(adapter,vault,dir){
    var manifest=JSON.parse(await adapter.read(vault,dir+'/manifest.json')),restored=[],removed=[];
    for(var i=0;i<manifest.files.length;i++){var f=manifest.files[i],text=await adapter.read(vault,dir+'/files/'+mapPath(f.path));
      if(text==null)throw new Error('backup incompleto: '+f.path);
      if(f.sha256){var h=await sha256(text);if(h&&h!==f.sha256)throw new Error('backup corrompido: '+f.path)}
      await adapter.write(vault,f.path,text);restored.push(f.path)}
    for(var j=0;j<manifest.absent.length;j++){try{await adapter.remove(vault,manifest.absent[j]);removed.push(manifest.absent[j])}catch(_){}}
    return{restored:restored,removed:removed,manifest:manifest};
  }
  global.UrbeBackup={ROOT:ROOT,create:create,list:list,restore:restore,mapPath:mapPath,unmapPath:unmapPath};
})(window);
