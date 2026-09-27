import fs from 'node:fs';import vm from 'node:vm';
const read=f=>fs.readFileSync(new URL('../'+f,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
function eq(a,b,m){if(JSON.stringify(a)!==JSON.stringify(b))throw new Error((m||'')+'\n esperado '+JSON.stringify(b)+'\n obtido  '+JSON.stringify(a))}

const c={window:{},console,setTimeout,clearTimeout};vm.createContext(c);
for(const f of ['src/core/core.js','src/core/documents.js','src/core/trash.js','src/ai/tools.js','src/pages/engine.js','src/pages/templates.js','src/pages/ai-tools.js'])vm.runInContext(read(f),c);
const W=c.window,P=W.UrbePages,TPL=W.UrbePageTemplates,T=W.UrbeAITools,core=W.UrbeCore,docs=core.service('documents');
docs.upsert({path:'Guia/Instalação.md',content:'# Instalação\n\nRode `npm i`.\n\nVeja [[Uso]].',tags:['guia']});
docs.upsert({path:'Guia/Uso.md',content:'# Uso\n\nUse com calma. #guia'});
docs.upsert({path:'Solta.md',content:'Fora da pasta.'});

await test('normalize: preenche padrões, aponta erros por caminho e avisa propriedades desconhecidas',()=>{
  const n=P.normalize({theme:{preset:'neon',primary:'vermelho!!'},sections:[{type:'hero',props:{title:'Oi',extra:1}},{type:'nope'},{type:'faq',props:{items:'x'}},{type:'hero',style:{padding:'gigante'}}]});
  ok(n.spec.meta.title==='Página sem título','meta padrão');eq(n.spec.sections.length,3,'tipo desconhecido removido');
  const paths=n.errors.map(e=>e.path);ok(paths.includes('theme.primary'),'cor inválida');ok(paths.includes('sections[1].type'),'tipo');ok(paths.includes('sections[2].props.items'),'lista');ok(paths.includes('sections[3].style.padding'),'select');
  ok(n.warnings.some(w=>w.path==='sections[0].props.extra'),'aviso de prop desconhecida');
  ok(n.spec.sections[0].props.buttons&&n.spec.sections[0].props.layout==='center','padrões do bloco');
  const bad=P.normalize('{oops');ok(bad.errors[0].message.startsWith('JSON inválido'));
});

await test('render: página completa, independente e sem HTML/JS injetado pelo conteúdo',()=>{
  const html=P.render({meta:{title:'<script>alert(1)</script>'},sections:[
    {type:'hero',props:{title:'Oi <img src=x onerror=alert(1)>',buttons:[{label:'mal',url:'javascript:alert(1)'},{label:'ok',url:'https://exemplo.com'}],image:'data:text/html;base64,PHNjcmlwdD4='}},
    {type:'text',props:{markdown:'[x](javascript:alert(1)) ![i](data:image/png;base64,AAAA) <b>cru</b>'}}]},{documents:docs});
  ok(html.startsWith('<!doctype html>')&&html.includes('<meta name="viewport"'),'documento completo');
  ok(!/<script>alert/.test(html)&&!/<img src=x/.test(html)&&!/javascript:/.test(html),'escapado');
  ok(html.includes('href="https://exemplo.com"')&&html.includes('src="data:image/png;base64,AAAA"'),'URLs seguras mantidas');
  ok(!html.includes('data:text/html'),'data: não-imagem bloqueado');ok(html.includes('&lt;b&gt;cru&lt;/b&gt;'),'HTML no markdown é texto');
});

await test('markdown: tabelas, tarefas, listas aninhadas, callouts, código e wikilinks',()=>{
  const h=P.markdown('| a | b |\n|:-|-:|\n| 1 | 2 |\n\n- [x] feito\n- item\n  - sub\n\n> [!tip] Dica\n> texto\n\n```js\nif(a<b)x()\n```\n\n**n** *i* ~~r~~ ==m== `c` [[Nota|rótulo]]',{wikilink:(t,l)=>'<a data-w="'+t+'">'+(l||t)+'</a>'});
  ok(h.includes('<table>')&&h.includes('text-align:right'),'tabela');ok(h.includes('checked')&&h.includes('<ul><li>sub</li></ul>'),'listas');
  ok(h.includes('callout-tip')&&h.includes('if(a&lt;b)x()'),'callout e código escapado');
  ok(h.includes('<strong>n</strong>')&&h.includes('<em>i</em>')&&h.includes('<del>r</del>')&&h.includes('<mark>m</mark>')&&h.includes('<code>c</code>'),'inline');
  ok(h.includes('<a data-w="Nota">rótulo</a>'),'wikilink');
});

await test('blocos de notas: conteúdo real, pasta/tag, texto completo com âncoras e links internos',()=>{
  const html=P.render({sections:[{type:'notes',props:{source:'folder',folder:'Guia',expand:true}},{type:'note',props:{path:'Guia/Uso.md'}},{type:'notes',props:{source:'tag',tag:'guia',layout:'list'}}]},{documents:docs});
  ok(html.includes('id="nota-instalacao"')&&html.includes('id="nota-uso"'),'âncoras das notas');
  ok(html.includes('href="#nota-uso"'),'[[Uso]] vira link interno');ok(!html.includes('Fora da pasta'),'filtra pela pasta');
  ok(html.includes('Use com calma'),'nota embutida');const miss=P.render({sections:[{type:'note',props:{path:'Nada.md'}}]},{documents:docs});ok(miss.includes('Nota não encontrada'));
});

await test('menu, sumário, seções ocultas e modo prévia',()=>{
  const spec={layout:{nav:true},sections:[{id:'a1',type:'features',props:{title:'Recursos'},style:{anchor:'Recursos',menu:true}},{id:'b2',type:'toc',props:{}},{id:'c3',type:'text',props:{title:'Oculta',markdown:'segredo'},style:{hidden:true}}]};
  const out=P.render(spec,{documents:docs}),pre=P.render(spec,{documents:docs,preview:true});
  ok(out.includes('<a href="#recursos">Recursos</a>'),'menu');ok(!out.includes('segredo')&&pre.includes('segredo'),'oculta só na prévia');
  ok(pre.includes('data-sid="a1"')&&pre.includes('urbePage')&&!out.includes('data-sid')&&!out.includes('urbePage'),'marcas da prévia não vão para o export');
});

await test('temas: todos os presets e modos geram CSS; fontes do Google só quando precisa',()=>{
  for(const k of Object.keys(P.THEMES))for(const mode of ['','dark','light','auto']){const h=P.render({theme:{preset:k,mode},sections:[]});ok(h.includes('--primary:'),k+' '+mode);if(mode==='auto')ok(h.includes('prefers-color-scheme:dark'))}
  ok(!P.render({theme:{preset:'grafite',headingFont:'system',bodyFont:'system'}}).includes('fonts.googleapis'),'sem fonte externa');
});

await test('modelos embutidos: todos válidos e renderizáveis',()=>{
  for(const t of TPL.list()){const spec=TPL.build(t.id,{title:'X',folder:'Guia',note:{title:'Uso',path:'Guia/Uso.md'}}),n=P.normalize(spec);eq(n.errors,[],t.id);ok(n.spec.sections.length>0,t.id);ok(P.render(spec,{documents:docs}).length>3000,t.id)}
});

await test('Assistente: page_schema descreve todos os blocos; write_page valida, cria, edita e desfaz',async()=>{
  const sc=(await T.get('page_schema').run({},{core})).content;for(const k of Object.keys(P.BLOCKS))ok(sc.includes('\n'+k+' — '),'schema sem '+k);
  const w=T.get('write_page');
  let r=await w.run({path:'Portfólio',template:'portfolio',title:'Ana Lima'},{core});ok(/criada: Páginas\/Portfólio\.page\.json/.test(r.content),r.content);
  const d=docs.get('Páginas/Portfólio.page.json');ok(JSON.parse(d.content).meta.title==='Ana Lima');
  let e=null;try{await w.run({path:'Páginas/Portfólio.page.json',spec:{sections:[{type:'foguete'}]}},{core})}catch(x){e=x}ok(e&&e.toolError&&/foguete/.test(e.message),'erro devolvido ao modelo');
  const pv=w.preview({path:'Páginas/Portfólio.page.json',spec:JSON.stringify({meta:{title:'Nova'},sections:[{type:'text',props:{markdown:'oi'}}]})},{core});ok(pv.before&&pv.after.includes('"Nova"'),'prévia com antes/depois');
  r=await w.run({path:'Páginas/Portfólio.page.json',spec:{meta:{title:'Nova'},sections:[{type:'text',props:{markdown:'oi'}}]}},{core});ok(/atualizada/.test(r.content));
  T.revert({core},r.changes);ok(JSON.parse(docs.get('Páginas/Portfólio.page.json').content).meta.title==='Ana Lima','desfazer volta a versão anterior');
});

if(failed)console.error(failed+' falha(s)');
