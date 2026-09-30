'use strict';
/* Decisões de segurança do app de computador, sem depender do Electron.
   main.js só liga estas funções aos eventos do Electron; aqui ficam as regras, para que
   tests/desktop-main.mjs as prove com simulações (RM-F3-09/10/19, REQ-062).
   Regra geral: na dúvida, negar. Comparações de origem são por igualdade exata. */
const path=require('path');

const SCHEME='app';
const HOST='urbe';
const ORIGIN=SCHEME+'://'+HOST;           // origem única da página do Urbe
const PRINT_HOST='print';
const PRINT_ORIGIN=SCHEME+'://'+PRINT_HOST; // origem separada, só do documento de impressão
const PRINT_DOC='/doc.html';

/* Esquemas que o sistema pode abrir por pedido da página (mesma lista do Android e da ponte). */
const EXTERNAL_SCHEMES=['http:','https:','mailto:','tel:'];
const MAX_URL=8192;

/* Permissões concedidas à página do Urbe (e a mais ninguém). O app não usa câmera/microfone
   ('media') nem notificações do sistema: negadas. Área de transferência e tela cheia são usadas. */
const PERMISSIONS=['clipboard-read','clipboard-sanitized-write','fullscreen'];

/* Arquivos do app servidos por app://urbe/… : só o que a página carrega. */
const ROOT_FILES=['index.html','manifest.webmanifest'];
const ROOT_ICON=/^[A-Za-z0-9][A-Za-z0-9_-]*\.png$/;
const SERVED_DIRS=['src','vendor'];

function parse(url){try{return new URL(String(url))}catch(_){return null}}

/* "app://urbe" exato: protocolo app, host urbe, sem porta nem usuário. */
function originOf(url){
  const u=parse(url);
  if(!u||u.username||u.password||u.port)return '';
  return u.protocol+'//'+u.host;
}
function isTrustedUrl(url){return originOf(url)===ORIGIN}

/* Guarda de IPC: só o quadro principal da página do Urbe pode chamar. */
function isTrustedSender(event){
  const f=event&&event.senderFrame;
  if(!f||typeof f.url!=='string')return false;
  if(f.parent)return false;                 // iframes nunca
  return isTrustedUrl(f.url);
}

function isPermissionAllowed(permission,requestingUrl,isMainFrame){
  if(isMainFrame===false)return false;      // iframes (mesmo do app) nunca recebem permissão
  return PERMISSIONS.includes(String(permission))&&isTrustedUrl(requestingUrl);
}

/* Link externo: devolve a URL normalizada ou null. */
function externalUrl(url){
  const s=String(url==null?'':url);
  if(!s||s.length>MAX_URL||/[\u0000-\u001f\u007f]/.test(s))return null;
  const u=parse(s);
  if(!u||!EXTERNAL_SCHEMES.includes(u.protocol))return null;
  if((u.protocol==='http:'||u.protocol==='https:')&&!u.hostname)return null;
  return u.toString();
}

/* Navegação da janela principal: fica na página do Urbe; links de fora vão ao sistema; o resto é bloqueado. */
function decideNavigation(url){
  if(isTrustedUrl(url))return{action:'allow'};
  const ext=externalUrl(url);
  return ext?{action:'external',url:ext}:{action:'block'};
}

/* Janelas novas: links externos vão ao sistema; conteúdo do próprio app (blob: criado pela página) abre
   numa janela simples, sem preload e sem acesso ao sistema; o resto é negado. */
const INTERNAL_WINDOW_OPTIONS={autoHideMenuBar:true,backgroundColor:'#ffffff',webPreferences:{sandbox:true,contextIsolation:true,nodeIntegration:false}};
function decideWindowOpen(url){
  const ext=externalUrl(url);
  if(ext)return{action:'external',url:ext};
  const s=String(url==null?'':url);
  const u=parse(s);
  if(u&&u.protocol==='blob:'&&isTrustedUrl(u.pathname))return{action:'allow',options:INTERNAL_WINDOW_OPTIONS};
  if(isTrustedUrl(s))return{action:'allow',options:INTERNAL_WINDOW_OPTIONS};
  return{action:'deny'};
}

/* Resolve um pedido app://urbe/… para um arquivo dentro de appDir.
   Devolve {ok:true,rel,file} ou {ok:false,status}. Só serve a allowlist acima. */
