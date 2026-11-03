// Run after ReaderEngineTests exports HTML via URBE_READER_ENGINE_TEST_DIR.
// No external HTTP request is permitted in these local-content engine tests.
import { createRequire } from 'node:module';
import fs from 'node:fs/promises';
import http from 'node:http';
import assert from 'node:assert/strict';
const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = process.env.URBE_READER_ENGINE_TEST_DIR;
assert.ok(root, 'URBE_READER_ENGINE_TEST_DIR must name the exported test documents');
const server = http.createServer(async (req, res) => {
  if (!['/ocr.html', '/slides.html', '/book.html'].includes(req.url)) {res.writeHead(404).end();return;}
  res.setHeader('Content-Type', 'text/html; charset=utf-8');res.end(await fs.readFile(root+req.url));
});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const origin='http://127.0.0.1:'+server.address().port;
const browser=await chromium.launch({executablePath:process.env.URBE_CHROMIUM_PATH,args:['--no-sandbox']});
try {
 const context=await browser.newContext({viewport:{width:1200,height:800}});
 const unexpected=[];
 await context.route('**/*',async route=>{const u=route.request().url();if(u.startsWith(origin+'/')||u.startsWith('blob:')||u.startsWith('data:'))await route.continue();else{unexpected.push(u);await route.abort();}});
 const page=await context.newPage();
 page.on('pageerror',e=>process.stderr.write(e.message+'\n'));
 await page.goto(origin+'/slides.html');
 await page.locator('#slide > svg').waitFor({timeout:60000});
 const text=await page.locator('#slide').textContent();
 assert.ok(text.includes('Estudo de apresentações'));assert.ok(text.includes('Forma e texto preservados'));assert.ok(text.includes('42'));
 assert.ok(await page.locator('#slide svg image').count()>0,'slide image must be rendered');
 await page.screenshot({path:root+'/slides.png'});
 await page.evaluate(()=>window.urbeRenderSlide(1));
 await page.waitForFunction(()=>document.querySelector('#slide').textContent.includes('Segundo slide: trecho exato.'));
 console.log('PPTX: text, shape, image, table and second-slide navigation verified');
 const locator=JSON.parse(await fs.readFile(root+'/locator.json','utf8'));
 await page.goto(origin+'/book.html');
 const expected=await page.evaluate(()=>{
   const p=[...document.querySelectorAll('p')].find(p=>p.textContent.startsWith('Parágrafo 20.'));
   const range=document.createRange();range.selectNodeContents(p);const selection=window.getSelection();selection.removeAllRanges();selection.addRange(range);p.scrollIntoView();return p.textContent;
 });
 const position=JSON.parse(await page.evaluate(script=>eval(script),locator.read));
 assert.equal(position.Exact,expected);assert.ok(position.Offset>1000);
 await page.evaluate(()=>{document.body.style.fontSize='32px';document.body.style.lineHeight='2';window.getSelection().removeAllRanges();window.scrollTo(0,0)});
 const restore=locator.restore.replace('let at=0,len=0;',`let at=${position.Offset},len=${position.Length};`);
 await page.evaluate(script=>eval(script),restore);
 assert.equal(await page.evaluate(()=>window.getSelection().toString()),expected);
 assert.ok(await page.evaluate(()=>window.scrollY)>0);
 console.log('EPUB: exact text locator survives typography changes and restores selection');
 await page.goto(origin+'/ocr.html');
 await page.waitForFunction(()=>['done','error'].includes(window.urbeOcrResult?.status),{},{timeout:120000});
 const ocr=await page.evaluate(()=>window.urbeOcrResult);
 assert.equal(ocr.status,'done',JSON.stringify(ocr));assert.match(ocr.text,/Estudo local/);assert.match(ocr.text,/2026/);assert.ok(ocr.words.length>5);
 await fs.writeFile(root+'/ocr-result.json',JSON.stringify(ocr,null,2));
 await page.screenshot({path:root+'/ocr.png'});
 assert.deepEqual(unexpected,[],'local document engines must make no external request');
 console.log('OCR: bundled Portuguese/English WASM, text and word boxes verified offline; confidence '+ocr.confidence);
} finally {await browser.close();server.close();}
