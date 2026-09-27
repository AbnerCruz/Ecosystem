// Gera src/tutorial/content.js a partir das notas em tutorial/ e confere a integridade:
// todo [[Tutorial/...]] aponta para uma nota que existe, nenhuma tag acidental,
// e a página de exemplo passa na validação do motor de páginas.
//   node tools/build-tutorial.mjs          → gera o arquivo
//   node tools/build-tutorial.mjs --check  → só confere (usado no CI)
import fs from 'node:fs';import path from 'node:path';import vm from 'node:vm';import crypto from 'node:crypto';
const root=new URL('..',import.meta.url).pathname,src=path.join(root,'tutorial'),out=path.join(root,'src/tutorial/content.js');
function walk(d){return fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)])}
const files={};for(const f of walk(src).sort()){const rel='Tutorial/'+path.relative(src,f).split(path.sep).join('/');files[rel]=fs.readFileSync(f,'utf8').replace(/\r\n?/g,'\n')}

/* conferências */
const ctx={window:{},console};vm.createContext(ctx);
for(const f of ['src/core/core.js','src/core/documents.js','src/pages/engine.js'])vm.runInContext(fs.readFileSync(path.join(root,f),'utf8'),ctx);
const M=ctx.window.UrbeDocumentModel,P=ctx.window.UrbePages,problems=[];
const paths=new Set(Object.keys(files).map(p=>p.toLowerCase()));
for(const [p,c] of Object.entries(files)){
  if(!p.endsWith('.md'))continue;
  for(const l of M.parseLinks(c)){const alvo=l.split('|')[0].split('#')[0].trim();if(!alvo.startsWith('Tutorial/'))problems.push(p+': link fora do Tutorial [['+l+']] (use o caminho completo)');
    else if(!paths.has(alvo.toLowerCase())&&!paths.has((alvo+'.md').toLowerCase()))problems.push(p+': link quebrado [['+l+']]')}
  const tags=M.parseTags(c,M.parseFrontmatter(c));if(tags.length)problems.push(p+': tags acidentais '+tags.join(', ')+' (ponha exemplos de tag entre crases)');
  if(!/^# /.test(c))problems.push(p+': a nota deve começar com um título "# "');
}
for(const [p,c] of Object.entries(files))if(p.endsWith('.page.json')){const n=P.normalize(c);if(n.errors.length)problems.push(p+': '+n.errors.map(e=>e.path+' '+e.message).join('; '))}
const inicio='Tutorial/Comece aqui.md';if(!files[inicio])problems.push('falta '+inicio);
/* toda nota é alcançável a partir de "Comece aqui" */
const alcance=new Set([inicio.toLowerCase()]),fila=[inicio];
while(fila.length){const p=fila.shift(),c=files[p]||'';for(const l of M.parseLinks(c)){const a=l.split('|')[0].trim(),k=[a,a+'.md'].map(x=>x.toLowerCase()).find(x=>paths.has(x));if(k&&!alcance.has(k)){alcance.add(k);fila.push(Object.keys(files).find(x=>x.toLowerCase()===k))}}}
for(const p of Object.keys(files))if(!alcance.has(p.toLowerCase()))problems.push(p+': ninguém chega aqui a partir de "Comece aqui"');

if(problems.length){console.error('Tutorial com problemas:\n- '+problems.join('\n- '));process.exit(1)}
const version=crypto.createHash('sha256').update(JSON.stringify(files)).digest('hex').slice(0,12);
const body='/* Gerado por tools/build-tutorial.mjs a partir de tutorial/. Não edite à mão. */\n(function(global){global.UrbeTutorialContent='+JSON.stringify({version,files},null,1)+'})(typeof window!==\'undefined\'?window:globalThis);\n';
if(process.argv.includes('--check')){
  const atual=fs.existsSync(out)?fs.readFileSync(out,'utf8'):'';
  if(atual!==body){console.error('src/tutorial/content.js está desatualizado: rode node tools/build-tutorial.mjs');process.exit(1)}
  console.log('OK   tutorial: '+Object.keys(files).length+' arquivos, links e tags conferidos (versão '+version+')');
}else{fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,body);console.log('tutorial: '+Object.keys(files).length+' arquivos → src/tutorial/content.js ('+(body.length/1024).toFixed(1)+' KB, versão '+version+')')}
