(function(global){
  'use strict';
  /* Plugins do Urbe: arquivos .js em "Personalização/plugins/".
     Um plugin é código de verdade, com o mesmo poder do app. Por isso:
       - nada roda sem o usuário ligar, neste aparelho, pela tela de Plugins;
       - a aprovação guarda a impressão digital (SHA-256) do código: se o arquivo mudar
         (por outra pessoa, por sincronização ou pelo Assistente), o plugin para e pede
         nova aprovação;
       - o modo seguro (?seguro=1 ou Configurações) desliga todos de uma vez.
     Formato:
       urbe.plugin({
         nome: 'Meu plugin', versao: '1.0', descricao: '...',
         ligar(api) { ... },      // obrigatório
         desligar(api) { ... }    // opcional; o que foi criado pela api é desfeito sozinho
       });
     A api entrega comandos, avisos, diálogos, notas, eventos, estilos, camadas no mapa,
     ferramentas para o Assistente, botões e dados guardados — tudo limpo ao desligar. */
  var core=global.UrbeCore,doc=global.document;if(!core||!doc)return;
  var docs=core.service('documents'),PASTA='Personalização/plugins/',CHAVE='urbe.plugins.v1';
  var ativos=new Map(),estado=new Map(),falhou=new Map();   /* path → impressão do código que deu erro: não tenta de novo até mudar */

  function store(){try{return global.localStorage}catch(_){return null}}
  function vault(){var p=core.service('persistence');return(p&&p.vault)||'Urbe'}
  function aprovacoes(){try{return JSON.parse((store()&&store().getItem(CHAVE))||'{}')||{}}catch(_){return{}}}
  function salvarAprovacoes(a){try{store()&&store().setItem(CHAVE,JSON.stringify(a))}catch(_){}}
  function chave(path){return vault()+'::'+path}
  async function impressao(txt){
    try{if(global.crypto&&global.crypto.subtle&&global.TextEncoder){var b=await global.crypto.subtle.digest('SHA-256',new TextEncoder().encode(txt));return'sha256:'+Array.from(new Uint8Array(b)).map(function(x){return('0'+x.toString(16)).slice(-2)}).join('')}}catch(_){}
    /* sem crypto.subtle (página fora de https): FNV-1a de 2 sementes + tamanho */
    var h1=2166136261,h2=5381;for(var i=0;i<txt.length;i++){var c=txt.charCodeAt(i);h1=Math.imul(h1^c,16777619);h2=Math.imul(h2,33)^c}return'fnv:'+(h1>>>0).toString(16)+(h2>>>0).toString(16)+':'+txt.length;
  }
  function arquivos(){if(!docs)return[];return docs.list().filter(function(d){return d.path.indexOf(PASTA)===0&&/\.js$/i.test(d.path)}).sort(function(a,b){return a.path.localeCompare(b.path)})}
  function seguro(){var c=core.service('customize');return !!(c&&c.safeMode())}
  function slug(t){return String(t||'').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g,'').replace(/[^a-z0-9]+/g,'-').replace(/^-|-$/g,'')||'plugin'}
  function aviso(msg){var t=doc.getElementById('toast');if(!t){console.log(msg);return}t.textContent=String(msg);t.classList.add('show');clearTimeout(aviso._t);aviso._t=setTimeout(function(){t.classList.remove('show')},2400)}
  function setEstado(path,patch){var s=Object.assign({path:path},estado.get(path)||{},patch);estado.set(path,s);core.events.emit('plugins:changed',{path:path,state:s})}

  var EVENTOS={'nota:criada':'document:created','nota:alterada':'document:updated','nota:removida':'document:removed','cidade:aberta':'workspace:loaded','salvo':'workspace:saved','comando':'command:after','aparencia':'customize:applied'};
  function notaSimples(d){return d?{id:d.id,caminho:d.path,titulo:d.title,conteudo:d.content,tags:d.tags.slice(),links:d.links.slice()}:null}

  /* ---------- a api entregue a cada plugin ---------- */
  function criarApi(path,meta){
    var limpar=[],id=slug(meta.id||meta.nome||path.split('/').pop().replace(/\.js$/i,'')),pref='urbe.plugin.'+vault()+'.'+id+'.';
    function guarda(fn){limpar.push(fn);return fn}
    var api={
      id:id,nome:meta.nome||id,
      aviso:aviso,
      dialogo:global.UrbeDialogs,
      comando:function(c){if(!c||typeof c.executar!=='function')throw new Error('comando precisa de executar()');var cid='plugin.'+id+'.'+slug(c.id||c.titulo);
        guarda(core.commands.register(cid,{title:c.titulo||cid,category:'Plugin · '+api.nome,execute:function(ctx){return c.executar(ctx)}}));return cid},
      botao:function(b){if(!b||typeof b.executar!=='function')throw new Error('botão precisa de executar()');var top=doc.getElementById('v23SettingsBtn');if(!top||!top.parentNode)return null;
        var el=doc.createElement('button');el.className='v23TopBtn urbe-plugin-btn';el.type='button';el.title=el.ariaLabel=b.titulo||api.nome;
        el.innerHTML=b.icone&&global.UrbeIcons&&/^[a-zA-Z]+$/.test(b.icone)?global.UrbeIcons.icon(b.icone):'<span aria-hidden="true">'+String(b.texto||b.icone||'★').slice(0,2).replace(/[<>&]/g,'')+'</span>';
        el.onclick=function(){try{b.executar()}catch(e){erroPlugin(path,e)}};top.parentNode.insertBefore(el,top);guarda(function(){el.remove()});return el},
      notas:{
        listar:function(pasta){return docs.list().filter(function(d){return!pasta||d.path.indexOf(String(pasta).replace(/\/?$/,'/'))===0}).map(function(d){return{id:d.id,caminho:d.path,titulo:d.title}})},
        ler:function(c){return notaSimples(docs.get(c))},
        existe:function(c){return !!docs.get(c)},
        escrever:function(c,conteudo){if(!c)throw new Error('caminho vazio');c=String(c);if(/(^|\/)\.\.?(\/|$)/.test(c))throw new Error('caminho inválido');
          if(c.indexOf(PASTA)===0)throw new Error('plugins não podem escrever na pasta de plugins');var d=docs.get(c);return notaSimples(docs.upsert(Object.assign({},d||{},{id:d?d.id:undefined,path:c,content:String(conteudo==null?'':conteudo)}),{source:'plugin:'+id}))},
        abrir:function(c){var d=docs.get(c);if(!d)return false;core.commands.execute('document.open',{id:d.id,source:'plugin'});return true},
        atual:function(){var s=core.service('editor.session'),did=s&&s.activeId,d=did&&docs.get(did);return notaSimples(d||null)},
        buscar:function(t){t=String(t||'').toLowerCase();return docs.list().filter(function(d){return d.title.toLowerCase().indexOf(t)>=0||d.content.toLowerCase().indexOf(t)>=0}).slice(0,50).map(function(d){return{id:d.id,caminho:d.path,titulo:d.title}})}
      },
      eventos:{on:function(nome,fn){var real=EVENTOS[nome]||nome;return guarda(core.events.on(real,function(p){try{fn(p&&p.document?notaSimples(p.document):p)}catch(e){erroPlugin(path,e)}}))}},
      estilo:function(css){var el=doc.createElement('style');el.setAttribute('data-urbe-plugin',id);el.textContent=String(css||'');doc.head.appendChild(el);guarda(function(){el.remove()});return function(){el.remove()}},
      mapa:{
        camada:function(fn){var w=core.service('world.custom');if(!w)return function(){};return guarda(w.layer('plugin:'+id,fn,function(e){erroPlugin(path,e)}))},
        vista:function(){var w=core.service('world.custom');return w?w.view():null},
        ir:function(x,y,z){var w=core.service('world.custom');if(w)w.goTo(x,y,z)},
        opcoes:function(o){var w=core.service('world.custom');return w?w.options(o):{}},
        redesenhar:function(){var w=core.service('world.custom');if(w)w.redraw()}
      },
      ia:{ferramenta:function(t){var T=global.UrbeAITools;if(!T||!t||!t.nome||typeof t.executar!=='function')throw new Error('ferramenta precisa de nome e executar()');
        var nome='plugin_'+slug(id).replace(/-/g,'_')+'_'+slug(t.nome).replace(/-/g,'_');
        guarda(T.register({name:nome,access:t.escreve?'write':'read',description:(t.descricao||t.nome)+' (plugin '+api.nome+')',parameters:t.parametros||{type:'object',properties:{},additionalProperties:true},
          label:function(){return t.rotulo||t.nome},run:async function(i){var r=await t.executar(i||{});return{content:typeof r==='string'?r:JSON.stringify(r,null,2)}}}));return nome}},
      dados:{
        ler:function(k,padrao){try{var v=store()&&store().getItem(pref+k);return v==null?padrao:JSON.parse(v)}catch(_){return padrao}},
        gravar:function(k,v){try{store()&&store().setItem(pref+k,JSON.stringify(v))}catch(_){}}
      },
      intervalo:function(fn,ms){var t=setInterval(function(){try{fn()}catch(e){erroPlugin(path,e)}},Math.max(250,ms|0));guarda(function(){clearInterval(t)});return t},
      depois:function(fn,ms){var t=setTimeout(function(){try{fn()}catch(e){erroPlugin(path,e)}},Math.max(0,ms|0));guarda(function(){clearTimeout(t)});return t},
      aoDesligar:function(fn){guarda(fn)},
      executar:function(cmd,ctx){return core.commands.execute(cmd,ctx)},
      comandos:function(){return core.commands.list().map(function(c){return{id:c.id,titulo:c.title,categoria:c.category}})},
      personalizacao:{ler:function(){var c=core.service('customize');return c?c.get():null},mudar:function(p){var c=core.service('customize');return c?c.set(p):null}}
    };
    return{api:api,limpar:function(){while(limpar.length){var f=limpar.pop();try{f()}catch(_){}}}};
  }

  function erroPlugin(path,e){console.warn('plugin '+path,e);setEstado(path,{status:'erro',erro:String(e&&e.message||e)});core.events.emit('plugins:error',{path:path,error:e})}

  /* ---------- rodar e parar ---------- */
  function executarCodigo(path,codigo){
    var def=null,urbe={plugin:function(d){def=d},versao:(core.service('legacy.runtime')||{}).version?core.service('legacy.runtime').version():''};
    var fn=new Function('urbe','"use strict";\n'+codigo+'\n//# sourceURL=urbe-plugin:'+encodeURI(path));
    var ret=fn(urbe);if(!def&&ret&&typeof ret==='object')def=ret;
    if(!def||typeof(def.ligar||def.onload)!=='function')throw new Error('O arquivo não chamou urbe.plugin({ ligar(api){...} }).');
    return def;
  }
  function parar(path){var a=ativos.get(path);if(!a)return;ativos.delete(path);try{var off=a.def.desligar||a.def.onunload;if(off)off.call(a.def,a.api)}catch(e){console.warn(e)}a.limpar()}
  function iniciar(path,d){
    parar(path);
    try{
      var def=executarCodigo(path,d.content),meta={id:def.id,nome:def.nome||def.name||d.title.replace(/\.js$/i,'')},ctx=criarApi(path,meta);
      ativos.set(path,{def:def,api:ctx.api,limpar:ctx.limpar});
      var r=(def.ligar||def.onload).call(def,ctx.api);
      setEstado(path,{status:'ativo',erro:'',nome:meta.nome,versao:def.versao||def.version||'',descricao:def.descricao||def.description||''});
      if(r&&typeof r.then==='function')r.catch(function(e){parar(path);erroPlugin(path,e)});
    }catch(e){parar(path);erroPlugin(path,e)}
  }

  /* estado de cada arquivo: desligado, ativo, erro, mudou (código diferente do aprovado) */
  var sincronizando=Promise.resolve();
  function sincronizar(){sincronizando=sincronizando.then(sync,sync);return sincronizando}
  async function sync(){
    var lista=arquivos(),ok=aprovacoes(),vistos=new Set(),sg=seguro();
    for(var d of lista){
      vistos.add(d.path);var ap=ok[chave(d.path)],info=ler(d);
      if(!ap){parar(d.path);setEstado(d.path,Object.assign(info,{status:'desligado',erro:''}));continue}
      var h=await impressao(d.content);
      if(h!==ap.hash){parar(d.path);setEstado(d.path,Object.assign(info,{status:'mudou',erro:'O código mudou desde que você ligou este plugin. Revise e ligue de novo.'}));continue}
      if(sg){parar(d.path);setEstado(d.path,Object.assign(info,{status:'seguro',erro:''}));continue}
      var a=ativos.get(d.path);if(a&&a.hash===h)continue;
      if(falhou.get(d.path)===h)continue;
      iniciar(d.path,d);if(ativos.get(d.path))ativos.get(d.path).hash=h;else falhou.set(d.path,h);
    }
    Array.from(estado.keys()).forEach(function(p){if(!vistos.has(p)){parar(p);estado.delete(p);core.events.emit('plugins:changed',{path:p,removed:true})}});
  }
  /* nome e descrição lidos do código sem executá-lo */
  function ler(d){var m=/nome\s*:\s*(['"`])([^'"`\n]{1,80})\1/.exec(d.content),v=/versao\s*:\s*(['"`])([^'"`\n]{1,20})\1/.exec(d.content),ds=/descricao\s*:\s*(['"`])([^'"`\n]{1,200})\1/.exec(d.content);
    return{path:d.path,nome:m?m[2]:d.title.replace(/\.js$/i,''),versao:v?v[2]:'',descricao:ds?ds[2]:'',tamanho:d.content.length}}

  async function ligar(path){falhou.delete(path);var d=docs.get(path);if(!d)throw new Error('Plugin não encontrado: '+path);var ok=aprovacoes();ok[chave(path)]={hash:await impressao(d.content),em:Date.now()};salvarAprovacoes(ok);await sincronizar();return estado.get(path)}
  async function desligar(path){falhou.delete(path);var ok=aprovacoes();delete ok[chave(path)];salvarAprovacoes(ok);parar(path);await sincronizar();return estado.get(path)}
  function lista(){return arquivos().map(function(d){return Object.assign(ler(d),estado.get(d.path)||{status:'desligado'})})}

  /* ---------- modelos para começar ---------- */
  var MODELOS={
    basico:{nome:'Básico',descricao:'Um comando na paleta que mostra um aviso',codigo:function(n){return[
      "// "+n+" — plugin do Urbe. Documentação: Tutorial/Personalização/Plugins.",
      "urbe.plugin({",
      "  nome: '"+n+"',",
      "  versao: '1.0',",
      "  descricao: 'Mostra quantas notas existem',",
      "",
      "  ligar(api) {",
      "    api.comando({",
      "      titulo: 'Contar notas',",
      "      executar() {",
      "        const total = api.notas.listar().length;",
      "        api.aviso('Esta cidade tem ' + total + ' notas.');",
      "      }",
      "    });",
      "  }",
      "});",""].join('\n')}},
    diario:{nome:'Diário',descricao:'Botão na cidade que abre (ou cria) a nota do dia',codigo:function(n){return[
      "// "+n+" — cria uma nota por dia em Diário/ e abre com um toque.",
      "urbe.plugin({",
      "  nome: '"+n+"',",
      "  versao: '1.0',",
      "  descricao: 'Nota do dia com um toque',",
      "",
      "  ligar(api) {",
      "    function abrirHoje() {",
      "      const hoje = new Date().toISOString().slice(0, 10);",
      "      const caminho = 'Diário/' + hoje + '.md';",
      "      if (!api.notas.existe(caminho)) {",
      "        api.notas.escrever(caminho, '# ' + hoje.split('-').reverse().join('/') + '\\n\\n');",
      "      }",
      "      api.notas.abrir(caminho);",
      "    }",
      "    api.comando({ titulo: 'Abrir nota de hoje', executar: abrirHoje });",
      "    api.botao({ icone: 'clock', titulo: 'Nota de hoje', executar: abrirHoje });",
      "  }",
      "});",""].join('\n')}},
    mapa:{nome:'Desenho no mapa',descricao:'Uma camada que marca as notas com a tag #importante',codigo:function(n){return[
      "// "+n+" — desenha uma estrela sobre as casas das notas com #importante.",
      "urbe.plugin({",
      "  nome: '"+n+"',",
      "  versao: '1.0',",
      "  descricao: 'Estrelas nas notas importantes',",
      "",
      "  ligar(api) {",
      "    const importante = new Set();",
      "    function recontar() {",
      "      importante.clear();",
      "      for (const n of api.notas.listar()) {",
      "        const nota = api.notas.ler(n.caminho);",
      "        if (nota && nota.tags.includes('importante')) importante.add(nota.id);",
      "      }",
      "      api.mapa.redesenhar();",
      "    }",
      "    recontar();",
      "    api.eventos.on('nota:alterada', recontar);",
      "    api.eventos.on('nota:criada', recontar);",
      "",
      "    api.mapa.camada((ctx, vista) => {",
      "      for (const casa of vista.casas()) {",
      "        if (!importante.has(casa.id)) continue;",
      "        const p = vista.paraTela(casa.x + casa.w / 2, casa.y - 0.4);",
      "        ctx.font = Math.round(vista.tile * 0.9) + 'px sans-serif';",
      "        ctx.textAlign = 'center';",
      "        ctx.fillText('⭐', p.x, p.y);",
      "      }",
      "    });",
      "  }",
      "});",""].join('\n')}},
    assistente:{nome:'Ferramenta do Assistente',descricao:'Ensina uma habilidade nova ao Assistente',codigo:function(n){return[
      "// "+n+" — o Assistente ganha uma ferramenta para saber a data e a hora.",
      "urbe.plugin({",
      "  nome: '"+n+"',",
      "  versao: '1.0',",
      "  descricao: 'Data e hora para o Assistente',",
      "",
      "  ligar(api) {",
      "    api.ia.ferramenta({",
      "      nome: 'agora',",
      "      descricao: 'Devolve a data e a hora atuais do aparelho do usuário.',",
      "      executar() {",
      "        return new Date().toLocaleString('pt-BR');",
      "      }",
      "    });",
      "  }",
      "});",""].join('\n')}}
  };
  function novo(nome,modelo){
    nome=String(nome||'').trim()||'Meu plugin';var m=MODELOS[modelo]||MODELOS.basico,base=PASTA+nome.replace(/[\\/:*?"<>|]/g,'-'),p=base+'.js',i=2;
    while(docs.get(p)){p=base+' '+i+'.js';i++}
    return docs.upsert({path:p,content:m.codigo(nome.replace(/'/g,"\\'"))},{source:'plugins.new'});
  }
  function guia(){
    return['PLUGINS DO URBE — arquivos .js em '+PASTA,
      'Um plugin chama urbe.plugin({ nome, versao, descricao, ligar(api){...}, desligar(api){...} }).',
      'Ele só roda depois que o usuário liga na tela Personalização → Plugins; se o código mudar, precisa ser ligado de novo.',
      '',
      'api.aviso(texto)                                   → mensagem rápida na tela',
      'api.dialogo.alert/confirm/prompt/choose/menu        → diálogos (devolvem Promise)',
      'api.comando({ titulo, executar(ctx) })              → aparece na paleta de comandos (Ctrl/⌘+K)',
      'api.botao({ icone, texto, titulo, executar() })     → botão na barra da cidade (ícones: '+(global.UrbeIcons&&global.UrbeIcons.names?global.UrbeIcons.names.join(', '):'search, map, clock, star, file...')+')',
      'api.notas.listar(pasta?) / ler(caminho) / existe(caminho) / escrever(caminho, texto) / abrir(caminho) / atual() / buscar(texto)',
      '   nota = { id, caminho, titulo, conteudo, tags, links }',
      'api.eventos.on(nome, fn)  nomes: nota:criada, nota:alterada, nota:removida, cidade:aberta, salvo, comando, aparencia',
      'api.estilo(css)                                     → adiciona CSS (removido ao desligar)',
      'api.mapa.camada((ctx, vista) => {...})              → desenha sobre a cidade; vista = { tile, zoom, centro, visivel, largura, altura, paraTela(x,y), casas(), ambiente }',
      'api.mapa.ir(x, y, zoom) / opcoes({moradores,fauna,nomes,bairros,ambiente}) / redesenhar() / vista()',
      'api.ia.ferramenta({ nome, descricao, parametros (JSON Schema), escreve: bool, executar(args) }) → nova ferramenta do Assistente',
      'api.dados.ler(chave, padrao) / gravar(chave, valor)  → memória do plugin neste aparelho',
      'api.intervalo(fn, ms) / api.depois(fn, ms) / api.aoDesligar(fn)',
      'api.executar(idDoComando, ctx) / api.comandos()     → usa qualquer comando do app',
      'api.personalizacao.ler() / mudar({...})             → lê ou muda o tema.json',
      '',
      'Tudo que a api cria é desfeito sozinho quando o plugin é desligado. Erros não derrubam o app: o plugin fica marcado com erro.',
      'Plugins não podem escrever na pasta de plugins.'].join('\n');
  }

  /* ---------- ciclo de vida ---------- */
  var t=null;function agendar(){clearTimeout(t);t=setTimeout(sincronizar,150)}
  core.events.on('workspace:loaded',function(){Array.from(ativos.keys()).forEach(parar);estado.clear();sincronizar()});
  ['document:created','document:updated','document:removed'].forEach(function(n){core.events.on(n,function(e){var d=e&&(e.document||e.previous);if(d&&d.path&&(d.path.indexOf(PASTA)===0||(e.previous&&e.previous.path.indexOf(PASTA)===0)))agendar()})});
  core.events.on('customize:safe-mode',function(){sincronizar()});

  var api={FOLDER:PASTA,TEMPLATES:Object.keys(MODELOS).map(function(k){return{id:k,nome:MODELOS[k].nome,descricao:MODELOS[k].descricao}}),
    list:lista,enable:ligar,disable:desligar,sync:sincronizar,create:novo,guide:guia,fingerprint:impressao,active:function(){return Array.from(ativos.keys())}};
  core.provide('plugins',api);global.UrbePlugins=api;
  core.commands.register('plugins.reload',{title:'Recarregar plugins',category:'Personalização',execute:function(){Array.from(ativos.keys()).forEach(parar);return sincronizar()}});
})(window);
