// Cliente JS unificado: calcula cada família; DOM/IndexedDB reais apenas em Chromium.
import fs from 'node:fs';
import vm from 'node:vm';
import {read} from './lib/csharp-parity.mjs';
import {runVaultCase,runRestoreCase,runCrashCase} from './lib/parity-vault.mjs';
import {runStorageCase,runLegacyIdbCase} from './lib/parity-storage.mjs';
import {runDomainCase} from './lib/parity-domain.mjs';
import {runNativeCase} from './lib/parity-native.mjs';
import {runUpdateCase} from './lib/parity-update.mjs';
import {createBrowserReference} from './lib/parity-browser.mjs';
const req=JSON.parse(fs.readFileSync(0,'utf8'));
if(req.schemaVersion!==1||!Array.isArray(req.cases))throw new Error('protocolo inválido');
const ctx={window:{},console};ctx.window.window=ctx.window;vm.createContext(ctx);
for(const p of ['src/math/core.js','src/editor/markdown.js'])vm.runInContext(read(p),ctx);
const operations={'vault.scenario':runVaultCase,'vault.restore':runRestoreCase,'vault.crash-recovery':runCrashCase,'storage.scenario':runStorageCase,'idb.legacy-city':runLegacyIdbCase,'native.scenario':runNativeCase,'update.scenario':runUpdateCase};
let browser;const results=[];
try{
  for(const c of req.cases){
    let output;
    if(c.operation==='markdown.render')output={html:ctx.window.UrbeMarkdown.render(c.input.markdown)};
    else if(['visual.serialize','ui.scenario','browser.vault','browser.idb-legacy','browser.storage','browser.world','browser.zip'].includes(c.operation)){
      browser ||= await createBrowserReference();output=await browser[{'ui.scenario':'ui','visual.serialize':'visual','browser.vault':'vault','browser.idb-legacy':'legacy','browser.storage':'storage','browser.world':'world','browser.zip':'zip'}[c.operation]](c.input);
    }else if(operations[c.operation])output=await operations[c.operation](c.input);
    else output=await runDomainCase(c.operation,c.input);
    results.push({id:c.id,output});
  }
}finally{if(browser)await browser.close()}
// As cascas nativas simuladas não têm app.quit para encerrar os timers do main.
process.stdout.write(JSON.stringify({schemaVersion:1,results}),()=>process.exit(0));
