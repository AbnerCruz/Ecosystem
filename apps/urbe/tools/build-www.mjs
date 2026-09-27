// Copia o app (o mesmo do site) para native/www, a pasta que o Capacitor empacota no Android.
// Não há etapa de build: são os mesmos arquivos, sem o service worker (o app instalado
// se atualiza pelo APK novo, não pelo cache do navegador).
import fs from 'node:fs';import path from 'node:path';
const root=path.resolve(path.dirname(new URL(import.meta.url).pathname),'..');
const out=path.join(root,'native','www');
fs.rmSync(out,{recursive:true,force:true});fs.mkdirSync(out,{recursive:true});
const items=['index.html','manifest.webmanifest','icon-192.png','icon-512.png','icon-maskable-512.png','apple-touch-icon.png','src','vendor'];
for(const it of items){const from=path.join(root,it);if(!fs.existsSync(from))throw new Error('faltou '+it);fs.cpSync(from,path.join(out,it),{recursive:true})}
let n=0;(function walk(d){for(const e of fs.readdirSync(d,{withFileTypes:true})){if(e.isDirectory())walk(path.join(d,e.name));else n++}})(out);
console.log('native/www: '+n+' arquivos');
