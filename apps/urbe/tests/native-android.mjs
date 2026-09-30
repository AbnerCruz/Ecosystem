// App Android: o adaptador da ponte sobre o Capacitor (Filesystem + UrbeAndroid), com um
// Capacitor simulado que responde como o plugin real (base64, "does not exist", "already exists").
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}

import {fakeCapacitor,load} from './lib/fake-capacitor.mjs';

await test('Android: UrbeNative montado sobre o Capacitor, vault em Documentos/Urbe',async()=>{
  const f=fakeCapacitor(),{W}=load(f);
  ok(W.UrbeNative&&W.UrbeNative.shell==='capacitor'&&W.UrbeNative.platform==='android','ponte ativa');
  const v=await W.UrbeNative.vault();ok(v.label==='Documentos/Urbe'&&f.dirs.has('Documents/Urbe'),'cria a pasta: '+JSON.stringify(v));
  ok(typeof W.showDirectoryPicker!=='function','pasta fixa no Android');
});
await test('Android: notas e binários viram arquivos de verdade (texto, acentos, bytes)',async()=>{
  const f=fakeCapacitor(),{W}=load(f),root=await W.UrbeNativeFS.root(),v=await root.getDirectoryHandle('Urbe');
  const fh=await (await v.getDirectoryHandle('Diário',{create:true})).getFileHandle('Hoje.md',{create:true}),w=await fh.createWritable();await w.write('# Hoje\n\nçãõ 🙂');await w.close();
  ok(f.files.get('Documents/Urbe/Diário/Hoje.md').toString()==='# Hoje\n\nçãõ 🙂','utf-8 intacto');
  ok(await (await fh.getFile()).text()==='# Hoje\n\nçãõ 🙂','lê de volta');
  const big=new Uint8Array(300000).map((_,i)=>i%256),bh=await v.getFileHandle('foto.png',{create:true}),bw=await bh.createWritable();await bw.write(big);await bw.close();
  ok(Buffer.compare(f.files.get('Documents/Urbe/foto.png'),Buffer.from(big))===0,'binário grande intacto');
  const names=[];for await(const [n,h] of v.entries())names.push(n+':'+h.kind);ok(names.sort().join()==='Diário:directory,foto.png:file',names.join());
  let e=null;try{await v.getFileHandle('nada.md')}catch(x){e=x}ok(e&&e.name==='NotFoundError','não existe → NotFoundError');
  await v.getDirectoryHandle('Diário',{create:true});ok(true,'criar pasta que já existe não falha');
  await v.removeEntry('Diário',{recursive:true});ok(!f.files.has('Documents/Urbe/Diário/Hoje.md'),'apaga pasta');
});
await test('Android: links abrem no navegador, downloads vão para Downloads/Urbe, voltar minimiza na cidade',async()=>{
  const f=fakeCapacitor(),{W,listeners}=load(f);
  await W.UrbeNative.openExternal('https://urbe.app');ok(f.calls.includes('open https://urbe.app'),'openUrl');
  const r=await W.UrbeNative.saveFile('livro.html',new TextEncoder().encode('<h1>oi</h1>'),'text/html');ok(r.where==='Downloads/Urbe/livro.html'&&f.calls.some(c=>c==='save livro.html <h1>oi</h1>'),'saveFile');
  ok((listeners.urbeBack||[]).length===1,'escuta o voltar do Android');
});
await test('Android: atualização compara com o último lançamento e oferece o APK',async()=>{
  const rel=tag=>async()=>({ok:true,json:async()=>({tag_name:tag,body:'notas',assets:[{name:'Urbe-Setup.exe',browser_download_url:'x'},{name:'Urbe-'+tag.slice(1)+'.apk',browser_download_url:'https://github.com/AbnerCruz/Urbe/releases/download/'+tag+'/Urbe.apk'}]})});
  let f=fakeCapacitor(),{W}=load(f,rel('v1.7.1-beta'));const seen=[];W.UrbeNative.update.onStatus(s=>seen.push(s.state));
  let s=await W.UrbeNative.update.check();ok(s.state==='available'&&s.manualInstall&&s.version==='1.7.1-beta'&&/\.apk$/.test(s.url),JSON.stringify(s));
  await W.UrbeNative.update.install();ok(f.calls.some(c=>c.startsWith('open https://github.com/')),'abre o download do APK');
  ok(seen.join()==='checking,available','avisa o app');
  ({W}=load(fakeCapacitor(),rel('v1.7.0-beta')));s=await W.UrbeNative.update.check();ok(s.state==='none','mesma versão');
  ({W}=load(fakeCapacitor(),async()=>({ok:false,status:403})));s=await W.UrbeNative.update.check();ok(s.state==='error','sem internet/limite: erro, sem travar');
  const n=W.UrbeNative.update._newer;
  ok(n('1.7.0','1.7.0-beta')&&n('1.7.0-beta.2','1.7.0-beta.1')&&n('1.10.0-beta','1.9.9')&&!n('1.6.9','1.7.0-beta')&&!n('1.7.0-beta','1.7.0-beta'),'ordem das versões');
});
await test('Android: abrir lê a pasta em 2 chamadas (espelho); gravar mantém o espelho; voltar ao app recarrega',async()=>{
  const f=fakeCapacitor({lote:true});
  f.dirs.add('Documents/Urbe');f.dirs.add('Documents/Urbe/Tutorial');
  for(let i=0;i<30;i++)f.files.set('Documents/Urbe/Tutorial/Nota '+i+'.md',Buffer.from('# Nota '+i+' ção'));
  f.files.set('Documents/Urbe/foto.png',Buffer.from([137,80,78,71]));
  const {W}=load(f),v=await (await W.UrbeNativeFS.root()).getDirectoryHandle('Urbe');
  ok(W.UrbeNativeFS.mirrored(),'espelho ativo');ok(f.calls.includes('listTree')&&f.calls.includes('readTexts 30'),'2 chamadas: '+f.calls.join());
  f.fsCalls.length=0;
  const t=await v.getDirectoryHandle('Tutorial'),names=[];for await(const [n] of t.entries())names.push(n);
  let texto='';for(const n of names)texto+=await (await (await t.getFileHandle(n)).getFile()).text();
  ok(names.length===30&&texto.includes('# Nota 29 ção'),'leu tudo');ok(f.fsCalls.length===0,'sem tocar o disco: '+f.fsCalls.join());
  const w=await (await t.getFileHandle('Nova.md',{create:true})).createWritable();await w.write('# Nova');await w.close();
  ok(f.files.get('Documents/Urbe/Tutorial/Nova.md').toString()==='# Nova','gravou no disco');
  f.fsCalls.length=0;ok(await (await (await t.getFileHandle('Nova.md')).getFile()).text()==='# Nova'&&f.fsCalls.length===0,'espelho atualizado pela gravação');
  await t.removeEntry('Nota 0.md');let e=null;try{await t.getFileHandle('Nota 0.md')}catch(x){e=x}ok(e&&e.name==='NotFoundError'&&!f.files.has('Documents/Urbe/Tutorial/Nota 0.md'),'apagar reflete no espelho');
  f.files.set('Documents/Urbe/Tutorial/De fora.md',Buffer.from('# PC'));await W.UrbeNativeFS.prefetch();
  ok(await (await (await t.getFileHandle('De fora.md')).getFile()).text()==='# PC','recarregar enxerga o que veio de fora');
  const png=await (await v.getFileHandle('foto.png')).getFile();ok((await png.arrayBuffer()).byteLength===4,'binário lido do disco');
});
await test('Android: sem a leitura em lote (app antigo), continua funcionando arquivo a arquivo',async()=>{
  const f=fakeCapacitor(),{W}=load(f);const v=await (await W.UrbeNativeFS.root()).getDirectoryHandle('Urbe');
  ok(!W.UrbeNativeFS.mirrored(),'sem espelho');const w=await (await v.getFileHandle('a.md',{create:true})).createWritable();await w.write('oi');await w.close();
  ok(await (await (await v.getFileHandle('a.md')).getFile()).text()==='oi','lê e grava');
});
if(failed)console.error(failed+' falha(s)');
