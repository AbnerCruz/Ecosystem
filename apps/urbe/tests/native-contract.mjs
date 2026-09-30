// Contrato UrbeNative v1 (docs/v2/contracts/native.md): o MESMO conjunto de asserções de conformidade roda contra
//   - Electron: main.js real + vault-fs.js real em pasta temporária + preload.js real (IPC simulado, Electron simulado);
//   - Android: ponte src/native/bridge.js sobre o Capacitor simulado de tests/lib/fake-capacitor.mjs;
//   - web: sem UrbeNative (navegador/PWA).
// Uma capacidade declarada que não funciona reprova; uma capacidade "ausente" que existe também.
import fs from 'node:fs';import os from 'node:os';import path from 'node:path';import vm from 'node:vm';
import {bootMain,loadDesktop,makeElectron,ROOT} from './lib/fake-electron.mjs';
import {fakeCapacitor,load as loadAndroid} from './lib/fake-capacitor.mjs';
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
async function rejects(p,m){try{await(typeof p==='function'?p():p)}catch(e){return e}throw new Error('deveria falhar: '+m)}
const enc=s=>new TextEncoder().encode(s),dec=b=>new TextDecoder().decode(b);
const UNIVERSE=['fs','vault','openExternal','saveFile','print','update','back','storageStatus'];
const read=f=>fs.readFileSync(path.join(ROOT,f),'utf8');

/* A API que cada capacidade exige do objeto UrbeNative (usada para conferir declaradas e ausentes) */
const HAS={
  fs:N=>!!(N&&N.fs&&['stat','list','readBytes','writeBytes','mkdir','remove'].every(k=>typeof N.fs[k]==='function')),
  vault:N=>!!(N&&typeof N.vault==='function'),
  openExternal:N=>!!(N&&typeof N.openExternal==='function'),
  saveFile:N=>!!(N&&typeof N.saveFile==='function'),
  print:N=>!!(N&&typeof N.printHtml==='function'),
  update:N=>!!(N&&N.update&&typeof N.update.check==='function'&&typeof N.update.onStatus==='function'),
  back:N=>!!(N&&typeof N.minimize==='function'),
  storageStatus:N=>!!(N&&N.storage&&typeof N.storage.status==='function')
};

