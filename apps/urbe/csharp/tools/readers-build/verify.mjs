import fs from 'node:fs/promises';import path from 'node:path';import {fileURLToPath} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../src/Urbe.Client/Resources/Readers');
const manifest=JSON.parse(await fs.readFile(path.join(root,'provenance.json'),'utf8'));
for(const item of manifest.files){const data=await fs.readFile(path.join(root,item.path));if(createHash('sha256').update(data).digest('hex')!==item.sha256)throw Error('Changed reader resource: '+item.path)}
console.log('Verified '+manifest.files.length+' bundled reader resources');
