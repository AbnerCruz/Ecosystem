// Todo arquivo do app está onde precisa: carregado no index.html, guardado pelo
// service worker (para funcionar offline) e conferido pelo CI. Um arquivo esquecido
// em qualquer um desses lugares é um bug silencioso.
import fs from 'node:fs';import path from 'node:path';
const root=new URL('..',import.meta.url).pathname;
const read=f=>fs.readFileSync(path.join(root,f),'utf8');
function walk(d){return fs.readdirSync(path.join(root,d),{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(d+'/'+e.name):[d+'/'+e.name])}
const index=read('index.html'),sw=read('sw.js'),ci=read('.github/workflows/structural-checks.yml');
let fail=0;function check(ok,msg){if(!ok){fail++;console.error('FAIL',msg);process.exitCode=1}}

const js=walk('src').filter(f=>f.endsWith('.js')),css=walk('src').filter(f=>f.endsWith('.css'));
const workers=['src/world/chunk-worker.js'];
for(const f of js){
  if(!workers.includes(f))check(index.includes('"./'+f+'"'),f+' não está no index.html');
  check(sw.includes("'./"+f+"'"),f+' não está no service worker');
}
for(const f of css){check(index.includes('"./'+f+'"'),f+' não está no index.html');check(sw.includes("'./"+f+"'"),f+' não está no service worker')}
check(/git ls-files '\*\.js'/.test(ci)&&/node --check/.test(ci),'o CI precisa conferir a sintaxe de todos os .js');
check(/for t in tests\/\*\.mjs/.test(ci),'o CI precisa rodar todos os testes de tests/');
/* nada no index/sw aponta para arquivo que não existe */
for(const m of index.matchAll(/(?:src|href)="\.\/([^"]+)"/g))check(fs.existsSync(path.join(root,m[1])),'index.html aponta para '+m[1]+', que não existe');
for(const m of sw.matchAll(/'\.\/([^']+)'/g))check(fs.existsSync(path.join(root,m[1])),'sw.js guarda '+m[1]+', que não existe');
/* uma versão só em todo lugar */
const v=(read('src/app.js').match(/V21_VERSION='([^']+)'/g)||[]).pop().slice(13,-1);
check(index.includes('<title>Urbe v'+v+'</title>'),'título do index.html não é a versão '+v);
check(sw.includes("urbe-shell-v"+v+"'"),'cache do service worker não é da versão '+v);
check(read('CHANGELOG.md').includes('## v'+v),'CHANGELOG sem a versão '+v);
/* sem capturas de tela ou rascunhos esquecidos na raiz */
for(const f of fs.readdirSync(root))check(!/\.(png|jpe?g)$/i.test(f)||/^(icon-|apple-touch)/.test(f),'arquivo solto na raiz: '+f);
if(!fail)console.log('OK   consistência: '+js.length+' scripts e '+css.length+' folhas em index.html, sw.js e CI; versão '+v);
