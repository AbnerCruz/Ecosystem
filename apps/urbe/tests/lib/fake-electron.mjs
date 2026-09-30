// Simulação mínima do Electron para testar native/desktop/main.js e preload.js sem o Electron.
// Intercepta require('electron') (e electron-updater/chokidar) e registra tudo o que o código faz.
import Module from 'node:module';import path from 'node:path';import os from 'node:os';import fs from 'node:fs';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
export const ROOT=path.resolve(new URL('../..',import.meta.url).pathname);
export const APP_DIR=ROOT;

export function makeElectron(opts){
  opts=opts||{};
  const tmp=opts.tmp||fs.mkdtempSync(path.join(os.tmpdir(),'urbe-electron-'));
  const S={tmp,handlers:new Map(),windows:[],opened:[],openedPaths:[],saves:[],saveResults:[],pathsSet:{},privileged:null,protocols:new Map(),
    partitions:[],menus:[],lock:0,readyP:null,rendererOn:new Map(),exposed:null,requires:[],invoked:[],netFetched:[]};
  const paths={userData:path.join(tmp,'userData'),documents:path.join(tmp,'Documents'),...(opts.paths||{})};
  function mkSession(name){
    const s={name,protocol:{handle(sch,fn){S.protocols.set(name+'|'+sch,fn)},unhandle(sch){S.protocols.delete(name+'|'+sch)}},
      webRequest:{onBeforeRequest(fn){s.beforeRequest=fn}},
      setPermissionRequestHandler(fn){s.permReq=fn},setPermissionCheckHandler(fn){s.permCheck=fn}};
    return s;
  }
  const defaultSession=mkSession('default');
  class WebContents{
    constructor(){this.handlers={};this.sent=[];this.loaded=[]}
    on(ev,fn){(this.handlers[ev]=this.handlers[ev]||[]).push(fn)}
    once(ev,fn){this.on(ev,fn)}
    setWindowOpenHandler(fn){this.openHandler=fn}
    send(ch,...a){this.sent.push([ch,...a])}
    async executeJavaScript(src){this.js=(this.js||[]).concat(src);return true}
    async printToPDF(o){this.pdfOpts=o;return Buffer.from('%PDF-fake')}
    emit(ev,...a){const e={prevented:false,preventDefault(){this.prevented=true}};(this.handlers[ev]||[]).forEach(f=>f(e,...a));return e}
  }
  class BrowserWindow{
    constructor(o){this.opts=o;this.webContents=new WebContents();this.destroyed=false;this.handlers={};S.windows.push(this)}
    once(ev,fn){this.handlers[ev]=fn}
    show(){}
    isDestroyed(){return this.destroyed}
    destroy(){this.destroyed=true}
    async loadURL(u){this.loadedUrl=u;this.webContents.loaded.push(u)}
    isMinimized(){return false}
    focus(){}
    restore(){}
  }
  const electron={
    app:{isPackaged:!!opts.isPackaged,getPath:n=>paths[n]||path.join(tmp,n),setPath(n,p){S.pathsSet[n]=p;paths[n]=p},getVersion:()=>'0.0.0-test',
      requestSingleInstanceLock(){S.lock++;return true},on(){},quit(){S.quit=true},
      whenReady(){return{then(cb){S.readyP=Promise.resolve().then(cb);return S.readyP}}}},
    BrowserWindow,
    ipcMain:{handle(n,fn){S.handlers.set(n,fn)}},
    dialog:{async showSaveDialog(w,o){S.saves.push(o);return S.saveResults.length?S.saveResults.shift():{canceled:true}},async showOpenDialog(){return{canceled:true,filePaths:[]}}},
    shell:{openExternal(u){S.opened.push(u)},openPath(p){S.openedPaths.push(p);return Promise.resolve('')}},
    protocol:{registerSchemesAsPrivileged(l){S.privileged=l},handle(sch,fn){S.protocols.set('default|'+sch,fn)}},
    net:{async fetch(u){S.netFetched.push(u);return new Response('ok',{status:200})}},
    session:{defaultSession,fromPartition(n){S.partitions.push(n);const s=mkSession(n);(S.sessions=S.sessions||{})[n]=s;return s}},
    Menu:{setApplicationMenu(m){S.menus.push(m)}},
    contextBridge:{exposeInMainWorld(n,o){S.exposed={name:n,api:o}}},
    ipcRenderer:{on(ch,fn){(S.rendererOn.get(ch)||S.rendererOn.set(ch,[]).get(ch)).push(fn)},
      invoke(ch,...a){S.invoked.push([ch,...a]);const h=S.rendererHandler;return h?h(ch,...a):Promise.resolve(undefined)}}
  };
  S.defaultSession=defaultSession;
  return{electron,S};
}

/* Carrega um arquivo CommonJS de native/desktop com o 'electron' simulado. */
export function loadDesktop(file,electron,S){
  const target=path.join(ROOT,'native','desktop',file);
  for(const k of Object.keys(require.cache))if(k.startsWith(path.join(ROOT,'native','desktop')))delete require.cache[k];
  const orig=Module._load;
  const fakeUpdater={autoUpdater:{on(){},checkForUpdates:async()=>{},quitAndInstall(){}}};
  const fakeChokidar={watch(){return{on(){return this},close(){}}}};
  Module._load=function(req,parent,isMain){
    if(parent&&parent.filename&&parent.filename.startsWith(path.join(ROOT,'native','desktop'))){if(S)S.requires.push(req)}
    if(req==='electron')return electron;
    if(req==='electron-updater')return fakeUpdater;
    if(req==='chokidar')return fakeChokidar;
    return orig.apply(this,arguments);
  };
  try{return require(target)}finally{Module._load=orig}
}

/* Sobe o main.js com o Electron simulado e espera o app ficar pronto. */
export async function bootMain(opts,env){
  const {electron,S}=makeElectron(opts);
  const saved={};
  for(const k of Object.keys(env||{})){saved[k]=process.env[k];if(env[k]==null)delete process.env[k];else process.env[k]=env[k]}
  const warn=console.warn;console.warn=()=>{};
  try{loadDesktop('main.js',electron,S);if(S.readyP)await S.readyP}
  finally{console.warn=warn;for(const k of Object.keys(saved)){if(saved[k]==null)delete process.env[k];else process.env[k]=saved[k]}}
  const call=async(ch,frameUrl,...a)=>{const h=S.handlers.get(ch);if(!h)throw new Error('sem handler '+ch);
    return h({senderFrame:frameUrl===null?null:{url:frameUrl===undefined?'app://urbe/index.html':frameUrl,parent:null}},...a)};
  return{electron,S,call,win:S.windows[0]};
}
