(function(global){
  'use strict';
  /* Coleta de órfãos (REQ-042, RM-F1-16): referências a documentos que não existem mais em history, trash e compositions.
     Regras de segurança (nenhum dado ativo se perde):
       - documento vivo ou na lixeira nunca é órfão (a lixeira pode restaurar, e o histórico volta junto);
       - histórico órfão só sai depois de `orphanDays` sem mudança (padrão 30);
       - itens da lixeira só expiram se `trashDays` for definido (padrão: nunca);
       - composições não são apagadas: só perdem as fontes que não existem mais.
     `workspace.gc` é simulação por padrão (`dryRun:true`); com `dryRun:false` aplica e registra em `vault.json` (`maintenance`). */
  var core=global.UrbeCore;if(!core)return;
  var DAY=86400000,LOG_MAX=20;

  function plan(state,opts){
    opts=opts||{};var now=opts.now||Date.now(),orphanDays=opts.orphanDays==null?30:Number(opts.orphanDays),trashDays=opts.trashDays==null?null:Number(opts.trashDays);
    var live=new Set(state.docs.map(function(d){return d.id})),trashed=new Set(state.trash.map(function(t){return t.document&&t.document.id}));
    var known=function(id){return live.has(id)||trashed.has(id)};
    var out={history:[],trash:[],compositions:[]};
    Object.keys(state.history||{}).forEach(function(id){
      if(known(id))return;var revs=state.history[id]||[],last=revs.reduce(function(m,r){return Math.max(m,r&&r.timestamp||0)},0);
      if(now-last>=orphanDays*DAY)out.history.push({id:id,revisions:revs.length,last:last||null});
    });
    if(trashDays!=null&&trashDays>=0)state.trash.forEach(function(t){if(t&&t.document&&now-(t.deletedAt||0)>=trashDays*DAY)out.trash.push({id:t.document.id,path:t.originalPath||t.document.path,deletedAt:t.deletedAt||null})});
    (state.compositions||[]).forEach(function(c){var gone=(c.sources||[]).filter(function(s){return !known(String(s))});if(gone.length)out.compositions.push({id:c.id,name:c.name,sources:gone})});
    return out;
  }
  function counts(p){return{history:p.history.length,trash:p.trash.length,compositionRefs:p.compositions.reduce(function(n,c){return n+c.sources.length},0)}}

  /* API sem interface (fora da paleta); quem conversa com a pessoa é `workspace.cleanOrphans` em src/ui/vault-notices.js */
  core.commands.register('workspace.gc',{title:'Coletar referências órfãs',category:'Workspace',enabled:function(ctx){return !(ctx&&ctx.source==='palette')},execute:async function(ctx){
    ctx=ctx||{};var P=core.service('persistence'),docs=core.service('documents'),history=core.service('history'),trash=core.service('trash'),comps=core.service('compositions');
    if(!P||!docs)throw new Error('workspace.gc: persistência indisponível');
    var state={docs:docs.list(),trash:trash?trash.list():[],history:history?history.export().documents:{},compositions:comps?comps.list():[]};
    var p=plan(state,ctx),report={dryRun:ctx.dryRun!==false,counts:counts(p),plan:p};
    if(report.dryRun)return report;
    if(P.readOnly)throw new Error('Vault somente leitura: nada foi alterado.');
    p.history.forEach(function(h){history.forget(h.id)});
    p.trash.forEach(function(t){trash.purge(t.id)});
    p.compositions.forEach(function(c){var cur=comps.get(c.id);if(!cur)return;var drop=new Set(c.sources);comps.update(c.id,{sources:(cur.sources||[]).filter(function(s){return !drop.has(String(s))})})});
    await P.ensureVaultFormat();
    var data=P.vaultInfo&&P.vaultInfo.data;
    if(data){var log=Array.isArray(data.maintenance)?data.maintenance:[];log.push({kind:'gc',at:new Date(ctx.now||Date.now()).toISOString(),removed:report.counts,orphanDays:ctx.orphanDays==null?30:ctx.orphanDays,trashDays:ctx.trashDays==null?null:ctx.trashDays});data.maintenance=log.slice(-LOG_MAX)}
    core.events.emit('workspace:gc',{vault:P.vault,counts:report.counts});
    await P.flush();return report;
  }});
  global.UrbeGC={plan:plan};
})(window);
