(function(global){
  'use strict';
  /* Ferramentas do Assistente para a personalização: ler o formato do tema.json,
     gravar a aparência validada (com prévia e desfazer) e conhecer a api de plugins.
     O Assistente pode escrever plugins, mas eles só rodam depois que o usuário os liga. */
  var T=global.UrbeAITools;if(!T)return;
  function err(m){var e=new Error(m);e.toolError=true;return e}
  function C(ctx){var c=ctx.core.service('customize');if(!c)throw err('Personalização indisponível.');return c}
  function montar(i,ctx){
    var c=C(ctx),base=i.replace?{}:c.get(),alvo=i.config;
    if(typeof alvo==='string'){try{alvo=JSON.parse(alvo)}catch(e){throw err('config não é JSON válido: '+e.message)}}
    if(!alvo||typeof alvo!=='object')throw err('Envie config: objeto com as partes do tema.json a mudar.');
    function merge(a,b){var o=JSON.parse(JSON.stringify(a||{}));Object.keys(b).forEach(function(k){if(b[k]&&typeof b[k]==='object'&&!Array.isArray(b[k])&&o[k]&&typeof o[k]==='object'&&!Array.isArray(o[k]))o[k]=merge(o[k],b[k]);else o[k]=b[k]});return o}
    var n=c.normalize(merge(base,alvo));
    if(n.errors.length)throw err('O tema tem '+n.errors.length+' erro(s); corrija e envie de novo:\n'+n.errors.map(function(x){return '- '+(x.path||'(arquivo)')+': '+x.message}).join('\n')+'\n(Chame appearance_schema para ver o formato.)');
    return n;
  }
  T.register({name:'appearance_schema',access:'read',
    description:'Formato completo da personalização do Urbe: tema.json (tema, cores, fontes, forma, editor, cidade, texturas, estilos), temas salvos e pacotes de texturas. Mostra também a configuração atual. Chame antes de mudar a aparência.',
    parameters:{type:'object',properties:{},additionalProperties:false},
    label:function(){return 'Lendo o formato da personalização'},
    run:function(i,ctx){var c=C(ctx);return{content:c.schemaText()+'\n\nTEMAS PRONTOS: '+Object.keys(c.PRESETS).map(function(k){return k+' ('+c.PRESETS[k].nome+(c.PRESETS[k].claro?', claro':', escuro')+')'}).join(', ')+
      '\nFONTES: '+Object.keys(c.FONTS).join(', ')+'\nTEMAS SALVOS: '+(c.themes().map(function(t){return t.id}).join(', ')||'nenhum')+'\nPACOTES DE TEXTURAS: '+(c.packs().map(function(p){return p.path}).join(', ')||'nenhum')+
      '\n\nCONFIGURAÇÃO ATUAL:\n'+JSON.stringify(c.get(),null,2)}}});
  T.register({name:'set_appearance',access:'write',
    description:'Muda a aparência do Urbe gravando o tema.json (validado). Envie só o que muda em config, ex.: {"tema":"sepia","texto":{"fonte":"serifada"}}. Use replace=true para substituir tudo em vez de mesclar. Aplica na hora; o usuário pode desfazer.',
    parameters:{type:'object',properties:{config:{type:'object',description:'Partes do tema.json no formato de appearance_schema',additionalProperties:true},replace:{type:'boolean',description:'true = substitui a configuração inteira'}},required:['config'],additionalProperties:false},
    label:function(){return 'Mudando a aparência'},
    preview:function(i,ctx){var c=C(ctx),n=montar(i,ctx),d=ctx.core.service('documents').get(c.FILE);return{path:c.FILE,before:d?d.content:null,after:JSON.stringify(n.config,null,2)+'\n',note:'Aparência · tema '+n.config.tema}},
    run:function(i,ctx){var c=C(ctx),docs=ctx.core.service('documents'),n=montar(i,ctx),d=docs.get(c.FILE),before=d?{path:d.path,content:d.content}:null;
      c.replace(n.config);var u=docs.get(c.FILE);
      return{content:'Aparência aplicada (tema '+n.config.tema+').'+(n.warnings.length?'\nAvisos: '+n.warnings.map(function(w){return w.path+' '+w.message}).join('; '):''),
        changes:u?[{id:u.id,before:before,after:{path:u.path,content:u.content}}]:[]}}});
  T.register({name:'plugin_guide',access:'read',
    description:'Como escrever plugins do Urbe (arquivos .js em Personalização/plugins/): formato, api completa e regras de segurança. Chame antes de criar ou editar um plugin; depois grave o arquivo com write_note e diga ao usuário para ligá-lo em Personalização → Plugins.',
    parameters:{type:'object',properties:{},additionalProperties:false},
    label:function(){return 'Lendo o guia de plugins'},
    run:function(i,ctx){var p=ctx.core.service('plugins');if(!p)throw err('Plugins indisponíveis.');
      return{content:p.guide()+'\n\nPLUGINS NESTA CIDADE:\n'+(p.list().map(function(x){return '- '+x.path+' ('+x.status+(x.erro?': '+x.erro:'')+')'}).join('\n')||'nenhum')}}});
})(typeof window!=='undefined'?window:globalThis);
