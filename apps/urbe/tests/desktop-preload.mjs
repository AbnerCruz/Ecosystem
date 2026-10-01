// Electron preload.js: forma do objeto UrbeNative exposto, sem vazar ipcRenderer/Node, e o mapeamento
// de cada método para o canal IPC certo. O Electron é simulado (tests/lib/fake-electron.mjs).
import {makeElectron,loadDesktop} from './lib/fake-electron.mjs';
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}

function boot(platform){
  const {electron,S}=makeElectron();
  const p=Object.getOwnPropertyDescriptor(process,'platform');
  if(platform)Object.defineProperty(process,'platform',{value:platform});
  try{loadDesktop('preload.js',electron,S)}finally{Object.defineProperty(process,'platform',p)}
  S.rendererHandler=async(ch)=>ch==='update:last'?S.lastUpdate:({ch});
  return{S,api:S.exposed&&S.exposed.api,ipc:electron.ipcRenderer};
}

await test('expõe só window.UrbeNative, com a forma do contrato',()=>{
  const {S,api}=boot();
  ok(S.exposed.name==='UrbeNative','nome');
  ok(S.requires.length===1&&S.requires[0]==='electron','preload só requer "electron" (sandbox): '+S.requires);
  const keys=Object.keys(api).sort();
  ok(keys.join()==='contract,fs,info,onVaultChanged,openExternal,pickVault,platform,printHtml,revealVault,saveFile,shell,update,vault',keys.join());
  ok(Object.keys(api.fs).sort().join()==='list,mkdir,readBytes,readTexts,remove,stat,tree,writeBytes','fs: '+Object.keys(api.fs));
  ok(Object.keys(api.update).sort().join()==='check,install,onStatus','update: '+Object.keys(api.update));
  ok(api.shell==='electron'&&['windows','mac','linux'].includes(api.platform),'shell/platform');
  for(const k of ['info','vault','pickVault','revealVault','onVaultChanged','openExternal','printHtml','saveFile'])ok(typeof api[k]==='function',k);
});
await test('plataforma mapeada (win32→windows, darwin→mac, resto→linux)',()=>{
  ok(boot('win32').api.platform==='windows'&&boot('darwin').api.platform==='mac'&&boot('linux').api.platform==='linux'&&boot('freebsd').api.platform==='linux','mapa');
});
await test('contrato: versão 1, capacidades declaradas e ausentes explícitas',()=>{
  const c=boot().api.contract;
  ok(c.version===1,'versão');
  ok(c.capabilities.join()==='fs,vault,openExternal,saveFile,print,update','capabilities: '+c.capabilities);
  ok(c.unsupported.join()==='back,storageStatus','unsupported: '+c.unsupported);
});
await test('não vaza ipcRenderer, require, process nem Node (varredura profunda)',()=>{
  const {api,ipc}=boot(),seen=new Set(),bad=[];
  (function walk(o,p,d){
    if(d>6||o==null)return;
    if(o===ipc)bad.push(p+' é o ipcRenderer');
    if(typeof o!=='object'&&typeof o!=='function')return;
    if(seen.has(o))return;seen.add(o);
    for(const k of Reflect.ownKeys(o)){
      if(/^(ipcRenderer|require|process|module|electron|remote|webFrame)$/.test(String(k)))bad.push(p+'.'+String(k));
      let v;try{v=o[k]}catch(_){continue}walk(v,p+'.'+String(k),d+1);
    }
  })(api,'UrbeNative',0);
  ok(!bad.length,bad.join(', '));
  for(const k of ['send','invoke','on','sendSync','postMessage','removeListener'])ok(!(k in api),'não expõe '+k);
  // nenhuma função do objeto devolve o ipcRenderer
  const r=api.onVaultChanged(()=>{});ok(r===undefined,'onVaultChanged não devolve nada');
  ok(api.update.onStatus(()=>{})===undefined,'onStatus não devolve nada');
});
await test('cada método chama o canal IPC certo, com argumentos coagidos',async()=>{
  const {S,api}=boot(),last=()=>S.invoked[S.invoked.length-1];
  await api.info();ok(last().join()==='app:info','info');
  await api.vault();ok(last().join()==='vault:get','vault');
  await api.pickVault();ok(last().join()==='vault:pick','pickVault');
  await api.revealVault();ok(last().join()==='vault:reveal','revealVault');
  await api.fs.stat('a');ok(last().join()==='fs:stat,a','stat');
  await api.fs.list('a');ok(last().join()==='fs:list,a','list');
  await api.fs.readBytes('a');ok(last().join()==='fs:readBytes,a','readBytes');
  const b=new Uint8Array([1,2]);await api.fs.writeBytes('a',b);ok(last()[0]==='fs:writeBytes'&&last()[1]==='a'&&last()[2]===b,'writeBytes');
  await api.fs.mkdir('a');ok(last().join()==='fs:mkdir,a','mkdir');
  await api.fs.tree();ok(last().join()==='fs:tree','tree');
  await api.fs.readTexts(['a','b']);ok(last()[0]==='fs:readTexts'&&last()[1].join()==='a,b','readTexts');
  await api.fs.remove('a');ok(last().join()==='fs:remove,a,false','remove sem recursivo = false');
  await api.fs.remove('a','sim');ok(last().join()==='fs:remove,a,true','remove recursivo coagido para booleano');
  await api.openExternal(123);ok(last().join()==='shell:open,123'&&typeof last()[1]==='string','openExternal coage para string');
  await api.printHtml(42);ok(last().join()==='print:html,42,Urbe','printHtml: nome padrão');
  await api.printHtml('<p>',null);ok(last().join()==='print:html,<p>,Urbe','nome nulo');
  await api.printHtml('<p>','Livro');ok(last().join()==='print:html,<p>,Livro','nome');
  await api.saveFile('livro.html',b,'text/html');ok(last()[0]==='fs:saveFile'&&last()[1]==='livro.html'&&last()[2]===b&&last()[3]==='text/html','saveFile');
  await api.saveFile(undefined,b);ok(last()[1]==='arquivo'&&last()[3]==='','saveFile: padrões');
  await api.update.check();ok(last().join()==='update:check','update.check');
  await api.update.install();ok(last().join()==='update:install','update.install');
});
await test('eventos: vault:changed e update:status chegam aos ouvintes; ouvinte que falha não derruba os outros',async()=>{
  const {S,api}=boot(),got=[],st=[];
  api.onVaultChanged(p=>got.push(p));api.onVaultChanged(()=>{throw new Error('x')});api.onVaultChanged(p=>got.push('2:'+p));api.onVaultChanged('não é função');api.onVaultChanged(null);
  S.rendererOn.get('vault:changed').forEach(f=>f({},['a.md']));
  ok(got.length===2&&got[0].join()==='a.md'&&got[1]==='2:a.md','entregou aos ouvintes válidos: '+JSON.stringify(got));
  S.lastUpdate={state:'ready',version:'9'};
  api.update.onStatus(s=>st.push(s.state));api.update.onStatus(()=>{throw new Error('x')});api.update.onStatus(7);
  await new Promise(r=>setTimeout(r,5));
  ok(st.join()==='ready','estado atual entregue ao registrar: '+st);
  S.rendererOn.get('update:status').forEach(f=>f({},{state:'downloading'}));
  ok(st.join()==='ready,downloading','novos estados');
  S.lastUpdate={state:'none'};const st2=[];api.update.onStatus(s=>st2.push(s.state));await new Promise(r=>setTimeout(r,5));ok(!st2.length,'"none" não é entregue no registro');
});
if(failed)console.error(failed+' falha(s)');
process.exit(process.exitCode||0);
