// Install only reviewed tooling with npm ci --ignore-scripts. The upstream source is never executed as a build script.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
import { build } from 'esbuild';
const here=path.dirname(fileURLToPath(import.meta.url));
const source=process.env.URBE_WEB_PPT_SOURCE;
if(!source)throw Error('URBE_WEB_PPT_SOURCE must be a checkout of https://github.com/unstone/web-ppt');
const revision=execFileSync('git',['-C',source,'rev-parse','HEAD'],{encoding:'utf8'}).trim();
if(revision!=='88c8df92ee853fa2dc45a413fa38316527b20025')throw Error('Unreviewed Web-PPT revision: '+revision);
if(execFileSync('git',['-C',source,'status','--porcelain'],{encoding:'utf8'}).trim())throw Error('Web-PPT source must be clean');
const output=path.resolve(here,'../../src/Urbe.Readers.Assets/Readers');
await build({stdin:{contents:'import { parse, slideToSvgFile } from '+JSON.stringify(path.resolve(source,'packages/core/src/index.ts'))+';\nglobalThis.UrbePresentation = { parse, slideToSvgFile };',resolveDir:here,sourcefile:'entry.ts'},bundle:true,format:'iife',platform:'browser',target:'es2020',minify:true,legalComments:'inline',nodePaths:[path.join(here,'node_modules')],outfile:path.join(output,'presentation.js')});
const modules=path.join(here,'node_modules');
for(const [src,dest] of Object.entries({'tesseract.js/dist/tesseract.min.js':'tesseract.min.js','tesseract.js/dist/worker.min.js':'worker.min.js','tesseract.js-core/tesseract-core-lstm.wasm.js':'core.wasm.js','tesseract.js/LICENSE.md':'Tesseract-js-LICENSE.txt','tesseract.js-core/LICENSE':'Tesseract-core-LICENSE.txt','tesseract.js/dist/tesseract.min.js.LICENSE.txt':'tesseract-notices.txt','tesseract.js/dist/worker.min.js.LICENSE.txt':'worker-notices.txt'}))await fs.copyFile(path.join(modules,src),path.join(output,'Ocr',dest));
// Models are checked-in, pinned data, not downloaded at app runtime. verify.mjs covers their exact bytes.
console.log('Built local document engines; run node verify.mjs and browser/readers.mjs before updating provenance.');