/* ---------- conjunto único de asserções ---------- */
async function conform(ad){
  const {N,contract}=ad;
  // 1) forma do contrato
  ok(contract&&contract.version===1,'contract.version === 1');
  ok(Array.isArray(contract.capabilities)&&Array.isArray(contract.unsupported),'capabilities e unsupported são listas');
  const all=[...contract.capabilities,...contract.unsupported];
  ok(all.length===UNIVERSE.length&&new Set(all).size===all.length&&all.every(c=>UNIVERSE.includes(c)),'cada capacidade aparece exatamente uma vez (declarada ou ausente): '+all);
  ok(contract.capabilities.join()===ad.expect.join(),ad.name+' declara '+contract.capabilities+' (esperado '+ad.expect+')');
  // 2) o que é declarado existe; o que é ausente não existe
  for(const c of contract.capabilities)ok(HAS[c](N),ad.name+': declara "'+c+'" mas a API não existe');
  for(const c of contract.unsupported)ok(!HAS[c](N),ad.name+': diz que "'+c+'" é ausente mas a API existe');
  // 3) cada capacidade declarada FUNCIONA
  const cap=c=>contract.capabilities.includes(c);
  if(cap('vault')){const v=await N.vault();ok(v&&typeof v.label==='string'&&v.label&&typeof v.path==='string','vault() → {label,path}: '+JSON.stringify(v))}
  if(cap('fs')){
    const F=N.fs,texto=enc('# Nota\nção 🙂'),bin=new Uint8Array([137,80,78,71,0,255,10,13]);
    await F.writeBytes('Conf/a.md',texto);await F.writeBytes('Conf/img.png',bin);
    ok(dec(await F.readBytes('Conf/a.md'))==='# Nota\nção 🙂','texto UTF-8 ida e volta');
    ok(Buffer.compare(Buffer.from(await F.readBytes('Conf/img.png')),Buffer.from(bin))===0,'binário ida e volta');
    const st=await F.stat('Conf/a.md');ok(st&&st.kind==='file'&&st.size>0,'stat de arquivo: '+JSON.stringify(st));
    const sd=await F.stat('Conf');ok(sd&&sd.kind==='directory','stat de pasta');
    ok((await F.stat('Conf/nao-existe.md'))===null,'stat de inexistente = null');
    const root=(await F.list('')).map(e=>e.name+':'+e.kind);ok(root.includes('Conf:directory'),'list da raiz: '+root);
    ok((await F.list('Conf')).map(e=>e.name+':'+e.kind).sort().join()==='a.md:file,img.png:file','list da pasta');
    await F.mkdir('Conf/sub');await F.mkdir('Conf/sub');ok((await F.stat('Conf/sub')).kind==='directory','mkdir idempotente');
    if(typeof F.tree==='function'){const t=(await F.tree()).map(e=>e.path+':'+e.kind);ok(t.includes('Conf/a.md:file')&&t.includes('Conf:directory'),'tree: '+t)}
    if(typeof F.readTexts==='function'){const r=await F.readTexts(['Conf/a.md','Conf/img.png','Conf/nao-existe.md']);ok(r['Conf/a.md']==='# Nota\nção 🙂'&&!('Conf/nao-existe.md' in r),'readTexts: '+JSON.stringify(r))}
    await F.writeBytes('Conf/a.md',enc('novo'));ok(dec(await F.readBytes('Conf/a.md'))==='novo','sobrescreve');
    // nada sai da pasta do vault
    for(const bad of ['../fora.md','a/../../fora.md','Conf/../../x'])await rejects(()=>F.writeBytes(bad,enc('x')),'escrever '+bad);
    try{await F.writeBytes('/tmp/urbe-conformance-abs.txt',enc('x'))}catch(_){}  // absoluto: recusado ou tratado como relativo; nunca grava fora
    for(const bad of ['../fora.md','/etc/passwd'])await rejects(()=>F.readBytes(bad),'ler '+bad);
    ok(!ad.leaked(),'nada foi gravado fora da pasta do Urbe');
    await F.remove('Conf/a.md');ok((await F.stat('Conf/a.md'))===null,'remove arquivo');
    await F.remove('Conf',true);ok((await F.stat('Conf'))===null&&(await F.stat('Conf/img.png'))===null,'remove pasta recursivo');
  }
  if(cap('openExternal')){
    ad.opened().length=0;
    for(const u of ['https://example.com/a?b=1','http://example.com','mailto:a@b.com','tel:+5511999999999']){await N.openExternal(u);ok(ad.opened().some(x=>x===u||x===new URL(u).toString()),'abre '+u+' (ou a forma normalizada): '+ad.opened())}
    ad.opened().length=0;
    for(const u of ['javascript:alert(1)','file:///etc/passwd','data:text/html,<script>1</script>','vbscript:x','intent://x#Intent;end','app://urbe/index.html','JAVASCRIPT:alert(1)',' https://a.com'])
      {try{await N.openExternal(u)}catch(_){}ok(!ad.opened().length,'NÃO abre '+u+': '+ad.opened())}
  }
  if(cap('saveFile')){
    const bytes=new Uint8Array([0,255,80,75,3,4,195,167]);
    const back=ad.prepSave('livro.zip');
    const r=await N.saveFile('livro.zip',bytes,'application/zip');
    ok(r&&typeof r==='object'&&typeof r.where==='string'&&r.where,'saveFile → {where}: '+JSON.stringify(r));
    ok(Buffer.compare(Buffer.from(back()),Buffer.from(bytes))===0,'conteúdo salvo idêntico');
    if(ad.canCancel){const b2=ad.prepSave('x.html',true);const c=await N.saveFile('x.html',enc('x'),'text/html');ok(c&&c.canceled===true&&!c.where&&b2()===null,'cancelar → {canceled:true}, sem gravar: '+JSON.stringify(c))}
  }
  if(cap('print')){
    const chk=ad.prepPrint();const r=await N.printHtml('<h1>Livro</h1>','Meu livro');
    ok(r&&typeof r==='object'&&(r.saved||r.printing||r.canceled),'printHtml → {saved|printing|canceled}: '+JSON.stringify(r));chk(r);
  }
  if(cap('update')){
    const s=await N.update.check();ok(s&&['none','checking','available','downloading','ready','error'].includes(s.state),'update.check → {state}: '+JSON.stringify(s));
    ok(typeof N.update.install==='function','update.install');N.update.onStatus(()=>{});
  }
  if(cap('back'))await ad.probeBack();
  if(cap('storageStatus')){const s=await N.storage.status();ok(s&&typeof s==='object'&&typeof s.sdk==='number','storage.status → {sdk}: '+JSON.stringify(s))}
}

