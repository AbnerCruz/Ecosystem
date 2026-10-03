// UC-7: prova de classificação pelo CLI real; não usa réplica do matcher.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
const root=path.resolve(new URL('../..',import.meta.url).pathname);
const dotnet=process.env.URBE_DOTNET||'dotnet';
const proposed=JSON.parse(fs.readFileSync(path.join(root,'docs/governance/integration-policy.json'),'utf8'));
const baseRun=spawnSync('git',['show','origin/main:docs/governance/integration-policy.json'],{cwd:root,encoding:'utf8'});
assert.equal(baseRun.status,0);const before=JSON.parse(baseRun.stdout);
for(const key of ['routine','critical','authorization'])assert.deepEqual(proposed[key],before[key]);
for(const c of before.classes) {
  const n=proposed.classes.find(x=>x.id===c.id);assert(n);
  for(const p of c.paths)assert(n.paths.includes(p),`proteção removida: ${p}`);
  for(const r of c.rules)assert(n.rules.some(x=>JSON.stringify(x)===JSON.stringify(r)));
}
const cases=[
 ['csharp/Core/Persistence/Writer.cs','user-data'],['csharp/Persistence/Reader.cs','user-data'],
 ['csharp/Core/Storage/Files.cs','user-data'],['csharp/Core/Migration/Run.cs','user-data'],
 ['csharp/Core/Backup/Restore.cs','user-data'],['csharp/Core/Identity/Reconcile.cs','user-data'],
 ['csharp/Core/DocumentStore.cs','user-data'],['csharp/Core/VaultReader.cs','user-data'],
 ['csharp/Core/Security/Guard.cs','security'],['csharp/Core/Credentials/Keys.cs','security'],
 ['csharp/Core/Plugins/Loader.cs','security'],['csharp/Host/Interop/Bridge.cs','security'],
 ['csharp/Host/Native/Files.cs','security'],['csharp/Core/Core.csproj','distribution'],
 ['csharp/Directory.Build.props','distribution'],['csharp/Build.targets','distribution'],
 ['csharp/Host/Platforms/Android/AndroidManifest.xml','distribution'],
 ['csharp/Host/Updates/Updater.cs','distribution'],['csharp/Packaging/release.yml','distribution'],
 ['csharp/Distribution/channel.json','distribution'],['src/persistence/backup.js','user-data'],
 ['src/customize/plugins.js','security'],['native/desktop/guards.js','security'],
 ['csharp/UI/EditorView.cs',null],['docs/csharp/DEPENDENCIES.md',null]
];
const temp=fs.mkdtempSync(path.join(os.tmpdir(),'urbe-policy-'));
try {
  const changed=path.join(temp,'files.txt');
  for(const [relative,cl] of cases) {
    fs.writeFileSync(changed,`apps/urbe/${relative}\n`);
    const r=spawnSync(dotnet,['run',path.join(root,'tests/consistency/Check.cs'),'--','--integration-gates','--base-root',root,'--combined-root',root,'--files',changed],{cwd:root,encoding:'utf8',timeout:60000});
    assert.equal(r.status,0,r.stderr);const lines=Object.fromEntries(r.stdout.trim().split('\n').filter(x=>x.includes('=')).map(x=>{const i=x.indexOf('=');return [x.slice(0,i),x.slice(i+1)]}));
    assert.equal(lines.criticality,cl?'critical':'routine',relative);
    assert.equal(lines.requires_owner,cl?'true':'false',relative);
    if(cl)assert(JSON.parse(lines.critical_classes).includes(cl),relative);
  }
  console.log(`UC-7: ${cases.length} classificações reais; proteções e autorizadores preservados.`);
} finally {fs.rmSync(temp,{recursive:true,force:true})}
