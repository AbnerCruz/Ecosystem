// Electron (native/desktop/main.js + guards.js): guard de IPC por origem, permissões, protocolo app://,
// links externos, window.open, printToPDF isolado, modo de teste e fs:saveFile.
// O Electron é simulado (tests/lib/fake-electron.mjs): provamos a lógica e a fiação, não o Chromium.
import fs from 'node:fs';import os from 'node:os';import path from 'node:path';import {createRequire} from 'node:module';
import {bootMain,APP_DIR,ROOT} from './lib/fake-electron.mjs';
const require=createRequire(import.meta.url);
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
async function rejects(p,m){try{await(typeof p==='function'?p():p)}catch(e){return e}throw new Error('deveria falhar: '+m)}
const G=require('../native/desktop/guards.js');
const H='app://urbe/index.html';

/* ---------- guards.js puro ---------- */
await test('origem: só app://urbe exato (nada de prefixo, porta, usuário, outro host)',()=>{
  for(const u of ['app://urbe','app://urbe/','app://urbe/index.html','app://urbe/src/app.js?x=1#y'])ok(G.isTrustedUrl(u),'deveria confiar: '+u);
  for(const u of ['app://urbe.evil.com/','app://urbeX/','app://urbe:8080/','app://user@urbe/','app://evil/urbe','app://evil/','https://urbe/','http://urbe/','blob:app://urbe/abc','file:///urbe','about:blank','javascript:alert(1)','null','','app:urbe','//urbe/'])
    ok(!G.isTrustedUrl(u),'deveria negar: '+u);
  ok(!G.isTrustedUrl(undefined)&&!G.isTrustedUrl(null),'undefined/null');
});
await test('remetente do IPC: quadro principal em app://urbe; iframe ou sem quadro não',()=>{
  ok(G.isTrustedSender({senderFrame:{url:H,parent:null}}),'principal');
  ok(!G.isTrustedSender({senderFrame:{url:H,parent:{}}}),'iframe');
  ok(!G.isTrustedSender({senderFrame:{url:'app://urbe.evil.com/',parent:null}}),'host parecido');
  ok(!G.isTrustedSender({senderFrame:{url:'https://evil.example/',parent:null}}),'externo');
  ok(!G.isTrustedSender({senderFrame:null})&&!G.isTrustedSender({})&&!G.isTrustedSender(null)&&!G.isTrustedSender({senderFrame:{url:5}}),'sem quadro');
});
await test('permissões: só a lista mínima, só para app://urbe (quadro principal)',()=>{
  ok(G.PERMISSIONS.join()==='clipboard-read,clipboard-sanitized-write,fullscreen','lista mínima: '+G.PERMISSIONS);
  for(const p of G.PERMISSIONS)ok(G.isPermissionAllowed(p,'app://urbe/index.html',true),p);
  for(const p of ['media','notifications','geolocation','midi','openExternal','clipboard-write','pointerLock','camera','microphone','display-capture','usb','hid','serial'])ok(!G.isPermissionAllowed(p,H,true),'nega '+p);
  ok(!G.isPermissionAllowed('clipboard-read','https://evil.example/',true)&&!G.isPermissionAllowed('fullscreen','app://urbe.evil.com/',true)&&!G.isPermissionAllowed('fullscreen','blob:app://urbe/x',true),'origem errada');
  ok(!G.isPermissionAllowed('clipboard-read',H,false),'iframe do próprio app');
  ok(!G.isPermissionAllowed('clipboard-read',undefined,true),'sem origem');
});
await test('allowlist de links externos: http, https, mailto, tel; nada de javascript:/file:/data:/vbscript:/intent:',()=>{
  for(const u of ['https://urbe.app','http://a.com/x?y=1','mailto:a@b.com','tel:+5511999999999','HTTPS://Example.com'])ok(G.externalUrl(u),'permite '+u);
  for(const u of ['javascript:alert(1)','JaVaScRiPt:alert(1)','file:///C:/Windows/system.ini','data:text/html,<script>1</script>','vbscript:x','intent://x#Intent;end','app://urbe/index.html','blob:app://urbe/x','ftp://x/','ms-msdt:/id x','search-ms:query=a','https://','http://',' https://a.com','https://a.com\n','','x',null,undefined,'https://'+'a'.repeat(9000)])
    ok(!G.externalUrl(u),'bloqueia '+String(u).slice(0,40));
});
await test('caminhos servidos: index, manifest, ícones, src/**, vendor/**; nunca package.json, native/**, .., dotfiles',()=>{
  const R=u=>G.resolveAppRequest(u,APP_DIR);
  for(const u of ['app://urbe','app://urbe/','app://urbe/index.html','app://urbe/manifest.webmanifest','app://urbe/icon-192.png','app://urbe/icon-512.png','app://urbe/apple-touch-icon.png','app://urbe/src/app.js','app://urbe/src/native/bridge.js','app://urbe/vendor/katex/katex.min.css','app://urbe/src/ui/x%20y.js'])ok(R(u).ok,'serve '+u);
  ok(R('app://urbe/').rel==='index.html','raiz = index');
  for(const u of ['app://urbe/package.json','app://urbe/package-lock.json','app://urbe/native/desktop/main.js','app://urbe/native/desktop/preload.js','app://urbe/tests/x.mjs','app://urbe/docs/v2/SPEC.md','app://urbe/tools/x.mjs','app://urbe/.git/config','app://urbe/AGENTS.md','app://urbe/sw.js','app://urbe/src','app://urbe/src/','app://urbe/src/.env','app://urbe/src/../package.json','app://urbe/src/%2e%2e/package.json','app://urbe/src/%2E%2E/native/desktop/main.js','app://urbe/vendor/..%2fpackage.json','app://urbe/src%5c..%5cpackage.json','app://urbe/src//app.js','app://urbe/src/app.js%00.png','app://urbe/%2e%2e/%2e%2e/etc/passwd','app://urbe/icon.png/x','app://urbe/sub/icon-192.png','app://urbe/.hidden.png','app://urbe/src/%ZZ',
    'app://evil/index.html','app://urbe.evil.com/index.html','app://urbe:81/index.html','app://user@urbe/index.html','https://urbe/index.html','file:///x','app://print/index.html'])
    ok(!R(u).ok,'nega '+u);
  ok(R('app://urbe/src/%ZZ').status===400,'escape malformado = 400');
  ok(R('app://urbe/package.json').status===404,'404');
});
await test('tudo que o index.html carrega está na allowlist (e existe no disco)',()=>{
  const html=fs.readFileSync(path.join(ROOT,'index.html'),'utf8'),refs=[...html.matchAll(/(?:src|href)="(\.\/[^"#?]+)"/g)].map(m=>m[1].slice(2));
  ok(refs.length>20,'achou referências: '+refs.length);
  for(const r of refs){const d=G.resolveAppRequest('app://urbe/'+r,APP_DIR);ok(d.ok,'index.html carrega '+r+' mas a allowlist nega');ok(fs.existsSync(d.file),'não existe: '+r)}
});
await test('window.open e navegação: decisões',()=>{
  ok(G.decideWindowOpen('https://a.com').action==='external','https vai ao sistema');
  ok(G.decideWindowOpen('blob:app://urbe/9f8e').action==='allow'&&G.decideWindowOpen('app://urbe/preview.html').action==='allow','conteúdo do app abre');
  const o=G.decideWindowOpen('blob:app://urbe/9f8e').options.webPreferences;ok(o.sandbox&&o.contextIsolation&&!o.nodeIntegration&&!o.preload,'janela sem preload e com sandbox');
  for(const u of ['javascript:alert(1)','file:///x','data:text/html,x','blob:https://evil.com/x','blob:app://urbe.evil.com/x','app://urbe.evil.com/','app://urbeX','about:blank','','vbscript:x','intent://x'])ok(G.decideWindowOpen(u).action==='deny','nega '+u);
  ok(G.decideNavigation('app://urbe/index.html').action==='allow'&&G.decideNavigation('https://x.com').action==='external'&&G.decideNavigation('file:///x').action==='block'&&G.decideNavigation('app://urbe.evil.com/').action==='block','navegação');
});
await test('modo de teste: só com URBE_TEST_MODE=1 e app não empacotado',()=>{
  const env={URBE_TEST_MODE:'1',URBE_TEST_USERDATA:'/u',URBE_TEST_VAULT:'/v'};
  ok(G.testConfig(env,false).enabled&&G.testConfig(env,false).userData==='/u'&&G.testConfig(env,false).vault==='/v','ligado');
  ok(!G.testConfig(env,true).enabled&&G.testConfig(env,true).userData===null&&G.testConfig(env,true).vault===null,'empacotado: inerte');
  ok(!G.testConfig({URBE_TEST_USERDATA:'/u',URBE_TEST_VAULT:'/v'},false).enabled,'sem a flag: inerte');
  ok(!G.testConfig({...env,URBE_TEST_MODE:'true'},false).enabled&&!G.testConfig({...env,URBE_TEST_MODE:'0'},false).enabled,'só "1"');
  ok(!G.testConfig(undefined,false).enabled,'sem env');
});
await test('nome de arquivo e bytes do saveFile',()=>{
  ok(G.safeFileName('../../evil.html')==='evil.html'||!/[\\/]/.test(G.safeFileName('../../evil.html')),'sem separadores: '+G.safeFileName('../../evil.html'));
  ok(G.safeFileName('a:b*c.md')==='a-b-c.md'&&G.safeFileName('')==='arquivo'&&G.safeFileName('...')==='arquivo'&&G.safeFileName('con.txt')==='_con.txt','nomes');
  ok(G.saveBuffer(new Uint8Array([1,2])).length===2&&G.saveBuffer(new ArrayBuffer(3)).length===3&&G.saveBuffer('é').length===2,'aceita bytes');
  ok(G.saveBuffer(5)===null&&G.saveBuffer({})===null&&G.saveBuffer(null)===null&&G.saveBuffer([1,2])===null,'recusa o resto');
});

/* ---------- main.js com o Electron simulado ---------- */
const TEST_ENV={URBE_TEST_MODE:null,URBE_TEST_USERDATA:null,URBE_TEST_VAULT:null};
async function boot(opts,env){return bootMain(opts||{},{...TEST_ENV,...(env||{})})}

await test('main: esquema app privilegiado; janela com contextIsolation, sandbox e sem Node',async()=>{
  const m=await boot();
  ok(m.S.privileged&&m.S.privileged[0].scheme==='app','registra app://');
  const wp=m.win.opts.webPreferences;ok(wp.contextIsolation===true&&wp.sandbox===true&&wp.nodeIntegration===false,JSON.stringify(wp));
  ok(m.win.loadedUrl==='app://urbe/index.html','abre app://urbe/index.html');
  ok(m.S.menus[0]===null,'sem menu');
});
await test('IPC: canais existem e só respondem à origem exata app://urbe',async()=>{
  const m=await boot();
  for(const ch of ['fs:stat','fs:list','fs:readBytes','fs:writeBytes','fs:mkdir','fs:tree','fs:readTexts','fs:remove','fs:saveFile','vault:get','vault:pick','vault:reveal','shell:open','app:info','print:html','update:check','update:install','update:last'])ok(m.S.handlers.has(ch),'canal '+ch);
  ok((await m.call('app:info')).version==='0.0.0-test','origem certa responde');
  for(const bad of ['app://urbe.evil.com/index.html','app://urbeX/','app://urbe:9/','https://evil.example/','blob:app://urbe/x','file:///C:/x.html','about:blank'])
    await rejects(m.call('app:info',bad),'origem '+bad);
  await rejects(m.call('app:info',null),'sem quadro');
  await rejects(()=>m.S.handlers.get('app:info')({senderFrame:{url:H,parent:{}}}),'iframe');
  await rejects(()=>m.S.handlers.get('fs:readBytes')({},'x'),'sem senderFrame');
  for(const ch of m.S.handlers.keys()){const e=await rejects(m.call(ch,'app://urbe.evil.com/'),ch);ok(/Origem não autorizada/.test(e.message),ch+': '+e.message)}
});
await test('IPC de arquivos continua preso à pasta do vault',async()=>{
  const m=await boot();
  await m.call('fs:writeBytes',undefined,'Notas/a.md',new TextEncoder().encode('oi'));
  ok(Buffer.from(await m.call('fs:readBytes',undefined,'Notas/a.md')).toString()==='oi','grava e lê');
  await rejects(m.call('fs:readBytes',undefined,'../userData/config.json'),'..');
  await rejects(m.call('fs:writeBytes',undefined,'../fora.md',new Uint8Array(1)),'.. escrita');
});
await test('permissões (sessão padrão): concede só a lista mínima à página do Urbe',async()=>{
  const m=await boot(),ds=m.S.defaultSession;
  ok(ds.permReq&&ds.permCheck,'handlers instalados');
  const ask=(perm,url,main)=>new Promise(r=>ds.permReq({},perm,r,{requestingUrl:url,isMainFrame:main!==false}));
  for(const p of ['clipboard-read','clipboard-sanitized-write','fullscreen'])ok(await ask(p,H)===true,'concede '+p);
  for(const p of ['media','notifications','geolocation','midi','openExternal','clipboard-write'])ok(await ask(p,H)===false,'nega '+p);
  ok(await ask('clipboard-read','https://evil.example/')===false&&await ask('fullscreen','app://urbe.evil.com/')===false,'terceiros');
  ok(await ask('clipboard-read',H,false)===false,'iframe');
  ok(await new Promise(r=>ds.permReq({},'fullscreen',r))===false,'sem detalhes: nega');
  ok(ds.permCheck({},'clipboard-read','app://urbe',{requestingUrl:H,isMainFrame:true})===true&&ds.permCheck({},'media','app://urbe',{requestingUrl:H,isMainFrame:true})===false&&ds.permCheck({},'clipboard-read','https://evil.example/',{})===false,'verificação de permissão');
});
await test('protocolo app://: host exato e allowlist (net.fetch só recebe arquivos permitidos)',async()=>{
  const m=await boot(),h=m.S.protocols.get('default|app');ok(h,'protocol.handle("app")');
  const get=async u=>{m.S.netFetched.length=0;const r=await h({url:u});return{status:r.status,fetched:m.S.netFetched.slice()}};
  let r=await get('app://urbe/');ok(r.status===200&&r.fetched.length===1&&/index\.html$/.test(r.fetched[0])&&r.fetched[0].startsWith('file:'),'index: '+JSON.stringify(r));
  r=await get('app://urbe/src/app.js');ok(r.status===200&&r.fetched[0].endsWith('/src/app.js'),'src');
  r=await get('app://urbe/vendor/katex/katex.min.css');ok(r.status===200,'vendor');
  for(const u of ['app://urbe/package.json','app://urbe/native/desktop/main.js','app://urbe/src/%2e%2e/package.json','app://urbe.evil.com/index.html','app://evil/index.html','app://urbe:8080/index.html','app://urbe/.git/config']){
    r=await get(u);ok(r.status>=400&&r.fetched.length===0,'bloqueia '+u+' '+JSON.stringify(r))}
});
await test('links: shell:open e window.open só passam pela allowlist de esquemas',async()=>{
  const m=await boot();
  for(const u of ['https://urbe.app','http://a.com','mailto:a@b.com','tel:+551199999999'])await m.call('shell:open',undefined,u);
  ok(m.S.opened.length===4,'abriu os 4: '+m.S.opened);
  m.S.opened.length=0;
  for(const u of ['javascript:alert(1)','file:///C:/Windows/system.ini','data:text/html,x','vbscript:x','intent://x#Intent;end','app://urbe/x','ms-msdt:x'])await m.call('shell:open',undefined,u);
  ok(m.S.opened.length===0,'nada perigoso abre: '+m.S.opened);
  const oh=m.win.webContents.openHandler;ok(oh,'setWindowOpenHandler');
  let d=oh({url:'https://a.com/x'});ok(d.action==='deny'&&m.S.opened.length===1&&m.S.opened[0]==='https://a.com/x','https: sistema abre, janela negada');
  m.S.opened.length=0;
  for(const u of ['javascript:alert(1)','file:///x','data:text/html,x','app://urbe.evil.com/','app://urbeX/a','blob:https://evil.com/x','about:blank']){d=oh({url:u});ok(d.action==='deny'&&!m.S.opened.length,'nega '+u)}
  d=oh({url:'blob:app://urbe/abc'});ok(d.action==='allow'&&d.overrideBrowserWindowOptions.webPreferences.sandbox===true&&!d.overrideBrowserWindowOptions.webPreferences.preload,'blob do app: janela simples');
  d=oh({url:'app://urbe/preview.html'});ok(d.action==='allow','app://urbe/…');
  let e=m.win.webContents.emit('will-navigate','app://urbe/index.html#x');ok(!e.prevented,'navegação interna livre');
  e=m.win.webContents.emit('will-navigate','https://outro.com/');ok(e.prevented&&m.S.opened.join()==='https://outro.com/','externa: bloqueia e abre no sistema');
  m.S.opened.length=0;
  for(const u of ['app://urbe.evil.com/','javascript:1','file:///x'])e=m.win.webContents.emit('will-navigate',u),ok(e.prevented&&!m.S.opened.length,'bloqueia '+u);
});
await test('printToPDF: app://print numa sessão isolada, sem preload, sem file://, navegação bloqueada',async()=>{
  const m=await boot(),before=m.S.windows.length;
  m.S.saveResults.push({canceled:false,filePath:path.join(m.S.tmp,'saida.pdf')});
  const html='<!doctype html><h1>Livro</h1><img src="file:///C:/Users/x/segredo.png">';
  const r=await m.call('print:html',undefined,html,'../Meu: Livro');
  ok(r.saved===path.join(m.S.tmp,'saida.pdf')&&fs.readFileSync(r.saved,'utf8')==='%PDF-fake','gravou o PDF: '+JSON.stringify(r));
  ok(m.S.openedPaths[0]===r.saved,'abre o PDF salvo');
  const w=m.S.windows[before];ok(w&&w!==m.win,'janela de impressão criada');
  const wp=w.opts.webPreferences;
  ok(!wp.preload&&wp.sandbox===true&&wp.contextIsolation===true&&wp.nodeIntegration===false&&wp.webSecurity===true,'prefs: '+JSON.stringify({...wp,session:'…'}));
  ok(wp.session&&wp.session!==m.S.defaultSession&&/^urbe-print-/.test(wp.session.name)&&!/^persist:/.test(wp.session.name),'sessão isolada e não persistente: '+(wp.session&&wp.session.name));
  ok(w.loadedUrl==='app://print/doc.html'&&!/^file:/i.test(w.loadedUrl),'carrega app://print/doc.html');
  ok(w.destroyed,'janela destruída');
  const ses=wp.session,ph=m.S.protocols.get(ses.name+'|app');
  ok(ph===undefined,'protocolo removido ao final (unhandle)');
  ok(m.S.saves[0].defaultPath.startsWith(path.join(m.S.tmp,'Documents'))&&!/[\\/]\.\./.test(path.relative(path.join(m.S.tmp,'Documents'),m.S.saves[0].defaultPath)),'nome do PDF seguro: '+m.S.saves[0].defaultPath);
  ok(!fs.readdirSync(os.tmpdir()).some(f=>/^urbe-print-.*\.html$/.test(f)),'sem HTML temporário em disco');
  ok(!/loadFile|file:\/\//.test(fs.readFileSync(path.join(ROOT,'native/desktop/main.js'),'utf8').replace(/\/\*[\s\S]*?\*\//g,'')),'main.js não usa loadFile nem file:// (fora de comentários)');
});
await test('printToPDF: o que a janela de impressão pode buscar e a navegação',async()=>{
  const m=await boot(),before=m.S.windows.length;
  // segura a impressão no meio para inspecionar a sessão viva
  let inspect;const orig=m.electron.BrowserWindow.prototype.loadURL;
  m.electron.BrowserWindow.prototype.loadURL=async function(u){inspect=this;const ses=this.opts.webPreferences.session;
    const ph=m.S.protocols.get(ses.name+'|app');
    const good=await ph({url:'app://print/doc.html'}),other=await ph({url:'app://print/outro.html'}),urbe=await ph({url:'app://urbe/index.html'}),pkg=await ph({url:'app://print/../package.json'});
    inspect.probe={good:good.status,goodBody:await good.text(),other:other.status,urbe:urbe.status,pkg:pkg.status,type:good.headers.get('content-type')};
    const dec=u=>new Promise(r=>ses.beforeRequest({url:u},x=>r(!x.cancel)));
    inspect.probe.allow={};for(const x of ['app://print/doc.html','data:image/png;base64,AAAA','https://fonts.googleapis.com/css2?family=X','https://cdn.jsdelivr.net/npm/katex/x.css','blob:app://print/uuid'])inspect.probe.allow[x]=await dec(x);
    inspect.probe.deny={};for(const x of ['file:///C:/Windows/system.ini','file:///etc/passwd','app://urbe/index.html','app://urbe/package.json','http://insecure.example/x','blob:app://urbe/uuid','ftp://x/','javascript:alert(1)','chrome://gpu'])inspect.probe.deny[x]=await dec(x);
    inspect.probe.perm=await new Promise(r=>ses.permReq({},'media',r,{}));inspect.probe.permCheck=ses.permCheck({}, 'clipboard-read','app://print',{});
    return orig.call(this,u)};
  try{await m.call('print:html',undefined,'<h1>oi</h1>','Livro')}finally{m.electron.BrowserWindow.prototype.loadURL=orig}
  const p=inspect.probe;
  ok(p.good===200&&p.goodBody==='<h1>oi</h1>'&&/text\/html/.test(p.type),'serve o HTML da memória');
  ok(p.other===404&&p.urbe===404&&p.pkg===404,'só /doc.html: '+JSON.stringify(p));
  for(const [u,v] of Object.entries(p.allow))ok(v===true,'permite '+u);
  for(const [u,v] of Object.entries(p.deny))ok(v===false,'cancela '+u);
  ok(p.perm===false&&p.permCheck===false,'nenhuma permissão na janela de impressão');
  const wc=inspect.webContents;
  ok(wc.openHandler({url:'https://x.com'}).action==='deny','window.open negado');
  for(const ev of ['will-navigate','will-redirect','will-frame-navigate'])ok(wc.emit(ev,'https://evil.example/').prevented,ev+' bloqueado');
  ok(m.S.windows.length===before+1,'uma janela');
});
await test('printToPDF: cancelar o diálogo não grava nada e libera a janela; erro também libera',async()=>{
  const m=await boot(),before=m.S.windows.length;
  m.S.saveResults.push({canceled:true});
  const r=await m.call('print:html',undefined,'<p>x</p>','X');ok(r.canceled===true&&!m.S.openedPaths.length,'cancelado');
  ok(m.S.windows[before].destroyed,'janela destruída');
  const orig=m.electron.BrowserWindow.prototype.loadURL;m.electron.BrowserWindow.prototype.loadURL=async function(){throw new Error('falhou')};
  await rejects(m.call('print:html',undefined,'<p>x</p>','X'),'erro de carga');m.electron.BrowserWindow.prototype.loadURL=orig;
  ok(m.S.windows[before+1].destroyed,'janela destruída após erro');
});
await test('modo de teste no main: URBE_TEST_* inertes sem a flag e em app empacotado',async()=>{
  const env={URBE_TEST_USERDATA:'__T__/ud-teste',URBE_TEST_VAULT:'__T__/vault-teste'};
  const mk=(tmp,over)=>({...over,URBE_TEST_USERDATA:path.join(tmp,'ud-teste'),URBE_TEST_VAULT:path.join(tmp,'vault-teste')});
  // 1) sem flag
  let tmp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-tm-')),m=await boot({tmp},mk(tmp));
  ok(m.S.pathsSet.userData===undefined,'userData de teste ignorado');
  let v=await m.call('vault:get');ok(v.path===path.join(tmp,'Documents','Urbe')&&!fs.existsSync(path.join(tmp,'vault-teste')),'vault padrão em Documentos: '+v.path);
  ok(m.S.lock===1,'trava de instância única ativa');
  // 2) empacotado, mesmo com a flag
  tmp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-tm-'));m=await boot({tmp,isPackaged:true},mk(tmp,{URBE_TEST_MODE:'1'}));
  ok(m.S.pathsSet.userData===undefined,'empacotado: userData ignorado');v=await m.call('vault:get');ok(v.path===path.join(tmp,'Documents','Urbe'),'empacotado: vault padrão: '+v.path);ok(m.S.lock===1,'empacotado: trava ativa');
  // 3) valor errado da flag
  tmp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-tm-'));m=await boot({tmp},mk(tmp,{URBE_TEST_MODE:'true'}));v=await m.call('vault:get');ok(v.path===path.join(tmp,'Documents','Urbe'),'flag diferente de "1"');
  // 4) flag + não empacotado: vale
  tmp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-tm-'));m=await boot({tmp},mk(tmp,{URBE_TEST_MODE:'1'}));
  ok(m.S.pathsSet.userData===path.join(tmp,'ud-teste'),'modo de teste: userData de teste');v=await m.call('vault:get');ok(v.path===path.join(tmp,'vault-teste'),'modo de teste: vault de teste: '+v.path);ok(m.S.lock===0,'modo de teste: sem trava');
  void env;
});
await test('fs:saveFile: grava onde a pessoa escolheu; cancelar não grava; nome e bytes validados',async()=>{
  const m=await boot(),out=path.join(m.S.tmp,'export','livro.html');fs.mkdirSync(path.dirname(out));
  m.S.saveResults.push({canceled:false,filePath:out});
  const bytes=new TextEncoder().encode('<h1>Olá ção</h1>');
  const r=await m.call('fs:saveFile',undefined,'../../livro.html',bytes,'text/html');
  ok(r.where===out&&fs.readFileSync(out,'utf8')==='<h1>Olá ção</h1>','gravou: '+JSON.stringify(r));
  const o=m.S.saves[0];ok(o.defaultPath===path.join(m.S.tmp,'Documents','livro.html'),'nome sugerido sem caminho: '+o.defaultPath);
  ok(o.filters[0].extensions[0]==='html','filtro pela extensão');
  m.S.saveResults.push({canceled:true});fs.rmSync(out);
  const c=await m.call('fs:saveFile',undefined,'x.zip',new Uint8Array([80,75]));ok(c.canceled===true&&!fs.existsSync(out)&&!c.where,'cancelou');
  m.S.saveResults.push({canceled:false,filePath:''});
  ok((await m.call('fs:saveFile',undefined,'x.zip',new Uint8Array([1]))).canceled===true,'sem caminho = cancelado');
  const zip=path.join(m.S.tmp,'a.zip');m.S.saveResults.push({canceled:false,filePath:zip});
  await m.call('fs:saveFile',undefined,'a.zip',new Uint8Array([0,255,80,75,3,4]));ok(Buffer.compare(fs.readFileSync(zip),Buffer.from([0,255,80,75,3,4]))===0,'binário intacto');
  const n=m.S.saves.length;
  await rejects(m.call('fs:saveFile',undefined,'x',12345),'bytes inválidos');await rejects(m.call('fs:saveFile',undefined,'x',{a:1}),'objeto');
  ok(m.S.saves.length===n,'nem abre o diálogo com conteúdo inválido');
  await rejects(m.call('fs:saveFile','app://urbe.evil.com/','x',bytes),'origem');
});
await test('empacotamento: guards.js entra no build (native/desktop/*.js) e preload só usa electron',()=>{
  const pkg=JSON.parse(fs.readFileSync(path.join(ROOT,'package.json'),'utf8'));
  ok(pkg.build.files.includes('native/desktop/*.js')&&fs.existsSync(path.join(ROOT,'native/desktop/guards.js')),'guards.js coberto por build.files');
  ok(!/require\(['"]\.\.?\//.test(fs.readFileSync(path.join(ROOT,'native/desktop/preload.js'),'utf8')),'preload sandbox: sem require de arquivo local');
});
if(failed)console.error(failed+' falha(s)');
process.exit(process.exitCode||0);