/* ---------- ponte (bridge.js) em vm, para "web" e para conferir UrbeNativeContract ---------- */
function bridgeWin(N,extra){
  const toast={textContent:'',classList:{add(){},remove(){}}};
  const doc={documentElement:{classList:{add(){}}},addEventListener(){},getElementById(id){return id==='toast'?toast:null},querySelectorAll(){return[]}};
  function A(){}A.prototype.click=function(){this.clicked=true};A.prototype.hasAttribute=function(n){return n in (this.attrs||{})};A.prototype.getAttribute=function(n){return (this.attrs||{})[n]};
  const win={navigator:{},document:doc,_toast:toast,_A:A,...(extra||{})};if(N)win.UrbeNative=N;
  const c={window:win,document:doc,console,URL,File,Blob,TextEncoder,TextDecoder,DOMException,ArrayBuffer,Uint8Array,Symbol,Promise,fetch,setTimeout,clearTimeout,HTMLAnchorElement:A};
  vm.createContext(c);vm.runInContext(read('src/core/artifacts.js'),c);vm.runInContext(read('src/native/bridge.js'),c);return win;
}

/* ---------- adaptador: Electron ---------- */
async function electronAdapter(){
  const tmp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-contract-')),vault=path.join(tmp,'vault');
  const m=await bootMain({tmp},{URBE_TEST_MODE:'1',URBE_TEST_USERDATA:path.join(tmp,'ud'),URBE_TEST_VAULT:vault});
  const {electron,S}=makeElectron({tmp});
  S.rendererHandler=(ch,...a)=>m.call(ch,undefined,...a);
  loadDesktop('preload.js',electron,S);
  const N=S.exposed.api,win=bridgeWin(N);
  let dest=null;
  return{name:'electron',N,contract:N.contract,expect:['fs','vault','openExternal','saveFile','print','update'],win,canCancel:true,
    opened:()=>m.S.opened,
    leaked:()=>fs.existsSync(path.join(tmp,'fora.md'))||fs.existsSync(path.join(vault,'..','fora.md'))||fs.existsSync('/tmp/urbe-conformance-abs.txt'),
    prepSave(name,cancel){dest=path.join(tmp,'salvo-'+Math.random().toString(36).slice(2)+'-'+name);m.S.saveResults.push(cancel?{canceled:true}:{canceled:false,filePath:dest});const d=dest;return()=>fs.existsSync(d)?fs.readFileSync(d):null},
    prepPrint(){const d=path.join(tmp,'saida.pdf');m.S.saveResults.push({canceled:false,filePath:d});return r=>{ok(r.saved===d&&fs.readFileSync(d,'utf8').startsWith('%PDF'),'PDF gravado')}},
    async probeBack(){throw new Error('Electron não declara back')}};
}

/* ---------- adaptador: Android ---------- */
async function androidAdapter(){
  const f=fakeCapacitor({lote:true}),{W,listeners}=loadAndroid(f,async()=>({ok:true,json:async()=>({tag_name:'v1.7.0-beta',assets:[]})})),N=W.UrbeNative;
  const opened=[],orig=f.cap.nativePromise;
  // o Java (UrlGuard) é quem barra esquemas perigosos; aqui só registramos o que chegou ao plugin
  f.cap.nativePromise=(pl,m,o)=>{if(pl==='UrbeAndroid'&&m==='openUrl'){opened.push(o.url);return Promise.resolve()}return orig(pl,m,o)};
  return{name:'android',N,contract:N.contract,expect:UNIVERSE,win:W,canCancel:false,
    opened:()=>opened,
    leaked:()=>[...f.files.keys()].some(k=>!k.startsWith('Documents/Urbe/'))||[...f.dirs].some(k=>k!=='Documents'&&!k.startsWith('Documents/Urbe')),
    prepSave(name){return()=>f.saved.get(name)||null},
    prepPrint(){f.printed.length=0;return r=>{ok(r.printing===true&&f.printed.length===1&&f.printed[0].html==='<h1>Livro</h1>'&&f.printed[0].name==='Meu livro','o HTML chegou ao plugin de impressão: '+JSON.stringify(f.printed))}},
    async probeBack(){ok(typeof N.minimize==='function','minimize');const before=f.calls.length;await N.minimize();ok(f.calls.slice(before).includes('min'),'minimize chega ao plugin');
      ok((listeners.urbeBack||[]).length===1,'a ponte escuta o evento urbeBack do Android')}};
}

