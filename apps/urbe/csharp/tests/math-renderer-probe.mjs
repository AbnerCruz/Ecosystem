// UC-16 discovery only. Real candidate packages are restored in a temporary directory;
// no package reference is added to Urbe.Core, UI, Web or native hosts.
import assert from 'node:assert/strict';
import vm from 'node:vm';
import {mkdtempSync,readFileSync,writeFileSync,mkdirSync,copyFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import {tmpdir} from 'node:os';
import {join,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {loadPlaywright} from '../../tools/lib/playwright.mjs';
import {startServer} from '../../tools/lib/static-server.mjs';
const here=dirname(fileURLToPath(import.meta.url)),sources=join(here,'probes/math-renderer'),root=mkdtempSync(join(tmpdir(),'urbe-math-renderer-'));
const packages={'CSharpMath':'1.0.0-pre.2','CSharpMath.Rendering':'1.0.0-pre.2','CSharpMath.VectSharp':'1.0.0-pre.2','VectSharp':'2.6.1','VectSharp.SVG':'1.10.0','ExCSS':'4.2.5'};
const references=Object.entries(packages).map(([id,version])=>`<PackageReference Include="${id}" Version="[${version}]"/>`).join('');
function run(args){const result=spawnSync(process.env.URBE_DOTNET_EXE||'dotnet',args,{cwd:root,encoding:'utf8',timeout:300000,maxBuffer:8*1024*1024});if(result.status!==0)throw Error(result.error?.message||result.stdout+'\n'+result.stderr);return result.stdout}
writeFileSync(join(root,'MathProbe.csproj'),`<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup>${references}</ItemGroup></Project>`);
copyFileSync(join(sources,'Console.cs'),join(root,'Program.cs'));
const fixture=join(here,'fixtures/math-editing.json'),resultFile=join(root,'results.json');
const ctx={window:{}};ctx.window.window=ctx.window;vm.createContext(ctx);vm.runInContext(readFileSync(join(here,'../../src/math/core.js'),'utf8'),ctx);
const corpus=[];for(const name of ['Fórmulas em qualquer nota.md','Galeria de fórmulas.md']){const source='tutorial/Matemática/'+name,bytes=readFileSync(join(here,'../../',source));for(const formula of ctx.window.UrbeMath.scan(bytes.toString('utf8')))corpus.push({source,sourceSha256:createHash('sha256').update(bytes).digest('hex'),tex:formula.tex})}
const corpusFile=join(root,'real-corpus.json');writeFileSync(corpusFile,JSON.stringify(corpus));
const consoleLog=run(['run','--project',join(root,'MathProbe.csproj'),'-c','Release','--',fixture,resultFile,corpusFile]);writeFileSync(join(root,'console.log'),consoleLog);
const results=JSON.parse(readFileSync(resultFile)),counts={};for(const result of results){counts[result.status]=(counts[result.status]||0)+1;if(result.external)assert.deepEqual(result.external,[])}
assert.equal(counts.exception||0,0);assert.equal(results.length,179+corpus.length);assert.ok(results.some(r=>r.status==='rendered'&&r.paths>0));
const report={packages,fixtureSha256:createHash('sha256').update(readFileSync(fixture)).digest('hex'),inputs:results.length,realCorpus:corpus,realCorpusCounts:results.slice(179).reduce((a,r)=>(a[r.status]=(a[r.status]||0)+1,a),{}),counts,unsupportedSnippets:[...new Set(results.slice(0,172).filter(r=>r.status==='diagnostic').map(r=>r.tex))],web:'not-executed',native:'not-executed'};
if(!process.argv.includes('--console-only')){
 const web=join(root,'web');mkdirSync(join(web,'wwwroot'),{recursive:true});
 writeFileSync(join(web,'MathWeb.csproj'),`<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="[10.0.0]"/>${references}</ItemGroup></Project>`);
 for(const [src,dest] of [['App.razor','App.razor'],['WebProgram.cs','Program.cs'],['index.html','wwwroot/index.html']])copyFileSync(join(sources,src),join(web,dest));
 const publish=join(web,'publish');writeFileSync(join(root,'web-build.log'),run(['publish',join(web,'MathWeb.csproj'),'-c','Release','-o',publish]));
 const {chromium}=await loadPlaywright(),server=await startServer({root:join(publish,'wwwroot')}),browser=await chromium.launch();
 try{const context=await browser.newContext(),page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(server.url);await page.waitForFunction(()=>document.querySelector('#status')?.textContent==='rendered:1',{timeout:90000});assert.ok(await page.locator('#formula svg path').count()>0);await context.setOffline(true);await page.click('#render');await page.waitForFunction(()=>document.querySelector('#status').textContent==='rendered:2');assert.equal(await page.locator('#formula svg text').count(),0);assert.deepEqual(errors,[]);report.web='actual-wasm-render-and-offline-rerender-passed';await page.screenshot({path:join(root,'offline.png')});await context.close()}finally{await browser.close();await server.close()}
}
writeFileSync(join(root,'report.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({root,...report},null,2));
