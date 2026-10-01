// Layout livre: árvore de peças, estilos seguros e responsivos, conversão de blocos e arquivo enxuto.
import fs from 'node:fs';import vm from 'node:vm';
const read=f=>fs.readFileSync(new URL('../'+f,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
const c={window:{},console};vm.createContext(c);
for(const f of ['src/core/artifacts.js','src/core/core.js','src/core/documents.js','src/pages/engine.js','src/pages/free.js','src/pages/templates.js'])vm.runInContext(read(f),c);
const W=c.window,P=W.UrbePages,X=P.free,TPL=W.UrbePageTemplates,docs=W.UrbeCore.service('documents');
docs.upsert({path:'Cap/01 A.md',content:'# 01 A\n\nPrimeiro parágrafo.\n\n## Seção\n\nSegundo.\n\n***\n\n> Uma citação'});
const css=h=>h.match(/<style>([\s\S]*?)<\/style>/)[1];

await test('árvore: tipos, ids únicos, só containers têm filhos, erros apontados pelo caminho',()=>{
  const n=P.normalize({sections:[{id:'s1',type:'free',props:{root:{type:'box',children:[{id:'a',type:'text'},{id:'a',type:'heading',children:[{type:'text'}]},{type:'xpto'},{type:'box',style:{width:'banana',color:'red;}body{x',gap:'.5rem'}}]}}}]});
  const r=n.spec.sections[0].props.root,ids=new Set();X.walk(r,k=>{ok(!ids.has(k.id),'id repetido');ids.add(k.id)});
  ok(r.children.length===3,'o tipo inexistente sai');ok(!r.children[1].children,'título não tem filhos');
  const msgs=n.errors.map(e=>e.path).join(' ');ok(/children\[2\]\.type/.test(msgs)&&/style\.width/.test(msgs)&&/style\.color/.test(msgs),msgs);
  ok(r.children[2].style.gap==='.5rem'&&!r.children[2].style.width,'o válido fica, o inválido sai');
  ok(n.warnings.some(w=>/children\[1\]\.children/.test(w.path)),'aviso de filhos ignorados');
});
await test('responsivo: base, tablet e celular; lado a lado quebra linha; grade por colunas',()=>{
  const h=P.render({sections:[{id:'s1',type:'free',props:{root:{type:'box',style:{display:'row',minCol:'240px',gap:'20px'},mobile:{display:'stack'},children:[{id:'t',type:'heading',content:{text:'Oi',level:1},style:{size:'3rem'},tablet:{size:'2.4rem'},mobile:{size:'2rem',display:'none'}},{type:'box',style:{display:'grid',columns:3},tablet:{columns:2},children:[]}]}}}]});
  const s=css(h);
  ok(/\.fx-s1-[\w]+\{display:flex;flex-direction:row;flex-wrap:wrap;gap:20px;--fx-basis:240px\}/.test(s),'linha com quebra');
  ok(/>\*\{flex:1 1 var\(--fx-basis,220px\)\}/.test(s),'filhos dividem a linha');
  ok(/@media \(max-width:900px\)\{[^@]*font-size:2\.4rem[^@]*repeat\(2,/.test(s),'tablet');
  ok(/@media \(max-width:600px\)\{[^@]*flex-direction:column[^@]*>\*\{flex:0 1 auto\}[^@]*display:none;font-size:2rem/.test(s),'celular');
  ok(/<h1 class="fx fx-s1-t">/.test(h),'título h1 com classe da seção');
});
await test('seguro: texto, CSS, imagem de fundo e classe não injetam nada',()=>{
  const h=P.render({sections:[{id:'s1',type:'free',props:{root:{type:'box',style:{bgImage:"https://x/a.png') ;}</style><script>alert(1)</script>",className:'a"><b',css:'</style><script>alert(2)</script>'},children:[{type:'text',content:{text:'<img src=x onerror=alert(3)>'}},{type:'heading',content:{text:'<script>alert(4)</script>'}},{type:'button',content:{label:'<b>x',url:'javascript:alert(5)'}}]}}}]});
  [/<script>alert/,/<img src=x onerror/,/javascript:alert/,/<\/style><script/].forEach(r=>{const m=h.match(r);ok(!m,'vazou '+r+' em: '+(m?h.slice(Math.max(0,m.index-80),m.index+30):''))});ok(!/class="[^"]*"><b/.test(h),'classe limpa');
});
await test('duas seções livres não colidem (classes por seção) e a base não sobrescreve estilos',()=>{
  const root={type:'box',children:[{id:'k',type:'text',style:{color:'#ff0000'}}]};
  const h=P.render({sections:[{id:'s1',type:'free',props:{root}},{id:'s2',type:'free',props:{root:JSON.parse(JSON.stringify(root))}}]});
  ok(h.includes('fx-s1-k')&&h.includes('fx-s2-k'),'classes separadas');ok((css(h).match(/:where\(\.fx-box\)/g)||[]).length===1,'base uma vez só');
});
await test('converter: capítulo vira título + parágrafos soltos; capa, rosto e outros também',()=>{
  const note=p=>{const d=docs.get(p)||docs.get(p+'.md');return d?{title:d.title,content:d.content}:null};
  const ch=X.fromBlock({type:'chapter',props:{source:'note',path:'Cap/01 A.md',title:''}},{note});
  ok(ch.children.map(k=>k.type).join()==='heading,text,heading,text,divider,quote',ch.children.map(k=>k.type).join());
  for(const s of P.normalize(TPL.build('book',{title:'L'})).spec.sections){if(!X.CONVERTIBLE.includes(s.type))continue;const t=X.fromBlock(s,{note});const n=P.normalize({sections:[{type:'free',props:{root:t}}]});ok(!n.errors.length,s.type+': '+JSON.stringify(n.errors))}
});
await test('livro: página livre vira folha própria, página inteira ou continua o texto; quebra de página',()=>{
  const h=P.render({layout:{format:'book'},sections:[{type:'free',props:{sheet:'full',root:{type:'box',children:[{type:'text'},{type:'pagebreak'}]}}},{type:'free',props:{sheet:'flow'}},{type:'free',props:{sheet:'page'}}]});
  ok(/class="sheet bk-free bk-full bk-bleed"/.test(h)&&/class="sheet flow"/.test(h)&&/class="sheet bk-free"/.test(h),'três modos');ok(/\.bk-bleed\{page:cover/.test(h)&&/fx-break/.test(h),'sangria e quebra');
});
await test('arquivo enxuto: grava só o que muda e abre igual (todos os modelos)',()=>{
  for(const t of TPL.list()){const sp=TPL.build(t.id,{title:'X',folder:'Cap',note:{title:'A',path:'Cap/01 A.md'}});
    ok(JSON.stringify(P.normalize(sp).spec)===JSON.stringify(P.normalize(P.compact(sp)).spec),t.id+' ida e volta');
    ok(JSON.stringify(P.compact(sp)).length<JSON.stringify(P.normalize(sp).spec).length,t.id+' menor')}
  const c2=P.compact({sections:[{type:'text',props:{markdown:'oi'}}]});ok(!c2.theme&&!(c2.meta&&c2.meta.lang)&&!c2.layout&&!c2.sections[0].style,'padrões fora do arquivo');
});
await test('composições prontas e modelo Tela livre são válidos',()=>{
  for(const k of Object.keys(X.PRESETS)){const n=P.normalize({sections:[{type:'free',props:{root:{type:'box',children:[X.PRESETS[k].build()]}}}]});ok(!n.errors.length,k+': '+JSON.stringify(n.errors))}
  const sp=TPL.build('canvas',{title:'T'});ok(sp.sections[0].type==='free'&&!P.normalize(sp).errors.length,'Tela livre');
  ok(P.schemaText().includes('free — Layout livre'),'o Assistente conhece o bloco');
});
await test('peças novas: tabela, código, fórmula, selo e incorporar (só https, isolado); tamanhos com clamp sem injeção',()=>{
  const root={type:'box',children:[{type:'table'},{type:'code',content:{code:'<b>x</b>',lang:'js'}},{type:'formula',content:{tex:'a^2+b^2=c^2'}},{type:'badge',content:{text:'Novo'}},
    {type:'embed',content:{url:'https://example.com/m'}},{id:'bad',type:'embed',content:{url:'javascript:alert(1)'}},{id:'c',type:'heading',style:{size:'clamp(1.5rem, 5vw, 3rem)'}},{id:'x',type:'heading',style:{size:'calc(1px);}body{x:1'}}]};
  const n=P.normalize({sections:[{id:'s1',type:'free',props:{root}}]});const h=P.render({sections:[{id:'s1',type:'free',props:{root}}]});
  ok(/<table/.test(h)&&/<pre class="[^"]* code"/.test(h)&&h.includes('&lt;b&gt;x&lt;/b&gt;')&&!h.includes('<b>x</b>'),'tabela e código escapado');
  ok(/fx-badge/.test(h)&&/<iframe[^>]*sandbox[^>]*src="https:\/\/example\.com\/m"|<iframe[^>]*src="https:\/\/example\.com\/m"[^>]*sandbox/.test(h),'selo e iframe isolado');
  ok(!h.includes('javascript:'),'incorporar recusa endereço que não é https');
  ok(h.includes('clamp(1.5rem, 5vw, 3rem)')&&!css(h).includes('body{x:1'),'clamp passa, injeção não');
  ok(n.spec.sections.length===1,'normaliza');
});
if(failed)console.error(failed+' falha(s)');
