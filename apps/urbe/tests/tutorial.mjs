import fs from 'node:fs';import vm from 'node:vm';
const read=f=>fs.readFileSync(new URL('../'+f,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
const wait=ms=>new Promise(r=>setTimeout(r,ms));

function montar(opts){
  const storage=new Map(),files=new Map(),respostas=(opts&&opts.respostas)||[];
  const W={console:{...console,warn(){}},setTimeout,clearTimeout,localStorage:{getItem:k=>storage.has(k)?storage.get(k):null,setItem:(k,v)=>storage.set(k,String(v)),removeItem:k=>storage.delete(k)},
    document:{getElementById(){return null}},UrbeDialogs:{confirm:async()=>respostas.shift()??false,menu:async()=>{},alert:async()=>{}}};
  W.window=W;const c=vm.createContext(W);
  for(const f of ['src/core/core.js','src/core/documents.js','src/tutorial/content.js','src/tutorial/tutorial.js'])vm.runInContext(read(f),c,{filename:f});
  const core=W.UrbeCore,abertos=[];
  core.provide('persistence',{vault:'Teste',adapter:{read:async(v,p)=>files.get(p)||null,write:async(v,p,t)=>files.set(p,t)}});
  core.commands.register('document.open',{execute:ctx=>abertos.push(ctx.path||ctx.id)});
  return{W,core,docs:core.service('documents'),T:core.service('tutorial'),storage,files,abertos,respostas};
}
const C=montar().W.UrbeTutorialContent;

await test('conteúdo: dezenas de notas, começa em "Comece aqui", cobre todas as áreas',()=>{
  const paths=Object.keys(C.files);ok(paths.length>=40,'poucas notas: '+paths.length);ok(paths.includes('Tutorial/Comece aqui.md'));
  for(const area of ['Cidade','Notas','Matemática','Páginas','Assistente','Personalização','Arquivos e segurança','Celular e computador'])ok(paths.some(p=>p.startsWith('Tutorial/'+area+'/')),'falta '+area);
  const tudo=Object.values(C.files).join('\n');
  for(const termo of ['Ctrl+K','Lixeira','Versões anteriores','Exportar tudo (.zip)','Modo seguro','urbe.plugin','tema.json','Composições','Pede aprovação','Diagnóstico de toques','Instalar','Markdown','[!tip]'])ok(tudo.includes(termo),'o Tutorial não fala de '+termo);
});

await test('primeira abertura: cria a pasta, abre "Comece aqui" e marca no vault e no aparelho',async()=>{
  const m=montar();m.core.events.emit('workspace:loaded',{});await wait(1400);
  ok(m.docs.list().filter(d=>d.path.startsWith('Tutorial/')).length===Object.keys(C.files).length,'todas as notas');
  ok(m.files.has('.urbe/tutorial.json')&&[...m.storage.keys()].some(k=>k.startsWith('urbe.tutorial.v1::')),'marcas');
  ok(m.abertos.includes('Tutorial/Comece aqui.md'),'abre o começo numa cidade vazia');
});

await test('apagado pela pessoa não volta sozinho; outra cidade com marca no vault também não recria',async()=>{
  const m=montar();m.core.events.emit('workspace:loaded',{});await wait(400);
  m.docs.list().filter(d=>d.path.startsWith('Tutorial/')).forEach(d=>m.docs.remove(d.id));
  m.core.events.emit('workspace:loaded',{});await wait(400);ok(!m.docs.list().some(d=>d.path.startsWith('Tutorial/')),'não recriou');
  const n=montar();n.files.set('.urbe/tutorial.json','{"versao":"x"}');n.core.events.emit('workspace:loaded',{});await wait(400);
  ok(!n.docs.list().length,'marca no vault vale em outro aparelho');
  const cheia=montar();cheia.docs.upsert({path:'Minha nota.md',content:'oi'});cheia.core.events.emit('workspace:loaded',{});await wait(1400);
  ok(cheia.docs.get('Tutorial/Comece aqui.md')&&!cheia.abertos.length,'cidade com notas: cria sem abrir');
});

await test('pasta nova com o mesmo nome: a marca antiga do aparelho não impede o Tutorial',async()=>{
  const m=montar();m.storage.set('urbe.tutorial.v1::Teste','versao-antiga');m.core.events.emit('workspace:loaded',{});await wait(1400);
  ok(m.docs.get('Tutorial/Comece aqui.md'),'cria na pasta nova, que não tem a marca dentro dela');ok(m.files.has('.urbe/tutorial.json'),'marca gravada na pasta');
});

await test('restaurar: recria o que falta e só substitui o que mudou se a pessoa aceitar',async()=>{
  const m=montar({respostas:[false,true]});m.core.events.emit('workspace:loaded',{});await wait(400);
  const g='Tutorial/Glossário.md',n='Tutorial/Notas/Links entre notas.md';
  m.docs.remove(m.docs.get(g).id);const d=m.docs.get(n);m.docs.upsert({...d,content:'minha versão'});
  let r=await m.T.restore(false);ok(r.criadas===1&&r.substituidas===0&&m.docs.get(g)&&m.docs.get(n).content==='minha versão','mantém a versão da pessoa');
  r=await m.T.restore(false);ok(r.substituidas===1&&m.docs.get(n).content===C.files[n],'substitui quando aceita');
});

if(failed)console.error(failed+' falha(s)');
