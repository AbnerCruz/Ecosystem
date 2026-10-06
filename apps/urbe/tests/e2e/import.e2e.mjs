// REQ-110: production UI, real browser file selection and persistence at mobile size.
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,writeFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {launchApp,openApp,waitSaved} from '../../tools/lib/browser.mjs';
const root=mkdtempSync(join(tmpdir(),'urbe-mobile-import-')),app=await launchApp();
try {
  writeFileSync(join(root,'ação.md'),'# Ação\n![imagem](imagem.png)');
  writeFileSync(join(root,'segunda.md'),'# Segunda');
  writeFileSync(join(root,'imagem.png'),Buffer.from([137,80,78,71,13,10,26,10,0,255]));
  mkdirSync(join(root,'pasta'));writeFileSync(join(root,'pasta','terceira.md'),'# Pasta inteira');
  const h=await app.newPage({mobile:true});await openApp(h,{docs:1});
  await h.page.evaluate(()=>{delete window.showDirectoryPicker;delete window.showOpenFilePicker});
  async function select(label,files) {
    await h.page.evaluate(()=>{UrbeCore.service('legacy.runtime').importFiles(null)});
    const actions=h.page.locator('#explorerSheetActions');
    await actions.getByRole('button',{name:/^Arquivos/}).waitFor();
    assert.equal(await actions.getByRole('button').count(),2);
    const [chooser]=await Promise.all([h.page.waitForEvent('filechooser'),actions.getByRole('button',{name:label}).click()]);
    await chooser.setFiles(files);try{await h.page.waitForSelector('.udlg [data-primary]')}catch(error){console.log(await h.page.evaluate(()=>({toast:document.getElementById('toast').textContent,files:[...document.getElementById('vaultFolderIn').files].map(f=>({name:f.name,path:f.webkitRelativePath})),dialog:document.querySelector('.udlg')?.textContent})));throw error}
  }
  await select(/^Arquivos/,[join(root,'ação.md'),join(root,'segunda.md'),join(root,'imagem.png')]);
  assert.match(await h.page.textContent('.udlg-msg'),/2 documento\(s\) e 1 arquivo\(s\)/);
  await h.page.click('.udlg [data-primary]');
  await h.page.waitForFunction(()=>UrbeCore.service('documents').get('ação.md')?.content.includes('![imagem]'));
  await waitSaved(h.page);await h.page.waitForFunction(()=>document.getElementById('toast').textContent.includes('Importação concluída'));
  assert.equal(await h.page.evaluate(()=>UrbeCore.service('documents').get('segunda.md').content),'# Segunda');
  await select(/^Pasta inteira/,join(root,'pasta'));
  assert.match(await h.page.textContent('.udlg-msg'),/incorporados ao vault atual/);
  await h.page.click('.udlg [data-primary]');
  await h.page.waitForFunction(()=>UrbeCore.service('documents').get('terceira.md')?.content==='# Pasta inteira');
  await waitSaved(h.page);await h.page.reload();
  await h.page.waitForFunction(()=>UrbeCore.service('documents').get('terceira.md')?.content==='# Pasta inteira');
  assert.equal(await h.page.evaluate(()=>UrbeCore.service('documents').get('ação.md').content),'# Ação\n![imagem](imagem.png)');
  assert.deepEqual(h.errors,[]);
  console.log('import.e2e: mobile multiple files + folder + reopen passed');
} finally {await app.close();rmSync(root,{recursive:true,force:true})}
