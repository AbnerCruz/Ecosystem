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
  core.events.on('workspace:migrated',function(e){
    toast('Vault atualizado para o formato do Urbe 2.0. Uma cópia dos arquivos antigos ficou em '+(e&&e.backup)+'.');
  });
})(window);
