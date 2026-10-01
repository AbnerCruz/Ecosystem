(function(global){
  'use strict';
  /* Dicas de primeiro uso: aparecem uma vez, no momento em que fazem sentido,
     e nunca bloqueiam a tela. A escolha "Entendi" fica guardada neste aparelho. */
  var core=global.UrbeCore,docs=core&&core.service('documents'),knowledge=core&&core.service('knowledge');if(!core||!docs||!knowledge)return;
  var doc=global.document,editor=doc.getElementById('editorFull');
  function seen(k){try{return global.localStorage.getItem('urbe.tip.'+k)==='1'}catch(_){return false}}
  function mark(k){try{global.localStorage.setItem('urbe.tip.'+k,'1')}catch(_){}}
  var card=doc.createElement('section');card.id='urbeTip';card.className='ui-float-card';card.hidden=true;card.setAttribute('role','status');
  doc.getElementById('app').appendChild(card);
  function show(k,title,text){
    if(seen(k)||!card.hidden)return;
    card.innerHTML='<h2>'+title+'</h2><p>'+text+'</p><div class="uw-actions"><button type="button" class="ui-btn ui-btn-primary" data-ok>Entendi</button></div>';
    card.hidden=false;card.querySelector('[data-ok]').onclick=function(){mark(k);card.hidden=true};
  }
  function links(){return knowledge.stats().links}
  function cityVisible(){return !(editor&&editor.classList.contains('open'))&&!(core.service('explorer.ui')&&core.service('explorer.ui').isOpen())}
  /* 1) voltou para a cidade com notas, mas nenhuma ligação ainda */
  function maybeLinkTip(){if(cityVisible()&&docs.list().length>=1&&links()===0)show('link','Ligue suas notas','Dentro de uma nota, digite [[ e escolha outra nota. Uma rua aparece ligando as duas casas.')}
  if(editor)new MutationObserver(function(){if(!editor.classList.contains('open'))setTimeout(maybeLinkTip,300);else card.hidden=true}).observe(editor,{attributes:true,attributeFilter:['class']});
  core.events.on('explorer:visibility',function(e){if(e&&e.open)card.hidden=true});
  /* 2) a primeira rua foi construída */
  var before=null;
  core.events.on('knowledge:indexed',function(e){var n=e&&e.links||0;if(before===0&&n>0&&!seen('road')){mark('road');mark('link');card.hidden=true;var t=doc.getElementById('toast');if(t){t.textContent='Primeira rua construída! Volte à cidade para ver a ligação.';t.classList.add('show');clearTimeout(t._urbeTip);t._urbeTip=setTimeout(function(){t.classList.remove('show')},3200)}}before=n});
  core.events.on('workspace:loaded',function(){before=links()});
})(window);
