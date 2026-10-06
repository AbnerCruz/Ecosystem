// UC-2: export/import ZIP como resultado observável portável; usa o app real em Chromium.
import {mkdtempSync,readFileSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {encodedFixture} from './parity-browser-vault.mjs';
import {launchApp,openApp,waitSaved} from './browser.mjs';

const decode=files=>new Map(files.map(f=>[f.path,f.encoding==='base64'?new Uint8Array(Buffer.from(f.content,'base64')):f.content]));

export function makeBrowserZipCases(){
  return [{
    id:'browser-zip-roundtrip',
    operation:'browser.zip',
    requirements:['REQ-044','REQ-058','REQ-061'],
    source:'tests/e2e/zip.e2e.mjs',
    input:{
      files:encodedFixture('v1-mapa-v4'),
      docs:3,
      preference:{key:'urbe.tip.parity',value:'1'},
      secret:{key:'urbe.ai.apiKey',value:'sk-PARITY-NOT-A-REAL-SECRET'}
    },
    expected:{
      zipFilename:true,
      notesIncluded:true,
      mapIncluded:true,
      journalExcluded:true,
      assetIncluded:true,
      manifestValid:true,
      manifestCoversAll:true,
      preferenceExported:true,
      secretExcluded:true,
      importedContentEqual:true,
      preferenceImported:true,
      secretNotImported:true,
      tamperedWarned:true,
      tamperedCancelled:true,
      futureWarned:true,
      futureRefused:true
    }
  }];
}

async function importZip(h,file){
  await h.page.evaluate(()=>{delete window.showDirectoryPicker;delete window.showOpenFilePicker});
  await h.page.evaluate(()=>{UrbeCore.service('legacy.runtime').importFiles(null)});
  const [chooser]=await Promise.all([
    h.page.waitForEvent('filechooser'),
    h.page.locator('#explorerSheetActions').getByRole('button',{name:/^Arquivos/}).click()
  ]);
  await chooser.setFiles(file);
}

export async function runBrowserZip(input){
  if(!Array.isArray(input.files)||!Number.isInteger(input.docs)||!input.preference||!input.secret)throw new Error('cenário ZIP inválido');
  const app=await launchApp();
  try{
    const seed=decode(input.files),a=await app.newPage({seed});
    await openApp(a,{docs:input.docs});
    await a.page.evaluate(([pref,secret])=>{
      localStorage.setItem(pref.key,pref.value);
      localStorage.setItem(secret.key,secret.value);
      return UrbeAIStore.setConfig({provider:'openai',apiKey:secret.value});
    },[input.preference,input.secret]);
    await waitSaved(a.page);
    await a.page.evaluate(()=>UrbeCore.commands.execute('workspace.save'));

    const [download]=await Promise.all([
      a.page.waitForEvent('download',{timeout:30000}),
      a.page.evaluate(()=>UrbeCore.service('legacy.runtime').exportZip())
    ]);
    const file=join(mkdtempSync(join(tmpdir(),'urbe-parity-zip-')),download.suggestedFilename());
    await download.saveAs(file);
    const bytes=readFileSync(file);
    const inspected=await a.page.evaluate(async bytes=>{
      const z=await JSZip.loadAsync(new Uint8Array(bytes)),names=Object.keys(z.files).filter(n=>!z.files[n].dir);
      const parsed=UrbeExportManifest.parse(await z.file('urbe-export.json').async('string')),all=new Map();
      for(const n of names)if(n!=='urbe-export.json')all.set(n,await z.file(n).async('uint8array'));
      const check=await UrbeExportManifest.verify(parsed.manifest,all);
      const markdown={};
      for(const n of names)if(n.endsWith('.md')&&!n.startsWith('.'))markdown[n.split('/').pop()]=await z.file(n).async('string');
      return{names,manifest:parsed.manifest,check,markdown};
    },[...bytes]);
    const state=JSON.stringify(inspected.manifest.state||{});
    const exported={
      zipFilename:/\.zip$/i.test(download.suggestedFilename()),
      notesIncluded:inspected.names.filter(n=>n.endsWith('.md')).length>=input.docs,
      mapIncluded:inspected.names.includes('.urbe/mapa.json'),
      journalExcluded:!inspected.names.includes('.urbe/journal.json'),
      assetIncluded:inspected.names.some(n=>/\.png$/i.test(n)),
      manifestValid:inspected.check.ok===true,
      manifestCoversAll:inspected.manifest.files.length===inspected.names.length-1,
      preferenceExported:inspected.manifest.state?.localStorage?.[input.preference.key]===input.preference.value,
      secretExcluded:!bytes.toString('latin1').includes(input.secret.value)&&!state.includes(input.secret.value)&&!state.includes(input.secret.key)
    };
    if(a.errors.length)throw new Error('erros no export: '+a.errors.join(' | '));

    const b=await app.newPage();
    await openApp(b,{docs:1});
    await importZip(b,file);
    await b.page.waitForSelector('.udlg [data-primary]');
    await b.page.click('.udlg [data-primary]');
    await b.page.waitForFunction(()=>document.querySelector('.udlg-msg')?.textContent.includes('substituirá'));
    await b.page.click('.udlg [data-primary]');
    await b.page.waitForFunction(()=>document.querySelector('.udlg-msg')?.textContent.includes('preferências deste backup'));
    const prefDialog=/preferências/i.test(await b.page.textContent('.udlg-msg'));
    await b.page.click('.udlg [data-primary]');
    await b.page.waitForFunction(n=>UrbeCore.service('documents').list().filter(d=>d.path.endsWith('.md')&&!d.path.startsWith('Tutorial/')).length>=n,input.docs,{timeout:30000});
    await waitSaved(b.page);
    const imported=Object.fromEntries(await b.page.evaluate(()=>UrbeCore.service('documents').list().filter(d=>d.path.endsWith('.md')&&!d.path.startsWith('Tutorial/')).map(d=>[d.path.split('/').pop(),d.content])));
    const importedContentEqual=prefDialog&&Object.entries(inspected.markdown).every(([name,body])=>imported[name]===body);
    const preferenceImported=await b.page.evaluate(key=>localStorage.getItem(key),input.preference.key)===input.preference.value;
    const secretNotImported=await b.page.evaluate(key=>localStorage.getItem(key),input.secret.key)===null;
    if(b.errors.length)throw new Error('erros no import: '+b.errors.join(' | '));

    const tampered=file.replace(/\.zip$/i,'-adulterado.zip');
    const tamperedBytes=await a.page.evaluate(async bytes=>{
      const z=await JSZip.loadAsync(new Uint8Array(bytes)),name=Object.keys(z.files).find(n=>n.endsWith('.md')&&!n.startsWith('.'));
      z.file(name,'ADULTERADO');
      return [...await z.generateAsync({type:'uint8array'})];
    },[...bytes]);
    writeFileSync(tampered,Buffer.from(tamperedBytes));
    const c=await app.newPage();
    await openApp(c,{docs:1});
    const before=await c.page.evaluate(()=>UrbeCore.service('documents').list().length);
    await importZip(c,tampered);
    await c.page.waitForSelector('.udlg [data-primary]');
    const tamperedWarned=/não conferem/i.test(await c.page.textContent('.udlg-msg'));
    await c.page.click('.udlg [data-primary]');
    await c.page.waitForTimeout(500);
    const tamperedCancelled=await c.page.evaluate(()=>UrbeCore.service('documents').list().length)===before;

    const future=file.replace(/\.zip$/i,'-futuro.zip');
    const futureBytes=await a.page.evaluate(async()=>{
      const z=new JSZip();
      z.file('Nota futura.md','# x\n');
      z.file('urbe-export.json',JSON.stringify({format:'urbe-export',formatVersion:9,files:[]}));
      return [...await z.generateAsync({type:'uint8array'})];
    });
    writeFileSync(future,Buffer.from(futureBytes));
    await importZip(c,future);
    await c.page.waitForSelector('.udlg [data-primary]');
    const futureWarned=/versão mais recente/i.test(await c.page.textContent('.udlg-msg'));
    await c.page.click('.udlg [data-primary]');
    await c.page.waitForTimeout(300);
    const futureRefused=await c.page.evaluate(()=>!UrbeCore.service('documents').list().some(d=>d.path.endsWith('Nota futura.md')));
    if(c.errors.length)throw new Error('erros nas recusas ZIP: '+c.errors.join(' | '));

    return{...exported,importedContentEqual,preferenceImported,secretNotImported,tamperedWarned,tamperedCancelled,futureWarned,futureRefused};
  }finally{await app.close()}
}
