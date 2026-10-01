(function(global){
  'use strict';
  /* Migração multi-cidade da 1.x (REQ-045, RM-F1-19): instalações antigas tinham várias cidades (vaults) lado a lado;
     a 2.x usa uma só ("Urbe") e copia cada cidade para `Cidades/<nome>/`, deixando a original intacta.
     Divisão de responsabilidades:
       1. antes do load (app.js, urbeEnsureSingleVault): copia os arquivos (pula o que já existe) e grava a cópia do mapa da origem
          em `.urbe/origens/<nome>.json`; marca a cidade em `.urbe/merged-v1.json` com `pendente:true`;
       2. no load (gancho `addLoadHook`, antes de montar os documentos): funde os mapas pendentes no mapa do WorkspacePersistence;
          ao terminar o load (`workspace:loaded`): grava (único escritor de mapa.json), registra `multi-city` em
          `vault.json.migrations` e só então tira o `pendente` do marcador.
     Uma cidade marcada nunca é copiada de novo (idempotente); fusão interrompida é refeita no próximo boot, sem duplicar.
     Arquivar = esconder as cidades de origem da lista (`vault.json.archivedCities`); nenhum arquivo é movido ou apagado. */
  var MARKER='.urbe/merged-v1.json',ORIGENS='.urbe/origens/';

  var safeName=function(s){return global.UrbeArtifacts.safeName(s)};
  function folderFor(source){return 'Cidades/'+safeName(source)}
  function parseMarker(text){try{var d=JSON.parse(text||'{}');return d&&typeof d==='object'?d:{}}catch(_){return{}}}
  /** Cidades já migradas: marcador da 1.x/2.x + registro em vault.json (se o marcador sumir, não recopia). */
  function doneSet(marker,vaultJson){
    var out=new Set(Object.keys(marker||{}).filter(function(k){return marker[k]}));
    ((vaultJson&&vaultJson.migrations)||[]).forEach(function(m){if(m&&m.id==='multi-city')(m.sources||[]).forEach(function(s){out.add(s)})});
    return out;
  }
  /** Funde o mapa de uma cidade de origem no mapa alvo, deslocando-o para a direita do que já existe. Idempotente por caminho. */
  function mergeMap(target,sourceText,folder){
    var original;try{original=JSON.parse(sourceText)}catch(_){return false}if(!original||typeof original!=='object')return false;
    target.v=target.v||4;target.regioes=target.regioes||[];target.notas=target.notas||{};target.construcoes=target.construcoes||[];
    var prefix=folder+'/',regions=original.regioes||[],notes=original.notas||{},cons=original.construcoes||[];
    var withPrefix=function(p){return p?prefix+p:folder};
    var already=target.regioes.some(function(r){return r&&(r.caminho===folder||String(r.caminho||'').indexOf(prefix)===0)})||Object.keys(target.notas).some(function(p){return p.indexOf(prefix)===0});
    if(already)return false; // fusão já aplicada (boot interrompido depois de gravar o mapa)
    var positions=target.regioes.map(function(r){return{x:r.x||0,w:r.w||0}}).concat(target.construcoes.map(function(b){return{x:b.x||0,w:b.w||0}}),Object.keys(target.notas).map(function(k){return{x:target.notas[k].x||0,w:3}}));
    var minX=Math.min.apply(null,[0].concat(regions.map(function(r){return r.x||0}),Object.keys(notes).map(function(k){return notes[k].x||0}),cons.map(function(b){return b.x||0})));
    var dx=positions.length?Math.max.apply(null,positions.map(function(p){return p.x+p.w}))+40-minX:0;
    var asset=function(a){return Object.assign({},a,{relPath:a.relPath?withPrefix(a.relPath):a.relPath,folderPath:a.folderPath?withPrefix(a.folderPath):folder})};
    regions.forEach(function(r){target.regioes.push(Object.assign({},r,{caminho:withPrefix(r.caminho),x:(r.x||0)+dx}))});
    Object.keys(notes).forEach(function(p){var n=notes[p];target.notas[withPrefix(p)]=Object.assign({},n,{x:(n.x||0)+dx,anexos:(n.anexos||[]).map(asset)})});
    cons.forEach(function(b){var files=(b.files||b.anexos||[]).map(asset);target.construcoes.push(Object.assign({},b,{caminho:withPrefix(b.caminho),x:(b.x||0)+dx,files:files,anexos:files}))});
    return true;
  }
  /* 2a) no load, antes dos documentos: funde os mapas pendentes (as notas copiadas já nascem com o ID da origem) */
  async function mergePendingHook(ctx){
    var marker=parseMarker(await ctx.adapter.read(ctx.vault,MARKER)),pend=Object.keys(marker).filter(function(k){return marker[k]&&marker[k].pendente});
    if(!pend.length)return;var meta=ctx.meta&&typeof ctx.meta==='object'?ctx.meta:{v:4};
    for(const s of pend){var txt=await ctx.adapter.read(ctx.vault,ORIGENS+safeName(s)+'.json');if(txt!=null)mergeMap(meta,txt,marker[s].folder||folderFor(s))}
    lastPending={vault:ctx.vault,sources:pend};return meta;
  }
  /* 2b) terminado o load: grava o mapa fundido (WorkspacePersistence), registra em vault.json e baixa a pendência do marcador */
  async function finishPending(P){
    var lp=lastPending;lastPending=null;if(!lp||!P||P.vault!==lp.vault||P.readOnly||P.mapaReadonly)return{merged:[]};
    await P.recordMigration({id:'multi-city',from:1,to:2,sources:lp.sources});await P.flush();
    var marker=parseMarker(await P.adapter.read(P.vault,MARKER));lp.sources.forEach(function(s){if(marker[s])delete marker[s].pendente});
    await P.adapter.write(P.vault,MARKER,JSON.stringify(marker));
    return{merged:lp.sources};
  }
  var lastPending=null;
  /** Cidades de origem já migradas para dentro de Urbe (candidatas a arquivar). */
  async function mergedSources(P){var marker=parseMarker(await P.adapter.read(P.vault,MARKER));return Array.from(doneSet(marker,P.vaultInfo&&P.vaultInfo.data)).sort()}
  /** Nomes escondidos da lista de cidades (nada é movido nem apagado). */
  function archived(P){var d=P&&P.vaultInfo&&P.vaultInfo.data;return new Set(d&&Array.isArray(d.archivedCities)?d.archivedCities:[])}
  async function setArchived(P,on){
    if(!P||!P.vault||P.readOnly)throw new Error('Abra o vault (não somente leitura) para arquivar as cidades antigas.');
    var list=on?await mergedSources(P):[];await P.ensureVaultFormat();P.vaultInfo.data.archivedCities=list;P.schedule();await P.flush();return list;
  }
  var core=global.UrbeCore,P0=core&&core.service('persistence');
  if(P0&&P0.addLoadHook){P0.addLoadHook(mergePendingHook);core.events.on('workspace:loaded',function(){api.finishing=finishPending(P0).catch(function(e){console.warn('multi-cidade: fusão não gravada; será refeita no próximo boot',e);return{merged:[],error:e}})})}
  if(core){
    core.commands.register('workspace.archiveOldCities',{title:'Arquivar cidades antigas',category:'Workspace',execute:function(){return setArchived(core.service('persistence'),true)}});
    core.commands.register('workspace.unarchiveOldCities',{title:'Mostrar cidades antigas',category:'Workspace',execute:function(){return setArchived(core.service('persistence'),false)}});
  }
  var api=global.UrbeMultiCity={finishing:Promise.resolve({merged:[]}),MARKER:MARKER,ORIGENS:ORIGENS,safeName:safeName,folderFor:folderFor,parseMarker:parseMarker,doneSet:doneSet,mergeMap:mergeMap,mergePendingHook:mergePendingHook,finishPending:finishPending,mergedSources:mergedSources,archived:archived,setArchived:setArchived};
})(window);
