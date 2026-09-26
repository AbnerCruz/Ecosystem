(function(global){
  'use strict';
  /* Diálogos da interface. Substituem prompt()/confirm()/alert() nativos, que no
     celular aparecem como caixas do sistema, fora do visual do app e sem contexto.
     Tudo é assíncrono e devolve Promise; um diálogo por vez, os demais aguardam. */
  var doc=global.document,queue=Promise.resolve(),seq=0;
  function esc(v){return String(v==null?'':v).replace(/[&<>"']/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]})}
  function icon(name){return global.UrbeIcons?global.UrbeIcons.icon(name):''}
  function norm(a,b,defaults){var o=typeof a==='object'&&a?Object.assign({},a):{title:a,value:b};return Object.assign({},defaults,o)}

  function open(build){
    var run=function(){return new Promise(function(resolve){
      var id='udlg'+(++seq),prev=doc.activeElement,root=doc.createElement('div');
      root.className='udlg';root.innerHTML='<div class="udlg-backdrop" data-cancel></div><div class="udlg-card" role="dialog" aria-modal="true" aria-labelledby="'+id+'-t"></div>';
      var card=root.querySelector('.udlg-card'),done=false;
      function finish(value){if(done)return;done=true;root.classList.remove('open');doc.removeEventListener('keydown',onKey,true);setTimeout(function(){root.remove()},160);try{prev&&prev.focus&&prev.focus({preventScroll:true})}catch(_){}resolve(value)}
      var api=build(card,id,finish);
      function onKey(e){if(e.key==='Escape'){e.preventDefault();e.stopPropagation();finish(api.cancelValue)}else if(e.key==='Enter'&&api.onEnter&&!(e.target&&e.target.tagName==='TEXTAREA')){e.preventDefault();e.stopPropagation();api.onEnter()}}
      root.addEventListener('click',function(e){if(e.target.closest('[data-cancel]'))finish(api.cancelValue)});
      doc.addEventListener('keydown',onKey,true);doc.body.appendChild(root);
      card.tabIndex=-1;
      /* foco imediato: o que for digitado logo após abrir já cai no diálogo */
      var target=api.focus||card.querySelector('[data-primary]')||card;try{target.focus({preventScroll:true})}catch(_){}
      if(api.afterFocus)api.afterFocus();
      requestAnimationFrame(function(){root.classList.add('open')});
    })};
    var p=queue.then(run,run);queue=p.catch(function(){});return p;
  }

  function head(id,title,message){return '<header class="udlg-head"><h2 id="'+id+'-t">'+esc(title)+'</h2><button type="button" class="udlg-x" data-cancel aria-label="Fechar">'+icon('close')+'</button></header>'+(message?'<p class="udlg-msg">'+esc(message)+'</p>':'')}
  function actions(cancel,confirm,danger){return '<footer class="udlg-actions"><button type="button" class="ui-btn" data-cancel>'+esc(cancel)+'</button><button type="button" class="ui-btn '+(danger?'ui-btn-danger':'ui-btn-primary')+'" data-primary>'+esc(confirm)+'</button></footer>'}

  function prompt(a,b){
    var o=norm(a,b,{title:'Nome',value:'',confirm:'Confirmar',cancel:'Cancelar',placeholder:''});
    return open(function(card,id,finish){
      card.innerHTML=head(id,o.title,o.message)+'<label class="udlg-field">'+(o.label?'<span>'+esc(o.label)+'</span>':'')+
        (o.multiline?'<textarea rows="4"></textarea>':'<input type="text" autocomplete="off" autocapitalize="'+(o.autocapitalize||'sentences')+'" spellcheck="false">')+
        '</label><p class="udlg-error" hidden></p>'+(o.hint?'<p class="udlg-hint">'+esc(o.hint)+'</p>':'')+actions(o.cancel,o.confirm,o.danger);
      var input=card.querySelector('input,textarea'),err=card.querySelector('.udlg-error');input.value=o.value==null?'':String(o.value);input.placeholder=o.placeholder||'';
      function submit(){var v=input.value;if(o.required!==false&&!v.trim()){showError(o.requiredMessage||'Preencha este campo.');return}var problem=o.validate&&o.validate(v);if(problem){showError(problem);return}finish(v)}
      function showError(t){err.textContent=t;err.hidden=false;input.setAttribute('aria-invalid','true');input.focus()}
      input.addEventListener('input',function(){err.hidden=true;input.removeAttribute('aria-invalid')});
      card.querySelector('[data-primary]').onclick=submit;
      function selectName(){var v=input.value,dot=v.lastIndexOf('.');if(o.selectBaseName!==false&&dot>0&&input.setSelectionRange)input.setSelectionRange(0,dot);else if(input.select)input.select()}
      return{cancelValue:null,onEnter:submit,focus:input,afterFocus:selectName};
    });
  }

  function confirm(a,b){
    var o=norm(a,null,{title:'Confirmar',confirm:'Confirmar',cancel:'Cancelar'});if(typeof a==='string'){o.message=b||'';}
    return open(function(card,id,finish){
      card.innerHTML=head(id,o.title,o.message)+actions(o.cancel,o.confirm,o.danger);
      card.querySelector('[data-primary]').onclick=function(){finish(true)};
      return{cancelValue:false,onEnter:function(){finish(true)}};
    });
  }

  function alert(a,b){
    var o=norm(a,null,{title:'Aviso',confirm:'Ok'});if(typeof a==='string'){o.message=b||'';if(!b){o.message=a;o.title='Aviso'}}
    return open(function(card,id,finish){
      card.innerHTML=head(id,o.title,o.message)+'<footer class="udlg-actions udlg-single"><button type="button" class="ui-btn ui-btn-primary" data-primary>'+esc(o.confirm)+'</button></footer>';
      card.querySelector('[data-primary]').onclick=function(){finish(undefined)};
      return{cancelValue:undefined,onEnter:function(){finish(undefined)}};
    });
  }

  /* Lista de opções (ações ou destinos). Devolve o value escolhido ou undefined. */
  function choose(a){
    var o=norm(a,null,{title:'Escolha',options:[]});
    return open(function(card,id,finish){
      card.innerHTML=head(id,o.title,o.message)+'<div class="udlg-list" role="listbox">'+o.options.map(function(x,i){
        return '<button type="button" class="udlg-option'+(x.danger?' danger':'')+(x.current?' current':'')+'" role="option" data-i="'+i+'"'+(i===0?' data-primary':'')+'>'+
          (x.icon?'<span class="udlg-option-icon">'+icon(x.icon)+'</span>':'')+
          '<span class="udlg-option-text"><strong>'+esc(x.label)+'</strong>'+(x.detail?'<small>'+esc(x.detail)+'</small>':'')+'</span></button>'}).join('')+'</div>';
      card.querySelectorAll('[data-i]').forEach(function(el){el.onclick=function(){var x=o.options[+el.dataset.i];finish(x.value!==undefined?x.value:+el.dataset.i)}});
      return{cancelValue:undefined};
    });
  }

  /* Menu de ações: cada item traz run(); fecha antes de executar. */
  function menu(title,items,message){
    var list=(items||[]).filter(Boolean);
    return choose({title:title,message:message,options:list.map(function(x,i){return{value:i,label:x.label,icon:x.icon,detail:x.detail,danger:x.danger}})}).then(function(i){if(i!==undefined&&list[i]&&list[i].run)return list[i].run()});
  }

  global.UrbeDialogs={prompt:prompt,confirm:confirm,alert:alert,choose:choose,menu:menu};
})(window);
