'use strict';
/* Urbe para computador (Windows). Abre o mesmo app do site, mas:
   - as notas ficam numa pasta de verdade (por padrão Documentos\Urbe), escolhida pela pessoa;
   - atualiza sozinho pelos lançamentos do GitHub (baixa em segundo plano e pede para reiniciar);
   - "Imprimir ou salvar PDF" gera o PDF direto, respeitando o tamanho de página do livro. */
const {app,BrowserWindow,ipcMain,dialog,shell,protocol,net,session,Menu}=require('electron');
const path=require('path');
const fsp=require('fs/promises');
const {pathToFileURL}=require('url');
const {createVaultFS}=require('./vault-fs');
const G=require('./guards');
const {createImportSources}=require('./import-source');
let importSources;

const APP_DIR=path.resolve(__dirname,'..','..');
const ORIGIN=G.ORIGIN;
/* URBE_TEST_* só valem com URBE_TEST_MODE=1 e app não empacotado; em produção são ignorados (RM-F3-10) */
const T=G.testConfig(process.env,app.isPackaged);
const isTest=T.enabled;
if(isTest&&T.userData)app.setPath('userData',T.userData);

protocol.registerSchemesAsPrivileged([{scheme:'app',privileges:{standard:true,secure:true,supportFetchAPI:true,corsEnabled:true,stream:true,codeCache:true}}]);

