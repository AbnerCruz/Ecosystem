'use strict';
/* Urbe para computador (Windows). Abre o mesmo app do site, mas:
   - as notas ficam numa pasta de verdade (por padrão Documentos\Urbe), escolhida pela pessoa;
   - atualiza sozinho pelos lançamentos do GitHub (baixa em segundo plano e pede para reiniciar);
   - "Imprimir ou salvar PDF" gera o PDF direto, respeitando o tamanho de página do livro. */
const {app,BrowserWindow,ipcMain,dialog,shell,protocol,net,session,Menu}=require('electron');
const path=require('path');
const os=require('os');
const fsp=require('fs/promises');
const {pathToFileURL}=require('url');
const {createVaultFS}=require('./vault-fs');

const APP_DIR=path.resolve(__dirname,'..','..');
const ORIGIN='app://urbe';
const isTest=!!process.env.URBE_TEST_USERDATA;
if(isTest)app.setPath('userData',process.env.URBE_TEST_USERDATA);

protocol.registerSchemesAsPrivileged([{scheme:'app',privileges:{standard:true,secure:true,supportFetchAPI:true,corsEnabled:true,stream:true,codeCache:true}}]);

/* ---------------- configuração (pasta do vault) ---------------- */
const CONFIG=()=>path.join(app.getPath('userData'),'config.json');
let config={};
async function loadConfig(){try{config=JSON.parse(await fsp.readFile(CONFIG(),'utf8'))||{}}catch(_){config={}}
  if(!config.vault)config.vault=process.env.URBE_TEST_VAULT||path.join(app.getPath('documents'),'Urbe');
  await fsp.mkdir(config.vault,{recursive:true})}
async function saveConfig(){await fsp.mkdir(path.dirname(CONFIG()),{recursive:true});await fsp.writeFile(CONFIG(),JSON.stringify(config,null,2))}
const vault=createVaultFS(()=>config.vault);

/* ---------------- vigia da pasta: mudanças feitas por fora aparecem no app aberto ---------------- */
let watcher=null,pendingPaths=new Set(),watchTimer=null;
function watchVault(){
  try{if(watcher)watcher.close()}catch(_){}watcher=null;
  let chokidar;try{chokidar=require('chokidar')}catch(e){console.warn('vigia da pasta',e);return}
  const root=config.vault;
  watcher=chokidar.watch(root,{ignoreInitial:true,persistent:true,
    ignored:(p)=>{const rel=path.relative(root,p);return !!rel&&(rel.split(path.sep).some(x=>x.startsWith('.'))||/\.urbe-tmp-/.test(rel)||rel.split(path.sep).includes('node_modules'))},
    awaitWriteFinish:{stabilityThreshold:250,pollInterval:100}});
  const on=(_ev,p)=>{const rel=path.relative(root,p).split(path.sep).join('/');if(!rel)return;pendingPaths.add(rel);clearTimeout(watchTimer);
    watchTimer=setTimeout(()=>{const list=[...pendingPaths];pendingPaths.clear();if(win&&!win.isDestroyed())win.webContents.send('vault:changed',list)},400)};
  watcher.on('all',on);watcher.on('error',e=>console.warn('vigia da pasta',e&&e.message));
}

/* ---------------- janela ---------------- */
let win=null;
function external(url){try{const u=new URL(url);if(['http:','https:','mailto:'].includes(u.protocol)){shell.openExternal(u.toString());return true}}catch(_){}return false}
function createWindow(){
  win=new BrowserWindow({width:1320,height:860,minWidth:360,minHeight:480,show:false,backgroundColor:'#0b0e14',autoHideMenuBar:true,title:'Urbe',
    icon:path.join(APP_DIR,'icon-512.png'),
    webPreferences:{preload:path.join(__dirname,'preload.js'),contextIsolation:true,sandbox:true,nodeIntegration:false,spellcheck:true}});
  win.once('ready-to-show',()=>win.show());
  win.webContents.setWindowOpenHandler(({url})=>{
    if(external(url))return{action:'deny'};
    /* conteúdo do próprio app (anexos, prévia de página): janela simples, sem acesso ao sistema */
    if(/^(blob:app:\/\/urbe|app:\/\/urbe)/.test(url))return{action:'allow',overrideBrowserWindowOptions:{autoHideMenuBar:true,backgroundColor:'#ffffff',webPreferences:{sandbox:true,contextIsolation:true}}};
    return{action:'deny'};
  });
  win.webContents.on('will-navigate',(e,url)=>{if(!url.startsWith(ORIGIN)){e.preventDefault();external(url)}});
  win.loadURL(ORIGIN+'/index.html');
}

/* ---------------- arquivos do app (app://urbe/…) ---------------- */
function serveApp(){
  protocol.handle('app',async req=>{
    const u=new URL(req.url);let p=decodeURIComponent(u.pathname||'/');if(p==='/'||!p)p='/index.html';
    const file=path.resolve(APP_DIR,'.'+p),rel=path.relative(APP_DIR,file);
    if(rel.startsWith('..')||path.isAbsolute(rel))return new Response('Não encontrado',{status:404});
    try{return await net.fetch(pathToFileURL(file).toString())}catch(_){return new Response('Não encontrado',{status:404})}
  });
}