function resolveAppRequest(reqUrl,appDir){
  const u=parse(reqUrl);
  if(!u||u.protocol!==SCHEME+':'||u.host!==HOST||u.username||u.password)return{ok:false,status:404};
  let p;try{p=decodeURIComponent(u.pathname||'/')}catch(_){return{ok:false,status:400}}
  if(p===''||p==='/')p='/index.html';
  if(/[\u0000-\u001f\\]/.test(p)||p[0]!=='/')return{ok:false,status:400};
  const parts=p.split('/').slice(1);
  if(parts.some(s=>s===''||s==='.'||s==='..'||s[0]==='.'))return{ok:false,status:404};
  const rel=parts.join('/');
  const allowed=parts.length===1
    ?(ROOT_FILES.includes(rel)||ROOT_ICON.test(rel))
    :SERVED_DIRS.includes(parts[0]);
  if(!allowed)return{ok:false,status:404};
  const file=path.resolve(appDir,...parts),r=path.relative(appDir,file);
  if(!r||r.startsWith('..')||path.isAbsolute(r))return{ok:false,status:404};
  return{ok:true,rel,file};
}

/* Documento de impressão: servido de memória em app://print/doc.html, numa sessão à parte. */
function resolvePrintRequest(reqUrl){
  const u=parse(reqUrl);
  return !!u&&u.protocol===SCHEME+':'&&u.host===PRINT_HOST&&!u.username&&!u.password&&u.pathname===PRINT_DOC;
}
/* O que a janela de impressão pode buscar: o próprio documento, data:/blob: e https: (fontes e imagens
   que a página de exportação referencia). Nunca file:, http:, app://urbe nem o disco. */
function isPrintRequestAllowed(url){
  const u=parse(url);
  if(!u)return false;
  if(u.protocol==='data:')return true;
  if(u.protocol==='blob:')return originOf(u.pathname)===PRINT_ORIGIN;
  if(u.protocol==='https:')return !!u.hostname&&!u.username&&!u.password;
  return resolvePrintRequest(url);
}

/* Modo de teste: só com URBE_TEST_MODE=1 e app NÃO empacotado. Em produção os URBE_TEST_* são ignorados. */
function testConfig(env,isPackaged){
  env=env||{};
  if(isPackaged||env.URBE_TEST_MODE!=='1')return{enabled:false,userData:null,vault:null};
  return{enabled:true,userData:env.URBE_TEST_USERDATA||null,vault:env.URBE_TEST_VAULT||null};
}

/* Nome de arquivo seguro para o diálogo "Salvar como". */
function safeFileName(name,fallback){
  let s=String(name==null?'':name).replace(/[\u0000-\u001f\\/:*?"<>|]+/g,'-').replace(/^[.\s-]+|[.\s]+$/g,'').slice(0,120);
  if(/^(con|prn|aux|nul|com\d|lpt\d)(\..*)?$/i.test(s))s='_'+s;
  return s||fallback||'arquivo';
}
const MAX_SAVE_BYTES=512*1024*1024;
/* Converte o que chegou pelo IPC em Buffer ou null. */
function saveBuffer(bytes){
  let b=null;
  if(bytes instanceof Uint8Array)b=Buffer.from(bytes.buffer,bytes.byteOffset,bytes.byteLength);
  else if(bytes instanceof ArrayBuffer)b=Buffer.from(bytes);
  else if(typeof bytes==='string')b=Buffer.from(bytes,'utf8');
  return b&&b.length<=MAX_SAVE_BYTES?b:null;
}
function saveFilters(name){
  const m=/\.([A-Za-z0-9]{1,8})$/.exec(name);
  return m?[{name:m[1].toUpperCase(),extensions:[m[1].toLowerCase()]},{name:'Todos os arquivos',extensions:['*']}]:[{name:'Todos os arquivos',extensions:['*']}];
}

module.exports={SCHEME,HOST,ORIGIN,PRINT_HOST,PRINT_ORIGIN,PRINT_DOC,EXTERNAL_SCHEMES,PERMISSIONS,INTERNAL_WINDOW_OPTIONS,MAX_SAVE_BYTES,
  originOf,isTrustedUrl,isTrustedSender,isPermissionAllowed,externalUrl,decideNavigation,decideWindowOpen,
  resolveAppRequest,resolvePrintRequest,isPrintRequestAllowed,testConfig,safeFileName,saveBuffer,saveFilters};
