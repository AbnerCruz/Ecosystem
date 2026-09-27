(function(global){
  'use strict';
  /* Pasta "Tutorial": criada sozinha na primeira vez que uma cidade abre neste aparelho
     (e só uma vez: se a pessoa apagar, não volta). "Restaurar o Tutorial" recria o que
     falta e pergunta antes de substituir notas que a pessoa mudou.
     O conteúdo vem de src/tutorial/content.js, gerado a partir de tutorial/*.md. */
  var core=global.UrbeCore,C=global.UrbeTutorialContent;if(!core||!C)return;
  var docs=core.service('documents'),D=global.UrbeDialogs,INICIO='Tutorial/Comece aqui.md',MARCA='.urbe/tutorial.json';
  function store(){try{return global.localStorage}catch(_){return null}}
  function persist(){return core.service('persistence')}
  function chave(){var p=persist();return 'urbe.tutorial.v1::'+((p&&p.vault)||'Urbe')}
  function toast(m){var t=global.document&&global.document.getElementById('toast');if(!t)return;t.textContent=m;t.classList.add('show');clearTimeout(toast._t);toast._t=setTimeout(function(){t.classList.remove('show')},3200)}
  function tem(){return docs.list().some(function(d){return d.path.indexOf('Tutorial/')===0})}
  function criar(path,conteudo){var d=docs.get(path);return docs.upsert(Object.assign({},d||{},{id:d?d.id:undefined,path:path,content:conteudo}),{source:'tutorial'})}

  /* bairros do tamanho certo antes das casas: cada pasta comporta as notas de dentro e as subpastas */
  function prepararBairros(lista){
    var rt=core.service('legacy.runtime');if(!rt||!rt.ensureFolders||!lista.length)return;var qtd={};
    lista.forEach(function(p){var partes=p.split('/');partes.pop();for(var i=1;i<=partes.length;i++){var k=partes.slice(0,i).join('/');qtd[k]=(qtd[k]||0)+1}});
    Object.keys(qtd).forEach(function(k){var filhos=Object.keys(qtd).filter(function(x){return x.indexOf(k+'/')===0&&x.split('/').length===k.split('/').length+1}).length;qtd[k]+=filhos*3});
    try{rt.ensureFolders(qtd)}catch(e){console.warn('tutorial: bairros',e)}
  }
  async function marcaNoVault(){var p=persist();try{if(p&&p.adapter&&p.vault)return !!(await p.adapter.read(p.vault,MARCA))}catch(_){}return false}
  async function marcar(){var s=store();try{s&&s.setItem(chave(),C.version)}catch(_){}
    var p=persist();try{if(p&&p.adapter&&p.vault)await p.adapter.write(p.vault,MARCA,JSON.stringify({versao:C.version,em:new Date().toISOString()}))}catch(_){}}

  function abrir(){
    if(!docs.get(INICIO)){
      if(!D)return false;
      return D.confirm({title:'Tutorial',message:'A pasta Tutorial não está nesta cidade. Quer criá-la de novo?',confirm:'Criar Tutorial'}).then(function(ok){if(ok)return restaurar(true)});
    }
    core.commands.execute('document.open',{path:INICIO,source:'tutorial'});return true;
  }

  /* cria só o que falta; com mudadas=true também oferece trocar as notas editadas */
  async function restaurar(abrirDepois){
    var faltam=[],mudadas=[];
    Object.keys(C.files).forEach(function(p){var d=docs.get(p);if(!d)faltam.push(p);else if(d.content!==C.files[p])mudadas.push(p)});
    prepararBairros(faltam);
    faltam.forEach(function(p){criar(p,C.files[p])});
    var trocar=false;
    if(mudadas.length&&D){
      trocar=await D.confirm({title:'Restaurar o Tutorial',message:(faltam.length?faltam.length+' nota(s) recriada(s). ':'')+mudadas.length+' nota(s) do Tutorial estão diferentes das originais (você mudou, ou são de uma versão anterior). Substituir pelas originais?',confirm:'Substituir',cancel:'Manter as minhas'});
      if(trocar)mudadas.forEach(function(p){criar(p,C.files[p])});
    }
    await marcar();
    toast(faltam.length||trocar?'Tutorial restaurado':'O Tutorial já está completo');
    if(abrirDepois!==false)abrir();
    return{criadas:faltam.length,substituidas:trocar?mudadas.length:0};
  }

  /* primeira vez nesta cidade: cria a pasta e, se a cidade estava vazia, abre o começo */
  async function semear(){
    var s=store(),ja=false;try{ja=!!(s&&s.getItem(chave()))}catch(_){}
    if(ja||tem())return false;
    if(await marcaNoVault())return false;
    var vazia=!docs.list().some(function(d){return d.path.indexOf('Personalização/')!==0});
    prepararBairros(Object.keys(C.files));
    Object.keys(C.files).forEach(function(p){criar(p,C.files[p])});
    await marcar();
    if(vazia){setTimeout(function(){try{if(core.commands.has('city.fit'))core.commands.execute('city.fit')}catch(_){}abrir()},900)}
    else toast('Nova pasta Tutorial: tudo o que o Urbe faz, explicado passo a passo.');
    return true;
  }
  core.events.on('workspace:loaded',function(){setTimeout(function(){semear().catch(function(e){console.warn('tutorial',e)})},300)});

  function menu(){
    if(!D)return abrir();
    return D.menu('Tutorial',[
      {icon:'book',label:'Abrir o Tutorial',detail:'Começa pela nota “Comece aqui”',run:abrir},
      {icon:'restore',label:'Restaurar o Tutorial',detail:'Recria as notas que faltam ou foram mudadas',run:function(){return restaurar(true)}}
    ]);
  }
  core.commands.register('tutorial.open',{title:'Tutorial',category:'Ajuda',execute:abrir});
  core.commands.register('tutorial.restore',{title:'Restaurar o Tutorial',category:'Ajuda',execute:function(){return restaurar(true)}});
  core.commands.register('tutorial.menu',{title:'Tutorial: abrir ou restaurar',category:'Ajuda',execute:menu});
  core.provide('tutorial',{open:abrir,restore:restaurar,seed:semear,menu:menu,version:C.version,files:function(){return Object.keys(C.files)}});
})(window);
