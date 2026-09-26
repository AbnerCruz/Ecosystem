import fs from 'node:fs';import vm from 'node:vm';
const app=fs.readFileSync(new URL('../src/app.js',import.meta.url),'utf8');
function test(name,fn){try{fn();console.log('OK  ',name)}catch(e){console.error('FAIL',name,e.message);process.exitCode=1}}
function trecho(inicio,fim){const i=app.indexOf(inicio),j=app.indexOf(fim,i);if(i<0||j<0)throw new Error('trecho não encontrado: '+inicio);return app.slice(i,j)}

test('enquadramento: abre mostrando todas as casas quando cabem',()=>{
  const src=trecho('function urbeEnquadrarNotas(forcar){','(function urbeEnquadrarAoAbrir(){');
  const TILE=32,c={TILE,clamp:(v,a,b)=>Math.max(a,Math.min(b,v)),counts(){},pedirDesenho(){},cv:{w:412,h:915},camera:{x:0,y:0,z:1},
    world:{buildings:[{x:0,y:0,w:3,h:3},{x:14,y:20,w:3,h:3}]}};
  c.faixaVisivel=()=>{const hw=c.cv.w/2/c.camera.z/TILE,hh=c.cv.h/2/c.camera.z/TILE,cx=c.camera.x/TILE,cy=c.camera.y/TILE;return{x0:cx-hw,x1:cx+hw,y0:cy-hh,y1:cy+hh}};
  vm.createContext(c);vm.runInContext(src,c);
  c.camera={x:1.5*TILE,y:1.5*TILE,z:1};
  if(!vm.runInContext('urbeEnquadrarNotas(false)',c))throw new Error('deveria enquadrar: uma casa estava fora da tela');
  const f=c.faixaVisivel();for(const b of c.world.buildings)if(b.x<f.x0||b.x+b.w>f.x1||b.y<f.y0||b.y+b.h>f.y1)throw new Error('casa fora do enquadramento');
  if(vm.runInContext('urbeEnquadrarNotas(false)',c))throw new Error('não deveria mexer quando tudo já está à vista');
  c.world.buildings=[];if(vm.runInContext('urbeEnquadrarNotas(true)',c))throw new Error('cidade vazia não enquadra');
});
test('chão sem a faixa escura dos sprites (a “grade”)',()=>{
  if(!/limpar\('grass',38/.test(app)||!/limpar\('water',38/.test(app))throw new Error('grama/água sem recorte limpo');
});
test('toda nota vira casa, não só as restauradas da lixeira',()=>{
  const h=trecho("urbeCore.events.on('document:created',function(evt){","function urbeCasaParaDocumento(d){");
  if(h.includes("source!=='trash.restore'"))throw new Error('criação ainda restrita à lixeira');
  if(!h.includes('urbeBuildingPath(x,regioes)===d.path'))throw new Error('sem proteção contra casa duplicada');
});
test('ruas: refeitas ao abrir e quando links mudam fora do editor; desenho contínuo',()=>{
  if(!trecho('abrirCidade=async function(name){','/* An existing installation').includes('rebuildRoadNetwork();'))throw new Error('abrir não refaz ruas');
  if(!trecho("urbeCore.events.on('document:updated'",'marcarSinc();').includes('scheduleRoadRebuild()'))throw new Error('link alterado fora do editor não refaz ruas');
  const r=trecho('drawRoads=function(){','/* abre mostrando as notas');if(r.includes('A.road'))throw new Error('ruas voltaram ao sprite em blocos');
});
test('rótulos da cidade usam a fonte da interface',()=>{
  const b=trecho('drawBuildings=function(){\n  var f=faixaVisivel(),rotulos=[];','drawRegions=function(){');
  if(!b.includes('urbeRotulo('))throw new Error('rótulo antigo');
  if(!app.includes("core.commands.register('city.fit'"))throw new Error('comando Enquadrar ausente');
});
