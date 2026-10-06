// UC-2: cliente de referência do contrato nativo. Hosts/IPC simulados; filesystem desktop real.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import vm from 'node:vm';
import {ROOT} from './v2-docs.mjs';
import {bootMain,makeElectron,loadDesktop} from '../../tests/lib/fake-electron.mjs';
import {fakeCapacitor,load as loadAndroid} from '../../tests/lib/fake-capacitor.mjs';
const universe=['fs','vault','openExternal','saveFile','print','update','back','storageStatus','importSelection'];
const methods={fs:['stat','list','readBytes','writeBytes','mkdir','remove'],update:['check','install','onStatus']};
export function makeNativeCases(){return JSON.parse(fs.readFileSync(path.join(ROOT,'docs/csharp/acceptance/native-cases.json'),'utf8')).cases}
function bridge(N){
  const document={documentElement:{classList:{add(){}}},addEventListener(){},getElementById(){return null},querySelectorAll(){return[]}};
  const window={document,navigator:{}};if(N)window.UrbeNative=N;
  function HTMLAnchorElement(){}
  HTMLAnchorElement.prototype.click=function(){};
  HTMLAnchorElement.prototype.hasAttribute=function(){return false};
  const c={window,document,console,HTMLAnchorElement,URL,File,Blob,TextEncoder,TextDecoder,DOMException,ArrayBuffer,Uint8Array,Symbol,Promise,fetch,setTimeout,clearTimeout};
  vm.createContext(c);
  for(const p of ['src/core/artifacts.js','src/native/bridge.js'])vm.runInContext(fs.readFileSync(path.join(ROOT,p),'utf8'),c,{filename:p});
  return window;
}
async function createHost(input){
  if(input.surface==='web')return{W:bridge(),cleanup(){}};
  if(input.surface==='android'){
    const f=fakeCapacitor({lote:true}),original=f.cap.nativePromise;
    f.cap.nativePromise=(plugin,method,args)=>{
      if(input.deny==='write'&&plugin==='Filesystem'&&method==='writeFile')return Promise.reject(new Error('permission denied'));
      if(input.deny==='save'&&plugin==='UrbeAndroid'&&method==='saveFile')return Promise.reject(new Error('permission denied'));
      return original(plugin,method,args);
    };
    // A resposta HTTP é injetada; nunca consulta rede nem release corrente.
    const {W,listeners}=loadAndroid(f,async()=>({ok:false,status:403}));
    await W.UrbeNative.vault();
    return{W,f,listeners,opened:()=>f.calls.filter(x=>x.startsWith('open ')).map(x=>x.slice(5)),cleanup(){}};
  }
  if(input.surface==='windows'){
    const temp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-native-parity-')),vault=path.join(temp,'vault');
    try{
      const m=await bootMain({tmp:temp},{URBE_TEST_MODE:'1',URBE_TEST_USERDATA:path.join(temp,'ud'),URBE_TEST_VAULT:vault});
      const {electron,S}=makeElectron({tmp:temp});
      S.rendererHandler=(channel,...args)=>{
        if(input.deny==='write'&&channel==='fs:writeBytes')return Promise.reject(new Error('permission denied'));
        if(input.deny==='save'&&channel==='fs:saveFile')return Promise.reject(new Error('permission denied'));
        return m.call(channel,undefined,...args);
      };
      loadDesktop('preload.js',electron,S);
      return{W:bridge(S.exposed.api),m,S,temp,vault,opened:()=>m.S.opened,cleanup(){fs.rmSync(temp,{recursive:true,force:true})}};
    }catch(e){fs.rmSync(temp,{recursive:true,force:true});throw e}
  }
  throw new Error('superfície desconhecida');
}
function availability(N){return universe.filter(cap=>{
  if(cap==='fs'||cap==='update')return methods[cap].every(k=>typeof N?.[cap]?.[k]==='function');
  if(cap==='importSelection')return ['pick','readChunk','release'].every(k=>typeof N?.importSelection?.[k]==='function');
  if(cap==='storageStatus')return typeof N?.storage?.status==='function';
  return typeof N?.[{print:'printHtml',back:'minimize'}[cap]||cap]==='function';
})}
export async function runNativeCase(input){
  if(!Array.isArray(input.steps))throw new Error('passos nativos inválidos');
  const h=await createHost(input),N=h.W.UrbeNative,values=[];
  try{
    for(const s of input.steps){
      let value;
      // A captura de rejeição só vale para operações explicitamente marcadas. Erros de tooling não viram verde.
      if(!['contract','vault','write','read','stat','list','mkdir','remove','open','save','print','storage','back','update'].includes(s.method)||!Array.isArray(s.args))throw new Error('operação nativa desconhecida');
      try{
        const a=s.args;
        switch(s.method){
          case 'contract': value={...h.W.UrbeNativeContract,api:availability(N)};break;
          case 'vault': {const v=await N.vault();value={labelPresent:typeof v.label==='string'&&!!v.label,pathPresent:typeof v.path==='string'&&!!v.path};break}
          case 'write': await N.fs.writeBytes(a[0],new Uint8Array(a[1]));value=null;break;
          case 'read': value=Array.from(await N.fs.readBytes(a[0]));break;
          case 'stat': {const st=await N.fs.stat(a[0]);value=st?{kind:st.kind}:null;break}
          case 'list': value=(await N.fs.list(a[0])).map(e=>({name:e.name,kind:e.kind})).sort((x,y)=>x.name<y.name?-1:x.name>y.name?1:0);break;
          case 'mkdir': await N.fs.mkdir(a[0]);value=null;break;
          case 'remove': await N.fs.remove(...a);value=null;break;
          case 'open': {
            const before=h.opened().length;try{await N.openExternal(a[0])}catch(e){if(!s.blocked)throw e}
            value={opened:h.opened().slice(before)};break;
          }
          case 'save': {
            const bytes=new Uint8Array(a[1]),dest=h.temp&&path.join(h.temp,'export.bin');
            if(h.m)h.m.S.saveResults.push(s.cancel?{canceled:true}:{canceled:false,filePath:dest});
            const r=await N.saveFile(a[0],bytes,a[2]);
            const saved=h.m?(fs.existsSync(dest)?Array.from(fs.readFileSync(dest)):null):(h.f.saved.has(a[0])?Array.from(h.f.saved.get(a[0])):null);
            value={canceled:r.canceled===true,wherePresent:typeof r.where==='string'&&!!r.where,bytes:saved};break;
          }
          case 'print': {
            const dest=h.temp&&path.join(h.temp,'print.pdf');
            if(h.m)h.m.S.saveResults.push(s.cancel?{canceled:true}:{canceled:false,filePath:dest});
            const r=await N.printHtml(...a);
            if(h.m){const w=h.m.S.windows.at(-1);value={canceled:r.canceled===true,savedPresent:!!r.saved,pdfWritten:fs.existsSync(dest),isolated:w.opts.webPreferences.nodeIntegration===false&&w.opts.webPreferences.sandbox===true&&!w.opts.webPreferences.preload};}
            else value={printing:r.printing===true,submitted:h.f.printed.map(p=>({html:p.html,name:p.name}))};break;
          }
          case 'storage': value=await N.storage.status();break;
          case 'back': {await N.minimize();value={minimized:h.f.calls.includes('min'),listenerPresent:(h.listeners.urbeBack||[]).length===1};break}
          case 'update': {const r=await N.update.check();value={state:r.state};break}
        }
      }catch(e){if(!s.expectError)throw e;value={rejected:true}}
      if(s.expectError&&value?.rejected!==true)throw new Error('operação deveria ser recusada');
      values.push(value);
    }
    return{values};
  }finally{h.cleanup()}
}
