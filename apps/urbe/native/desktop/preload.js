'use strict';
/* Expõe à página só o necessário (window.UrbeNative). A página nunca recebe acesso ao
   Node nem ao disco inteiro: cada chamada passa pelo processo principal, que limita
   tudo à pasta do Urbe. Preload com sandbox só enxerga 'electron': este arquivo é
   autossuficiente (não use require de arquivos locais aqui). */
const {contextBridge,ipcRenderer}=require('electron');
const inv=(c,...a)=>ipcRenderer.invoke(c,...a);
const listeners=new Set(),vaultListeners=new Set();
ipcRenderer.on('vault:changed',(_e,paths)=>vaultListeners.forEach(fn=>{try{fn(paths)}catch(_){}}));
ipcRenderer.on('update:status',(_e,s)=>listeners.forEach(fn=>{try{fn(s)}catch(_){}}));
contextBridge.exposeInMainWorld('UrbeNative',{
  shell:'electron',
  /* contrato de capacidades (docs/v2/contracts/native.md): o que esta casca oferece e o que não */
  contract:{version:1,capabilities:['fs','vault','openExternal','saveFile','print','update','importSelection'],unsupported:['back','storageStatus']},
  platform:process.platform==='win32'?'windows':process.platform==='darwin'?'mac':'linux',
  info:()=>inv('app:info'),
  vault:()=>inv('vault:get'),
  pickVault:()=>inv('vault:pick'),
  revealVault:()=>inv('vault:reveal'),
  importSelection:{pick:kind=>inv('import:pick',kind),readChunk:(token,id,offset)=>inv('import:read',token,id,offset),release:token=>inv('import:release',token)},
  fs:{
    stat:rel=>inv('fs:stat',rel),
    list:rel=>inv('fs:list',rel),
    readBytes:rel=>inv('fs:readBytes',rel),
    writeBytes:(rel,bytes)=>inv('fs:writeBytes',rel,bytes),
    mkdir:rel=>inv('fs:mkdir',rel),
    tree:()=>inv('fs:tree'),
    readTexts:paths=>inv('fs:readTexts',paths),
    remove:(rel,recursive)=>inv('fs:remove',rel,!!recursive)
  },
  onVaultChanged:fn=>{if(typeof fn==='function')vaultListeners.add(fn)},
  openExternal:url=>inv('shell:open',String(url)),
  saveFile:(name,bytes,mime)=>inv('fs:saveFile',String(name||'arquivo'),bytes,String(mime||'')),
  printHtml:(html,name)=>inv('print:html',String(html),String(name||'Urbe')),
  update:{
    check:()=>inv('update:check'),
    install:()=>inv('update:install'),
    onStatus:fn=>{if(typeof fn!=='function')return;listeners.add(fn);inv('update:last').then(s=>{if(s&&s.state!=='none')fn(s)}).catch(()=>{})}
  }
});
