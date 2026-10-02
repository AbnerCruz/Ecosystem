import fs from 'node:fs';import vm from 'node:vm';
const read=p=>fs.readFileSync(new URL('../'+p,import.meta.url),'utf8');
function test(name,fn){try{fn();console.log('OK  ',name)}catch(e){console.error('FAIL',name,e.message);process.exitCode=1}}
const context={window:{},setTimeout,clearTimeout,console};vm.createContext(context);
for(const f of ['src/core/artifacts.js','src/core/core.js','src/core/documents.js','src/ui/quick-open.js'])vm.runInContext(read(f),context);
const core=context.window.UrbeCore,docs=core.service('documents'),rank=core.service('quickOpen').rank;
docs.upsert({path:'Receitas/Pão de queijo.md',content:'# Pão de queijo\n\nPolvilho azedo e queijo minas.'});
docs.upsert({path:'Viagem.md',content:'Comprar queijo na feira.'});
docs.upsert({path:'Queijaria.md',content:'loja'});
docs.upsert({path:'Ideias.md',content:'nada'});

test('busca parcial e sem acentos',()=>{
  const r=rank(docs.list(),'queij').map(x=>x.doc.title);
  if(!r.includes('Pão de queijo')||!r.includes('Viagem'))throw new Error(JSON.stringify(r));
  if(rank(docs.list(),'pao').length===0)throw new Error('“pao” deveria achar “Pão”');
});
test('título vem antes de conteúdo; começa-com antes de contém',()=>{
  const r=rank(docs.list(),'queij').map(x=>x.doc.title);
  if(r[0]!=='Queijaria')throw new Error('ordem: '+r.join(', '));
  if(r.indexOf('Pão de queijo')>r.indexOf('Viagem'))throw new Error('título deveria vencer conteúdo: '+r.join(', '));
});
test('trecho destaca o termo e ignora o cabeçalho igual ao título',()=>{
  const hit=rank(docs.list(),'polvilho')[0];
  if(!hit||hit.snippet.match.toLowerCase()!=='polvilho')throw new Error(JSON.stringify(hit&&hit.snippet));
  const t=rank(docs.list(),'queijo').find(x=>x.doc.title==='Pão de queijo');
  if(/^#/.test(t.snippet.before)||t.snippet.before.includes('Pão de queijo'))throw new Error('trecho repetiu o título: '+JSON.stringify(t.snippet));
});
test('várias palavras em qualquer ordem',()=>{
  const r=rank(docs.list(),'minas polvilho').map(x=>x.doc.title);
  if(r[0]!=='Pão de queijo')throw new Error(r.join(', '));
});
test('consulta vazia não lista nada no ranking',()=>{if(rank(docs.list(),'  ').length)throw new Error('vazio')});

test('editor Visual: menu /, bolha e serialização de <div> com blocos',()=>{
  const vt=read('src/editor/visual-tools.js'),app=read('src/app.js'),index=read('index.html');
  if(!index.includes('./src/editor/visual-tools.js')||index.indexOf('./src/editor/visual-tools.js')<index.indexOf('./src/app.js'))throw new Error('visual-tools deve carregar depois do app');
  for(const k of ['urbeSlashMenu','urbeFormatBubble',"act:'rule'","act:'task'"])if(!vt.includes(k))throw new Error('falta '+k);
  if(!read('src/editor/visual.js').includes('if(tag==="div"&&[...el.children].some('))throw new Error('div com blocos não é serializada (serializador em src/editor/visual.js)');
  if(app.includes("md==='\\\\n---\\\\n'"))throw new Error('comparação de divisória com barra literal voltou');
  if(!app.includes('if(!sel.isCollapsed)return sel.getRangeAt(0).getBoundingClientRect();'))throw new Error('clique volta a colapsar a seleção');
});
