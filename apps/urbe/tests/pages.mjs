import fs from 'node:fs';import vm from 'node:vm';
const read=f=>fs.readFileSync(new URL('../'+f,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
function eq(a,b,m){if(JSON.stringify(a)!==JSON.stringify(b))throw new Error((m||'')+'\n esperado '+JSON.stringify(b)+'\n obtido  '+JSON.stringify(a))}

const c={window:{},console,setTimeout,clearTimeout};vm.createContext(c);
for(const f of ['src/core/core.js','src/core/documents.js','src/core/trash.js','src/ai/tools.js','src/pages/engine.js','src/pages/free.js','src/pages/templates.js','src/pages/ai-tools.js'])vm.runInContext(read(f),c);
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
  for(const t of TPL.list()){const spec=TPL.build(t.id,{title:'X',folder:'Guia',note:{title:'Uso',path:'Guia/Uso.md'}}),n=P.normalize(spec);eq(n.errors,[],t.id);ok(n.spec.sections.length>0||t.id==='empty',t.id);ok(P.render(spec,{documents:docs}).length>3000,t.id)}
});

await test('livro: capítulos numerados (inclusive de uma pasta), sumário com links, páginas de impressão e injeção barrada',()=>{
  const sp=TPL.build('book',{title:'A Cidade'});eq(sp.layout.format,'book');
  sp.sections.push({type:'chapters',props:{folder:'Guia',sort:'path'}});
  const h=P.render(sp,{documents:docs});
  ok(/<html[^>]*class="[^"]*book/.test(h),'html.book');ok(!/class="nav/.test(h)&&!/class="foot/.test(h)&&/fab-print/.test(h),'sem barra e rodapé; com botão de imprimir');
  ok(/@page\{size:148mm 210mm/.test(h)&&/@page :left/.test(h)&&/@page :right/.test(h),'A5 com margens espelhadas');
  ok(/@bottom-center\{content:counter\(page\)/.test(h)&&/@top-center\{content:"A Cidade"/.test(h),'número de página e cabeçalho');
  ok(/@page cover\{margin:0/.test(h)&&/\.bk-front\{page:front\}/.test(h)&&/@page chapter:first/.test(h),'capa sangrada, páginas iniciais sem número, abertura de capítulo sem cabeçalho');
  ['Capítulo 1','Capítulo 2','Capítulo 3','Capítulo 4','Capítulo 5','Parte I','Parte II'].forEach(t=>ok(h.includes(t),'falta '+t));
  ok(/bk-toc"[\s\S]*href="#nota-instalacao"[\s\S]*href="#nota-uso"/.test(h),'sumário lista os capítulos da pasta com links');
  ok(/<article class="sheet bk-chap dropcap" id="nota-instalacao"/.test(h),'capítulo da pasta com âncora e capitular');
  ok(/<a class="wikilink" href="#nota-uso">/.test(h),'[[Uso]] vira link para o capítulo');
  const sheets=(h.match(/class="sheet /g)||[]).length;ok(sheets>=13,'folhas: '+sheets);
  const evil=P.render({layout:{format:'book',runningHead:'x"}body{display:none}'},sections:[{type:'chapter',props:{title:'<img src=x onerror=alert(1)>',markdown:'<script>alert(1)</script>'}}]});
  ok(!/<img src=x/.test(evil)&&!/<script>alert/.test(evil),'sem HTML injetado');ok(evil.includes('content:"x\\"}body{display:none}"'),'cabeçalho escapado no CSS');
  for(const size of ['a5','6x9','pocket','a4','letter'])for(const m of ['narrow','normal','wide'])ok(/@page\{size:[\d.]+mm [\d.]+mm;margin:[\d.]+mm/.test(P.render({layout:{format:'book',pageSize:size,margins:m},sections:[]})),size+'/'+m);
  const web=P.render({sections:[{type:'chapter',props:{title:'Só'}}]});ok(!/@page\{size/.test(web)&&/class="bk-wrap"/.test(web),'no formato Site, o bloco aparece sem regras de impressão');
});

await test('personalização: tema, seção e página aceitam estilo próprio sem quebrar o HTML',()=>{
  const h=P.render({meta:{title:'x',head:'<meta name="k" content="1">'},theme:{buttonStyle:'pill',cardStyle:'glass',headingCase:'upper',headingScale:1.2,lineHeight:1.8,border:'#abcdef',css:'body{x:1}</style><script>alert(1)</script>'},
    sections:[{id:'s_a',type:'text',props:{markdown:'oi'},style:{bgColor:'#123456',textColor:'#ffffff',boxed:true,minHeight:'half',animation:'zoom',className:'minha "><x',css:'& h2{color:red}'}},{id:'s_b',type:'text',props:{markdown:'b'},style:{css:'padding:0'}}]});
  ['border-radius:999px','backdrop-filter','text-transform:uppercase','line-height:1.8','--border:#abcdef','background:#123456','--text:#ffffff',' boxed',' mh-half','an-zoom','[data-s="s_a"] h2{color:red}','[data-s="s_b"]{padding:0}','<meta name="k"'].forEach(k=>ok(h.includes(k),'falta '+k));
  ok(!/<\/style><script>alert/.test(h),'CSS não fecha o <style>');ok(!/minha "><x/.test(h)&&h.includes('minha x'),'classe limpa');
  const n=P.normalize({theme:{buttonStyle:'balão'},sections:[{type:'text',style:{minHeight:'enorme'}}]});ok(n.errors.length===2,'valores inválidos apontados');
});
await test('modelos: Vazio é o mínimo; toda variação (completo, simplificado, só estrutura) é válida',()=>{
  const e=TPL.build('empty',{});eq(e.sections.length,0);ok(!e.layout.nav&&!e.layout.footer&&!e.theme.animations,'sem barra, rodapé e animação');
  for(const t of TPL.list()){const sp=TPL.build(t.id,{title:'X',folder:'Guia',note:{title:'Uso',path:'Guia/Uso.md'}});
    const si=TPL.variant(sp,'simple'),sk=TPL.variant(sp,'skeleton');
    for(const v of [si,sk])eq(P.normalize(v).errors,[],t.id);
    ok(si.sections.length<=Math.max(4,sp.layout.format==='book'?6:4)&&si.sections.length<=sp.sections.length,t.id+' simplificado menor');
    eq(sk.sections.map(x=>x.type),sp.sections.map(x=>x.type),t.id+' estrutura mantém os blocos');
    si.sections.forEach(x=>Object.values(x.props).forEach(v=>{if(Array.isArray(v))ok(v.length<=2,t.id+' listas curtas')}));}
  const land=TPL.variant(TPL.build('landing',{title:'P'}),'skeleton');ok(!JSON.stringify(land).includes('O jeito mais simples'),'sem os textos de exemplo');
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
