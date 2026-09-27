(function(global){
  'use strict';
  /* Ferramentas do Assistente para páginas: ele lê o formato (page_schema) e
     cria/edita o arquivo .page.json (write_page) — validado antes de gravar,
     com prévia do diff na aprovação e desfazer como qualquer outra alteração. */
  var T=global.UrbeAITools,P=global.UrbePages,TPL=global.UrbePageTemplates;if(!T||!P||!TPL)return;
  function err(m){var e=new Error(m);e.toolError=true;return e}
  function pagePath(p,folder){p=String(p||'').trim().replace(/\\/g,'/').replace(/^\/+|\/+$/g,'');if(!p)throw err('Informe path (ex.: "Páginas/Portfólio.page.json").');
    if(!/\.(page|template)\.json$/i.test(p))p=p.replace(/\.(json|md|markdown|html?)$/i,'')+'.page.json';
    if(p.indexOf('/')<0)p=(folder||'Páginas')+'/'+p;
    if(/(^|\/)\.\.?(\/|$)/.test(p)||/[:*?"<>|\u0000-\u001f]/.test(p))throw err('Caminho inválido: '+p);return p}
  function build(i,ctx){
    var docs=ctx.core.service('documents'),spec=i.spec;
    if(spec==null&&i.template){if(!TPL.get(i.template))throw err('Modelo "'+i.template+'" não existe. Modelos: '+TPL.list().map(function(t){return t.id}).join(', '));
      var c={title:i.title||'',folder:i.folder||''};if(i.note){var d=docs.get(i.note)||docs.get(i.note+'.md');if(!d)throw err('Nota não encontrada: '+i.note);c.note={title:d.title,path:d.path};c.title=c.title||d.title}
      spec=TPL.build(i.template,c);if(i.title){spec.meta.title=i.title;if(spec.layout.brand)spec.layout.brand=i.title}}
    if(spec==null)throw err('Envie spec (o JSON completo da página) ou template.');
    if(typeof spec==='string'){try{spec=JSON.parse(spec)}catch(e){throw err('spec não é JSON válido: '+e.message)}}
    var n=P.normalize(spec);
    if(n.errors.length)throw err('A página tem '+n.errors.length+' erro(s); corrija e envie de novo:\n'+n.errors.slice(0,20).map(function(x){return '- '+x.path+': '+x.message}).join('\n')+'\n(Chame page_schema para ver o formato.)');
    return n;
  }
  T.register({name:'page_schema',access:'read',
    description:'Formato completo das páginas HTML do Urbe (.page.json): meta, tema, layout, estilos de seção, todos os blocos e modelos prontos. Chame antes de criar ou editar uma página.',
    parameters:{type:'object',properties:{},additionalProperties:false},
    label:function(){return 'Lendo o formato de páginas'},
    run:function(){return{content:P.schemaText()+'\n\nMODELOS PRONTOS (write_page com template):\n'+TPL.list().map(function(t){return '- '+t.id+(t.needs?' (precisa de '+(t.needs==='note'?'note':'folder')+')':'')+': '+t.description}).join('\n')+
      '\n\nTemas: '+Object.keys(P.THEMES).map(function(k){return k+' ('+P.THEMES[k].label+', '+P.THEMES[k].mode+')'}).join(', ')+'.\nPara editar uma página existente: read_note no .page.json, altere o JSON e envie tudo com write_page.'}}});

  T.register({name:'write_page',access:'write',
    description:'Cria ou substitui uma página HTML do vault (arquivo .page.json) a partir do JSON completo (spec) ou de um modelo (template). Valida antes de gravar e devolve os erros se houver. Depois, abra com open_note para o usuário ver no estúdio.',
    parameters:{type:'object',properties:{
      path:{type:'string',description:'Ex.: "Páginas/Meu site.page.json" (a extensão é adicionada se faltar)'},
      spec:{type:'object',description:'JSON completo da página no formato de page_schema',additionalProperties:true},
      template:{type:'string',description:'Alternativa a spec: id de um modelo ('+TPL.list().map(function(t){return t.id}).join(', ')+')'},
      title:{type:'string',description:'Título ao usar template'},folder:{type:'string',description:'Pasta de notas para os modelos vault-site/docs'},note:{type:'string',description:'Caminho da nota para o modelo article'}},
      required:['path'],additionalProperties:false},
    label:function(i){return 'Página '+String(i.path||'').split('/').pop().replace(/\.page\.json$/i,'')},
    preview:function(i,ctx){var p=pagePath(i.path),n=build(i,ctx),d=ctx.core.service('documents').get(p);return{path:p,before:d?d.content:null,after:JSON.stringify(n.spec,null,2)+'\n',note:(d?'Substitui a página existente':'Nova página')+' · '+n.spec.sections.length+' seções · tema '+n.spec.theme.preset}},
    run:function(i,ctx){var docs=ctx.core.service('documents'),p=pagePath(i.path),n=build(i,ctx),d=docs.get(p),before=d?{path:d.path,content:d.content}:null;
      var u=docs.upsert(Object.assign({},d||{},{id:d?d.id:undefined,path:p,content:JSON.stringify(n.spec,null,2)+'\n'}),{source:'ai.agent',agent:ctx.agentId||null});
      return{content:(d?'Página atualizada: ':'Página criada: ')+u.path+' ('+n.spec.sections.length+' seções: '+n.spec.sections.map(function(s){return s.type}).join(', ')+').'+(n.warnings.length?'\nAvisos: '+n.warnings.slice(0,8).map(function(w){return w.path+' '+w.message}).join('; '):'')+'\nO usuário pode abrir no estúdio (open_note) e exportar como HTML.',
        changes:[{id:u.id,before:before,after:{path:u.path,content:u.content}}]}}});
})(typeof window!=='undefined'?window:globalThis);
