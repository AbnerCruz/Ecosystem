// App instalado: a ponte (src/native/bridge.js) sobre o disco de verdade (native/desktop/vault-fs.js)
// e a sincronização com mudanças feitas por fora do app (persistence.syncFromDisk).
import fs from 'node:fs';import os from 'node:os';import path from 'node:path';import vm from 'node:vm';import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const read=f=>fs.readFileSync(new URL('../'+f,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
async function rejects(p,name,m){try{await p}catch(e){if(name&&e.name!==name)throw new Error((m||'erro')+': esperava '+name+', veio '+e.name+' '+e.message);return}throw new Error((m||'')+': deveria falhar')}

const {createVaultFS}=require('../native/desktop/vault-fs.js');
const tmp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-native-'));
const vaultDir=path.join(tmp,'Meu Vault');fs.mkdirSync(vaultDir);
fs.writeFileSync(path.join(tmp,'segredo.txt'),'fora da pasta');
const vfs=createVaultFS(()=>vaultDir);

function bridgeContext(){
  const doc={documentElement:{classList:{add(){}}},addEventListener(){},getElementById(){return null},querySelectorAll(){return[]}};
  const win={UrbeNative:{shell:'electron',platform:'linux',vault:async()=>({label:vaultDir,path:vaultDir}),fs:vfs},navigator:{},document:doc};
  const c={window:win,document:doc,console,File,Blob,TextEncoder,TextDecoder,DOMException,ArrayBuffer,Uint8Array,Symbol,Promise,fetch,setTimeout,clearTimeout};
  vm.createContext(c);vm.runInContext(read('src/native/bridge.js'),c);return win;
}

await test('disco: nada sai da pasta do Urbe (.., absoluto, atalho)',async()=>{
  for(const p of ['../segredo.txt','a/../../segredo.txt','..','/etc/passwd','a/./b'])await rejects(vfs.readBytes(p),null,p);
  try{fs.symlinkSync(tmp,path.join(vaultDir,'atalho'))}catch(_){}
  if(fs.existsSync(path.join(vaultDir,'atalho')))await rejects(vfs.readBytes('atalho/segredo.txt'),null,'atalho');
  await rejects(vfs.remove('',true),null,'apagar a raiz');
  fs.rmSync(path.join(vaultDir,'atalho'),{force:true});
});
await test('disco: grava por troca atômica (sem arquivo temporário sobrando)',async()=>{
  await vfs.writeBytes('Notas/a.md',new TextEncoder().encode('um'));await vfs.writeBytes('Notas/a.md',new TextEncoder().encode('dois'));
  ok(fs.readFileSync(path.join(vaultDir,'Notas','a.md'),'utf8')==='dois','conteúdo');
  ok(fs.readdirSync(path.join(vaultDir,'Notas')).join()==='a.md','sem temporários: '+fs.readdirSync(path.join(vaultDir,'Notas')));
});
await test('disco: gravações simultâneas do mesmo arquivo não se atropelam (a última vence)',async()=>{
  const jobs=[];for(let i=0;i<20;i++)jobs.push(vfs.writeBytes('Corrida/n.md',new TextEncoder().encode('versão '+i)));
  await Promise.all(jobs);
  ok(fs.readFileSync(path.join(vaultDir,'Corrida','n.md'),'utf8')==='versão 19','última gravação vence');
  ok(fs.readdirSync(path.join(vaultDir,'Corrida')).join()==='n.md','sem temporários');
  let e=null;try{await vfs.writeBytes('../fora.md',new Uint8Array(1))}catch(x){e=x}ok(e&&!fs.existsSync(path.join(tmp,'fora.md')),'fora da pasta: rejeita');
});
await test('ponte: raiz com um vault só ("Urbe") apontando para a pasta escolhida',async()=>{
  const W=bridgeContext(),root=await W.UrbeNativeFS.root();
  const names=[];for await(const [n] of root.entries())names.push(n);
  ok(names.join()==='Urbe',names.join());ok(root.name===vaultDir,'nome é o caminho real');
  await rejects(root.getDirectoryHandle('Outra',{create:true}),'NotAllowedError','não cria outro vault');
  await rejects(root.removeEntry('Urbe',{recursive:true}),'NotAllowedError','não apaga o vault');
  ok(typeof W.showDirectoryPicker!=='function','sem escolher pasta quando a casca não oferece');
});
await test('ponte: handles iguais aos do File System Access (criar, ler, gravar, listar, apagar)',async()=>{
  const W=bridgeContext(),v=await (await W.UrbeNativeFS.root()).getDirectoryHandle('Urbe');
  await rejects(v.getDirectoryHandle('Tutorial'),'NotFoundError','não existe ainda');
  const d=await (await v.getDirectoryHandle('Tutorial',{create:true})).getDirectoryHandle('Cidade',{create:true});
  const fh=await d.getFileHandle('Bairros.md',{create:true}),w=await fh.createWritable();await w.write('# Bairros\n');await w.write(new Blob(['ção']));await w.close();
  ok(fs.readFileSync(path.join(vaultDir,'Tutorial','Cidade','Bairros.md'),'utf8')==='# Bairros\nção','arquivo real no disco');
  const f=await fh.getFile();ok(await f.text()==='# Bairros\nção'&&f.type==='text/markdown'&&f.name==='Bairros.md','getFile');
  const png=await d.getFileHandle('mapa.png',{create:true}),pw=await png.createWritable();await pw.write(new Uint8Array([137,80,78,71,0,255]));await pw.close();
  ok(Buffer.compare(fs.readFileSync(path.join(vaultDir,'Tutorial','Cidade','mapa.png')),Buffer.from([137,80,78,71,0,255]))===0,'binário intacto');
  const seen=[];for await(const [n,h] of d.entries())seen.push(n+':'+h.kind);ok(seen.sort().join()==='Bairros.md:file,mapa.png:file',seen.join());
  await rejects(v.getFileHandle('Tutorial'),'TypeMismatchError','pasta não é arquivo');
  await rejects(v.getDirectoryHandle('..'),'TypeError','nome inválido');
  await rejects(v.removeEntry('Tutorial'),'InvalidModificationError','pasta com conteúdo');
  await d.removeEntry('mapa.png');ok(!fs.existsSync(path.join(vaultDir,'Tutorial','Cidade','mapa.png')),'apagou arquivo');
  await v.removeEntry('Tutorial',{recursive:true});ok(!fs.existsSync(path.join(vaultDir,'Tutorial')),'apagou pasta');
  ok(await v.queryPermission({mode:'readwrite'})==='granted','permissão');
});

/* persistência: a mesma do app, com um adaptador em memória no lugar do disco */
function persistenceContext(){
  const c={window:{},console,setTimeout,clearTimeout};vm.createContext(c);
  for(const f of ['src/core/core.js','src/core/documents.js','src/persistence/workspace.js'])vm.runInContext(read(f),c);
  const W=c.window,disk=new Map([['A.md','# A'],['Pasta/B.md','# B']]);
  const adapter={list:async()=>[...disk.keys()],read:async(_v,p)=>disk.has(p)?disk.get(p):null,write:async(_v,p,t)=>{disk.set(p,t)},remove:async(_v,p)=>{disk.delete(p)},createFolder:async()=>{},removeFolder:async()=>{}};
  const P=W.UrbeCore.service('persistence').configure(adapter),D=W.UrbeCore.service('documents');return{P,D,disk};
}
const content=(D,p)=>{const d=D.list().find(x=>x.path===p);return d?d.content:null};
await test('mudanças feitas por fora entram no app (nova, editada, apagada)',async()=>{
  const {P,D,disk}=persistenceContext();await P.load('Urbe');
  disk.set('Nova.md','# Nova');disk.set('A.md','# A editada fora');disk.delete('Pasta/B.md');
  const n=await P.syncFromDisk();
  ok(n===3,'3 mudanças: '+n);ok(content(D,'Nova.md')==='# Nova'&&content(D,'A.md')==='# A editada fora'&&content(D,'Pasta/B.md')===null,'aplicadas');
  ok(await P.syncFromDisk()===0,'segunda leitura não muda nada');
  disk.set('Nova.md','# Nova 2');ok(await P.syncFromDisk(['Nova.md'])===1&&content(D,'Nova.md')==='# Nova 2','só os caminhos avisados');
});
await test('edição local ainda não gravada nunca é atropelada pelo disco',async()=>{
  const {P,D,disk}=persistenceContext();await P.load('Urbe');P.delay=60000;
  const a=D.list().find(x=>x.path==='A.md');D.upsert({...a,content:'# A minha edição'},{source:'editor.input'});
  clearTimeout(P.timer);P.timer=null;
  disk.set('A.md','# A de fora');disk.delete('Pasta/B.md');
  const b=D.list().find(x=>x.path==='Pasta/B.md');D.upsert({...b,content:'# B mexida aqui'},{source:'editor.input'});clearTimeout(P.timer);P.timer=null;
  await P.syncFromDisk();
  ok(content(D,'A.md')==='# A minha edição','local vence enquanto não grava');ok(content(D,'Pasta/B.md')==='# B mexida aqui','não apaga nota com edição local');
  await P.flush();ok(disk.get('A.md')==='# A minha edição','e depois grava a edição local');
});
await test('digitar numa nota grava só ela (sem copiar o vault inteiro no diário); várias de uma vez usam o diário',async()=>{
  const {P,D,disk}=persistenceContext();await P.load('Urbe');const writes=[];const w0=P.adapter.write;P.adapter.write=async(v,p,t)=>{writes.push(p);return w0(v,p,t)};
  const a=D.list().find(x=>x.path==='A.md');D.upsert({...a,content:'# A 1'},{source:'editor.input'});await P.flush();
  ok(!writes.includes('.urbe/journal.json')&&writes.includes('A.md'),'uma nota: '+writes.join());
  writes.length=0;const b=D.list().find(x=>x.path==='Pasta/B.md');D.upsert({...D.list().find(x=>x.path==='A.md'),content:'# A 2'});D.upsert({...b,content:'# B 2'});await P.flush();
  ok(writes[0]==='.urbe/journal.json'&&!disk.has('.urbe/journal.json'),'várias: diário antes e apagado depois: '+writes.join());
});
fs.rmSync(tmp,{recursive:true,force:true});
if(failed)console.error(failed+' falha(s)');
