(function(global){
  'use strict';
  /* Versão de formato do vault (REQ-036, ADR-0004): `.urbe/vault.json`.
     Vault sem o arquivo é um vault 1.x; `formatVersion` maior que o suportado ⇒ modo seguro (somente leitura). */
  var FORMAT_VERSION=2;
  var PATH='.urbe/vault.json';

  /** raw: texto do arquivo (ou null). → {state:'absent'|'current'|'future'|'corrupt', data} */
  function parse(raw){
    if(raw==null||raw==='')return{state:'absent',data:null};
    var data;try{data=JSON.parse(raw)}catch(_){return{state:'corrupt',data:null}}
    if(!data||typeof data!=='object'||typeof data.formatVersion!=='number')return{state:'corrupt',data:null};
    if(data.formatVersion>FORMAT_VERSION)return{state:'future',data:data};
    return{state:'current',data:data};
  }
  function writer(version){return'urbe@'+(version||'?')}
  /** Cria o conteúdo inicial de vault.json. migration: entrada opcional de migração já realizada. */
  function create(opts){
    opts=opts||{};var now=new Date(opts.now||Date.now()).toISOString();
    return{formatVersion:FORMAT_VERSION,createdBy:writer(opts.appVersion),lastWriter:writer(opts.appVersion),created:now,migrations:opts.migration?[opts.migration]:[]};
  }
  /** Atualiza lastWriter (não muda formatVersion nem migrations). */
  function touch(data,appVersion){var o=JSON.parse(JSON.stringify(data));o.lastWriter=writer(appVersion);return o}
  function migrationEntry(o){return{id:o.id||'1-to-2',from:o.from,to:o.to,at:new Date(o.now||Date.now()).toISOString(),backup:o.backup||null}}
  function serialize(data){return JSON.stringify(data,null,1)}

  global.UrbeVaultMeta={FORMAT_VERSION:FORMAT_VERSION,PATH:PATH,parse:parse,create:create,touch:touch,migrationEntry:migrationEntry,serialize:serialize};
})(window);
