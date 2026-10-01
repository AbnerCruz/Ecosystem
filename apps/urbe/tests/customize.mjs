import fs from 'node:fs';import vm from 'node:vm';
const read=f=>fs.readFileSync(new URL('../'+f,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
function eq(a,b,m){if(JSON.stringify(a)!==JSON.stringify(b))throw new Error((m||'')+'\n esperado '+JSON.stringify(b)+'\n obtido  '+JSON.stringify(a))}

/* DOM mínimo: o suficiente para aplicar variáveis, atributos e <style> */
function el(tag){return{tagName:tag,attrs:{},children:[],textContent:'',style:{props:{},setProperty(k,v){this.props[k]=v},removeProperty(k){delete this.props[k]}},
  setAttribute(k,v){this.attrs[k]=String(v)},getAttribute(k){return this.attrs[k]},appendChild(c){this.children.push(c);c.parent=this;return c},remove(){if(this.parent)this.parent.children=this.parent.children.filter(x=>x!==this)},
  addEventListener(){},querySelector(){return null},querySelectorAll(){return[]},insertBefore(c){this.children.push(c);c.parent=this}}}
const html=el('html'),head=el('head'),meta=el('meta');
const document={documentElement:html,head,body:el('body'),createElement:el,getElementById(){return null},querySelector(s){return s.includes('theme-color')?meta:null},querySelectorAll(){return[]},addEventListener(){}};
const storage=new Map(),localStorage={getItem:k=>storage.has(k)?storage.get(k):null,setItem:(k,v)=>storage.set(k,String(v)),removeItem:k=>storage.delete(k)};
const W={document,localStorage,location:{search:''},console:{...console,warn(){}},setTimeout,clearTimeout,setInterval,clearInterval,crypto:globalThis.crypto,TextEncoder,Image:function(){}};
W.window=W;const c=vm.createContext(W);
for(const f of ['src/core/artifacts.js','src/core/core.js','src/core/documents.js','src/ui/dialogs.js','src/ai/tools.js','src/world/pixel-art.js','src/customize/customize.js','src/customize/plugins.js','src/customize/ai-tools.js'])vm.runInContext(read(f),c,{filename:f});
const core=W.UrbeCore,docs=core.service('documents'),C=core.service('customize'),P=core.service('plugins'),T=W.UrbeAITools;
core.provide('persistence',{vault:'Teste'});
const wait=ms=>new Promise(r=>setTimeout(r,ms));

await test('normalize: padrões, erros por caminho e avisos',()=>{
  const n=C.normalize({tema:'sepia',cores:{destaque:'#f00',fundo:'azul'},texto:{tamanho:40,fonte:'serifada'},forma:{cantos:'quadrados'},cidade:{ambiente:'noite',paleta:{agua:['#123456'],lava:['#ff0000']}},extra:1});
  eq(n.config.cores.destaque,'#ff0000','#rgb vira #rrggbb');ok(n.config.texto.fonte==='serifada');ok(n.config.cidade.ambiente==='noite');eq(n.config.cidade.paleta.agua,['#123456']);
  const paths=n.errors.map(e=>e.path);for(const p of ['cores.fundo','texto.tamanho','forma.cantos','cidade.paleta.lava'])ok(paths.includes(p),'erro em '+p);
  ok(n.warnings.some(w=>w.path==='extra'),'aviso de propriedade desconhecida');
  ok(C.normalize('{oops').errors[0].message.startsWith('JSON inválido'));
  eq(C.normalize({}).errors,[],'vazio é válido');
});

await test('variáveis: tema claro/escuro, contraste do destaque, fontes, cantos e largura',()=>{
  const escuro=C.variables(C.normalize({}).config),claro=C.variables(C.normalize({tema:'claro'}).config);
  ok(escuro.vars['--ui-bg']==='#0e0f11'&&escuro.attrs['data-tema']==='escuro');ok(claro.attrs['data-tema']==='claro'&&claro.vars['--ui-tint']==='0,0,0');
  const amarelo=C.variables(C.normalize({cores:{destaque:'#ffd400'}}).config);ok(amarelo.vars['--ui-accent-ink']==='#0b1325','texto escuro sobre destaque claro');
  const azul=C.variables(C.normalize({tema:'claro',cores:{destaque:'#1a3a9a'}}).config);ok(azul.vars['--ui-accent-ink']==='#ffffff','texto branco sobre destaque escuro');
  const v=C.variables(C.normalize({texto:{fonte:'mono',tamanhoEditor:20},forma:{cantos:'retos'},editor:{largura:'total'}}).config).vars;
  ok(/monospace/.test(v['--ui-font'])&&v['--ui-editor-size']==='20px'&&v['--ui-r-m']==='4px'&&v['--ui-editor-width']==='100%');
  const fundoClaro=C.variables(C.normalize({cores:{fundo:'#fafafa'}}).config);ok(fundoClaro.claro,'trocar o fundo decide claro/escuro');
  ok(C.variables(C.normalize({texto:{fonte:'Inter; } body{display:none'}}).config).vars['--ui-font'].indexOf('}')<0,'fonte própria sem injeção');
});

await test('set grava só o que difere do padrão e aplica na raiz; replace remove chaves',async()=>{
  C.set({tema:'floresta',cidade:{fauna:false}});const d=docs.get('Personalização/tema.json');ok(d,'arquivo criado');
  const j=JSON.parse(d.content);eq(Object.keys(j).sort(),['$schema','cidade','tema','versao'].sort());eq(j.cidade,{fauna:false});
  ok(html.style.props['--ui-accent']==='#8fd18a'&&html.attrs['data-tema']==='escuro','variáveis na raiz');ok(meta.attrs.content==='#0d120e','theme-color');
  ok(JSON.parse(storage.get('urbe.aparencia.v1')).vars['--ui-bg']==='#0d120e','cache para abrir já com o tema');
  const cfg=C.get();cfg.cidade.fauna=true;C.replace(cfg);ok(!JSON.parse(docs.get('Personalização/tema.json').content).cidade,'voltou ao padrão');
  const bad=C.set({cores:{texto:'nada'}});ok(bad.errors.length&&C.get().cores.texto===undefined,'erro não grava');
});

await test('editar o tema.json à mão recarrega; tema salvo e estilos CSS',async()=>{
  docs.upsert({path:'Personalização/tema.json',content:'{"tema":"vinho","css":"body{--x:1}"}'});await wait(200);
  ok(C.get().tema==='vinho','recarregou');ok(head.children.some(s=>s.textContent==='body{--x:1}'),'css rápido aplicado');
  const p=C.saveTheme('Meu roxo');ok(p==='Personalização/temas/Meu roxo.json');C.set({tema:p});ok(C.palette().cores.destaque===C.PRESETS.vinho.cores.destaque,'tema salvo guarda as cores');
  docs.upsert({path:'Personalização/estilos/a.css',content:'h1{color:red}'});C.set({estilos:{'Personalização/estilos/a.css':true}});
  ok(head.children.some(s=>s.textContent==='h1{color:red}'),'estilo ligado');eq(C.styles().map(s=>s.ligado),[true]);
  C.safeMode(true);ok(!head.children.some(s=>s.textContent==='h1{color:red}'),'modo seguro desliga estilos');C.safeMode(false);ok(head.children.some(s=>s.textContent==='h1{color:red}'));
});

await test('mundo: paleta e texturas próprias no motor de arte',()=>{
  const A=W.UrbeArt,orig=A.PAL.grass.slice();
  A.configure({palette:{grass:['#ff0000']}});ok(A.PAL.grass[0]==='#ff0000'&&A.PAL.grass[1]!==orig[1],'tons derivados da base');
  const t=new Array(1024).fill(0).map((_,i)=>i%4===3?0:200);A.configure({textures:{sea:[t]}});ok(A.texture('sea',2)[3]===255,'textura própria opaca e repetida nas variações');
  A.configure({});eq(A.PAL.grass,orig,'configure vazio restaura');ok(C.biomeId('agua').length===4&&C.biomeId('grama')[0]==='grass'&&C.buildingId('casa')==='house');
});

await test('plugins: desligado por padrão, ligar roda, api limpa ao desligar, código mudado para',async()=>{
  const d=P.create('Contador','basico');await wait(250);eq(P.list().map(x=>x.status),['desligado']);
  ok(!core.commands.list().some(c=>c.id.startsWith('plugin.')),'nada roda sem aprovação');
  const st=await P.enable(d.path);ok(st.status==='ativo',JSON.stringify(st));ok(core.commands.has('plugin.contador.contar-notas'),'comando registrado');
  await P.disable(d.path);ok(!core.commands.has('plugin.contador.contar-notas'),'comando removido ao desligar');
  await P.enable(d.path);docs.upsert({...docs.get(d.path),content:docs.get(d.path).content+'\n//x'});await wait(300);
  ok(P.list()[0].status==='mudou'&&!core.commands.has('plugin.contador.contar-notas'),'código mudou → para até nova aprovação');
  const e=docs.upsert({path:'Personalização/plugins/quebrado.js',content:'urbe.plugin({nome:"Q",ligar(){throw new Error("bum")}})'});const s2=await P.enable(e.path);ok(s2.status==='erro'&&/bum/.test(s2.erro),'erro isolado');
  const f=docs.upsert({path:'Personalização/plugins/ia.js',content:'urbe.plugin({nome:"IA",ligar(api){api.ia.ferramenta({nome:"eco",executar:a=>"eco "+a.t})}})'});await P.enable(f.path);
  ok(T.get('plugin_ia_eco'),'ferramenta do Assistente registrada');await P.disable(f.path);ok(!T.get('plugin_ia_eco'),'e removida');
  const g=docs.upsert({path:'Personalização/plugins/escreve.js',content:'urbe.plugin({nome:"W",ligar(api){api.notas.escrever("Personalização/plugins/x.js","mal")}})'});const s3=await P.enable(g.path);ok(s3.status==='erro','não escreve na pasta de plugins');
});

await test('Assistente: appearance_schema, set_appearance valida e desfaz, plugin_guide',async()=>{
  const sc=(await T.get('appearance_schema').run({},{core})).content;ok(sc.includes('"cidade"')&&sc.includes('CONFIGURAÇÃO ATUAL'));
  let err=null;try{await T.get('set_appearance').run({config:{forma:{cantos:'estrela'}}},{core})}catch(e){err=e}ok(err&&err.toolError&&/forma.cantos/.test(err.message));
  const antes=docs.get('Personalização/tema.json').content,r=await T.get('set_appearance').run({config:{tema:'oceano'}},{core});ok(C.get().tema==='oceano');
  T.revert({core},r.changes);await wait(200);ok(docs.get('Personalização/tema.json').content===antes,'desfazer');
  ok((await T.get('plugin_guide').run({},{core})).content.includes('api.mapa.camada'));
});

if(failed)console.error(failed+' falha(s)');
