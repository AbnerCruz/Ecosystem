(function(global){
  'use strict';
  /* Configurações gerais (botão de ajustes na tela da Cidade).
     Antes esse botão abria direto as configurações do Assistente. */
  var core=global.UrbeCore,D=global.UrbeDialogs;if(!core||!D)return;
  function rt(){return core.service('legacy.runtime')||{}}
  function open(){
    var r=rt(),storage=core.service('workspace.storage'),dbg=global.UrbeTouchDebug;
    D.menu('Configurações',[
      core.commands.has('ui.customize')?{icon:'brush',label:'Personalização',detail:'Tema, cores, fontes, cidade, texturas, estilos e plugins',run:function(){core.commands.execute('ui.customize')}}:null,
      r.openVaults?{icon:'city',label:'Cidade: '+(r.vaultName?r.vaultName():'Urbe'),detail:'Trocar, criar ou gerenciar cidades',run:function(){r.openVaults()}}:null,
      storage?{icon:'storage',label:'Pasta no dispositivo',detail:'Onde os arquivos ficam salvos',run:function(){storage.chooseRoot()}}:null,
      r.aiSettings?{icon:'sparkle',label:'Assistente',detail:'Provedores, modelos, instruções e memória',run:function(){r.aiSettings()}}:null,
      core.commands.has('pages.home')?{icon:'page',label:'Páginas',detail:'Sites e páginas HTML do vault',run:function(){core.commands.execute('pages.home')}}:null,
      dbg?{icon:'select',label:'Diagnóstico de toques',detail:dbg.isOn()?'Ligado — toque para desligar':'Para descobrir botões que não respondem',run:function(){var v=!dbg.isOn();dbg.set(v);
        if(v)D.alert({title:'Diagnóstico ligado',message:'Agora toque nos botões que não funcionam. Cada toque deixa um ponto: verde = o botão recebeu o toque; vermelho = não recebeu.\n\nDepois toque em “Copiar relatório” no quadro do topo e cole na conversa. Para desligar, use o × do quadro.'})}}:null,
      {icon:'restore',label:'Procurar atualização',detail:'Versão '+(r.version?r.version():''),run:function(){var sw=global.navigator.serviceWorker;
        (sw&&sw.getRegistration?sw.getRegistration().then(function(g){return g&&g.update()}):Promise.resolve()).catch(function(){}).then(function(){global.location.reload()})}}
    ].filter(Boolean));
  }
  core.commands.register('ui.settings',{title:'Configurações',category:'Aplicativo',execute:open});
  global.UrbeSettings={open:open};
})(window);
