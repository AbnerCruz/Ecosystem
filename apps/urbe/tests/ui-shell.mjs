import fs from 'node:fs';
const read=p=>fs.readFileSync(new URL('../'+p,import.meta.url),'utf8');
const index=read('index.html'),sw=read('sw.js'),theme=read('src/styles/theme.css'),base=read('src/styles/base.css');
const ui=['src/app.js','src/explorer/mobile-ui.js','src/composition/ui.js','src/ui/quick-open.js','src/ui/tips.js','src/editor/visual-tools.js'].map(f=>[f,read(f)]);
function test(name,fn){try{fn();console.log('OK  ',name)}catch(e){console.error('FAIL',name,e.message);process.exitCode=1}}

test('interface sem prompt/confirm/alert nativos',()=>{
  for(const [f,src] of ui){const m=src.match(/(?<![\w.])(?:global\.|window\.|g\.)?(prompt|confirm|alert)\(/);if(m)throw new Error(f+' usa '+m[1]+'()')}
});
test('ícones e diálogos carregam antes do app; tema por último',()=>{
  const at=s=>index.indexOf(s);
  if(!(at('./src/ui/icons.js')>0&&at('./src/ui/icons.js')<at('./src/ui/dialogs.js')&&at('./src/ui/dialogs.js')<at('./src/app.js')))throw new Error('ordem de scripts');
  const links=[...index.matchAll(/<link rel="stylesheet" href="([^"]+)"/g)].map(m=>m[1]);
  if(links[links.length-1]!=='./src/styles/theme.css')throw new Error('theme.css precisa ser o último estilo');
});
test('cache offline inclui a nova casca',()=>{
  for(const f of ['./src/styles/theme.css','./src/ui/icons.js','./src/ui/dialogs.js','./src/editor/visual-tools.js','./src/ui/tips.js','./src/world/terrain.js','./src/world/pixel-art.js','./src/world/chunk-worker.js'])if(!sw.includes("'"+f+"'"))throw new Error('sw.js sem '+f);
});
test('sem cantos retos forçados globalmente',()=>{
  if(/button,input,textarea,select\{border-radius:0!important\}/.test(base))throw new Error('regra global de border-radius:0 voltou');
});
test('tema define tokens e redireciona os legados',()=>{
  for(const t of ['--ui-bg','--ui-accent','--ui-font','--v23-accent:var(--ui-accent)'])if(!theme.includes(t))throw new Error('token ausente: '+t);
});
test('diálogos: API assíncrona completa',()=>{
  const d=read('src/ui/dialogs.js');
  for(const k of ['prompt:prompt','confirm:confirm','alert:alert','choose:choose','menu:menu'])if(!d.includes(k))throw new Error('UrbeDialogs sem '+k);
  if(!d.includes("aria-modal=\"true\""))throw new Error('diálogo sem aria-modal');
});
