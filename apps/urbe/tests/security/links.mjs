// Links externos (RM-F3-18): a MESMA tabela de vetores contra os três lugares que abrem links fora do app:
//   1) Electron: native/desktop/guards.js (externalUrl) e main.js (shell:open, window.open, will-navigate);
//   2) Android: UrbeAndroidPlugin.openUrl → UrlGuard.java (executado com javac/java, sem Android SDK);
//   3) ponte: src/native/bridge.js (UrlNativeFS.isExternalAllowed e N.openExternal do Android).
// Só http, https, mailto e tel passam. Se não houver JDK, a parte Java é pulada com aviso
// (defina URBE_REQUIRE_JAVA=1 para tornar isso uma falha, como na CI).
import fs from 'node:fs';import os from 'node:os';import path from 'node:path';import {spawnSync} from 'node:child_process';import {createRequire} from 'node:module';
import {bootMain,ROOT} from '../lib/fake-electron.mjs';
import {fakeCapacitor,load as loadAndroid} from '../lib/fake-capacitor.mjs';
const require=createRequire(import.meta.url);
let failed=0;async function test(name,fn){try{await fn()==='skip'?console.log('SKIP',name):console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
const G=require('../../native/desktop/guards.js');

/* [url, permitida?] */
const V=[
  ['https://urbe.app',1],['https://example.com/a/b?x=1&y=2#frag',1],['http://example.com',1],['HTTPS://EXAMPLE.COM',1],['HtTp://Example.com/x',1],['https://user@example.com:8443/x',1],
  ['https://[::1]:8080/x',1],['https://exemplo.com/ação?q=ç',1],['mailto:alguem@example.com',1],['mailto:a@b.com?subject=oi',1],['MAILTO:a@b.com',1],['tel:+5511999999999',1],['TEL:112',1],
  ['javascript:alert(1)',0],['JavaScript:alert(1)',0],['javascript://example.com/%0aalert(1)',0],['file:///etc/passwd',0],['file:///C:/Windows/system.ini',0],['FILE:///x',0],
  ['data:text/html,<script>alert(1)</script>',0],['data:text/html;base64,PHNjcmlwdD4=',0],['vbscript:msgbox(1)',0],['intent://scan/#Intent;scheme=zxing;end',0],['intent:#Intent;action=android.intent.action.VIEW;end',0],
  ['content://media/external/file/1',0],['android-app://com.example',0],['market://details?id=x',0],['ftp://example.com/x',0],['ws://example.com',0],['blob:https://example.com/uuid',0],['blob:app://urbe/uuid',0],
  ['about:blank',0],['app://urbe/index.html',0],['app://print/doc.html',0],['chrome://gpu',0],['ms-msdt:/id PCWDiagnostic',0],['search-ms:query=x',0],['smb://host/share',0],['sms:+5511999999999',0],['geo:0,0',0],
  [' https://example.com',0],['https://example.com ',0],['\thttps://example.com',0],['https://example.com\n',0],['java\u0000script:alert(1)',0],['https://exa\u0000mple.com',0],
  ['https:example.com',0],['https:/example.com',0],['https:///example.com',0],['https://',0],['http://',0],['https://?x',0],['https://#x',0],['mailto:',0],['tel:',0],
  ['https://a b.com',0],['https://a\\b.com',0],['https:\\\\example.com',0],['',0],['example.com',0],['//example.com',0],[':',0],['://x',0],['https',0],
  ['https://x.com/'+'a'.repeat(9000),0]
];
const hex=s=>Buffer.from(s,'utf8').toString('hex');

await test('vetores: a tabela cobre permitidos e bloqueados',()=>{ok(V.filter(v=>v[1]).length>=12&&V.filter(v=>!v[1]).length>=40,'tabela pequena demais')});

await test('Electron: guards.externalUrl',()=>{
  for(const [u,allow] of V)ok(!!G.externalUrl(u)===!!allow,'externalUrl('+JSON.stringify(u.slice(0,60))+') deveria '+(allow?'permitir':'bloquear'));
  ok(G.EXTERNAL_SCHEMES.join()==='http:,https:,mailto:,tel:','lista de esquemas: '+G.EXTERNAL_SCHEMES);
});
await test('Electron: main.js (shell:open, window.open, will-navigate) só abre o que a allowlist permite',async()=>{
  const m=await bootMain({}, {URBE_TEST_MODE:null,URBE_TEST_USERDATA:null,URBE_TEST_VAULT:null});
  for(const [u,allow] of V){
    m.S.opened.length=0;await m.call('shell:open',undefined,u);
    ok((m.S.opened.length===1)===!!allow,'shell:open '+JSON.stringify(u.slice(0,60))+' → abriu '+m.S.opened.length);
    m.S.opened.length=0;const d=m.win.webContents.openHandler({url:u});
    ok(d.action!=='allow'||/^(blob:)?app:\/\/urbe/.test(u),'window.open nunca abre janela nova para '+u.slice(0,40));
    ok((m.S.opened.length===1)===!!allow,'window.open '+JSON.stringify(u.slice(0,60)));
    m.S.opened.length=0;const e=m.win.webContents.emit('will-navigate',u);
    if(!/^app:\/\/urbe(\/|$)/.test(u))ok(e.prevented&&(m.S.opened.length===1)===!!allow,'will-navigate '+JSON.stringify(u.slice(0,60)));
  }
});
await test('ponte: bridge.js (isExternalAllowed e openExternal do Android)',async()=>{
  const f=fakeCapacitor(),{W}=loadAndroid(f),opened=[],orig=f.cap.nativePromise;
  f.cap.nativePromise=(pl,m,o)=>{if(pl==='UrbeAndroid'&&m==='openUrl'){opened.push(o.url);return Promise.resolve()}return orig(pl,m,o)};
  for(const [u,allow] of V){
    ok(W.UrbeNativeFS.isExternalAllowed(u)===!!allow,'isExternalAllowed('+JSON.stringify(u.slice(0,60))+')');
    opened.length=0;try{await W.UrbeNative.openExternal(u)}catch(_){}
    ok((opened.length===1)===!!allow,'openExternal '+JSON.stringify(u.slice(0,60))+' → chegou ao plugin: '+opened.length);
  }
});
await test('Android: UrbeAndroidPlugin.openUrl usa o UrlGuard (e não sobrou verificação própria)',()=>{
  const j=fs.readFileSync(path.join(ROOT,'native/android/app/src/main/java/app/urbe/UrbeAndroidPlugin.java'),'utf8');
  const m=/public void openUrl\(PluginCall call\)\s*\{([\s\S]*?)\n    \}/.exec(j);ok(m,'achou openUrl');
  ok(/UrlGuard\.isAllowed\(url\)/.test(m[1])&&!/startsWith\("(https?|mailto|tel)/.test(m[1]),'openUrl deve validar só por UrlGuard.isAllowed');
  ok(m[1].indexOf('UrlGuard.isAllowed')<m[1].indexOf('startActivity'),'valida antes de abrir');
});
await test('Android: UrlGuard.java com os mesmos vetores (javac/java)',()=>{
  const javac=spawnSync('javac',['-version'],{encoding:'utf8'});
  if(javac.error||javac.status!==0){
    if(process.env.URBE_REQUIRE_JAVA==='1')throw new Error('JDK não encontrado e URBE_REQUIRE_JAVA=1');
    console.warn('  (sem JDK: parte Java pulada)');return 'skip';
  }
  const out=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-java-')),M=path.join(ROOT,'native/android/app/src/main/java/app/urbe'),T=path.join(ROOT,'native/android/app/src/test/java/app/urbe');
  const c=spawnSync('javac',['-d',out,path.join(M,'UrlGuard.java'),path.join(M,'PathGuard.java'),path.join(T,'UrlGuardProbe.java'),path.join(T,'GuardChecks.java')],{encoding:'utf8'});
  ok(c.status===0,'javac: '+c.stderr);
  const r=spawnSync('java',['-cp',out,'app.urbe.UrlGuardProbe'],{input:V.map(v=>hex(v[0])).join('\n')+'\n',encoding:'utf8'});
  ok(r.status===0,'java: '+r.stderr);
  const got=r.stdout.trim().split('\n');ok(got.length===V.length,'uma resposta por vetor: '+got.length+' de '+V.length);
  V.forEach((v,i)=>ok((got[i]==='1')===!!v[1],'UrlGuard('+JSON.stringify(v[0].slice(0,60))+') deveria '+(v[1]?'permitir':'bloquear')));
  const s=spawnSync('java',['-cp',out,'app.urbe.GuardChecks'],{encoding:'utf8'});ok(s.status===0,'GuardChecks (runner autossuficiente):\n'+s.stdout+s.stderr);
  fs.rmSync(out,{recursive:true,force:true});
});
if(failed)console.error(failed+' falha(s)');
process.exit(process.exitCode||0);