/* ---------------- configuração (pasta do vault) ---------------- */
const CONFIG=()=>path.join(app.getPath('userData'),'config.json');
let config={};
async function loadConfig(){try{config=JSON.parse(await fsp.readFile(CONFIG(),'utf8'))||{}}catch(_){config={}}
  if(!config.vault)config.vault=T.vault||path.join(app.getPath('documents'),'Urbe');
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
function external(url){const u=G.externalUrl(url);if(u){shell.openExternal(u);return true}return false}
function createWindow(){
  win=new BrowserWindow({width:1320,height:860,minWidth:360,minHeight:480,show:false,backgroundColor:'#0b0e14',autoHideMenuBar:true,title:'Urbe',
    icon:path.join(APP_DIR,'icon-512.png'),
    webPreferences:{preload:path.join(__dirname,'preload.js'),contextIsolation:true,sandbox:true,nodeIntegration:false,spellcheck:true}});
  win.once('ready-to-show',()=>win.show());
  win.webContents.setWindowOpenHandler(({url})=>{
    const d=G.decideWindowOpen(url);
    if(d.action==='external'){external(d.url);return{action:'deny'}}
    /* conteúdo do próprio app (anexos, prévia de página): janela simples, sem acesso ao sistema */
    if(d.action==='allow')return{action:'allow',overrideBrowserWindowOptions:d.options};
    return{action:'deny'};
  });
  win.webContents.on('will-navigate',(e,url)=>{const d=G.decideNavigation(url);if(d.action==='allow')return;e.preventDefault();if(d.action==='external')external(d.url)});
  win.loadURL(ORIGIN+'/index.html');
}

/* ---------------- arquivos do app (app://urbe/…) ---------------- */
function serveApp(){
  protocol.handle('app',async req=>{
    /* host exato "urbe" e só a allowlist (index, manifest, ícones, src/**, vendor/**): nada de package.json nem native/** */
    const r=G.resolveAppRequest(req.url,APP_DIR);
    if(!r.ok)return new Response('Não encontrado',{status:r.status});
    try{return await net.fetch(pathToFileURL(r.file).toString())}catch(_){return new Response('Não encontrado',{status:404})}
  });
}

/* ---------------- ponte com a página ---------------- */
function ipc(){
  const h=(name,fn)=>ipcMain.handle(name,(e,...a)=>{if(!G.isTrustedSender(e))throw new Error('Origem não autorizada');return fn(...a)});
  importSources=createImportSources(path.join(app.getPath('temp'),'urbe-import'));
  h('import:pick',async kind=>{if(kind!=='files'&&kind!=='folder')throw Error('Seleção inválida.');const result=await dialog.showOpenDialog(win,{title:kind==='folder'?'Importar pasta inteira':'Importar arquivos',properties:kind==='folder'?['openDirectory']:['openFile','multiSelections']});if(result.canceled)return{canceled:true};return importSources.pick(result.filePaths,kind==='folder')});
  h('import:read',(token,id,offset)=>importSources.read(token,id,offset));
  h('import:release',token=>importSources.release(token));
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
  h('fs:saveFile',(name,bytes)=>saveFile(String(name||'arquivo'),bytes));
  h('print:html',(html,name)=>printToPdf(String(html),String(name||'Urbe')));
  h('update:check',()=>checkUpdates(true));
  h('update:install',()=>installUpdate());
  h('update:last',()=>lastStatus);
}

/* ---------------- salvar arquivo (exportações HTML/ZIP…) ---------------- */
async function saveFile(name,bytes){
  const buf=G.saveBuffer(bytes);
  if(!buf)throw new Error('Conteúdo inválido ou grande demais');
  const safe=G.safeFileName(name,'arquivo');
  const r=await dialog.showSaveDialog(win,{title:'Salvar arquivo',defaultPath:path.join(app.getPath('documents'),safe),filters:G.saveFilters(safe)});
  if(r.canceled||!r.filePath)return{canceled:true};
  await fsp.writeFile(r.filePath,buf);
  return{where:r.filePath};
}

/* ---------------- PDF ----------------
   O HTML da exportação vem de conteúdo do usuário: é aberto em app://print/doc.html (origem própria),
   numa sessão isolada e descartável, sem preload, com toda navegação bloqueada. Nunca file://. */
let printSeq=0;
async function printToPdf(html,name){
  const ses=session.fromPartition('urbe-print-'+Date.now()+'-'+(++printSeq));
  ses.protocol.handle('app',req=>G.resolvePrintRequest(req.url)
    ?new Response(html,{headers:{'content-type':'text/html; charset=utf-8'}})
    :new Response('Não encontrado',{status:404}));
  ses.setPermissionRequestHandler((_wc,_perm,cb)=>cb(false));
  if(ses.setPermissionCheckHandler)ses.setPermissionCheckHandler(()=>false);
  ses.webRequest.onBeforeRequest((d,cb)=>cb({cancel:!G.isPrintRequestAllowed(d.url)}));
  const w=new BrowserWindow({show:false,webPreferences:{session:ses,sandbox:true,contextIsolation:true,nodeIntegration:false,javascript:true,webSecurity:true}});
  const wc=w.webContents,stop=e=>e.preventDefault();
  wc.setWindowOpenHandler(()=>({action:'deny'}));
  wc.on('will-navigate',stop);wc.on('will-redirect',stop);wc.on('will-frame-navigate',stop);
  try{
    await w.loadURL(G.PRINT_ORIGIN+G.PRINT_DOC);
    await wc.executeJavaScript('(document.fonts&&document.fonts.ready||Promise.resolve()).then(()=>new Promise(r=>setTimeout(r,300)))');
    const pdf=await wc.printToPDF({printBackground:true,preferCSSPageSize:true});
    const safe=G.safeFileName(name,'Urbe');
    const r=await dialog.showSaveDialog(win,{title:'Salvar PDF',defaultPath:path.join(app.getPath('documents'),safe+'.pdf'),filters:[{name:'PDF',extensions:['pdf']}]});
    if(r.canceled||!r.filePath)return{canceled:true};
    await fsp.writeFile(r.filePath,pdf);shell.openPath(r.filePath);return{saved:r.filePath};
  }finally{w.destroy();try{ses.protocol.unhandle('app')}catch(_){}}
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
    /* permissões só para a página do Urbe e só as mínimas (sem media/notificações: o app não usa) */
    const ds=session.defaultSession;
    ds.setPermissionRequestHandler((_wc,perm,cb,d)=>cb(G.isPermissionAllowed(perm,d&&d.requestingUrl,d&&d.isMainFrame)));
    if(ds.setPermissionCheckHandler)ds.setPermissionCheckHandler((_wc,perm,origin,d)=>G.isPermissionAllowed(perm,(d&&d.requestingUrl)||origin,d&&d.isMainFrame));
    await loadConfig();serveApp();ipc();if(!isTest)await importSources.initialize().catch(()=>{});createWindow();setupUpdater();watchVault();
  });
  app.on('window-all-closed',()=>app.quit());
}
