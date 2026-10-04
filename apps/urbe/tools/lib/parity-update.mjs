// UC-2: fluxo Android real sobre host/rede injetados, sem consultar releases externas.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import vm from 'node:vm';
import {createRequire} from 'node:module';
import {ROOT} from './v2-docs.mjs';
import {fakeCapacitor,load} from '../../tests/lib/fake-capacitor.mjs';
import {makeElectron} from '../../tests/lib/fake-electron.mjs';
export function makeUpdateCases(){return JSON.parse(fs.readFileSync(path.join(ROOT,'docs/csharp/acceptance/update-cases.json'),'utf8')).cases}
export async function runUpdateCase(input){
  if(input.surface==='windows')return runWindows(input);
  if(input.surface!=='android'||!Array.isArray(input.responses)||!Array.isArray(input.steps))throw new Error('cenário de atualização inválido');
  const fake=fakeCapacitor(),original=fake.cap.nativePromise;
  fake.cap.nativePromise=(plugin,method,args)=>plugin==='UrbeAndroid'&&method==='getInfo'?Promise.resolve({version:input.currentVersion,sdk:34}):original(plugin,method,args);
  const requests=[],events=[],late=[],values=[];let cursor=0;
  const {W}=load(fake,async(url,options)=>{
    requests.push({url,cache:options.cache,accept:options.headers.Accept});
    const r=input.responses[cursor++];if(!r)throw new Error('resposta injetada ausente');
    if(r.reject)throw new Error(r.reject);
    return{ok:r.status===200,status:r.status,json:async()=>{if(r.jsonError)throw new Error(r.jsonError);return r.releases}};
  });
  const update=W.UrbeNative.update;
  update.onStatus(s=>events.push(JSON.parse(JSON.stringify(s))));
  for(const step of input.steps){
    switch(step){
      case 'check': values.push(JSON.parse(JSON.stringify(await update.check())));break;
      case 'install': await update.install();values.push(null);break;
      case 'subscribe': update.onStatus(s=>late.push(JSON.parse(JSON.stringify(s))));values.push(null);break;
      default: throw new Error('passo de atualização desconhecido');
    }
  }
  if(cursor!==input.responses.length)throw new Error('resposta injetada não consumida');
  return{values,events,late,requests,opened:fake.calls.filter(x=>x.startsWith('open ')).map(x=>x.slice(5))};
}
async function runWindows(input){
  if(!Array.isArray(input.responses)||!Array.isArray(input.steps))throw new Error('cenário de atualização inválido');
  const allowed=['checking-for-update','update-not-available','update-available','download-progress','update-downloaded','error'];
  for(const r of input.responses)for(const e of r.events||[])if(!allowed.includes(e.name))throw new Error('evento injetado desconhecido');
  const temp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-update-parity-'));
  try{
    const {electron,S}=makeElectron({tmp:temp,isPackaged:input.packaged!==false});
    const handlers=new Map(),events=[],late=[],values=[],installed=[],scheduled=[];let cursor=0;
    const updater={on(n,fn){handlers.set(n,fn)},async checkForUpdates(){
      const r=input.responses[cursor++];if(!r)throw new Error('resposta injetada ausente');
      if(r.reject)throw new Error(r.reject);
      for(const e of r.events||[]){const fn=handlers.get(e.name);if(!fn)throw new Error('evento injetado desconhecido');fn(e.data)}
    },quitAndInstall(...args){installed.push(args)}};
    const file=path.join(ROOT,'native/desktop/main.js'),realRequire=createRequire(file);
    const require=(name)=>name==='electron'?electron:name==='electron-updater'?{autoUpdater:updater}:name==='chokidar'?{watch:()=>({on(){return this},close(){}})}:realRequire(name);
    const context={require,__dirname:path.dirname(file),process:{env:{},platform:'win32'},console,Buffer,URL,Response,
      setTimeout(fn,ms){if(ms===250)queueMicrotask(fn);else scheduled.push({fn,ms});return{unref(){}}},
      clearTimeout(){},setInterval(fn,ms){scheduled.push({fn,ms});return{unref(){}}},clearInterval(){},setImmediate:fn=>queueMicrotask(fn)};
    vm.runInNewContext(fs.readFileSync(file,'utf8'),context,{filename:file});await S.readyP;
    // Mesmo preload; cada IPC atravessa o guard de origem do main real.
    S.rendererHandler=async(name,...args)=>S.handlers.get(name)({senderFrame:{url:'app://urbe/index.html',parent:null}},...args);
    const preload=path.join(ROOT,'native/desktop/preload.js');
    vm.runInNewContext(fs.readFileSync(preload,'utf8'),{require:n=>{if(n!=='electron')throw new Error('require inesperado');return electron},process:{platform:'win32'}},{filename:preload});
    const win=S.windows[0];win.webContents.send=(name,state)=>{for(const fn of S.rendererOn.get(name)||[])fn({},state)};
    const update=S.exposed.api.update;
    const copy=s=>JSON.parse(JSON.stringify(s));update.onStatus(s=>events.push(copy(s)));await Promise.resolve();await Promise.resolve();
    for(const step of input.steps){
      if(step==='check')values.push(copy(await update.check()));
      else if(step==='install'){await update.install();await Promise.resolve();values.push(null)}
      else if(step==='subscribe'){update.onStatus(s=>late.push(copy(s)));await Promise.resolve();await Promise.resolve();values.push(null)}
      else throw new Error('passo de atualização desconhecido');
    }
    if(cursor!==input.responses.length)throw new Error('resposta injetada não consumida');
    return{values,events,late,installed,checks:cursor,automatic:!!updater.autoDownload&&!!updater.allowPrerelease&&!!updater.autoInstallOnAppQuit};
  }finally{fs.rmSync(temp,{recursive:true,force:true})}
}
