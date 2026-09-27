// App Android: o adaptador da ponte sobre o Capacitor (Filesystem + UrbeAndroid), com um
// Capacitor simulado que responde como o plugin real (base64, "does not exist", "already exists").
import fs from 'node:fs';import vm from 'node:vm';
const read=f=>fs.readFileSync(new URL('../'+f,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}

function fakeCapacitor(){
  const files=new Map(),dirs=new Set(['Documents']),calls=[];
  const full=o=>{ok(o.directory==='DOCUMENTS','sempre na pasta Documentos: '+o.directory);return 'Documents/'+o.path};
  const parent=p=>p.split('/').slice(0,-1).join('/');
  const err=(m,p)=>Promise.reject(new Error("'"+m+"' failed because file at '"+p+"' does not exist."));
  const FS={
    stat(o){const p=full(o);if(dirs.has(p))return Promise.resolve({type:'directory',size:0,mtime:1});if(files.has(p))return Promise.resolve({type:'file',size:files.get(p).length,mtime:1700000000000});return err('stat',p)},
    readdir(o){const p=full(o);if(!dirs.has(p))return err('readdir',p);const out=[];
      for(const d of dirs)if(parent(d)===p)out.push({name:d.split('/').pop(),type:'directory'});for(const f of files.keys())if(parent(f)===p)out.push({name:f.split('/').pop(),type:'file'});return Promise.resolve({files:out})},
    readFile(o){const p=full(o);if(!files.has(p))return err('readFile',p);ok(!o.encoding,'lê em base64');return Promise.resolve({data:files.get(p).toString('base64')})},
    writeFile(o){const p=full(o);ok(o.recursive===true,'cria as pastas do caminho');let d=parent(p);while(d&&!dirs.has(d)){dirs.add(d);d=parent(d)}files.set(p,Buffer.from(o.data,'base64'));return Promise.resolve({uri:'file:///'+p})},
    mkdir(o){const p=full(o);if(dirs.has(p))return Promise.reject(new Error("Directory at '"+p+"' already exists, cannot be overwritten."));let d=p;while(d&&!dirs.has(d)){dirs.add(d);d=parent(d)}return Promise.resolve()},
    rmdir(o){const p=full(o);for(const f of [...files.keys()])if(f.startsWith(p+'/'))files.delete(f);for(const d of [...dirs])if(d===p||d.startsWith(p+'/'))dirs.delete(d);return Promise.resolve()},
    deleteFile(o){files.delete(full(o));return Promise.resolve()}
  };
  const UA={getInfo:()=>Promise.resolve({version:'1.7.0-beta',sdk:34}),openUrl:o=>{calls.push('open '+o.url);return Promise.resolve()},minimize:()=>{calls.push('min');return Promise.resolve()},
    saveFile:o=>{calls.push('save '+o.name+' '+Buffer.from(o.data,'base64').toString());return Promise.resolve({where:'Downloads/Urbe/'+o.name})},storageStatus:()=>Promise.resolve({sdk:34,needsAllFiles:true,allFiles:false})};
  const cap={isNativePlatform:()=>true,nativePromise:(pl,m,o)=>{const P=pl==='Filesystem'?FS:pl==='UrbeAndroid'?UA:null;if(!P||!P[m])return Promise.reject(new Error('sem '+pl+'.'+m));return P[m](o)}};
  return{cap,files,dirs,calls};
}
function load(fake,fetchImpl){
  const listeners={};
  const doc={documentElement:{classList:{add(){}}},addEventListener(t,f){(listeners[t]=listeners[t]||[]).push(f)},getElementById(){return null},querySelectorAll(){return[]},visibilityState:'visible'};
  function A(){}A.prototype.click=function(){};A.prototype.hasAttribute=function(){return false};
  const win={Capacitor:fake.cap,navigator:{},document:doc,open:()=>null};
  const c={window:win,document:doc,console,File,Blob,TextEncoder,TextDecoder,DOMException,Symbol,Promise,btoa,atob,fetch:fetchImpl||fetch,setTimeout,clearTimeout,HTMLAnchorElement:A,KeyboardEvent:class{},getComputedStyle:()=>({})};
  vm.createContext(c);vm.runInContext(read('src/native/bridge.js'),c);return{W:win,listeners};
}

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
if(failed)console.error(failed+' falha(s)');