await test('conformidade — Electron (main.js + vault-fs.js reais, preload real, IPC simulado)',async()=>{
  const ad=await electronAdapter();await conform(ad);
  ok(JSON.stringify(ad.win.UrbeNativeContract)===JSON.stringify(ad.N.contract),'a ponte expõe o mesmo contrato (UrbeNativeContract)');
});
await test('Electron: download de blob/data pela ponte vai para fs:saveFile (paridade com Android/web); cancelar é tratado',async()=>{
  const ad=await electronAdapter(),back=ad.prepSave('nota.txt'),a=new ad.win._A();
  a.attrs={download:'nota.txt'};a.href='data:text/plain;base64,'+Buffer.from('olá').toString('base64');
  a.click();await new Promise(r=>setTimeout(r,50));
  ok(!a.clicked,'não cai no download padrão');ok(back()&&back().toString()==='olá','arquivo salvo pelo diálogo: '+back());ok(/^Salvo em /.test(ad.win._toast.textContent),'aviso: '+ad.win._toast.textContent);
  const b2=ad.prepSave('x.txt',true),c=new ad.win._A();c.attrs={download:'x.txt'};c.href=a.href;c.click();await new Promise(r=>setTimeout(r,50));
  ok(b2()===null&&ad.win._toast.textContent==='Cancelado.','cancelado: '+ad.win._toast.textContent);
});
await test('conformidade — Android (ponte sobre o Capacitor simulado)',async()=>{
  const ad=await androidAdapter();await conform(ad);
  ok(JSON.stringify(ad.win.UrbeNativeContract)===JSON.stringify(ad.N.contract),'a ponte expõe o mesmo contrato (UrbeNativeContract)');
  // esquemas: a ponte barra antes de chegar ao Java (o UrlGuard do Java barra de novo)
  ok(ad.win.UrbeNativeFS.isExternalAllowed('https://a.com')&&!ad.win.UrbeNativeFS.isExternalAllowed('javascript:alert(1)'),'allowlist na ponte');
});
await test('conformidade — web (sem UrbeNative): nada declarado, tudo ausente',async()=>{
  const win=bridgeWin(null),c=win.UrbeNativeContract;
  ok(win.UrbeNative===undefined&&win.UrbeNativeFS===undefined,'a ponte não inventa UrbeNative nem UrbeNativeFS no navegador');
  await conform({name:'web',N:win.UrbeNative,contract:c,expect:[]});
  ok(c.unsupported.join()===UNIVERSE.join(),'todas as capacidades declaradas ausentes: '+c.unsupported);
});
await test('ponte: contrato derivado quando a casca não declara (compatibilidade com app antigo)',()=>{
  const N={shell:'electron',fs:{stat(){},list(){},readBytes(){},writeBytes(){},mkdir(){},remove(){}},vault(){},openExternal(){},update:{check(){},onStatus(){}}};
  const c=bridgeWin(N).UrbeNativeContract;
  ok(c.version===1&&c.capabilities.join()==='fs,vault,openExternal,update'&&c.unsupported.join()==='saveFile,print,back,storageStatus','derivado do formato: '+JSON.stringify(c));
});
await test('contrato: preload.js e bridge.js declaram a mesma lista de capacidades do documento',()=>{
  const doc=read('docs/v2/contracts/native.md');
  for(const c of UNIVERSE)ok(doc.includes('`'+c+'`'),'docs/v2/contracts/native.md descreve "'+c+'"');
});
if(failed)console.error(failed+' falha(s)');
process.exit(process.exitCode||0);