/* ---------------- ponte com a página ---------------- */
function ipc(){
  const h=(name,fn)=>ipcMain.handle(name,(e,...a)=>{if(!e.senderFrame||!String(e.senderFrame.url).startsWith(ORIGIN))throw new Error('Origem não autorizada');return fn(...a)});
  h('fs:stat',rel=>vault.stat(rel));
  h('fs:list',rel=>vault.list(rel));
  h('fs:readBytes',rel=>vault.readBytes(rel));
  h('fs:writeBytes',(rel,bytes)=>vault.writeBytes(rel,bytes));
  h('fs:mkdir',rel=>vault.mkdir(rel));
  h('fs:tree',()=>vault.tree());
  h('fs:readTexts',paths=>vault.readTexts(Array.isArray(paths)?paths.map(String):[]));
  h('fs:remove',(rel,rec)=>vault.remove(rel,rec));
  h('vault:get',()=>({label:config.vault,path:config.vault}));
  h('vault:pick',async()=>{
    const r=await dialog.showOpenDialog(win,{title:'Pasta do Urbe',buttonLabel:'Usar esta pasta',defaultPath:config.vault,properties:['openDirectory','createDirectory','promptToCreate']});
    if(r.canceled||!r.filePaths[0])return null;
    config.vault=r.filePaths[0];await fsp.mkdir(config.vault,{recursive:true});await saveConfig();watchVault();return{label:config.vault,path:config.vault};
  });
  h('vault:reveal',()=>shell.openPath(config.vault));
  h('shell:open',url=>{external(String(url))});
  h('app:info',()=>({version:app.getVersion(),platform:process.platform,vault:config.vault}));
  h('print:html',(html,name)=>printToPdf(String(html),String(name||'Urbe')));
  h('update:check',()=>checkUpdates(true));
  h('update:install',()=>installUpdate());
  h('update:last',()=>lastStatus);
}

/* ---------------- PDF ---------------- */
async function printToPdf(html,name){
  const tmp=path.join(os.tmpdir(),'urbe-print-'+Date.now()+'.html');
  await fsp.writeFile(tmp,html,'utf8');
  const w=new BrowserWindow({show:false,webPreferences:{sandbox:true,contextIsolation:true,javascript:true}});
  try{
    await w.loadFile(tmp);
    await w.webContents.executeJavaScript('(document.fonts&&document.fonts.ready||Promise.resolve()).then(()=>new Promise(r=>setTimeout(r,300)))');
    const pdf=await w.webContents.printToPDF({printBackground:true,preferCSSPageSize:true});
    const safe=name.replace(/[\\/:*?"<>|]+/g,'-').slice(0,90)||'Urbe';
    const r=await dialog.showSaveDialog(win,{title:'Salvar PDF',defaultPath:path.join(app.getPath('documents'),safe+'.pdf'),filters:[{name:'PDF',extensions:['pdf']}]});
    if(r.canceled||!r.filePath)return{canceled:true};
    await fsp.writeFile(r.filePath,pdf);shell.openPath(r.filePath);return{saved:r.filePath};
  }finally{w.destroy();fsp.rm(tmp,{force:true}).catch(()=>{})}
}

/* ---------------- atualização automática ---------------- */
let updater=null,lastStatus={state:'none'};
function status(s){lastStatus=s;if(win&&!win.isDestroyed())win.webContents.send('update:status',s)}
function setupUpdater(){
  if(!app.isPackaged||isTest)return;
  try{updater=require('electron-updater').autoUpdater}catch(e){console.warn('updater',e);return}
  updater.autoDownload=true;updater.allowPrerelease=true;updater.autoInstallOnAppQuit=true;
  updater.on('checking-for-update',()=>status({state:'checking'}));
  updater.on('update-not-available',i=>status({state:'none',version:i&&i.version}));
  updater.on('update-available',i=>status({state:'downloading',version:i.version,percent:0}));
  updater.on('download-progress',p=>status({state:'downloading',version:lastStatus.version,percent:p.percent}));
  updater.on('update-downloaded',i=>status({state:'ready',version:i.version}));
  updater.on('error',e=>status({state:'error',message:String(e&&e.message||e).split('\n')[0]}));
  setTimeout(()=>checkUpdates(false),8000);
  setInterval(()=>checkUpdates(false),6*60*60*1000);
}
async function checkUpdates(manual){
  if(!updater)return manual?{state:'none',message:'Atualização automática só no app instalado.'}:lastStatus;
  if(lastStatus.state==='ready'||lastStatus.state==='downloading')return lastStatus;
  try{await updater.checkForUpdates()}catch(e){status({state:'error',message:String(e&&e.message||e).split('\n')[0]})}
  /* espera o primeiro resultado */
  for(let i=0;i<40&&lastStatus.state==='checking';i++)await new Promise(r=>setTimeout(r,250));
  return lastStatus;
}
function installUpdate(){if(updater&&lastStatus.state==='ready'){setImmediate(()=>updater.quitAndInstall(false,true))}}

/* ---------------- ciclo de vida ---------------- */
if(!isTest&&!app.requestSingleInstanceLock())app.quit();
else{
  app.on('second-instance',()=>{if(win){if(win.isMinimized())win.restore();win.focus()}});
  app.whenReady().then(async()=>{
    Menu.setApplicationMenu(null);
    await loadConfig();serveApp();ipc();createWindow();setupUpdater();watchVault();
    session.defaultSession.setPermissionRequestHandler((wc,perm,cb)=>cb(['clipboard-read','clipboard-sanitized-write','media','fullscreen','notifications'].includes(perm)));
  });
  app.on('window-all-closed',()=>app.quit());
}
