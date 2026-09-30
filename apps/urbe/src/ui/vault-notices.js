(function(global){
  'use strict';
  /* Avisos sobre o formato do vault (REQ-035, REQ-038, REQ-082): arquivos de versão mais nova preservados,
     vault somente leitura e migração com backup. A tela completa de Recuperação vem em REQ-085. */
  var core=global.UrbeCore;if(!core)return;
  function toast(m){var t=global.document&&global.document.getElementById&&global.document.getElementById('toast');if(!t)return;t.textContent=m;t.classList.add('show');clearTimeout(toast._t);toast._t=setTimeout(function(){t.classList.remove('show')},7000)}
  function names(items){return items.map(function(i){return i.path.replace(/^\.urbe\//,'')}).join(', ')}
  core.events.on('workspace:foreign',function(e){
    var items=(e&&e.items||[]).filter(function(i){return i.path!=='.urbe/vault.json'});if(!items.length)return;
    toast('Arquivos de uma versão mais nova do Urbe foram preservados sem alteração: '+names(items)+'.');
  });
  core.events.on('workspace:readonly',function(e){
    var msg='Este vault foi criado por uma versão mais nova do Urbe (formato '+(e&&e.formatVersion)+'). Para não corromper nada, ele está aberto somente para leitura. Atualize o Urbe para editar.';
    var D=global.UrbeDialogs;if(D&&D.alert)D.alert({title:'Vault somente leitura',message:msg});else toast(msg);
  });
  core.events.on('workspace:reconciled',function(e){
    var n=(e&&e.renames||[]).length,amb=(e&&e.ambiguous||[]).length;
    if(amb)toast('Notas idênticas apareceram fora do app ('+e.ambiguous.map(function(a){return a.to.join(', ')}).join('; ')+'): entraram como notas novas.');
    else if(n)toast(n===1?'Uma nota renomeada fora do app foi reconhecida: '+e.renames[0].to+'.':n+' notas renomeadas ou movidas fora do app foram reconhecidas.');
  });
  /* Limpeza de órfãos (REQ-042, RM-F1-16): mostra o que achou e só apaga com confirmação. */
  core.commands.register('workspace.cleanOrphans',{title:'Limpar referências órfãs',category:'Workspace',execute:async function(){
    var D=global.UrbeDialogs,r=await core.commands.execute('workspace.gc',{dryRun:true}),c=r.counts,total=c.history+c.trash+c.compositionRefs;
    if(!D||!D.confirm)return r;
    if(!total){await D.alert({title:'Nada para limpar',message:'Não há referências a notas que não existem mais.'});return r}
    var parts=[];if(c.history)parts.push(c.history+(c.history===1?' histórico de uma nota que não existe mais há mais de 30 dias':' históricos de notas que não existem mais há mais de 30 dias'));
    if(c.compositionRefs)parts.push(c.compositionRefs+(c.compositionRefs===1?' fonte de composição que sumiu':' fontes de composição que sumiram'));
    var ok=await D.confirm({title:'Limpar referências órfãs?',message:'Encontrei '+parts.join(' e ')+'. Notas, notas na lixeira e o histórico delas não são tocados.',confirm:'Limpar'});
    if(!ok)return r;var done=await core.commands.execute('workspace.gc',{dryRun:false});toast('Limpeza feita e registrada no vault.');return done;
  }});
  core.events.on('workspace:migrated',function(e){
    toast('Vault atualizado para o formato do Urbe 2.0. Uma cópia dos arquivos antigos ficou em '+(e&&e.backup)+'.');
  });
})(window);
