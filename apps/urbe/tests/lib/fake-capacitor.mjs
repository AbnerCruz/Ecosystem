// Capacitor simulado (responde como o plugin real: base64, "does not exist", "already exists") e o carregador
// da ponte src/native/bridge.js num contexto vm. Usado por tests/native-android.mjs e tests/native-contract.mjs.
import fs from 'node:fs';import vm from 'node:vm';
const read=f=>fs.readFileSync(new URL('../../'+f,import.meta.url),'utf8');
function ok(v,m){if(!v)throw new Error(m||'falhou')}
export function fakeCapacitor(opts){opts=opts||{};
  const files=new Map(),dirs=new Set(['Documents']),calls=[],saved=new Map(),printed=[];
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
  const UA={getInfo:()=>Promise.resolve({version:'1.7.0-beta',sdk:34}),openUrl:o=>{calls.push('open '+o.url);return Promise.resolve()},minimize:()=>{calls.push('min');return Promise.resolve()},printHtml:o=>{printed.push(o);return Promise.resolve({printing:true})},
    saveFile:o=>{calls.push('save '+o.name+' '+Buffer.from(o.data,'base64').toString());saved.set(o.name,Buffer.from(o.data,'base64'));return Promise.resolve({where:'Downloads/Urbe/'+o.name})},storageStatus:()=>Promise.resolve({sdk:34,needsAllFiles:true,allFiles:false})};
  if(opts.lote){
    UA.listTree=()=>{calls.push('listTree');const B='Documents/Urbe/',out=[];if(!dirs.has('Documents/Urbe'))return Promise.resolve({exists:false,entries:[]});
      for(const d of dirs)if(d.startsWith(B))out.push({path:d.slice(B.length),kind:'directory'});for(const [f,v] of files)if(f.startsWith(B))out.push({path:f.slice(B.length),kind:'file',size:v.length});return Promise.resolve({exists:true,entries:out})};
    UA.readTexts=o=>{calls.push('readTexts '+o.paths.length);const r={};for(const p of o.paths){const v=files.get('Documents/Urbe/'+p);if(v)r[p]=v.toString('utf8')}return Promise.resolve({files:r})};
  }
  const fsCalls=[];for(const k of Object.keys(FS)){const o=FS[k];FS[k]=a=>{fsCalls.push(k);return o(a)}}
  const cap={isNativePlatform:()=>true,nativePromise:(pl,m,o)=>{const P=pl==='Filesystem'?FS:pl==='UrbeAndroid'?UA:null;if(!P||!P[m])return Promise.reject(new Error('sem '+pl+'.'+m));return P[m](o)}};
  return{cap,files,dirs,calls,fsCalls,saved,printed};
}
export function load(fake,fetchImpl){
  const listeners={};
  const doc={documentElement:{classList:{add(){}}},addEventListener(t,f){(listeners[t]=listeners[t]||[]).push(f)},getElementById(){return null},querySelectorAll(){return[]},visibilityState:'visible'};
  function A(){}A.prototype.click=function(){};A.prototype.hasAttribute=function(){return false};
  const win={Capacitor:fake.cap,navigator:{},document:doc,open:()=>null};
  const c={window:win,document:doc,console,URL,File,Blob,TextEncoder,TextDecoder,DOMException,Symbol,Promise,btoa,atob,fetch:fetchImpl||fetch,setTimeout,clearTimeout,HTMLAnchorElement:A,KeyboardEvent:class{},getComputedStyle:()=>({})};
  vm.createContext(c);vm.runInContext(read('src/core/artifacts.js'),c);vm.runInContext(read('src/native/bridge.js'),c);return{W:win,listeners};
}
